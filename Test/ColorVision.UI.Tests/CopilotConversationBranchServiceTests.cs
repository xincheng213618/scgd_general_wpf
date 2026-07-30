using ColorVision.Copilot;
using System.Collections.ObjectModel;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class CopilotConversationBranchServiceTests
{
    [Fact]
    public void CreateBranchCopiesTranscriptButStartsWithFreshRuntimeState()
    {
        var source = new CopilotConversationRecord
        {
            Id = "source-conversation",
            Title = "Inspect camera workflow",
            DraftText = "unsent follow-up",
            ProfileId = "profile-1",
            ProfileDisplayName = "Test Profile",
            AgentSessionCheckpoint = new CopilotAgentSessionCheckpoint(),
            Goal = CopilotConversationGoal.Create(
                    "Inspect the camera workflow until every stage is verified",
                    DateTimeOffset.UtcNow)
                .WithTurnOutcome(
                    CopilotConversationGoalState.Active,
                    new CopilotTokenUsage(90, 10, 100),
                    evaluated: true,
                    continued: true,
                    "One stage remains.",
                    DateTimeOffset.UtcNow.AddMinutes(1)),
        };
        source.SetLastUsage(new CopilotTokenUsage(100, 20, 120));
        source.Attachments.Add(CopilotAttachmentItem.CreateContext("composer context", "Composer"));

        var user = new CopilotChatMessage(CopilotChatRole.User, "Inspect the current workflow.")
        {
            RequestMode = CopilotAgentMode.Auto,
            RecoveryRequest = new CopilotAgentRecoveryRequest
            {
                Mode = CopilotAgentRecoveryMode.Replan,
                PreviousStopReason = CopilotAgentStopReason.Interrupted,
            },
        };
        user.Attachments.Add(CopilotAttachmentItem.CreateContext("captured context", "Captured"));
        var assistant = new CopilotChatMessage(CopilotChatRole.Assistant, "The workflow has three stages.")
        {
            RequestMode = CopilotAgentMode.Auto,
        };
        assistant.UpsertAgentTrace(CreateWorkspaceApplyTrace());
        source.Messages.Add(user);
        source.Messages.Add(assistant);
        source.Compaction = new CopilotConversationCompaction
        {
            StrategyVersion = CopilotConversationCompaction.CurrentStrategyVersion,
            Summary = "Earlier context summary.",
            ThroughMessageId = assistant.Id,
            SourceMessageCount = 2,
            SourceCharacters = 64,
        };
        source.RefreshSummary();

        var branch = CopilotConversationBranchService.CreateBranch(source, assistant, "Try another approach");

        Assert.NotEqual(source.Id, branch.Id);
        Assert.Equal("Try another approach", branch.Title);
        Assert.True(branch.HasCustomTitle);
        Assert.False(branch.IsPinned);
        Assert.Equal(source.ProfileId, branch.ProfileId);
        Assert.Equal(source.ProfileDisplayName, branch.ProfileDisplayName);
        Assert.Equal(2, branch.Messages.Count);
        Assert.Equal(source.Messages.Select(message => message.Content), branch.Messages.Select(message => message.Content));
        Assert.DoesNotContain(branch.Messages, message => source.Messages.Any(sourceMessage => sourceMessage.Id == message.Id));
        Assert.Null(branch.Messages[0].RecoveryRequest);
        Assert.NotEqual(user.Attachments[0].Id, branch.Messages[0].Attachments[0].Id);
        Assert.True(Assert.Single(assistant.AgentTraceEntries).CanRequestWorkspaceRollback);
        var branchedTrace = Assert.Single(branch.Messages[1].AgentTraceEntries);
        Assert.False(branchedTrace.CanRequestWorkspaceRollback);
        Assert.Empty(branchedTrace.WorkspaceChangeSetId);
        Assert.Null(branchedTrace.WorkspaceChangeSetExpiresAtUtc);
        Assert.Single(branchedTrace.WorkspaceChangedFiles);
        Assert.Empty(branch.Attachments);
        Assert.Empty(branch.DraftText);
        Assert.Null(branch.AgentSessionCheckpoint);
        Assert.False(branch.LastUsage.HasAny);
        Assert.True(branch.HasBranchOrigin);
        Assert.NotNull(branch.BranchOrigin);
        Assert.Equal(source.Id, branch.BranchOrigin.ParentConversationId);
        Assert.Equal(source.Id, branch.BranchOrigin.RootConversationId);
        Assert.Equal(assistant.Id, branch.BranchOrigin.ThroughMessageId);
        Assert.NotEqual(default, branch.BranchOrigin.ForkedAtUtc);
        Assert.Same(
            source,
            CopilotConversationBranchService.FindBranchOriginTarget([branch, source], branch));
        Assert.NotNull(branch.Compaction);
        Assert.Equal(branch.Messages[1].Id, branch.Compaction.ThroughMessageId);
        Assert.NotNull(branch.Goal);
        Assert.NotEqual(source.Goal.Id, branch.Goal.Id);
        Assert.Equal(source.Goal.Objective, branch.Goal.Objective);
        Assert.Equal(source.Goal.State, branch.Goal.State);
        Assert.Equal(0, branch.Goal.TurnCount);
        Assert.Equal(0, branch.Goal.EvaluationCount);
        Assert.Equal(0, branch.Goal.TokensUsed);

        Assert.Equal("source-conversation", source.Id);
        Assert.Equal("unsent follow-up", source.DraftText);
        Assert.Single(source.Attachments);
        Assert.NotNull(source.AgentSessionCheckpoint);
        Assert.True(source.LastUsage.HasAny);
        Assert.NotNull(user.RecoveryRequest);
    }

    [Fact]
    public void NestedBranchKeepsRootAndFallsBackToItWhenItsParentIsMissing()
    {
        var root = CreateBranchableConversation("root");
        var firstBranch = CopilotConversationBranchService.CreateBranch(
            root,
            root.Messages[1],
            "first branch");
        var nestedBranch = CopilotConversationBranchService.CreateBranch(
            firstBranch,
            firstBranch.Messages[1],
            "nested branch");

        Assert.NotNull(firstBranch.BranchOrigin);
        Assert.Equal(root.Id, firstBranch.BranchOrigin.RootConversationId);
        Assert.NotNull(nestedBranch.BranchOrigin);
        Assert.Equal(firstBranch.Id, nestedBranch.BranchOrigin.ParentConversationId);
        Assert.Equal(root.Id, nestedBranch.BranchOrigin.RootConversationId);
        Assert.Equal(firstBranch.Messages[1].Id, nestedBranch.BranchOrigin.ThroughMessageId);
        Assert.Same(
            firstBranch,
            CopilotConversationBranchService.FindBranchOriginTarget(
                [root, firstBranch, nestedBranch],
                nestedBranch));
        Assert.Same(
            root,
            CopilotConversationBranchService.FindBranchOriginTarget(
                [root, nestedBranch],
                nestedBranch));
    }

    [Fact]
    public void BranchLineageSurvivesChatStateRestart()
    {
        var stateRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var source = CreateBranchableConversation("source");
            var branch = CopilotConversationBranchService.CreateBranch(
                source,
                source.Messages[1],
                "persisted branch");
            var state = new CopilotChatState
            {
                ActiveConversationId = branch.Id,
                ActiveProfileId = "profile",
                Conversations = new ObservableCollection<CopilotConversationRecord> { source, branch },
            };

            new CopilotChatStateStore(stateRoot).Save(state);

            var restored = new CopilotChatStateStore(stateRoot).Load();
            var restoredBranch = Assert.Single(restored.Conversations, conversation => conversation.Title == "persisted branch");
            var restoredSource = Assert.Single(restored.Conversations, conversation => conversation.Title == "source");
            Assert.True(restoredBranch.HasBranchOrigin);
            Assert.NotNull(restoredBranch.BranchOrigin);
            Assert.Equal(restoredSource.Id, restoredBranch.BranchOrigin.ParentConversationId);
            Assert.Equal(restoredSource.Id, restoredBranch.BranchOrigin.RootConversationId);
            Assert.Equal(restoredSource.Messages[1].Id, restoredBranch.BranchOrigin.ThroughMessageId);
            Assert.NotEqual(default, restoredBranch.BranchOrigin.ForkedAtUtc);
            Assert.Same(
                restoredSource,
                CopilotConversationBranchService.FindBranchOriginTarget(
                    restored.Conversations,
                    restoredBranch));
        }
        finally
        {
            if (Directory.Exists(stateRoot))
                Directory.Delete(stateRoot, recursive: true);
        }
    }

    [Fact]
    public void ConversationValidationDropsSelfReferentialBranchLineage()
    {
        var conversation = new CopilotConversationRecord();
        conversation.BranchOrigin = new CopilotConversationBranchOrigin
        {
            ParentConversationId = conversation.Id,
            RootConversationId = conversation.Id,
            ThroughMessageId = Guid.NewGuid().ToString("N"),
            ForkedAtUtc = DateTimeOffset.UtcNow,
        };

        Assert.True(conversation.EnsureValid());
        Assert.Null(conversation.BranchOrigin);
        Assert.False(conversation.HasBranchOrigin);
    }

    [Fact]
    public void FindLatestBranchPointIgnoresAnInProgressAssistant()
    {
        var source = new CopilotConversationRecord();
        var complete = new CopilotChatMessage(CopilotChatRole.Assistant, "Completed answer.");
        var pending = new CopilotChatMessage(CopilotChatRole.Assistant, "Partial answer.")
        {
            IsResponsePending = true,
        };
        source.Messages.Add(complete);
        source.Messages.Add(pending);

        var branchPoint = CopilotConversationBranchService.FindLatestBranchPoint(source);

        Assert.Same(complete, branchPoint);
        Assert.Same(pending, CopilotConversationBranchService.FindCurrentBranchPoint(source));
        Assert.Throws<InvalidOperationException>(() =>
            CopilotConversationBranchService.CreateBranch(source, pending));
    }

    [Fact]
    public void CreateCurrentBranchTurnsInProgressTurnIntoDurableInterruptedSnapshot()
    {
        var source = new CopilotConversationRecord
        {
            Id = "running-source",
            Title = "Running source",
            ProfileId = "profile",
            ProfileDisplayName = "Profile",
            AgentSessionCheckpoint = new CopilotAgentSessionCheckpoint(),
        };
        var user = new CopilotChatMessage(CopilotChatRole.User, "Continue the active inspection.")
        {
            RequestMode = CopilotAgentMode.Auto,
        };
        var assistant = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty)
        {
            RequestMode = CopilotAgentMode.Auto,
            IsExecutionInProgress = true,
        };
        assistant.MarkThinkingStarted();
        assistant.BeginResponseTimeline();
        assistant.AppendResponseTimelineText("One file has been checked.");
        var runningTrace = new CopilotAgentTraceEntry
        {
            CallId = "running-read",
            Round = 1,
            ToolName = "ReadTextFile",
            State = CopilotToolExecutionState.Running,
            StartedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-2),
        };
        assistant.UpsertAgentTrace(runningTrace);
        assistant.RecordResponseTimelineTool(runningTrace.CallId);
        source.Messages.Add(user);
        source.Messages.Add(assistant);

        var branch = CopilotConversationBranchService.CreateCurrentBranch(source, "Running snapshot");

        Assert.True(assistant.IsThinkingInProgress);
        Assert.Equal(CopilotToolExecutionState.Running, Assert.Single(assistant.AgentTraceEntries).State);
        Assert.NotNull(source.AgentSessionCheckpoint);
        Assert.Equal(2, branch.Messages.Count);
        Assert.Null(branch.AgentSessionCheckpoint);
        Assert.NotNull(branch.BranchOrigin);
        Assert.Equal(source.Id, branch.BranchOrigin.ParentConversationId);
        Assert.Equal(assistant.Id, branch.BranchOrigin.ThroughMessageId);

        var snapshot = branch.Messages[1];
        Assert.False(snapshot.IsThinkingInProgress);
        Assert.True(snapshot.WasResponseInterrupted);
        Assert.True(snapshot.UsesResponseTimeline);
        Assert.Contains("源会话仍会继续运行", snapshot.ResponseInterruptionDetail);
        Assert.Contains("[会话分支快照", snapshot.Content);
        Assert.Contains("[会话分支快照", snapshot.ModelContent);
        var interruptedTrace = Assert.Single(snapshot.AgentTraceEntries);
        Assert.Equal(CopilotToolExecutionState.Interrupted, interruptedTrace.State);
        Assert.Equal("fork_snapshot_incomplete", interruptedTrace.FailureCode);
        Assert.Contains("not running in this branch", interruptedTrace.ErrorMessage);

        var stateRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            new CopilotChatStateStore(stateRoot).Save(new CopilotChatState
            {
                ActiveConversationId = branch.Id,
                ActiveProfileId = "profile",
                Conversations = new ObservableCollection<CopilotConversationRecord> { branch },
            });

            var restored = new CopilotChatStateStore(stateRoot).Load();
            var restoredBranch = Assert.Single(restored.Conversations);
            var restoredSnapshot = restoredBranch.Messages[1];
            Assert.False(restoredSnapshot.IsThinkingInProgress);
            Assert.True(restoredSnapshot.WasResponseInterrupted);
            Assert.True(restoredSnapshot.UsesResponseTimeline);
            Assert.Contains("源会话仍会继续运行", restoredSnapshot.ResponseInterruptionDetail);
            Assert.Contains("[会话分支快照", restoredSnapshot.ModelContent);
            Assert.Equal(
                CopilotToolExecutionState.Interrupted,
                Assert.Single(restoredSnapshot.AgentTraceEntries).State);
        }
        finally
        {
            if (Directory.Exists(stateRoot))
                Directory.Delete(stateRoot, recursive: true);
        }
    }

    [Fact]
    public void ForkAndBranchCommandsAcceptAnOptionalConversationName()
    {
        var fork = CopilotLocalCommandCatalog.Parse("/fork alternative approach");
        var branch = CopilotLocalCommandCatalog.Parse("/branch another option");

        Assert.NotNull(fork);
        Assert.Equal(CopilotLocalCommandKind.ForkConversation, fork.Command.Kind);
        Assert.Equal("alternative approach", fork.Arguments);
        Assert.True(fork.Command.AvailableWhileAgentRuns);
        Assert.NotNull(branch);
        Assert.Equal(CopilotLocalCommandKind.ForkConversation, branch.Command.Kind);
        Assert.Equal("another option", branch.Arguments);
        Assert.True(branch.Command.AvailableWhileAgentRuns);
    }

    private static CopilotConversationRecord CreateBranchableConversation(string title)
    {
        var conversation = new CopilotConversationRecord
        {
            Title = title,
            HasCustomTitle = true,
            ProfileId = "profile",
            ProfileDisplayName = "Profile",
        };
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "Inspect the current state."));
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "The current state is ready."));
        return conversation;
    }

    private static CopilotAgentTraceEntry CreateWorkspaceApplyTrace()
    {
        var now = DateTimeOffset.UtcNow;
        return CopilotAgentTraceEntry.FromResult(
            new CopilotToolExecutionInfo
            {
                CallId = "workspace-apply",
                Round = 1,
                ToolName = "ApplyWorkspacePatchEnvelope",
                State = CopilotToolExecutionState.Completed,
                StartedAtUtc = now.AddSeconds(-1),
                CompletedAtUtc = now,
            },
            new CopilotToolResult
            {
                ToolName = "ApplyWorkspacePatchEnvelope",
                Success = true,
                Summary = "Applied one workspace change set.",
                Content = string.Join(
                    Environment.NewLine,
                    "[Workspace Change Set Result]",
                    "change_set_id: workspace-change-set:11111111111111111111111111111111",
                    "file_count: 1",
                    "state: Applied",
                    $"expires_at_utc: {now.AddMinutes(20):O}",
                    "file_1_operation: Update",
                    @"file_1_path: C:\workspace\target.txt",
                    "file_1_before_sha256: before",
                    "file_1_after_sha256: after"),
            });
    }
}
