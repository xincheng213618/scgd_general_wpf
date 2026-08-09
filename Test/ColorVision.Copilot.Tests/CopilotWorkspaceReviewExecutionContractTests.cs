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

        source.Revision = "changed-after-create";
        Assert.Equal("origin/develop", request.WorkspaceReviewTarget?.Revision);
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
        CopilotWorkspaceReviewTargetContext? target)
    {
        return CopilotAgentExecutionContract.Create(
            new CopilotAgentRequest
            {
                Mode = CopilotAgentMode.Review,
                UserText = "Review this change without modifying files.",
                WorkspaceReviewTarget = target,
            },
            ReviewTools);
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
        return new CopilotAgentStepRecord
        {
            Round = round,
            ToolCall = new CopilotToolCall
            {
                ToolName = toolName,
                ToolInput = toolInput,
            },
            Observation = new CopilotToolObservation { Success = success },
            Execution = new CopilotToolExecutionInfo
            {
                ToolName = toolName,
                Round = round,
                State = state,
                StartedAtUtc = DateTimeOffset.UtcNow.AddSeconds(round),
            },
        };
    }
}
