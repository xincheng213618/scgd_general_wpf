using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot.Mcp
{


    internal sealed partial class CopilotMcpConfirmationStore
    {
        public const int MaximumActiveActions = 64;
        public const int MaximumRetainedActions = 256;
        public const int MaximumReviewDetailsCharacters = 131_072;

        private static readonly Lazy<CopilotMcpConfirmationStore> LazyInstance = new(() => new CopilotMcpConfirmationStore());
        private static readonly TimeSpan DefaultLifetime = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan MaximumLifetime = TimeSpan.FromMinutes(30);
        private readonly object _syncRoot = new();
        private readonly List<ConfirmableAction> _actions = new();
        private TimeSpan _actionLifetime = DefaultLifetime;

        private CopilotMcpConfirmationStore()
        {
        }

        public static CopilotMcpConfirmationStore Instance => LazyInstance.Value;

        public event EventHandler? ActionsChanged;

        public event EventHandler<ConfirmableActionChangedEventArgs>? ActionStatusChanged;

        public TimeSpan ActionLifetime
        {
            get
            {
                lock (_syncRoot)
                    return _actionLifetime;
            }
            set
            {
                if (value <= TimeSpan.Zero || value > MaximumLifetime)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(value),
                        $"Confirmation action lifetime must be greater than zero and no longer than {MaximumLifetime.TotalMinutes:0} minutes.");
                }

                lock (_syncRoot)
                    _actionLifetime = value;
            }
        }

        public int PendingCount => GetPendingActions().Count;

        public ConfirmableAction Create(
            string title,
            string description,
            string riskLevel,
            string toolName,
            string argumentsSummary,
            Func<CancellationToken, Task<CopilotMcpToolCallResult>> executor,
            bool executeOnApproval = false,
            bool resumesAgentOnApproval = false,
            CopilotConfirmationRequestContext? requestContext = null,
            string? exactArgumentsBinding = null,
            string? reviewDetails = null,
            string? agentCallId = null)
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
                agentCallId ?? string.Empty,
                requestContext,
                exactArgumentsBinding,
                null,
                reviewDetails);
        }

        internal ConfirmableAction CreateAgentFrameworkApproval(
            string title,
            string description,
            string toolName,
            string argumentsSummary,
            string exactArgumentsBinding,
            string agentCallId,
            CopilotConfirmationRequestContext requestContext,
            Action<ConfirmableAction> beforePublish,
            string? reviewDetails = null)
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
                requestContext,
                exactArgumentsBinding,
                beforePublish,
                reviewDetails);
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
            CopilotConfirmationRequestContext? requestContext,
            string? exactArgumentsBinding,
            Action<ConfirmableAction>? beforePublish,
            string? reviewDetails)
        {
            ArgumentNullException.ThrowIfNull(executor);
            var normalizedReviewDetails = NormalizeReviewDetails(reviewDetails);

            ExpireStaleActions();
            var now = DateTimeOffset.UtcNow;
            var lifetime = ActionLifetime;
            var action = new ConfirmableAction
            {
                ActionId = CreateActionId(),
                Title = Sanitize(title),
                Description = Sanitize(description),
                RiskLevel = Sanitize(riskLevel),
                ToolName = Sanitize(toolName),
                ArgumentsSummary = Sanitize(argumentsSummary),
                ArgumentsDigest = ComputeArgumentsDigest(exactArgumentsBinding ?? argumentsSummary),
                ReviewDetails = normalizedReviewDetails,
                ExecuteOnApproval = executeOnApproval,
                ResumesAgentOnApproval = resumesAgentOnApproval,
                AgentCallId = Sanitize(agentCallId),
                RequestContext = NormalizeRequestContext(requestContext),
                CreatedAt = now,
                ExpiresAt = now.Add(lifetime),
                Executor = executor,
            };

            beforePublish?.Invoke(action);

            lock (_syncRoot)
            {
                PruneTerminalActionsNoLock();
                if (_actions.Count(IsActive) >= MaximumActiveActions)
                {
                    throw new InvalidOperationException(
                        $"ColorVision Copilot already has {MaximumActiveActions} active confirmation actions. Resolve or cancel an existing action before creating another.");
                }
                _actions.Add(action);
            }

            CopilotMcpAuditLogger.ActionCreated(action);
            RaiseActionStatusChanged(action);
            RaiseActionsChanged();
            return action;
        }

        public bool LinkAgentCall(
            string actionId,
            string callId,
            CopilotAgentRequest request,
            CopilotExecutionScope? executionScope = null)
        {
            if (string.IsNullOrWhiteSpace(callId) || request == null)
                return false;

            var action = Find(actionId);
            if (action == null)
                return false;

            executionScope ??= CopilotExecutionScope.ForAgentRequest(request);
            CopilotConfirmationRequestContext? updatedContext = null;
            lock (_syncRoot)
            {
                var requestContext = action.RequestContext;
                var actionScope = requestContext.ResolveExecutionScope();
                if (requestContext.SourceKind != CopilotApprovalSourceKind.InAppAgent
                    || !string.Equals(
                        requestContext.RequestSource,
                        CopilotMcpToolDispatcher.InAppAgentCallerSource,
                        StringComparison.OrdinalIgnoreCase)
                    || (!string.IsNullOrWhiteSpace(requestContext.ConversationId)
                        && !string.Equals(requestContext.ConversationId, request.ConversationId, StringComparison.Ordinal))
                    || (!string.IsNullOrWhiteSpace(requestContext.TaskId)
                        && !string.Equals(requestContext.TaskId, request.TaskId, StringComparison.Ordinal)))
                {
                    return false;
                }
                if (!actionScope.MatchesAuthorizationScope(executionScope)
                    || (actionScope.HasToolCallBinding
                        && !actionScope.MatchesOperationBinding(executionScope)))
                {
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(action.AgentCallId)
                    && !string.Equals(action.AgentCallId, callId.Trim(), StringComparison.Ordinal))
                {
                    return false;
                }

                action.AgentCallId = callId.Trim();
                updatedContext = action.RequestContext.MergeAgentScope(
                    request,
                    CopilotMcpToolDispatcher.InAppAgentCallerSource,
                    executionScope);
            }

            if (updatedContext != null)
                action.UpdateRequestContext(updatedContext);
            RaiseActionStatusChanged(action);
            RaiseActionsChanged();
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

        public bool Approve(
            string actionId,
            CopilotConfirmationReviewContext reviewContext,
            out string message) =>
            ApproveCore(
                actionId,
                reviewContext,
                decisionSource: "user",
                decisionReason: string.Empty,
                out message);

        internal bool ApproveAutomatically(
            string actionId,
            CopilotConfirmationReviewContext reviewContext,
            string decisionReason,
            out string message) =>
            ApproveCore(
                actionId,
                reviewContext,
                decisionSource: "automatic-review",
                decisionReason,
                out message);

        private bool ApproveCore(
            string actionId,
            CopilotConfirmationReviewContext reviewContext,
            string decisionSource,
            string decisionReason,
            out string message)
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
                if (!ValidateReviewContextNoLock(action, reviewContext, out message))
                    return false;

                if (action.Status != ConfirmableActionStatus.Pending)
                {
                    message = $"The action is {action.StatusLabel}.";
                    return false;
                }

                action.ApprovalDecisionSource = Sanitize(decisionSource);
                action.ApprovalDecisionReason = Sanitize(decisionReason);
                action.Status = ConfirmableActionStatus.Approved;
            }

            CopilotMcpAuditLogger.ActionApproved(action);
            RaiseActionStatusChanged(action);
            RaiseActionsChanged();
            message = string.Equals(
                action.ApprovalDecisionSource,
                "automatic-review",
                StringComparison.Ordinal)
                ? "The action was approved by the automatic permission reviewer."
                : "The action was approved.";
            return true;
        }

        public bool Reject(
            string actionId,
            CopilotConfirmationReviewContext reviewContext,
            out string message)
        {
            var action = Find(actionId);
            if (action == null)
            {
                message = "The action id was not found.";
                return false;
            }

            lock (_syncRoot)
            {
                if (!ValidateReviewContextNoLock(action, reviewContext, out message))
                    return false;

                if (action.Status != ConfirmableActionStatus.Pending && action.Status != ConfirmableActionStatus.Approved)
                {
                    message = $"The action is {action.StatusLabel}.";
                    return false;
                }

                action.Status = ConfirmableActionStatus.Rejected;
                action.CompletedAt = DateTimeOffset.UtcNow;
                action.ReleaseExecutor();
            }

            CopilotMcpAuditLogger.ActionRejected(action);
            RaiseActionStatusChanged(action);
            RaiseActionsChanged();
            message = "The action was rejected.";
            return true;
        }

        public IReadOnlyList<ConfirmableAction> GetPendingActionsForConversation(string? conversationId)
        {
            return GetPendingActions()
                .Where(action => action.CanReviewFromConversation(conversationId))
                .ToArray();
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
                action.ReleaseExecutor();
            }

            CopilotMcpAuditLogger.ActionCancelled(action);
            RaiseActionStatusChanged(action);
            RaiseActionsChanged();
            message = "The action was cancelled.";
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
                            || action.Status == ConfirmableActionStatus.Approved)
                        && action.ExpiresAt <= now)
                    .ToList();

                foreach (var action in expired)
                {
                    action.Status = ConfirmableActionStatus.Expired;
                    action.CompletedAt = now;
                    action.ReleaseExecutor();
                }
            }

            foreach (var action in expired)
                PublishExpired(action, raiseActionsChanged: false);

            if (expired.Count > 0)
                RaiseActionsChanged();
        }

        public void ClearForTests()
        {
            lock (_syncRoot)
            {
                foreach (var action in _actions)
                    action.ClearReviewDetails();
                _actions.Clear();
                _actionLifetime = DefaultLifetime;
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
            var changed = false;
            lock (_syncRoot)
            {
                if (action.Status == ConfirmableActionStatus.Expired)
                    return true;
                if ((action.Status == ConfirmableActionStatus.Pending || action.Status == ConfirmableActionStatus.Approved)
                    && action.ExpiresAt <= DateTimeOffset.UtcNow)
                {
                    action.Status = ConfirmableActionStatus.Expired;
                    action.CompletedAt = DateTimeOffset.UtcNow;
                    action.ReleaseExecutor();
                    changed = true;
                }
            }

            if (!changed)
                return false;

            PublishExpired(action);
            return true;
        }

        private void PruneTerminalActionsNoLock()
        {
            var removeCount = Math.Max(0, _actions.Count - MaximumRetainedActions + 1);
            if (removeCount == 0)
                return;

            var removable = _actions
                .Where(action => !IsActive(action))
                .OrderBy(action => action.CompletedAt ?? action.ExpiresAt)
                .ThenBy(action => action.CreatedAt)
                .Take(removeCount)
                .ToArray();
            foreach (var action in removable)
                _actions.Remove(action);
        }

        private static bool IsActive(ConfirmableAction action)
        {
            return action.Status is ConfirmableActionStatus.Pending
                or ConfirmableActionStatus.Approved
                or ConfirmableActionStatus.Executing;
        }

        private void PublishExpired(ConfirmableAction action, bool raiseActionsChanged = true)
        {
            CopilotMcpAuditLogger.ActionExpired(action);
            RaiseActionStatusChanged(action);
            if (raiseActionsChanged)
                RaiseActionsChanged();
        }

        private static string CreateActionId()
        {
            Span<byte> bytes = stackalloc byte[6];
            RandomNumberGenerator.Fill(bytes);
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        private static string ComputeArgumentsDigest(string? exactArgumentsBinding)
        {
            var bytes = Encoding.UTF8.GetBytes(exactArgumentsBinding ?? string.Empty);
            return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        }

        private static bool ArgumentsDigestsMatch(string expectedDigest, string? suppliedDigest)
        {
            var normalized = (suppliedDigest ?? string.Empty).Trim().ToLowerInvariant();
            if (expectedDigest.Length != 64
                || normalized.Length != 64
                || normalized.Any(character => !Uri.IsHexDigit(character)))
            {
                return false;
            }

            return CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(expectedDigest),
                Encoding.ASCII.GetBytes(normalized));
        }

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
