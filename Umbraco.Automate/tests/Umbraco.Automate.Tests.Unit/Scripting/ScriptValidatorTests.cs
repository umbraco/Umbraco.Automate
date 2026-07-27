using Shouldly;
using Umbraco.Automate.Core.Scripting;

namespace Umbraco.Automate.Tests.Unit.Scripting;

public class ScriptValidatorTests
{
    private readonly ScriptValidator _validator = new();

    [Fact]
    public async Task ValidateScriptAsync_ValidScript_ReturnsNoErrors()
    {
        (await _validator.ValidateScriptAsync("export default function (data) { return data; }")).ShouldBeEmpty();
    }

    [Fact]
    public async Task ValidateScriptAsync_ValidArrowFunction_ReturnsNoErrors()
    {
        (await _validator.ValidateScriptAsync("export default (data) => data")).ShouldBeEmpty();
    }

    [Fact]
    public async Task ValidateScriptAsync_EmptyScript_ReturnsError()
    {
        (await _validator.ValidateScriptAsync("   ")).ShouldHaveSingleItem().ShouldContain("required");
    }

    [Fact]
    public async Task ValidateScriptAsync_SyntaxError_ReturnsError()
    {
        var errors = await _validator.ValidateScriptAsync("export default function ( {");
        errors.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task ValidateScriptAsync_NoDefaultExport_ReturnsError()
    {
        var errors = await _validator.ValidateScriptAsync("export function foo() {}");
        errors.ShouldHaveSingleItem().ShouldContain("default function");
    }

    [Fact]
    public async Task ValidateScriptAsync_DefaultExportNotAFunction_ReturnsError()
    {
        var errors = await _validator.ValidateScriptAsync("export default 42");
        errors.ShouldHaveSingleItem().ShouldContain("default function");
    }

    [Fact]
    public async Task ValidateScriptAsync_AlreadyCancelled_Throws()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(
            () => _validator.ValidateScriptAsync("export default (d) => d", cts.Token));
    }
}
