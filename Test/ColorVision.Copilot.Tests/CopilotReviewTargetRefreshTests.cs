using ColorVision.Copilot.Mcp;
using ColorVision.Solution;
using ColorVision.Solution.Explorer;
using Microsoft.Extensions.AI;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ColorVision.Copilot.Tests;

[Collection(CopilotApprovalReviewTestGroup.CollectionName)]
public sealed class CopilotReviewTargetRefreshTests
{
    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    [InlineData(false, false, true)]
    public async Task ReviewCanReinspectTheRequestedTargetAfterDifferentEvidenceAndResubmitTheSameEmptyFindings(
        bool explicitTarget, bool pathFilteredEvidence, bool otherRepositoryEvidence)
    {
        using var workspace = new Workspace();
        using var otherWorkspace = otherRepositoryEvidence ? new Workspace(setCurrentSolution: false) : null;
        var runner = new GitRunner(workspace.Root);
        var diff = new CopilotInspectGitDiffTool(new CopilotGitDiffInspectionService(runner, () => workspace.GitExecutable));
        ICopilotTool[] tools = [diff, new CopilotSubmitCodeReviewFindingsTool()];
        using var provider = new ReviewProvider(pathFilteredEvidence, otherWorkspace?.Root);
        var catalog = new CopilotCapabilityCatalog();
        catalog.PublishSource(CopilotCapabilitySourceKind.BuiltIn, "review-refresh", "Review refresh", tools);
        var runtime = new CopilotMicrosoftAgentFrameworkRuntime(new CopilotToolRegistry(tools),
            new CopilotAgentContextBuilder(), new CopilotToolExecutor(hooks: []), _ => provider,
            new NoExternalTools(), catalog, new CopilotAgentSkillUsageStore(workspace.Root));
        var request = new CopilotAgentRequest
        {
            ConversationId = "review-refresh", TaskId = "review-refresh-task", WorkspacePath = workspace.Root,
            Mode = CopilotAgentMode.Review, Profile = new CopilotProfileConfig { ProviderType = CopilotProviderType.LocalCodex },
            UserText = "Review the current working tree without changing files.",
            TaskIntentText = "Review the current working tree without changing files.",
            WorkspaceReviewTarget = explicitTarget ? CopilotWorkspaceReviewTargetContext.WorkingTree() : null,
            ReviewEvidenceContext = new CopilotReviewEvidenceContext(),
            SearchRootPaths = otherWorkspace == null ? [workspace.Root] : [workspace.Root, otherWorkspace.Root],
            HarnessFeatures = CopilotAgentHarnessFeatures.None,
            CodexApprovalPolicy = CopilotCodexApprovalPolicy.CreateScalar(CopilotCodexApprovalPolicyMode.OnRequest),
            RunBudgetOverride = new CopilotAgentRunBudgetOverride
            {
                MaxToolCalls = 12, MaxAgentPasses = 2, RequestTokenBudget = 32_768, TotalDuration = TimeSpan.FromSeconds(20),
            },
        };
        CopilotCodeReviewSnapshot? captured = null;
        var approvalCount = 0;
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var result = await runtime.RunAsync(request, item =>
        {
            if (item.ToolExecution?.State == CopilotToolExecutionState.AwaitingApproval)
            {
                Assert.Equal("InspectGitDiff", item.ToolExecution.ToolName);
                Assert.NotNull(item.ToolResult?.Approval);
                var reviewContext = new CopilotConfirmationReviewContext(request.ConversationId, request.TaskId, workspace.Root);
                Assert.True(CopilotMcpConfirmationStore.Instance.Approve(item.ToolResult.Approval.ActionId, reviewContext, out var message), message);
                approvalCount++;
            }
            if (CopilotTurnCodeReviewSnapshotCapture.TryCaptureUpdate(request.GetEffectiveWorkspaceReviewTarget()!, captured, item, out var update))
                captured = update;
            if (otherWorkspace != null && item.ToolResult?.Success == true
                && item.ToolResult.ToolName == "InspectGitDiff"
                && CopilotGitDiffResultProtocol.TryParse(item.ToolResult.Content, out var inspected, out _)
                && inspected.RepositoryRoot == otherWorkspace.Root)
            {
                Assert.NotNull(captured);
                Assert.Equal(workspace.Root, captured.RepositoryRoot);
                Assert.False(captured.HasFindingsSubmission());
            }
        }, cancellation.Token);

        Assert.True(result.StopReason == CopilotAgentStopReason.Completed,
            $"Stop={result.StopReason}; tokens={result.Budget.ConsumedTokens}/{result.Budget.RequestTokenBudget}; "
            + $"providerCalls={result.Budget.ProviderCalls}; timeExhausted={result.Budget.TimeBudgetExhausted}; "
            + $"steps={string.Join(", ", result.StepRecords.Select(step => $"{step.Execution.ToolName}:{step.Execution.State}:{step.Observation.FailureCode}"))}");
        Assert.Equal(3, approvalCount);
        Assert.Equal(2, runner.FullTreeUnstagedPatchCalls); // First A and the real, identically parameterized refresh of A.
        Assert.Equal(3, result.StepRecords.Count(step => step.Execution.ToolName == "InspectGitDiff" && step.Observation.Success));
        Assert.Contains(result.StepRecords, step => step.Execution.ToolName == "InspectGitDiff"
            && step.Observation.FailureKind == CopilotToolFailureKind.Conflict);
        var submissions = result.StepRecords.Where(step => step.Execution.ToolName == "SubmitCodeReviewFindings").ToArray();
        Assert.Equal([true, pathFilteredEvidence, true], submissions.Select(step => step.Observation.Success));
        if (otherRepositoryEvidence)
        {
            var beforeRefresh = result.StepRecords.TakeWhile(step => step != submissions[1]).Append(submissions[1]).ToArray();
            var mismatched = CopilotAgentExecutionContract.Create(request, tools).Evaluate(beforeRefresh);
            Assert.False(mismatched.IsSatisfied);
            Assert.True(mismatched.ShouldReinvoke);
            Assert.Equal(["InspectGitDiff"], mismatched.MissingToolNames);
            Assert.Contains(System.Text.Json.JsonSerializer.Serialize(workspace.Root), mismatched.Feedback, StringComparison.Ordinal);
        }
        Assert.NotNull(captured);
        Assert.Equal(workspace.Root, captured.RepositoryRoot);
        Assert.Equal("both", captured.Scope);
        Assert.Empty(captured.PathFilter);
        Assert.True(captured.TryReadFindings(out var findings));
        Assert.Empty(findings);
        Assert.True(CopilotAgentExecutionContract.Create(request, tools).Evaluate(result.StepRecords).IsSatisfied);
    }

