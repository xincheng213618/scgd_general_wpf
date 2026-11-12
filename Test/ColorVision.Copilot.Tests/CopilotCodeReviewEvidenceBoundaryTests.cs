namespace ColorVision.Copilot.Tests;

public sealed class CopilotCodeReviewEvidenceBoundaryTests
{
    [Theory]
    [InlineData(1, '中', true)]
    [InlineData(6, '中', false)]
    [InlineData(33, 'a', false)]
    public async Task SubmittedFindingsMustFitTheSharedSerializedPayloadLimit(int findingCount, char bodyCharacter, bool expectedSuccess)
    {
        var patch = "--- /dev/null\n+++ b/Changed.cs\n@@ -0,0 +1," + findingCount + " @@\n"
            + string.Join('\n', Enumerable.Range(1, findingCount).Select(line => "+visible line " + line)) + "\n";
        var snapshot = CreateReviewSnapshot(CreateDiff(patch));
        var context = new CopilotReviewEvidenceContext();
        context.RecordEvidence(snapshot);
        var body = new string(bodyCharacter, CopilotCodeReviewFindingsResultProtocol.MaximumBodyCharacters);
        var findings = Enumerable.Range(1, findingCount)
            .Select(line => Finding("Changed.cs", "new", line, line) with { Title = "Concrete defect " + line, Body = body })
            .ToArray();
        Assert.True(snapshot.TryReadStructuredModelDiff(out var modelDiff));
        Assert.All(findings, finding =>
        {
            Assert.True(CopilotCodeReviewFindingsResultProtocol.TryNormalizeFinding(finding, out _, out var error), error);
            Assert.True(CopilotGitDiffLineCoverage.Contains(modelDiff, finding));
        });

        var result = await new CopilotSubmitCodeReviewFindingsTool().ExecuteAsync(new()
        {
            Mode = CopilotAgentMode.Review, ReviewEvidenceContext = context,
        }, new()
        {
            Arguments = new Dictionary<string, object?>
            {
                ["findings"] = findings.Select(finding => new
                {
                    priority = finding.Priority, title = finding.Title, body = finding.Body, path = finding.Path,
                    side = finding.Side, line_start = finding.LineStart, line_end = finding.LineEnd,
                }).ToArray(),
            },
        }, CancellationToken.None);

        Assert.Equal(expectedSuccess, result.Success);
        if (!expectedSuccess)
        {
            Assert.Equal(CopilotToolFailureKind.Validation, result.FailureKind);
            Assert.NotEmpty(result.ErrorMessage);
            Assert.Empty(result.Content);
            return;
        }

        Assert.True(CopilotCodeReviewFindingsResultProtocol.TryParse(result.Content, out var parsed, out var parseError), parseError);
        Assert.Equal(findings, parsed.Findings);
        Assert.True(snapshot.TryApplyFindings(result.Content, out var submitted));
        Assert.True(submitted.TryReadFindings(out var restored));
        Assert.Equal(findings, restored);
    }

    [Theory]
    [InlineData("old", 10, 12, true)]
    [InlineData("new", 20, 23, true)]
    [InlineData("old", 13, 13, false)]
    [InlineData("new", 24, 24, false)]
    public void CoverageCountsVisibleContextAddedAndDeletedLines(string side, int start, int end, bool expected)
    {
        var diff = CreateDiff("--- a/Changed.cs\n+++ b/Changed.cs\n@@ -10,3 +20,4 @@\n context\n-deleted\n+added\n+extra\n context\n");
        Assert.Equal(expected, CopilotGitDiffLineCoverage.Contains(diff, Finding("Changed.cs", side, start, end)));
    }

    [Theory]
    [InlineData(1, 1, true)]
    [InlineData(2, 2, false)]
    [InlineData(500, 500, false)]
    [InlineData(1000, 1000, false)]
    [InlineData(1, 500, false)]
    [InlineData(2000, 2000, true)]
    public void TruncatedHunkCoversOnlyActualLinesUntilTheNextCompleteHeader(int start, int end, bool expected)
    {
        var diff = CreateDiff("--- /dev/null\n+++ b/Changed.cs\n@@ -0,0 +1,1000 @@\n+visible first\n+possibly partial boundary"
            + "\n"
            + "...<Git diff excerpt truncated>...\n+unlocated tail\n@@ -0,0 +2000 @@\n+visible later\n", truncated: true);
        Assert.Equal(expected, CopilotGitDiffLineCoverage.Contains(diff, Finding("Changed.cs", "new", start, end)));
    }

    [Fact]
    public void CodeLiteralContainingATruncationMarkerRemainsVisibleEvidence()
    {
        var diff = CreateDiff("--- /dev/null\n+++ b/Changed.cs\n@@ -0,0 +1,2 @@\n+var before = 1;\n+var marker = \"...<Git diff excerpt truncated>...\";\n");
        var context = new CopilotReviewEvidenceContext();
        context.RecordEvidence(CreateReviewSnapshot(diff));
        Assert.True(context.TryCreateSubmission([Finding("Changed.cs", "new", 1, 2)], out _, out var error), error);
    }

