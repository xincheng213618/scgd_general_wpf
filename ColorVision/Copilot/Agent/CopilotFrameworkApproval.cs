using ColorVision.Copilot.Mcp;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal sealed class CopilotFrameworkApprovalHandle
    {
        public ConfirmableAction Action { get; init; } = null!;

        public Task<bool> Decision { get; init; } = Task.FromResult(false);
    }

    internal sealed class CopilotFrameworkApprovalCoordinator
    {
        private readonly CopilotMcpConfirmationStore _confirmationStore;

        public CopilotFrameworkApprovalCoordinator()
            : this(CopilotMcpConfirmationStore.Instance)
        {
        }

        internal CopilotFrameworkApprovalCoordinator(CopilotMcpConfirmationStore confirmationStore)
        {
            _confirmationStore = confirmationStore ?? throw new ArgumentNullException(nameof(confirmationStore));
        }

        public CopilotFrameworkApprovalHandle RequestApproval(
            ICopilotTool tool,
            CopilotAgentToolInput input,
            string callId,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(tool);
            cancellationToken.ThrowIfCancellationRequested();

            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var argumentsSummary = CopilotToolExecutionAuditLogger.CreateArgumentSummary(input);
            ConfirmableAction? action = null;
            EventHandler<ConfirmableActionChangedEventArgs>? statusChanged = null;
            statusChanged = (_, eventArgs) =>
            {
                if (!ReferenceEquals(eventArgs.Action, action))
                    return;

                switch (eventArgs.Action.Status)
                {
                    case ConfirmableActionStatus.Approved:
                        completion.TrySetResult(true);
                        break;
                    case ConfirmableActionStatus.Rejected:
                    case ConfirmableActionStatus.Expired:
                        completion.TrySetResult(false);
                        break;
                }
            };
            _confirmationStore.ActionStatusChanged += statusChanged;

            var cancellationRegistration = cancellationToken.Register(() =>
            {
                var currentAction = action;
                if (currentAction?.Status == ConfirmableActionStatus.Pending)
                    _confirmationStore.Reject(currentAction.ActionId, out _);
                completion.TrySetCanceled(cancellationToken);
            });

            try
            {
                action = _confirmationStore.CreateAgentFrameworkApproval(
                    $"Approve {tool.Name}",
                    $"Microsoft Agent Framework wants to run the protected ColorVision tool {tool.Name} with {argumentsSummary}.",
                    tool.Name,
                    argumentsSummary,
                    callId,
                    createdAction => action = createdAction);

                if (cancellationToken.IsCancellationRequested)
                {
                    _confirmationStore.Reject(action.ActionId, out _);
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }
            catch
            {
                cancellationRegistration.Dispose();
                _confirmationStore.ActionStatusChanged -= statusChanged;
                throw;
            }

            return new CopilotFrameworkApprovalHandle
            {
                Action = action,
                Decision = AwaitDecisionAsync(action, completion.Task, statusChanged, cancellationRegistration, cancellationToken),
            };
        }

        public void Cancel(CopilotFrameworkApprovalHandle handle)
        {
            ArgumentNullException.ThrowIfNull(handle);
            if (handle.Action.Status == ConfirmableActionStatus.Pending)
                _confirmationStore.Reject(handle.Action.ActionId, out _);
        }

        private async Task<bool> AwaitDecisionAsync(
            ConfirmableAction action,
            Task<bool> decision,
            EventHandler<ConfirmableActionChangedEventArgs> statusChanged,
            CancellationTokenRegistration cancellationRegistration,
            CancellationToken cancellationToken)
        {
            try
            {
                while (!decision.IsCompleted)
                {
                    var remaining = action.ExpiresAt - DateTimeOffset.UtcNow;
                    if (remaining > TimeSpan.Zero
                        && await Task.WhenAny(decision, Task.Delay(remaining, CancellationToken.None)) == decision)
                    {
                        break;
                    }

                    _confirmationStore.ExpireStaleActions();
                    if (!decision.IsCompleted)
                        await Task.Delay(TimeSpan.FromMilliseconds(1), cancellationToken);
                }
                return await decision.WaitAsync(cancellationToken);
            }
            finally
            {
                cancellationRegistration.Dispose();
                _confirmationStore.ActionStatusChanged -= statusChanged;
            }
        }
    }
}