    [Fact]
    public async Task ReviewCanCorrectRepositoryStatusAfterSubmittingFindingsWithoutRepeatingTheDiff()
    {
        using var workspace = new Workspace();
        using var otherWorkspace = new Workspace(setCurrentSolution: false);
        var runner = new GitRunner(workspace.Root);
        ICopilotTool[] tools =
        [
            new CopilotInspectGitWorkingTreeTool(new CopilotGitWorkingTreeInspectionService(runner, () => workspace.GitExecutable)),
            new CopilotInspectGitDiffTool(new CopilotGitDiffInspectionService(runner, () => workspace.GitExecutable)),
            new CopilotSubmitCodeReviewFindingsTool(),
        ];
        using var provider = new StatusCorrectionProvider(workspace.Root, otherWorkspace.Root);
        var catalog = new CopilotCapabilityCatalog();
        catalog.PublishSource(CopilotCapabilitySourceKind.BuiltIn, "review-status-correction", "Review status correction", tools);
        var runtime = new CopilotMicrosoftAgentFrameworkRuntime(new CopilotToolRegistry(tools),
            new CopilotAgentContextBuilder(), new CopilotToolExecutor(hooks: []), _ => provider,
            new NoExternalTools(), catalog, new CopilotAgentSkillUsageStore(workspace.Root));
        var request = new CopilotAgentRequest
        {
            ConversationId = "review-status-correction", TaskId = "review-status-correction-task", WorkspacePath = workspace.Root,
            Mode = CopilotAgentMode.Review, Profile = new CopilotProfileConfig { ProviderType = CopilotProviderType.LocalCodex },
            UserText = "Review the current working tree without changing files.",
            TaskIntentText = "Review the current working tree without changing files.",
            ReviewEvidenceContext = new CopilotReviewEvidenceContext(), SearchRootPaths = [workspace.Root, otherWorkspace.Root],
            HarnessFeatures = CopilotAgentHarnessFeatures.None,
            CodexApprovalPolicy = CopilotCodexApprovalPolicy.CreateScalar(CopilotCodexApprovalPolicyMode.OnRequest),
            RunBudgetOverride = new CopilotAgentRunBudgetOverride
            {
                MaxToolCalls = 8, MaxAgentPasses = 2, RequestTokenBudget = 32_768, TotalDuration = TimeSpan.FromSeconds(20),
            },
        };
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var result = await runtime.RunAsync(request, item =>
        {
            if (item.ToolExecution?.State != CopilotToolExecutionState.AwaitingApproval)
                return;
            Assert.NotNull(item.ToolResult?.Approval);
            var reviewContext = new CopilotConfirmationReviewContext(request.ConversationId, request.TaskId, workspace.Root);
            Assert.True(CopilotMcpConfirmationStore.Instance.Approve(item.ToolResult.Approval.ActionId, reviewContext, out var message), message);
        }, cancellation.Token);

        Assert.True(result.StopReason == CopilotAgentStopReason.Completed,
            $"Stop={result.StopReason}; steps={string.Join(", ", result.StepRecords.Select(step => $"{step.Execution.ToolName}:{step.Execution.State}:{step.Observation.FailureCode}"))}");
        Assert.Equal(
            ["InspectGitWorkingTree", "InspectGitDiff", "SubmitCodeReviewFindings", "InspectGitWorkingTree"],
            result.StepRecords.Where(step => step.Observation.Success).Select(step => step.Execution.ToolName));
        Assert.Equal(1, runner.FullTreeUnstagedPatchCalls);
        Assert.True(CopilotAgentExecutionContract.Create(request, tools).Evaluate(result.StepRecords).IsSatisfied);
    }

