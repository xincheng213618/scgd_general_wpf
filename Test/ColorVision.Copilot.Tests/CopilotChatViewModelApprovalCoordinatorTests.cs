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
