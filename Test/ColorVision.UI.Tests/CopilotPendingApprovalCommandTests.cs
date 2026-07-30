using ColorVision.Copilot;
using ColorVision.Copilot.Mcp;

namespace ColorVision.UI.Tests;

public sealed class CopilotPendingApprovalCommandTests
{
    private static readonly DateTimeOffset NowUtc = new(2026, 7, 31, 4, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ApproveCommandAcceptsOptionalOrdinalDuringAgentRuns()
    {
        var list = CopilotLocalCommandCatalog.Parse("/approve");
        var selected = CopilotLocalCommandCatalog.Parse("/approve 2");

        Assert.NotNull(list);
        Assert.Equal(CopilotLocalCommandKind.Approve, list.Command.Kind);
        Assert.True(list.Command.AvailableWhileAgentRuns);
        Assert.True(list.Command.AcceptsArguments);
        Assert.Equal("2", selected?.Arguments);
    }

    [Fact]
    public void SingleActionOpensExistingReviewWithoutApprovingIt()
    {
        var action = CreateAction("ApplyWorkspacePatch", 2);

        var result = CopilotPendingApprovalCommand.Evaluate([action], null, NowUtc);

        Assert.True(result.OpensReview);
        Assert.Same(action, result.Action);
        Assert.Equal(ConfirmableActionStatus.Pending, action.Status);
        Assert.Empty(result.Report);
    }

    [Fact]
    public void MultipleActionsAreOrderedByExpiryAndHideApprovalPayload()
    {
        var later = new ConfirmableAction
        {
            ToolName = "Invoke\r\nTool",
            ActionId = "private-action-id",
            ArgumentsSummary = @"secret C:\private\workspace",
            ReviewDetails = "full private review details",
            RiskLevel = "confirmation-required",
            CreatedAt = NowUtc.AddMinutes(-1),
            ExpiresAt = NowUtc.AddMinutes(8),
        };
        var sooner = CreateAction("Apply\u202ePatch", 2);

        var result = CopilotPendingApprovalCommand.Evaluate([later, sooner], string.Empty, NowUtc);

        Assert.False(result.OpensReview);
        Assert.True(
            result.Report.IndexOf("1. ApplyPatch", StringComparison.Ordinal)
            < result.Report.IndexOf("2. Invoke Tool", StringComparison.Ordinal));
        Assert.Contains("该命令本身不会批准或执行操作", result.Report, StringComparison.Ordinal);
        Assert.Contains("必须在原生窗口中核对完整详情并再次确认", result.Report, StringComparison.Ordinal);
        Assert.DoesNotContain("private-action-id", result.Report, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", result.Report, StringComparison.Ordinal);
        Assert.DoesNotContain("private", result.Report, StringComparison.Ordinal);
        Assert.DoesNotContain("full private review details", result.Report, StringComparison.Ordinal);
        Assert.DoesNotContain('\u202e', result.Report);
        Assert.All([later, sooner], action => Assert.Equal(ConfirmableActionStatus.Pending, action.Status));
    }

    [Fact]
    public void OrdinalSelectionIgnoresExpiredAndNonPendingActions()
    {
        var expired = CreateAction("ExpiredTool", -1);
        var rejected = CreateAction("RejectedTool", 1);
        rejected.Status = ConfirmableActionStatus.Rejected;
        var selected = CreateAction("SelectedTool", 3);

        var result = CopilotPendingApprovalCommand.Evaluate(
            [expired, rejected, selected],
            "1",
            NowUtc);

        Assert.True(result.OpensReview);
        Assert.Same(selected, result.Action);
        Assert.Equal(ConfirmableActionStatus.Pending, selected.Status);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("2")]
    [InlineData("1 extra")]
    [InlineData("-1")]
    public void InvalidOrdinalReturnsBoundedRecoveryWithoutStateMutation(string selector)
    {
        var action = CreateAction("Shell", 2);

        var result = CopilotPendingApprovalCommand.Evaluate([action], selector, NowUtc);

        Assert.False(result.OpensReview);
        Assert.Contains("参数无效", result.Report, StringComparison.Ordinal);
        Assert.Contains("/approve N", result.Report, StringComparison.Ordinal);
        Assert.Equal(ConfirmableActionStatus.Pending, action.Status);
    }

    [Fact]
    public void EmptyReviewableSetDoesNotOpenAWindow()
    {
        var result = CopilotPendingApprovalCommand.Evaluate([], null, NowUtc);

        Assert.False(result.OpensReview);
        Assert.Contains("没有仍有效且可审核", result.Report, StringComparison.Ordinal);
    }

    private static ConfirmableAction CreateAction(string toolName, int expiresInMinutes) => new()
    {
        ActionId = Guid.NewGuid().ToString("N"),
        ToolName = toolName,
        RiskLevel = "confirmation-required",
        CreatedAt = NowUtc.AddMinutes(-1),
        ExpiresAt = NowUtc.AddMinutes(expiresInMinutes),
    };
}
