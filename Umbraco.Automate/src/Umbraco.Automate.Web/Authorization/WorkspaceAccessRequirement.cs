using Microsoft.AspNetCore.Authorization;

namespace Umbraco.Automate.Web.Authorization;

/// <summary>
/// Authorization requirement for workspace membership access.
/// </summary>
public sealed class WorkspaceAccessRequirement : IAuthorizationRequirement;
