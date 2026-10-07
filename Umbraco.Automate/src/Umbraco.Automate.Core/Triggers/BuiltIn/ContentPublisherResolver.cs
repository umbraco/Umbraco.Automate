using Microsoft.Extensions.Logging;
using UmbracoConstants = Umbraco.Cms.Core.Constants;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Services;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Classifies the user recorded as a content item's publisher (<c>IContent.PublisherId</c>)
/// into a <see cref="ContentPublisherKind"/> value. Used by <see cref="ContentPublishedTrigger"/>
/// to populate the trigger output's <c>PublisherKind</c>.
/// </summary>
/// <remarks>
/// The super user (id -1) is classified as <see cref="ContentPublisherKind.System"/> without a
/// lookup — that id never resolves to a stored user and is how in-process code and scheduled
/// publishing publish. Failures (missing user, service error) degrade to <c>null</c> so the
/// publish flow is never blocked by Automate; a "Published by" filter set to a specific kind
/// will naturally fail to match, which is the right outcome.
/// </remarks>
internal static class ContentPublisherResolver
{
    public static string? Resolve(IUserService userService, int? publisherId, ILogger logger)
    {
        if (publisherId is null)
        {
            return null;
        }

        if (publisherId.Value == UmbracoConstants.Security.SuperUserId)
        {
            return ContentPublisherKind.System;
        }

        try
        {
            var user = userService.GetUserById(publisherId.Value);
            if (user is null)
            {
                return null;
            }

            return user.Kind == UserKind.Api ? ContentPublisherKind.Api : ContentPublisherKind.User;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to resolve publishing user {PublisherId}; publisher-kind filters will treat the publisher as unknown",
                publisherId);
            return null;
        }
    }
}
