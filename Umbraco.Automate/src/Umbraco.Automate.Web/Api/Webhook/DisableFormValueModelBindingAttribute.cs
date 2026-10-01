using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Umbraco.Automate.Web.Api.Webhook;

/// <summary>
/// Stops MVC parsing a form-encoded request body before the action runs.
/// </summary>
/// <remarks>
/// Model binding creates every value provider up front, whatever the action binds, and the form
/// value providers parse <c>application/x-www-form-urlencoded</c> and <c>multipart/form-data</c>
/// bodies to do it. A body sent with a form content type that is not a valid form (a key over the
/// form key length limit, say) then fails model binding with a 400 before the action can read it
/// raw. Removing those factories leaves the body to the action.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
internal sealed class DisableFormValueModelBindingAttribute : Attribute, IResourceFilter
{
    /// <inheritdoc />
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        var factories = context.ValueProviderFactories;
        factories.RemoveType<FormValueProviderFactory>();
        factories.RemoveType<FormFileValueProviderFactory>();
        factories.RemoveType<JQueryFormValueProviderFactory>();
    }

    /// <inheritdoc />
    public void OnResourceExecuted(ResourceExecutedContext context)
    {
    }
}
