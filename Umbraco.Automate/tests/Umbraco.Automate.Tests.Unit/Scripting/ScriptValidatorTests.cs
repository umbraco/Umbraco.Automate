using Shouldly;
using Umbraco.Automate.Core.Scripting;

namespace Umbraco.Automate.Tests.Unit.Scripting;

public class ScriptValidatorTests
{
    private readonly ScriptValidator _validator = new();

    [Fact]
    public void Validate_ValidScript_ReturnsNoErrors()
    {
        _validator.Validate("export default function (data) { return data; }").ShouldBeEmpty();
    }

    [Fact]
    public void Validate_ValidArrowFunction_ReturnsNoErrors()
    {
        _validator.Validate("export default (data) => data").ShouldBeEmpty();
    }

    [Fact]
    public void Validate_EmptyScript_ReturnsError()
    {
        _validator.Validate("   ").ShouldHaveSingleItem().ShouldContain("required");
    }

    [Fact]
    public void Validate_SyntaxError_ReturnsError()
    {
        var errors = _validator.Validate("export default function ( {");
        errors.ShouldNotBeEmpty();
    }

    [Fact]
    public void Validate_NoDefaultExport_ReturnsError()
    {
        var errors = _validator.Validate("export function foo() {}");
        errors.ShouldHaveSingleItem().ShouldContain("default function");
    }

    [Fact]
    public void Validate_DefaultExportNotAFunction_ReturnsError()
    {
        var errors = _validator.Validate("export default 42");
        errors.ShouldHaveSingleItem().ShouldContain("default function");
    }
}
