using ColorVision.Copilot;
using Newtonsoft.Json;
using System.IO;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotTurnWorkspaceDiffTests
{
    [Fact]
    public void AccumulatorBuildsNetUnifiedDiffAcrossRepeatedChangesAndClearsAfterRollback()
    {
        var root = CreateWorkspace();
        try
        {
            var path = Path.Combine(root, "src", "sample.cs");
            var accumulator = new CopilotTurnWorkspaceDiffAccumulator(root);

            Assert.True(accumulator.Observe(CreateMutationEvent(path, true, "one\ntwo\nthree\n", true, "one\nTWO\nthree\n"), out var first));
            Assert.Equal(1, first.FileCount);
            Assert.False(first.DiffTruncated);
            Assert.Contains("--- a/src/sample.cs", first.Diff, StringComparison.Ordinal);
            Assert.Contains("@@ -1,3 +1,3 @@", first.Diff, StringComparison.Ordinal);
            Assert.Contains("-two", first.Diff, StringComparison.Ordinal);
            Assert.Contains("+TWO", first.Diff, StringComparison.Ordinal);
            Assert.True(
                first.Diff.IndexOf("-two", StringComparison.Ordinal)
                < first.Diff.IndexOf("+TWO", StringComparison.Ordinal));

            Assert.True(accumulator.Observe(CreateMutationEvent(path, true, "one\nTWO\nthree\n", true, "zero\none\nTWO\nthree\n"), out var second));
            Assert.Equal(1, second.FileCount);
            Assert.Contains("+zero", second.Diff, StringComparison.Ordinal);
            Assert.Contains("-two", second.Diff, StringComparison.Ordinal);
            Assert.Contains("+TWO", second.Diff, StringComparison.Ordinal);

            Assert.True(accumulator.Observe(CreateMutationEvent(path, true, "zero\none\nTWO\nthree\n", true, "one\ntwo\nthree\n"), out var rolledBack));
            Assert.Equal(string.Empty, rolledBack.Diff);
            Assert.Equal(0, rolledBack.FileCount);
            Assert.False(rolledBack.DiffTruncated);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void AccumulatorRendersCreationDeletionAndMissingTerminalNewline()
    {
        var root = CreateWorkspace();
        try
        {
            var accumulator = new CopilotTurnWorkspaceDiffAccumulator(root);
            var createdPath = Path.Combine(root, "created.txt");
            var deletedPath = Path.Combine(root, "deleted.txt");
            var result = new CopilotToolResult
            {
                ToolName = "ApplyWorkspacePatchEnvelope",
                Success = true,
                WorkspaceMutation = new CopilotWorkspaceMutationSnapshot(
                [
                    new CopilotWorkspaceMutationFileSnapshot(createdPath, false, string.Empty, true, "created"),
                    new CopilotWorkspaceMutationFileSnapshot(deletedPath, true, "deleted\n", false, string.Empty),
                ]),
            };

            Assert.True(accumulator.Observe(CopilotAgentEvent.FromToolResult(result), out var snapshot));

            Assert.Equal(2, snapshot.FileCount);
            Assert.Contains("--- /dev/null\n+++ b/created.txt", snapshot.Diff, StringComparison.Ordinal);
            Assert.Contains("+created", snapshot.Diff, StringComparison.Ordinal);
            Assert.Contains("\\ No newline at end of file", snapshot.Diff, StringComparison.Ordinal);
            Assert.Contains("--- a/deleted.txt\n+++ /dev/null", snapshot.Diff, StringComparison.Ordinal);
            Assert.Contains("-deleted", snapshot.Diff, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void AccumulatorRejectsDiscontinuousMutationEvidence()
    {
        var root = CreateWorkspace();
        try
        {
            var path = Path.Combine(root, "sample.txt");
            var accumulator = new CopilotTurnWorkspaceDiffAccumulator(root);
            Assert.True(accumulator.Observe(CreateMutationEvent(path, true, "before", true, "middle"), out _));

            var exception = Assert.Throws<InvalidOperationException>(() =>
                accumulator.Observe(CreateMutationEvent(path, true, "different", true, "after"), out _));

            Assert.Contains("discontinuous", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void AccumulatorBoundsLargeDiffWithoutSplittingItsContract()
    {
        var root = CreateWorkspace();
        try
        {
            var path = Path.Combine(root, "large.txt");
            var accumulator = new CopilotTurnWorkspaceDiffAccumulator(root);

            Assert.True(accumulator.Observe(
                CreateMutationEvent(path, true, new string('a', 120_000), true, new string('b', 120_000)),
                out var snapshot));

            Assert.True(snapshot.DiffTruncated);
            Assert.Equal(CopilotTurnWorkspaceDiffAccumulator.MaxDiffCharacters, snapshot.Diff.Length);
            Assert.Contains(CopilotTurnWorkspaceDiffAccumulator.DiffTruncationMarker, snapshot.Diff, StringComparison.Ordinal);
            Assert.True(snapshot.IsStructurallyValid());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ReducerRequiresDiffImmediatelyAfterStructuredMutationResult()
    {
        var path = Path.Combine(Path.GetTempPath(), "sample.txt");
        var state = CopilotTurnEventReducer.Reduce(
            CreateStartedState(CopilotAgentMode.Auto),
            new CopilotTurnAgentEvent(CreateMutationEvent(path, true, "before", true, "after")));

        Assert.True(state.WorkspaceDiffExpected);
        var exception = Assert.Throws<InvalidOperationException>(() =>
            CopilotTurnEventReducer.Reduce(
                state,
                new CopilotTurnAgentEvent(CopilotAgentEvent.AnswerDelta("continued"))));
        Assert.Contains("before its workspace diff update", exception.Message, StringComparison.Ordinal);

        var snapshot = new CopilotTurnWorkspaceDiffSnapshot("--- a/sample.txt\n+++ b/sample.txt", 1, false);
        state = CopilotTurnEventReducer.Reduce(state, new CopilotTurnWorkspaceDiffUpdatedEvent(snapshot));
        Assert.False(state.WorkspaceDiffExpected);
        Assert.Same(snapshot, state.WorkspaceDiff);
    }

    [Fact]
    public void PresenterAppliesAndClearsAuthoritativeDiffSnapshot()
    {
        var assistant = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty);
        var snapshot = new CopilotTurnWorkspaceDiffSnapshot("--- /dev/null\n+++ b/new.txt\n@@ -0,0 +1 @@\n+new", 1, false);

        var result = CopilotAssistantMessagePresenter.ApplyWorkspaceDiffUpdated(assistant, snapshot);

        Assert.Equal(CopilotAgentEventPersistenceMode.Immediate, result.PersistenceMode);
        Assert.Equal(snapshot.Diff, assistant.WorkspaceDiff);
        Assert.True(assistant.HasWorkspaceDiff);
        Assert.Equal("本轮文件变更 · 1 个文件", assistant.WorkspaceDiffHeader);

        CopilotAssistantMessagePresenter.ApplyWorkspaceDiffUpdated(
            assistant,
            new CopilotTurnWorkspaceDiffSnapshot(string.Empty, 0, false));
        Assert.False(assistant.HasWorkspaceDiff);
        Assert.Equal(0, assistant.WorkspaceDiffFileCount);
    }

    [Fact]
    public void WorkspaceMutationEvidenceIsNotSerializedWithToolObservation()
    {
        var secretBefore = "private-before-content";
        var result = new CopilotToolResult
        {
            ToolName = "ApplyWorkspacePatchEnvelope",
            Success = true,
            Content = "bounded model observation",
            WorkspaceMutation = new CopilotWorkspaceMutationSnapshot(
            [
                new CopilotWorkspaceMutationFileSnapshot(
                    Path.Combine(Path.GetTempPath(), "sample.txt"),
                    true,
                    secretBefore,
                    true,
                    "private-after-content"),
            ]),
        };

        var json = JsonConvert.SerializeObject(result);

        Assert.Contains("bounded model observation", json, StringComparison.Ordinal);
        Assert.DoesNotContain(secretBefore, json, StringComparison.Ordinal);
        Assert.DoesNotContain("WorkspaceMutation", json, StringComparison.Ordinal);
    }

    [Fact]
    public void MessageWorkspaceDiffRoundTripsAndValidationRejectsUserOwnedDiffs()
    {
        var assistant = new CopilotChatMessage(CopilotChatRole.Assistant, "done");
        assistant.ApplyWorkspaceDiff(new CopilotTurnWorkspaceDiffSnapshot(
            "--- a/a.txt\n+++ b/a.txt\n@@ -1 +1 @@\n-old\n+new",
            1,
            false));

        var json = JsonConvert.SerializeObject(assistant);
        var restored = Assert.IsType<CopilotChatMessage>(JsonConvert.DeserializeObject<CopilotChatMessage>(json));

        Assert.Equal(assistant.WorkspaceDiff, restored.WorkspaceDiff);
        Assert.Equal(1, restored.WorkspaceDiffFileCount);
        Assert.True(restored.HasWorkspaceDiff);

        restored.Role = CopilotChatRole.User;
        Assert.True(restored.EnsureValid());
        Assert.False(restored.HasWorkspaceDiff);
        Assert.Equal(0, restored.WorkspaceDiffFileCount);
    }

    [Fact]
    public async Task WorkspacePatchStorePublishesExactCreationAndRollbackSnapshots()
    {
        var root = CreateWorkspace();
        try
        {
            var request = new CopilotAgentRequest
            {
                ConversationId = "diff-test-conversation",
                TaskId = "diff-test-task",
                WorkspacePath = root,
                SearchRootPaths = [root],
                WritableLocalRootPaths = [root],
            };
            var store = new CopilotWorkspacePatchStore();
            var preview = await store.PreviewPatchEnvelopeAsync(
                request,
                new CopilotAgentToolInput
                {
                    Arguments = new Dictionary<string, object?>
                    {
                        ["operations"] = new[]
                        {
                            new { operation = "add", path = "new.txt", content = "new content\n" },
                        },
                    },
                },
                CancellationToken.None);
            var changeSetId = GetChangeSetId(preview);
            var input = new CopilotAgentToolInput
            {
                Arguments = new Dictionary<string, object?> { ["changeSetId"] = changeSetId },
            };

            var applied = await store.ApplyPatchEnvelopeAsync(request, input, CancellationToken.None);

            Assert.True(applied.Success, applied.ErrorMessage);
            var appliedFile = Assert.Single(Assert.IsType<CopilotWorkspaceMutationSnapshot>(applied.WorkspaceMutation).Files);
            Assert.False(appliedFile.BeforeExists);
            Assert.True(appliedFile.AfterExists);
            Assert.Equal("new content\n", appliedFile.AfterText);
            Assert.Equal("new content\n", File.ReadAllText(Path.Combine(root, "new.txt")));

            var rolledBack = await store.RollbackPatchEnvelopeAsync(request, input, CancellationToken.None);

            Assert.True(rolledBack.Success, rolledBack.ErrorMessage);
            var rolledBackFile = Assert.Single(Assert.IsType<CopilotWorkspaceMutationSnapshot>(rolledBack.WorkspaceMutation).Files);
            Assert.True(rolledBackFile.BeforeExists);
            Assert.False(rolledBackFile.AfterExists);
            Assert.False(File.Exists(Path.Combine(root, "new.txt")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static CopilotAgentEvent CreateMutationEvent(
        string path,
        bool beforeExists,
        string beforeText,
        bool afterExists,
        string afterText)
    {
        var completedAt = DateTimeOffset.UtcNow;
        return CopilotAgentEvent.FromToolResult(
            new CopilotToolResult
            {
                ToolName = "ApplyWorkspacePatchEnvelope",
                Success = true,
                WorkspaceMutation = new CopilotWorkspaceMutationSnapshot(
                [
                    new CopilotWorkspaceMutationFileSnapshot(path, beforeExists, beforeText, afterExists, afterText),
                ]),
            },
            new CopilotToolExecutionInfo
            {
                CallId = "workspace-mutation-test-call",
                Round = 1,
                Attempt = 1,
                MaxAttempts = 1,
                RuntimeName = "protocol-test",
                ToolName = "ApplyWorkspacePatchEnvelope",
                Access = CopilotToolAccess.Write,
                RiskLevel = CopilotToolRiskLevel.High,
                ApprovalMode = CopilotToolApprovalMode.Always,
                Idempotency = CopilotToolIdempotency.NonIdempotent,
                ConcurrencyMode = CopilotToolConcurrencyMode.Exclusive,
                ConcurrencyKey = "resource:workspace",
                ArgumentSummary = "workspace mutation test",
                State = CopilotToolExecutionState.Completed,
                StartedAtUtc = completedAt.AddMilliseconds(-1),
                CompletedAtUtc = completedAt,
                DurationMs = 1,
                TimeoutMs = 30_000,
            });
    }

    private static CopilotTurnEventState CreateStartedState(CopilotAgentMode mode) =>
        CopilotTurnEventReducer.Reduce(
            CopilotTurnEventState.Create(mode),
            new CopilotTurnStartedEvent(mode));

    private static string GetChangeSetId(CopilotToolResult preview)
    {
        Assert.True(preview.Success, preview.ErrorMessage);
        return preview.Content
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
            .Single(line => line.StartsWith("change_set_id:", StringComparison.Ordinal))
            ["change_set_id:".Length..]
            .Trim();
    }

    private static string CreateWorkspace()
    {
        var root = Path.Combine(Path.GetTempPath(), "ColorVisionCopilotDiff", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}
