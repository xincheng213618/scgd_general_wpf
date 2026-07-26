using ColorVision.Copilot;
using ColorVision.Copilot.Mcp;

namespace ColorVision.UI.Tests
{
    public sealed class CopilotMcpConfirmationDecisionTests
    {
        private const string ConversationId = "conversation-safety-contract";
        private const string TaskId = "task-safety-contract";
        private const string WorkspacePath = @"C:\ColorVision\SafetyContract";

        [Fact]
        public void ApprovalPromptShowsSourceTaskWorkspaceImpactAndReversibility()
        {
            var action = CreateAction(executeOnApproval: false);
            try
            {
                var prompt = CopilotMcpConfirmationDecision.BuildApprovalPrompt(action);

                Assert.Contains(action.Title, prompt, StringComparison.Ordinal);
                Assert.Contains(action.RequesterLabel, prompt, StringComparison.Ordinal);
                Assert.Contains(action.TaskScopeLabel, prompt, StringComparison.Ordinal);
                Assert.Contains(action.WorkspaceLabel, prompt, StringComparison.Ordinal);
                Assert.Contains(action.ImpactLabel, prompt, StringComparison.Ordinal);
                Assert.Contains(action.ReversibilityLabel, prompt, StringComparison.Ordinal);
                Assert.Contains(action.ToolName, prompt, StringComparison.Ordinal);
                Assert.Contains(action.ArgumentsSummary, prompt, StringComparison.Ordinal);
                Assert.Contains("请仅在来源、任务、工作区和影响都符合你的意图时批准", prompt, StringComparison.Ordinal);
            }
            finally
            {
                CancelIfActive(action);
            }
        }

        [Fact]
        public async Task ExactConversationTaskAndWorkspaceCanApproveAction()
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
                    CreateReviewContext(),
                    CancellationToken.None);

