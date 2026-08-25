using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using OpenIddict.Validation.AspNetCore;
using Swashbuckle.AspNetCore.SwaggerGen;
using Umbraco.Automate.Core.Configuration;
using Umbraco.Automate.Core.Realtime;
using Umbraco.Automate.Web.Authorization;
using Umbraco.Automate.Web;
using Umbraco.Automate.Web.Realtime;
using Umbraco.Automate.Web.Api.Management.Automation.Mapping;
using Umbraco.Automate.Web.Api.Management.Catalogue.Mapping;
using Umbraco.Automate.Web.Api.Management.Common.Configuration;
using Umbraco.Automate.Web.Api.Management.Common.Json;
using Umbraco.Automate.Web.Api.Management.Run.Mapping;
using Umbraco.Automate.Web.Api.Management.Connection.Mapping;
using Umbraco.Automate.Web.Api.Management.Versioning.Mapping;
using Umbraco.Automate.Web.Api.Management.Workspace.Group.Mapping;
using Umbraco.Automate.Web.Api.Management.Workspace.Mapping;
using Umbraco.Cms.Api.Common.DependencyInjection;
using Umbraco.Cms.Api.Common.OpenApi;
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

        builder.Services.AddAuthorization(o =>
        {
            o.AddPolicy(AutomateAuthorizationPolicies.SectionAccessAutomate, policy =>
            {
                policy.AuthenticationSchemes.Add(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
#pragma warning disable CS0618 // Type or member is obsolete
                policy.RequireClaim(Umbraco.Cms.Core.Constants.Security.AllowedApplicationsClaimType, Core.Constants.Sections.Automate);
#pragma warning restore CS0618 // Type or member is obsolete
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
        builder.Services.Configure<SwaggerGenOptions>(options =>
        {
            if (options.SwaggerGeneratorOptions.SwaggerDocs.ContainsKey(Constants.ManagementApi.ApiName))
                return;

            options.SwaggerDoc(
                Constants.ManagementApi.ApiName,
                new OpenApiInfo
                {
                    Title = Constants.ManagementApi.ApiTitle,
                    Version = "Latest",
                    Description = $"Describes the {Constants.ManagementApi.ApiTitle} available for managing automations, triggers, actions, and runs when authenticated as a backoffice user.",
                });

            options.OperationFilter<UmbracoAutomateManagementApiBackOfficeSecurityRequirementsOperationFilter>(Constants.ManagementApi.ApiName);

            // Map System.Type to string in OpenAPI schema (JsonStringTypeConverter handles serialization).
            // Guard against other packages (e.g. Umbraco.AI.Web) registering the same mapping on the
            // shared SwaggerGenOptions — MapType() throws if the key already exists.
            if (!options.SchemaGeneratorOptions.CustomTypeMappings.ContainsKey(typeof(Type)))
            {
                options.MapType<Type>(() => new OpenApiSchema { Type = JsonSchemaType.String });
            }
        });

        builder.Services.AddSingleton<IOperationIdHandler, UmbracoAutomateApiOperationIdHandler>();
        builder.Services.AddSingleton<ISchemaIdHandler, UmbracoAutomateApiSchemaIdHandler>();

        builder.AddUmbracoAutomateJsonOptions();

        return builder;
    }

    private static IUmbracoBuilder AddUmbracoAutomateWebhookApi(this IUmbracoBuilder builder)
    {
        builder.Services.Configure<SwaggerGenOptions>(options =>
        {
            if (options.SwaggerGeneratorOptions.SwaggerDocs.ContainsKey(Constants.WebhookApi.ApiName))
                return;

            options.SwaggerDoc(
                Constants.WebhookApi.ApiName,
                new OpenApiInfo
                {
                    Title = Constants.WebhookApi.ApiTitle,
                    Version = "Latest",
                    Description = "Public webhook endpoints for triggering automations from external systems. No authentication required.",
                });
        });

        builder.AddUmbracoAutomateWebhookRateLimiting();

        return builder;
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
