using ColorVision.Copilot;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotCodeReviewSnapshotTests
{
    private static readonly JsonSerializerSettings SerializerSettings = new()
    {
        Formatting = Formatting.None,
        NullValueHandling = NullValueHandling.Ignore,
    };

    [Fact]
    public void GitDiffProtocolRoundTripsBoundedWorkingTreeEvidence()
    {
        var expected = CreateWorkingTreeSnapshot();

        var content = CopilotGitDiffResultProtocol.Serialize(expected);
        var parsed = CopilotGitDiffResultProtocol.TryParse(content, out var actual, out var error);

        Assert.True(parsed, error);
        Assert.True(CopilotGitDiffResultProtocol.AreEquivalent(expected, actual));
        Assert.NotSame(expected, actual);
        Assert.NotSame(expected.Sections, actual.Sections);
        Assert.StartsWith("[Git Diff Inspection]\nresult_json: ", content, StringComparison.Ordinal);
    }

    [Fact]
    public void GitDiffProtocolNormalizesLegacyWorkingTreeEvidence()
    {
        var content = CopilotGitDiffResultProtocol.Serialize(CreateWorkingTreeSnapshot());
        var markerIndex = content.IndexOf(
            CopilotGitDiffResultProtocol.ResultJsonMarker,
            StringComparison.Ordinal);
        var document = JObject.Parse(
            content[(markerIndex + CopilotGitDiffResultProtocol.ResultJsonMarker.Length)..]);
        document.Remove("changed_paths");
        document.Remove("changed_paths_complete");
        Assert.IsType<JArray>(document["sections"]).RemoveAt(2);
        var legacy = CopilotGitDiffResultProtocol.Header
            + "\n"
            + CopilotGitDiffResultProtocol.ResultJsonMarker
            + document.ToString(Formatting.None);

        Assert.True(CopilotGitDiffResultProtocol.TryParse(legacy, out var parsed, out var error), error);
        Assert.Equal(3, parsed.Sections.Count);
        Assert.Equal("untracked", parsed.Sections[^1].Scope);
        Assert.Empty(parsed.ChangedPaths);
        Assert.False(parsed.ChangedPathsComplete);
    }

    [Fact]
    public void GitDiffProtocolRejectsEscapingChangedPaths()
    {
        var content = CopilotGitDiffResultProtocol.Serialize(CreateWorkingTreeSnapshot());
        var markerIndex = content.IndexOf(
            CopilotGitDiffResultProtocol.ResultJsonMarker,
            StringComparison.Ordinal);
        var document = JObject.Parse(
            content[(markerIndex + CopilotGitDiffResultProtocol.ResultJsonMarker.Length)..]);
        document["changed_paths"] = new JArray("../outside.cs");
        var malformed = CopilotGitDiffResultProtocol.Header
            + "\n"
            + CopilotGitDiffResultProtocol.ResultJsonMarker
            + document.ToString(Formatting.None);

        Assert.False(CopilotGitDiffResultProtocol.TryParse(malformed, out _, out var error));
        Assert.Contains("inconsistent", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GitDiffProtocolRejectsInconsistentAndOversizedEvidence()
    {
        var content = CopilotGitDiffResultProtocol.Serialize(CreateWorkingTreeSnapshot());
        var markerIndex = content.IndexOf(CopilotGitDiffResultProtocol.ResultJsonMarker, StringComparison.Ordinal);
        var document = JObject.Parse(content[(markerIndex + CopilotGitDiffResultProtocol.ResultJsonMarker.Length)..]);
        document["output_complete"] = false;
        var inconsistent = CopilotGitDiffResultProtocol.Header
            + "\n"
            + CopilotGitDiffResultProtocol.ResultJsonMarker
            + document.ToString(Formatting.None);

        Assert.False(CopilotGitDiffResultProtocol.TryParse(inconsistent, out _, out var error));
        Assert.Contains("inconsistent", error, StringComparison.OrdinalIgnoreCase);

        var oversized = new CopilotGitDiffSnapshot(
            @"C:\repo",
            "unstaged",
            string.Empty,
            true,
            true,
            false,
            [
                new CopilotGitDiffSection(
                    "unstaged",
                    true,
                    true,
                    false,
                    new string('x', CopilotGitDiffInspectionService.MaxPatchCharactersPerSection + 1)),
            ]);
        Assert.False(oversized.IsStructurallyValid());
    }

    [Fact]
    public void CaptureAcceptsOnlySuccessfulDiffForTheExactReviewTarget()
    {
        var workingTree = CreateWorkingTreeSnapshot();
        var toolEvent = CreateDiffEvent(workingTree);

        Assert.True(CopilotTurnCodeReviewSnapshotCapture.TryCapture(
            CopilotWorkspaceReviewTargetContext.WorkingTree(),
            toolEvent,
            out var captured));
        Assert.True(captured.TryReadStructuredModelDiff(out var modelDiff));
        Assert.True(CopilotGitDiffResultProtocol.AreEquivalent(workingTree, modelDiff));

        Assert.False(CopilotTurnCodeReviewSnapshotCapture.TryCapture(
            new CopilotWorkspaceReviewTargetContext
            {
                Target = CopilotWorkspaceReviewTarget.Commit,
                Revision = "abcdef1",
            },
            toolEvent,
            out _));

        var failedEvent = CopilotAgentEvent.FromToolResult(new CopilotToolResult
        {
            ToolName = "InspectGitDiff",
            Success = false,
            Content = CopilotGitDiffResultProtocol.Serialize(workingTree),
        });
        Assert.False(CopilotTurnCodeReviewSnapshotCapture.TryCapture(
            CopilotWorkspaceReviewTargetContext.WorkingTree(),
            failedEvent,
            out _));
    }

    [Fact]
    public void CaptureMatchesCommitObjectIdsCaseInsensitively()
    {
        var snapshot = CreateCommitSnapshot("ABCDEF1");
        var target = new CopilotWorkspaceReviewTargetContext
        {
            Target = CopilotWorkspaceReviewTarget.Commit,
            Revision = "abcdef1",
        };

        Assert.True(CopilotTurnCodeReviewSnapshotCapture.TryCapture(
            target,
            CreateDiffEvent(snapshot),
            out var captured));
        Assert.Equal("ABCDEF1", captured.Revision);
    }

    [Fact]
    public void CaptureRequiresTheExactModelVisibleToolResult()
    {
        var snapshot = CreateWorkingTreeSnapshot();
        var rawOnly = CopilotAgentEvent.FromToolResult(new CopilotToolResult
        {
            ToolName = "InspectGitDiff",
            Success = true,
            Content = CopilotGitDiffResultProtocol.Serialize(snapshot),
        });

        Assert.False(CopilotTurnCodeReviewSnapshotCapture.TryCapture(
            CopilotWorkspaceReviewTargetContext.WorkingTree(),
            rawOnly,
            out _));
    }

    [Fact]
    public void FindingsProtocolRoundTripsCanonicalLineLevelResults()
    {
        var evidence = CreateCodeReviewSnapshot();
        var expected = new CopilotCodeReviewFindingsSubmission(
            evidence.EvidenceId,
            [CreateFinding()]);

        var content = CopilotCodeReviewFindingsResultProtocol.Serialize(expected);
        var parsed = CopilotCodeReviewFindingsResultProtocol.TryParse(
            content,
            out var actual,
            out var error);

        Assert.True(parsed, error);
        Assert.Equal(expected.EvidenceId, actual.EvidenceId);
        Assert.Equal(expected.Findings, actual.Findings);
        Assert.StartsWith("[Code Review Findings]\nresult_json: ", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FindingsToolAcceptsOnlyLocationsInTheLatestModelVisibleDiff()
    {
        var evidence = CreateCodeReviewSnapshot();
        var context = new CopilotReviewEvidenceContext();
        context.RecordEvidence(evidence);
        var request = new CopilotAgentRequest
        {
            Mode = CopilotAgentMode.Review,
            ReviewEvidenceContext = context,
        };
        var tool = new CopilotSubmitCodeReviewFindingsTool();

        var accepted = await tool.ExecuteAsync(
            request,
            CreateFindingsInput(CreateFinding()),
            CancellationToken.None);
        var rejected = await tool.ExecuteAsync(
            request,
            CreateFindingsInput(CreateFinding() with { LineStart = 43, LineEnd = 43 }),
            CancellationToken.None);

        Assert.True(accepted.Success, accepted.ErrorMessage);
        Assert.True(CopilotCodeReviewFindingsResultProtocol.TryParse(
            accepted.Content,
            out var submission,
            out var error), error);
        Assert.Equal(evidence.EvidenceId, submission.EvidenceId);
        Assert.Single(submission.Findings);
        Assert.False(rejected.Success);
        Assert.Equal(CopilotToolFailureKind.Validation, rejected.FailureKind);
        Assert.Contains("not inside a visible Git diff hunk", rejected.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FindingsToolAllowsExplicitEmptyResultWhenModelDiffWasCompacted()
    {
        var evidence = CreateCodeReviewSnapshot(
            CreateWorkingTreeSnapshot() with
            {
                Sections =
                [
                    new CopilotGitDiffSection(
                        "unstaged",
                        true,
                        true,
                        false,
                        "diff --git a/Large.cs b/Large.cs\n--- a/Large.cs\n+++ b/Large.cs\n@@ -1 +1 @@\n"
                        + new string('x', 10_000)),
                    new CopilotGitDiffSection("staged", false, true, false, string.Empty),
                    new CopilotGitDiffSection("untracked", false, true, false, string.Empty),
                ],
                ChangedPaths = ["Large.cs"],
            },
            toolOutputTokenLimit: 500);
        var context = new CopilotReviewEvidenceContext();
        context.RecordEvidence(evidence);
        var request = new CopilotAgentRequest
        {
            Mode = CopilotAgentMode.Review,
            ReviewEvidenceContext = context,
        };

        var result = await new CopilotSubmitCodeReviewFindingsTool().ExecuteAsync(
            request,
            CreateFindingsInput(),
            CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(CopilotCodeReviewFindingsResultProtocol.TryParse(
            result.Content,
            out var submission,
            out var error), error);
        Assert.Empty(submission.Findings);
    }

    [Fact]
    public void FindingsCaptureMergesOnlyAResultBoundToTheCurrentEvidence()
    {
        var target = CopilotWorkspaceReviewTargetContext.WorkingTree();
        var current = CreateCodeReviewSnapshot();
        var submitted = ApplyFindings(current, CreateFinding());
        var findingsEvent = CreateFindingsEvent(submitted.FindingsResult);

        Assert.True(CopilotTurnCodeReviewSnapshotCapture.TryCaptureUpdate(
            target,
            current,
            findingsEvent,
            out var updated));
        Assert.Equal(submitted, updated);
        Assert.True(updated.TryReadFindings(out var findings));
        Assert.Single(findings);

        var otherEvidence = CreateCodeReviewSnapshot(CreateWorkingTreeSnapshot() with
        {
            PathFilter = "other",
        });
        Assert.False(CopilotTurnCodeReviewSnapshotCapture.TryCaptureUpdate(
            target,
            otherEvidence,
            findingsEvent,
            out _));
    }

    [Fact]
    public void ANewDiffReplacesTheEvidenceAndClearsEarlierFindings()
    {
        var target = CopilotWorkspaceReviewTargetContext.WorkingTree();
        var current = ApplyFindings(CreateCodeReviewSnapshot(), CreateFinding());
        var replacementDiff = CreateWorkingTreeSnapshot() with
        {
            PathFilter = "Parser.cs",
        };

        Assert.True(CopilotTurnCodeReviewSnapshotCapture.TryCaptureUpdate(
            target,
            current,
            CreateDiffEvent(replacementDiff),
            out var updated));

        Assert.NotEqual(current.EvidenceId, updated.EvidenceId);
        Assert.False(updated.HasFindingsSubmission());
        Assert.Empty(updated.FindingsResult);
    }

    [Fact]
    public void CapturePersistsRedactedModelEvidenceInsteadOfTheRawPatch()
    {
        const string secret = "review-diff-secret";
        var snapshot = CreateWorkingTreeSnapshot() with
        {
            Sections =
            [
                new CopilotGitDiffSection(
                    "unstaged",
                    true,
                    true,
                    false,
                    "diff --git a/app.config b/app.config\n+api_key=" + secret),
                new CopilotGitDiffSection("staged", false, true, false, string.Empty),
                new CopilotGitDiffSection("untracked", false, true, false, string.Empty),
            ],
        };

        Assert.True(CopilotTurnCodeReviewSnapshotCapture.TryCapture(
            CopilotWorkspaceReviewTargetContext.WorkingTree(),
            CreateDiffEvent(snapshot),
            out var captured));

        Assert.DoesNotContain(secret, captured.ModelObservation, StringComparison.Ordinal);
        Assert.Contains("api_key=<redacted>", captured.ModelObservation, StringComparison.Ordinal);
        var message = new CopilotChatMessage(CopilotChatRole.Assistant, "finding")
        {
            RequestMode = CopilotAgentMode.Review,
            CodeReviewSnapshot = captured,
        };
        var json = JsonConvert.SerializeObject(message, SerializerSettings);
        Assert.DoesNotContain(secret, json, StringComparison.Ordinal);
    }

    [Fact]
    public void CapturePreservesModelOutputCompactionAsAnExplicitEvidenceLimit()
    {
        var snapshot = CreateWorkingTreeSnapshot() with
        {
            Sections =
            [
                new CopilotGitDiffSection(
                    "unstaged",
                    true,
                    true,
                    false,
                    "diff --git a/Large.cs b/Large.cs\n" + new string('x', 10_000)),
                new CopilotGitDiffSection("staged", false, true, false, string.Empty),
                new CopilotGitDiffSection("untracked", false, true, false, string.Empty),
            ],
        };
        var captured = CreateCodeReviewSnapshot(snapshot, toolOutputTokenLimit: 500);

        Assert.True(captured.TryReadModelObservation(out var modelContent, out var truncated));
        Assert.True(truncated);
        Assert.False(captured.TryReadStructuredModelDiff(out _));
        Assert.Contains("tool content compacted", modelContent, StringComparison.Ordinal);

        var message = new CopilotChatMessage(CopilotChatRole.Assistant, "bounded finding")
        {
            RequestMode = CopilotAgentMode.Review,
            CodeReviewSnapshot = captured,
        };
        var model = CopilotCodeReviewWindowModel.Create(message);
        Assert.True(model.HasEvidenceWarning);
        Assert.Contains("模型输出预算", model.EvidenceWarning, StringComparison.Ordinal);
        Assert.Equal(modelContent, model.DiffText);
    }

    [Fact]
    public async Task ToolExecutorPublishesTheSameFormattedDiffResultUsedByTheFramework()
    {
        var toolSnapshot = CreateWorkingTreeSnapshot();
        var tool = new StaticGitDiffTool(toolSnapshot);
        var request = new CopilotAgentRequest
        {
            Mode = CopilotAgentMode.Review,
            UserText = "Review the working tree.",
            ToolOutputTokenLimitOverride = 500,
        };
        var invocation = new CopilotToolInvocation
        {
            CallId = "exact-model-evidence",
            Round = 1,
            Attempt = 1,
            MaxAttempts = 1,
            RuntimeName = "code-review-test",
            Tool = tool,
            AgentRequest = request,
        };
        var events = new List<CopilotAgentEvent>();

        var outcome = await new CopilotToolExecutor().ExecuteAsync(
            invocation,
            events.Add,
            CancellationToken.None);

        var toolResultEvent = Assert.Single(
            events,
            item => item.Type == CopilotAgentEventType.ToolResult);
        Assert.NotNull(outcome.FormattedModelResult);
        Assert.Equal(outcome.FormattedModelResult, toolResultEvent.ModelToolResult);
        Assert.Equal(
            CopilotFrameworkToolResultFormatter.Format(outcome, request.ToolOutputTokenLimitOverride),
            toolResultEvent.ModelToolResult);
        Assert.True(CopilotTurnCodeReviewSnapshotCapture.TryCapture(
            CopilotWorkspaceReviewTargetContext.WorkingTree(),
            toolResultEvent,
            out _));
    }

    [Fact]
    public async Task LocalDiffReportUsesTheCanonicalGitDiffProtocol()
    {
        var status = new CopilotToolResult
        {
            ToolName = "InspectGitWorkingTree",
            Success = true,
            Content = "[Git Working Tree Inspection]\nresult_json: "
                + "{\"repository_root\":\"C:\\\\repo\",\"branch\":\"main\","
                + "\"changed_path_count\":1,\"staged_count\":0,\"unstaged_count\":1,"
                + "\"untracked_count\":0,\"conflict_count\":0,\"entries_truncated\":false,\"entries\":[]}",
        };
        var diff = new CopilotToolResult
        {
            ToolName = "InspectGitDiff",
            Success = true,
            Content = CopilotGitDiffResultProtocol.Serialize(CreateWorkingTreeSnapshot()),
        };
        var service = new CopilotLocalGitDiffService(
            (_, _, _) => Task.FromResult(status),
            (_, _, _) => Task.FromResult(diff));

        var result = await service.ExecuteAsync([@"C:\repo"], "both", CancellationToken.None);

        Assert.True(result.Success, result.Report);
        Assert.Contains("仓库：C:\\repo", result.Report, StringComparison.Ordinal);
        Assert.Contains("未暂存补丁", result.Report, StringComparison.Ordinal);
        Assert.Contains("diff --git a/Parser.cs b/Parser.cs", result.Report, StringComparison.Ordinal);
    }

    [Fact]
    public void ReducerBindsReviewSnapshotToItsMatchingToolResult()
    {
        var toolSnapshot = CreateWorkingTreeSnapshot();
        var toolEvent = CreateDiffEvent(toolSnapshot);
        Assert.True(CopilotTurnCodeReviewSnapshotCapture.TryCapture(
            CopilotWorkspaceReviewTargetContext.WorkingTree(),
            toolEvent,
            out var snapshot));
        var state = CopilotTurnEventReducer.Reduce(
            CopilotTurnEventState.Create(CopilotAgentMode.Review),
            new CopilotTurnStartedEvent(CopilotAgentMode.Review));
        state = CopilotTurnEventReducer.Reduce(
            state,
            new CopilotTurnReviewEnteredEvent(CopilotWorkspaceReviewTargetContext.WorkingTree()));
        state = CopilotTurnEventReducer.Reduce(
            state,
            new CopilotTurnAgentEvent(toolEvent));

        Assert.True(state.CodeReviewSnapshotExpected);
        Assert.Throws<InvalidOperationException>(() =>
            CopilotTurnEventReducer.Reduce(
                state,
                new CopilotTurnAgentEvent(CopilotAgentEvent.AnswerDelta("finding"))));

        state = CopilotTurnEventReducer.Reduce(
            state,
            new CopilotTurnCodeReviewSnapshotUpdatedEvent(snapshot));

        Assert.False(state.CodeReviewSnapshotExpected);
        Assert.NotSame(snapshot, state.CodeReviewSnapshot);
        Assert.Equal(snapshot, state.CodeReviewSnapshot);
    }

    [Fact]
    public void ReducerRejectsUncausedOrMismatchedReviewSnapshot()
    {
        var state = CopilotTurnEventReducer.Reduce(
            CopilotTurnEventState.Create(CopilotAgentMode.Review),
            new CopilotTurnStartedEvent(CopilotAgentMode.Review));
        state = CopilotTurnEventReducer.Reduce(
            state,
            new CopilotTurnReviewEnteredEvent(CopilotWorkspaceReviewTargetContext.WorkingTree()));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            CopilotTurnEventReducer.Reduce(
                state,
                new CopilotTurnCodeReviewSnapshotUpdatedEvent(CreateCodeReviewSnapshot())));

        Assert.Contains("without a matching Git diff result", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReducerCausallyBindsStructuredFindingsToTheirToolResult()
    {
        var target = CopilotWorkspaceReviewTargetContext.WorkingTree();
        var diffEvent = CreateDiffEvent(CreateWorkingTreeSnapshot());
        Assert.True(CopilotTurnCodeReviewSnapshotCapture.TryCapture(
            target,
            diffEvent,
            out var evidence));
        var submitted = ApplyFindings(evidence, CreateFinding());
        var findingsEvent = CreateFindingsEvent(submitted.FindingsResult);
        var state = CopilotTurnEventReducer.Reduce(
            CopilotTurnEventState.Create(CopilotAgentMode.Review),
            new CopilotTurnStartedEvent(CopilotAgentMode.Review));
        state = CopilotTurnEventReducer.Reduce(
            state,
            new CopilotTurnReviewEnteredEvent(target));
        state = CopilotTurnEventReducer.Reduce(state, new CopilotTurnAgentEvent(diffEvent));
        state = CopilotTurnEventReducer.Reduce(
            state,
            new CopilotTurnCodeReviewSnapshotUpdatedEvent(evidence));
        state = CopilotTurnEventReducer.Reduce(
            state,
            new CopilotTurnAgentEvent(findingsEvent));

        Assert.True(state.CodeReviewSnapshotExpected);
        Assert.Throws<InvalidOperationException>(() =>
            CopilotTurnEventReducer.Reduce(
                state,
                new CopilotTurnCodeReviewSnapshotUpdatedEvent(evidence)));

        state = CopilotTurnEventReducer.Reduce(
            state,
            new CopilotTurnCodeReviewSnapshotUpdatedEvent(submitted));
        Assert.False(state.CodeReviewSnapshotExpected);
        Assert.True(state.CodeReviewSnapshot!.HasFindingsSubmission());
    }

    [Fact]
    public void ReducerRejectsTurnCompletionBeforeExpectedReviewSnapshot()
    {
        var state = CopilotTurnEventReducer.Reduce(
            CopilotTurnEventState.Create(CopilotAgentMode.Review),
            new CopilotTurnStartedEvent(CopilotAgentMode.Review));
        state = CopilotTurnEventReducer.Reduce(
            state,
            new CopilotTurnReviewEnteredEvent(CopilotWorkspaceReviewTargetContext.WorkingTree()));
        state = CopilotTurnEventReducer.Reduce(
            state,
            new CopilotTurnAgentEvent(CreateDiffEvent(CreateWorkingTreeSnapshot())));
        var result = CopilotTurnResult.FromAgent(
            CopilotAgentMode.Review,
            CopilotTokenUsage.Empty,
            new CopilotAgentRunResult());

        var exception = Assert.Throws<InvalidOperationException>(() =>
            CopilotTurnEventReducer.Reduce(state, new CopilotTurnCompletedEvent(result)));

        Assert.Contains("before its code review snapshot update", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EventSinkCopiesModelVisibleEvidenceSnapshot()
    {
        var snapshot = CreateCodeReviewSnapshot();
        var emitted = new List<CopilotTurnEvent>();
        var sink = new CopilotTurnEventSink(emitted.Add);

        sink.OnCodeReviewSnapshotUpdated(snapshot);

        var update = Assert.IsType<CopilotTurnCodeReviewSnapshotUpdatedEvent>(Assert.Single(emitted));
        Assert.NotSame(snapshot, update.Snapshot);
        Assert.Equal(snapshot, update.Snapshot);
        Assert.True(update.Snapshot.IsStructurallyValid());
    }

    [Fact]
    public void ReviewMessageRoundTripsSnapshotAndBuildsReadOnlyPaneModel()
    {
        var message = new CopilotChatMessage(
            CopilotChatRole.Assistant,
            "[P1] Fix the boundary check in `Parser.cs:42`.")
        {
            RequestMode = CopilotAgentMode.Review,
            CodeReviewSnapshot = CreateCodeReviewSnapshot(),
        };

        var json = JsonConvert.SerializeObject(message, SerializerSettings);
        var restored = JsonConvert.DeserializeObject<CopilotChatMessage>(json);

        Assert.NotNull(restored);
        restored.EnsureValid();
        Assert.True(restored.HasCodeReviewSnapshot);
        Assert.Contains(nameof(CopilotChatMessage.CodeReviewSnapshot), json, StringComparison.Ordinal);
        Assert.Contains("当前未提交变更", restored.CodeReviewSnapshotHeader, StringComparison.Ordinal);

        var model = CopilotCodeReviewWindowModel.Create(restored);
        Assert.Equal(restored.Content, model.ConclusionText);
        Assert.Contains("未暂存 Diff", model.DiffText, StringComparison.Ordinal);
        Assert.Contains("已暂存 Diff", model.DiffText, StringComparison.Ordinal);
        Assert.Contains("diff --git a/Parser.cs b/Parser.cs", model.DiffText, StringComparison.Ordinal);
        Assert.Equal("整个仓库", model.PathLabel);
        Assert.False(model.HasEvidenceWarning);
        Assert.Equal(restored.CodeReviewSnapshot!.ModelObservation, model.ModelObservationText);

        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "review")
        {
            RequestMode = CopilotAgentMode.Review,
        });
        conversation.Messages.Add(restored);
        var branch = CopilotConversationBranchService.CreateBranch(conversation, restored);
        var branchedReview = Assert.Single(branch.Messages, item => !item.IsUser);
        Assert.True(branchedReview.HasCodeReviewSnapshot);
        Assert.Equal(restored.CodeReviewSnapshot, branchedReview.CodeReviewSnapshot);
        Assert.NotSame(restored.CodeReviewSnapshot, branchedReview.CodeReviewSnapshot);
    }

    [Fact]
    public void SubmittedFindingsPersistAndPopulateTheReadOnlyPane()
    {
        var snapshot = ApplyFindings(CreateCodeReviewSnapshot(), CreateFinding());
        var message = new CopilotChatMessage(CopilotChatRole.Assistant, "review conclusion")
        {
            RequestMode = CopilotAgentMode.Review,
            CodeReviewSnapshot = snapshot,
        };

        var restored = JsonConvert.DeserializeObject<CopilotChatMessage>(
            JsonConvert.SerializeObject(message, SerializerSettings));

        Assert.NotNull(restored);
        restored.EnsureValid();
        Assert.Contains("1 条 finding", restored.CodeReviewSnapshotHeader, StringComparison.Ordinal);
        var model = CopilotCodeReviewWindowModel.Create(restored);
        var finding = Assert.Single(model.Findings);
        Assert.Equal("P1", finding.Priority);
        Assert.Equal("Parser.cs:42 · 新行", finding.LocationLabel);
        Assert.Contains("行级 Findings (1)", model.FindingsTabHeader, StringComparison.Ordinal);
        Assert.Contains("Parser.cs:42", model.FindingsText, StringComparison.Ordinal);
    }

    [Fact]
    public void MessageNormalizationRemovesReviewEvidenceFromInvalidOwner()
    {
        var message = new CopilotChatMessage(CopilotChatRole.User, "review")
        {
            RequestMode = CopilotAgentMode.Review,
            CodeReviewSnapshot = CreateCodeReviewSnapshot(),
        };

        Assert.True(message.EnsureValid());
        Assert.Null(message.CodeReviewSnapshot);
        Assert.False(message.HasCodeReviewSnapshot);
    }

    [Fact]
    public void MessageDeserializationDefersMalformedSnapshotRemovalToNormalization()
    {
        var message = new CopilotChatMessage(CopilotChatRole.Assistant, "finding")
        {
            RequestMode = CopilotAgentMode.Review,
            CodeReviewSnapshot = CreateCodeReviewSnapshot(),
        };
        var document = JObject.Parse(JsonConvert.SerializeObject(message, SerializerSettings));
        document[nameof(CopilotChatMessage.CodeReviewSnapshot)]![nameof(CopilotCodeReviewSnapshot.ModelObservation)] = "{bad";

        var restored = JsonConvert.DeserializeObject<CopilotChatMessage>(document.ToString(Formatting.None));

        Assert.NotNull(restored);
        Assert.False(restored.HasCodeReviewSnapshot);
        Assert.True(restored.EnsureValid());
        Assert.Null(restored.CodeReviewSnapshot);
    }

    [Fact]
    public void PresenterPersistsReviewEvidenceImmediately()
    {
        var assistant = new CopilotChatMessage(CopilotChatRole.Assistant, "finding")
        {
            RequestMode = CopilotAgentMode.Review,
        };
        var snapshot = CreateCodeReviewSnapshot();

        var result = CopilotAssistantMessagePresenter.ApplyCodeReviewSnapshotUpdated(
            assistant,
            snapshot);

        Assert.Equal(CopilotAgentEventPersistenceMode.Immediate, result.PersistenceMode);
        Assert.True(assistant.HasCodeReviewSnapshot);
        Assert.NotSame(snapshot, assistant.CodeReviewSnapshot);
        Assert.Equal(snapshot, assistant.CodeReviewSnapshot);
    }

    private static CopilotGitDiffSnapshot CreateWorkingTreeSnapshot() => new(
        @"C:\repo",
        "both",
        string.Empty,
        true,
        true,
        false,
        [
            new CopilotGitDiffSection(
                "unstaged",
                true,
                true,
                false,
                "diff --git a/Parser.cs b/Parser.cs\n--- a/Parser.cs\n+++ b/Parser.cs\n@@ -42 +42 @@\n-old\n+new"),
            new CopilotGitDiffSection("staged", false, true, false, string.Empty),
            new CopilotGitDiffSection("untracked", false, true, false, string.Empty),
        ])
    {
        ChangedPaths = ["Parser.cs"],
        ChangedPathsComplete = true,
    };

    private static CopilotGitDiffSnapshot CreateCommitSnapshot(string revision) => new(
        @"C:\repo",
        "unstaged",
        string.Empty,
        true,
        true,
        false,
        [
            new CopilotGitDiffSection(
                "commit",
                true,
                true,
                false,
                "diff --git a/Parser.cs b/Parser.cs\n--- a/Parser.cs\n+++ b/Parser.cs\n@@ -42 +42 @@\n-old\n+new"),
        ])
    {
        Target = "commit",
        Revision = revision,
        ResolvedRevision = new string('d', 40),
        ChangedPaths = ["Parser.cs"],
        ChangedPathsComplete = true,
    };

    private static CopilotCodeReviewSnapshot CreateCodeReviewSnapshot(
        CopilotGitDiffSnapshot? toolSnapshot = null,
        int? toolOutputTokenLimit = null,
        string? modelFeedback = null)
    {
        toolSnapshot ??= CreateWorkingTreeSnapshot();
        Assert.True(CopilotTurnCodeReviewSnapshotCapture.TryCapture(
            CopilotWorkspaceReviewTargetContext.WorkingTree(),
            CreateDiffEvent(toolSnapshot, toolOutputTokenLimit, modelFeedback),
            out var snapshot));
        return snapshot;
    }

    private static CopilotCodeReviewSnapshot ApplyFindings(
        CopilotCodeReviewSnapshot snapshot,
        params CopilotCodeReviewFinding[] findings)
    {
        var ordered = CopilotCodeReviewFindingsResultProtocol.OrderFindings(findings);
        var result = CopilotCodeReviewFindingsResultProtocol.Serialize(
            new CopilotCodeReviewFindingsSubmission(snapshot.EvidenceId, ordered));
        Assert.True(snapshot.TryApplyFindings(result, out var updated));
        return updated;
    }

    private static CopilotCodeReviewFinding CreateFinding() => new(
        "P1",
        "Fix the boundary check",
        "The changed condition accepts an invalid boundary value; reject it before parsing.",
        "Parser.cs",
        "new",
        42,
        42);

    private static CopilotAgentToolInput CreateFindingsInput(
        params CopilotCodeReviewFinding[] findings) => new()
        {
            Arguments = new Dictionary<string, object?>
            {
                ["findings"] = findings.Select(finding => new Dictionary<string, object?>
                {
                    ["priority"] = finding.Priority,
                    ["title"] = finding.Title,
                    ["body"] = finding.Body,
                    ["path"] = finding.Path,
                    ["side"] = finding.Side,
                    ["line_start"] = finding.LineStart,
                    ["line_end"] = finding.LineEnd,
                }).ToArray(),
            },
        };

    private static CopilotAgentEvent CreateFindingsEvent(string content)
    {
        var result = new CopilotToolResult
        {
            ToolName = "SubmitCodeReviewFindings",
            Success = true,
            Summary = "Submitted structured code review findings.",
            Content = content,
        };
        var completedAt = DateTimeOffset.UtcNow;
        return CopilotAgentEvent.FromToolResult(
            result,
            new CopilotToolExecutionInfo
            {
                CallId = "review-findings-call",
                Round = 2,
                Attempt = 1,
                MaxAttempts = 1,
                RuntimeName = "protocol-test",
                ToolName = result.ToolName,
                Access = CopilotToolAccess.ReadOnly,
                RiskLevel = CopilotToolRiskLevel.Low,
                ApprovalMode = CopilotToolApprovalMode.Never,
                Idempotency = CopilotToolIdempotency.Idempotent,
                ConcurrencyMode = CopilotToolConcurrencyMode.SharedRead,
                ConcurrencyKey = "review-findings",
                State = CopilotToolExecutionState.Completed,
                StartedAtUtc = completedAt.AddMilliseconds(-1),
                CompletedAtUtc = completedAt,
                DurationMs = 1,
                TimeoutMs = 5_000,
            });
    }

    private static CopilotAgentEvent CreateDiffEvent(
        CopilotGitDiffSnapshot snapshot,
        int? toolOutputTokenLimit = null,
        string? modelFeedback = null)
    {
        var result = new CopilotToolResult
        {
            ToolName = "InspectGitDiff",
            Success = true,
            Summary = "Git returned staged and unstaged changes.",
            Content = CopilotGitDiffResultProtocol.Serialize(snapshot),
        };
        var completedAt = DateTimeOffset.UtcNow;
        var execution = new CopilotToolExecutionInfo
        {
            CallId = "review-diff-call",
            Round = 1,
            Attempt = 1,
            MaxAttempts = 1,
            RuntimeName = "protocol-test",
            ToolName = "InspectGitDiff",
            Access = CopilotToolAccess.ReadOnly,
            RiskLevel = CopilotToolRiskLevel.Low,
            ApprovalMode = CopilotToolApprovalMode.Never,
            Idempotency = CopilotToolIdempotency.Idempotent,
            ConcurrencyMode = CopilotToolConcurrencyMode.SharedRead,
            ConcurrencyKey = "git-read",
            ArgumentSummary = "target=working_tree, scope=both",
            State = CopilotToolExecutionState.Completed,
            StartedAtUtc = completedAt.AddMilliseconds(-1),
            CompletedAtUtc = completedAt,
            DurationMs = 1,
            TimeoutMs = 30_000,
        };
        var outcome = new CopilotToolExecutionOutcome
        {
            Result = result,
            Execution = execution,
        };
        if (!string.IsNullOrWhiteSpace(modelFeedback))
            outcome.ApplyModelVisibleFeedback(modelFeedback);
        var modelToolResult = CopilotFrameworkToolResultFormatter.Format(
            outcome,
            toolOutputTokenLimit);
        return CopilotAgentEvent.FromToolResult(
            result,
            execution,
            hookRuns: null,
            modelToolResult: modelToolResult);
    }

    private sealed class StaticGitDiffTool(CopilotGitDiffSnapshot snapshot) : ICopilotTool
    {
        public string Name => "InspectGitDiff";

        public string Description => "Returns deterministic Git diff evidence for protocol tests.";

        public CopilotToolCapabilityDescriptor Capability { get; } =
            CopilotToolCapabilityDescriptor.ReadOnly();

        public bool CanHandle(CopilotAgentRequest request) => true;

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken) => Task.FromResult(new CopilotToolResult
            {
                ToolName = Name,
                Success = true,
                Summary = "Git returned staged and unstaged changes.",
                Content = CopilotGitDiffResultProtocol.Serialize(snapshot),
            });
    }
}
