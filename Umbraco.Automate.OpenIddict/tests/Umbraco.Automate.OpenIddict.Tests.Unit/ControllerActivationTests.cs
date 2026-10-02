using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Automate.OpenIddict.Controllers;

namespace Umbraco.Automate.OpenIddict.Tests.Unit;

/// <summary>
/// MVC creates controllers through <see cref="ActivatorUtilities"/>, which throws at request time — not
/// at startup — when it can satisfy more than one constructor. Keeping an obsolete constructor alongside
/// a new one therefore breaks every request to that controller unless the intended one carries
/// <see cref="ActivatorUtilitiesConstructorAttribute"/>. This sweeps every public controller so a future
/// overload is caught here rather than in the browser.
/// </summary>
public class ControllerActivationTests
{
    public static TheoryData<Type> ControllerTypes()
    {
        var data = new TheoryData<Type>();

        var controllers = typeof(OAuthCallbackController).Assembly
            .GetTypes()
            .Where(t => t is { IsAbstract: false, IsPublic: true } && typeof(ControllerBase).IsAssignableFrom(t))
            .OrderBy(t => t.FullName);

        foreach (var controller in controllers)
        {
            data.Add(controller);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(ControllerTypes))]
    public void Controller_HasExactlyOneActivatableConstructor(Type controllerType)
    {
        // Mirrors what MVC's controller activator does per request. Building the factory is enough —
        // it throws on ambiguity without needing any service to be registered.
        Should.NotThrow(
            () => ActivatorUtilities.CreateFactory(controllerType, Type.EmptyTypes),
            $"{controllerType.Name} cannot be activated by MVC. If it has more than one constructor, "
                + "mark the intended one with [ActivatorUtilitiesConstructor].");
    }
}
