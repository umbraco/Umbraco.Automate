using Shouldly;
using Umbraco.Automate.Core.Configuration;
using Xunit;

namespace Umbraco.Automate.Tests.Unit.Configuration;

public sealed class McpOptionsTests
{
    [Fact]
    public void DefaultRateLimitPerMinute_Is60()
    {
        new McpOptions().RateLimitPerMinute.ShouldBe(60);
    }
}
