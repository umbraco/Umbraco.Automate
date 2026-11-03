using Examine;
using Examine.Lucene;
using Examine.Lucene.Directories;
using Examine.Lucene.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Extensions;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Search;
using Umbraco.Cms.Core.Search.Indexing;
using Umbraco.Cms.Search.Provider.Examine.Configuration;
using Umbraco.Cms.Search.Provider.Examine.Services;
using UmbracoConstants = Umbraco.Cms.Core.Constants;
using LuceneDirectory = Lucene.Net.Store.Directory;

namespace Umbraco.Automate.Tests.Integration.Search;

/// <summary>
/// Hosts the real Umbraco Search Examine provider (its <c>Indexer</c> and <c>Searcher</c>) over
/// in-memory Lucene indexes, wired the way the CMS's own provider tests wire it, so Find Content
/// and Find Media queries run against the provider's real field definitions and analyzers.
/// </summary>
/// <remarks>
/// The provider's registration helpers and Lucene field configuration are internal to the CMS
/// package, so the field configuration type is resolved by name. If a CMS release renames it
/// the host fails to build, which is the signal to re-check this wiring against the CMS tests.
/// </remarks>
public sealed class UmbracoSearchTestHost : IDisposable
{
    private const string FieldConfigurationType =
        "Umbraco.Cms.Search.Provider.Examine.Configuration.ConfigureIndexOptions";

    private static readonly string[] IndexAliases =
    [
        UmbracoConstants.Search.IndexAliases.PublishedContent,
        UmbracoConstants.Search.IndexAliases.DraftContent,
        UmbracoConstants.Search.IndexAliases.DraftMedia,
    ];

    private readonly ServiceProvider _services;
    private readonly IIndexer _indexer;

    public UmbracoSearchTestHost()
    {
        var configurationType = typeof(Searcher).Assembly.GetType(FieldConfigurationType, throwOnError: true)!;

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddExamine();
        services.ConfigureOptions(configurationType);
        services.AddSingleton<InMemoryDirectoryFactory>();
        foreach (var alias in IndexAliases)
        {
            services.AddExamineLuceneIndex<SettlingLuceneIndex, InMemoryDirectoryFactory>(alias, _ => { });
        }

        _services = services.BuildServiceProvider();

        var examineManager = _services.GetRequiredService<IExamineManager>();
        var activeIndexManager = new SingleIndexManager();
        _indexer = new Indexer(examineManager, Options.Create(new FieldOptions { Fields = [] }), activeIndexManager);
        Searcher = new Searcher(examineManager, Options.Create(new SearcherOptions()), activeIndexManager);
    }

    /// <summary>Gets the provider's searcher, serving every index alias.</summary>
    public Umbraco.Cms.Core.Search.ISearcher Searcher { get; }

    /// <summary>Indexes one item the way the CMS's system fields indexer describes it.</summary>
    /// <param name="indexAlias">The index to write to.</param>
    /// <param name="objectType">Document or media.</param>
    /// <param name="key">The item key.</param>
    /// <param name="contentTypeKey">The item's content or media type key.</param>
    /// <param name="names">The name per culture; a single entry with a null culture for invariant items.</param>
    /// <param name="ancestorKeys">The keys of the ancestors, outermost first. Include the recycle bin key for trashed items.</param>
    /// <param name="protectedFor">Member or group keys with access, or null for unprotected items.</param>
    public Task IndexAsync(
        string indexAlias,
        UmbracoObjectTypes objectType,
        Guid key,
        Guid contentTypeKey,
        IReadOnlyList<(string? Culture, string Name)> names,
        IReadOnlyList<Guid> ancestorKeys,
        IReadOnlyList<Guid>? protectedFor = null)
    {
        var fields = new List<IndexField>
        {
            new(UmbracoConstants.Search.FieldNames.Id, new IndexValue { Keywords = [key.AsKeyword()] }, null, null),
            new(
                UmbracoConstants.Search.FieldNames.PathIds,
                new IndexValue { Keywords = ancestorKeys.Append(key).Select(k => k.AsKeyword()).ToArray() },
                null,
                null),
            new(UmbracoConstants.Search.FieldNames.ContentTypeId, new IndexValue { Keywords = [contentTypeKey.AsKeyword()] }, null, null),
        };

        // Indexed both as analyzed text (free text search) and as a keyword, as the CMS does.
        fields.AddRange(names.Select(item => new IndexField(
            UmbracoConstants.Search.FieldNames.Name,
            new IndexValue { TextsR1 = [item.Name], Keywords = [item.Name] },
            item.Culture,
            null)));

        return _indexer.AddOrUpdateAsync(
            indexAlias,
            key,
            objectType,
            names.Select(item => new Variation(item.Culture, null)).ToArray(),
            fields,
            protectedFor is null ? null : new ContentProtection(protectedFor));
    }

