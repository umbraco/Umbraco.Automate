using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Umbraco.Automate.Web.Api.Webhook;

namespace Umbraco.Automate.Tests.Unit.Webhook;

public class DisableFormValueModelBindingAttributeTests
{
    [Fact]
    public void OnResourceExecuting_RemovesFormValueProviders_AndKeepsTheRest()
    {
        var factories = new List<IValueProviderFactory>
        {
            new FormValueProviderFactory(),
            new FormFileValueProviderFactory(),
            new JQueryFormValueProviderFactory(),
            new RouteValueProviderFactory(),
            new QueryStringValueProviderFactory(),
        };
        var context = new ResourceExecutingContext(
            new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()),
            new List<IFilterMetadata>(),
            factories);

        new DisableFormValueModelBindingAttribute().OnResourceExecuting(context);

        context.ValueProviderFactories.Select(f => f.GetType()).ShouldBe(
            [typeof(RouteValueProviderFactory), typeof(QueryStringValueProviderFactory)],
            ignoreOrder: true);
    }
}