                Assert.True(result.Success);
                Assert.False(result.ExecutedImmediately);
                Assert.Equal(ConfirmableActionStatus.Approved, action.Status);
                Assert.Equal(0, executionCount);
            }
            finally
            {
                CancelIfActive(action);
            }
        }

        [Theory]
        [InlineData("another-conversation", TaskId, WorkspacePath)]
        [InlineData(ConversationId, "another-task", WorkspacePath)]
        [InlineData(ConversationId, TaskId, @"C:\ColorVision\AnotherWorkspace")]
        public async Task MismatchedConversationTaskOrWorkspaceCannotApproveAction(
            string conversationId,
            string taskId,
            string workspacePath)
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
                    new CopilotConfirmationReviewContext(conversationId, taskId, workspacePath),
                    CancellationToken.None);

                Assert.False(result.Success);
                Assert.False(result.ExecutedImmediately);
                Assert.Equal(ConfirmableActionStatus.Pending, action.Status);
                Assert.Equal(0, executionCount);
            }
            finally
            {
                CancelIfActive(action);
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
                CreateReviewContext(),
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
                CreateRequestContext(),
                _ => { });
            try
            {
                var result = await CopilotMcpConfirmationDecision.ApproveAsync(
                    CopilotMcpConfirmationStore.Instance,
                    action,
                    CreateReviewContext(),
                    CancellationToken.None);

                Assert.True(result.Success);
                Assert.False(result.ExecutedImmediately);
                Assert.Equal(ConfirmableActionStatus.Approved, action.Status);
                Assert.Contains("Agent 将在同一任务中继续执行", result.Message, StringComparison.Ordinal);

                Assert.False(CopilotMcpConfirmationStore.Instance.BeginAgentFrameworkAction(
                    action.ActionId,
                    CreateAgentRequest(),
                    @"C:\ColorVision\OtherWorkspace"));
                Assert.Equal(ConfirmableActionStatus.Approved, action.Status);
                Assert.True(CopilotMcpConfirmationStore.Instance.BeginAgentFrameworkAction(
                    action.ActionId,
                    CreateAgentRequest(),
                    WorkspacePath));
                Assert.Equal(ConfirmableActionStatus.Executing, action.Status);
            }
            finally
            {
                CancelIfActive(action);
            }
        }

        [Fact]
        public async Task ApprovedExternalActionRevalidatesCallerAndWorkspaceBeforeExecution()
        {
            const string callerSource = "tcp://127.0.0.1";
            var executionCount = 0;
            var action = CopilotMcpConfirmationStore.Instance.Create(
                "Apply external change",
                "Apply a protected external MCP change.",
                "confirmation-required",
                "external_change",
                "{\"value\":1}",
                _ =>
                {
                    executionCount++;
                    return Task.FromResult(CopilotMcpToolCallResult.Ok("executed"));
                },
                requestContext: new CopilotConfirmationRequestContext
                {
                    SourceKind = CopilotApprovalSourceKind.ExternalMcp,
                    RequestSource = callerSource,
                    WorkspacePath = WorkspacePath,
                    ImpactSummary = "Modifies the active workspace.",
                });
            try
            {
                Assert.True(CopilotMcpConfirmationStore.Instance.Approve(
                    action.ActionId,
                    new CopilotConfirmationReviewContext(string.Empty, string.Empty, WorkspacePath),
                    out _));

                var wrongCaller = await CopilotMcpConfirmationStore.Instance.ExecuteApprovedAsync(
                    action.ActionId,
                    action.ToolName,
                    action.ArgumentsSummary,
                    "tcp://127.0.0.2",
                    WorkspacePath,
                    CancellationToken.None);
                Assert.False(wrongCaller.Success);
                Assert.Equal("action_source_mismatch", wrongCaller.ErrorCode);
                Assert.Equal(ConfirmableActionStatus.Approved, action.Status);

                var wrongWorkspace = await CopilotMcpConfirmationStore.Instance.ExecuteApprovedAsync(
                    action.ActionId,
                    action.ToolName,
                    action.ArgumentsSummary,
                    callerSource,
                    @"C:\ColorVision\OtherWorkspace",
                    CancellationToken.None);
                Assert.False(wrongWorkspace.Success);
                Assert.Equal("action_workspace_mismatch", wrongWorkspace.ErrorCode);
                Assert.Equal(ConfirmableActionStatus.Approved, action.Status);

                var executed = await CopilotMcpConfirmationStore.Instance.ExecuteApprovedAsync(
                    action.ActionId,
                    action.ToolName,
                    action.ArgumentsSummary,
                    callerSource,
                    WorkspacePath,
                    CancellationToken.None);
                Assert.True(executed.Success);
                Assert.Equal(1, executionCount);
                Assert.Equal(ConfirmableActionStatus.Executed, action.Status);
            }
            finally
            {
                CancelIfActive(action);
            }
        }

        [Fact]
        public void ExternalActionCannotBeReboundToAnAgentTask()
        {
            var action = CopilotMcpConfirmationStore.Instance.Create(
                "External action",
                "External callers cannot transfer their approval to an in-app task.",
                "confirmation-required",
                "external_change",
                "{\"value\":2}",
                _ => Task.FromResult(CopilotMcpToolCallResult.Ok("executed")),
                requestContext: new CopilotConfirmationRequestContext
                {
                    SourceKind = CopilotApprovalSourceKind.ExternalMcp,
                    RequestSource = "tcp://127.0.0.1",
                    WorkspacePath = WorkspacePath,
                });
            try
            {
                Assert.False(CopilotMcpConfirmationStore.Instance.LinkAgentCall(
                    action.ActionId,
                    $"call-{Guid.NewGuid():N}",
                    CreateAgentRequest()));
                Assert.Equal(CopilotApprovalSourceKind.ExternalMcp, action.RequestContext.SourceKind);
                Assert.Empty(action.AgentCallId);
            }
            finally
            {
                CancelIfActive(action);
            }
        }

        [Fact]
        public async Task EmptyWorkspaceApprovalDoesNotBecomeApplicationWide()
        {
            const string callerSource = "tcp://127.0.0.1";
            var executionCount = 0;
            var action = CopilotMcpConfirmationStore.Instance.Create(
                "No-workspace external action",
                "An approval created without a workspace must remain bound to that empty scope.",
                "confirmation-required",
                "external_no_workspace_change",
                "{\"value\":3}",
                _ =>
                {
                    executionCount++;
                    return Task.FromResult(CopilotMcpToolCallResult.Ok("executed"));
                },
                requestContext: new CopilotConfirmationRequestContext
                {
                    SourceKind = CopilotApprovalSourceKind.ExternalMcp,
                    RequestSource = callerSource,
                    WorkspacePath = string.Empty,
                });
            try
            {
                Assert.False(CopilotMcpConfirmationStore.Instance.Approve(
                    action.ActionId,
                    new CopilotConfirmationReviewContext(string.Empty, string.Empty, WorkspacePath),
                    out _));
                Assert.Equal(ConfirmableActionStatus.Pending, action.Status);

                Assert.True(CopilotMcpConfirmationStore.Instance.Approve(
                    action.ActionId,
                    new CopilotConfirmationReviewContext(string.Empty, string.Empty, string.Empty),
                    out _));
                var wrongWorkspace = await CopilotMcpConfirmationStore.Instance.ExecuteApprovedAsync(
                    action.ActionId,
                    action.ToolName,
                    action.ArgumentsSummary,
                    callerSource,
                    WorkspacePath,
                    CancellationToken.None);
                Assert.False(wrongWorkspace.Success);
                Assert.Equal("action_workspace_mismatch", wrongWorkspace.ErrorCode);
                Assert.Equal(ConfirmableActionStatus.Approved, action.Status);

                var executed = await CopilotMcpConfirmationStore.Instance.ExecuteApprovedAsync(
                    action.ActionId,
                    action.ToolName,
                    action.ArgumentsSummary,
                    callerSource,
                    string.Empty,
                    CancellationToken.None);
                Assert.True(executed.Success);
                Assert.Equal(1, executionCount);
            }
            finally
            {
                CancelIfActive(action);
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
                executeOnApproval,
                requestContext: CreateRequestContext());
        }

        private static CopilotConfirmationRequestContext CreateRequestContext() =>
            new()
            {
                SourceKind = CopilotApprovalSourceKind.InAppAgent,
                RequestSource = "in-app-agent",
                ConversationId = ConversationId,
                TaskId = TaskId,
                TaskLabel = "检查并应用桌宠模板",
                WorkspacePath = WorkspacePath,
                ImpactSummary = "将修改当前工作区的桌宠设置。",
                Reversibility = CopilotApprovalReversibility.ManualOnly,
                ReversibilitySummary = "需要手动恢复原设置。",
            };

        private static CopilotConfirmationReviewContext CreateReviewContext() =>
            new(ConversationId, TaskId, WorkspacePath);

        private static CopilotAgentRequest CreateAgentRequest() =>
            new()
            {
                ConversationId = ConversationId,
                TaskId = TaskId,
                WorkspacePath = WorkspacePath,
                UserText = "检查并应用桌宠模板",
                TaskIntentText = "检查并应用桌宠模板",
            };

        private static void CancelIfActive(ConfirmableAction action)
        {
            CopilotMcpConfirmationStore.Instance.Cancel(action.ActionId, out _);
        }
    }
}