    private sealed class GitRunner(string repositoryRoot) : ICopilotShellProcessRunner
    {
        public int FullTreeUnstagedPatchCalls { get; private set; }
        public Task<CopilotShellProcessResult> RunAsync(CopilotShellProcessCommand command, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string output;
            if (command.Arguments.Contains("status")) output = "# branch.oid abcdef\n# branch.head main\n";
            else if (command.WorkingDirectory != repositoryRoot || command.Arguments.Contains("ls-files")) output = string.Empty;
            else if (command.Arguments.Contains("--name-only")) output = "Changed.cs\0";
            else
            {
                if (command.WorkingDirectory == repositoryRoot && !command.Arguments.Contains("--cached")
                    && !command.Arguments.Contains("Changed.cs")) FullTreeUnstagedPatchCalls++;
                output = "diff --git a/Changed.cs b/Changed.cs\n--- a/Changed.cs\n+++ b/Changed.cs\n@@ -1 +1 @@\n-old\n+new\n";
            }
            return Task.FromResult(new CopilotShellProcessResult(0, false, output, string.Empty, TimeSpan.FromMilliseconds(1)));
        }
    }

    private sealed class ReviewProvider(bool pathFilteredEvidence, string? otherRepositoryRoot) : IChatClient
    {
        private int _turn;
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) => throw new InvalidOperationException("No approval reviewer or network request is needed.");

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            cancellationToken.ThrowIfCancellationRequested();
            var turn = ++_turn;
            if (turn <= 7)
            {
                var submit = turn is 3 or 5 or 7;
                var name = CopilotMicrosoftAgentFrameworkRuntime.HarnessToolBridge.ToFunctionName(submit ? "SubmitCodeReviewFindings" : "InspectGitDiff");
                Assert.Contains(options!.Tools!.OfType<AIFunction>(), function => function.Name == name);
                var arguments = submit
                    ? new Dictionary<string, object?> { ["findings"] = Array.Empty<object>() }
                    : new Dictionary<string, object?> { ["target"] = "working_tree", ["scope"] = turn == 4 && !pathFilteredEvidence && otherRepositoryRoot == null ? "staged" : "both" };
                if (turn == 4 && pathFilteredEvidence)
                    arguments["path"] = "Changed.cs";
                else if (turn == 4 && otherRepositoryRoot != null)
                    arguments["path"] = otherRepositoryRoot;
                yield return new ChatResponseUpdate(ChatRole.Assistant,
                    [new FunctionCallContent("review-refresh-" + turn, name, arguments), FixtureUsage()])
                {
                    FinishReason = ChatFinishReason.ToolCalls,
                };
            }
            else
            {
                Assert.Equal(8, turn);
                yield return new ChatResponseUpdate(ChatRole.Assistant,
                    [new TextContent("No actionable findings remain in the reviewed working tree."), FixtureUsage()])
                {
                    FinishReason = ChatFinishReason.Stop,
                };
            }
        }

        // This fixture exercises evidence refresh, independent of cumulative prompt-size estimates.
        private static UsageContent FixtureUsage() => new(new UsageDetails
        {
            InputTokenCount = 12, OutputTokenCount = 8, TotalTokenCount = 20,
        });

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    private sealed class StatusCorrectionProvider(string repositoryRoot, string otherRepositoryRoot) : IChatClient
    {
        private int _turn;
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) => throw new InvalidOperationException("No approval reviewer or network request is needed.");

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            cancellationToken.ThrowIfCancellationRequested();
            var turn = ++_turn;
            Assert.InRange(turn, 1, 6);
            if (turn is 1 or 2 or 3 or 5)
            {
                if (turn == 5)
                    Assert.Contains(messages, message => message.Text?.Contains(
                        "Execution contract: Review mode requires current working-tree evidence", StringComparison.Ordinal) == true);
                var toolName = turn switch
                {
                    2 => "InspectGitDiff",
                    3 => "SubmitCodeReviewFindings",
                    _ => "InspectGitWorkingTree",
                };
                var name = CopilotMicrosoftAgentFrameworkRuntime.HarnessToolBridge.ToFunctionName(toolName);
                Assert.Contains(options!.Tools!.OfType<AIFunction>(), function => function.Name == name);
                var arguments = turn switch
                {
                    2 => new Dictionary<string, object?> { ["path"] = repositoryRoot, ["target"] = "working_tree", ["scope"] = "both" },
                    3 => new Dictionary<string, object?> { ["findings"] = Array.Empty<object>() },
                    _ => new Dictionary<string, object?> { ["path"] = turn == 1 ? otherRepositoryRoot : repositoryRoot },
                };
                yield return new ChatResponseUpdate(ChatRole.Assistant,
                    [new FunctionCallContent("review-status-correction-" + turn, name, arguments), FixtureUsage()])
                {
                    FinishReason = ChatFinishReason.ToolCalls,
                };
            }
            else
            {
                yield return new ChatResponseUpdate(ChatRole.Assistant,
                    [new TextContent("No actionable findings remain in the reviewed working tree."), FixtureUsage()])
                {
                    FinishReason = ChatFinishReason.Stop,
                };
            }
        }

        private static UsageContent FixtureUsage() => new(new UsageDetails
        {
            InputTokenCount = 12, OutputTokenCount = 8, TotalTokenCount = 20,
        });

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    private sealed class NoExternalTools : ICopilotExternalToolProvider
    {
        public Task<CopilotExternalToolLease> DiscoverAsync(CopilotAgentRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new CopilotExternalToolLease());
    }

    private sealed class Workspace : IDisposable
    {
        private static readonly FieldInfo Instance = typeof(SolutionManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly object? _previous = Instance.GetValue(null);
        private readonly bool _setCurrentSolution;
        public string Root { get; } = Directory.CreateTempSubdirectory("CopilotReviewRefresh-").FullName;
        public string GitExecutable => Path.Combine(Root, "git.exe");

        public Workspace(bool setCurrentSolution = true)
        {
            _setCurrentSolution = setCurrentSolution;
            Directory.CreateDirectory(Path.Combine(Root, ".git"));
            File.WriteAllText(GitExecutable, string.Empty);
            File.WriteAllText(Path.Combine(Root, "Changed.cs"), "new");
            if (!setCurrentSolution)
                return;
            var manager = (SolutionManager)RuntimeHelpers.GetUninitializedObject(typeof(SolutionManager));
            var explorer = (SolutionExplorer)RuntimeHelpers.GetUninitializedObject(typeof(SolutionExplorer));
            typeof(SolutionExplorer).GetProperty(nameof(SolutionExplorer.DirectoryInfo))!.SetValue(explorer, new DirectoryInfo(Root));
            typeof(SolutionManager).GetField("_CurrentSolutionExplorer", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(manager, explorer);
            Instance.SetValue(null, manager);
        }

        public void Dispose()
        {
            if (_setCurrentSolution)
                Instance.SetValue(null, _previous);
            var resolved = Path.GetFullPath(Root);
            Assert.Equal(Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())), Path.GetDirectoryName(resolved), ignoreCase: true);
            Assert.StartsWith("CopilotReviewRefresh-", Path.GetFileName(resolved), StringComparison.Ordinal);
            Directory.Delete(resolved, recursive: true);
        }
    }
}
