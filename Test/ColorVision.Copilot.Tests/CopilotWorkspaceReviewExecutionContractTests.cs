using ColorVision.Copilot;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotWorkspaceReviewExecutionContractTests
{
    private static readonly ICopilotTool[] ReviewTools =
    [
        new CopilotInspectGitWorkingTreeTool(),
        new CopilotInspectGitDiffTool(),
    ];

    private static readonly ICopilotTool[] StructuredReviewTools =
    [
        .. ReviewTools,
        new CopilotSubmitCodeReviewFindingsTool(),
    ];

    [Fact]
    public void BaseBranchRejectsSuccessfulDiffForWrongTargetAndRequestsCorrection()
    {
        var contract = CreateContract(new CopilotWorkspaceReviewTargetContext
        {
            Target = CopilotWorkspaceReviewTarget.BaseBranch,
            Revision = "origin/develop",
        });
        var steps = new[]
        {
            Success("InspectGitWorkingTree", 1),
            Success("InspectGitDiff", 2, ("target", "working_tree"), ("scope", "both")),
        };

        var mismatch = contract.Evaluate(steps);

        Assert.False(mismatch.IsSatisfied);
        Assert.True(mismatch.ShouldReinvoke);
        Assert.Contains("target=\"base_branch\"", mismatch.Feedback, StringComparison.Ordinal);
        Assert.Contains("revision=\"origin/develop\"", mismatch.Feedback, StringComparison.Ordinal);

        var corrected = contract.Evaluate(
        [
            .. steps,
            Success("InspectGitDiff", 3, ("target", "base_branch"), ("revision", "origin/develop")),
        ]);
        Assert.True(corrected.IsSatisfied);
    }

    [Fact]
    public void CommitRequiresTheRequestedRevision()
    {
        var contract = CreateContract(new CopilotWorkspaceReviewTargetContext
        {
            Target = CopilotWorkspaceReviewTarget.Commit,
            Revision = "abcdef1",
        });

        var mismatch = contract.Evaluate(
        [
            Success("InspectGitWorkingTree", 1),
            Success("InspectGitDiff", 2, ("target", "commit"), ("revision", "1234567")),
        ]);
        Assert.False(mismatch.IsSatisfied);
        Assert.True(mismatch.ShouldReinvoke);

        var matched = contract.Evaluate(
        [
            Success("InspectGitWorkingTree", 1),
            Success("InspectGitDiff", 2, ("target", "commit"), ("revision", "ABCDEF1")),
        ]);
        Assert.True(matched.IsSatisfied);
    }

    [Fact]
    public void FailedAttemptForCorrectTargetDoesNotLoopAfterEarlierMismatch()
    {
        var contract = CreateContract(new CopilotWorkspaceReviewTargetContext
        {
            Target = CopilotWorkspaceReviewTarget.BaseBranch,
            Revision = "origin/develop",
        });
        var failedCorrectAttempt = Step(
            "InspectGitDiff",
            3,
            false,
            CopilotToolExecutionState.Failed,
            ("target", "base_branch"),
            ("revision", "origin/develop"));

        var evaluation = contract.Evaluate(
        [
            Success("InspectGitWorkingTree", 1),
            Success("InspectGitDiff", 2, ("target", "working_tree"), ("scope", "both")),
            failedCorrectAttempt,
        ]);

        Assert.False(evaluation.IsSatisfied);
        Assert.False(evaluation.ShouldReinvoke);
    }

    [Fact]
    public void ReviewWithoutExplicitMetadataRequiresCompleteWorkingTreeDiff()
    {
        var contract = CreateContract(target: null);
        Assert.Contains(
            "target=\"working_tree\" and scope=\"both\"",
            contract.BuildInitialInstruction(),
            StringComparison.Ordinal);

        var incomplete = contract.Evaluate(
        [
            Success("InspectGitWorkingTree", 1),
            Success("InspectGitDiff", 2, ("target", "working_tree"), ("scope", "unstaged")),
        ]);
        Assert.False(incomplete.IsSatisfied);
        Assert.True(incomplete.ShouldReinvoke);

        var complete = contract.Evaluate(
        [
            Success("InspectGitWorkingTree", 1),
            Success("InspectGitDiff", 2, ("target", "working_tree"), ("scope", "both")),
        ]);
        Assert.True(complete.IsSatisfied);
    }

    [Fact]
    public void ReviewRejectsRawToolSuccessWithoutModelVisibleDiffEvidence()
    {
        var contract = CreateContract(target: null);
        var diff = Success(
            "InspectGitDiff",
            2,
            ("target", "working_tree"),
            ("scope", "both"));
        var rawOnly = new CopilotAgentStepRecord
        {
            Round = diff.Round,
            ToolCall = diff.ToolCall,
            Observation = diff.Observation,
            ModelToolResult = string.Empty,
            Execution = diff.Execution,
        };

        var evaluation = contract.Evaluate(
        [
            Success("InspectGitWorkingTree", 1),
            rawOnly,
        ]);

        Assert.False(evaluation.IsSatisfied);
        Assert.Contains("successful Git patch", evaluation.Feedback, StringComparison.Ordinal);
    }

    [Fact]
    public void ReviewRequiresAFindingsSubmissionBoundToTheCollectedDiff()
    {
        var contract = CreateContract(target: null, StructuredReviewTools);
        var status = Success("InspectGitWorkingTree", 1);
        var diff = Success(
            "InspectGitDiff",
            2,
            ("target", "working_tree"),
            ("scope", "both"));

        var missing = contract.Evaluate([status, diff]);

        Assert.False(missing.IsSatisfied);
        Assert.True(missing.ShouldReinvoke);
        Assert.Equal(["SubmitCodeReviewFindings"], missing.MissingToolNames);
        Assert.Contains("findings=[]", missing.Feedback, StringComparison.Ordinal);
        Assert.Contains("SubmitCodeReviewFindings", contract.BuildInitialInstruction(), StringComparison.Ordinal);
        Assert.Equal(
            "required_code_review_findings_missing",
            contract.CreateBlocker(missing)?.Code);

        var complete = contract.Evaluate([status, diff, SuccessFindings(diff, 3)]);

        Assert.True(complete.IsSatisfied);
    }

    [Fact]
    public void ReviewRequiresANewFindingsSubmissionAfterTheLatestDiff()
    {
        var contract = CreateContract(target: null, StructuredReviewTools);
        var status = Success("InspectGitWorkingTree", 1);
        var firstDiff = Success(
            "InspectGitDiff",
            2,
            ("target", "working_tree"),
            ("scope", "both"));
        var firstSubmission = SuccessFindings(firstDiff, 3);
        var latestDiff = Success(
            "InspectGitDiff",
            4,
            ("target", "working_tree"),
            ("scope", "both"),
            ("path", "Parser.cs"));

        var stale = contract.Evaluate([status, firstDiff, firstSubmission, latestDiff]);

        Assert.False(stale.IsSatisfied);
        Assert.True(stale.ShouldReinvoke);
        Assert.Equal(["SubmitCodeReviewFindings"], stale.MissingToolNames);

        var refreshed = contract.Evaluate(
            [status, firstDiff, firstSubmission, latestDiff, SuccessFindings(latestDiff, 5)]);

        Assert.True(refreshed.IsSatisfied);
    }

    [Fact]
    public void ReviewTargetPersistsWithUserMessageAndMalformedStateIsDiscarded()
    {
        var message = new CopilotChatMessage(CopilotChatRole.User, "Review the selected base branch.")
        {
            RequestMode = CopilotAgentMode.Review,
            WorkspaceReviewTarget = new CopilotWorkspaceReviewTargetContext
            {
                Target = CopilotWorkspaceReviewTarget.BaseBranch,
                Revision = "origin/develop",
            },
        };

        var restored = JsonConvert.DeserializeObject<CopilotChatMessage>(
            JsonConvert.SerializeObject(message));
        Assert.NotNull(restored);
        restored!.EnsureValid();
        Assert.Equal(CopilotWorkspaceReviewTarget.BaseBranch, restored.WorkspaceReviewTarget?.Target);
        Assert.Equal("origin/develop", restored.WorkspaceReviewTarget?.Revision);

        restored.WorkspaceReviewTarget = new CopilotWorkspaceReviewTargetContext
        {
            Target = CopilotWorkspaceReviewTarget.BaseBranch,
            Revision = "main..feature",
        };
        Assert.True(restored.EnsureValid());
        Assert.Null(restored.WorkspaceReviewTarget);
    }

    [Fact]
    public void RequestFactorySnapshotsStructuredReviewTarget()
    {
        var source = new CopilotWorkspaceReviewTargetContext
        {
            Target = CopilotWorkspaceReviewTarget.BaseBranch,
            Revision = "origin/develop",
        };
        var request = CopilotAgentRequestFactory.Create(
            new CopilotAgentRequestPlan
            {
                Mode = CopilotAgentMode.Review,
                UserText = "Review the selected base branch.",
            },
            new CopilotAgentRequestBuildInput
            {
                Profile = new CopilotProfileConfig(),
                AgentDefaults = new CopilotAgentDefaultsConfig(),
                WorkspaceReviewTarget = source,
            });

        Assert.NotSame(source, request.WorkspaceReviewTarget);
        Assert.Equal(CopilotWorkspaceReviewTarget.BaseBranch, request.WorkspaceReviewTarget?.Target);
        Assert.Equal("origin/develop", request.WorkspaceReviewTarget?.Revision);
        Assert.NotNull(request.ReviewEvidenceContext);

        source.Revision = "changed-after-create";
        Assert.Equal("origin/develop", request.WorkspaceReviewTarget?.Revision);

        var returned = request.WorkspaceReviewTarget!;
        returned.Revision = "changed-through-getter";
        Assert.Equal("origin/develop", request.WorkspaceReviewTarget?.Revision);
        Assert.NotSame(returned, request.WorkspaceReviewTarget);
    }

    [Fact]
    public void ComposerStashRetainsReviewTargetAndClearsItOutsideReviewMode()
    {
        var stash = CopilotComposerStash.Capture(
            "Review the selected commit.",
            3,
            CopilotAgentMode.Review,
            Array.Empty<CopilotAttachmentItem>(),
            new CopilotWorkspaceReviewTargetContext
            {
                Target = CopilotWorkspaceReviewTarget.Commit,
                Revision = "abcdef1",
            });

        Assert.Equal(CopilotWorkspaceReviewTarget.Commit, stash.WorkspaceReviewTarget?.Target);
        Assert.Equal("abcdef1", stash.WorkspaceReviewTarget?.Revision);

        stash.RequestMode = CopilotAgentMode.Auto;
        Assert.True(stash.EnsureValid());
        Assert.Null(stash.WorkspaceReviewTarget);
    }

    private static CopilotAgentExecutionContract CreateContract(
        CopilotWorkspaceReviewTargetContext? target,
        IReadOnlyList<ICopilotTool>? tools = null)
    {
        return CopilotAgentExecutionContract.Create(
            new CopilotAgentRequest
            {
                Mode = CopilotAgentMode.Review,
                UserText = "Review this change without modifying files.",
                WorkspaceReviewTarget = target,
            },
            tools ?? ReviewTools);
    }

    private static CopilotAgentStepRecord SuccessFindings(
        CopilotAgentStepRecord diff,
        int round)
    {
        Assert.True(CopilotGitDiffResultProtocol.TryParse(
            diff.Observation.Content,
            out var toolSnapshot,
            out var parseError), parseError);
        Assert.True(CopilotCodeReviewSnapshot.TryCreate(
            toolSnapshot,
            diff.ModelToolResult,
            out var reviewSnapshot));
        var result = new CopilotToolResult
        {
            ToolName = "SubmitCodeReviewFindings",
            Success = true,
            Summary = "Submitted a structured no-findings result.",
            Content = CopilotCodeReviewFindingsResultProtocol.Serialize(
                new CopilotCodeReviewFindingsSubmission(
                    reviewSnapshot.EvidenceId,
                    Array.Empty<CopilotCodeReviewFinding>())),
        };
        var execution = new CopilotToolExecutionInfo
        {
            ToolName = result.ToolName,
            Round = round,
            Attempt = 1,
            MaxAttempts = 1,
            State = CopilotToolExecutionState.Completed,
            StartedAtUtc = DateTimeOffset.UtcNow.AddSeconds(round),
        };
        return new CopilotAgentStepRecord
        {
            Round = round,
            ToolCall = new CopilotToolCall
            {
                ToolName = result.ToolName,
                ToolInput = new CopilotAgentToolInput(),
            },
            Observation = CopilotToolObservation.FromResult(result),
            ModelToolResult = CopilotFrameworkToolResultFormatter.Format(new CopilotToolExecutionOutcome
            {
                Result = result,
                Execution = execution,
            }),
            Execution = execution,
        };
    }

    private static CopilotAgentStepRecord Success(
        string toolName,
        int round,
        params (string Name, object? Value)[] arguments) =>
        Step(toolName, round, true, CopilotToolExecutionState.Completed, arguments);

    private static CopilotAgentStepRecord Step(
        string toolName,
        int round,
        bool success,
        CopilotToolExecutionState state,
        params (string Name, object? Value)[] arguments)
    {
        var toolInput = new CopilotAgentToolInput
        {
            Arguments = new Dictionary<string, object?>(
                arguments.ToDictionary(argument => argument.Name, argument => argument.Value),
                StringComparer.OrdinalIgnoreCase),
        };
        var execution = new CopilotToolExecutionInfo
        {
            ToolName = toolName,
            Round = round,
            Attempt = 1,
            MaxAttempts = 1,
            State = state,
            StartedAtUtc = DateTimeOffset.UtcNow.AddSeconds(round),
        };
        var observation = new CopilotToolObservation { Success = success };
        var modelToolResult = string.Empty;
        if (success
            && state == CopilotToolExecutionState.Completed
            && string.Equals(toolName, "InspectGitDiff", StringComparison.Ordinal))
        {
            var result = new CopilotToolResult
            {
                ToolName = toolName,
                Success = true,
                Summary = "Git diff inspected.",
                Content = CopilotGitDiffResultProtocol.Serialize(CreateDiffSnapshot(toolInput)),
            };
            observation = CopilotToolObservation.FromResult(result);
            modelToolResult = CopilotFrameworkToolResultFormatter.Format(new CopilotToolExecutionOutcome
            {
                Result = result,
                Execution = execution,
            });
        }
        return new CopilotAgentStepRecord
        {
            Round = round,
            ToolCall = new CopilotToolCall
            {
                ToolName = toolName,
                ToolInput = toolInput,
            },
            Observation = observation,
            ModelToolResult = modelToolResult,
            Execution = execution,
        };
    }

    private static CopilotGitDiffSnapshot CreateDiffSnapshot(CopilotAgentToolInput input)
    {
        var target = ReadArgument(input, "target");
        var revision = ReadArgument(input, "revision");
        var scope = ReadArgument(input, "scope");
        var path = ReadArgument(input, "path");
        if (string.Equals(target, "working_tree", StringComparison.Ordinal))
        {
            var normalizedScope = scope.Length == 0 ? "unstaged" : scope;
            var sections = normalizedScope switch
            {
                "both" => new[]
                {
                    new CopilotGitDiffSection("unstaged", false, true, false, string.Empty),
                    new CopilotGitDiffSection("staged", false, true, false, string.Empty),
                    new CopilotGitDiffSection("untracked", false, true, false, string.Empty),
                },
                "staged" => new[]
                {
                    new CopilotGitDiffSection("staged", false, true, false, string.Empty),
                },
                _ => new[]
                {
                    new CopilotGitDiffSection("unstaged", false, true, false, string.Empty),
                    new CopilotGitDiffSection("untracked", false, true, false, string.Empty),
                },
            };
            return new CopilotGitDiffSnapshot(
                @"C:\repo",
                normalizedScope,
                path,
                false,
                true,
                false,
                sections);
        }

        return new CopilotGitDiffSnapshot(
            @"C:\repo",
            "unstaged",
            path,
            false,
            true,
            false,
            [new CopilotGitDiffSection(target, false, true, false, string.Empty)])
        {
            Target = target,
            Revision = revision,
            ResolvedRevision = new string('d', 40),
        };
    }

    private static string ReadArgument(CopilotAgentToolInput input, string name) =>
        input.Arguments.TryGetValue(name, out var value)
            ? value?.ToString() ?? string.Empty
            : string.Empty;
}
