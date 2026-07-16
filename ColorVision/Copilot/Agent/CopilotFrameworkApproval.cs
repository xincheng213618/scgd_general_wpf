using ColorVision.Copilot.Mcp;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal enum CopilotFrameworkApprovalDecisionKind
    {
        Approved,
        Rejected,
        Expired,
        Cancelled,
        PolicyDenied,
    }

    internal sealed record CopilotFrameworkApprovalDecision
    {
        private CopilotFrameworkApprovalDecision(CopilotFrameworkApprovalDecisionKind kind, string reason)
        {
            Kind = kind;
            Reason = reason;
        }

        public CopilotFrameworkApprovalDecisionKind Kind { get; }

        public string Reason { get; }

        public bool IsApproved => Kind == CopilotFrameworkApprovalDecisionKind.Approved;

        public string FormatStatus(string toolName)
        {
            var name = string.IsNullOrWhiteSpace(toolName) ? "The protected tool" : toolName.Trim();
            return Kind switch
            {
                CopilotFrameworkApprovalDecisionKind.Approved => $"{name} was approved. Agent Framework is resuming the same session.",
                CopilotFrameworkApprovalDecisionKind.Rejected => $"{name} was rejected by the ColorVision user. Agent Framework will continue without executing it.",
                CopilotFrameworkApprovalDecisionKind.Expired => $"{name} approval expired. Agent Framework will continue without executing it.",
                CopilotFrameworkApprovalDecisionKind.Cancelled => $"{name} approval was cancelled. Agent Framework will continue without executing it.",
                _ => $"{name} was denied by ColorVision policy. Agent Framework will continue without executing it.",
            };
        }

        public string FormatToolSummary(string toolName)
        {
            var name = string.IsNullOrWhiteSpace(toolName) ? "The protected tool" : toolName.Trim();
            return Kind switch
            {
                CopilotFrameworkApprovalDecisionKind.Rejected => $"{name} was rejected by the user.",
                CopilotFrameworkApprovalDecisionKind.Expired => $"{name} approval expired.",
                CopilotFrameworkApprovalDecisionKind.Cancelled => $"{name} approval was cancelled.",
                CopilotFrameworkApprovalDecisionKind.PolicyDenied => $"{name} was denied by policy.",
                _ => $"{name} was approved.",
            };
        }

        public static CopilotFrameworkApprovalDecision FromStatus(ConfirmableActionStatus status)
        {
            return status switch
            {
                ConfirmableActionStatus.Approved => new(CopilotFrameworkApprovalDecisionKind.Approved, "Approved in ColorVision."),
                ConfirmableActionStatus.Rejected => new(CopilotFrameworkApprovalDecisionKind.Rejected, "Rejected by the ColorVision user."),
                ConfirmableActionStatus.Expired => new(CopilotFrameworkApprovalDecisionKind.Expired, "The ColorVision approval expired before a decision."),
                ConfirmableActionStatus.Cancelled => new(CopilotFrameworkApprovalDecisionKind.Cancelled, "The ColorVision approval was cancelled before execution."),
                _ => throw new ArgumentOutOfRangeException(nameof(status), status, "The confirmation action has no terminal approval decision."),
            };
        }

        public static CopilotFrameworkApprovalDecision PolicyDenied(string reason)
        {
            var detail = string.IsNullOrWhiteSpace(reason) ? "The protected tool call did not satisfy the approval policy." : reason.Trim();
            return new CopilotFrameworkApprovalDecision(
                CopilotFrameworkApprovalDecisionKind.PolicyDenied,
                "ColorVision policy denied this protected tool call: " + detail);
        }
    }

    internal sealed class CopilotFrameworkApprovalHandle
    {
        public ConfirmableAction Action { get; init; } = null!;

        public Task<CopilotFrameworkApprovalDecision> Decision { get; init; } =
            Task.FromResult(CopilotFrameworkApprovalDecision.PolicyDenied("The approval handle was not initialized."));
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
            CopilotAgentRequest request,
            CopilotAgentToolInput input,
            string callId,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(tool);
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();

            var completion = new TaskCompletionSource<CopilotFrameworkApprovalDecision>(TaskCreationOptions.RunContinuationsAsynchronously);
            var argumentsSummary = CopilotToolApprovalArgumentFormatter.Create(input);
            var presentation = tool switch
            {
                ICopilotFrameworkContextualApprovalPresentation contextualPresenter => contextualPresenter.CreateApprovalPresentation(request, input),
                ICopilotFrameworkApprovalPresentation presenter => presenter.CreateApprovalPresentation(input),
                _ => new CopilotToolApprovalPresentation(
                    $"Approve {tool.Name}",
                    $"Microsoft Agent Framework wants to run the protected ColorVision tool {tool.Name} with {argumentsSummary}."),
            };
            ConfirmableAction? action = null;
            EventHandler<ConfirmableActionChangedEventArgs>? statusChanged = null;
            statusChanged = (_, eventArgs) =>
            {
                if (!ReferenceEquals(eventArgs.Action, action))
                    return;

                switch (eventArgs.Action.Status)
                {
                    case ConfirmableActionStatus.Approved:
                    case ConfirmableActionStatus.Rejected:
                    case ConfirmableActionStatus.Expired:
                    case ConfirmableActionStatus.Cancelled:
                        completion.TrySetResult(CopilotFrameworkApprovalDecision.FromStatus(eventArgs.Action.Status));
                        break;
                }
            };
            _confirmationStore.ActionStatusChanged += statusChanged;

            var cancellationRegistration = cancellationToken.Register(() =>
            {
                var currentAction = action;
                if (currentAction?.Status == ConfirmableActionStatus.Pending)
                    _confirmationStore.Cancel(currentAction.ActionId, out _, "The approval request was cancelled with the Agent run.");
                completion.TrySetCanceled(cancellationToken);
            });

            try
            {
                action = _confirmationStore.CreateAgentFrameworkApproval(
                    presentation.Title,
                    presentation.Description,
                    tool.Name,
                    argumentsSummary,
                    callId,
                    createdAction => action = createdAction);

                if (cancellationToken.IsCancellationRequested)
                {
                    _confirmationStore.Cancel(action.ActionId, out _, "The approval request was cancelled with the Agent run.");
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
                _confirmationStore.Cancel(handle.Action.ActionId, out _, "The approval request was cancelled with the Agent run.");
        }

        public void Cancel(string actionId, string reason) => _confirmationStore.Cancel(actionId, out _, reason);

        public bool Begin(string actionId) => _confirmationStore.BeginAgentFrameworkAction(actionId);

        public void Complete(string actionId, CopilotToolResult result) => _confirmationStore.CompleteAgentFrameworkAction(actionId, result);

        private async Task<CopilotFrameworkApprovalDecision> AwaitDecisionAsync(
            ConfirmableAction action,
            Task<CopilotFrameworkApprovalDecision> decision,
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
