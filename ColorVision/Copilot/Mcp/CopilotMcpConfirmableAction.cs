using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot.Mcp
{
    public enum ConfirmableActionStatus
    {
        Pending,
        Approved,
        Rejected,
        Expired,
        Cancelled,
        Executing,
        Executed,
    }

    public sealed class ConfirmableAction : INotifyPropertyChanged
    {
        private static readonly JsonSerializerOptions ConfirmActionPayloadJsonOptions = new() { WriteIndented = true };
        private static readonly TimeSpan ExpiringSoonThreshold = TimeSpan.FromSeconds(60);
        private ConfirmableActionStatus _status = ConfirmableActionStatus.Pending;

        public string ActionId { get; init; } = string.Empty;

        public string Title { get; init; } = string.Empty;

        public string Description { get; init; } = string.Empty;

        public string RiskLevel { get; init; } = string.Empty;

        public string ToolName { get; init; } = string.Empty;

        public string ArgumentsSummary { get; init; } = string.Empty;

        public bool ExecuteOnApproval { get; init; }

        public bool ResumesAgentOnApproval { get; init; }

        public string AgentCallId { get; internal set; } = string.Empty;

        public bool? ExecutionSucceeded { get; internal set; }

        public string ExecutionResultText { get; internal set; } = string.Empty;

        public DateTimeOffset? CompletedAt { get; internal set; }

        public DateTimeOffset CreatedAt { get; init; }

        public DateTimeOffset ExpiresAt { get; init; }

        public ConfirmableActionStatus Status
        {
            get => _status;
            internal set
            {
                if (_status == value)
                    return;

                _status = value;
                OnPropertyChanged(nameof(Status));
                OnPropertyChanged(nameof(StatusLabel));
                OnPropertyChanged(nameof(IsPending));
                OnPropertyChanged(nameof(IsExpiringSoon));
                OnPropertyChanged(nameof(RemainingLifetimeLabel));
                OnPropertyChanged(nameof(ReviewDeadlineLabel));
            }
        }

        public string StatusLabel => Status.ToString();

        public bool IsPending => Status == ConfirmableActionStatus.Pending;

        public string CreatedAtLabel => CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

        public string ExpiresAtLabel => ExpiresAt.ToLocalTime().ToString("HH:mm:ss");

        public bool IsExpiringSoon
        {
            get
            {
                var remaining = ExpiresAt - DateTimeOffset.UtcNow;
                return IsPending && remaining > TimeSpan.Zero && remaining <= ExpiringSoonThreshold;
            }
        }

        public string RemainingLifetimeLabel
        {
            get
            {
                var remaining = ExpiresAt - DateTimeOffset.UtcNow;
                if (remaining <= TimeSpan.Zero)
                    return "expired";

                if (remaining.TotalSeconds < 60)
                    return $"{Math.Max(1, (int)Math.Ceiling(remaining.TotalSeconds))}s left";

                if (remaining.TotalMinutes < 60)
                    return $"{Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes))}m left";

                return $"{Math.Max(1, (int)Math.Ceiling(remaining.TotalHours))}h left";
            }
        }

        public string ReviewDeadlineLabel => $"{RemainingLifetimeLabel} · expires {ExpiresAtLabel}";

        public string ConfirmActionPayloadJson => JsonSerializer.Serialize(new
        {
            action_id = ActionId,
            tool_name = ToolName,
            arguments_summary = ArgumentsSummary,
        }, ConfirmActionPayloadJsonOptions);

        internal Func<CancellationToken, Task<CopilotMcpToolCallResult>> Executor { get; init; } = _ => Task.FromResult(CopilotMcpToolCallResult.Fail("action_executor_missing", "No executor is attached to this action."));

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public sealed class ConfirmableActionChangedEventArgs : EventArgs
    {
        public ConfirmableActionChangedEventArgs(ConfirmableAction action)
        {
            Action = action ?? throw new ArgumentNullException(nameof(action));
        }

        public ConfirmableAction Action { get; }
    }

    public sealed class CopilotMcpConfirmationStore
    {
        private static readonly Lazy<CopilotMcpConfirmationStore> LazyInstance = new(() => new CopilotMcpConfirmationStore());
        private static readonly TimeSpan DefaultLifetime = TimeSpan.FromMinutes(5);
        private readonly object _syncRoot = new();
        private readonly List<ConfirmableAction> _actions = new();

        private CopilotMcpConfirmationStore()
        {
        }

        public static CopilotMcpConfirmationStore Instance => LazyInstance.Value;

        public event EventHandler? ActionsChanged;

        public event EventHandler<ConfirmableActionChangedEventArgs>? ActionStatusChanged;

        public TimeSpan ActionLifetime { get; set; } = DefaultLifetime;

        public int PendingCount => GetPendingActions().Count;

        public ConfirmableAction Create(
            string title,
            string description,
            string riskLevel,
            string toolName,
            string argumentsSummary,
            Func<CancellationToken, Task<CopilotMcpToolCallResult>> executor,
            bool executeOnApproval = false,
            bool resumesAgentOnApproval = false)
        {
            return CreateCore(
                title,
                description,
                riskLevel,
                toolName,
                argumentsSummary,
                executor,
                executeOnApproval,
                resumesAgentOnApproval,
                string.Empty,
                null);
        }

        internal ConfirmableAction CreateAgentFrameworkApproval(
            string title,
            string description,
            string toolName,
            string argumentsSummary,
            string agentCallId,
            Action<ConfirmableAction> beforePublish)
        {
            return CreateCore(
                title,
                description,
                "confirmation-required",
                toolName,
                argumentsSummary,
                _ => Task.FromResult(CopilotMcpToolCallResult.Fail("framework_approval_only", "This action resumes Microsoft Agent Framework and is not executed directly by the confirmation store.")),
                executeOnApproval: false,
                resumesAgentOnApproval: true,
                agentCallId,
                beforePublish);
        }

        private ConfirmableAction CreateCore(
            string title,
            string description,
            string riskLevel,
            string toolName,
            string argumentsSummary,
            Func<CancellationToken, Task<CopilotMcpToolCallResult>> executor,
            bool executeOnApproval,
            bool resumesAgentOnApproval,
            string agentCallId,
            Action<ConfirmableAction>? beforePublish)
        {
            ArgumentNullException.ThrowIfNull(executor);

            var now = DateTimeOffset.UtcNow;
            var action = new ConfirmableAction
            {
                ActionId = CreateActionId(),
                Title = Sanitize(title),
                Description = Sanitize(description),
                RiskLevel = Sanitize(riskLevel),
                ToolName = Sanitize(toolName),
                ArgumentsSummary = Sanitize(argumentsSummary),
                ExecuteOnApproval = executeOnApproval,
                ResumesAgentOnApproval = resumesAgentOnApproval,
                AgentCallId = Sanitize(agentCallId),
                CreatedAt = now,
                ExpiresAt = now.Add(ActionLifetime),
                Executor = executor,
            };

            beforePublish?.Invoke(action);

            lock (_syncRoot)
            {
                _actions.Add(action);
            }

            CopilotMcpAuditLogger.ActionCreated(action);
            RaiseActionStatusChanged(action);
            RaiseActionsChanged();
            return action;
        }

        public bool LinkAgentCall(string actionId, string callId)
        {
            if (string.IsNullOrWhiteSpace(callId))
                return false;

            var action = Find(actionId);
            if (action == null)
                return false;

            lock (_syncRoot)
            {
                if (!string.IsNullOrWhiteSpace(action.AgentCallId)
                    && !string.Equals(action.AgentCallId, callId.Trim(), StringComparison.Ordinal))
                {
                    return false;
                }

                action.AgentCallId = callId.Trim();
            }

            RaiseActionStatusChanged(action);
            return true;
        }

        public IReadOnlyList<ConfirmableAction> GetPendingActions()
        {
            ExpireStaleActions();
            lock (_syncRoot)
            {
                return _actions
                    .Where(action => action.Status == ConfirmableActionStatus.Pending)
                    .OrderBy(action => action.ExpiresAt)
                    .ToArray();
            }
        }

        public bool Approve(string actionId, out string message)
        {
            var action = Find(actionId);
            if (action == null)
            {
                message = "The action id was not found.";
                return false;
            }

            if (ExpireIfNeeded(action))
            {
                message = "The action has expired.";
                return false;
            }

            lock (_syncRoot)
            {
                if (action.Status != ConfirmableActionStatus.Pending)
                {
                    message = $"The action is {action.StatusLabel}.";
                    return false;
                }

                action.Status = ConfirmableActionStatus.Approved;
            }

            CopilotMcpAuditLogger.ActionApproved(action);
            RaiseActionStatusChanged(action);
            RaiseActionsChanged();
            message = "The action was approved.";
            return true;
        }

        public bool Reject(string actionId, out string message)
        {
            var action = Find(actionId);
            if (action == null)
            {
                message = "The action id was not found.";
                return false;
            }

            lock (_syncRoot)
            {
                if (action.Status != ConfirmableActionStatus.Pending && action.Status != ConfirmableActionStatus.Approved)
                {
                    message = $"The action is {action.StatusLabel}.";
                    return false;
                }

                action.Status = ConfirmableActionStatus.Rejected;
            }

            CopilotMcpAuditLogger.ActionRejected(action);
            RaiseActionStatusChanged(action);
            RaiseActionsChanged();
            message = "The action was rejected.";
            return true;
        }

        public bool Cancel(string actionId, out string message, string? resultText = null)
        {
            var action = Find(actionId);
            if (action == null)
            {
                message = "The action id was not found.";
                return false;
            }

            lock (_syncRoot)
            {
                var canCancelFrameworkExecution = action.ResumesAgentOnApproval && action.Status == ConfirmableActionStatus.Executing;
                if (action.Status != ConfirmableActionStatus.Pending
                    && action.Status != ConfirmableActionStatus.Approved
                    && !canCancelFrameworkExecution)
                {
                    message = $"The action is {action.StatusLabel}.";
                    return false;
                }

                action.ExecutionSucceeded = false;
                action.ExecutionResultText = Sanitize(string.IsNullOrWhiteSpace(resultText)
                    ? "The action was cancelled before execution completed."
                    : resultText);
                action.CompletedAt = DateTimeOffset.UtcNow;
                action.Status = ConfirmableActionStatus.Cancelled;
            }

            CopilotMcpAuditLogger.ActionCancelled(action);
            RaiseActionStatusChanged(action);
            RaiseActionsChanged();
            message = "The action was cancelled.";
            return true;
        }

        public async Task<CopilotMcpToolCallResult> ExecuteApprovedAsync(string actionId, string toolName, string argumentsSummary, CancellationToken cancellationToken)
        {
            var action = Find(actionId);
            if (action == null)
                return CopilotMcpToolCallResult.Fail("action_not_found", $"No confirmable action exists for action_id={actionId}.");

            if (ExpireIfNeeded(action))
                return CopilotMcpToolCallResult.Fail("action_expired", $"The action has expired: action_id={action.ActionId}; expires_at={action.ExpiresAt:O}.");

            Func<CancellationToken, Task<CopilotMcpToolCallResult>> executor;
            lock (_syncRoot)
            {
                if (!string.Equals(action.ToolName, toolName, StringComparison.OrdinalIgnoreCase))
                    return CopilotMcpToolCallResult.Fail("action_tool_mismatch", $"The action was created for tool_name={action.ToolName}, not {toolName}.");

                if (!string.Equals(action.ArgumentsSummary, Sanitize(argumentsSummary), StringComparison.Ordinal))
                    return CopilotMcpToolCallResult.Fail("action_arguments_mismatch", "The confirmation arguments do not match the pending action arguments_summary.");

                if (!string.Equals(action.RiskLevel, "confirmation-required", StringComparison.OrdinalIgnoreCase))
                    return CopilotMcpToolCallResult.Fail("action_invalid_risk", $"The action risk level is {action.RiskLevel}; confirm_action only executes confirmation-required actions.");

                if (action.Status == ConfirmableActionStatus.Pending)
                    return CopilotMcpToolCallResult.Fail("action_pending", $"The action is waiting for user approval in ColorVision: action_id={action.ActionId}.");

                if (action.Status == ConfirmableActionStatus.Rejected)
                    return CopilotMcpToolCallResult.Fail("action_rejected", $"The action was rejected in ColorVision: action_id={action.ActionId}.");

                if (action.Status == ConfirmableActionStatus.Cancelled)
                    return CopilotMcpToolCallResult.Fail("action_cancelled", $"The action was cancelled in ColorVision: action_id={action.ActionId}.");

                if (action.Status == ConfirmableActionStatus.Executed || action.Status == ConfirmableActionStatus.Executing)
                    return CopilotMcpToolCallResult.Fail("action_already_executed", $"The action has already been executed: action_id={action.ActionId}.");

                if (action.Status != ConfirmableActionStatus.Approved)
                    return CopilotMcpToolCallResult.Fail("action_not_approved", $"The action status is {action.StatusLabel}, not Approved.");

                action.Status = ConfirmableActionStatus.Executing;
                executor = action.Executor;
            }

            RaiseActionStatusChanged(action);
            RaiseActionsChanged();
            CopilotMcpToolCallResult result;
            try
            {
                result = await executor(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                lock (_syncRoot)
                {
                    action.ExecutionSucceeded = false;
                    action.ExecutionResultText = "The approved action execution was cancelled before completion.";
                    action.CompletedAt = DateTimeOffset.UtcNow;
                    action.Status = ConfirmableActionStatus.Cancelled;
                }

                CopilotMcpAuditLogger.ActionCancelled(action);
                RaiseActionStatusChanged(action);
                RaiseActionsChanged();
                throw;
            }
            catch (Exception ex)
            {
                result = CopilotMcpToolCallResult.Fail("action_execution_failed", $"The approved action failed: {CopilotMcpAuditLogger.RedactText(ex.Message)}");
            }

            lock (_syncRoot)
            {
                action.ExecutionSucceeded = result.Success;
                action.ExecutionResultText = Sanitize(result.Text);
                action.CompletedAt = DateTimeOffset.UtcNow;
                action.Status = ConfirmableActionStatus.Executed;
            }

            CopilotMcpAuditLogger.ActionExecuted(action, result.Success, result.Success ? "OK" : result.Text);
            RaiseActionStatusChanged(action);
            RaiseActionsChanged();
            return result;
        }

        public async Task<CopilotMcpToolCallResult> ApproveAndExecuteAsync(string actionId, CancellationToken cancellationToken)
        {
            var action = Find(actionId);
            if (action == null)
                return CopilotMcpToolCallResult.Fail("action_not_found", $"No confirmable action exists for action_id={actionId}.");

            if (!action.ExecuteOnApproval)
                return CopilotMcpToolCallResult.Fail("action_requires_client_confirmation", "This action requires the MCP client to call confirm_action after user approval.");

            if (!Approve(actionId, out var approvalMessage))
                return CopilotMcpToolCallResult.Fail("action_approval_failed", approvalMessage);

            return await ExecuteApprovedAsync(action.ActionId, action.ToolName, action.ArgumentsSummary, cancellationToken);
        }

        internal bool BeginAgentFrameworkAction(string actionId)
        {
            var action = Find(actionId);
            if (action == null)
                return false;

            lock (_syncRoot)
            {
                if (!action.ResumesAgentOnApproval || action.Status != ConfirmableActionStatus.Approved)
                    return false;

                action.Status = ConfirmableActionStatus.Executing;
            }

            RaiseActionStatusChanged(action);
            RaiseActionsChanged();
            return true;
        }

        internal bool CompleteAgentFrameworkAction(string actionId, CopilotToolResult result)
        {
            ArgumentNullException.ThrowIfNull(result);
            var action = Find(actionId);
            if (action == null)
                return false;

            lock (_syncRoot)
            {
                if (!action.ResumesAgentOnApproval || action.Status != ConfirmableActionStatus.Executing)
                    return false;

                action.ExecutionSucceeded = result.Success;
                action.ExecutionResultText = Sanitize(result.Success
                    ? FirstNonEmpty(result.Summary, result.Content, "The approved Agent Framework action completed.")
                    : FirstNonEmpty(result.ErrorMessage, result.Summary, "The approved Agent Framework action failed."));
                action.CompletedAt = DateTimeOffset.UtcNow;
                action.Status = ConfirmableActionStatus.Executed;
            }

            CopilotMcpAuditLogger.ActionExecuted(action, result.Success, result.Success ? "OK" : action.ExecutionResultText);
            RaiseActionStatusChanged(action);
            RaiseActionsChanged();
            return true;
        }

        public void ExpireStaleActions()
        {
            List<ConfirmableAction> expired;
            lock (_syncRoot)
            {
                var now = DateTimeOffset.UtcNow;
                expired = _actions
                    .Where(action => (action.Status == ConfirmableActionStatus.Pending
                            || action.Status == ConfirmableActionStatus.Approved && !action.ResumesAgentOnApproval)
                        && action.ExpiresAt <= now)
                    .ToList();

                foreach (var action in expired)
                    action.Status = ConfirmableActionStatus.Expired;
            }

            foreach (var action in expired)
            {
                CopilotMcpAuditLogger.ActionExpired(action);
                RaiseActionStatusChanged(action);
            }

            if (expired.Count > 0)
                RaiseActionsChanged();
        }

        public void ClearForTests()
        {
            lock (_syncRoot)
            {
                _actions.Clear();
                ActionLifetime = DefaultLifetime;
            }

            RaiseActionsChanged();
        }

        private ConfirmableAction? Find(string actionId)
        {
            if (string.IsNullOrWhiteSpace(actionId))
                return null;

            lock (_syncRoot)
            {
                return _actions.FirstOrDefault(action => string.Equals(action.ActionId, actionId.Trim(), StringComparison.OrdinalIgnoreCase));
            }
        }

        private bool ExpireIfNeeded(ConfirmableAction action)
        {
            if (action.ExpiresAt > DateTimeOffset.UtcNow)
                return false;

            var changed = false;
            lock (_syncRoot)
            {
                if (action.Status == ConfirmableActionStatus.Pending || action.Status == ConfirmableActionStatus.Approved)
                {
                    action.Status = ConfirmableActionStatus.Expired;
                    changed = true;
                }
            }

            if (!changed)
                return action.Status == ConfirmableActionStatus.Expired;

            CopilotMcpAuditLogger.ActionExpired(action);
            RaiseActionStatusChanged(action);
            RaiseActionsChanged();
            return true;
        }

        private static string CreateActionId()
        {
            Span<byte> bytes = stackalloc byte[6];
            RandomNumberGenerator.Fill(bytes);
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        private static string Sanitize(string? value)
        {
            var text = CopilotMcpAuditLogger.RedactText(value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
            return text.Length <= 1000 ? text : text[..1000] + "...";
        }

        private static string FirstNonEmpty(params string?[] values) =>
            values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

        private void RaiseActionsChanged()
        {
            ActionsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void RaiseActionStatusChanged(ConfirmableAction action)
        {
            ActionStatusChanged?.Invoke(this, new ConfirmableActionChangedEventArgs(action));
        }
    }
}
