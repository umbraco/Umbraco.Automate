using Microsoft.Extensions.DependencyInjection;
using Umbraco.Automate.Core.Dispatch;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Automate.Extensions;

/// <summary>
/// Extension methods for <see cref="IUmbracoBuilder"/> for trigger notification wiring.
/// </summary>
public static partial class UmbracoBuilderExtensions
{
    /// <summary>
    /// Scans discovered trigger types for <see cref="INotificationTrigger{TNotification}"/> implementations
    /// and registers the corresponding <see cref="TriggerNotificationHandler{TNotification}"/> for each
    /// distinct notification type.
    /// </summary>
    internal static IUmbracoBuilder RegisterTriggerNotificationHandlers(this IUmbracoBuilder builder)
    {
        var notificationTriggerOpen = typeof(INotificationTrigger<>);
        var handlerOpen = typeof(TriggerNotificationHandler<>);
        var asyncHandlerOpen = typeof(INotificationAsyncHandler<>);

        var registeredNotificationTypes = new HashSet<Type>();

        // Find all trigger types via Umbraco's TypeLoader (same discovery as TriggerCollectionBuilder).
        var triggerTypes = builder.TypeLoader.GetTypesWithAttribute<ITrigger, TriggerAttribute>();

        foreach (var triggerType in triggerTypes)
        {
            var notificationTriggerInterfaces = triggerType.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == notificationTriggerOpen);

            foreach (var iface in notificationTriggerInterfaces)
            {
                var notificationType = iface.GetGenericArguments()[0];

                // Register the trigger as INotificationTrigger<T> so the handler can resolve IEnumerable<>.
                builder.Services.AddTransient(iface, triggerType);

                // Register the notification handler once per distinct notification type.
                if (registeredNotificationTypes.Add(notificationType))
                {
                    var handlerServiceType = asyncHandlerOpen.MakeGenericType(notificationType);
                    var handlerImplType = handlerOpen.MakeGenericType(notificationType);

                    builder.Services.AddTransient(handlerServiceType, handlerImplType);
                }
            }
        }

        return builder;
    }
}
