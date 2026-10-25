using System.IO;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotReviewRepositoryBindingTests
{
    [Theory]
    [InlineData("A", "", "A")]
    [InlineData("A/sub", "A", "A")]
    [InlineData("A/sub", "A/sub", "")]
    [InlineData("A/nested", "A", "A/nested")]
    [InlineData("plain", "", "")]
    [InlineData("missing", "", "")]
    [InlineData("", "", null)]
    public void ReviewBindsOnlyTheWorkspaceRepositoryInsideAllowedRoots(
        string workspacePath, string allowedRoot, string? expectedRepository)
    {
        using var workspace = new Workspace();
        var request = new CopilotAgentRequest
        {
            Mode = CopilotAgentMode.Review,
            WorkspacePath = workspacePath.Length == 0 ? string.Empty : workspace.PathFor(workspacePath),
            SearchRootPaths = [workspace.PathFor(allowedRoot)],
        };
        var target = request.GetEffectiveWorkspaceReviewTarget()!;
        var expectedRoot = expectedRepository is null or "" ? expectedRepository : workspace.PathFor(expectedRepository);
        Assert.Equal(expectedRoot, target.BoundRepositoryRoot);

        var unrelatedDiff = EmptyDiff(workspace.RepositoryB);
        Assert.Equal(expectedRoot == null, CopilotTurnCodeReviewSnapshotCapture.MatchesTarget(target, unrelatedDiff));
        Assert.Equal(expectedRoot == null, CopilotTurnCodeReviewSnapshotCapture.MatchesTarget(target, ReviewSnapshot(unrelatedDiff)));
        if (!string.IsNullOrEmpty(expectedRoot))
        {
            var filteredDiff = EmptyDiff(expectedRoot) with { PathFilter = "Changed.cs" };
            Assert.True(CopilotTurnCodeReviewSnapshotCapture.MatchesTarget(target, filteredDiff));
            Assert.True(CopilotTurnCodeReviewSnapshotCapture.MatchesTarget(target, ReviewSnapshot(filteredDiff)));
        }
        else if (expectedRoot == string.Empty)
        {
            var contract = CopilotAgentExecutionContract.Create(request, [new CopilotInspectGitDiffTool()]);
            var mismatch = contract.Evaluate([Step(DiffResult(unrelatedDiff), DiffInput(workspace.RepositoryB), 1)]);
            Assert.False(mismatch.IsSatisfied);
            Assert.False(mismatch.ShouldReinvoke);
            Assert.Contains("could not be resolved", mismatch.Feedback, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void FactoryFreezesDetachedRepositoryBindingWithoutPersistingIt()
    {
        using var workspace = new Workspace();
        var source = CopilotWorkspaceReviewTargetContext.WorkingTree();
        var request = CopilotAgentRequestFactory.Create(
            new CopilotAgentRequestPlan { Mode = CopilotAgentMode.Review, SearchRootPaths = [workspace.RepositoryA] },
            new CopilotAgentRequestBuildInput
            {
                Profile = new CopilotProfileConfig(), WorkspacePath = workspace.PathFor("A/sub"),
                WorkspaceReviewTarget = source,
            });
        Directory.CreateDirectory(workspace.PathFor("A/sub/.git"));
        var first = request.GetEffectiveWorkspaceReviewTarget()!;
        Assert.Equal(workspace.RepositoryA, first.BoundRepositoryRoot);
        Assert.Equal(workspace.RepositoryA, first.CreateSnapshot().BoundRepositoryRoot);
        Assert.Null(source.BoundRepositoryRoot);
        Assert.Null(request.WorkspaceReviewTarget!.BoundRepositoryRoot);
        Assert.DoesNotContain("BoundRepositoryRoot", JsonSerializer.Serialize(first), StringComparison.Ordinal);
        Assert.DoesNotContain("BoundRepositoryRoot", Newtonsoft.Json.JsonConvert.SerializeObject(first), StringComparison.Ordinal);
        first.BoundRepositoryRoot = workspace.RepositoryB;
        Assert.Equal(workspace.RepositoryA, request.GetEffectiveWorkspaceReviewTarget()!.BoundRepositoryRoot);
        Assert.Null(new CopilotAgentRequest
        {
            Mode = CopilotAgentMode.Auto, WorkspacePath = workspace.RepositoryA, SearchRootPaths = [workspace.RepositoryA],
        }.GetEffectiveWorkspaceReviewTarget());
    }

    [Fact]
    public async Task WorkingTreeStatusMustBelongToTheSameRepositoryAsTheReviewDiff()
    {
        using var workspace = new Workspace();
        var request = new CopilotAgentRequest
        {
            Mode = CopilotAgentMode.Review, UserText = "Review the current working tree.",
            WorkspacePath = workspace.RepositoryA, SearchRootPaths = [workspace.RepositoryA, workspace.RepositoryB],
            ReviewEvidenceContext = new CopilotReviewEvidenceContext(),
        };
        var runner = new GitRunner();
        var statusService = new CopilotGitWorkingTreeInspectionService(runner, () => workspace.GitExecutable);
        var statusB = await statusService.ExecuteAsync(request, new CopilotAgentToolInput { Path = workspace.RepositoryB }, CancellationToken.None);
        var statusA = await statusService.ExecuteAsync(request, new CopilotAgentToolInput { Path = workspace.RepositoryA }, CancellationToken.None);
        Assert.True(statusA.Success, statusA.ErrorMessage);
        Assert.True(statusB.Success, statusB.ErrorMessage);
        var diffInput = DiffInput(workspace.RepositoryA);
        var diffResult = await new CopilotGitDiffInspectionService(runner, () => workspace.GitExecutable)
            .ExecuteAsync(request, diffInput, CancellationToken.None);
        Assert.True(diffResult.Success, diffResult.ErrorMessage);
        var diff = Step(diffResult, diffInput, 2);
        Assert.True(CopilotGitDiffResultProtocol.TryParse(diffResult.Content, out var snapshot, out var error), error);
        Assert.True(CopilotCodeReviewSnapshot.TryCreate(snapshot, diff.ModelToolResult, out var evidence));
        request.ReviewEvidenceContext.RecordEvidence(evidence);
        var submission = await new CopilotSubmitCodeReviewFindingsTool().ExecuteAsync(request,
            new CopilotAgentToolInput { Arguments = new Dictionary<string, object?> { ["findings"] = Array.Empty<object>() } },
            CancellationToken.None);
        Assert.True(submission.Success, submission.ErrorMessage);
        var findings = Step(submission, CopilotAgentToolInput.Empty, 3);
        var contract = CopilotAgentExecutionContract.Create(request,
            [new CopilotInspectGitWorkingTreeTool(), new CopilotInspectGitDiffTool(), new CopilotSubmitCodeReviewFindingsTool()]);

        var mismatchedSteps = new[] { Step(statusB, new CopilotAgentToolInput { Path = workspace.RepositoryB }, 1), diff, findings };
        var mismatch = contract.Evaluate(mismatchedSteps);
        Assert.False(mismatch.IsSatisfied);
        Assert.True(mismatch.ShouldReinvoke);
        Assert.Equal(["InspectGitWorkingTree"], mismatch.MissingToolNames);
        Assert.Contains(JsonSerializer.Serialize(workspace.RepositoryA), mismatch.Feedback, StringComparison.Ordinal);
        Assert.True(contract.Evaluate([Step(statusA, new CopilotAgentToolInput { Path = workspace.RepositoryA }, 1), diff, findings]).IsSatisfied);

        // Recovery appends evidence to the real history; it cannot replace round 1.
        var corrected = contract.Evaluate(
            [.. mismatchedSteps, Step(statusA, new CopilotAgentToolInput { Path = workspace.RepositoryA }, 4)]);
        Assert.True(corrected.IsSatisfied, $"Corrected repository status must preserve the existing diff and findings. {corrected.Feedback}");
    }

    private static CopilotAgentToolInput DiffInput(string path) => new()
    {
        Path = path,
        Arguments = new Dictionary<string, object?> { ["target"] = "working_tree", ["scope"] = "both" },
    };

    private static CopilotGitDiffSnapshot EmptyDiff(string repositoryRoot) => new(repositoryRoot, "both", string.Empty, false, true, false,
        [new("unstaged", false, true, false, string.Empty), new("staged", false, true, false, string.Empty),
            new("untracked", false, true, false, string.Empty)]) { ChangedPathsComplete = true };

    private static CopilotToolResult DiffResult(CopilotGitDiffSnapshot diff) => new()
    {
        ToolName = "InspectGitDiff", Success = true, Content = CopilotGitDiffResultProtocol.Serialize(diff),
    };

    private static CopilotCodeReviewSnapshot ReviewSnapshot(CopilotGitDiffSnapshot diff)
    {
        var step = Step(DiffResult(diff), CopilotAgentToolInput.Empty, 1);
        Assert.True(CopilotCodeReviewSnapshot.TryCreate(diff, step.ModelToolResult, out var snapshot));
        return snapshot;
    }

    private static CopilotAgentStepRecord Step(CopilotToolResult result, CopilotAgentToolInput input, int round)
    {
        var execution = new CopilotToolExecutionInfo { ToolName = result.ToolName, State = CopilotToolExecutionState.Completed, Round = round };
        return new CopilotAgentStepRecord
        {
            Round = round, ToolCall = new CopilotToolCall { ToolName = result.ToolName, ToolInput = input },
            Observation = CopilotToolObservation.FromResult(result), Execution = execution,
            ModelToolResult = CopilotFrameworkToolResultFormatter.Format(new CopilotToolExecutionOutcome { Result = result, Execution = execution }),
        };
    }

    private sealed class GitRunner : ICopilotShellProcessRunner
    {
        public Task<CopilotShellProcessResult> RunAsync(CopilotShellProcessCommand command, CancellationToken cancellationToken) =>
            Task.FromResult(new CopilotShellProcessResult(0, false,
                command.Arguments.Contains("status") ? "# branch.oid abcdef\n# branch.head main\n" : string.Empty,
                string.Empty, TimeSpan.FromMilliseconds(1)));
    }

    private sealed class Workspace : IDisposable
    {
        private readonly string _root = Directory.CreateTempSubdirectory("CopilotReviewRepository-").FullName;
        public string RepositoryA => PathFor("A");
        public string RepositoryB => PathFor("B");
        public string GitExecutable => PathFor("git.exe");
        public string PathFor(string relative) => Path.GetFullPath(Path.Combine(_root, relative));

        public Workspace()
        {
            Directory.CreateDirectory(PathFor("A/.git"));
            Directory.CreateDirectory(PathFor("A/sub"));
            Directory.CreateDirectory(PathFor("A/nested"));
            File.WriteAllText(PathFor("A/nested/.git"), "gitdir: linked-worktree-metadata");
            Directory.CreateDirectory(PathFor("B/.git"));
            Directory.CreateDirectory(PathFor("plain"));
            File.WriteAllText(GitExecutable, string.Empty);
        }

        public void Dispose()
        {
            var resolved = Path.GetFullPath(_root);
            Assert.Equal(Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())), Path.GetDirectoryName(resolved), ignoreCase: true);
            Assert.StartsWith("CopilotReviewRepository-", Path.GetFileName(resolved), StringComparison.Ordinal);
            Directory.Delete(resolved, recursive: true);
        }
    }
}
