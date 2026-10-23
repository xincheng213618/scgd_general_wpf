using ColorVision.Solution;
using ColorVision.Solution.Workspace;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotChatInitializationFailureTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StatePreparationFailureDoesNotRetainAnIncompleteViewModel(bool failCleanup)
    {
        StaTest.Run(() =>
        {
            using var fixture = new Fixture();
            var failure = new IOException("The conversation file is temporarily unavailable.");
            if (failCleanup)
                fixture.Store.CleanupFailure = failure;
            else
                fixture.Store.LoadFailure = failure;
            using var subscriptions = new SubscriptionSnapshot();

            var actual = Assert.Throws<IOException>(() => fixture.CreateViewModel());

            Assert.Same(failure, actual);
            Assert.Equal(0, fixture.Store.SaveCalls);
            subscriptions.AssertUnchanged();
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PanelReadFailureCanBeRetriedWithoutReplacingThePanelOrHistory(bool unauthorized)
    {
        StaTest.Run(() =>
        {
            using var fixture = new Fixture();
            var panel = new CopilotChatPanel();
            var originalContent = panel.Content;
            fixture.Store.LoadFailure = unauthorized
                ? new UnauthorizedAccessException("Reading the conversation file was denied.")
                : new IOException("The conversation file is temporarily unavailable.");
            using var subscriptions = new SubscriptionSnapshot();
            try
            {
                CopilotPanelService.InitializePanel(panel, fixture.CreateViewModel);

                Assert.Null(panel.DataContext);
                var failureContent = Assert.IsType<StackPanel>(panel.Content);
                Assert.False(string.IsNullOrWhiteSpace(Assert.IsType<TextBlock>(failureContent.Children[0]).Text));
                var retry = Assert.IsType<Button>(failureContent.Children[1]);
                retry.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Equal(2, fixture.Store.LoadCalls);
                Assert.Same(failureContent, panel.Content);
                Assert.Null(panel.DataContext);
                Assert.Equal(0, fixture.Store.SaveCalls);
                subscriptions.AssertUnchanged();

                fixture.Store.LoadFailure = null;
                retry.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

                Assert.Equal(3, fixture.Store.LoadCalls);
                var viewModel = Assert.IsType<CopilotChatViewModel>(panel.DataContext);
                Assert.Same(originalContent, panel.Content);
                Assert.Same(fixture.Conversation, viewModel.SelectedConversation);
                Assert.Equal("Keep the draft exactly.", viewModel.InputText);
                Assert.Equal("Existing question", viewModel.Messages[0].Content);
                Assert.Equal("Existing answer", viewModel.Messages[1].Content);
            }
            finally
            {
                var viewModel = panel.DataContext as CopilotChatViewModel;
                panel.DataContext = null;
                viewModel?.Dispose();
            }
        });
    }

    [Fact]
    public void PanelInitializationDoesNotHideUnrelatedFailures()
    {
        StaTest.Run(() =>
        {
            try
            {
                var panel = new CopilotChatPanel();
                var originalContent = panel.Content;
                var failure = new InvalidOperationException("Unrelated initialization failure.");

                Assert.Same(failure, Assert.Throws<InvalidOperationException>(() =>
                    CopilotPanelService.InitializePanel(panel, () => throw failure)));

                Assert.Same(originalContent, panel.Content);
                Assert.Null(panel.DataContext);
            }
            finally
            {
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
    }

    private sealed class Fixture : IDisposable
    {
        private static readonly FieldInfo SolutionInstance = typeof(SolutionManager).GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static)!;
        private readonly object? _previousSolution = SolutionInstance.GetValue(null);
        private readonly CopilotAgentTaskHost _host = new();
        private readonly CopilotConfig _config;

        public Fixture()
        {
            SolutionInstance.SetValue(null, RuntimeHelpers.GetUninitializedObject(typeof(SolutionManager)));
            var profile = new CopilotProfileConfig
            {
                Id = "initialization-profile", Name = "Initialization test", VendorType = CopilotVendorType.Custom,
                ProviderType = CopilotProviderType.OpenAICompatible, ApiKey = "test-key", BaseUrl = "https://unit.test/v1", Model = "test-model",
            };
            _config = new CopilotConfig { SchemaVersion = CopilotConfig.CurrentSchemaVersion, McpBearerToken = "test-token", Profiles = [profile] };
            Conversation = CopilotConversationRecord.CreateEmpty(profile.Id, profile.DisplayLabel);
            Conversation.DraftText = "Keep the draft exactly.";
            Conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "Existing question"));
            Conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "Existing answer"));
            Store = new StateStore(new CopilotChatState
            {
                ActiveConversationId = Conversation.Id, ActiveProfileId = profile.Id, Conversations = [Conversation],
            });
        }

        public CopilotConversationRecord Conversation { get; }
        public StateStore Store { get; }

        public CopilotChatViewModel CreateViewModel() => new(
            new CopilotChatService(), Store, _config, new UnusedRuntime(), _host);

        public void Dispose()
        {
            _host.Shutdown();
            SolutionInstance.SetValue(null, _previousSolution);
            Dispatcher.CurrentDispatcher.InvokeShutdown();
        }
    }

    private sealed class StateStore(CopilotChatState state) : ICopilotChatStateStore
    {
        public Exception? LoadFailure { get; set; }
        public Exception? CleanupFailure { get; set; }
        public int LoadCalls { get; private set; }
        public int SaveCalls { get; private set; }
        public string AttachmentDirectoryPath => string.Empty;

        public CopilotChatState Load()
        {
            LoadCalls++;
            if (LoadFailure != null)
                throw LoadFailure;
            return state;
        }

        public int CleanupOrphanedAttachments(CopilotChatState value)
        {
            if (CleanupFailure != null)
                throw CleanupFailure;
            return 0;
        }

        public void Save(CopilotChatState value) => SaveCalls++;
        public CopilotChatStateSnapshot CaptureSnapshot(CopilotChatState value) => new(new JObject());
        public string Serialize(CopilotChatStateSnapshot snapshot) => "{}";
        public string Serialize(CopilotChatState value) => "{}";
        public Task SaveSerializedAsync(string serializedState, CancellationToken cancellationToken = default)
        {
            SaveCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class SubscriptionSnapshot : IDisposable
    {
        private readonly (FieldInfo Field, Delegate? Value)[] _events =
        [
            Capture(typeof(WorkspaceManager), "ContentIdSelected"),
            Capture(typeof(CopilotLiveContextRegistry), "CurrentChanged"),
            Capture(typeof(CopilotAgentSkillCatalog), "CatalogChanged"),
        ];

        private static (FieldInfo, Delegate?) Capture(Type owner, string name)
        {
            var field = owner.GetField(name, BindingFlags.Static | BindingFlags.NonPublic)!;
            return (field, field.GetValue(null) as Delegate);
        }

        public void AssertUnchanged()
        {
            foreach (var (field, original) in _events)
                Assert.Equal(original, field.GetValue(null));
        }

        public void Dispose()
        {
            // Keep a regression failure from leaking an incomplete VM into later tests.
            foreach (var (field, original) in _events)
            {
                var originalHandlers = original?.GetInvocationList() ?? [];
                var addedHandlers = (field.GetValue(null) as Delegate)?.GetInvocationList().Except(originalHandlers) ?? [];
                foreach (var target in addedHandlers.Select(handler => handler.Target).OfType<CopilotChatViewModel>())
                {
                    var coordinator = typeof(CopilotChatViewModel).GetField("_statePersistenceCoordinator", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target) as IDisposable;
                    coordinator?.Dispose();
                    if (Application.Current != null)
                    {
                        var exit = typeof(CopilotChatViewModel).GetMethod("Application_Exit", BindingFlags.Instance | BindingFlags.NonPublic)!;
                        Application.Current.Exit -= (ExitEventHandler)exit.CreateDelegate(typeof(ExitEventHandler), target);
                    }
                }
                field.SetValue(null, original);
            }
        }
    }

    private sealed class UnusedRuntime : ICopilotTurnRuntime
    {
        public IAsyncEnumerable<CopilotTurnEvent> RunAsync(CopilotTurnRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public CopilotSteeringAdmissionResult EnqueueSteeringMessage(string taskId, string message) => new(CopilotSteeringAdmissionReason.RuntimeUnavailable);
        public bool TryEnqueueBackgroundShellCommandCompletion(CopilotBackgroundShellCommandSnapshot snapshot) => false;
        public bool TryEnqueueBackgroundShellCommandOutput(CopilotBackgroundShellOutputMonitorEventArgs eventArgs) => false;
        public bool TryAnswerUserQuestion(string taskId, string requestId, string answer) => false;
        public Task<CopilotWorkspaceRollbackActionResult> RequestWorkspaceRollbackAsync(CopilotWorkspaceRollbackActionRequest request, Action<CopilotAgentEvent> onEvent, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
