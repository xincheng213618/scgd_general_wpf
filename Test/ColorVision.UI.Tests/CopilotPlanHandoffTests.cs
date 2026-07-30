using ColorVision.Copilot;
using Newtonsoft.Json;

namespace ColorVision.UI.Tests;

public sealed class CopilotPlanHandoffTests
{
    [Fact]
    public void CompletedPlanExposesReviewActionsAndPlanSpecificStatus()
    {
        var message = CreateCompletedPlan();

        Assert.True(message.HasCompletedPlan);
        Assert.True(message.HasAgentTaskState);
        Assert.Equal("计划已生成", message.AgentStopReasonLabel);
        Assert.Equal("2 个计划步骤", message.AgentTaskProgressLabel);
        Assert.Contains("计划已生成", message.AgentTaskSummaryToolTip, StringComparison.Ordinal);
    }

    [Fact]
    public void ExecutionRequestBindsTheExactPlanWithoutGrantingToolApproval()
    {
        var message = CreateCompletedPlan("# Plan\n1. Inspect\n2. Implement\n3. Verify");

        Assert.True(CopilotPlanHandoff.TryCreateExecutionRequest(message, out var request));
        Assert.Equal(CopilotPlanHandoff.VisibleExecutionPrompt, request.VisiblePrompt);
        Assert.Equal(message.Id, request.PlanMessageId);
        Assert.Equal(64, request.PlanSha256.Length);
        Assert.StartsWith(CopilotPlanHandoff.ApprovedExecutionPrefix, request.ModelPrompt, StringComparison.Ordinal);
        Assert.Contains(message.Content, request.ModelPrompt, StringComparison.Ordinal);
        Assert.Contains($"assistant_message_id={message.Id}", request.ModelPrompt, StringComparison.Ordinal);
        Assert.Contains($"sha256={request.PlanSha256}", request.ModelPrompt, StringComparison.Ordinal);
        Assert.Contains("does not pre-approve any protected tool call", request.ModelPrompt, StringComparison.Ordinal);
        Assert.Contains("Revalidate mutable workspace state", request.ModelPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public void ApprovedExecutionContentSurvivesPersistenceAndRemainsTheEffectiveRequest()
    {
        var plan = CreateCompletedPlan();
        Assert.True(CopilotPlanHandoff.TryCreateExecutionRequest(plan, out var request));
        var userMessage = new CopilotChatMessage(CopilotChatRole.User, request.VisiblePrompt)
        {
            RequestMode = CopilotAgentMode.Auto,
            RequestContent = request.ModelPrompt,
        };

        var json = JsonConvert.SerializeObject(userMessage);
        var restored = JsonConvert.DeserializeObject<CopilotChatMessage>(json);

        Assert.NotNull(restored);
        Assert.True(CopilotPlanHandoff.IsApprovedExecutionRequest(restored.RequestContent));
        Assert.Equal(
            request.ModelPrompt,
            CopilotPlanHandoff.ResolveEffectiveUserText(restored.Content, restored.RequestContent));
        Assert.Equal(
            "ordinary request",
            CopilotPlanHandoff.ResolveEffectiveUserText("ordinary request", "prepared but unbound context"));
    }

    [Fact]
    public void IncompleteOrUnsafePlanCannotBeApproved()
    {
        var pending = CreateCompletedPlan();
        pending.IsResponsePending = true;
        var interrupted = CreateCompletedPlan();
        interrupted.WasResponseInterrupted = true;
        var truncated = CreateCompletedPlan();
        truncated.IsResponseContentTruncated = true;
        var failed = CreateCompletedPlan();
        failed.AgentStopReason = CopilotAgentStopReason.IncompleteOutput;
        var ordinary = CreateCompletedPlan();
        ordinary.RequestMode = CopilotAgentMode.Auto;

        foreach (var message in new[] { pending, interrupted, truncated, failed, ordinary })
        {
            Assert.False(message.HasCompletedPlan);
            Assert.False(CopilotPlanHandoff.TryCreateExecutionRequest(message, out _));
        }
    }

    private static CopilotChatMessage CreateCompletedPlan(string content = "# Plan\n1. Change the runtime\n2. Run focused tests")
    {
        return new CopilotChatMessage(CopilotChatRole.Assistant, content)
        {
            Id = "plan-message-1",
            RequestMode = CopilotAgentMode.Plan,
            AgentStopReason = CopilotAgentStopReason.Completed,
            AgentTaskLedger = new CopilotAgentTaskLedgerSnapshot
            {
                Mode = "plan",
                Items =
                [
                    new CopilotAgentTaskItem { Id = 1, Title = "Change the runtime" },
                    new CopilotAgentTaskItem { Id = 2, Title = "Run focused tests" },
                ],
            },
        };
    }
}
