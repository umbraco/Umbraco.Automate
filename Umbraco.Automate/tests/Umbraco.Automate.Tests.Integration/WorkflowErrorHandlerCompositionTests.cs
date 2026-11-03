using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Extensions;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Logging;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace Umbraco.Automate.Tests.Integration;

/// <summary>
/// Composition guard for <see cref="AutomateRetryHandler"/>. WorkflowCore runs every registered error
/// handler whose type matches the failing step's behaviour, so the stock <c>RetryHandler</c> must be
/// replaced, not joined: if <c>ReplaceWorkflowRetryHandler</c> were dropped from
/// <c>AddUmbracoAutomateCore</c>, a step that retrying cannot fix would be retried instead of
/// stopping the run, and nothing else would notice. Composes through the real
/// <c>AddUmbracoAutomateCore</c> rather than a hand-copied registration block.
/// </summary>
public class WorkflowErrorHandlerCompositionTests
{
    [Fact]
    public void AddUmbracoAutomateCore_RegistersASingleRetryHandler()
    {
        var handlers = ResolveErrorHandlers();

        handlers.Count(h => h.Type == WorkflowErrorHandling.Retry).ShouldBe(1);
    }

    [Fact]
    public void AddUmbracoAutomateCore_MakesAutomateRetryHandlerTheRetryHandler()
    {
        var handlers = ResolveErrorHandlers();

        handlers.Single(h => h.Type == WorkflowErrorHandling.Retry).ShouldBeOfType<AutomateRetryHandler>();
    }

    private static List<IWorkflowErrorHandler> ResolveErrorHandlers()
    {
        var services = new ServiceCollection();

        // What the Umbraco host provides before any composer runs.
        services.AddLogging();

        new CompositionOnlyUmbracoBuilder(services).AddUmbracoAutomateCore();

        using var provider = services.BuildServiceProvider();
        return provider.GetServices<IWorkflowErrorHandler>().ToList();
    }

    /// <summary>
    /// The smallest <see cref="IUmbracoBuilder"/> that <c>AddUmbracoAutomateCore</c> can run against:
    /// real services and configuration, a type loader scanning the Automate core assembly (trigger
    /// notification handlers are discovered eagerly), and collection builders created on demand.
    /// Umbraco's own <c>UmbracoBuilder</c> needs a full host to construct.
    /// </summary>
    private sealed class CompositionOnlyUmbracoBuilder : IUmbracoBuilder
    {
        private readonly Dictionary<Type, ICollectionBuilder> _collectionBuilders = [];

        public CompositionOnlyUmbracoBuilder(IServiceCollection services)
        {
            Services = services;

            var coreAssembly = typeof(AutomateRetryHandler).Assembly;
            var typeFinder = new TypeFinder(
                NullLogger<TypeFinder>.Instance,
                new DefaultUmbracoAssemblyProvider(coreAssembly, NullLoggerFactory.Instance),
                null);
            TypeLoader = new TypeLoader(typeFinder, NullLogger<TypeLoader>.Instance, [coreAssembly]);
        }

        public IServiceCollection Services { get; }

        public IConfiguration Config { get; } = new ConfigurationBuilder().Build();

        public TypeLoader TypeLoader { get; }

        public ILoggerFactory BuilderLoggerFactory => NullLoggerFactory.Instance;

        public IProfiler Profiler { get; } = new NoopProfiler();

        public AppCaches AppCaches => AppCaches.NoCache;

        public TBuilder WithCollectionBuilder<TBuilder>()
            where TBuilder : ICollectionBuilder
        {
            if (!_collectionBuilders.TryGetValue(typeof(TBuilder), out var builder))
            {
                builder = Activator.CreateInstance<TBuilder>();
                _collectionBuilders[typeof(TBuilder)] = builder;
            }

            return (TBuilder)builder;
        }

        public void Build()
        {
            foreach (var builder in _collectionBuilders.Values)
            {
                builder.RegisterWith(Services);
            }
        }
    }
}
