using ColorVision.Copilot;
using ColorVision.Copilot.Mcp;
using System.IO;
using System.Text;

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
        public async Task AppliedChangeSetSurvivesRestartInUserEncryptedCheckpoint()
        {
            var testRoot = Path.Combine(
                Path.GetTempPath(),
                "ColorVisionDurableRollback",
                Guid.NewGuid().ToString("N"));
            var workspacePath = Path.Combine(testRoot, "Workspace");
            var checkpointPath = Path.Combine(testRoot, "Checkpoints");
            Directory.CreateDirectory(workspacePath);
            try
            {
                const string beforeText = "checkpoint-before-secret";
                const string afterText = "checkpoint-after-secret";
                const string deletedText = "checkpoint-deleted-secret";
                const string createdText = "checkpoint-created-secret";
                var targetPath = Path.Combine(workspacePath, "target.txt");
                var deletedPath = Path.Combine(workspacePath, "deleted.txt");
                var createdPath = Path.Combine(workspacePath, "nested", "created.txt");
                await File.WriteAllTextAsync(targetPath, beforeText);
                await File.WriteAllTextAsync(deletedPath, deletedText);
                var request = CreateWorkspaceRequest(
                    "durable-rollback-conversation",
                    workspacePath);
                var firstStore = new CopilotWorkspacePatchStore(
                    new CopilotWorkspaceChangeSetCheckpointStore(checkpointPath));
                var preview = await firstStore.PreviewPatchEnvelopeAsync(
                    request,
                    new CopilotAgentToolInput
                    {
                        Arguments = new Dictionary<string, object?>
                        {
                            ["operations"] = new object[]
                            {
                                new
                                {
                                    operation = "update",
                                    path = "target.txt",
                                    oldText = beforeText,
                                    newText = afterText,
                                },
                                new
                                {
                                    operation = "add",
                                    path = "nested/created.txt",
                                    content = createdText,
                                },
                                new
                                {
                                    operation = "delete",
                                    path = "deleted.txt",
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

                var applied = await firstStore.ApplyPatchEnvelopeAsync(
                    request,
                    changeSetInput,
                    CancellationToken.None);

                Assert.True(applied.Success, applied.ErrorMessage);
                Assert.Equal(afterText, await File.ReadAllTextAsync(targetPath));
                Assert.Equal(createdText, await File.ReadAllTextAsync(createdPath));
                Assert.False(File.Exists(deletedPath));
                var checkpointFile = Assert.Single(Directory.GetFiles(
                    checkpointPath,
                    "*.checkpoint",
                    SearchOption.TopDirectoryOnly));
                var encryptedHex = Convert.ToHexString(await File.ReadAllBytesAsync(checkpointFile));
                Assert.DoesNotContain(
                    Convert.ToHexString(Encoding.UTF8.GetBytes(beforeText)),
                    encryptedHex,
                    StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(
                    Convert.ToHexString(Encoding.UTF8.GetBytes(afterText)),
                    encryptedHex,
                    StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(
                    Convert.ToHexString(Encoding.UTF8.GetBytes(deletedText)),
                    encryptedHex,
                    StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(
                    Convert.ToHexString(Encoding.UTF8.GetBytes(createdText)),
                    encryptedHex,
                    StringComparison.OrdinalIgnoreCase);

                var restartedStore = new CopilotWorkspacePatchStore(
                    new CopilotWorkspaceChangeSetCheckpointStore(checkpointPath));
                var presentation = restartedStore.CreateChangeSetApprovalPresentation(
                    changeSetInput,
                    rollback: true);
                Assert.Contains("target.txt", presentation.ReviewDetails, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(
                    "no longer available",
                    presentation.Description,
                    StringComparison.OrdinalIgnoreCase);

                var wrongConversation = CreateWorkspaceRequest(
                    "different-conversation",
                    workspacePath);
                var rejected = await restartedStore.RollbackPatchEnvelopeAsync(
                    wrongConversation,
                    changeSetInput,
                    CancellationToken.None);
                Assert.False(rejected.Success);
                Assert.Equal(CopilotToolFailureKind.Authorization, rejected.FailureKind);
                Assert.Equal(afterText, await File.ReadAllTextAsync(targetPath));
                Assert.Equal(createdText, await File.ReadAllTextAsync(createdPath));
                Assert.False(File.Exists(deletedPath));

                var differentWorkspacePath = Path.Combine(testRoot, "DifferentWorkspace");
                Directory.CreateDirectory(differentWorkspacePath);
                var wrongWorkspace = CreateWorkspaceRequest(
                    request.ConversationId,
                    differentWorkspacePath);
                rejected = await restartedStore.RollbackPatchEnvelopeAsync(
                    wrongWorkspace,
                    changeSetInput,
                    CancellationToken.None);
                Assert.False(rejected.Success);
                Assert.Equal(CopilotToolFailureKind.Authorization, rejected.FailureKind);
                Assert.Equal(afterText, await File.ReadAllTextAsync(targetPath));

                var rolledBack = await restartedStore.RollbackPatchEnvelopeAsync(
                    request,
                    changeSetInput,
                    CancellationToken.None);

                Assert.True(rolledBack.Success, rolledBack.ErrorMessage);
                Assert.Equal(beforeText, await File.ReadAllTextAsync(targetPath));
                Assert.False(File.Exists(createdPath));
                Assert.False(Directory.Exists(Path.GetDirectoryName(createdPath)));
                Assert.Equal(deletedText, await File.ReadAllTextAsync(deletedPath));
                Assert.Empty(Directory.GetFiles(
                    checkpointPath,
                    "*.checkpoint",
                    SearchOption.TopDirectoryOnly));
            }
            finally
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }

        [Fact]
        public void CorruptCheckpointIsDiscardedWithoutBreakingRollbackReview()
        {
            var checkpointPath = Path.Combine(
                Path.GetTempPath(),
                "ColorVisionCorruptRollback",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(checkpointPath);
            try
            {
                var changeSetSuffix = Guid.NewGuid().ToString("N");
                var changeSetId = "workspace-change-set:" + changeSetSuffix;
                File.WriteAllBytes(
                    Path.Combine(checkpointPath, changeSetSuffix + ".checkpoint"),
                    [0x01, 0x02, 0x03, 0x04]);
                var restartedStore = new CopilotWorkspacePatchStore(
                    new CopilotWorkspaceChangeSetCheckpointStore(checkpointPath));

                var presentation = restartedStore.CreateChangeSetApprovalPresentation(
                    new CopilotAgentToolInput
                    {
                        Arguments = new Dictionary<string, object?>
                        {
                            ["changeSetId"] = changeSetId,
                        },
                    },
                    rollback: true);

                Assert.Contains(
                    "no longer available",
                    presentation.Description,
                    StringComparison.OrdinalIgnoreCase);
                Assert.Empty(Directory.GetFiles(
                    checkpointPath,
                    "*.checkpoint",
                    SearchOption.TopDirectoryOnly));
            }
            finally
            {
                Directory.Delete(checkpointPath, recursive: true);
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

        private static CopilotAgentRequest CreateWorkspaceRequest(
            string conversationId,
            string workspacePath)
        {
            return new CopilotAgentRequest
            {
                ConversationId = conversationId,
                TaskId = "durable-workspace-change",
                WorkspacePath = workspacePath,
                UserText = "Update the requested workspace file.",
                TaskIntentText = "Update the requested workspace file.",
                SearchRootPaths = [workspacePath],
                TrustedProjectRootPaths = [workspacePath],
                WritableLocalRootPaths = [workspacePath],
                Mode = CopilotAgentMode.Auto,
            };
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
