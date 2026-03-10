using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using Umbraco.Automate.Web;
using Umbraco.Automate.Web.Api.Management.Automation.Mapping;
using Umbraco.Automate.Web.Api.Management.Catalogue.Mapping;
using Umbraco.Automate.Web.Api.Management.Common.Configuration;
using Umbraco.Automate.Web.Api.Management.Common.Json;
using Umbraco.Automate.Web.Api.Management.Run.Mapping;
using Umbraco.Cms.Api.Common.DependencyInjection;
using Umbraco.Cms.Api.Common.OpenApi;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Mapping;

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
        builder.AddUmbracoAutomateManagementApi();
        builder.AddUmbracoAutomateWebhookApi();
        builder.AddUmbracoAutomateMapDefinitions();

        return builder;
    }

    private static IUmbracoBuilder AddUmbracoAutomateMapDefinitions(this IUmbracoBuilder builder)
    {
        builder.WithCollectionBuilder<MapDefinitionCollectionBuilder>()
            .Add<AutomationMapDefinition>()
            .Add<RunMapDefinition>()
            .Add<CatalogueMapDefinition>();

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
