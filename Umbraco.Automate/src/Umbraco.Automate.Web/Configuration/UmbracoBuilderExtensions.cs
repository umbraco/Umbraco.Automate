using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenIddict.Validation.AspNetCore;
using Umbraco.Automate.Core.Configuration;
using Umbraco.Automate.Core.Realtime;
using Umbraco.Automate.Web.Authorization;
using Umbraco.Automate.Web;
using Umbraco.Automate.Web.Realtime;
using Umbraco.Automate.Web.Api.Management.Automation.Mapping;
using Umbraco.Automate.Web.Api.Management.Catalogue.Mapping;
using Umbraco.Automate.Web.Api.Management.Common.Json;
using Umbraco.Automate.Web.Api.Management.Run.Mapping;
using Umbraco.Automate.Web.Api.Management.Connection.Mapping;
using Umbraco.Automate.Web.Api.Management.Versioning.Mapping;
using Umbraco.Automate.Web.Api.Management.Workspace.Group.Mapping;
using Umbraco.Automate.Web.Api.Management.Workspace.Mapping;
using Umbraco.Cms.Api.Common.DependencyInjection;
using Umbraco.Cms.Api.Common.OpenApi;
using Umbraco.Cms.Api.Management.OpenApi;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Mapping;
using Umbraco.Cms.Web.Common.ApplicationBuilder;

namespace Umbraco.Automate.Extensions;

/// <summary>
/// Extension methods for configuring Umbraco Automate web services.
/// </summary>
public static partial class UmbracoBuilderExtensions
{
    /// <summary>
    /// Adds Umbraco Automate web services including the Management API.
    /// </summary>
    internal static IUmbracoBuilder AddUmbracoAutomateWeb(this IUmbracoBuilder builder)
    {
        builder.AddUmbracoAutomateAuthorization();
        builder.AddUmbracoAutomateManagementApi();
        builder.AddUmbracoAutomateWebhookApi();
        builder.AddUmbracoAutomateMapDefinitions();
        builder.AddUmbracoAutomateRealtime();

        return builder;
    }

    private static IUmbracoBuilder AddUmbracoAutomateRealtime(this IUmbracoBuilder builder)
    {
        // Do NOT configure JsonHubProtocolOptions here — that options object is app-wide,
        // and mutating it would change the wire format for every other SignalR hub in the
        // process (notably Umbraco Deploy's hubs, whose frontend expects numeric enum
        // values). Our own backoffice listener reads numeric enum values instead.
        builder.Services.AddSignalR();
        builder.Services.AddSingleton<IEditorNotifier, EditorNotifier>();

        // Map the hub endpoint. The Endpoints callback runs before Umbraco's own UseEndpoints,
        // so calling UseEndpoints here adds our hub middleware ahead of the main endpoint mapping.
        builder.Services.Configure<UmbracoPipelineOptions>(options =>
        {
            options.AddFilter(new UmbracoPipelineFilter("UmbracoAutomateEditorNotificationHub")
            {
                Endpoints = app => app.UseEndpoints(endpoints =>
                {
                    endpoints.MapHub<EditorNotificationHub>(EditorNotificationHub.Route);
                }),
            });
        });

        return builder;
    }

    private static IUmbracoBuilder AddUmbracoAutomateMapDefinitions(this IUmbracoBuilder builder)
    {
        builder.WithCollectionBuilder<MapDefinitionCollectionBuilder>()
            .Add<AutomationMapDefinition>()
            .Add<RunMapDefinition>()
            .Add<CatalogueMapDefinition>()
            .Add<WorkspaceMapDefinition>()
            .Add<ConnectionMapDefinition>()
            .Add<WorkspaceGroupMapDefinition>()
            .Add<VersioningMapDefinition>();

        return builder;
    }

