using ColorVision.Copilot;
using ColorVision.Copilot.Mcp;
using ColorVision.Solution;
using Newtonsoft.Json.Linq;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotChatViewModelApprovalCoordinatorTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public void StoreEventsUpdatePendingProjectionAndTraceUntilViewModelIsDisposed()
    {
        var confirmationStore = CopilotMcpConfirmationStore.Instance;
        confirmationStore.ClearForTests();
        var profile = new CopilotProfileConfig
        {
            Id = "approval-profile",
            Name = "Approval Profile",
            VendorType = CopilotVendorType.Custom,
            ProviderType = CopilotProviderType.OpenAICompatible,
            ApiKey = "approval-test-key",
            BaseUrl = "https://unit.test/v1",
            Model = "approval-test-model",
        };
        var config = new CopilotConfig
        {
            SchemaVersion = CopilotConfig.CurrentSchemaVersion,
            McpBearerToken = "approval-view-model-test-token",
            Profiles = new ObservableCollection<CopilotProfileConfig> { profile },
        };
        var conversation = CopilotConversationRecord.CreateEmpty(profile.Id, profile.DisplayLabel);
        conversation.Id = "approval-conversation";
        var message = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty)
        {
            IsExecutionInProgress = true,
        };
        var trace = new CopilotAgentTraceEntry
        {
            CallId = "approval-call",
            ToolName = "approval_test",
            StartedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1),
        };
        message.AgentTraceEntries.Add(trace);
        conversation.Messages.Add(message);
        var state = new CopilotChatState
        {
            ActiveConversationId = conversation.Id,
            ActiveProfileId = profile.Id,
            Conversations = new ObservableCollection<CopilotConversationRecord> { conversation },
        };

        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        var viewModel = new CopilotChatViewModel(
            new CopilotChatService(),
            new InMemoryStateStore(state),
            config,
            new IdleTurnRuntime(),
            new CopilotAgentTaskHost());
        ConfirmableAction? firstAction = null;
        ConfirmableAction? afterDisposeAction = null;
        try
        {
            Assert.IsType<ObservableCollection<ConfirmableAction>>(viewModel.PendingActions);
            Assert.Empty(viewModel.PendingActions);

            firstAction = confirmationStore.Create(
                "Protected UI action",
                "Exercise the ViewModel approval projection.",
                "confirmation-required",
                "approval_test",
                "{}",
                _ => Task.FromResult(CopilotMcpToolCallResult.Ok("executed")),
                requestContext: new CopilotConfirmationRequestContext
                {
                    SourceKind = CopilotApprovalSourceKind.ColorVisionUi,
                    ConversationId = conversation.Id,
                    WorkspacePath = string.Empty,
                },
                agentCallId: trace.CallId);

            Assert.Same(firstAction, Assert.Single(viewModel.PendingActions));
            Assert.True(viewModel.HasPendingActions);
            Assert.Equal(CopilotToolExecutionState.AwaitingApproval, trace.State);
            Assert.Equal(firstAction.ActionId, trace.ApprovalActionId);

            Assert.True(confirmationStore.Reject(
                firstAction.ActionId,
                new CopilotConfirmationReviewContext(conversation.Id, string.Empty, string.Empty),
                out _));

            Assert.Empty(viewModel.PendingActions);
            Assert.False(viewModel.HasPendingActions);
            Assert.Equal(CopilotToolExecutionState.Denied, trace.State);

            viewModel.Dispose();
            afterDisposeAction = confirmationStore.Create(
                "Action after disposal",
                "The disposed ViewModel must not observe this action.",
                "confirmation-required",
                "approval_after_dispose",
                "{}",
                _ => Task.FromResult(CopilotMcpToolCallResult.Ok("executed")),
                requestContext: new CopilotConfirmationRequestContext
                {
                    SourceKind = CopilotApprovalSourceKind.ExternalMcp,
                    RequestSource = "approval-test-client",
                    WorkspacePath = string.Empty,
                });

            Assert.Empty(viewModel.PendingActions);
        }
        finally
        {
            viewModel.Dispose();
            if (firstAction != null)
                confirmationStore.Cancel(firstAction.ActionId, out _);
            if (afterDisposeAction != null)
                confirmationStore.Cancel(afterDisposeAction.ActionId, out _);
            confirmationStore.ClearForTests();
        }
    }

    [Fact]
    public async Task PendingApprovalPromotesBackgroundRunStatusToNeedsInput()
    {
        var confirmationStore = CopilotMcpConfirmationStore.Instance;
        confirmationStore.ClearForTests();
        var profile = new CopilotProfileConfig
        {
            Id = "attention-profile",
            Name = "Attention Profile",
            VendorType = CopilotVendorType.Custom,
            ProviderType = CopilotProviderType.OpenAICompatible,
            ApiKey = "attention-test-key",
            BaseUrl = "https://unit.test/v1",
            Model = "attention-test-model",
        };
        var config = new CopilotConfig
        {
            SchemaVersion = CopilotConfig.CurrentSchemaVersion,
            McpBearerToken = "attention-view-model-test-token",
            Profiles = new ObservableCollection<CopilotProfileConfig> { profile },
        };
        var runningConversation = CopilotConversationRecord.CreateEmpty(profile.Id, profile.DisplayLabel);
        runningConversation.Id = "running-conversation";
        var selectedConversation = CopilotConversationRecord.CreateEmpty(profile.Id, profile.DisplayLabel);
        selectedConversation.Id = "selected-conversation";
        var state = new CopilotChatState
        {
            ActiveConversationId = selectedConversation.Id,
            ActiveProfileId = profile.Id,
            Conversations = new ObservableCollection<CopilotConversationRecord>
            {
                runningConversation,
                selectedConversation,
            },
        };
        var taskHost = new CopilotAgentTaskHost();
        var releaseRun = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        var viewModel = new CopilotChatViewModel(
            new CopilotChatService(),
            new InMemoryStateStore(state),
            config,
            new IdleTurnRuntime(),
            taskHost);
        ConfirmableAction? action = null;
        CopilotHostedAgentRun? run = null;
        try
        {
            run = taskHost.Start(
                runningConversation.Id,
                CopilotAgentMode.Auto,
                _ => releaseRun.Task);

            Assert.Equal("运行中", runningConversation.AgentRunStatusLabel);
            Assert.Empty(selectedConversation.AgentRunStatusLabel);

            action = confirmationStore.Create(
                "Protected background action",
                "Require attention while another conversation is selected.",
                "confirmation-required",
                "attention_test",
                "{}",
                _ => Task.FromResult(CopilotMcpToolCallResult.Ok("executed")),
                requestContext: new CopilotConfirmationRequestContext
                {
                    SourceKind = CopilotApprovalSourceKind.InAppAgent,
                    ConversationId = runningConversation.Id,
                    TaskId = run.Id,
                    WorkspacePath = string.Empty,
                });

            Assert.Empty(viewModel.PendingActions);
            Assert.Equal("需要输入", runningConversation.AgentRunStatusLabel);

            Assert.True(confirmationStore.Reject(
                action.ActionId,
                new CopilotConfirmationReviewContext(
                    runningConversation.Id,
                    run.Id,
                    string.Empty),
                out _));

            Assert.Equal("运行中", runningConversation.AgentRunStatusLabel);
        }
        finally
        {
            if (action != null)
                confirmationStore.Cancel(action.ActionId, out _);
            releaseRun.TrySetResult();
            if (run != null)
                await run.Completion.WaitAsync(TestTimeout);
            viewModel.Dispose();
            confirmationStore.ClearForTests();
        }
    }

    [Fact]
    public async Task BackgroundCompletionsRemainVisibleUntilEachConversationIsOpened()
    {
        var profile = new CopilotProfileConfig
        {
            Id = "activity-profile",
            Name = "Activity Profile",
            VendorType = CopilotVendorType.Custom,
            ProviderType = CopilotProviderType.OpenAICompatible,
            ApiKey = "activity-test-key",
            BaseUrl = "https://unit.test/v1",
            Model = "activity-test-model",
        };
        var config = new CopilotConfig
        {
            SchemaVersion = CopilotConfig.CurrentSchemaVersion,
            McpBearerToken = "activity-view-model-test-token",
            Profiles = new ObservableCollection<CopilotProfileConfig> { profile },
        };
        var readyConversation = CreateConversationWithAssistant(
            profile,
            "ready-conversation",
            CopilotAgentStopReason.Completed);
        var blockedConversation = CreateConversationWithAssistant(
            profile,
            "blocked-conversation",
            CopilotAgentStopReason.Blocked);
        var selectedConversation = CopilotConversationRecord.CreateEmpty(profile.Id, profile.DisplayLabel);
        selectedConversation.Id = "selected-conversation";
        var state = new CopilotChatState
        {
            ActiveConversationId = selectedConversation.Id,
            ActiveProfileId = profile.Id,
            Conversations = new ObservableCollection<CopilotConversationRecord>
            {
                readyConversation,
                blockedConversation,
                selectedConversation,
            },
        };
        var taskHost = new CopilotAgentTaskHost();

        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        using var viewModel = new CopilotChatViewModel(
            new CopilotChatService(),
            new InMemoryStateStore(state),
            config,
            new IdleTurnRuntime(),
            taskHost);

        await CompleteHostedRunAsync(
            taskHost,
            readyConversation.Id,
            CopilotAgentStopReason.Completed);
        await CompleteHostedRunAsync(
            taskHost,
            blockedConversation.Id,
            CopilotAgentStopReason.Blocked);

        Assert.Equal(CopilotConversationActivityState.Ready, readyConversation.AgentActivity?.State);
        Assert.Equal("待查看", readyConversation.AgentRunStatusLabel);
        Assert.Equal(CopilotConversationActivityState.Blocked, blockedConversation.AgentActivity?.State);
        Assert.Equal("任务受阻", blockedConversation.AgentRunStatusLabel);
        Assert.Equal(2, viewModel.ActivityConversationCount);
        Assert.True(viewModel.HasUnreadConversationActivity);

        viewModel.ToggleActivityViewCommand.Execute(null);

        Assert.True(viewModel.IsActivityViewOpen);
        Assert.Equal(
            [blockedConversation.Id, readyConversation.Id],
            viewModel.FilteredConversations.Select(conversation => conversation.Id));

        viewModel.SelectConversationCommand.Execute(readyConversation);

        Assert.Null(readyConversation.AgentActivity);
        Assert.Empty(readyConversation.AgentRunStatusLabel);
        Assert.Equal(CopilotConversationActivityState.Blocked, blockedConversation.AgentActivity?.State);
        Assert.Equal([blockedConversation.Id], viewModel.FilteredConversations.Select(conversation => conversation.Id));

        viewModel.SelectConversationCommand.Execute(blockedConversation);

        Assert.Null(blockedConversation.AgentActivity);
        Assert.Empty(blockedConversation.AgentRunStatusLabel);
        Assert.Empty(viewModel.FilteredConversations);
        Assert.True(viewModel.HasNoActivityConversations);
        Assert.False(viewModel.HasUnreadConversationActivity);
    }

    [Fact]
    public async Task MarkAllActivityReadPreservesNeedsInputAndRunningConversations()
    {
        var profile = new CopilotProfileConfig
        {
            Id = "activity-filter-profile",
            Name = "Activity Filter Profile",
            VendorType = CopilotVendorType.Custom,
            ProviderType = CopilotProviderType.OpenAICompatible,
            ApiKey = "activity-filter-test-key",
            BaseUrl = "https://unit.test/v1",
            Model = "activity-filter-test-model",
        };
        var config = new CopilotConfig
        {
            SchemaVersion = CopilotConfig.CurrentSchemaVersion,
            McpBearerToken = "activity-filter-view-model-test-token",
            Profiles = new ObservableCollection<CopilotProfileConfig> { profile },
        };
        var runningConversation = CreateConversationWithAssistant(
            profile,
            "running-activity-conversation",
            CopilotAgentStopReason.None);
        var readyConversation = CreateConversationWithAssistant(
            profile,
            "ready-activity-conversation",
            CopilotAgentStopReason.Completed);
        var blockedConversation = CreateConversationWithAssistant(
            profile,
            "blocked-activity-conversation",
            CopilotAgentStopReason.Blocked);
        var needsInputConversation = CreateConversationWithAssistant(
            profile,
            "needs-input-activity-conversation",
            CopilotAgentStopReason.Paused);
        var selectedConversation = CopilotConversationRecord.CreateEmpty(profile.Id, profile.DisplayLabel);
        selectedConversation.Id = "selected-activity-conversation";
        var now = new DateTimeOffset(2026, 8, 10, 10, 0, 0, TimeSpan.Zero);
        readyConversation.ReplaceAgentActivity(CopilotConversationActivity.Create(
            CopilotConversationActivityState.Ready,
            readyConversation.Messages[^1].Id,
            now));
        blockedConversation.ReplaceAgentActivity(CopilotConversationActivity.Create(
            CopilotConversationActivityState.Blocked,
            blockedConversation.Messages[^1].Id,
            now.AddMinutes(1)));
        needsInputConversation.ReplaceAgentActivity(CopilotConversationActivity.Create(
            CopilotConversationActivityState.NeedsInput,
            needsInputConversation.Messages[^1].Id,
            now.AddMinutes(2)));
        var state = new CopilotChatState
        {
            ActiveConversationId = selectedConversation.Id,
            ActiveProfileId = profile.Id,
            Conversations = new ObservableCollection<CopilotConversationRecord>
            {
                runningConversation,
                readyConversation,
                blockedConversation,
                needsInputConversation,
                selectedConversation,
            },
        };
        var taskHost = new CopilotAgentTaskHost();
        var releaseRun = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var completionPublished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CopilotHostedAgentRun? run = null;
        EventHandler<CopilotAgentTaskHostChangedEventArgs> handler = (_, args) =>
        {
            if (args.Kind == CopilotAgentTaskHostChangeKind.Completed
                && ReferenceEquals(args.Run, run))
            {
                completionPublished.TrySetResult();
            }
        };
        taskHost.Changed += handler;

        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        var viewModel = new CopilotChatViewModel(
            new CopilotChatService(),
            new InMemoryStateStore(state),
            config,
            new IdleTurnRuntime(),
            taskHost);
        try
        {
            run = taskHost.Start(
                runningConversation.Id,
                CopilotAgentMode.Auto,
                _ => releaseRun.Task);
            viewModel.ToggleActivityViewCommand.Execute(null);

            Assert.Equal(
                [
                    needsInputConversation.Id,
                    blockedConversation.Id,
                    readyConversation.Id,
                    runningConversation.Id,
                ],
                viewModel.FilteredConversations.Select(conversation => conversation.Id));
            Assert.Equal(4, viewModel.ActivityConversationCount);
            Assert.True(viewModel.HasUnreadConversationActivity);
            Assert.True(viewModel.MarkAllActivityReadCommand.CanExecute(null));

            viewModel.MarkAllActivityReadCommand.Execute(null);

            Assert.Null(readyConversation.AgentActivity);
            Assert.Null(blockedConversation.AgentActivity);
            Assert.Equal(CopilotConversationActivityState.NeedsInput, needsInputConversation.AgentActivity?.State);
            Assert.Equal(
                [needsInputConversation.Id, runningConversation.Id],
                viewModel.FilteredConversations.Select(conversation => conversation.Id));
            Assert.Equal(2, viewModel.ActivityConversationCount);
            Assert.False(viewModel.HasUnreadConversationActivity);
            Assert.False(viewModel.MarkAllActivityReadCommand.CanExecute(null));
        }
        finally
        {
            if (run != null)
                run.SetAgentStopReason(CopilotAgentStopReason.Cancelled);
            releaseRun.TrySetResult();
            if (run != null)
                await completionPublished.Task.WaitAsync(TestTimeout);
            taskHost.Changed -= handler;
            viewModel.Dispose();
        }
    }

    [Fact]
    public void GoalProgressCommandsReuseThePersistedGoalLifecycle()
    {
        var profile = new CopilotProfileConfig
        {
            Id = "goal-controls-profile",
            Name = "Goal Controls Profile",
            VendorType = CopilotVendorType.Custom,
            ProviderType = CopilotProviderType.OpenAICompatible,
            ApiKey = "goal-controls-test-key",
            BaseUrl = "https://unit.test/v1",
            Model = "goal-controls-test-model",
        };
        var config = new CopilotConfig
        {
            SchemaVersion = CopilotConfig.CurrentSchemaVersion,
            McpBearerToken = "goal-controls-view-model-test-token",
            Profiles = new ObservableCollection<CopilotProfileConfig> { profile },
        };
        var conversation = CopilotConversationRecord.CreateEmpty(profile.Id, profile.DisplayLabel);
        var state = new CopilotChatState
        {
            ActiveConversationId = conversation.Id,
            ActiveProfileId = profile.Id,
            Conversations = new ObservableCollection<CopilotConversationRecord> { conversation },
        };

        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        using var viewModel = new CopilotChatViewModel(
            new CopilotChatService(),
            new InMemoryStateStore(state),
            config,
            new IdleTurnRuntime(),
            new CopilotAgentTaskHost());
        conversation.Goal = CopilotConversationGoal.Create(
            "完成原生进度条控制闭环",
            new DateTimeOffset(2026, 8, 10, 10, 0, 0, TimeSpan.Zero));

        Assert.True(viewModel.ShowConversationGoalHistoryCommand.CanExecute(null));
        Assert.True(viewModel.PauseConversationGoalCommand.CanExecute(null));
        Assert.False(viewModel.ResumeConversationGoalCommand.CanExecute(null));
        Assert.True(viewModel.EditConversationGoalCommand.CanExecute(null));
        Assert.True(viewModel.ClearConversationGoalCommand.CanExecute(null));

        viewModel.ShowConversationGoalHistoryCommand.Execute(null);

        Assert.True(viewModel.HasLocalCommandResult);
        Assert.Contains("迭代记录", viewModel.LocalCommandResultText, StringComparison.Ordinal);

        viewModel.PauseConversationGoalCommand.Execute(null);

        Assert.Equal(CopilotConversationGoalState.Paused, conversation.Goal?.State);
        Assert.False(viewModel.PauseConversationGoalCommand.CanExecute(null));
        Assert.True(viewModel.ResumeConversationGoalCommand.CanExecute(null));

        viewModel.ClearConversationGoalCommand.Execute(null);

        Assert.Null(conversation.Goal);
        Assert.False(viewModel.ShowConversationGoalHistoryCommand.CanExecute(null));
        Assert.False(viewModel.ClearConversationGoalCommand.CanExecute(null));
    }

    [Fact]
    public void GoalLifecycleResetsOnlyBoundWorkThatNoLongerMatches()
    {
        var now = new DateTimeOffset(2026, 8, 11, 10, 0, 0, TimeSpan.Zero);
        var active = CopilotConversationGoal.Create("持续完善 Copilot", now);
        var paused = CopilotConversationGoalCommand.Execute(active, "pause", now.AddMinutes(1));
        var cleared = CopilotConversationGoalCommand.Execute(active, "clear", now.AddMinutes(1));
        var edited = CopilotConversationGoalCommand.Execute(active, "edit 持续完善 Copilot 队列", now.AddMinutes(1));
        var resumed = CopilotConversationGoalCommand.Execute(paused.Goal, "resume", now.AddMinutes(2));
        var budgeted = CopilotConversationGoalCommand.Execute(active, "budget 100000", now.AddMinutes(1));
        var used = active.WithTurnOutcome(
            CopilotConversationGoalState.Active,
            new CopilotTokenUsage(600, 400, 1000),
            elapsedSeconds: 1,
            evaluated: false,
            continued: false,
            reason: string.Empty,
            now: now.AddMinutes(1));
        var budgetLimited = CopilotConversationGoalCommand.Execute(used, "budget 500", now.AddMinutes(2));

        Assert.True(CopilotChatViewModel.ShouldCancelGoalWork(active, paused));
        Assert.True(CopilotChatViewModel.ShouldCancelGoalWork(active, cleared));
        Assert.True(CopilotChatViewModel.ShouldCancelGoalWork(active, edited));
        Assert.True(CopilotChatViewModel.ShouldCancelGoalWork(paused.Goal, resumed));
        Assert.False(CopilotChatViewModel.ShouldCancelGoalWork(active, budgeted));
        Assert.True(CopilotChatViewModel.ShouldCancelGoalWork(used, budgetLimited));
    }

    private static CopilotConversationRecord CreateConversationWithAssistant(
        CopilotProfileConfig profile,
        string conversationId,
        CopilotAgentStopReason stopReason)
    {
        var conversation = CopilotConversationRecord.CreateEmpty(profile.Id, profile.DisplayLabel);
        conversation.Id = conversationId;
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "Run the task")
        {
            RequestMode = CopilotAgentMode.Auto,
        });
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "Task result")
        {
            AgentStopReason = stopReason,
            RequestMode = CopilotAgentMode.Auto,
        });
        return conversation;
    }

    private static async Task CompleteHostedRunAsync(
        CopilotAgentTaskHost taskHost,
        string conversationId,
        CopilotAgentStopReason stopReason)
    {
        var releaseRun = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var completionPublished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CopilotHostedAgentRun? run = null;
        EventHandler<CopilotAgentTaskHostChangedEventArgs> handler = (_, args) =>
        {
            if (args.Kind == CopilotAgentTaskHostChangeKind.Completed
                && ReferenceEquals(args.Run, run))
            {
                completionPublished.TrySetResult();
            }
        };
        taskHost.Changed += handler;
        try
        {
            run = taskHost.Start(
                conversationId,
                CopilotAgentMode.Auto,
                _ => releaseRun.Task);
            run.SetAgentStopReason(stopReason);
            releaseRun.TrySetResult();
            await completionPublished.Task.WaitAsync(TestTimeout);
        }
        finally
        {
            taskHost.Changed -= handler;
            releaseRun.TrySetResult();
        }
    }

    private sealed class IdleTurnRuntime : ICopilotTurnRuntime
    {
        public async IAsyncEnumerable<CopilotTurnEvent> RunAsync(
            CopilotTurnRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            yield break;
        }

        public CopilotSteeringAdmissionResult EnqueueSteeringMessage(string taskId, string message) =>
            new(CopilotSteeringAdmissionReason.RuntimeUnavailable);

        public bool TryEnqueueBackgroundShellCommandCompletion(CopilotBackgroundShellCommandSnapshot snapshot) => false;

        public bool TryEnqueueBackgroundShellCommandOutput(CopilotBackgroundShellOutputMonitorEventArgs eventArgs) => false;

        public bool TryAnswerUserQuestion(string taskId, string requestId, string answer) => false;

        public Task<CopilotWorkspaceRollbackActionResult> RequestWorkspaceRollbackAsync(
            CopilotWorkspaceRollbackActionRequest request,
            Action<CopilotAgentEvent> onEvent,
            CancellationToken cancellationToken) =>
            Task.FromException<CopilotWorkspaceRollbackActionResult>(new NotSupportedException());
    }

    private sealed class InMemoryStateStore(CopilotChatState state) : ICopilotChatStateStore
    {
        public string AttachmentDirectoryPath => string.Empty;

        public CopilotChatState Load() => state;

        public void Save(CopilotChatState value)
        {
        }

        public CopilotChatStateSnapshot CaptureSnapshot(CopilotChatState value) =>
            new(new JObject());

        public string Serialize(CopilotChatStateSnapshot snapshot) => "{}";

        public string Serialize(CopilotChatState value) => "{}";

        public Task SaveSerializedAsync(
            string serializedState,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public int CleanupOrphanedAttachments(CopilotChatState value) => 0;
    }

    private sealed class IsolatedSolutionManagerScope : IDisposable
    {
        private static readonly FieldInfo InstanceField = typeof(SolutionManager).GetField(
            "_instance",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("SolutionManager singleton field was not found.");

        private readonly object? _previousInstance = InstanceField.GetValue(null);
        private readonly SolutionManager _testInstance =
            (SolutionManager)RuntimeHelpers.GetUninitializedObject(typeof(SolutionManager));

        public IsolatedSolutionManagerScope()
        {
            InstanceField.SetValue(null, _testInstance);
        }

        public void Dispose()
        {
            if (ReferenceEquals(InstanceField.GetValue(null), _testInstance))
                InstanceField.SetValue(null, _previousInstance);
        }
    }
}
