using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Umbraco.Cms.Core.Security;

namespace Umbraco.Automate.Core.Security;

/// <summary>
/// Extension methods for decorating <see cref="IBackOfficeSecurityAccessor"/> in the DI container.
/// </summary>
internal static class ServiceCollectionExtensions
{
    /// <summary>
    /// Decorates the existing <see cref="IBackOfficeSecurityAccessor"/> registration with
    /// <see cref="AutomateBackOfficeSecurityAccessor"/> so that automation runs can set the
    /// backoffice identity to the workspace service principal without affecting the original
    /// accessor's behaviour outside of automation contexts.
    /// </summary>
    public static IServiceCollection DecorateBackOfficeSecurityAccessor(this IServiceCollection services)
    {
        var original = services.LastOrDefault(d => d.ServiceType == typeof(IBackOfficeSecurityAccessor));
        if (original is null)
        {
            return services;
        }

        services.Remove(original);

        services.Add(new ServiceDescriptor(
            typeof(IBackOfficeSecurityAccessor),
            sp =>
            {
                var inner = CreateInstance(sp, original);
                return new AutomateBackOfficeSecurityAccessor(inner);
            },
            original.Lifetime));

        return services;
    }

    private static IBackOfficeSecurityAccessor CreateInstance(IServiceProvider sp, ServiceDescriptor descriptor)
    {
        if (descriptor.ImplementationInstance is IBackOfficeSecurityAccessor instance)
        {
            return instance;
        }

        if (descriptor.ImplementationFactory is { } factory)
        {
            return (IBackOfficeSecurityAccessor)factory(sp);
        }

        if (descriptor.ImplementationType is { } type)
        {
            return (IBackOfficeSecurityAccessor)ActivatorUtilities.CreateInstance(sp, type);
        }

        throw new InvalidOperationException(
            $"Unable to create the original IBackOfficeSecurityAccessor from descriptor: {descriptor}");
    }
}
