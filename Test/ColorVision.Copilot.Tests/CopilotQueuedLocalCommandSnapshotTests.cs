using ColorVision.Copilot;
using ColorVision.Solution;
using ColorVision.Solution.Explorer;
using ColorVision.Solution.Workspace;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

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

        var request = await fixture.DispatchAsync();

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

        var request = await fixture.DispatchAsync();

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

        var request = await fixture.DispatchAsync();

        fixture.AssertNewerDraftWasPreserved(request, consumesQueuedAttachment: false, retryAttachment: originalAttachment);
        fixture.AssertSubmissionSnapshot(queued, request);
        Assert.Equal(originalUser.RequestMode, request.Mode);
        Assert.Equal(originalUser.Content, request.UserText);
        Assert.Equal(expectedHistory.ModelMessages, request.HostContext.ConversationHistory.ModelMessages);
        Assert.Equal(expectedHistory.VisibleMessages, request.HostContext.ConversationHistory.VisibleMessages);
        Assert.Contains(originalUser, fixture.Conversation.Messages);
        Assert.Equal(4, fixture.Conversation.Messages.Count);
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

        public QueueFixture()
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
            ViewModel = new CopilotChatViewModel(new CopilotChatService(), new MemoryStore(state, _directory.FullName), Config, _runtime, Host);
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

        public async Task<CopilotTurnRequest> DispatchAsync()
        {
            _releaseActive.TrySetResult();
            return await _runtime.Entered.Task.WaitAsync(TestTimeout);
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
            Assert.Equal("newer draft", ViewModel.InputText);
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
