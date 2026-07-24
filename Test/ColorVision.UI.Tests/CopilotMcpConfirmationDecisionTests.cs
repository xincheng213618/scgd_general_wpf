using ColorVision.Copilot.Mcp;

namespace ColorVision.UI.Tests
{
    public sealed class CopilotMcpConfirmationDecisionTests
    {
        [Fact]
        public void ApprovalPromptPreservesTheSafetyCriticalActionDetails()
        {
            var action = CreateAction(executeOnApproval: false);
            try
            {
                var prompt = CopilotMcpConfirmationDecision.BuildApprovalPrompt(action);

                Assert.Contains(action.Title, prompt, StringComparison.Ordinal);
                Assert.Contains(action.ToolName, prompt, StringComparison.Ordinal);
                Assert.Contains(action.RiskLevel, prompt, StringComparison.Ordinal);
                Assert.Contains(action.ArgumentsSummary, prompt, StringComparison.Ordinal);
                Assert.Contains("Only approve if", prompt, StringComparison.Ordinal);
            }
            finally
            {
                CopilotMcpConfirmationStore.Instance.Reject(action.ActionId, out _);
            }
        }

        [Fact]
        public async Task ClientConfirmedActionIsApprovedWithoutExecutingIt()
        {
            var executionCount = 0;
            var action = CreateAction(
                executeOnApproval: false,
                executor: _ =>
                {
                    executionCount++;
                    return Task.FromResult(CopilotMcpToolCallResult.Ok("executed"));
                });
            try
            {
                var result = await CopilotMcpConfirmationDecision.ApproveAsync(
                    CopilotMcpConfirmationStore.Instance,
                    action,
                    CancellationToken.None);

                Assert.True(result.Success);
                Assert.False(result.ExecutedImmediately);
                Assert.Equal(ConfirmableActionStatus.Approved, action.Status);
                Assert.Equal(0, executionCount);
            }
            finally
            {
                CopilotMcpConfirmationStore.Instance.Reject(action.ActionId, out _);
            }
        }

        [Fact]
        public async Task InAppActionExecutesImmediatelyAfterApproval()
        {
            var executionCount = 0;
            var action = CreateAction(
                executeOnApproval: true,
                executor: _ =>
                {
                    executionCount++;
                    return Task.FromResult(CopilotMcpToolCallResult.Ok("executed"));
                });

            var result = await CopilotMcpConfirmationDecision.ApproveAsync(
                CopilotMcpConfirmationStore.Instance,
                action,
                CancellationToken.None);

            Assert.True(result.Success);
            Assert.True(result.ExecutedImmediately);
            Assert.Equal(ConfirmableActionStatus.Executed, action.Status);
            Assert.Equal(1, executionCount);
        }

        [Fact]
        public async Task AgentFrameworkActionIsApprovedWithoutDirectExecution()
        {
            var action = CopilotMcpConfirmationStore.Instance.CreateAgentFrameworkApproval(
                "Continue the hosted task",
                "Resume after a user decision.",
                "agent_tool",
                "{\"scope\":\"current\"}",
                $"call-{Guid.NewGuid():N}",
                _ => { });
            try
            {
                var result = await CopilotMcpConfirmationDecision.ApproveAsync(
                    CopilotMcpConfirmationStore.Instance,
                    action,
                    CancellationToken.None);

                Assert.True(result.Success);
                Assert.False(result.ExecutedImmediately);
                Assert.Equal(ConfirmableActionStatus.Approved, action.Status);
                Assert.Contains("resume in the same session", result.Message, StringComparison.Ordinal);
            }
            finally
            {
                CopilotMcpConfirmationStore.Instance.Reject(action.ActionId, out _);
            }
        }

        private static ConfirmableAction CreateAction(
            bool executeOnApproval,
            Func<CancellationToken, Task<CopilotMcpToolCallResult>>? executor = null)
        {
            return CopilotMcpConfirmationStore.Instance.Create(
                $"Desktop pet test {Guid.NewGuid():N}",
                "Review a pending desktop pet action.",
                "confirmation-required",
                "desktop_pet_test",
                "{\"value\":1}",
                executor ?? (_ => Task.FromResult(CopilotMcpToolCallResult.Ok("executed"))),
                executeOnApproval);
        }
    }
}
