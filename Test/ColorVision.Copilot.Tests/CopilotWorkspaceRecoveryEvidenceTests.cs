using System.Collections;
using System.IO;
using System.Reflection;
using ColorVision.Copilot;
using Microsoft.Extensions.AI;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotWorkspaceRecoveryEvidenceTests : IDisposable
{
    private readonly string _workspace = Directory.CreateTempSubdirectory("CopilotRecoveryEvidence-").FullName;

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task FailedEnvelopeReportsEveryRecoveryAttemptAndAllowsFreshEvidence(bool rollback, bool recoveryFails)
    {
        var first = Path.Combine(_workspace, "first.txt");
        var second = Path.Combine(_workspace, "second.txt");
        var unrelated = Path.Combine(_workspace, "unrelated.txt");
        foreach (var path in new[] { first, second, unrelated }) File.WriteAllText(path, "original");
        var request = new CopilotAgentRequest
        {
            ConversationId = "recovery-evidence", TaskId = "recover", WorkspacePath = _workspace,
            Mode = CopilotAgentMode.Code, UserText = "修改文件，失败时检查实际结果。",
            WritableLocalRootPaths = [_workspace], ReadableLocalFilePaths = [first, second, unrelated],
            SearchRootPaths = [_workspace], CodexHooksEnabled = false,
        };
        var store = new CopilotWorkspacePatchStore();
        var preview = await store.PreviewPatchEnvelopeAsync(request, new()
        {
            Arguments = new Dictionary<string, object?>
            {
                ["operations"] = new[] { first, second }.Select(path => new
                {
                    operation = "update", path,
                    replacements = new[] { new { oldText = "original", newText = "patched" } },
                }).ToArray(),
            },
        }, CancellationToken.None);
        Assert.True(preview.Success, preview.ErrorMessage);
        var id = preview.Content.Split('\n').Single(line => line.StartsWith("change_set_id:", StringComparison.Ordinal))["change_set_id:".Length..].Trim();
        var input = new CopilotAgentToolInput { Arguments = new Dictionary<string, object?> { ["changeSetId"] = id } };
        if (rollback)
        {
            var applied = await store.ApplyPatchEnvelopeAsync(request, input, CancellationToken.None);
            Assert.True(applied.Success, applied.ErrorMessage);
        }
        var failedPath = rollback ? first : second;
        var recoveredPath = rollback ? second : first;
        File.SetAttributes(failedPath, FileAttributes.ReadOnly);
        CopilotToolResult? result = null;
        var writer = new RecoveryTool(rollback ? "RollbackWorkspacePatchEnvelope" : "ApplyWorkspacePatchEnvelope", async () =>
        {
            result = recoveryFails
                ? await FailRecoveryAfterOneCompletedChildAsync(store, request, id, rollback, recoveredPath)
                : rollback
                    ? await store.RollbackPatchEnvelopeAsync(request, input, CancellationToken.None)
                    : await store.ApplyPatchEnvelopeAsync(request, input, CancellationToken.None);
            return result;
        });
        var bridge = new CopilotMicrosoftAgentFrameworkRuntime.HarnessToolBridge(request,
            CopilotExecutionScope.ForAgentRun(request), [new CopilotReadLocalFileTool(), writer], 12,
            new CopilotToolExecutor(hooks: []), new CopilotFrameworkApprovalCoordinator(), _ => { }, () => 1);
        var functions = bridge.CreateFunctions().OfType<AIFunction>().ToArray();
        var read = Assert.Single(functions, f => f.Name == "colorvision_read_local_file");
        var write = Assert.Single(functions, f => f.Name != read.Name);
        AIFunctionArguments ReadInput(string path) => new() { ["path"] = path };
        foreach (var path in new[] { first, second, unrelated })
        {
            await read.InvokeAsync(ReadInput(path));
            Assert.True(bridge.StepRecords.Last().Observation.Success);
        }
        await write.InvokeAsync(new AIFunctionArguments());
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.False(bridge.StepRecords.Last().Observation.Success);
        Assert.Contains($"failed_path: {failedPath}", result.Content, StringComparison.Ordinal);
        Assert.Contains($"path: {recoveredPath}", result.Content, StringComparison.Ordinal);
        Assert.Contains("recovery_attempt_count: 1", result.Content, StringComparison.Ordinal);
        Assert.Contains(recoveryFails ? "recovery_result: failed" : "recovery_result: succeeded", result.Content, StringComparison.Ordinal);
        Assert.Contains(recoveryFails ? "state: Invalidated" : rollback ? "state: Applied" : "state: RolledBack", result.Content, StringComparison.Ordinal);
        if (recoveryFails) Assert.Contains("error: The target file is read-only.", result.Content, StringComparison.Ordinal);
        Assert.Equal(new[] { first, second }.Order(), result.WorkspaceRecheckPaths.Order());
        // Recovery reports never invent a successful mutation snapshot or net diff.
        Assert.Null(result.WorkspaceMutation);
        Assert.True(new CopilotTurnWorkspaceDiffAccumulator(_workspace).Observe(CopilotAgentEvent.FromToolResult(result), out var recoverySnapshot));
        Assert.Empty(recoverySnapshot.Diff);
        Assert.NotEmpty(recoverySnapshot.VerificationWarning);

        foreach (var path in new[] { first, second })
        {
            await read.InvokeAsync(ReadInput(path));
            var observation = bridge.StepRecords.Last().Observation;
            Assert.True(observation.Success, observation.ErrorMessage);
            Assert.Contains(File.ReadAllText(path), observation.Content, StringComparison.Ordinal);
        }
        Assert.Equal(rollback ? "patched" : "original", File.ReadAllText(failedPath));
        Assert.Equal(rollback != recoveryFails ? "patched" : "original", File.ReadAllText(recoveredPath));
        await read.InvokeAsync(ReadInput(unrelated));
        Assert.Equal(CopilotToolFailureKind.Conflict, bridge.StepRecords.Last().Observation.FailureKind);
        await read.InvokeAsync(ReadInput(first));
        Assert.Equal(CopilotToolFailureKind.Conflict, bridge.StepRecords.Last().Observation.FailureKind);
        await write.InvokeAsync(new AIFunctionArguments());
        Assert.Equal(CopilotToolFailureKind.Conflict, bridge.StepRecords.Last().Observation.FailureKind);
        Assert.Equal(1, writer.Calls);
    }

    [Fact]
    public async Task FailureBeforeAnyWriteDoesNotClaimRecoveryOrReopenObservations()
    {
        var path = Path.Combine(_workspace, "single.txt");
        File.WriteAllText(path, "original");
        var store = new CopilotWorkspacePatchStore();
        var request = new CopilotAgentRequest { ConversationId = "preflight", WorkspacePath = _workspace, WritableLocalRootPaths = [_workspace] };
        var preview = await store.PreviewPatchEnvelopeAsync(request, new()
        {
            Arguments = new Dictionary<string, object?> { ["operations"] = new[] { new { operation = "delete", path } } },
        }, CancellationToken.None);
        Assert.True(preview.Success, preview.ErrorMessage);
        var id = preview.Content.Split('\n').Single(line => line.StartsWith("change_set_id:", StringComparison.Ordinal))["change_set_id:".Length..].Trim();
        File.WriteAllText(path, "external update");
        var result = await store.ApplyPatchEnvelopeAsync(request,
            new() { Arguments = new Dictionary<string, object?> { ["changeSetId"] = id } }, CancellationToken.None);
        Assert.False(result.Success);
        Assert.Equal(CopilotToolFailureKind.Conflict, result.FailureKind);
        Assert.Empty(result.WorkspaceRecheckPaths);
        Assert.DoesNotContain("[Workspace Change Set Recovery]", result.Content, StringComparison.Ordinal);
        Assert.Equal("external update", File.ReadAllText(path));
    }

    // Drive real child writes to a precise recovery boundary, then use a real
    // read-only file to fail compensation. No timing race or production test hook.
    private static async Task<CopilotToolResult> FailRecoveryAfterOneCompletedChildAsync(
        CopilotWorkspacePatchStore store, CopilotAgentRequest request, string id, bool rollback, string recoveredPath)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var type = typeof(CopilotWorkspacePatchStore);
        var changes = (IDictionary)type.GetField("_changeSets", flags)!.GetValue(store)!;
        var patches = (IDictionary)type.GetField("_records", flags)!.GetValue(store)!;
        var changeSet = changes[id]!;
        var ids = (string[])changeSet.GetType().GetProperty("PreviewIds")!.GetValue(changeSet)!;
        var recordType = type.GetNestedType("WorkspacePatchRecord", BindingFlags.NonPublic)!;
        var records = Array.CreateInstance(recordType, ids.Length);
        for (var i = 0; i < ids.Length; i++) records.SetValue(patches[ids[i]], i);
        var completedRecord = records.GetValue(rollback ? 1 : 0)!;
        var failedRecord = records.GetValue(rollback ? 0 : 1)!;
        var child = type.GetMethod("MutateChangeSetChildAsync", flags)!;
        var completed = await (Task<CopilotToolResult>)child.Invoke(store, [request, completedRecord, rollback, id, CancellationToken.None])!;
        Assert.True(completed.Success, completed.ErrorMessage);
        File.SetAttributes(recoveredPath, FileAttributes.ReadOnly);
        var failure = await (Task<CopilotToolResult>)child.Invoke(store, [request, failedRecord, rollback, id, CancellationToken.None])!;
        Assert.False(failure.Success);
        var completedRecords = Array.CreateInstance(recordType, 1);
        completedRecords.SetValue(completedRecord, 0);
        var handler = type.GetMethod(rollback ? "HandleRollbackFailureAsync" : "HandleApplyFailureAsync", flags)!;
        return await (Task<CopilotToolResult>)handler.Invoke(store,
            [request, changeSet, records, completedRecords, failedRecord, failure, rollback ? "RollbackWorkspacePatchEnvelope" : "ApplyWorkspacePatchEnvelope"])!;
    }

    private sealed class RecoveryTool(string name, Func<Task<CopilotToolResult>> run) : ICopilotAgentDrivenTool
    {
        public string Name => name;
        public string Description => "Exercises the real patch recovery in a disposable workspace.";
        public int Calls { get; private set; }
        public CopilotToolCapabilityDescriptor Capability { get; } = CopilotToolCapabilityDescriptor.ProtectedWrite(CopilotToolIdempotency.NonIdempotent)
            with { ApprovalMode = CopilotToolApprovalMode.Never, RiskLevel = CopilotToolRiskLevel.Low };
        public bool CanHandle(CopilotAgentRequest request) => true;
        public bool IsAvailable(CopilotAgentRequest request) => true;
        public Task<CopilotToolResult> ExecuteAsync(CopilotAgentRequest request, CopilotAgentToolInput input, CancellationToken cancellationToken)
        {
            Calls++;
            return run();
        }
    }

    public void Dispose()
    {
        var path = Path.GetFullPath(_workspace);
        Assert.Equal(Path.TrimEndingDirectorySeparator(Path.GetTempPath()), Path.GetDirectoryName(path), ignoreCase: true);
        Assert.StartsWith("CopilotRecoveryEvidence-", Path.GetFileName(path), StringComparison.Ordinal);
        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)) File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(path, recursive: true);
    }
}
