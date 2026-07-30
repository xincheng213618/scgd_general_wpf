using ColorVision.Copilot;
using ColorVision.Copilot.Mcp;
using System.IO;

namespace ColorVision.UI.Tests
{
    public sealed class CopilotWorkspaceRollbackCoordinatorTests
    {
        [Fact]
        public async Task DirectUiApprovalRollsBackExactChangeSetWithoutAnotherAgentTurn()
        {
            var workspacePath = Path.Combine(
                Path.GetTempPath(),
                "ColorVisionDirectRollback",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(workspacePath);
            ConfirmableAction? action = null;
            try
            {
                var targetPath = Path.Combine(workspacePath, "target.txt");
                await File.WriteAllTextAsync(targetPath, "before");
                var request = new CopilotAgentRequest
                {
                    ConversationId = "direct-rollback-conversation",
                    TaskId = "apply-change-set-task",
                    WorkspacePath = workspacePath,
                    UserText = "Update the requested workspace file.",
                    TaskIntentText = "Update the requested workspace file.",
                    SearchRootPaths = [workspacePath],
                    TrustedProjectRootPaths = [workspacePath],
                    WritableLocalRootPaths = [workspacePath],
                    Mode = CopilotAgentMode.Auto,
                };
                var patchStore = new CopilotWorkspacePatchStore();
                var preview = await patchStore.PreviewPatchEnvelopeAsync(
                    request,
                    new CopilotAgentToolInput
                    {
                        Arguments = new Dictionary<string, object?>
                        {
                            ["operations"] = new[]
                            {
                                new
                                {
                                    operation = "update",
                                    path = "target.txt",
                                    oldText = "before",
                                    newText = "after",
                                },
                            },
                        },
                    },
                    CancellationToken.None);
                Assert.True(preview.Success, preview.ErrorMessage);
                var changeSetId = ReadMetadata(preview.Content, "change_set_id");
                var changeSetInput = new CopilotAgentToolInput
                {
                    Arguments = new Dictionary<string, object?>
                    {
                        ["changeSetId"] = changeSetId,
                    },
                };
                var applied = await patchStore.ApplyPatchEnvelopeAsync(
                    request,
                    changeSetInput,
                    CancellationToken.None);
                Assert.True(applied.Success, applied.ErrorMessage);
                Assert.Equal("after", await File.ReadAllTextAsync(targetPath));

                var rollbackTool = new CopilotRollbackWorkspacePatchEnvelopeTool(patchStore);
                var coordinator = new CopilotWorkspaceRollbackCoordinator(
                    new CopilotToolRegistry([rollbackTool]),
                    new CopilotToolExecutor(Array.Empty<ICopilotToolExecutionHook>()));
                var events = new List<CopilotAgentEvent>();
                var result = await coordinator.RequestAsync(
                    new CopilotWorkspaceRollbackActionRequest(
                        request.ConversationId,
                        workspacePath,
                        changeSetId),
                    events.Add,
                    CancellationToken.None);

                Assert.True(result.Success, result.ErrorMessage);
                action = Assert.IsType<ConfirmableAction>(result.Action);
                Assert.Equal(ConfirmableActionStatus.Pending, action.Status);
                Assert.True(action.ExecuteOnApproval);
                Assert.False(action.ResumesAgentOnApproval);
                Assert.Equal(CopilotApprovalSourceKind.ColorVisionUi, action.RequestContext.SourceKind);
                Assert.Equal(workspacePath, action.RequestContext.WorkspacePath);
                Assert.StartsWith("ui-rollback-", action.AgentCallId, StringComparison.Ordinal);
                Assert.Equal("after", await File.ReadAllTextAsync(targetPath));

                var awaiting = Assert.Single(events);
                Assert.Equal(CopilotAgentEventType.ToolResult, awaiting.Type);
                Assert.Equal(CopilotToolExecutionState.AwaitingApproval, awaiting.ToolExecution?.State);
                Assert.Equal(action.ActionId, awaiting.ToolExecution?.ApprovalActionId);
                Assert.Equal(action.AgentCallId, awaiting.ToolExecution?.CallId);
                Assert.Equal(changeSetId, ReadMetadata(awaiting.ToolResult?.Content, "change_set_id"));

                var wrongConversationApproval = await CopilotMcpConfirmationDecision.ApproveAsync(
                    CopilotMcpConfirmationStore.Instance,
                    action,
                    new CopilotConfirmationReviewContext(
                        "another-conversation",
                        string.Empty,
                        workspacePath),
                    CancellationToken.None);
                Assert.False(wrongConversationApproval.Success);
                Assert.Equal(ConfirmableActionStatus.Pending, action.Status);
                Assert.Equal("after", await File.ReadAllTextAsync(targetPath));

                var approval = await CopilotMcpConfirmationDecision.ApproveAsync(
                    CopilotMcpConfirmationStore.Instance,
                    action,
                    new CopilotConfirmationReviewContext(
                        request.ConversationId,
                        string.Empty,
                        workspacePath),
                    CancellationToken.None);

                Assert.True(approval.Success, approval.Message);
                Assert.True(approval.ExecutedImmediately);
                Assert.Equal(ConfirmableActionStatus.Executed, action.Status);
                Assert.Equal("before", await File.ReadAllTextAsync(targetPath));
                var completed = Assert.Single(events.Where(agentEvent =>
                    agentEvent.Type == CopilotAgentEventType.ToolResult
                    && agentEvent.ToolExecution?.State == CopilotToolExecutionState.Completed));
                Assert.True(completed.ToolResult?.Success);
                Assert.Equal(changeSetId, ReadMetadata(completed.ToolResult?.Content, "change_set_id"));
                Assert.All(events.Where(agentEvent => agentEvent.ToolExecution != null), agentEvent =>
                    Assert.Equal(action.AgentCallId, agentEvent.ToolExecution?.CallId));
            }
            finally
            {
                if (action?.Status is ConfirmableActionStatus.Pending or ConfirmableActionStatus.Approved)
                    CopilotMcpConfirmationStore.Instance.Cancel(action.ActionId, out _, "Test cleanup.");
                Directory.Delete(workspacePath, recursive: true);
            }
        }

        [Fact]
        public async Task InvalidChangeSetDoesNotCreateApprovalOrEmitTrace()
        {
            var workspacePath = Path.Combine(
                Path.GetTempPath(),
                "ColorVisionDirectRollback",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(workspacePath);
            try
            {
                var coordinator = new CopilotWorkspaceRollbackCoordinator(
                    new CopilotToolRegistry([
                        new CopilotRollbackWorkspacePatchEnvelopeTool(
                            new CopilotWorkspacePatchStore()),
                    ]),
                    new CopilotToolExecutor(Array.Empty<ICopilotToolExecutionHook>()));
                var events = new List<CopilotAgentEvent>();

                var result = await coordinator.RequestAsync(
                    new CopilotWorkspaceRollbackActionRequest(
                        "conversation",
                        workspacePath,
                        "not-a-change-set"),
                    events.Add,
                    CancellationToken.None);

                Assert.False(result.Success);
                Assert.Null(result.Action);
                Assert.Contains("identifier", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
                Assert.Empty(events);
            }
            finally
            {
                Directory.Delete(workspacePath, recursive: true);
            }
        }

        private static string ReadMetadata(string? content, string key)
        {
            var prefix = key + ":";
            return (content ?? string.Empty)
                .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
                .Single(line => line.StartsWith(prefix, StringComparison.Ordinal))
                [prefix.Length..]
                .Trim();
        }
    }
}