    [Theory]
    [InlineData("New File.cs", "\"b/New File.cs\"")]
    [InlineData("中文.cs", "\"b/\\344\\270\\255\\346\\226\\207.cs\"")]
    public void FindingsAcceptGitQuotedPaths(string path, string patchPath)
    {
        var diff = CreateDiff($"--- /dev/null\n+++ {patchPath}\n@@ -0,0 +1 @@\n+visible\n", path: path);
        var context = new CopilotReviewEvidenceContext();
        context.RecordEvidence(CreateReviewSnapshot(diff));
        Assert.True(context.TryCreateSubmission([Finding(path, "new", 1, 1)], out var submitted, out var error), error);
        Assert.True(CopilotCodeReviewFindingsResultProtocol.TryParse(submitted, out var result, out error), error);
        Assert.Equal(path, Assert.Single(result.Findings).Path);
    }

    [Fact]
    public void DifferentLatestTargetInvalidatesTheOldSnapshotAndRejectsItsDelayedSubmission()
    {
        var target = CopilotWorkspaceReviewTargetContext.WorkingTree();
        var first = CreateReviewSnapshot(CreateDiff("--- /dev/null\n+++ b/Changed.cs\n@@ -0,0 +1 @@\n+visible\n"));
        var oldSubmission = CopilotCodeReviewFindingsResultProtocol.Serialize(new(first.EvidenceId, []));
        Assert.True(first.TryApplyFindings(oldSubmission, out first));
        var other = CreateDiff(string.Empty, scope: "staged");
        var toolResult = new CopilotToolResult
        {
            ToolName = "InspectGitDiff", Success = true, Content = CopilotGitDiffResultProtocol.Serialize(other),
        };
        var outcome = Outcome(toolResult);
        var agentEvent = CopilotAgentEvent.FromToolResult(toolResult, null, null, CopilotFrameworkToolResultFormatter.Format(outcome));

        Assert.True(CopilotTurnCodeReviewSnapshotCapture.TryCaptureUpdate(target, first, agentEvent, out var invalidated));
        Assert.True(invalidated.IsStructurallyValid());
        Assert.Equal(first.ModelObservation, invalidated.ModelObservation);
        Assert.Equal("both", invalidated.Scope);
        Assert.False(invalidated.HasFindingsSubmission());
        Assert.False(invalidated.TryApplyFindings(oldSubmission, out _));
    }

    [Fact]
    public void PendingThenDeniedInspectionPreservesCompletedReviewEvidence()
    {
        var target = CopilotWorkspaceReviewTargetContext.WorkingTree();
        var current = CreateReviewSnapshot(CreateDiff("--- /dev/null\n+++ b/Changed.cs\n@@ -0,0 +1 @@\n+visible\n"));
        var submission = CopilotCodeReviewFindingsResultProtocol.Serialize(new(current.EvidenceId, []));
        Assert.True(current.TryApplyFindings(submission, out current));
        var pending = new CopilotToolResult
        {
            ToolName = "InspectGitDiff", Success = true,
            Approval = new CopilotToolApprovalInfo { ActionId = "pending-other-target", ResumesAgentOnApproval = true },
        };
        var pendingEvent = CopilotAgentEvent.FromToolResult(pending,
            new CopilotToolExecutionInfo { ToolName = pending.ToolName, State = CopilotToolExecutionState.AwaitingApproval });
        Assert.False(CopilotTurnCodeReviewSnapshotCapture.TryCaptureUpdate(target, current, pendingEvent, out _));

        var denied = new CopilotToolResult { ToolName = "InspectGitDiff", Success = false, FailureKind = CopilotToolFailureKind.Authorization };
        var deniedEvent = CopilotAgentEvent.FromToolResult(denied,
            new CopilotToolExecutionInfo { ToolName = denied.ToolName, State = CopilotToolExecutionState.Denied });
        Assert.False(CopilotTurnCodeReviewSnapshotCapture.TryCaptureUpdate(target, current, deniedEvent, out _));
        Assert.True(current.HasFindingsSubmission());
        Assert.True(current.TryApplyFindings(submission, out _));
    }

    private static CopilotCodeReviewFinding Finding(string path, string side, int start, int end) =>
        new("P2", "Concrete defect", "A defect grounded in this visible line.", path, side, start, end);

    private static CopilotGitDiffSnapshot CreateDiff(string patch, bool truncated = false, string path = "Changed.cs", string scope = "both") =>
        new(@"C:\repo", scope, string.Empty, patch.Length > 0, !truncated, truncated,
            scope == "both"
                ? [new("unstaged", patch.Length > 0, !truncated, truncated, patch), new("staged", false, true, false, string.Empty), new("untracked", false, true, false, string.Empty)]
                : [new(scope, patch.Length > 0, !truncated, truncated, patch)])
        {
            ChangedPaths = patch.Length > 0 ? [path] : [], ChangedPathsComplete = true,
        };

    private static CopilotCodeReviewSnapshot CreateReviewSnapshot(CopilotGitDiffSnapshot diff)
    {
        var result = new CopilotToolResult
        {
            ToolName = "InspectGitDiff", Success = true, Content = CopilotGitDiffResultProtocol.Serialize(diff),
        };
        var observation = CopilotFrameworkToolResultFormatter.Format(Outcome(result));
        Assert.True(CopilotCodeReviewSnapshot.TryCreate(diff, observation, out var snapshot));
        return snapshot;
    }

    private static CopilotToolExecutionOutcome Outcome(CopilotToolResult result) => new()
    {
        Result = result,
        Execution = new CopilotToolExecutionInfo { ToolName = result.ToolName, State = CopilotToolExecutionState.Completed },
    };
}