    /// <summary>Waits until every index has processed and committed its queued writes.</summary>
    public async Task WaitUntilSettledAsync()
    {
        var examineManager = _services.GetRequiredService<IExamineManager>();
        var indexes = examineManager.Indexes.OfType<SettlingLuceneIndex>().ToList();

        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (indexes.Any(index => !index.IsSettled))
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("The Umbraco Search test indexes did not settle within 30 seconds.");
            }

            await Task.Delay(25);
        }
    }

    public void Dispose() => _services.Dispose();

    private sealed class SingleIndexManager : IActiveIndexManager
    {
        public string ResolveActiveIndexName(string indexAlias) => indexAlias;

        public string ResolveShadowIndexName(string indexAlias) => indexAlias;

        public bool IsRebuilding(string indexAlias) => false;

        public void StartRebuilding(string indexAlias)
        {
        }

        public void CompleteRebuilding(string indexAlias)
        {
        }

        public void CancelRebuilding(string indexAlias)
        {
        }
    }

    private sealed class InMemoryDirectoryFactory : IDirectoryFactory
    {
        public LuceneDirectory CreateDirectory(LuceneIndex luceneIndex, bool forceUnlock)
            => new RandomIdRamDirectory();
    }

    private sealed class RandomIdRamDirectory : Lucene.Net.Store.RAMDirectory
    {
        private readonly string _lockId = Guid.NewGuid().ToString();

        public override string GetLockID() => _lockId;
    }

    /// <summary>
    /// A Lucene index that reports when every queued write has been processed and committed.
    /// Examine writes on a background queue and commits on a debounce timer, so a searcher only
    /// reliably sees a write once both have happened.
    /// </summary>
    private sealed class SettlingLuceneIndex : LuceneIndex
    {
        private int _pendingOperations;
        private volatile bool _hasUncommittedWrites;

        public SettlingLuceneIndex(
            ILoggerFactory loggerFactory,
            string name,
            IOptionsMonitor<LuceneDirectoryIndexOptions> indexOptions)
            : base(loggerFactory, name, indexOptions)
            => IndexCommitted += OnIndexCommitted;

        public bool IsSettled => Volatile.Read(ref _pendingOperations) == 0 && !_hasUncommittedWrites;

        protected override void PerformIndexItems(IEnumerable<ValueSet> values, Action<IndexOperationEventArgs>? onComplete)
        {
            BeginOperation();
            base.PerformIndexItems(values, TrackCompletion(onComplete));
        }

        protected override void PerformDeleteFromIndex(IEnumerable<string> itemIds, Action<IndexOperationEventArgs>? onComplete)
        {
            BeginOperation();
            base.PerformDeleteFromIndex(itemIds, TrackCompletion(onComplete));
        }

        private void BeginOperation()
        {
            _hasUncommittedWrites = true;
            Interlocked.Increment(ref _pendingOperations);
        }

        private Action<IndexOperationEventArgs> TrackCompletion(Action<IndexOperationEventArgs>? onComplete)
            => args =>
            {
                try
                {
                    onComplete?.Invoke(args);
                }
                finally
                {
                    Interlocked.Decrement(ref _pendingOperations);
                }
            };

        // A commit that lands while an operation is still in flight does not cover it.
        private void OnIndexCommitted(object? sender, EventArgs e)
        {
            if (Volatile.Read(ref _pendingOperations) == 0)
            {
                _hasUncommittedWrites = false;
            }
        }
    }
}
