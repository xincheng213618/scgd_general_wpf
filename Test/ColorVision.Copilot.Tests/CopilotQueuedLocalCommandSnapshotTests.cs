using ColorVision.Copilot;
using ColorVision.Solution;
using ColorVision.Solution.Explorer;
using ColorVision.Solution.Workspace;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace ColorVision.Copilot.Tests;

[Collection(CopilotChatViewModelProfileIsolationFixture.Name)]
public sealed class CopilotQueuedLocalCommandSnapshotTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QueuedPlanAndOrdinaryFollowUpKeepTheSubmittedWorkspaceContext(bool planCommand)
    {
        await using var fixture = new QueueFixture();
        var queued = await fixture.QueueAsync(planCommand ? "/plan inspect the submitted file" : "inspect the submitted file");
        fixture.SelectWorkspace(useSecondWorkspace: true);

        var request = await fixture.DispatchAsync();

        fixture.AssertNewerDraftWasPreserved(request, consumesQueuedAttachment: true);
        Assert.Equal(queued.SubmissionContext.ActiveDocumentPath, request.HostContext.ActiveDocumentPath);
        Assert.Equal(queued.SubmissionContext.SolutionDirectoryPath, request.HostContext.SolutionDirectoryPath);
        Assert.Equal(planCommand ? CopilotAgentMode.Plan : CopilotAgentMode.Auto, request.Mode);
        Assert.Equal("inspect the submitted file", request.UserText);
    }

    [Theory]
    [InlineData(false, "profile")]
    [InlineData(true, "profile")]
    [InlineData(false, "runtime")]
    [InlineData(true, "runtime")]
    public async Task QueuedPlanAndOrdinaryFollowUpKeepTheSubmittedConfiguration(bool planCommand, string changedConfiguration)
    {
        await using var fixture = new QueueFixture();
        var queued = await fixture.QueueAsync(planCommand ? "/plan inspect the submitted configuration" : "inspect the submitted configuration");
        if (changedConfiguration == "profile")
        {
            fixture.ViewModel.SelectedProfile.Model = "model-after-queue";
            fixture.ViewModel.SelectedProfile.MaxTokens = 2_048;
        }
        else
        {
            fixture.Config.AgentDefaults.RequestTokenBudget = 64_000;
            fixture.Config.ExternalMcpServers[0].Name = "after-queue";
            fixture.Config.ExternalMcpServers[0].Enabled = false;
        }

        var request = await fixture.DispatchAsync();

        fixture.AssertNewerDraftWasPreserved(request, consumesQueuedAttachment: true);
        if (changedConfiguration == "profile")
        {
            Assert.Equal(queued.Profile.Model, request.Profile.Model);
            Assert.Equal(queued.Profile.MaxTokens, request.Profile.MaxTokens);
        }
        else
        {
            Assert.Equal(queued.RuntimeConfigSnapshot.CreateAgentDefaultsSnapshot().RequestTokenBudget, request.AgentDefaults.RequestTokenBudget);
            var expectedServer = Assert.Single(queued.RuntimeConfigSnapshot.CreateExternalMcpServerSnapshots());
            var actualServer = Assert.Single(request.ExternalMcpServers);
            Assert.Equal(expectedServer.Name, actualServer.Name);
            Assert.Equal(expectedServer.Enabled, actualServer.Enabled);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task DispatchingQueuedFollowUpPreservesTheConversationsLaterProfileSelection(bool planCommand, bool switchConversation)
    {
        await using var fixture = new QueueFixture();
        var queued = await fixture.QueueAsync(planCommand ? "/plan inspect the submitted file" : "inspect the submitted file");
        var laterProfile = fixture.ViewModel.SelectedProfile!.Clone();
        laterProfile.Id = "later-profile";
        laterProfile.Name = "Later selected profile";
        laterProfile.Model = "later-selected-model";
        fixture.Config.Profiles.Add(laterProfile);
        fixture.ViewModel.SelectedProfile = laterProfile;
        var other = CopilotConversationRecord.CreateEmpty(queued.Profile.Id, queued.Profile.DisplayLabel);
        other.DraftText = "Other conversation draft";
        fixture.ViewModel.Conversations.Add(other);
        if (switchConversation)
            Assert.True(fixture.ViewModel.TrySelectConversation(other.Id));

        using var observation = fixture.ObserveUiOwnership(queued);
        var request = await fixture.DispatchAsync();

        observation.AssertUnchanged();
        Assert.Equal(queued.Profile.Id, request.Profile.Id);
        Assert.Equal(queued.Profile.Model, request.Profile.Model);
        Assert.Equal(fixture.Conversation.Id, request.ConversationId);
        Assert.Equal(laterProfile.Id, fixture.Conversation.ProfileId);
        Assert.Equal(laterProfile.DisplayLabel, fixture.Conversation.ProfileDisplayName);
        Assert.Equal("newer draft", fixture.Conversation.DraftText);
        Assert.Same(switchConversation ? other : fixture.Conversation, fixture.ViewModel.SelectedConversation);
        Assert.Equal("Other conversation draft", other.DraftText);
        Assert.True(fixture.ViewModel.TrySelectConversation(fixture.Conversation.Id));
        Assert.Same(laterProfile, fixture.ViewModel.SelectedProfile);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QueuedCompactionUsesSubmittedConfigurationAndPreservesLaterProfileSelection(bool switchConversation)
    {
        using var handler = new CompactionHandler();
        using var client = new HttpClient(handler);
        await using var fixture = new QueueFixture(new CopilotChatService(client));
        fixture.Conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "Original question " + new string('u', 300)));
        fixture.Conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "Original answer " + new string('a', 300)));
        var queued = await fixture.QueueAsync("/compact retain the original goal");
        var laterProfile = fixture.ViewModel.SelectedProfile!.Clone();
        laterProfile.Id = "later-compaction-profile";
        laterProfile.Name = "Later selected profile";
        laterProfile.Model = "later-compaction-model";
        fixture.Config.Profiles.Add(laterProfile);
        fixture.ViewModel.SelectedProfile = laterProfile;
        fixture.Config.AgentDefaults.ContextWindowTokens = CopilotAgentTokenBudget.MinimumContextWindowTokens;
        fixture.SelectWorkspace(useSecondWorkspace: true);
        var other = CopilotConversationRecord.CreateEmpty(queued.Profile.Id, queued.Profile.DisplayLabel);
        other.DraftText = "Other conversation draft";
        fixture.ViewModel.Conversations.Add(other);
        if (switchConversation)
            Assert.True(fixture.ViewModel.TrySelectConversation(other.Id));

        using var observation = fixture.ObserveUiOwnership(queued);
        await fixture.DispatchLocalCommandAsync();

        observation.AssertUnchanged();
        var payload = JObject.Parse(Assert.Single(handler.Payloads));
        Assert.Equal(queued.Profile.Model, (string?)payload["model"]);
        Assert.Equal(4_096, (int?)payload["max_tokens"]);
        Assert.Contains("Original question", payload.ToString(), StringComparison.Ordinal);
        Assert.Contains("Original answer", payload.ToString(), StringComparison.Ordinal);
        Assert.Equal(
            CopilotConversationCompactionPrompt.BuildRequest("retain the original goal", queued.SubmissionContext.ProjectInstructionDiscoveryOptions.CompactPrompt),
            (string?)payload["messages"]?.Last?["content"]);
        Assert.Equal("Captured queued summary", fixture.Conversation.Compaction?.Summary);
        Assert.Equal(laterProfile.Id, fixture.Conversation.ProfileId);
        Assert.Equal(laterProfile.DisplayLabel, fixture.Conversation.ProfileDisplayName);
        Assert.Equal("newer draft", fixture.Conversation.DraftText);
        Assert.Null(other.Compaction);
        Assert.Equal("Other conversation draft", other.DraftText);
        Assert.Same(switchConversation ? other : fixture.Conversation, fixture.ViewModel.SelectedConversation);
        Assert.True(fixture.ViewModel.TrySelectConversation(fixture.Conversation.Id));
        Assert.Same(laterProfile, fixture.ViewModel.SelectedProfile);
    }

    [Fact]
    public async Task QueuedInitializationKeepsDirectPromptSeparateFromQueuedAndNewerComposerAttachments()
    {
        await using var fixture = new QueueFixture();
        await fixture.QueueAsync("/init");

        var request = await fixture.DispatchAsync();

        fixture.AssertNewerDraftWasPreserved(request, consumesQueuedAttachment: false);
        Assert.Equal(CopilotAgentMode.Code, request.Mode);
        Assert.Equal(CopilotProjectInitialization.VisiblePrompt, request.UserText);
        Assert.True(CopilotProjectInitialization.IsInitializationRequest(request.ExistingRequestContent));
        Assert.Null(request.AgentSkillReference);
        Assert.Null(request.WorkspaceReviewTarget);
        Assert.Null(request.Recovery);
    }

    [Fact]
    public async Task QueuedInitializationKeepsItsAuthorizedTargetInTheSubmittedWorkspace()
    {
        await using var fixture = new QueueFixture();
        var queued = await fixture.QueueAsync("/init");
        fixture.SelectWorkspace(useSecondWorkspace: true);
        fixture.ChangeLiveConfiguration();
        var other = fixture.SelectOtherConversation();
        using var observation = fixture.ObserveUiOwnership(queued);

        var request = await fixture.DispatchAsync();

        observation.AssertUnchanged();
        Assert.Same(other, fixture.ViewModel.SelectedConversation);
        fixture.AssertNewerDraftWasPreserved(request, consumesQueuedAttachment: false);
        fixture.AssertSubmissionSnapshot(queued, request);
        var expectedPlan = CopilotProjectInitialization.Create(
            queued.SubmissionContext.SolutionDirectoryPath,
            queued.SubmissionContext.ProjectInstructionDiscoveryOptions);
        Assert.True(expectedPlan.CanStart);
        Assert.Equal(expectedPlan.ModelPrompt, request.ExistingRequestContent);
    }

    [Theory]
    [InlineData("/review --current inspect the queued workspace")]
    [InlineData("/verify inspect the queued workspace")]
    public async Task QueuedWorkspaceReviewsKeepTheSubmittedContextAndConfiguration(string command)
    {
        await using var fixture = new QueueFixture();
        var queued = await fixture.QueueAsync(command);
        fixture.SelectWorkspace(useSecondWorkspace: true);
        fixture.ChangeLiveConfiguration();
        var other = fixture.SelectOtherConversation();
        using var observation = fixture.ObserveUiOwnership(queued);

        var request = await fixture.DispatchAsync();

        observation.AssertUnchanged();
        Assert.Same(other, fixture.ViewModel.SelectedConversation);
        fixture.AssertNewerDraftWasPreserved(request, consumesQueuedAttachment: true);
        fixture.AssertSubmissionSnapshot(queued, request);
        Assert.Equal(CopilotAgentMode.Review, request.Mode);
        Assert.NotNull(request.WorkspaceReviewTarget);
    }

    [Theory]
    [InlineData("/retry", false)]
    [InlineData("/retry refresh", false)]
    [InlineData("/retry", true)]
    public async Task QueuedRetryKeepsOriginalMessageInputsAndSubmittedHostConfiguration(string command, bool clearLiveProfile)
    {
        await using var fixture = new QueueFixture();
        fixture.Conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "earlier question"));
        fixture.Conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "earlier answer"));
        var originalAttachment = CopilotAttachmentItem.CreateContext("Original request evidence.");
        var originalUser = new CopilotChatMessage(CopilotChatRole.User, "retry the original request")
        {
            RequestMode = CopilotAgentMode.Code,
            Attachments = [originalAttachment],
            AttachmentSnapshotCaptured = true,
        };
        fixture.Conversation.Messages.Add(originalUser);
        fixture.Conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "original answer")
        {
            AgentStopReason = CopilotAgentStopReason.Completed,
        });
        var expectedHistory = CopilotConversationRequestBuilder.CaptureHistorySnapshot(fixture.Conversation, originalUser);
        var queued = await fixture.QueueAsync(command);
        fixture.SelectWorkspace(useSecondWorkspace: true);
        fixture.ChangeLiveConfiguration();
        if (clearLiveProfile)
        {
            fixture.ViewModel.SelectedProfile.ApiKey = string.Empty;
            Assert.False(fixture.ViewModel.SelectedProfile.IsConfigured);
            Assert.True(queued.Profile.IsConfigured);
        }
        var other = fixture.SelectOtherConversation();
        using var observation = fixture.ObserveUiOwnership(queued);

        var request = await fixture.DispatchAsync();

        observation.AssertUnchanged();
        Assert.Same(other, fixture.ViewModel.SelectedConversation);
        fixture.AssertNewerDraftWasPreserved(request, consumesQueuedAttachment: false, retryAttachment: originalAttachment);
        fixture.AssertSubmissionSnapshot(queued, request);
        Assert.Equal(originalUser.RequestMode, request.Mode);
        Assert.Equal(originalUser.Content, request.UserText);
        Assert.Equal(expectedHistory.ModelMessages, request.HostContext.ConversationHistory.ModelMessages);
        Assert.Equal(expectedHistory.VisibleMessages, request.HostContext.ConversationHistory.VisibleMessages);
        Assert.Contains(originalUser, fixture.Conversation.Messages);
        Assert.Equal(4, fixture.Conversation.Messages.Count);
    }

    [Fact]
    public async Task QueuedPlanBudgetUsesItsSourceConversationInsteadOfTheViewedConversationsGoal()
    {
        await using var fixture = new QueueFixture();
        fixture.Config.AgentDefaults.RequestTokenBudget = CopilotAgentRunBudget.MinimumRequestTokenBudget;
        var task = "检查" + new string('字', 256);
        var queued = await fixture.QueueAsync("/plan " + task);
        var other = fixture.SelectOtherConversation();
        other.Goal = CopilotConversationGoal.Create(new string('目', CopilotConversationGoal.MaximumObjectiveCharacters), DateTimeOffset.UtcNow);
        using var observation = fixture.ObserveUiOwnership(queued);

        var request = await fixture.DispatchAsync();

        observation.AssertUnchanged();
        Assert.Equal(fixture.Conversation.Id, request.ConversationId);
        Assert.Equal(task, request.UserText);
        Assert.Same(other, fixture.ViewModel.SelectedConversation);
        fixture.AssertNewerDraftWasPreserved(request, consumesQueuedAttachment: true);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AwaitingQueuedCompactionDoesNotGiveUiRequestsSuccessorAdmission(bool switchConversation)
    {
        using var handler = new CompactionHandler(gateResponse: true);
        using var client = new HttpClient(handler);
        await using var fixture = new QueueFixture(new CopilotChatService(client));
        fixture.Conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "Question " + new string('u', 300)));
        fixture.Conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "Answer " + new string('a', 300)));
        var queued = await fixture.QueueAsync("/compact");
        var selected = switchConversation ? fixture.SelectOtherConversation() : fixture.Conversation;
        if (switchConversation)
        {
            selected.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "Other request"));
            selected.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "Other answer"));
        }
        using var observation = fixture.ObserveUiOwnership(queued);
        var dispatch = fixture.DispatchLocalCommandAsync();
        try
        {
            await handler.Entered.Task.WaitAsync(TestTimeout);
            Assert.Same(selected, fixture.ViewModel.SelectedConversation);
            Assert.True(fixture.ViewModel.IsBusy);
            observation.AssertUnchanged();
            var admission = Assert.IsType<CopilotRequestAdmissionResult>(typeof(CopilotChatViewModel)
                .GetMethod("EvaluateComposerRequestAdmission", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(fixture.ViewModel, [CopilotAgentMode.Auto]));
            Assert.False(admission.IsAllowed);
            Assert.False(fixture.ViewModel.RetryMessageCommand.CanExecute(selected.Messages.Last()));

            fixture.ViewModel.InputText = "Typed while the queued command is awaiting its provider";
            var send = Assert.IsAssignableFrom<Task>(typeof(CopilotChatViewModel)
                .GetMethod("SendAsync", BindingFlags.Instance | BindingFlags.NonPublic, Type.EmptyTypes)!
                .Invoke(fixture.ViewModel, null));
            await send.WaitAsync(TestTimeout);
            fixture.ViewModel.RetryMessageCommand.Execute(selected.Messages.Last());
            Assert.Empty(fixture.Host.QueuedRuns);
            Assert.Equal(2, fixture.Conversation.Messages.Count);
            Assert.Equal(2, selected.Messages.Count);
            Assert.Equal("Typed while the queued command is awaiting its provider", fixture.ViewModel.InputText);
        }
        finally
        {
            handler.Release.TrySetResult();
            await dispatch.WaitAsync(TestTimeout);
        }

        observation.AssertUnchanged();
        Assert.Same(selected, fixture.ViewModel.SelectedConversation);
        Assert.Equal("Typed while the queued command is awaiting its provider", selected.DraftText);
        Assert.Equal("Captured queued summary", fixture.Conversation.Compaction?.Summary);
        Assert.Single(handler.Payloads);
    }

    [Fact]
    public async Task CancellingAwaitingQueuedCompactionKeepsTheSourceHistoryAndTheViewedDraft()
    {
        using var handler = new CompactionHandler(gateResponse: true);
        using var client = new HttpClient(handler);
        await using var fixture = new QueueFixture(new CopilotChatService(client));
        var user = new CopilotChatMessage(CopilotChatRole.User, "Question " + new string('u', 300));
        var assistant = new CopilotChatMessage(CopilotChatRole.Assistant, "Answer " + new string('a', 300));
        fixture.Conversation.Messages.Add(user);
        fixture.Conversation.Messages.Add(assistant);
        var queued = await fixture.QueueAsync("/compact");
        var other = fixture.SelectOtherConversation();
        using var observation = fixture.ObserveUiOwnership(queued);
        var dispatch = fixture.DispatchLocalCommandAsync();
        try
        {
            await handler.Entered.Task.WaitAsync(TestTimeout);
            Assert.True(fixture.Host.RequestCancel(queued.RunId));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => dispatch.WaitAsync(TestTimeout));
        }
        finally
        {
            handler.Release.TrySetResult();
        }

        observation.AssertUnchanged();
        Assert.Same(other, fixture.ViewModel.SelectedConversation);
        Assert.Equal("Other conversation draft", fixture.ViewModel.InputText);
        Assert.Equal("newer draft", fixture.Conversation.DraftText);
        Assert.Null(fixture.Conversation.Compaction);
        Assert.Equal(new[] { user, assistant }, fixture.Conversation.Messages);
        Assert.Equal(2, fixture.Conversation.Attachments.Count);
        Assert.Empty(fixture.Host.QueuedRuns);
        Assert.Contains("上下文压缩已取消", fixture.ViewModel.LocalCommandResultText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ParentCancellationBeforeChildCallbackPreventsCompactionCommitButPreservesReturnedUsage()
    {
        using var handler = new CompactionHandler(gateResponse: true);
        using var client = new HttpClient(handler);
        await using var fixture = new QueueFixture(new CopilotChatService(client));
        var user = new CopilotChatMessage(CopilotChatRole.User, "Question " + new string('u', 300));
        var assistant = new CopilotChatMessage(CopilotChatRole.Assistant, "Answer " + new string('a', 300));
        fixture.Conversation.Messages.Add(user);
        fixture.Conversation.Messages.Add(assistant);
        var queued = await fixture.QueueAsync("/compact");
        var other = fixture.SelectOtherConversation();
        var dispatch = fixture.DispatchLocalCommandAsync();
        await handler.Entered.Task.WaitAsync(TestTimeout);
        var parentRun = Assert.IsType<CopilotHostedAgentRun>(fixture.Host.ActiveRun);
        var callbackEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var releaseCallbacks = new ManualResetEventSlim();
        // Cancellation callbacks run in reverse registration order. Hold the parent
        // before it can signal the child's HTTP token, then return a successful reply.
        using var callbackBlocker = parentRun.CancellationToken.Register(() =>
        {
            callbackEntered.TrySetResult();
            releaseCallbacks.Wait(TestTimeout);
        });
        try
        {
            Assert.True(fixture.Host.RequestCancel(queued.RunId));
            await callbackEntered.Task.WaitAsync(TestTimeout);
            Assert.True(parentRun.CancellationToken.IsCancellationRequested);
            Assert.False(handler.RequestCancellationToken.IsCancellationRequested);
            handler.Release.TrySetResult();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => dispatch.WaitAsync(TestTimeout));

            Assert.Null(fixture.Conversation.Compaction);
            Assert.Equal(new[] { user, assistant }, fixture.Conversation.Messages);
            Assert.Same(other, fixture.ViewModel.SelectedConversation);
            Assert.Equal("Other conversation draft", fixture.ViewModel.InputText);
            Assert.Equal("newer draft", fixture.Conversation.DraftText);
            var usage = Assert.IsType<CopilotConversationAuxiliaryUsage>(fixture.Conversation.CompactionUsage);
            Assert.Equal(1, usage.RequestCount);
            Assert.Equal(new CopilotTokenUsage(100, 10, 110), usage.Usage);
            Assert.Contains("上下文压缩已取消", fixture.ViewModel.LocalCommandResultText, StringComparison.Ordinal);
        }
        finally
        {
            releaseCallbacks.Set();
            handler.Release.TrySetResult();
        }
    }

    private sealed class QueueFixture : IAsyncDisposable
    {
        private static readonly FieldInfo SolutionInstanceField = typeof(SolutionManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)!;
        private static readonly FieldInfo CurrentExplorerField = typeof(SolutionManager).GetField("_CurrentSolutionExplorer", BindingFlags.Instance | BindingFlags.NonPublic)!;
        private static readonly PropertyInfo ExplorerDirectoryProperty = typeof(SolutionExplorer).GetProperty(nameof(SolutionExplorer.DirectoryInfo))!;
        private readonly object? _previousSolution = SolutionInstanceField.GetValue(null);
        private readonly object _isolatedSolution = RuntimeHelpers.GetUninitializedObject(typeof(SolutionManager));
        private readonly object _isolatedExplorer = RuntimeHelpers.GetUninitializedObject(typeof(SolutionExplorer));
        private readonly string _previousContentId = WorkspaceManager.SelectedContentId;
        private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("copilot-queued-command-snapshot-");
        private readonly TaskCompletionSource _activeStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _releaseActive = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly GatedRuntime _runtime = new();
        private readonly CopilotHostedAgentRun _initialRun;
        private readonly string _firstWorkspace;
        private readonly string _secondWorkspace;
        private readonly string _firstDocument;
        private readonly string _secondDocument;

        public QueueFixture(CopilotChatService? chatService = null)
        {
            _firstWorkspace = Directory.CreateDirectory(Path.Combine(_directory.FullName, "first")).FullName;
            _secondWorkspace = Directory.CreateDirectory(Path.Combine(_directory.FullName, "second")).FullName;
            _firstDocument = Path.Combine(_firstWorkspace, "first.txt");
            _secondDocument = Path.Combine(_secondWorkspace, "second.txt");
            File.WriteAllText(_firstDocument, "Submitted document.");
            File.WriteAllText(_secondDocument, "Document selected while waiting.");
            SolutionInstanceField.SetValue(null, _isolatedSolution);
            CurrentExplorerField.SetValue(_isolatedSolution, _isolatedExplorer);
            SelectWorkspace(useSecondWorkspace: false);
            var profile = new CopilotProfileConfig
            {
                Id = "queued-command-profile",
                Name = "Submitted profile",
                ProviderType = CopilotProviderType.OpenAICompatible,
                VendorType = CopilotVendorType.Custom,
                BaseUrl = "https://example.test/v1",
                ApiKey = "queued-command-test-key",
                Model = "model-at-queue",
                MaxTokens = 4_096,
            };
            Config = new CopilotConfig
            {
                SchemaVersion = CopilotConfig.CurrentSchemaVersion,
                McpBearerToken = "queued-command-snapshot-test-token",
                Profiles = [profile],
                AgentDefaults = new CopilotAgentDefaultsConfig { RequestTokenBudget = 128_000 },
                ExternalMcpServers = [new CopilotMcpClientServerConfig
                {
                    Name = "at-queue",
                    Endpoint = "https://example.test/mcp",
                    AccessPolicy = CopilotMcpClientAccessPolicy.ReadOnly,
                }],
            };
            Conversation = CopilotConversationRecord.CreateEmpty(profile.Id, profile.DisplayLabel);
            var state = new CopilotChatState
            {
                ActiveConversationId = Conversation.Id,
                ActiveProfileId = profile.Id,
                Conversations = [Conversation],
            };
            ViewModel = new CopilotChatViewModel(chatService ?? new CopilotChatService(), new MemoryStore(state, _directory.FullName), Config, _runtime, Host);
            Conversation.Attachments.Add(QueuedAttachment);
            _initialRun = Host.Start(Conversation.Id, CopilotAgentMode.Auto, async _ =>
            {
                _activeStarted.TrySetResult();
                await _releaseActive.Task;
            });
        }

        public CopilotAgentTaskHost Host { get; } = new();
        public CopilotConfig Config { get; }
        public CopilotConversationRecord Conversation { get; }
        public CopilotChatViewModel ViewModel { get; }
        private CopilotAttachmentItem QueuedAttachment { get; } = CopilotAttachmentItem.CreateContext("Queued attachment.");
        private CopilotAttachmentItem NewerAttachment { get; } = CopilotAttachmentItem.CreateContext("Newer draft attachment.");

        public async Task<CopilotQueuedFollowUp> QueueAsync(string prompt)
        {
            await _activeStarted.Task.WaitAsync(TestTimeout);
            ViewModel.InputText = prompt;
            Assert.True(ViewModel.TryQueueCurrentRunFollowUp());
            var queued = Assert.Single(ViewModel.QueuedFollowUps);
            Assert.Equal(_firstDocument, queued.SubmissionContext.ActiveDocumentPath);
            Assert.Equal(_firstWorkspace, queued.SubmissionContext.SolutionDirectoryPath);
            ViewModel.InputText = "newer draft";
            Conversation.Attachments.Add(NewerAttachment);
            return queued;
        }

        public void SelectWorkspace(bool useSecondWorkspace)
        {
            // Isolate workspace ownership without initializing real projects or watchers;
            // the document change itself travels through the production workspace event.
            ExplorerDirectoryProperty.SetValue(_isolatedExplorer, new DirectoryInfo(useSecondWorkspace ? _secondWorkspace : _firstWorkspace));
            WorkspaceManager.OnContentIdSelected(useSecondWorkspace ? _secondDocument : _firstDocument);
        }

        public CopilotConversationRecord SelectOtherConversation()
        {
            var other = CopilotConversationRecord.CreateEmpty(Conversation.ProfileId, Conversation.ProfileDisplayName);
            other.DraftText = "Other conversation draft";
            ViewModel.Conversations.Add(other);
            Assert.True(ViewModel.TrySelectConversation(other.Id));
            return other;
        }

        public UiOwnershipObservation ObserveUiOwnership(CopilotQueuedFollowUp queued) => new(ViewModel, Host, queued.RunId);

        public async Task<CopilotTurnRequest> DispatchAsync()
        {
            _releaseActive.TrySetResult();
            return await _runtime.Entered.Task.WaitAsync(TestTimeout);
        }

        public async Task DispatchLocalCommandAsync()
        {
            var queuedRun = Assert.Single(Host.QueuedRuns);
            _releaseActive.TrySetResult();
            await queuedRun.Completion.WaitAsync(TestTimeout);
        }

        public void ChangeLiveConfiguration()
        {
            ViewModel.SelectedProfile.Model = "model-after-queue";
            ViewModel.SelectedProfile.MaxTokens = 2_048;
            Config.AgentDefaults.RequestTokenBudget = 64_000;
            Config.ExternalMcpServers[0].Name = "after-queue";
            Config.ExternalMcpServers[0].Enabled = false;
        }

        public void AssertSubmissionSnapshot(CopilotQueuedFollowUp queued, CopilotTurnRequest request)
        {
            Assert.Equal(queued.SubmissionContext.ActiveDocumentPath, request.HostContext.ActiveDocumentPath);
            Assert.Equal(queued.SubmissionContext.SolutionDirectoryPath, request.HostContext.SolutionDirectoryPath);
            Assert.Equal(queued.Profile.Model, request.Profile.Model);
            Assert.Equal(queued.Profile.MaxTokens, request.Profile.MaxTokens);
            Assert.Equal(queued.Profile.EffectiveSystemPrompt, request.Profile.EffectiveSystemPrompt);
            Assert.Equal(queued.RuntimeConfigSnapshot.CreateAgentDefaultsSnapshot().RequestTokenBudget, request.AgentDefaults.RequestTokenBudget);
            var expectedServer = Assert.Single(queued.RuntimeConfigSnapshot.CreateExternalMcpServerSnapshots());
            var actualServer = Assert.Single(request.ExternalMcpServers);
            Assert.Equal(expectedServer.Name, actualServer.Name);
            Assert.Equal(expectedServer.Enabled, actualServer.Enabled);
        }

        public void AssertNewerDraftWasPreserved(CopilotTurnRequest request, bool consumesQueuedAttachment, CopilotAttachmentItem? retryAttachment = null)
        {
            Assert.Equal(ReferenceEquals(ViewModel.SelectedConversation, Conversation)
                ? "newer draft"
                : "Other conversation draft", ViewModel.InputText);
            Assert.Equal("newer draft", Conversation.DraftText);
            Assert.Contains(NewerAttachment, Conversation.Attachments);
            Assert.DoesNotContain(request.HostContext.Attachments, item => item.Id == NewerAttachment.Id);
            if (consumesQueuedAttachment)
            {
                Assert.Contains(request.HostContext.Attachments, item => item.Id == QueuedAttachment.Id);
                Assert.DoesNotContain(Conversation.Attachments, item => item.Id == QueuedAttachment.Id);
            }
            else
            {
                if (retryAttachment == null)
                    Assert.Empty(request.HostContext.Attachments);
                else
                    Assert.Equal(retryAttachment.Id, Assert.Single(request.HostContext.Attachments).Id);
                Assert.Contains(Conversation.Attachments, item => item.Id == QueuedAttachment.Id);
            }
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                var runs = Host.ScheduledRuns.Append(_initialRun).Distinct().ToArray();
                Host.Shutdown();
                _releaseActive.TrySetResult();
                _runtime.Release.TrySetResult();
                foreach (var run in runs)
                {
                    try { await run.Completion.WaitAsync(TestTimeout); }
                    catch (OperationCanceledException) { }
                }
            }
            finally
            {
                ViewModel.Dispose();
                WorkspaceManager.OnContentIdSelected(_previousContentId);
                if (ReferenceEquals(SolutionInstanceField.GetValue(null), _isolatedSolution))
                    SolutionInstanceField.SetValue(null, _previousSolution);
                var resolved = Path.GetFullPath(_directory.FullName);
                if (!string.Equals(Path.GetDirectoryName(resolved), Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())), StringComparison.OrdinalIgnoreCase)
                    || !Path.GetFileName(resolved).StartsWith("copilot-queued-command-snapshot-", StringComparison.Ordinal))
                    throw new InvalidOperationException("Unexpected temporary workspace path.");
                Directory.Delete(resolved, recursive: true);
            }
        }
    }

    private sealed class UiOwnershipObservation : IDisposable
    {
        private readonly CopilotChatViewModel _viewModel;
        private readonly CopilotAgentTaskHost _host;
        private readonly string _queuedRunId;
        private readonly CopilotConversationRecord? _selectedConversation;
        private readonly ConcurrentQueue<string> _unexpectedChanges = new();

        public UiOwnershipObservation(CopilotChatViewModel viewModel, CopilotAgentTaskHost host, string queuedRunId)
        {
            _viewModel = viewModel;
            _host = host;
            _queuedRunId = queuedRunId;
            _selectedConversation = viewModel.SelectedConversation;
            viewModel.PropertyChanged += OnPropertyChanged;
        }

        private void OnPropertyChanged(object? sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(CopilotChatViewModel.SelectedConversation)
                    && !ReferenceEquals(_viewModel.SelectedConversation, _selectedConversation)
                || args.PropertyName == nameof(CopilotChatViewModel.IsBusy)
                    && !_viewModel.IsBusy
                    && string.Equals(_host.ActiveRun?.Id, _queuedRunId, StringComparison.Ordinal))
            {
                _unexpectedChanges.Enqueue(args.PropertyName!);
            }
        }

        public void AssertUnchanged() => Assert.Empty(_unexpectedChanges);
        public void Dispose() => _viewModel.PropertyChanged -= OnPropertyChanged;
    }

    private sealed class CompactionHandler(bool gateResponse = false) : HttpMessageHandler
    {
        public List<string> Payloads { get; } = [];
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public CancellationToken RequestCancellationToken { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCancellationToken = cancellationToken;
            Payloads.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            Entered.TrySetResult();
            if (gateResponse)
                await Release.Task.WaitAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    {"choices":[{"message":{"role":"assistant","content":"Captured queued summary"},"finish_reason":"stop"}],
                    "usage":{"prompt_tokens":100,"completion_tokens":10,"total_tokens":110}}
                    """, Encoding.UTF8, "application/json"),
            };
        }
    }

    private sealed class MemoryStore(CopilotChatState state, string attachmentDirectory) : ICopilotChatStateStore
    {
        public string AttachmentDirectoryPath => attachmentDirectory;
        public CopilotChatState Load() => state;
        public void Save(CopilotChatState value) { }
        public CopilotChatStateSnapshot CaptureSnapshot(CopilotChatState value) => new(new JObject());
        public string Serialize(CopilotChatStateSnapshot snapshot) => "{}";
        public string Serialize(CopilotChatState value) => "{}";
        public Task SaveSerializedAsync(string serializedState, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public int CleanupOrphanedAttachments(CopilotChatState value) => 0;
    }

    private sealed class GatedRuntime : ICopilotTurnRuntime
    {
        public TaskCompletionSource<CopilotTurnRequest> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async IAsyncEnumerable<CopilotTurnEvent> RunAsync(CopilotTurnRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            Entered.TrySetResult(request);
            await Release.Task.WaitAsync(cancellationToken);
            yield return new CopilotTurnStartedEvent("queued-snapshot-test", request.Mode);
            throw new InvalidOperationException("Expected queued snapshot test completion.");
        }

        public CopilotSteeringAdmissionResult EnqueueSteeringMessage(string taskId, string message) => new(CopilotSteeringAdmissionReason.RuntimeUnavailable);
        public bool TryEnqueueBackgroundShellCommandCompletion(CopilotBackgroundShellCommandSnapshot snapshot) => false;
        public bool TryEnqueueBackgroundShellCommandOutput(CopilotBackgroundShellOutputMonitorEventArgs eventArgs) => false;
        public bool TryAnswerUserQuestion(string taskId, string requestId, string answer) => false;
        public Task<CopilotWorkspaceRollbackActionResult> RequestWorkspaceRollbackAsync(CopilotWorkspaceRollbackActionRequest request, Action<CopilotAgentEvent> onEvent, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
