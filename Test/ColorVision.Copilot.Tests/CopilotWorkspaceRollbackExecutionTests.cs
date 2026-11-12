using ColorVision.Copilot.Mcp;
using System.Collections.Concurrent;
using System.IO;
using System.Text;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotWorkspaceRollbackExecutionTests : IDisposable
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);
    private const string ConversationId = "direct-rollback-execution";
    private readonly string _workspace = Directory.CreateTempSubdirectory("CopilotDirectRollback-").FullName;
    private readonly List<ConfirmableAction> _actions = [];

    [Theory]
    [InlineData("success")]
    [InlineData("save-failure")]
    [InlineData("cancel")]
    public async Task ApprovedDirectRollbackWaitsForTheCandidatePathCheckpoint(string outcome)
    {
        var change = await PrepareAppliedChangeAsync();
        var observer = new ExecutionObserver(change.Path, change.Patched);
        var coordinator = CreateCoordinator(change.Patches, observer);
        var events = new ConcurrentQueue<CopilotAgentEvent>();
        var candidateSnapshots = new ConcurrentQueue<string[]>();
        var message = new CopilotChatMessage(CopilotChatRole.Assistant, "Direct rollback execution.");
        var conversation = CopilotConversationRecord.CreateEmpty("rollback-profile", "Rollback profile");
        conversation.Id = ConversationId;
        conversation.Messages.Add(message);
        var state = new CopilotChatState { Conversations = [conversation] };
        var stateStore = new CopilotChatStateStore(Path.Combine(_workspace, "chat-state"));
        var presentationGate = new object();
        var checkpointEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseCheckpoint = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var checkpointSettled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var checkpointCount = 0;

        void SaveCheckpoint(bool containsCandidatePaths)
        {
            lock (presentationGate)
            {
                Assert.Equal(change.Patched, File.ReadAllBytes(change.Path));
                var trace = Assert.Single(message.AgentTraceEntries);
                Assert.Equal(CopilotToolExecutionState.Running, trace.State);
                if (containsCandidatePaths)
                    Assert.Equal(new[] { change.Path }, trace.WorkspaceRecheckPaths);
                else
                    Assert.Empty(trace.WorkspaceRecheckPaths);
                stateStore.Save(state);
                Assert.Equal(change.Patched, File.ReadAllBytes(change.Path));
            }
        }

        async Task PersistBeforeWriteAsync(CancellationToken token)
        {
            if (Interlocked.Increment(ref checkpointCount) == 1)
            {
                SaveCheckpoint(containsCandidatePaths: false);
                return;
            }
            checkpointEntered.TrySetResult();
            try
            {
                await releaseCheckpoint.Task.WaitAsync(token);
                if (outcome == "save-failure")
                    throw new IOException("The host could not commit its candidate-path checkpoint.");
                SaveCheckpoint(containsCandidatePaths: true);
            }
            finally { checkpointSettled.TrySetResult(); }
        }

        void OnEvent(CopilotAgentEvent agentEvent)
        {
            lock (presentationGate)
            {
                events.Enqueue(agentEvent);
                CopilotAssistantMessagePresenter.ApplyAgentEvent(message, agentEvent);
                if (agentEvent.Type == CopilotAgentEventType.ToolProgress
                    && agentEvent.ToolExecution?.WorkspaceRecheckPaths.Count > 0)
                {
                    Assert.Equal(change.Patched, File.ReadAllBytes(change.Path));
                    candidateSnapshots.Enqueue(agentEvent.ToolExecution.WorkspaceRecheckPaths.ToArray());
                }
            }
        }

        var prepared = await coordinator.RequestAsync(new CopilotWorkspaceRollbackActionRequest(
            ConversationId, _workspace, change.ChangeSetId) { StatePersistenceBarrier = PersistBeforeWriteAsync },
            OnEvent, CancellationToken.None);
        Assert.True(prepared.Success, prepared.ErrorMessage);
        var action = Assert.IsType<ConfirmableAction>(prepared.Action);
        _actions.Add(action);
        Assert.Equal(ConfirmableActionStatus.Pending, action.Status);
        Assert.True(action.ExecuteOnApproval);
        Assert.Equal(CopilotApprovalSourceKind.ColorVisionUi, action.RequestContext.SourceKind);
        Assert.Equal(ConversationId, action.RequestContext.ConversationId);
        Assert.True(action.RequestContext.ResolveExecutionScope().HasToolCallBinding);
        Assert.NotEmpty(action.ArgumentsDigest);
        Assert.Equal(change.Patched, await File.ReadAllBytesAsync(change.Path));
        Assert.Empty(observer.Invocations);
        Assert.Equal(0, checkpointCount);
        var waiting = Assert.Single(events);
        Assert.Equal(CopilotToolExecutionState.AwaitingApproval, waiting.ToolExecution!.State);
        Assert.Equal(action.ActionId, waiting.ToolResult!.Approval!.ActionId);

        using var cancellation = new CancellationTokenSource();
        var execution = CopilotMcpConfirmationStore.Instance.ApproveAndExecuteAsync(action.ActionId,
            new CopilotConfirmationReviewContext(ConversationId, string.Empty, _workspace), cancellation.Token);
        try
        {
            await checkpointEntered.Task.WaitAsync(TestTimeout);
            Assert.False(execution.IsCompleted);
            Assert.Equal(change.Patched, await File.ReadAllBytesAsync(change.Path));
            Assert.Equal(2, Volatile.Read(ref checkpointCount));
            Assert.NotNull(Assert.Single(observer.Invocations).PreDispatchCheckpoint);
            Assert.Contains(candidateSnapshots, paths => paths.SequenceEqual(new[] { change.Path }));
            if (outcome == "cancel")
                cancellation.Cancel();
            else
                releaseCheckpoint.TrySetResult();

            var result = await execution.WaitAsync(TestTimeout);
            await checkpointSettled.Task.WaitAsync(TestTimeout);

            Assert.Equal(outcome == "success", result.Success);
            Assert.Equal(ConfirmableActionStatus.Executed, action.Status);
            Assert.Equal(outcome == "success", action.ExecutionSucceeded);
            Assert.Equal(outcome == "success" ? change.Original : change.Patched,
                await File.ReadAllBytesAsync(change.Path));
            if (outcome == "success")
            {
                Assert.Contains(events, agentEvent => agentEvent.Type == CopilotAgentEventType.ToolResult
                    && agentEvent.ToolExecution?.State == CopilotToolExecutionState.Completed
                    && agentEvent.ToolResult?.Success == true);
                var savedMessage = Assert.Single(Assert.Single(stateStore.Load().Conversations).Messages);
                Assert.Equal(new[] { change.Path }, Assert.Single(savedMessage.AgentTraceEntries).WorkspaceRecheckPaths);
            }
            else if (outcome == "save-failure")
            {
                Assert.Equal("tool_dispatch_checkpoint_failed", result.ErrorCode);
                Assert.Equal(CopilotToolFailureKind.Transient, result.FailureKind);
            }
            else
            {
                Assert.NotEqual(CopilotToolFailureCode.OutcomeUnknown, result.ErrorCode);
                Assert.Equal(CopilotToolFailureKind.Cancelled, result.FailureKind);
                Assert.Contains(events, agentEvent => agentEvent.Type == CopilotAgentEventType.ToolResult
                    && agentEvent.ToolExecution?.State == CopilotToolExecutionState.Cancelled
                    && agentEvent.ToolResult?.FailureKind == CopilotToolFailureKind.Cancelled
                    && agentEvent.ToolResult.WorkspaceRecheckPaths.Count == 0);
            }
        }
        finally
        {
            cancellation.Cancel();
            releaseCheckpoint.TrySetResult();
            await execution.WaitAsync(TestTimeout);
            if (checkpointEntered.Task.IsCompleted)
                await checkpointSettled.Task.WaitAsync(TestTimeout);
        }
    }

    [Fact]
    public async Task DirectRollbackWithoutAHostCheckpointCannotCreateAnApprovalAction()
    {
        var change = await PrepareAppliedChangeAsync();
        var observer = new ExecutionObserver(change.Path, change.Patched);
        var events = new ConcurrentQueue<CopilotAgentEvent>();

        var result = await CreateCoordinator(change.Patches, observer).RequestAsync(
            new CopilotWorkspaceRollbackActionRequest(ConversationId, _workspace, change.ChangeSetId),
            events.Enqueue, CancellationToken.None);

        if (result.Action != null)
            _actions.Add(result.Action);
        Assert.False(result.Success);
        Assert.Null(result.Action);
        Assert.Contains("checkpoint", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(observer.Invocations);
        Assert.Empty(events);
        Assert.Equal(change.Patched, await File.ReadAllBytesAsync(change.Path));
    }

    [Fact]
    public async Task CancellationAfterTheRollbackBodyKeepsItsUnknownOutcomeWhenTheRealWriteFinishesLate()
    {
        var change = await PrepareAppliedChangeAsync();
        var tool = new HeldRollbackTool(new CopilotRollbackWorkspacePatchEnvelopeTool(change.Patches));
        var executor = new CopilotToolExecutor(hooks: []);
        var coordinator = new CopilotWorkspaceRollbackCoordinator(new CopilotToolRegistry([tool]), executor);
        var events = new ConcurrentQueue<CopilotAgentEvent>();
        var checkpoints = 0;
        var prepared = await coordinator.RequestAsync(new CopilotWorkspaceRollbackActionRequest(
            ConversationId, _workspace, change.ChangeSetId)
        {
            StatePersistenceBarrier = _ =>
            {
                Interlocked.Increment(ref checkpoints);
                Assert.Equal(change.Patched, File.ReadAllBytes(change.Path));
                return Task.CompletedTask;
            },
        }, events.Enqueue, CancellationToken.None);
        Assert.True(prepared.Success, prepared.ErrorMessage);
        var action = Assert.IsType<ConfirmableAction>(prepared.Action);
        _actions.Add(action);
        using var cancellation = new CancellationTokenSource();
        var execution = CopilotMcpConfirmationStore.Instance.ApproveAndExecuteAsync(action.ActionId,
            new CopilotConfirmationReviewContext(ConversationId, string.Empty, _workspace), cancellation.Token);
        try
        {
            await tool.Entered.Task.WaitAsync(TestTimeout);
            Assert.Equal(2, Volatile.Read(ref checkpoints));
            Assert.Equal(change.Original, await File.ReadAllBytesAsync(change.Path));
            Assert.False(tool.Settled.Task.IsCompleted);

            cancellation.Cancel();
            var result = await execution.WaitAsync(TestTimeout);

            Assert.False(result.Success);
            Assert.Equal(CopilotToolFailureKind.OutcomeUnknown, result.FailureKind);
            Assert.Equal(CopilotToolFailureCode.OutcomeUnknown, result.ErrorCode);
            Assert.Equal(ConfirmableActionStatus.Executed, action.Status);
            Assert.False(action.ExecutionSucceeded);
            var terminal = Assert.Single(events, agentEvent => agentEvent.Type == CopilotAgentEventType.ToolResult
                && agentEvent.ToolExecution?.State != CopilotToolExecutionState.AwaitingApproval);
            Assert.Equal(CopilotToolExecutionState.Interrupted, terminal.ToolExecution!.State);
            Assert.Equal(CopilotToolFailureCode.OutcomeUnknown, terminal.ToolResult!.FailureCode);
            Assert.Equal(new[] { change.Path }, terminal.ToolResult.WorkspaceRecheckPaths);
            Assert.Null(terminal.ToolResult.WorkspaceMutation);
            var recordedResult = action.ExecutionResultText;

            tool.Release.TrySetResult();
            await tool.Settled.Task.WaitAsync(TestTimeout);
            // The read waits for the executor's held exclusive lease, proving the
            // late write task has fully settled before its terminal fact is checked.
            var read = await executor.ExecuteAsync(new CopilotToolInvocation
            {
                Tool = new CopilotReadLocalFileTool(), ToolInput = new() { Path = change.Path },
                AgentRequest = new CopilotAgentRequest
                {
                    WorkspacePath = _workspace, SearchRootPaths = [_workspace], ReadableLocalFilePaths = [change.Path],
                    Mode = CopilotAgentMode.Auto, UserText = "Read the completed rollback's current file.",
                },
            }, _ => { }, CancellationToken.None).WaitAsync(TestTimeout);
            Assert.True(read.Result.Success, read.Result.ErrorMessage);
            Assert.Same(terminal, Assert.Single(events, agentEvent => agentEvent.Type == CopilotAgentEventType.ToolResult
                && agentEvent.ToolExecution?.State != CopilotToolExecutionState.AwaitingApproval));
            Assert.Equal(recordedResult, action.ExecutionResultText);
            Assert.False(action.ExecutionSucceeded);
            Assert.Equal(change.Original, await File.ReadAllBytesAsync(change.Path));
        }
        finally
        {
            cancellation.Cancel();
            tool.Release.TrySetResult();
            await execution.WaitAsync(TestTimeout);
            if (tool.Entered.Task.IsCompleted)
                await tool.Settled.Task.WaitAsync(TestTimeout);
        }
    }

    private static CopilotWorkspaceRollbackCoordinator CreateCoordinator(CopilotWorkspacePatchStore patches,
        ExecutionObserver observer) => new(new CopilotToolRegistry([new CopilotRollbackWorkspacePatchEnvelopeTool(patches)]),
            new CopilotToolExecutor(hooks: [observer]));

    private async Task<AppliedChange> PrepareAppliedChangeAsync()
    {
        var path = Path.Combine(_workspace, "document.txt");
        var original = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetPreamble()
            .Concat(Encoding.UTF8.GetBytes("original value\r\n")).ToArray();
        await File.WriteAllBytesAsync(path, original);
        var request = new CopilotAgentRequest
        {
            ConversationId = ConversationId, TaskId = "original-patch", WorkspacePath = _workspace,
            Mode = CopilotAgentMode.Auto, UserText = "Change the document value and verify the result.",
            WritableLocalRootPaths = [_workspace], SearchRootPaths = [_workspace],
            TrustedProjectRootPaths = [_workspace], CodexHooksEnabled = false,
        };
        var patches = new CopilotWorkspacePatchStore();
        var preview = await patches.PreviewPatchEnvelopeAsync(request, new CopilotAgentToolInput
        {
            Arguments = new Dictionary<string, object?>
            {
                ["operations"] = new[] { new
                {
                    operation = "update", path,
                    replacements = new[] { new { oldText = "original value", newText = "patched value" } },
                } },
            },
        }, CancellationToken.None);
        Assert.True(preview.Success, preview.ErrorMessage);
        Assert.Equal(original, await File.ReadAllBytesAsync(path));
        var changeSetId = preview.Content.Split('\n')
            .Single(line => line.StartsWith("change_set_id:", StringComparison.Ordinal))["change_set_id:".Length..].Trim();
        var applied = await patches.ApplyPatchEnvelopeAsync(request, new CopilotAgentToolInput
        {
            Arguments = new Dictionary<string, object?> { ["changeSetId"] = changeSetId },
        }, CancellationToken.None);
        Assert.True(applied.Success, applied.ErrorMessage);
        var patched = await File.ReadAllBytesAsync(path);
        Assert.NotEqual(original, patched);
        return new(patches, path, original, patched, changeSetId);
    }

    private sealed record AppliedChange(CopilotWorkspacePatchStore Patches, string Path, byte[] Original,
        byte[] Patched, string ChangeSetId);

    private sealed class ExecutionObserver(string path, byte[] expectedBeforeWrite) : ICopilotToolExecutionHook
    {
        public ConcurrentQueue<CopilotToolInvocation> Invocations { get; } = new();
        public Task<CopilotToolExecutionHookDecision> BeforeExecuteAsync(CopilotToolExecutionHookContext context,
            CancellationToken cancellationToken)
        {
            Assert.Equal(expectedBeforeWrite, File.ReadAllBytes(path));
            Invocations.Enqueue(context.Invocation);
            return Task.FromResult(CopilotToolExecutionHookDecision.Proceed);
        }
        public Task AfterExecuteAsync(CopilotToolExecutionOutcome outcome, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class HeldRollbackTool(CopilotRollbackWorkspacePatchEnvelopeTool inner)
        : ICopilotFrameworkApprovedTool, ICopilotFrameworkApprovalPresentation, ICopilotAgentDrivenTool,
            ICopilotWorkspaceMutationEvidenceSource
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Settled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string Name => inner.Name;
        public string Description => inner.Description;
        public CopilotToolCapabilityDescriptor Capability => inner.Capability;
        public CopilotToolInputSchema InputSchema => inner.InputSchema;
        public bool CanHandle(CopilotAgentRequest request) => inner.CanHandle(request);
        public bool IsAvailable(CopilotAgentRequest request) => inner.IsAvailable(request);
        public string GetConcurrencyKey(CopilotAgentRequest request, CopilotAgentToolInput input) => inner.GetConcurrencyKey(request, input);
        public CopilotToolApprovalPresentation CreateApprovalPresentation(CopilotAgentToolInput input) => inner.CreateApprovalPresentation(input);
        public IReadOnlyList<string> GetWorkspaceRecheckPaths(CopilotAgentRequest request, CopilotAgentToolInput input) =>
            ((ICopilotWorkspaceMutationEvidenceSource)inner).GetWorkspaceRecheckPaths(request, input);
        public Task<CopilotToolResult> ExecuteAsync(CopilotAgentRequest request, CopilotAgentToolInput input, CancellationToken token) =>
            inner.ExecuteAsync(request, input, token);
        public async Task<CopilotToolResult> ExecuteApprovedAsync(CopilotAgentRequest request, CopilotAgentToolInput input,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await ((ICopilotFrameworkApprovedTool)inner).ExecuteApprovedAsync(request, input, cancellationToken);
                Assert.True(result.Success, result.ErrorMessage);
                Entered.TrySetResult();
                await Release.Task;
                return result;
            }
            finally { Settled.TrySetResult(); }
        }
    }

    public void Dispose()
    {
        foreach (var action in _actions)
            CopilotMcpConfirmationStore.Instance.Cancel(action.ActionId, out _);
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(_workspace));
        var temp = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
        Assert.True(string.Equals(Path.GetDirectoryName(root), temp, StringComparison.OrdinalIgnoreCase)
            && Path.GetFileName(root).StartsWith("CopilotDirectRollback-", StringComparison.Ordinal));
        Directory.Delete(root, recursive: true);
    }
}