    private static IUmbracoBuilder AddUmbracoAutomateAuthorization(this IUmbracoBuilder builder)
    {
        builder.Services.AddSingleton<IAuthorizationHandler, WorkspaceAccessHandler>();
        builder.Services.AddSingleton<IAuthorizationHandler, AutomateSectionAuthorizationHandler>();

        builder.Services.AddAuthorization(o =>
        {
            o.AddPolicy(AutomateAuthorizationPolicies.SectionAccessAutomate, policy =>
            {
                policy.AuthenticationSchemes.Add(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
                policy.Requirements.Add(new AutomateSectionRequirement());
            });

            o.AddPolicy(AutomateAuthorizationPolicies.WorkspaceAccess, policy =>
            {
                policy.AuthenticationSchemes.Add(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
                policy.Requirements.Add(new WorkspaceAccessRequirement());
            });
        });

        return builder;
    }

    private static IUmbracoBuilder AddUmbracoAutomateManagementApi(this IUmbracoBuilder builder)
    {
        builder.AddUmbracoAutomateJsonOptions();

        // Authenticated backoffice document. WithJsonOptions aligns schema generation with the
        // named serializer options registered in AddUmbracoAutomateJsonOptions (camelCase, custom
        // converters, alphabetised properties) so the generated client matches runtime serialisation.
        builder.AddBackOfficeOpenApiDocument(Constants.ManagementApi.ApiName, document => document
            .WithTitle(Constants.ManagementApi.ApiTitle)
            .WithBackOfficeAuthentication()
            .WithJsonOptions(Constants.ManagementApi.ApiName)
            .ConfigureOpenApiOptions(options =>
            {
                ConfigureDocumentInfo(
                    options,
                    $"Describes the {Constants.ManagementApi.ApiTitle} available for managing automations, triggers, actions, and runs when authenticated as a backoffice user.");
                PreserveAutomateSchemaIds(options);
            }));

        return builder;
    }

    private static IUmbracoBuilder AddUmbracoAutomateWebhookApi(this IUmbracoBuilder builder)
    {
        // Public document — no backoffice authentication. Webhook callers authenticate via signature.
        builder.AddBackOfficeOpenApiDocument(Constants.WebhookApi.ApiName, document => document
            .WithTitle(Constants.WebhookApi.ApiTitle)
            .ConfigureOpenApiOptions(options =>
            {
                ConfigureDocumentInfo(
                    options,
                    "Public webhook endpoints for triggering automations from external systems. No authentication required.");
                PreserveAutomateSchemaIds(options);
            }));

        builder.AddUmbracoAutomateWebhookRateLimiting();

        return builder;
    }

    /// <summary>
    /// Sets the OpenAPI document description and version via a document transformer, matching the
    /// metadata Swashbuckle produced prior to the v18 OpenAPI migration.
    /// </summary>
    private static void ConfigureDocumentInfo(OpenApiOptions options, string description)
        => options.AddDocumentTransformer((document, _, _) =>
        {
            document.Info.Description = description;
            document.Info.Version = "Latest";
            return Task.CompletedTask;
        });

    /// <summary>
    /// Applies Umbraco's schema-ID naming convention to types in the Umbraco.Automate namespace.
    /// </summary>
    /// <remarks>
    /// The default delegate set by <c>AddBackOfficeOpenApiDocument</c> only applies the convention to
    /// <c>Umbraco.Cms.*</c> types; ours fall through to the framework default, which yields different
    /// names than the v17 <c>UmbracoAutomateApiSchemaIdHandler</c>. Overriding here keeps generated
    /// TypeScript client type names stable across the v17 -> v18 migration.
    /// </remarks>
    private static void PreserveAutomateSchemaIds(OpenApiOptions options)
    {
        Func<JsonTypeInfo, string?> inheritedSchemaReferenceId = options.CreateSchemaReferenceId;
        options.CreateSchemaReferenceId = jsonTypeInfo =>
            IsAutomateType(jsonTypeInfo)
                ? UmbracoSchemaIdGenerator.Generate(Nullable.GetUnderlyingType(jsonTypeInfo.Type) ?? jsonTypeInfo.Type)
                : inheritedSchemaReferenceId(jsonTypeInfo);
    }

    private static bool IsAutomateType(JsonTypeInfo jsonTypeInfo)
    {
        Type targetType = Nullable.GetUnderlyingType(jsonTypeInfo.Type) ?? jsonTypeInfo.Type;
        return targetType.Namespace?.StartsWith(Constants.AppNamespaceRoot) is true;
    }

    private static IUmbracoBuilder AddUmbracoAutomateWebhookRateLimiting(this IUmbracoBuilder builder)
    {
        builder.Services.AddRateLimiter(options =>
        {
            options.AddPolicy(Constants.WebhookApi.RateLimitPolicy, context =>
            {
                var webhookOptions = context.RequestServices
                    .GetRequiredService<IOptions<WebhookOptions>>().Value;

                var partitionKey = context.Request.RouteValues["automationId"]?.ToString() ?? "global";

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: partitionKey,
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = webhookOptions.RateLimitPerMinute,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0,
                    });
            });

            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.Headers.RetryAfter = "60";
            };
        });

        // Register an Umbraco pipeline filter to add UseRateLimiter() middleware
        // after routing (so route values are available for partitioning).
        builder.Services.Configure<UmbracoPipelineOptions>(options =>
        {
            options.AddFilter(new UmbracoPipelineFilter(
                "UmbracoAutomateWebhookRateLimiting")
            {
                PostRouting = app => app.UseRateLimiter(),
            });
        });

        return builder;
    }

    private static IUmbracoBuilder AddUmbracoAutomateJsonOptions(this IUmbracoBuilder builder)
    {
        builder.Services.AddControllers()
            .AddJsonOptions(Constants.ManagementApi.ApiName, options =>
            {
                options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                options.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
                options.JsonSerializerOptions.WriteIndented = false;

                options.JsonSerializerOptions.TypeInfoResolver = new DefaultJsonTypeInfoResolver
                {
                    Modifiers = { AlphabetizeProperties() },
                };

                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                options.JsonSerializerOptions.Converters.Add(new JsonStringTypeConverter());
                options.JsonSerializerOptions.Converters.Add(new UtcDateTimeJsonConverter());
                options.JsonSerializerOptions.Converters.Add(new UtcNullableDateTimeJsonConverter());
            });

        return builder;
    }

    private static Action<JsonTypeInfo> AlphabetizeProperties() =>
        static typeInfo =>
        {
            if (typeInfo.Kind != JsonTypeInfoKind.Object)
            {
                return;
            }

            var properties = typeInfo.Properties.OrderBy(p => p.Name, StringComparer.Ordinal).ToList();
            typeInfo.Properties.Clear();
            for (var i = 0; i < properties.Count; i++)
            {
                properties[i].Order = i;
                typeInfo.Properties.Add(properties[i]);
            }
        };

}
