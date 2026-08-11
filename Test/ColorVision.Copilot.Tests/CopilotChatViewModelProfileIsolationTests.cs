using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using ColorVision.Copilot;
using ColorVision.Solution;
using Newtonsoft.Json.Linq;

namespace ColorVision.Copilot.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class CopilotChatViewModelProfileIsolationFixture
{
    public const string Name = "CopilotChatViewModel profile isolation";
}

[Collection(CopilotChatViewModelProfileIsolationFixture.Name)]
public sealed class CopilotChatViewModelProfileIsolationTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task BackgroundTurnCompletionDoesNotApplyProfileFromNewlySelectedConversation()
    {
        var profileA = CreateProfile("profile-a", "Profile A", "model-a");
        var profileB = CreateProfile("profile-b", "Profile B", "model-b");
        var config = new CopilotConfig
        {
            SchemaVersion = CopilotConfig.CurrentSchemaVersion,
            McpBearerToken = "profile-isolation-test-token",
            Profiles = new ObservableCollection<CopilotProfileConfig>
            {
                profileA,
                profileB,
            },
        };
        var conversationA = CopilotConversationRecord.CreateEmpty(profileA.Id, profileA.DisplayLabel);
        conversationA.Id = "conversation-a";
        conversationA.DraftRequestMode = CopilotAgentMode.Explain;
        var conversationB = CopilotConversationRecord.CreateEmpty(profileB.Id, profileB.DisplayLabel);
        conversationB.Id = "conversation-b";
        var state = new CopilotChatState
        {
            ActiveConversationId = conversationA.Id,
            ActiveProfileId = profileA.Id,
            Conversations = new ObservableCollection<CopilotConversationRecord>
            {
                conversationA,
                conversationB,
            },
        };
        var runtime = new GatedFailingTurnRuntime();
        var taskHost = new CopilotAgentTaskHost();
        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        var viewModel = new CopilotChatViewModel(
            new CopilotChatService(),
            new InMemoryStateStore(state),
            config,
            runtime,
            taskHost);
        CopilotHostedAgentRun? activeRun = null;
        try
        {
            Assert.Same(conversationA, viewModel.SelectedConversation);
            Assert.Same(profileA, viewModel.SelectedProfile);

            viewModel.InputText = "Explain conversation A.";
            viewModel.SendCommand.Execute(null);

            var request = await runtime.Entered.WaitAsync(TestTimeout);
            activeRun = taskHost.ActiveRun;
            Assert.NotNull(activeRun);
            Assert.Equal(conversationA.Id, request.ConversationId);
            Assert.Equal(profileA.Id, request.Profile.Id);
            Assert.True(activeRun.IsAgent);
            Assert.True(viewModel.CanSwitchConversation);
            Assert.True(viewModel.SelectConversationCommand.CanExecute(conversationB));

            viewModel.SelectConversationCommand.Execute(conversationB);

            Assert.Same(conversationB, viewModel.SelectedConversation);
            Assert.Same(profileB, viewModel.SelectedProfile);

            runtime.Release();
            await activeRun.Completion.WaitAsync(TestTimeout);

            Assert.Same(conversationB, viewModel.SelectedConversation);
            Assert.Same(profileB, viewModel.SelectedProfile);
            Assert.Equal(profileA.Id, conversationA.ProfileId);
            Assert.Equal(profileA.DisplayLabel, conversationA.ProfileDisplayName);
            Assert.Equal(profileB.Id, conversationB.ProfileId);
            Assert.Equal(profileB.DisplayLabel, conversationB.ProfileDisplayName);
        }
        finally
        {
            runtime.Release();
            var pendingRun = activeRun ?? taskHost.ActiveRun;
            if (pendingRun != null && !pendingRun.Completion.IsCompleted)
            {
                try
                {
                    await pendingRun.Completion.WaitAsync(TestTimeout);
                }
                catch
                {
                }
            }
            viewModel.Dispose();
        }
    }

    [Fact]
    public async Task ConfigRebindKeepsActiveTurnSnapshotAndUsesCurrentConfigForTheNextTurn()
    {
        var profileC1 = CreateProfile("reload-profile", "Profile C1", "model-c1");
        profileC1.ApiKey = "api-key-c1";
        profileC1.BaseUrl = "https://c1.unit.test/v1";
        var configC1 = CreateConfig(profileC1, "mcp-token-c1");
        configC1.AgentDefaults.ContextWindowTokens = 96_000;
        configC1.AgentDefaults.RequestTokenBudget = 24_000;
        configC1.ExternalMcpServers.Add(new CopilotMcpClientServerConfig
        {
            Name = "external-c1",
            Endpoint = "https://mcp-c1.unit.test/mcp",
            BearerTokenEnvironmentVariable = "COPILOT_MCP_TOKEN_C1",
        });

        var profileC2 = CreateProfile("reload-profile", "Profile C2", "model-c2");
        profileC2.ApiKey = "api-key-c2";
        profileC2.BaseUrl = "https://c2.unit.test/v1";
        var configC2 = CreateConfig(profileC2, "mcp-token-c2");
        configC2.AgentDefaults.ContextWindowTokens = 64_000;
        configC2.AgentDefaults.RequestTokenBudget = 16_000;
        configC2.ExternalMcpServers.Add(new CopilotMcpClientServerConfig
        {
            Name = "external-c2",
            Endpoint = "https://mcp-c2.unit.test/mcp",
            BearerTokenEnvironmentVariable = "COPILOT_MCP_TOKEN_C2",
        });

        var conversation = CreateConversation(profileC1, "reload-conversation", string.Empty);
        var runtime = new SequencedGatedFailingTurnRuntime();
        var taskHost = new CopilotAgentTaskHost();
        using var codexHomeScope = new EnvironmentVariableScope(
            "CODEX_HOME",
            Path.Combine(Path.GetTempPath(), $"ColorVision.Copilot.Tests-{Guid.NewGuid():N}"));
        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        var viewModel = CreateViewModel(conversation, configC1, runtime, taskHost);
        CopilotHostedAgentRun? activeRun = null;
        try
        {
            var firstQueueResult = viewModel.QueueExternalPrompt(
                "Run with C1.",
                startNewConversation: false,
                sendNow: true,
                mode: CopilotAgentMode.Chat);
            Assert.True(firstQueueResult.Accepted);
            Assert.True(firstQueueResult.WasSent);

            var firstInvocation = await runtime.WaitForNextAsync().WaitAsync(TestTimeout);
            activeRun = taskHost.ActiveRun;
            Assert.NotNull(activeRun);
            AssertRequestUsesConfig(
                firstInvocation.Request,
                expectedModel: "model-c1",
                expectedApiKey: "api-key-c1",
                expectedBaseUrl: "https://c1.unit.test/v1",
                expectedContextWindowTokens: 96_000,
                expectedRequestTokenBudget: 24_000,
                expectedExternalMcpServerName: "external-c1",
                expectedExternalMcpEndpoint: "https://mcp-c1.unit.test/mcp",
                expectedBearerTokenEnvironmentVariable: "COPILOT_MCP_TOKEN_C1");

            var propertyChanges = new ConcurrentQueue<string?>();
            viewModel.PropertyChanged += (_, args) => propertyChanges.Enqueue(args.PropertyName);
            viewModel.BindCurrentConfig(configC2);

            Assert.Same(configC2.Profiles, viewModel.Profiles);
            Assert.Same(profileC2, viewModel.SelectedProfile);
            Assert.Equal(profileC2.DisplayLabel, conversation.ProfileDisplayName);
            Assert.Contains(nameof(CopilotChatViewModel.CanShowCompactHistory), propertyChanges);

            propertyChanges.Clear();
            profileC1.Model = "stale-model-c1";
            Assert.DoesNotContain(nameof(CopilotChatViewModel.SelectedProfileToolTip), propertyChanges);
            profileC2.Model = "updated-model-c2";
            Assert.Contains(nameof(CopilotChatViewModel.SelectedProfileToolTip), propertyChanges);

            AssertRequestUsesConfig(
                firstInvocation.Request,
                expectedModel: "model-c1",
                expectedApiKey: "api-key-c1",
                expectedBaseUrl: "https://c1.unit.test/v1",
                expectedContextWindowTokens: 96_000,
                expectedRequestTokenBudget: 24_000,
                expectedExternalMcpServerName: "external-c1",
                expectedExternalMcpEndpoint: "https://mcp-c1.unit.test/mcp",
                expectedBearerTokenEnvironmentVariable: "COPILOT_MCP_TOKEN_C1");

            firstInvocation.Release();
            await activeRun.Completion.WaitAsync(TestTimeout);
            activeRun = null;

            var secondQueueResult = viewModel.QueueExternalPrompt(
                "Run with C2.",
                startNewConversation: false,
                sendNow: true,
                mode: CopilotAgentMode.Chat);
            Assert.True(secondQueueResult.Accepted);
            Assert.True(secondQueueResult.WasSent);

            var secondInvocation = await runtime.WaitForNextAsync().WaitAsync(TestTimeout);
            activeRun = taskHost.ActiveRun;
            Assert.NotNull(activeRun);
            AssertRequestUsesConfig(
                secondInvocation.Request,
                expectedModel: "updated-model-c2",
                expectedApiKey: "api-key-c2",
                expectedBaseUrl: "https://c2.unit.test/v1",
                expectedContextWindowTokens: 64_000,
                expectedRequestTokenBudget: 16_000,
                expectedExternalMcpServerName: "external-c2",
                expectedExternalMcpEndpoint: "https://mcp-c2.unit.test/mcp",
                expectedBearerTokenEnvironmentVariable: "COPILOT_MCP_TOKEN_C2");

            secondInvocation.Release();
            await activeRun.Completion.WaitAsync(TestTimeout);
            activeRun = null;
        }
        finally
        {
            runtime.ReleaseAll();
            var pendingRun = activeRun ?? taskHost.ActiveRun;
            if (pendingRun != null && !pendingRun.Completion.IsCompleted)
            {
                try
                {
                    await pendingRun.Completion.WaitAsync(TestTimeout);
                }
                catch
                {
                }
            }
            viewModel.Dispose();
        }
    }

    [Fact]
    public async Task GoalContinuationQueuedAfterConfigRebindCapturesCurrentConfig()
    {
        var profileC1 = CreateProfile("goal-reload-profile-c1", "Goal Profile C1", "goal-model-c1");
        profileC1.ApiKey = "goal-api-key-c1";
        profileC1.BaseUrl = "https://goal-c1.unit.test/v1";
        var configC1 = CreateConfig(profileC1, "goal-reload-token-c1");
        configC1.AgentDefaults.ContextWindowTokens = 96_000;
        configC1.ExternalMcpServers.Add(new CopilotMcpClientServerConfig
        {
            Name = "goal-external-c1",
            Endpoint = "https://goal-mcp-c1.unit.test/mcp",
        });

        var profileC2 = CreateProfile("goal-reload-profile-c2", "Goal Profile C2", "goal-model-c2");
        profileC2.ApiKey = "goal-api-key-c2";
        profileC2.BaseUrl = "https://goal-c2.unit.test/v1";
        var configC2 = CreateConfig(profileC2, "goal-reload-token-c2");
        configC2.Profiles.Add(CreateProfile(
            profileC1.Id,
            "Reloaded old-id profile",
            "goal-model-c2-old-id"));
        configC2.AgentDefaults.ContextWindowTokens = 64_000;
        configC2.ExternalMcpServers.Add(new CopilotMcpClientServerConfig
        {
            Name = "goal-external-c2",
            Endpoint = "https://goal-mcp-c2.unit.test/mcp",
        });

        var conversation = CreateConversation(profileC1, "goal-reload-conversation", string.Empty);
        var continuedGoal = CopilotConversationGoal.Create(
                "Continue after configuration reload",
                DateTimeOffset.UtcNow)
            .WithTurnOutcome(
                CopilotConversationGoalState.Active,
                new CopilotTokenUsage(10, 5, 15),
                elapsedSeconds: 1,
                evaluated: true,
                continued: true,
                "More work remains.",
                DateTimeOffset.UtcNow);
        var assistantMessage = new CopilotChatMessage(CopilotChatRole.Assistant, "C1 turn complete")
        {
            AgentStopReason = CopilotAgentStopReason.Completed,
        };
        var taskHost = new CopilotAgentTaskHost();
        var activeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseActive = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        var viewModel = CreateViewModel(
            conversation,
            configC1,
            new GatedFailingTurnRuntime(),
            taskHost);
        conversation.Goal = continuedGoal;
        var hostedRun = taskHost.Start(
            conversation.Id,
            CopilotAgentMode.Auto,
            async _ =>
            {
                activeStarted.TrySetResult();
                await releaseActive.Task;
            });
        try
        {
            await activeStarted.Task.WaitAsync(TestTimeout);
            var completedTurnSnapshot = new CopilotAgentHostContextSnapshot("", "", []);

            viewModel.BindCurrentConfig(configC2);
            viewModel.SelectedProfile = profileC2;
            Assert.Equal(profileC2.Id, conversation.ProfileId);
            Assert.True(TryQueueGoalContinuation(
                viewModel,
                hostedRun,
                conversation,
                assistantMessage,
                profileC1,
                completedTurnSnapshot,
                continuedGoal.Id,
                "More work remains."));

            CopilotQueuedFollowUp queuedContinuation = Assert.Single(viewModel.QueuedFollowUps);
            Assert.Equal("goal-model-c2", queuedContinuation.Profile.Model);
            Assert.Equal("goal-api-key-c2", queuedContinuation.Profile.ApiKey);
            Assert.Equal("https://goal-c2.unit.test/v1", queuedContinuation.Profile.BaseUrl);
            Assert.Equal(
                64_000,
                queuedContinuation.RuntimeConfigSnapshot.CreateAgentDefaultsSnapshot().ContextWindowTokens);
            CopilotMcpClientServerConfig externalMcp = Assert.Single(
                queuedContinuation.RuntimeConfigSnapshot.CreateExternalMcpServerSnapshots());
            Assert.Equal("goal-external-c2", externalMcp.Name);
            Assert.Equal("https://goal-mcp-c2.unit.test/mcp", externalMcp.Endpoint);
        }
        finally
        {
            foreach (CopilotHostedAgentRun queuedRun in taskHost.QueuedRuns.ToArray())
                taskHost.RequestCancel(queuedRun.Id);
            releaseActive.TrySetResult();
            if (!hostedRun.Completion.IsCompleted)
                await hostedRun.Completion.WaitAsync(TestTimeout);
            viewModel.Dispose();
        }
    }

    [Fact]
    public void DeliveredSteeringRecoveryRecordIsCommittedWithTheNextCheckpoint()
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = CreateConfig(profile, "steering-checkpoint-commit-test-token");
        var conversation = CreateConversation(profile, "conversation-a", string.Empty);
        var assistantMessage = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty);
        conversation.Messages.Add(assistantMessage);
        var steeringMessage = new CopilotSteeringMessageSnapshot(
            "steering:checkpoint-commit",
            "keep the current constraint");

        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        var viewModel = CreateViewModel(
            conversation,
            config,
            new GatedFailingTurnRuntime(),
            new CopilotAgentTaskHost());
        using var hostedRun = new CopilotHostedAgentRun(
            conversation.Id,
            CopilotAgentMode.Auto,
            "run:checkpoint-commit");
        try
        {
            Assert.True(CopilotSteeringRecovery.TrackPending(
                conversation,
                hostedRun.Id,
                steeringMessage,
                DateTimeOffset.UtcNow));
            ApplyAgentEvents(
                viewModel,
                hostedRun,
                conversation,
                assistantMessage,
                CopilotAgentEvent.SteeringDelivered([steeringMessage]));

            Assert.Single(conversation.PendingSteeringRecoveries);

            var taskLedger = new CopilotAgentTaskLedgerSnapshot
            {
                Mode = "execute",
                Items =
                [
                    new CopilotAgentTaskItem
                    {
                        Id = 1,
                        Title = "Persist steering",
                        Description = "Commit delivered steering with the checkpoint.",
                    },
                ],
            };
            var staleCheckpoint = new CopilotAgentSessionCheckpoint
            {
                ProfileKey = "profile-a|model-a",
                SerializedSessionJson = "{}",
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            };
            ApplyAgentEvents(
                viewModel,
                hostedRun,
                conversation,
                assistantMessage,
                CopilotAgentEvent.CheckpointUpdated(staleCheckpoint, taskLedger));

            Assert.Single(conversation.PendingSteeringRecoveries);

            var checkpoint = new CopilotAgentSessionCheckpoint
            {
                ProfileKey = "profile-a|model-a",
                SerializedSessionJson = "{}",
                ConversationMemory =
                [
                    new CopilotRequestMessage("user", steeringMessage.Text)
                    {
                        IsSteering = true,
                    },
                ],
                UpdatedAtUtc = staleCheckpoint.UpdatedAtUtc.AddSeconds(1),
            };
            ApplyAgentEvents(
                viewModel,
                hostedRun,
                conversation,
                assistantMessage,
                CopilotAgentEvent.CheckpointUpdated(checkpoint, taskLedger));

            Assert.Empty(conversation.PendingSteeringRecoveries);
            Assert.Same(checkpoint, conversation.AgentSessionCheckpoint);
        }
        finally
        {
            hostedRun.Complete(error: null);
            viewModel.Dispose();
        }
    }

    [Fact]
    public async Task PausedBoundGoalStillAccountsTheCompletedTurn()
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = CreateConfig(profile, "paused-goal-accounting-test-token");
        var conversation = CreateConversation(profile, "conversation-a", string.Empty);
        var createdAt = new DateTimeOffset(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);
        var activeGoal = CopilotConversationGoal.Create("Finish the verified runtime slice", createdAt);
        conversation.Goal = activeGoal.WithState(
            CopilotConversationGoalState.Paused,
            createdAt.AddMinutes(1),
            "用户暂停");
        var userMessage = new CopilotChatMessage(CopilotChatRole.User, activeGoal.Objective)
        {
            RequestMode = CopilotAgentMode.Auto,
        };
        var assistantMessage = new CopilotChatMessage(CopilotChatRole.Assistant, "Turn complete")
        {
            AgentStopReason = CopilotAgentStopReason.Completed,
        };
        conversation.Messages.Add(userMessage);
        conversation.Messages.Add(assistantMessage);
        var turnUsage = new CopilotTokenUsage(10, 5, 15);

        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        var viewModel = CreateViewModel(
            conversation,
            config,
            new GatedFailingTurnRuntime(),
            new CopilotAgentTaskHost());
        using var hostedRun = new CopilotHostedAgentRun(
            conversation.Id,
            CopilotAgentMode.Auto,
            "run:paused-goal-accounting");
        try
        {
            await ProcessGoalAfterTurnAsync(
                viewModel,
                hostedRun,
                conversation,
                profile,
                userMessage,
                assistantMessage,
                activeGoal.Id,
                turnUsage);

            Assert.NotNull(conversation.Goal);
            Assert.Equal(CopilotConversationGoalState.Paused, conversation.Goal.State);
            Assert.Equal(1, conversation.Goal.TurnCount);
            Assert.Equal(turnUsage.EffectiveTotalTokens, conversation.Goal.TokensUsed);
            var iteration = Assert.Single(conversation.Goal.IterationLog);
            Assert.False(iteration.Evaluated);
            Assert.False(iteration.ContinuationCounted);
        }
        finally
        {
            hostedRun.Complete(error: null);
            viewModel.Dispose();
        }
    }

    [Fact]
    public async Task PausingDuringGoalEvaluationAccountsEvaluationUsageWithoutApplyingItsVerdict()
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = CreateConfig(profile, "paused-goal-evaluation-test-token");
        var conversation = CreateConversation(profile, "conversation-a", string.Empty);
        var createdAt = new DateTimeOffset(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);
        var activeGoal = CopilotConversationGoal.Create("Finish the verified runtime slice", createdAt);
        conversation.Goal = activeGoal;
        var userMessage = new CopilotChatMessage(CopilotChatRole.User, activeGoal.Objective)
        {
            RequestMode = CopilotAgentMode.Auto,
        };
        var assistantMessage = new CopilotChatMessage(CopilotChatRole.Assistant, "Turn complete")
        {
            AgentStopReason = CopilotAgentStopReason.Completed,
        };
        conversation.Messages.Add(userMessage);
        conversation.Messages.Add(assistantMessage);
        var turnUsage = new CopilotTokenUsage(10, 5, 15);
        var evaluationUsage = new CopilotTokenUsage(3, 2, 5);
        var evaluator = new GatedGoalCompletionEvaluator(evaluationUsage);

        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        var viewModel = CreateViewModel(
            conversation,
            config,
            new GatedFailingTurnRuntime(),
            new CopilotAgentTaskHost());
        var evaluatorField = typeof(CopilotChatViewModel).GetField(
            "_goalCompletionEvaluator",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Goal completion evaluator field was not found.");
        evaluatorField.SetValue(viewModel, evaluator);
        conversation.Goal = activeGoal;
        using var hostedRun = new CopilotHostedAgentRun(
            conversation.Id,
            CopilotAgentMode.Auto,
            "run:paused-goal-evaluation");
        try
        {
            var processing = ProcessGoalAfterTurnAsync(
                viewModel,
                hostedRun,
                conversation,
                profile,
                userMessage,
                assistantMessage,
                activeGoal.Id,
                turnUsage);
            await evaluator.Entered.WaitAsync(TestTimeout);
            conversation.Goal = activeGoal.WithState(
                CopilotConversationGoalState.Paused,
                createdAt.AddMinutes(1),
                "用户暂停");
            evaluator.Release();
            await processing;

            Assert.NotNull(conversation.Goal);
            Assert.Equal(CopilotConversationGoalState.Paused, conversation.Goal.State);
            Assert.Equal(1, conversation.Goal.TurnCount);
            Assert.Equal(0, conversation.Goal.EvaluationCount);
            Assert.Equal(
                turnUsage.Add(evaluationUsage).EffectiveTotalTokens,
                conversation.Goal.TokensUsed);
            var iteration = Assert.Single(conversation.Goal.IterationLog);
            Assert.False(iteration.Evaluated);
            Assert.False(iteration.ContinuationCounted);
        }
        finally
        {
            evaluator.Release();
            hostedRun.Complete(error: null);
            viewModel.Dispose();
        }
    }

    [Fact]
    public async Task CancellingGoalEvaluationAfterCompletedTurnPreservesAccountingAndPausesGoal()
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = CreateConfig(profile, "cancelled-goal-evaluation-test-token");
        var conversation = CreateConversation(profile, "conversation-a", string.Empty);
        var createdAt = new DateTimeOffset(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);
        var activeGoal = CopilotConversationGoal.Create("Finish the verified runtime slice", createdAt);
        conversation.Goal = activeGoal;
        var userMessage = new CopilotChatMessage(CopilotChatRole.User, activeGoal.Objective)
        {
            RequestMode = CopilotAgentMode.Auto,
        };
        var assistantMessage = new CopilotChatMessage(CopilotChatRole.Assistant, "Turn complete")
        {
            AgentStopReason = CopilotAgentStopReason.Completed,
        };
        conversation.Messages.Add(userMessage);
        conversation.Messages.Add(assistantMessage);
        var turnUsage = new CopilotTokenUsage(10, 5, 15);
        var evaluator = new GatedGoalCompletionEvaluator(CopilotTokenUsage.Empty);

        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        var viewModel = CreateViewModel(
            conversation,
            config,
            new GatedFailingTurnRuntime(),
            new CopilotAgentTaskHost());
        var evaluatorField = typeof(CopilotChatViewModel).GetField(
            "_goalCompletionEvaluator",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Goal completion evaluator field was not found.");
        evaluatorField.SetValue(viewModel, evaluator);
        conversation.Goal = activeGoal;
        using var hostedRun = new CopilotHostedAgentRun(
            conversation.Id,
            CopilotAgentMode.Auto,
            "run:cancelled-goal-evaluation");
        try
        {
            var processing = ProcessGoalAfterTurnAsync(
                viewModel,
                hostedRun,
                conversation,
                profile,
                userMessage,
                assistantMessage,
                activeGoal.Id,
                turnUsage);
            await evaluator.Entered.WaitAsync(TestTimeout);

            Assert.True(hostedRun.TryRequestCancel());
            await processing;

            Assert.NotNull(conversation.Goal);
            Assert.Equal(CopilotConversationGoalState.Paused, conversation.Goal.State);
            Assert.Equal(1, conversation.Goal.TurnCount);
            Assert.Equal(0, conversation.Goal.EvaluationCount);
            Assert.Equal(turnUsage.EffectiveTotalTokens, conversation.Goal.TokensUsed);
            var iteration = Assert.Single(conversation.Goal.IterationLog);
            Assert.False(iteration.Evaluated);
            Assert.False(iteration.ContinuationCounted);
            Assert.Equal(CopilotAgentStopReason.Completed, assistantMessage.AgentStopReason);
            Assert.False(assistantMessage.WasResponseInterrupted);
        }
        finally
        {
            evaluator.Release();
            hostedRun.Complete(error: null);
            viewModel.Dispose();
        }
    }

    [Fact]
    public async Task CancellationObservedWithEvaluationResultRetainsItsUsageWithoutApplyingVerdict()
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = CreateConfig(profile, "late-cancelled-goal-evaluation-test-token");
        var conversation = CreateConversation(profile, "conversation-a", string.Empty);
        var createdAt = new DateTimeOffset(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);
        var activeGoal = CopilotConversationGoal.Create("Finish the verified runtime slice", createdAt);
        conversation.Goal = activeGoal;
        var userMessage = new CopilotChatMessage(CopilotChatRole.User, activeGoal.Objective)
        {
            RequestMode = CopilotAgentMode.Auto,
        };
        var assistantMessage = new CopilotChatMessage(CopilotChatRole.Assistant, "Turn complete")
        {
            AgentStopReason = CopilotAgentStopReason.Completed,
        };
        conversation.Messages.Add(userMessage);
        conversation.Messages.Add(assistantMessage);
        var turnUsage = new CopilotTokenUsage(10, 5, 15);
        var evaluationUsage = new CopilotTokenUsage(3, 2, 5);

        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        using var hostedRun = new CopilotHostedAgentRun(
            conversation.Id,
            CopilotAgentMode.Auto,
            "run:late-cancelled-goal-evaluation");
        var evaluator = new CancellingGoalCompletionEvaluator(
            () => Assert.True(hostedRun.TryRequestCancel()),
            evaluationUsage);
        var viewModel = CreateViewModel(
            conversation,
            config,
            new GatedFailingTurnRuntime(),
            new CopilotAgentTaskHost());
        var evaluatorField = typeof(CopilotChatViewModel).GetField(
            "_goalCompletionEvaluator",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Goal completion evaluator field was not found.");
        evaluatorField.SetValue(viewModel, evaluator);
        conversation.Goal = activeGoal;
        try
        {
            await ProcessGoalAfterTurnAsync(
                viewModel,
                hostedRun,
                conversation,
                profile,
                userMessage,
                assistantMessage,
                activeGoal.Id,
                turnUsage);

            Assert.NotNull(conversation.Goal);
            Assert.Equal(CopilotConversationGoalState.Paused, conversation.Goal.State);
            Assert.Equal(1, conversation.Goal.TurnCount);
            Assert.Equal(0, conversation.Goal.EvaluationCount);
            Assert.Equal(
                turnUsage.Add(evaluationUsage).EffectiveTotalTokens,
                conversation.Goal.TokensUsed);
            var iteration = Assert.Single(conversation.Goal.IterationLog);
            Assert.False(iteration.Evaluated);
            Assert.False(iteration.ContinuationCounted);
            Assert.Equal(CopilotAgentStopReason.Completed, assistantMessage.AgentStopReason);
            Assert.False(assistantMessage.WasResponseInterrupted);
        }
        finally
        {
            hostedRun.Complete(error: null);
            viewModel.Dispose();
        }
    }

    [Fact]
    public async Task CancellationBeforeGoalContinuationCommitPausesGoalAndRemovesAutomaticWork()
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = CreateConfig(profile, "cancelled-goal-continuation-test-token");
        var conversation = CreateConversation(profile, "conversation-a", string.Empty);
        var createdAt = new DateTimeOffset(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);
        var turnUsage = new CopilotTokenUsage(10, 5, 15);
        var continuedGoal = CopilotConversationGoal.Create(
                "Finish the verified runtime slice",
                createdAt)
            .WithTurnOutcome(
                CopilotConversationGoalState.Active,
                turnUsage,
                elapsedSeconds: 2,
                evaluated: true,
                continued: true,
                "More verified work remains.",
                createdAt.AddMinutes(1));
        conversation.Goal = continuedGoal;
        var assistantMessage = new CopilotChatMessage(CopilotChatRole.Assistant, "Turn complete")
        {
            AgentStopReason = CopilotAgentStopReason.Completed,
        };

        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        var taskHost = new CopilotAgentTaskHost();
        var viewModel = CreateViewModel(
            conversation,
            config,
            new GatedFailingTurnRuntime(),
            taskHost);
        conversation.Goal = continuedGoal;
        var activeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseActive = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var hostedRun = taskHost.Start(
            conversation.Id,
            CopilotAgentMode.Auto,
            async _ =>
            {
                activeStarted.TrySetResult();
                await releaseActive.Task;
            });
        try
        {
            await activeStarted.Task.WaitAsync(TestTimeout);
            var completedTurnSnapshot = new CopilotAgentHostContextSnapshot("", "", []);
            Assert.True(TryQueueGoalContinuation(
                viewModel,
                hostedRun,
                conversation,
                assistantMessage,
                profile,
                completedTurnSnapshot,
                continuedGoal.Id,
                "More verified work remains."));
            Assert.Single(taskHost.QueuedRuns);

            Assert.True(taskHost.RequestCancel(hostedRun.Id));

            var queued = TryQueueGoalContinuation(
                viewModel,
                hostedRun,
                conversation,
                assistantMessage,
                profile,
                completedTurnSnapshot,
                continuedGoal.Id,
                "More verified work remains.");

            Assert.False(queued);
            Assert.Empty(taskHost.QueuedRuns);
            Assert.NotNull(conversation.Goal);
            Assert.Equal(CopilotConversationGoalState.Paused, conversation.Goal.State);
            Assert.Equal(continuedGoal.TurnCount, conversation.Goal.TurnCount);
            Assert.Equal(continuedGoal.EvaluationCount, conversation.Goal.EvaluationCount);
            Assert.Equal(continuedGoal.TokensUsed, conversation.Goal.TokensUsed);
            Assert.Equal(continuedGoal.IterationLog.Count, conversation.Goal.IterationLog.Count);
            Assert.Contains(
                "continuation queueing cancelled",
                assistantMessage.ExecutionContent,
                StringComparison.Ordinal);
        }
        finally
        {
            foreach (var queuedRun in taskHost.QueuedRuns.ToArray())
                taskHost.RequestCancel(queuedRun.Id);
            releaseActive.TrySetResult();
            try
            {
                await hostedRun.Completion.WaitAsync(TestTimeout);
            }
            catch (OperationCanceledException)
            {
            }
            viewModel.Dispose();
        }
    }

    [Fact]
    public async Task EditingGoalDuringActiveTurnQueuesOnlyTheLatestGoalAsNextWork()
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = CreateConfig(profile, "running-goal-edit-test-token");
        var conversation = CreateConversation(profile, "conversation-a", string.Empty);
        var originalGoal = CopilotConversationGoal.Create(
            "Finish the original runtime objective",
            new DateTimeOffset(2026, 8, 11, 13, 0, 0, TimeSpan.Zero));
        conversation.Goal = originalGoal;
        var runtime = new GatedFailingTurnRuntime();
        var taskHost = new CopilotAgentTaskHost();
        var activeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseActive = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        var viewModel = CreateViewModel(conversation, config, runtime, taskHost);
        var activeRun = taskHost.Start(
            conversation.Id,
            CopilotAgentMode.Auto,
            async _ =>
            {
                activeStarted.TrySetResult();
                await releaseActive.Task;
            });
        try
        {
            await activeStarted.Task.WaitAsync(TestTimeout);

            Assert.True(viewModel.EditConversationGoalCommand.CanExecute(null));

            viewModel.InputText = "/goal edit Finish the revised runtime objective";
            viewModel.SendCommand.Execute(null);

            var firstEditedGoal = Assert.IsType<CopilotConversationGoal>(conversation.Goal);
            Assert.NotEqual(originalGoal.Id, firstEditedGoal.Id);
            Assert.Equal("Finish the revised runtime objective", firstEditedGoal.Objective);
            var firstQueued = Assert.Single(viewModel.QueuedFollowUps);
            Assert.Equal(firstEditedGoal.Id, firstQueued.GoalId);
            Assert.Equal(firstEditedGoal.Objective, firstQueued.Prompt);
            Assert.True(firstQueued.IsGoalBound);
            Assert.False(firstQueued.IsAutomaticGoalContinuation);

            viewModel.InputText = "/goal edit Finish the latest runtime objective";
            viewModel.SendCommand.Execute(null);

            var latestGoal = Assert.IsType<CopilotConversationGoal>(conversation.Goal);
            Assert.NotEqual(firstEditedGoal.Id, latestGoal.Id);
            Assert.Equal("Finish the latest runtime objective", latestGoal.Objective);
            var latestQueued = Assert.Single(viewModel.QueuedFollowUps);
            Assert.NotEqual(firstQueued.RunId, latestQueued.RunId);
            Assert.Equal(latestGoal.Id, latestQueued.GoalId);
            Assert.Equal(latestGoal.Objective, latestQueued.Prompt);
            Assert.True(latestQueued.IsGoalBound);
            Assert.False(latestQueued.IsAutomaticGoalContinuation);
            Assert.Single(taskHost.QueuedRuns);
        }
        finally
        {
            foreach (var queuedRun in taskHost.QueuedRuns.ToArray())
                taskHost.RequestCancel(queuedRun.Id);
            releaseActive.TrySetResult();
            await activeRun.Completion.WaitAsync(TestTimeout);
            viewModel.Dispose();
        }
    }

    [Fact]
    public async Task EditingGoalDuringActiveTurnPausesItWhenTheFollowUpQueueIsFull()
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = CreateConfig(profile, "running-goal-edit-full-queue-test-token");
        var conversation = CreateConversation(profile, "conversation-a", string.Empty);
        conversation.Goal = CopilotConversationGoal.Create(
            "Finish the original runtime objective",
            new DateTimeOffset(2026, 8, 11, 13, 30, 0, TimeSpan.Zero));
        var taskHost = new CopilotAgentTaskHost();
        var activeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseActive = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        var viewModel = CreateViewModel(
            conversation,
            config,
            new GatedFailingTurnRuntime(),
            taskHost);
        var activeRun = taskHost.Start(
            conversation.Id,
            CopilotAgentMode.Auto,
            async _ =>
            {
                activeStarted.TrySetResult();
                await releaseActive.Task;
            });
        try
        {
            await activeStarted.Task.WaitAsync(TestTimeout);
            for (var index = 0; index < taskHost.MaxQueuedRuns; index++)
            {
                Assert.True(taskHost.TryScheduleFollowUp(
                    conversation.Id,
                    CopilotAgentMode.Auto,
                    static _ => Task.CompletedTask,
                    out _,
                    out _));
            }

            viewModel.InputText = "/goal edit Finish the revised runtime objective";
            viewModel.SendCommand.Execute(null);

            var editedGoal = Assert.IsType<CopilotConversationGoal>(conversation.Goal);
            Assert.Equal("Finish the revised runtime objective", editedGoal.Objective);
            Assert.Equal(CopilotConversationGoalState.Paused, editedGoal.State);
            Assert.Empty(viewModel.QueuedFollowUps);
            Assert.Contains("队列已满", viewModel.LocalCommandResultText, StringComparison.Ordinal);
            Assert.Contains("目标已暂停", viewModel.LocalCommandResultText, StringComparison.Ordinal);
        }
        finally
        {
            foreach (var queuedRun in taskHost.QueuedRuns.ToArray())
                taskHost.RequestCancel(queuedRun.Id);
            releaseActive.TrySetResult();
            await activeRun.Completion.WaitAsync(TestTimeout);
            viewModel.Dispose();
        }
    }

    [Fact]
    public async Task SupersededExplicitGoalStartExitsWithoutRunningAgainstTheNewGoal()
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = CreateConfig(profile, "superseded-goal-start-test-token");
        var conversation = CreateConversation(profile, "conversation-a", string.Empty);
        conversation.Goal = CopilotConversationGoal.Create(
            "Finish the latest runtime objective",
            new DateTimeOffset(2026, 8, 11, 14, 0, 0, TimeSpan.Zero));
        var runtime = new GatedFailingTurnRuntime();
        var queuedFollowUp = new CopilotQueuedFollowUp(
            "superseded-goal-run",
            conversation.Id,
            conversation.Title,
            "Finish the superseded runtime objective",
            CopilotAgentMode.Auto,
            profile,
            new CopilotAgentHostContextSnapshot("", "", []),
            goalId: "superseded-goal-id",
            automaticGoalContinuation: false);

        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        using var viewModel = CreateViewModel(
            conversation,
            config,
            runtime,
            new CopilotAgentTaskHost());
        using var hostedRun = new CopilotHostedAgentRun(
            conversation.Id,
            CopilotAgentMode.Auto);
        var executeMethod = typeof(CopilotChatViewModel).GetMethod(
            "ExecuteQueuedFollowUpAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ExecuteQueuedFollowUpAsync was not found.");

        var execution = Assert.IsAssignableFrom<Task>(executeMethod.Invoke(
            viewModel,
            [hostedRun, queuedFollowUp]));
        await execution.WaitAsync(TestTimeout);

        Assert.False(runtime.Entered.IsCompleted);
        Assert.Equal("Finish the latest runtime objective", conversation.Goal.Objective);
        Assert.Empty(conversation.Messages);
    }

    [Fact]
    public void PromptHistorySearchUsesAnOverlayAndRestoresTheCompleteComposerDraft()
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = new CopilotConfig
        {
            SchemaVersion = CopilotConfig.CurrentSchemaVersion,
            McpBearerToken = "composer-search-overlay-test-token",
            Profiles = new ObservableCollection<CopilotProfileConfig> { profile },
        };
        var skillReference = new CopilotAgentSkillReference
        {
            Name = "sample-skill",
            SkillFilePath = Path.GetFullPath(
                Path.Combine("skills", "sample-skill", "SKILL.md")),
        };
        var conversation = CopilotConversationRecord.CreateEmpty(profile.Id, profile.DisplayLabel);
        conversation.Id = "conversation-a";
        conversation.DraftText = "$sample-skill current draft";
        conversation.DraftRequestMode = CopilotAgentMode.Review;
        conversation.DraftWorkspaceReviewTarget = CopilotWorkspaceReviewTargetContext.WorkingTree();
        conversation.DraftAgentSkillReference = skillReference;
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "older prompt"));
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
            new GatedFailingTurnRuntime(),
            new CopilotAgentTaskHost());
        try
        {
            Assert.True(viewModel.TryOpenPromptHistorySearch());

            viewModel.InputText = "older";

            Assert.Equal("older", viewModel.InputText);
            Assert.Equal("$sample-skill current draft", conversation.DraftText);
            Assert.Equal(CopilotAgentMode.Review, conversation.DraftRequestMode);
            Assert.Equal(
                CopilotWorkspaceReviewTarget.WorkingTree,
                conversation.DraftWorkspaceReviewTarget?.Target);
            Assert.Equal("sample-skill", conversation.DraftAgentSkillReference?.Name);

            viewModel.DismissPromptHistorySearch();

            Assert.Equal("$sample-skill current draft", viewModel.InputText);
            Assert.Equal(CopilotAgentMode.Review, conversation.DraftRequestMode);
            Assert.NotNull(conversation.DraftWorkspaceReviewTarget);
            Assert.Equal("sample-skill", conversation.DraftAgentSkillReference?.Name);
        }
        finally
        {
            viewModel.Dispose();
        }
    }

    [Fact]
    public async Task ScheduledTurnConsumesCapturedAttachmentsButPreservesAttachmentsAddedDuringScheduling()
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = CreateConfig(profile, "composer-attachment-race-test-token");
        var conversation = CreateConversation(profile, "conversation-a", "original draft");
        var capturedAttachment = CopilotAttachmentItem.CreateContext("captured context");
        var lateAttachment = CopilotAttachmentItem.CreateContext("late context");
        conversation.Attachments.Add(capturedAttachment);
        var runtime = new GatedFailingTurnRuntime();
        var taskHost = new CopilotAgentTaskHost();
        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        var viewModel = CreateViewModel(conversation, config, runtime, taskHost);
        CopilotHostedAgentRun? activeRun = null;
        EventHandler<CopilotAgentTaskHostChangedEventArgs>? onHostChanged = null;
        try
        {
            onHostChanged = (_, args) =>
            {
                if (args.Kind == CopilotAgentTaskHostChangeKind.Started)
                    conversation.Attachments.Add(lateAttachment);
            };
            taskHost.Changed += onHostChanged;

            viewModel.SendCommand.Execute(null);

            await runtime.Entered.WaitAsync(TestTimeout);
            activeRun = taskHost.ActiveRun;
            Assert.NotNull(activeRun);
            Assert.Equal(string.Empty, viewModel.InputText);
            Assert.DoesNotContain(capturedAttachment, conversation.Attachments);
            Assert.Contains(lateAttachment, conversation.Attachments);

            runtime.Release();
            await activeRun.Completion.WaitAsync(TestTimeout);
        }
        finally
        {
            if (onHostChanged != null)
                taskHost.Changed -= onHostChanged;
            await CompleteAndDisposeAsync(viewModel, runtime, taskHost, activeRun);
        }
    }

    [Fact]
    public async Task NewerComposerEditDuringSchedulingIsNotClearedByTheCapturedTurn()
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = CreateConfig(profile, "composer-stale-token-test-token");
        var conversation = CreateConversation(profile, "conversation-a", "original draft");
        conversation.DraftRequestMode = CopilotAgentMode.Review;
        conversation.DraftWorkspaceReviewTarget = CopilotWorkspaceReviewTargetContext.WorkingTree();
        var capturedAttachment = CopilotAttachmentItem.CreateContext("captured context");
        conversation.Attachments.Add(capturedAttachment);
        var runtime = new GatedFailingTurnRuntime();
        var taskHost = new CopilotAgentTaskHost();
        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        var viewModel = CreateViewModel(conversation, config, runtime, taskHost);
        CopilotHostedAgentRun? activeRun = null;
        EventHandler<CopilotAgentTaskHostChangedEventArgs>? onHostChanged = null;
        try
        {
            onHostChanged = (_, args) =>
            {
                if (args.Kind == CopilotAgentTaskHostChangeKind.Started)
                    viewModel.InputText = "newer draft";
            };
            taskHost.Changed += onHostChanged;

            viewModel.SendCommand.Execute(null);

            await runtime.Entered.WaitAsync(TestTimeout);
            activeRun = taskHost.ActiveRun;
            Assert.NotNull(activeRun);
            Assert.Equal("newer draft", viewModel.InputText);
            Assert.Equal("newer draft", conversation.DraftText);
            Assert.Equal(CopilotAgentMode.Review, conversation.DraftRequestMode);
            Assert.NotNull(conversation.DraftWorkspaceReviewTarget);
            Assert.Contains(capturedAttachment, conversation.Attachments);

            runtime.Release();
            await activeRun.Completion.WaitAsync(TestTimeout);
        }
        finally
        {
            if (onHostChanged != null)
                taskHost.Changed -= onHostChanged;
            await CompleteAndDisposeAsync(viewModel, runtime, taskHost, activeRun);
        }
    }

    [Fact]
    public async Task ActiveComposerSuggestsSlashCommandsThatCanRunFromTheNextTurn()
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = CreateConfig(profile, "queued-command-completion-test-token");
        var conversation = CreateConversation(profile, "conversation-a", string.Empty);
        var taskHost = new CopilotAgentTaskHost();
        var activeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseActive = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        using var viewModel = CreateViewModel(
            conversation,
            config,
            new GatedFailingTurnRuntime(),
            taskHost);
        var activeRun = taskHost.Start(
            conversation.Id,
            CopilotAgentMode.Auto,
            async _ =>
            {
                activeStarted.TrySetResult();
                await releaseActive.Task;
            });

        try
        {
            await activeStarted.Task.WaitAsync(TestTimeout);
            viewModel.InputText = "/pl";

            Assert.Contains(
                viewModel.LocalCommandSuggestions,
                command => command.Name == "/plan");
            Assert.Contains("排到下一轮", viewModel.LocalCommandSuggestionHeader, StringComparison.Ordinal);
        }
        finally
        {
            releaseActive.TrySetResult();
            await activeRun.Completion.WaitAsync(TestTimeout);
        }
    }

    [Fact]
    public async Task QueueActionDefersSlashCommandAndPreservesNewerComposerDraft()
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = CreateConfig(profile, "queued-status-command-test-token");
        var conversation = CreateConversation(profile, "conversation-a", string.Empty);
        var queuedAttachment = CopilotAttachmentItem.CreateContext("queued command attachment");
        var newerAttachment = CopilotAttachmentItem.CreateContext("newer draft attachment");
        conversation.Attachments.Add(queuedAttachment);
        var taskHost = new CopilotAgentTaskHost();
        var activeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseActive = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        var viewModel = CreateViewModel(conversation, config, new GatedFailingTurnRuntime(), taskHost);
        var activeRun = taskHost.Start(
            conversation.Id,
            CopilotAgentMode.Auto,
            async _ =>
            {
                activeStarted.TrySetResult();
                await releaseActive.Task;
            });

        try
        {
            await activeStarted.Task.WaitAsync(TestTimeout);
            viewModel.InputText = "/status";

            Assert.True(viewModel.QueueFollowUpCommand.CanExecute(null));
            viewModel.QueueFollowUpCommand.Execute(null);

            var queuedItem = Assert.Single(viewModel.QueuedFollowUps);
            var queuedRun = Assert.Single(taskHost.QueuedRuns);
            Assert.True(queuedItem.IsLocalCommand);
            Assert.False(queuedRun.IsAgent);
            Assert.Equal(string.Empty, viewModel.LocalCommandResultTitle);
            Assert.DoesNotContain(queuedAttachment, conversation.Attachments);
            Assert.Empty(conversation.Messages);

            viewModel.InputText = "newer draft";
            conversation.Attachments.Add(newerAttachment);
            releaseActive.TrySetResult();
            await activeRun.Completion.WaitAsync(TestTimeout);
            await queuedRun.Completion.WaitAsync(TestTimeout);

            Assert.Contains("/status", viewModel.LocalCommandResultTitle, StringComparison.Ordinal);
            Assert.Equal("newer draft", viewModel.InputText);
            Assert.Equal("newer draft", conversation.DraftText);
            Assert.Contains(conversation.Attachments, attachment => attachment.Id == queuedAttachment.Id);
            Assert.Contains(newerAttachment, conversation.Attachments);
            Assert.Empty(conversation.Messages);
            Assert.Empty(viewModel.QueuedFollowUps);
        }
        finally
        {
            releaseActive.TrySetResult();
            viewModel.Dispose();
        }
    }

    [Fact]
    public async Task QueuedSlashCommandRunsInItsOriginConversationAndRestoresLaterSelection()
    {
        var profileA = CreateProfile("profile-a", "Profile A", "model-a");
        var profileB = CreateProfile("profile-b", "Profile B", "model-b");
        var config = new CopilotConfig
        {
            SchemaVersion = CopilotConfig.CurrentSchemaVersion,
            McpBearerToken = "queued-command-conversation-binding-test-token",
            Profiles = new ObservableCollection<CopilotProfileConfig> { profileA, profileB },
        };
        var conversationA = CreateConversation(profileA, "conversation-a", string.Empty);
        var conversationB = CreateConversation(profileB, "conversation-b", "conversation B draft");
        var state = new CopilotChatState
        {
            ActiveConversationId = conversationA.Id,
            ActiveProfileId = profileA.Id,
            Conversations = new ObservableCollection<CopilotConversationRecord>
            {
                conversationA,
                conversationB,
            },
        };
        var taskHost = new CopilotAgentTaskHost();
        var activeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseActive = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        using var viewModel = new CopilotChatViewModel(
            new CopilotChatService(),
            new InMemoryStateStore(state),
            config,
            new GatedFailingTurnRuntime(),
            taskHost);
        var activeRun = taskHost.Start(
            conversationA.Id,
            CopilotAgentMode.Auto,
            async _ =>
            {
                activeStarted.TrySetResult();
                await releaseActive.Task;
            });

        try
        {
            await activeStarted.Task.WaitAsync(TestTimeout);
            viewModel.InputText = "/status";
            viewModel.QueueFollowUpCommand.Execute(null);
            var queuedRun = Assert.Single(taskHost.QueuedRuns);

            Assert.True(viewModel.SelectConversationCommand.CanExecute(conversationB));
            viewModel.SelectConversationCommand.Execute(conversationB);
            Assert.Same(conversationB, viewModel.SelectedConversation);

            releaseActive.TrySetResult();
            await activeRun.Completion.WaitAsync(TestTimeout);
            await queuedRun.Completion.WaitAsync(TestTimeout);

            Assert.Contains("/status", viewModel.LocalCommandResultTitle, StringComparison.Ordinal);
            Assert.Same(conversationB, viewModel.SelectedConversation);
            Assert.Same(profileB, viewModel.SelectedProfile);
            Assert.Equal("conversation B draft", viewModel.InputText);
            Assert.Empty(conversationA.Messages);
            Assert.Empty(conversationB.Messages);
        }
        finally
        {
            releaseActive.TrySetResult();
            if (!activeRun.Completion.IsCompleted)
                await activeRun.Completion.WaitAsync(TestTimeout);
        }
    }

    [Fact]
    public async Task QueuedPlanCommandStartsBeforeLaterFollowUpWithoutConsumingNewerDraft()
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = CreateConfig(profile, "queued-plan-command-test-token");
        var conversation = CreateConversation(profile, "conversation-a", string.Empty);
        var planAttachment = CopilotAttachmentItem.CreateContext("plan command attachment");
        var newerAttachment = CopilotAttachmentItem.CreateContext("newer draft attachment");
        conversation.Attachments.Add(planAttachment);
        var runtime = new GatedFailingTurnRuntime();
        var taskHost = new CopilotAgentTaskHost();
        var activeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseActive = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        var viewModel = CreateViewModel(conversation, config, runtime, taskHost);
        var activeRun = taskHost.Start(
            conversation.Id,
            CopilotAgentMode.Auto,
            async _ =>
            {
                activeStarted.TrySetResult();
                await releaseActive.Task;
            });
        CopilotHostedAgentRun? planRun = null;
        CopilotQueuedFollowUp? laterFollowUp = null;

        try
        {
            await activeStarted.Task.WaitAsync(TestTimeout);
            viewModel.InputText = "/plan inspect the deferred command path";
            viewModel.QueueFollowUpCommand.Execute(null);
            var commandItem = Assert.Single(viewModel.QueuedFollowUps);
            Assert.True(commandItem.IsLocalCommand);

            viewModel.InputText = "run after the plan";
            viewModel.QueueFollowUpCommand.Execute(null);
            laterFollowUp = Assert.Single(viewModel.QueuedFollowUps, item => !item.IsLocalCommand);
            viewModel.InputText = "newer draft";
            conversation.Attachments.Add(newerAttachment);

            releaseActive.TrySetResult();
            var request = await runtime.Entered.WaitAsync(TestTimeout);
            planRun = taskHost.ActiveRun;

            Assert.NotNull(planRun);
            Assert.Equal(CopilotAgentMode.Plan, request.Mode);
            Assert.Equal("inspect the deferred command path", request.UserText);
            Assert.Equal(1, taskHost.GetQueuePosition(laterFollowUp.RunId));
            Assert.Equal("newer draft", viewModel.InputText);
            Assert.Equal("newer draft", conversation.DraftText);
            Assert.Contains(request.HostContext.Attachments, attachment => attachment.Id == planAttachment.Id);
            Assert.DoesNotContain(conversation.Attachments, attachment => attachment.Id == planAttachment.Id);
            Assert.Contains(newerAttachment, conversation.Attachments);
            Assert.DoesNotContain(
                conversation.Messages,
                message => message.IsUser && string.Equals(message.Content, "/plan inspect the deferred command path", StringComparison.Ordinal));
            Assert.Contains(
                conversation.Messages,
                message => message.IsUser && string.Equals(message.Content, "inspect the deferred command path", StringComparison.Ordinal));
        }
        finally
        {
            runtime.Release();
            if (planRun != null && !planRun.Completion.IsCompleted)
            {
                try
                {
                    await planRun.Completion.WaitAsync(TestTimeout);
                }
                catch
                {
                }
            }
            if (laterFollowUp != null)
                taskHost.RequestCancel(laterFollowUp.RunId);
            releaseActive.TrySetResult();
            if (!activeRun.Completion.IsCompleted)
                await activeRun.Completion.WaitAsync(TestTimeout);
            viewModel.Dispose();
        }
    }

    [Fact]
    public async Task QueuedRetryCommandSchedulesTheRetryWithoutSendingItsSlashText()
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = CreateConfig(profile, "queued-retry-command-test-token");
        var conversation = CreateConversation(profile, "conversation-a", string.Empty);
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "retry this request")
        {
            RequestMode = CopilotAgentMode.Auto,
        });
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "old answer")
        {
            AgentStopReason = CopilotAgentStopReason.Completed,
        });
        var runtime = new GatedFailingTurnRuntime();
        var taskHost = new CopilotAgentTaskHost();
        var activeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseActive = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        using var viewModel = CreateViewModel(conversation, config, runtime, taskHost);
        var activeRun = taskHost.Start(
            conversation.Id,
            CopilotAgentMode.Auto,
            async _ =>
            {
                activeStarted.TrySetResult();
                await releaseActive.Task;
            });
        CopilotHostedAgentRun? retryRun = null;

        try
        {
            await activeStarted.Task.WaitAsync(TestTimeout);
            viewModel.InputText = "/retry";
            viewModel.QueueFollowUpCommand.Execute(null);
            var commandRun = Assert.Single(taskHost.QueuedRuns);
            viewModel.InputText = "newer draft";

            releaseActive.TrySetResult();
            var request = await runtime.Entered.WaitAsync(TestTimeout);
            retryRun = taskHost.ActiveRun;

            Assert.NotNull(retryRun);
            Assert.True(retryRun.IsAgent);
            Assert.Equal("retry this request", request.UserText);
            Assert.Equal(CopilotAgentMode.Auto, request.Mode);
            Assert.Equal("newer draft", viewModel.InputText);
            Assert.DoesNotContain(
                conversation.Messages,
                message => message.IsUser && string.Equals(message.Content, "/retry", StringComparison.Ordinal));
            await commandRun.Completion.WaitAsync(TestTimeout);
        }
        finally
        {
            runtime.Release();
            if (retryRun != null && !retryRun.Completion.IsCompleted)
            {
                try
                {
                    await retryRun.Completion.WaitAsync(TestTimeout);
                }
                catch
                {
                }
            }
            releaseActive.TrySetResult();
            if (!activeRun.Completion.IsCompleted)
                await activeRun.Completion.WaitAsync(TestTimeout);
        }
    }

    [Fact]
    public async Task UnknownQueuedSlashCommandReportsLocallyWithoutCreatingAgentMessages()
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = CreateConfig(profile, "queued-unknown-command-test-token");
        var conversation = CreateConversation(profile, "conversation-a", string.Empty);
        var taskHost = new CopilotAgentTaskHost();
        var activeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseActive = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        using var viewModel = CreateViewModel(
            conversation,
            config,
            new GatedFailingTurnRuntime(),
            taskHost);
        var activeRun = taskHost.Start(
            conversation.Id,
            CopilotAgentMode.Auto,
            async _ =>
            {
                activeStarted.TrySetResult();
                await releaseActive.Task;
            });

        await activeStarted.Task.WaitAsync(TestTimeout);
        viewModel.InputText = "/command-that-does-not-exist";
        viewModel.QueueFollowUpCommand.Execute(null);
        var queuedRun = Assert.Single(taskHost.QueuedRuns);

        releaseActive.TrySetResult();
        await activeRun.Completion.WaitAsync(TestTimeout);
        await queuedRun.Completion.WaitAsync(TestTimeout);

        Assert.NotEqual(string.Empty, viewModel.LocalCommandResultTitle);
        Assert.Empty(conversation.Messages);
    }

    [Fact]
    public async Task QueuedStopCommandDoesNotCancelItsDispatcherOrBlockTheNextFollowUp()
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = CreateConfig(profile, "queued-stop-command-test-token");
        var conversation = CreateConversation(profile, "conversation-a", string.Empty);
        var runtime = new GatedFailingTurnRuntime();
        var taskHost = new CopilotAgentTaskHost();
        var activeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseActive = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        using var viewModel = CreateViewModel(conversation, config, runtime, taskHost);
        var activeRun = taskHost.Start(
            conversation.Id,
            CopilotAgentMode.Auto,
            async _ =>
            {
                activeStarted.TrySetResult();
                await releaseActive.Task;
            });
        CopilotHostedAgentRun? followUpRun = null;

        try
        {
            await activeStarted.Task.WaitAsync(TestTimeout);
            viewModel.InputText = "/stop";
            viewModel.QueueFollowUpCommand.Execute(null);
            viewModel.InputText = "run after queued stop";
            viewModel.QueueFollowUpCommand.Execute(null);

            releaseActive.TrySetResult();
            var request = await runtime.Entered.WaitAsync(TestTimeout);
            followUpRun = taskHost.ActiveRun;

            Assert.Equal("run after queued stop", request.UserText);
            Assert.Contains("没有正在运行的任务", viewModel.LocalCommandResultText, StringComparison.Ordinal);
        }
        finally
        {
            runtime.Release();
            if (followUpRun != null && !followUpRun.Completion.IsCompleted)
            {
                try
                {
                    await followUpRun.Completion.WaitAsync(TestTimeout);
                }
                catch
                {
                }
            }
            releaseActive.TrySetResult();
            if (!activeRun.Completion.IsCompleted)
                await activeRun.Completion.WaitAsync(TestTimeout);
        }
    }

    [Fact]
    public async Task RestartedQueuedSlashCommandKeepsItsKindAndExecutesLocally()
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = CreateConfig(profile, "restarted-queued-command-test-token");
        var conversation = CreateConversation(profile, "conversation-a", string.Empty);
        var state = new CopilotChatState
        {
            ActiveConversationId = conversation.Id,
            ActiveProfileId = profile.Id,
            Conversations = new ObservableCollection<CopilotConversationRecord> { conversation },
            QueuedFollowUpRecoveries =
            [
                new CopilotQueuedFollowUpRecoveryRecord
                {
                    RunId = "queued-command-restart",
                    ConversationId = conversation.Id,
                    Prompt = "/status",
                    ComposerState = CopilotComposerStash.Capture(
                        "/status",
                        "/status".Length,
                        CopilotAgentMode.Auto,
                        []),
                    ProfileId = profile.Id,
                    QueuedAtUtc = new DateTimeOffset(2026, 8, 11, 16, 0, 0, TimeSpan.Zero),
                    ResumeAfterRestart = true,
                    IsLocalCommand = true,
                },
            ],
        };
        var taskHost = new CopilotAgentTaskHost();
        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        using var viewModel = new CopilotChatViewModel(
            new CopilotChatService(),
            new InMemoryStateStore(state),
            config,
            new GatedFailingTurnRuntime(),
            taskHost);
        var restoredRun = taskHost.ActiveRun ?? taskHost.QueuedRuns.SingleOrDefault();

        if (restoredRun != null)
        {
            Assert.False(restoredRun.IsAgent);
            Assert.Null(restoredRun.RunControl);
            await restoredRun.Completion.WaitAsync(TestTimeout);
        }

        Assert.Contains("/status", viewModel.LocalCommandResultTitle, StringComparison.Ordinal);
        Assert.Empty(conversation.Messages);
        Assert.Empty(state.QueuedFollowUpRecoveries);
    }

    private static CopilotConfig CreateConfig(CopilotProfileConfig profile, string bearerToken) => new()
    {
        SchemaVersion = CopilotConfig.CurrentSchemaVersion,
        McpBearerToken = bearerToken,
        Profiles = new ObservableCollection<CopilotProfileConfig> { profile },
    };

    private static CopilotConversationRecord CreateConversation(
        CopilotProfileConfig profile,
        string id,
        string draftText)
    {
        var conversation = CopilotConversationRecord.CreateEmpty(profile.Id, profile.DisplayLabel);
        conversation.Id = id;
        conversation.DraftText = draftText;
        return conversation;
    }

    private static CopilotChatViewModel CreateViewModel(
        CopilotConversationRecord conversation,
        CopilotConfig config,
        ICopilotTurnRuntime runtime,
        CopilotAgentTaskHost taskHost)
    {
        var state = new CopilotChatState
        {
            ActiveConversationId = conversation.Id,
            ActiveProfileId = conversation.ProfileId,
            Conversations = new ObservableCollection<CopilotConversationRecord> { conversation },
        };
        return new CopilotChatViewModel(
            new CopilotChatService(),
            new InMemoryStateStore(state),
            config,
            runtime,
            taskHost);
    }

    private static void ApplyAgentEvents(
        CopilotChatViewModel viewModel,
        CopilotHostedAgentRun hostedRun,
        CopilotConversationRecord conversation,
        CopilotChatMessage assistantMessage,
        params CopilotAgentEvent[] agentEvents)
    {
        var method = typeof(CopilotChatViewModel).GetMethod(
            "ApplyAgentEvents",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ApplyAgentEvents was not found.");
        method.Invoke(
            viewModel,
            [hostedRun, conversation, assistantMessage, agentEvents]);
    }

    private static async Task ProcessGoalAfterTurnAsync(
        CopilotChatViewModel viewModel,
        CopilotHostedAgentRun hostedRun,
        CopilotConversationRecord conversation,
        CopilotProfileConfig profile,
        CopilotChatMessage userMessage,
        CopilotChatMessage assistantMessage,
        string boundGoalId,
        CopilotTokenUsage turnUsage)
    {
        var method = typeof(CopilotChatViewModel).GetMethod(
            "ProcessGoalAfterTurnAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ProcessGoalAfterTurnAsync was not found.");
        var task = (Task?)method.Invoke(
            viewModel,
            [
                hostedRun,
                conversation,
                profile,
                userMessage,
                assistantMessage,
                boundGoalId,
                turnUsage,
            ]);
        Assert.NotNull(task);
        await task;
    }

    private static bool TryQueueGoalContinuation(
        CopilotChatViewModel viewModel,
        CopilotHostedAgentRun hostedRun,
        CopilotConversationRecord conversation,
        CopilotChatMessage assistantMessage,
        CopilotProfileConfig profile,
        CopilotAgentHostContextSnapshot completedTurnSnapshot,
        string goalId,
        string reason)
    {
        var method = typeof(CopilotChatViewModel).GetMethod(
            "TryQueueGoalContinuation",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("TryQueueGoalContinuation was not found.");
        return Assert.IsType<bool>(method.Invoke(
            viewModel,
            [
                hostedRun,
                conversation,
                assistantMessage,
                profile,
                completedTurnSnapshot,
                goalId,
                reason,
            ]));
    }

    private static async Task CompleteAndDisposeAsync(
        CopilotChatViewModel viewModel,
        GatedFailingTurnRuntime runtime,
        CopilotAgentTaskHost taskHost,
        CopilotHostedAgentRun? activeRun)
    {
        runtime.Release();
        var pendingRun = activeRun ?? taskHost.ActiveRun;
        if (pendingRun != null && !pendingRun.Completion.IsCompleted)
        {
            try
            {
                await pendingRun.Completion.WaitAsync(TestTimeout);
            }
            catch
            {
            }
        }
        viewModel.Dispose();
    }

    private static CopilotProfileConfig CreateProfile(string id, string name, string model) => new()
    {
        Id = id,
        Name = name,
        VendorType = CopilotVendorType.Custom,
        ProviderType = CopilotProviderType.OpenAICompatible,
        ApiKey = "profile-isolation-test-key",
        BaseUrl = "https://unit.test/v1",
        Model = model,
    };

    private static void AssertRequestUsesConfig(
        CopilotTurnRequest request,
        string expectedModel,
        string expectedApiKey,
        string expectedBaseUrl,
        int expectedContextWindowTokens,
        int expectedRequestTokenBudget,
        string expectedExternalMcpServerName,
        string expectedExternalMcpEndpoint,
        string expectedBearerTokenEnvironmentVariable)
    {
        Assert.Equal(expectedModel, request.Profile.Model);
        Assert.Equal(expectedApiKey, request.Profile.ApiKey);
        Assert.Equal(expectedBaseUrl, request.Profile.BaseUrl);
        Assert.Equal(expectedContextWindowTokens, request.AgentDefaults.ContextWindowTokens);
        Assert.Equal(expectedRequestTokenBudget, request.AgentDefaults.RequestTokenBudget);
        var externalMcpServer = Assert.Single(request.ExternalMcpServers);
        Assert.Equal(expectedExternalMcpServerName, externalMcpServer.Name);
        Assert.Equal(expectedExternalMcpEndpoint, externalMcpServer.Endpoint);
        Assert.Equal(expectedBearerTokenEnvironmentVariable, externalMcpServer.BearerTokenEnvironmentVariable);
    }

    private sealed class SequencedGatedFailingTurnRuntime : ICopilotTurnRuntime
    {
        private readonly Channel<TurnInvocation> _invocations = Channel.CreateUnbounded<TurnInvocation>();
        private readonly TaskCompletionSource _releaseAll =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<TurnInvocation> WaitForNextAsync()
        {
            return await _invocations.Reader.ReadAsync();
        }

        public async IAsyncEnumerable<CopilotTurnEvent> RunAsync(
            CopilotTurnRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var invocation = new TurnInvocation(request);
            await _invocations.Writer.WriteAsync(invocation, cancellationToken);
            await Task.WhenAny(invocation.Released, _releaseAll.Task).WaitAsync(cancellationToken);
            yield return new CopilotTurnStartedEvent("config-reload-turn", request.Mode);
            throw new InvalidOperationException("Expected config-reload test failure.");
        }

        public void ReleaseAll() => _releaseAll.TrySetResult();

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

        public sealed class TurnInvocation
        {
            private readonly TaskCompletionSource _released =
                new(TaskCreationOptions.RunContinuationsAsynchronously);

            public TurnInvocation(CopilotTurnRequest request)
            {
                Request = request;
            }

            public CopilotTurnRequest Request { get; }

            public Task Released => _released.Task;

            public void Release() => _released.TrySetResult();
        }
    }

    private sealed class GatedFailingTurnRuntime : ICopilotTurnRuntime
    {
        private readonly TaskCompletionSource _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<CopilotTurnRequest> Entered => _entered.Task;

        private readonly TaskCompletionSource<CopilotTurnRequest> _entered =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async IAsyncEnumerable<CopilotTurnEvent> RunAsync(
            CopilotTurnRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            _entered.TrySetResult(request);
            await _release.Task.WaitAsync(cancellationToken);
            yield return new CopilotTurnStartedEvent("profile-isolation-turn", request.Mode);
            throw new InvalidOperationException("Expected profile-isolation test failure.");
        }

        public void Release() => _release.TrySetResult();

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

        public Task SaveSerializedAsync(string serializedState, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public int CleanupOrphanedAttachments(CopilotChatState value) => 0;
    }

    private sealed class GatedGoalCompletionEvaluator(CopilotTokenUsage usage) : ICopilotGoalCompletionEvaluator
    {
        private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Entered => _entered.Task;

        public async Task<CopilotGoalEvaluationResult> EvaluateAsync(
            CopilotProfileConfig profile,
            CopilotConversationGoal goal,
            IReadOnlyList<CopilotRequestMessage> transcript,
            CopilotGoalTurnEvidence turnEvidence,
            CancellationToken cancellationToken)
        {
            _entered.TrySetResult();
            await _release.Task.WaitAsync(cancellationToken);
            return new CopilotGoalEvaluationResult(
                CopilotGoalEvaluationVerdict.Continue,
                "More verified work remains.",
                usage);
        }

        public void Release() => _release.TrySetResult();
    }

    private sealed class CancellingGoalCompletionEvaluator(
        Action requestCancellation,
        CopilotTokenUsage usage) : ICopilotGoalCompletionEvaluator
    {
        public Task<CopilotGoalEvaluationResult> EvaluateAsync(
            CopilotProfileConfig profile,
            CopilotConversationGoal goal,
            IReadOnlyList<CopilotRequestMessage> transcript,
            CopilotGoalTurnEvidence turnEvidence,
            CancellationToken cancellationToken)
        {
            requestCancellation();
            return Task.FromResult(new CopilotGoalEvaluationResult(
                CopilotGoalEvaluationVerdict.Continue,
                "More verified work remains.",
                usage));
        }
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

    private sealed class EnvironmentVariableScope : IDisposable
    {
        private readonly string _name;
        private readonly string? _previousValue;

        public EnvironmentVariableScope(string name, string value)
        {
            _name = name;
            _previousValue = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(_name, _previousValue);
        }
    }
}
