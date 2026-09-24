using System.Collections.Concurrent;
using System.IO;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotWorkspaceCancellationEvidenceTests : IDisposable
{
    private readonly string _workspace = Directory.CreateTempSubdirectory("CopilotCancelledWorkspace-").FullName;
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public async Task CancelledOrTimedOutPatchKeepsFrozenPathsWithoutInventingAnOutcome(bool timeout, bool rollback, bool writesBeforeBoundary)
    {
        var (store, request, input, paths) = await PrepareAsync();
        if (rollback) Assert.True((await store.ApplyPatchEnvelopeAsync(request, input, CancellationToken.None)).Success);
        ICopilotFrameworkApprovedTool inner = rollback ? new CopilotRollbackWorkspacePatchEnvelopeTool(store) : new CopilotApplyWorkspacePatchEnvelopeTool(store);
        var tool = new HeldWorkspaceTool(inner, timeout ? TimeSpan.FromMilliseconds(500) : TestTimeout, writesBeforeBoundary);
        using var cancellation = new CancellationTokenSource();
        var events = new ConcurrentQueue<CopilotAgentEvent>();
        var executor = new CopilotToolExecutor(hooks: []);
        var execution = executor.ExecuteAsync(Invocation(tool, request, input), events.Enqueue, cancellation.Token);
        try
        {
            await tool.Entered.Task.WaitAsync(TestTimeout);
            CopilotToolExecutionOutcome outcome;
            if (timeout) outcome = await execution.WaitAsync(TestTimeout);
            else
            {
                cancellation.Cancel();
                var exception = await Assert.ThrowsAsync<CopilotToolExecutionCancellationException>(() => execution.WaitAsync(TestTimeout));
                outcome = exception.Outcome;
            }
            Assert.Equal(timeout ? CopilotToolExecutionState.TimedOut : CopilotToolExecutionState.Interrupted, outcome.Execution.State);
            Assert.Equal(CopilotToolFailureKind.OutcomeUnknown, outcome.Result.FailureKind);
            Assert.False(outcome.Execution.RetryEligible);
            Assert.Equal(paths.Order(), outcome.Result.WorkspaceRecheckPaths.Order());
            Assert.Null(outcome.Result.WorkspaceMutation);
            Assert.Contains("not proof of completed writes", outcome.Result.Content, StringComparison.Ordinal);
            foreach (var path in paths) Assert.Contains(path, outcome.Result.Content, StringComparison.Ordinal);
            Assert.False(tool.Settled.Task.IsCompleted);
            Assert.Equal(1, tool.MetadataCalls);
            Assert.Equal(rollback != writesBeforeBoundary ? "patched" : "original", File.ReadAllText(paths[0]));
            var terminal = Assert.Single(events, e => e.Type == CopilotAgentEventType.ToolResult);
            var accumulator = new CopilotTurnWorkspaceDiffAccumulator(_workspace);
            Assert.True(accumulator.Observe(terminal, out var snapshot));
            Assert.Empty(snapshot.Diff);
            Assert.Contains("2 个文件", snapshot.VerificationWarning, StringComparison.Ordinal);
            var assistant = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty);
            CopilotAssistantMessagePresenter.ApplyWorkspaceDiffUpdated(assistant, snapshot);
            Assert.True(assistant.HasWorkspaceDiffWarning);

            // Late worker completion must not replace the already published unknown outcome.
            tool.Release.TrySetResult();
            await tool.Settled.Task.WaitAsync(TestTimeout);
            Assert.Equal(rollback ? "original" : "patched", File.ReadAllText(paths[0]));
            Assert.Single(events, e => e.Type == CopilotAgentEventType.ToolResult);
            Assert.Equal(CopilotToolFailureKind.OutcomeUnknown, terminal.ToolResult!.FailureKind);
            Assert.Equal(1, tool.Calls);
        }
        finally
        {
            tool.Release.TrySetResult();
            if (tool.Entered.Task.IsCompleted) await tool.Settled.Task.WaitAsync(TestTimeout);
        }
    }

    [Fact]
    public async Task CancellationDuringMetadataCaptureReturnsPromptlyAndCannotStartTheWriteLater()
    {
        var (store, request, input, paths) = await PrepareAsync();
        using var metadataRelease = new ManualResetEventSlim();
        var metadataEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tool = new HeldWorkspaceTool(new CopilotApplyWorkspacePatchEnvelopeTool(store), TestTimeout, true, () =>
        {
            metadataEntered.TrySetResult();
            metadataRelease.Wait();
        });
        var executor = new CopilotToolExecutor(hooks: []);
        using var cancellation = new CancellationTokenSource();
        var execution = executor.ExecuteAsync(Invocation(tool, request, input), _ => { }, cancellation.Token);
        try
        {
            await metadataEntered.Task.WaitAsync(TestTimeout);
            cancellation.Cancel();
            var exception = await Assert.ThrowsAsync<CopilotToolExecutionCancellationException>(() => execution.WaitAsync(TestTimeout));
            Assert.False(metadataRelease.IsSet);
            Assert.Empty(exception.Outcome.Result.WorkspaceRecheckPaths);
            metadataRelease.Set();
            // This read waits for the same executor's write barrier to release.
            // Its completion proves the delayed metadata worker has settled.
            var read = await executor.ExecuteAsync(new()
            {
                CallId = "read-after-metadata", Tool = new CopilotReadLocalFileTool(), AgentRequest = request,
                ToolInput = new() { Path = paths[0] },
            }, _ => { }, CancellationToken.None).WaitAsync(TestTimeout);
            Assert.True(read.Result.Success, read.Result.ErrorMessage);
            Assert.Equal(0, tool.Calls);
            Assert.Equal("original", File.ReadAllText(paths[0]));
        }
        finally { metadataRelease.Set(); tool.Release.TrySetResult(); }
    }

    [Theory]
    [InlineData("conversation")]
    [InlineData("workspace")]
    [InlineData("unknown-id")]
    public async Task PathEvidenceCannotBeResolvedFromAnotherBinding(string mismatch)
    {
        var (store, request, input, _) = await PrepareAsync();
        var source = (ICopilotWorkspaceMutationEvidenceSource)new CopilotApplyWorkspacePatchEnvelopeTool(store);
        var differentRequest = new CopilotAgentRequest
        {
            ConversationId = mismatch == "conversation" ? "other-conversation" : request.ConversationId,
            WorkspacePath = mismatch == "workspace" ? Path.Combine(_workspace, "other") : _workspace,
        };
        if (mismatch == "unknown-id") input = new() { Arguments = new Dictionary<string, object?> { ["changeSetId"] = "workspace-change-set:" + new string('0', 32) } };
        Assert.Empty(source.GetWorkspaceRecheckPaths(differentRequest, input));
    }

    [Fact]
    public async Task CheckpointDispatchFailureDoesNotCapturePathsOrStartTheTool()
    {
        var (store, request, input, _) = await PrepareAsync();
        var tool = new HeldWorkspaceTool(new CopilotApplyWorkspacePatchEnvelopeTool(store), TestTimeout, true);
        var outcome = await new CopilotToolExecutor(hooks: []).ExecuteAsync(new()
        {
            CallId = "undispatched-patch", Tool = tool, AgentRequest = request, ToolInput = input,
            FrameworkApprovalGranted = true, PreDispatchCheckpoint = _ => ValueTask.FromResult(false),
        }, _ => { }, CancellationToken.None);
        Assert.False(outcome.Result.Success);
        Assert.Equal("tool_dispatch_checkpoint_failed", outcome.Result.FailureCode);
        Assert.Empty(outcome.Result.WorkspaceRecheckPaths);
        Assert.Equal(0, tool.MetadataCalls);
        Assert.Equal(0, tool.Calls);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task PendingPatchCheckpointRestoresUnknownOutcomeAndPathsBeforeOrAfterTheWrite(bool rollback, bool writesBeforeReload)
    {
        var (patchStore, request, input, paths) = await PrepareAsync();
        if (rollback) Assert.True((await patchStore.ApplyPatchEnvelopeAsync(request, input, CancellationToken.None)).Success);
        ICopilotFrameworkApprovedTool inner = rollback ? new CopilotRollbackWorkspacePatchEnvelopeTool(patchStore) : new CopilotApplyWorkspacePatchEnvelopeTool(patchStore);
        var tool = new HeldWorkspaceTool(inner, TestTimeout, writesBeforeReload);
        var message = new CopilotChatMessage(CopilotChatRole.Assistant, "正在修改文件");
        message.ApplyWorkspaceDiff(new("--- a/history.txt\n+++ b/history.txt\n@@ -1 +1 @@\n-before\n+after\n", 1, false));
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.Messages.Add(message);
        var state = new CopilotChatState { Conversations = [conversation] };
        var stateStore = new CopilotChatStateStore(Path.Combine(_workspace, "chat-state"));
        var checkpointCount = 0;
        var presentationLock = new object();
        var executor = new CopilotToolExecutor(hooks: []);
        var execution = executor.ExecuteAsync(new()
        {
            CallId = "persisted-patch", Tool = tool, AgentRequest = request, ToolInput = input, FrameworkApprovalGranted = true,
            PreDispatchCheckpoint = _ =>
            {
                lock (presentationLock)
                {
                    Assert.Equal(0, tool.Calls);
                    var trace = Assert.Single(message.AgentTraceEntries);
                    if (++checkpointCount == 2) Assert.Equal(paths.Order(), trace.WorkspaceRecheckPaths.Order());
                    else Assert.Empty(trace.WorkspaceRecheckPaths);
                    stateStore.Save(state);
                }
                return ValueTask.FromResult(true);
            },
        }, agentEvent => { lock (presentationLock) CopilotAssistantMessagePresenter.ApplyAgentEvent(message, agentEvent); }, CancellationToken.None);
        try
        {
            await tool.Entered.Task.WaitAsync(TestTimeout);
            Assert.Equal(2, checkpointCount);
            Assert.Equal(rollback != writesBeforeReload ? "patched" : "original", File.ReadAllText(paths[0]));
            // Reload only the pre-dispatch checkpoint, with no terminal event or synthetic diff.
            var recoveredState = new CopilotChatStateStore(Path.Combine(_workspace, "chat-state")).Load();
            recoveredState.EnsureInitializedAfterRestore(new CopilotConfig());
            var recovered = Assert.Single(Assert.Single(recoveredState.Conversations).Messages);
            var recoveredTrace = Assert.Single(recovered.AgentTraceEntries);
            Assert.Equal(CopilotToolExecutionState.Interrupted, recoveredTrace.State);
            Assert.Equal(CopilotToolFailureKind.OutcomeUnknown, recoveredTrace.FailureKind);
            Assert.Equal(CopilotToolFailureCode.OutcomeUnknown, recoveredTrace.FailureCode);
            Assert.False(recoveredTrace.RetryEligible);
            Assert.Equal(paths.Order(), recoveredTrace.WorkspaceRecheckPaths.Order());
            foreach (var path in paths) Assert.Contains(path, recovered.WorkspaceDiffWarning, StringComparison.Ordinal);
            Assert.Equal(message.WorkspaceDiff, recovered.WorkspaceDiff);
            Assert.Contains("先前确认", recovered.WorkspaceDiffHeader, StringComparison.Ordinal);
            var warning = recovered.WorkspaceDiffWarning;
            recovered.EnsureValid();
            Assert.Equal(warning, recovered.WorkspaceDiffWarning);
            stateStore.Save(recoveredState);
            Assert.Equal(warning, Assert.Single(Assert.Single(stateStore.Load().Conversations).Messages).WorkspaceDiffWarning);

            tool.Release.TrySetResult();
            Assert.True((await execution.WaitAsync(TestTimeout)).Result.Success);
            Assert.Empty(Assert.Single(message.AgentTraceEntries).WorkspaceRecheckPaths);
            stateStore.Save(state);
            var completed = Assert.Single(Assert.Single(stateStore.Load().Conversations).Messages);
            Assert.Equal(CopilotToolExecutionState.Completed, Assert.Single(completed.AgentTraceEntries).State);
            Assert.False(completed.HasWorkspaceDiffWarning);
        }
        finally
        {
            tool.Release.TrySetResult();
            await execution.WaitAsync(TestTimeout);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationOrTimeoutDuringPathCheckpointCannotStartALateWrite(bool timeout)
    {
        var (store, request, input, paths) = await PrepareAsync();
        var tool = new HeldWorkspaceTool(new CopilotApplyWorkspacePatchEnvelopeTool(store), timeout ? TimeSpan.FromMilliseconds(500) : TestTimeout, true);
        var checkpointEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var checkpointRelease = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var checkpoints = 0;
        var events = new ConcurrentQueue<CopilotAgentEvent>();
        using var cancellation = new CancellationTokenSource();
        var executor = new CopilotToolExecutor(hooks: []);
        var execution = executor.ExecuteAsync(new()
        {
            CallId = "held-path-checkpoint", Tool = tool, AgentRequest = request, ToolInput = input, FrameworkApprovalGranted = true,
            PreDispatchCheckpoint = _ =>
            {
                if (++checkpoints == 1) return ValueTask.FromResult(true);
                checkpointEntered.TrySetResult();
                return new ValueTask<bool>(checkpointRelease.Task);
            },
        }, events.Enqueue, cancellation.Token);
        try
        {
            await checkpointEntered.Task.WaitAsync(TestTimeout);
            CopilotToolExecutionOutcome outcome;
            if (timeout) outcome = await execution.WaitAsync(TestTimeout);
            else
            {
                cancellation.Cancel();
                outcome = (await Assert.ThrowsAsync<CopilotToolExecutionCancellationException>(() => execution.WaitAsync(TestTimeout))).Outcome;
            }
            Assert.Equal(CopilotToolFailureKind.OutcomeUnknown, outcome.Result.FailureKind);
            Assert.Equal(paths.Order(), outcome.Result.WorkspaceRecheckPaths.Order());
            Assert.False(checkpointRelease.Task.IsCompleted);
            checkpointRelease.TrySetResult(true);
            var read = await executor.ExecuteAsync(new()
            {
                CallId = "read-after-checkpoint", Tool = new CopilotReadLocalFileTool(), AgentRequest = request, ToolInput = new() { Path = paths[0] },
            }, _ => { }, CancellationToken.None).WaitAsync(TestTimeout);
            Assert.True(read.Result.Success, read.Result.ErrorMessage);
            Assert.Equal(0, tool.Calls);
            Assert.Equal("original", File.ReadAllText(paths[0]));
            Assert.Equal(CopilotAgentEventType.ToolResult, events.Last().Type);
            Assert.Single(events, e => e.Type == CopilotAgentEventType.ToolResult);
        }
        finally { checkpointRelease.TrySetResult(true); tool.Release.TrySetResult(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedPathCheckpointPreventsActualWritesAndClearsCandidateEvidence(bool throws)
    {
        var (store, request, input, paths) = await PrepareAsync();
        var tool = new HeldWorkspaceTool(new CopilotApplyWorkspacePatchEnvelopeTool(store), TestTimeout, true);
        var checkpoints = 0;
        var message = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty);
        var outcome = await new CopilotToolExecutor(hooks: []).ExecuteAsync(new()
        {
            CallId = "path-checkpoint-fails", Tool = tool, AgentRequest = request, ToolInput = input, FrameworkApprovalGranted = true,
            PreDispatchCheckpoint = _ => ++checkpoints == 1 ? ValueTask.FromResult(true)
                : throws ? throw new IOException("Test checkpoint failure") : ValueTask.FromResult(false),
        }, agentEvent => CopilotAssistantMessagePresenter.ApplyAgentEvent(message, agentEvent), CancellationToken.None);
        Assert.Equal(2, checkpoints);
        Assert.Equal(1, tool.MetadataCalls);
        Assert.Equal(0, tool.Calls);
        Assert.False(outcome.Result.Success);
        Assert.Equal("tool_dispatch_checkpoint_failed", outcome.Result.FailureCode);
        Assert.Empty(outcome.Result.WorkspaceRecheckPaths);
        Assert.Empty(Assert.Single(message.AgentTraceEntries).WorkspaceRecheckPaths);
        message.EnsureValid();
        Assert.False(message.HasWorkspaceDiffWarning);
        foreach (var path in paths) Assert.Equal("original", File.ReadAllText(path));
    }

    private async Task<(CopilotWorkspacePatchStore Store, CopilotAgentRequest Request, CopilotAgentToolInput Input, string[] Paths)> PrepareAsync()
    {
        var paths = new[] { Path.Combine(_workspace, "first.txt"), Path.Combine(_workspace, "second.txt") };
        foreach (var path in paths) File.WriteAllText(path, "original");
        var request = new CopilotAgentRequest
        {
            ConversationId = "cancelled-workspace", TaskId = "patch", WorkspacePath = _workspace,
            Mode = CopilotAgentMode.Code, UserText = "Apply the requested workspace patch.", TaskIntentText = "Apply the requested workspace patch.",
            WritableLocalRootPaths = [_workspace], ReadableLocalFilePaths = paths, SearchRootPaths = [_workspace], CodexHooksEnabled = false,
        };
        var store = new CopilotWorkspacePatchStore();
        var preview = await store.PreviewPatchEnvelopeAsync(request, new()
        {
            Arguments = new Dictionary<string, object?>
            {
                ["operations"] = paths.Select(path => new { operation = "update", path, replacements = new[] { new { oldText = "original", newText = "patched" } } }).ToArray(),
            },
        }, CancellationToken.None);
        Assert.True(preview.Success, preview.ErrorMessage);
        var id = preview.Content.Split('\n').Single(line => line.StartsWith("change_set_id:", StringComparison.Ordinal))["change_set_id:".Length..].Trim();
        return (store, request, new() { Arguments = new Dictionary<string, object?> { ["changeSetId"] = id } }, paths);
    }

    private static CopilotToolInvocation Invocation(ICopilotTool tool, CopilotAgentRequest request, CopilotAgentToolInput input) => new()
    {
        CallId = "held-workspace-patch", Tool = tool, AgentRequest = request, ToolInput = input, FrameworkApprovalGranted = true,
    };

    private sealed class HeldWorkspaceTool(ICopilotFrameworkApprovedTool inner, TimeSpan timeout, bool writesBeforeBoundary, Action? beforeMetadata = null)
        : ICopilotFrameworkApprovedTool, ICopilotWorkspaceMutationEvidenceSource
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Settled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Calls { get; private set; }
        public int MetadataCalls { get; private set; }
        public string Name => inner.Name;
        public string Description => inner.Description;
        public CopilotToolCapabilityDescriptor Capability => inner.Capability with { ExecutionTimeout = timeout };
        public CopilotToolInputSchema InputSchema => inner.InputSchema;
        public bool CanHandle(CopilotAgentRequest request) => true;
        public IReadOnlyList<string> GetWorkspaceRecheckPaths(CopilotAgentRequest request, CopilotAgentToolInput input)
        {
            MetadataCalls++;
            beforeMetadata?.Invoke();
            return ((ICopilotWorkspaceMutationEvidenceSource)inner).GetWorkspaceRecheckPaths(request, input);
        }
        public Task<CopilotToolResult> ExecuteAsync(CopilotAgentRequest request, CopilotAgentToolInput input, CancellationToken cancellationToken) =>
            inner.ExecuteAsync(request, input, cancellationToken);
        public async Task<CopilotToolResult> ExecuteApprovedAsync(CopilotAgentRequest request, CopilotAgentToolInput input, CancellationToken cancellationToken)
        {
            Calls++;
            try
            {
                if (!writesBeforeBoundary) { Entered.TrySetResult(); await Release.Task; }
                var result = await inner.ExecuteApprovedAsync(request, input, CancellationToken.None);
                Assert.True(result.Success, result.ErrorMessage);
                if (writesBeforeBoundary) { Entered.TrySetResult(); await Release.Task; }
                return result;
            }
            finally { Settled.TrySetResult(); }
        }
    }

    public void Dispose()
    {
        var path = Path.GetFullPath(_workspace);
        Assert.Equal(Path.TrimEndingDirectorySeparator(Path.GetTempPath()), Path.GetDirectoryName(path), ignoreCase: true);
        Assert.StartsWith("CopilotCancelledWorkspace-", Path.GetFileName(path), StringComparison.Ordinal);
        Directory.Delete(path, recursive: true);
    }
}
