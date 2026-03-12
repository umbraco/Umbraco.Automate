using Umbraco.Automate.Core.Execution;

namespace Umbraco.Automate.Tests.Unit.Execution;

public class AutomationExecutionContextTests
{
    private readonly AutomationExecutionContext _context = new()
    {
        ServiceAccountKey = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
        WorkspaceId = Guid.Parse("11111111-2222-3333-4444-555555555555"),
        WorkspaceName = "My Workspace",
        AutomationId = Guid.Parse("66666666-7777-8888-9999-aaaaaaaaaaaa"),
        AutomationName = "Send Welcome Email",
        RunId = Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff"),
        InitiatorType = "user",
        InitiatorId = "admin@test.com",
    };

    [Fact]
    public void FormatPerformingDetails_IncludesAutomationNameAndRunId()
    {
        var result = _context.FormatPerformingDetails();

        result.ShouldBe("Umbraco Automate: 'Send Welcome Email' (Run bbbbbbbb-cccc-dddd-eeee-ffffffffffff)");
    }

    [Fact]
    public void FormatEventDetails_ProducesValidJson()
    {
        var stepId = Guid.Parse("cccccccc-dddd-eeee-ffff-111111111111");

        var result = _context.FormatEventDetails(stepId);

        result.ShouldContain("\"automationId\":\"66666666-7777-8888-9999-aaaaaaaaaaaa\"");
        result.ShouldContain("\"runId\":\"bbbbbbbb-cccc-dddd-eeee-ffffffffffff\"");
        result.ShouldContain("\"stepId\":\"cccccccc-dddd-eeee-ffff-111111111111\"");
        result.ShouldContain("\"workspace\":\"My Workspace\"");
        result.ShouldContain("\"initiatedBy\":\"user:admin@test.com\"");
    }

    [Fact]
    public void FormatEventDetails_HandlesNullInitiatorId()
    {
        var context = new AutomationExecutionContext
        {
            ServiceAccountKey = Guid.NewGuid(),
            WorkspaceId = Guid.NewGuid(),
            WorkspaceName = "Test",
            AutomationId = Guid.NewGuid(),
            AutomationName = "Test",
            RunId = Guid.NewGuid(),
            InitiatorType = "scheduled",
            InitiatorId = null,
        };

        var result = context.FormatEventDetails(Guid.NewGuid());

        result.ShouldContain("\"initiatedBy\":\"scheduled:unknown\"");
    }
}
