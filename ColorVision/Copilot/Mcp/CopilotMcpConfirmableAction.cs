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
    internal enum CopilotApprovalSourceKind
    {
        Unknown,
        InAppAgent,
        ExternalMcp,
        ColorVisionUi,
    }

    internal sealed class CopilotConfirmationRequestContext
    {
        public CopilotExecutionScope Scope { get; init; } = CopilotExecutionScope.Empty;

        public CopilotApprovalSourceKind SourceKind { get; init; }

        public string RequestSource { get; init; } = string.Empty;

        public string ConversationId { get; init; } = string.Empty;

        public string TaskId { get; init; } = string.Empty;

        public string TaskLabel { get; init; } = string.Empty;

        public string WorkspacePath { get; init; } = string.Empty;

        public string ImpactSummary { get; init; } = string.Empty;

        public CopilotApprovalReversibility Reversibility { get; init; }

        public string ReversibilitySummary { get; init; } = string.Empty;

        public string RequesterLabel => CopilotApprovalReviewTextEncoder.Encode(SourceKind switch
            {
                CopilotApprovalSourceKind.InAppAgent => "ColorVision Copilot 任务",
                CopilotApprovalSourceKind.ExternalMcp => string.IsNullOrWhiteSpace(RequestSource)
                    ? "外部 MCP 客户端"
                    : $"外部 MCP 客户端 · {RequestSource}",
                CopilotApprovalSourceKind.ColorVisionUi => "ColorVision 本地界面",
                _ => "来源未标记的本地操作",
            });

        public string TaskScopeLabel
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(TaskLabel))
                    return CopilotApprovalReviewTextEncoder.Encode(TaskLabel);
                if (!string.IsNullOrWhiteSpace(TaskId))
                    return CopilotApprovalReviewTextEncoder.Encode($"任务 {ShortId(TaskId)}");
                if (!string.IsNullOrWhiteSpace(ConversationId))
                    return CopilotApprovalReviewTextEncoder.Encode($"会话 {ShortId(ConversationId)}");
                return SourceKind == CopilotApprovalSourceKind.ExternalMcp
                    ? "外部 MCP 请求"
                    : "当前应用操作";
            }
        }

        public string WorkspaceLabel => string.IsNullOrWhiteSpace(WorkspacePath)
            ? "当前 ColorVision 应用"
            : CopilotApprovalReviewTextEncoder.Encode(WorkspacePath);

        public string ImpactLabel => string.IsNullOrWhiteSpace(ImpactSummary)
            ? "请根据操作说明和参数确认影响范围。"
            : CopilotApprovalReviewTextEncoder.Encode(ImpactSummary);

        public string ReversibilityLabel
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(ReversibilitySummary))
                    return CopilotApprovalReviewTextEncoder.Encode(ReversibilitySummary);
                return Reversibility switch
                {
                    CopilotApprovalReversibility.AutomaticUntilExpiry => "支持在有效期内自动撤销。",
                    CopilotApprovalReversibility.ManualOnly => "只能通过后续手动操作恢复。",
                    CopilotApprovalReversibility.NotReversible => "此操作无法由 Copilot 自动撤销。",
                    _ => "此工具未声明自动撤销能力；请在批准前核对影响。",
                };
            }
        }

        internal bool CanReviewFromConversation(string? conversationId)
        {
            if (SourceKind != CopilotApprovalSourceKind.InAppAgent)
                return true;

            return !string.IsNullOrWhiteSpace(ConversationId)
                && string.Equals(ConversationId, (conversationId ?? string.Empty).Trim(), StringComparison.Ordinal);
        }

        internal static CopilotConfirmationRequestContext ForAgent(
            CopilotAgentRequest request,
            CopilotToolApprovalPresentation? presentation = null,
            string requestSource = CopilotMcpToolDispatcher.InAppAgentCallerSource,
            CopilotExecutionScope? executionScope = null)
        {
            ArgumentNullException.ThrowIfNull(request);
            executionScope ??= CopilotExecutionScope.ForAgentRequest(request);
            return new CopilotConfirmationRequestContext
            {
                Scope = executionScope,
                SourceKind = CopilotApprovalSourceKind.InAppAgent,
                RequestSource = requestSource,
                ConversationId = request.ConversationId,
                TaskId = request.TaskId,
                TaskLabel = string.IsNullOrWhiteSpace(request.TaskIntentText)
                    ? request.UserText
                    : request.TaskIntentText,
                WorkspacePath = request.WorkspacePath,
                ImpactSummary = FirstNonEmpty(presentation?.ImpactSummary, presentation?.Description),
                Reversibility = presentation?.Reversibility ?? CopilotApprovalReversibility.Unknown,
                ReversibilitySummary = presentation?.ReversibilitySummary ?? string.Empty,
            };
        }

        internal CopilotConfirmationRequestContext MergeAgentScope(
            CopilotAgentRequest request,
            string requestSource,
            CopilotExecutionScope? executionScope = null)
        {
            var agent = ForAgent(request, requestSource: requestSource, executionScope: executionScope);
            return new CopilotConfirmationRequestContext
            {
                Scope = agent.Scope,
                SourceKind = CopilotApprovalSourceKind.InAppAgent,
                RequestSource = FirstNonEmpty(RequestSource, agent.RequestSource),
                ConversationId = agent.ConversationId,
                TaskId = agent.TaskId,
                TaskLabel = agent.TaskLabel,
                WorkspacePath = agent.WorkspacePath,
                ImpactSummary = FirstNonEmpty(ImpactSummary, agent.ImpactSummary),
                Reversibility = Reversibility,
                ReversibilitySummary = ReversibilitySummary,
            };
        }

        internal CopilotExecutionScope ResolveExecutionScope()
        {
            if (!Scope.IsEmpty)
                return Scope;

            return SourceKind switch
            {
                CopilotApprovalSourceKind.InAppAgent => CopilotExecutionScope.ForAgentRequest(new CopilotAgentRequest
                {
                    ConversationId = ConversationId,
                    TaskId = TaskId,
                    WorkspacePath = WorkspacePath,
                }),
                CopilotApprovalSourceKind.ExternalMcp => CopilotExecutionScope.ForExternalMcpSession(
                    RequestSource,
                    RequestSource,
                    WorkspacePath),
                _ => CopilotExecutionScope.ForInProcess(
                    FirstNonEmpty(RequestSource, "colorvision-ui"),
                    WorkspacePath),
            };
        }

        private static string ShortId(string value)
        {
            var normalized = value.Trim();
            return normalized.Length <= 10 ? normalized : normalized[..10];
        }

        private static string FirstNonEmpty(params string?[] values) =>
            values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }

    internal readonly record struct CopilotConfirmationReviewContext(
        string ConversationId,
        string TaskId,
        string WorkspacePath);

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
        private string _reviewDetails = string.Empty;

        public string ActionId { get; init; } = string.Empty;

        public string Title { get; init; } = string.Empty;

        public string Description { get; init; } = string.Empty;

        public string RiskLevel { get; init; } = string.Empty;

        public string ToolName { get; init; } = string.Empty;

        public string ArgumentsSummary { get; init; } = string.Empty;

        public string ArgumentsDigest { get; init; } = string.Empty;

        public string ReviewDetails
        {
            get => _reviewDetails;
            internal init => _reviewDetails = value;
        }

        public bool HasReviewDetails => !string.IsNullOrWhiteSpace(ReviewDetails);

        public string ReviewDetailsHeading => HasReviewDetails ? "完整执行详情" : "参数摘要";

        public string ReviewDisplayText => HasReviewDetails ? ReviewDetails : ArgumentsSummary;

        public bool ExecuteOnApproval { get; init; }

        public bool ResumesAgentOnApproval { get; init; }

        public string AgentCallId { get; internal set; } = string.Empty;

        internal CopilotConfirmationRequestContext RequestContext { get; set; } = new();

        public string RequesterLabel => RequestContext.RequesterLabel;

        public string TaskScopeLabel => RequestContext.TaskScopeLabel;

        public string WorkspaceLabel => RequestContext.WorkspaceLabel;

        public string ImpactLabel => RequestContext.ImpactLabel;

        public string ReversibilityLabel => RequestContext.ReversibilityLabel;

        public string RiskDisplayLabel => string.Equals(RiskLevel, "confirmation-required", StringComparison.OrdinalIgnoreCase)
            ? "受保护操作"
            : RiskLevel;

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
                if (value != ConfirmableActionStatus.Pending)
                    ClearReviewDetails();
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
                    return "已过期";

                if (remaining.TotalSeconds < 60)
                    return $"剩余 {Math.Max(1, (int)Math.Ceiling(remaining.TotalSeconds))} 秒";

                if (remaining.TotalMinutes < 60)
                    return $"剩余 {Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes))} 分钟";

                return $"剩余 {Math.Max(1, (int)Math.Ceiling(remaining.TotalHours))} 小时";
            }
        }

        public string ReviewDeadlineLabel => $"{RemainingLifetimeLabel} · {ExpiresAtLabel} 到期";

        public string ConfirmActionPayloadJson => JsonSerializer.Serialize(new
        {
            action_id = ActionId,
            tool_name = ToolName,
            arguments_summary = ArgumentsSummary,
            arguments_digest = ArgumentsDigest,
            request_source = RequestContext.RequestSource,
            conversation_id = RequestContext.ConversationId,
            task_id = RequestContext.TaskId,
            workspace_path = RequestContext.WorkspacePath,
        }, ConfirmActionPayloadJsonOptions);

        internal bool CanReviewFromConversation(string? conversationId) =>
            RequestContext.CanReviewFromConversation(conversationId);

        internal void UpdateRequestContext(CopilotConfirmationRequestContext context)
        {
            RequestContext = context ?? new CopilotConfirmationRequestContext();
            OnPropertyChanged(nameof(RequestContext));
            OnPropertyChanged(nameof(RequesterLabel));
            OnPropertyChanged(nameof(TaskScopeLabel));
            OnPropertyChanged(nameof(WorkspaceLabel));
            OnPropertyChanged(nameof(ImpactLabel));
            OnPropertyChanged(nameof(ReversibilityLabel));
            OnPropertyChanged(nameof(ConfirmActionPayloadJson));
        }

        internal Func<CancellationToken, Task<CopilotMcpToolCallResult>> Executor { get; set; } = MissingExecutorAsync;

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        internal void ReleaseExecutor() => Executor = MissingExecutorAsync;

        internal void ClearReviewDetails()
        {
            if (_reviewDetails.Length == 0)
                return;

            _reviewDetails = string.Empty;
            OnPropertyChanged(nameof(ReviewDetails));
            OnPropertyChanged(nameof(HasReviewDetails));
            OnPropertyChanged(nameof(ReviewDetailsHeading));
            OnPropertyChanged(nameof(ReviewDisplayText));
        }

        private static Task<CopilotMcpToolCallResult> MissingExecutorAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(CopilotMcpToolCallResult.Fail("action_executor_missing", "No executor is attached to this action."));
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

    internal sealed class CopilotMcpConfirmationStore
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
            string? reviewDetails = null)
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

                action.Status = ConfirmableActionStatus.Approved;
            }

            CopilotMcpAuditLogger.ActionApproved(action);
            RaiseActionStatusChanged(action);
            RaiseActionsChanged();
            message = "The action was approved.";
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

        public async Task<CopilotMcpToolCallResult> ExecuteApprovedAsync(
            string actionId,
            string toolName,
            string argumentsDigest,
            string callerSource,
            string workspacePath,
            CancellationToken cancellationToken)
        {
            return await ExecuteApprovedAsync(
                actionId,
                toolName,
                argumentsDigest,
                CopilotExecutionScope.ForExternalMcpSession(
                    callerSource,
                    callerSource,
                    workspacePath),
                cancellationToken);
        }

        public async Task<CopilotMcpToolCallResult> ExecuteApprovedAsync(
            string actionId,
            string toolName,
            string argumentsDigest,
            CopilotExecutionScope executionScope,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(executionScope);
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

                if (!ArgumentsDigestsMatch(action.ArgumentsDigest, argumentsDigest))
                    return CopilotMcpToolCallResult.Fail("action_arguments_mismatch", "The confirmation arguments_digest does not match the approved action.");

                if (action.RequestContext.SourceKind == CopilotApprovalSourceKind.ExternalMcp
                    && !string.Equals(
                        action.RequestContext.RequestSource,
                        Sanitize(executionScope.CallerIdentity),
                        StringComparison.Ordinal))
                {
                    return CopilotMcpToolCallResult.Fail(
                        "action_source_mismatch",
                        "The approved action belongs to a different MCP caller/source.");
                }

                var actionWorkspacePath = NormalizeWorkspaceForComparison(action.RequestContext.WorkspacePath);
                var currentWorkspacePath = NormalizeWorkspaceForComparison(executionScope.WorkspacePath);
                if (action.RequestContext.SourceKind is CopilotApprovalSourceKind.InAppAgent or CopilotApprovalSourceKind.ExternalMcp
                    && !string.Equals(actionWorkspacePath, currentWorkspacePath, StringComparison.OrdinalIgnoreCase))
                {
                    return CopilotMcpToolCallResult.Fail(
                        "action_workspace_mismatch",
                        "The active workspace changed after this action was approved.");
                }

                if (!action.RequestContext.ResolveExecutionScope().MatchesAuthorizationScope(executionScope))
                {
                    return CopilotMcpToolCallResult.Fail(
                        "action_scope_mismatch",
                        "The approved action belongs to a different execution session, task, run, caller, or capability contract.");
                }

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
                    action.ReleaseExecutor();
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
                action.ReleaseExecutor();
            }

            CopilotMcpAuditLogger.ActionExecuted(action, result.Success, result.Success ? "OK" : result.Text);
            RaiseActionStatusChanged(action);
            RaiseActionsChanged();
            return result;
        }

        public async Task<CopilotMcpToolCallResult> ApproveAndExecuteAsync(
            string actionId,
            CopilotConfirmationReviewContext reviewContext,
            CancellationToken cancellationToken)
        {
            var action = Find(actionId);
            if (action == null)
                return CopilotMcpToolCallResult.Fail("action_not_found", $"No confirmable action exists for action_id={actionId}.");

            if (!action.ExecuteOnApproval)
                return CopilotMcpToolCallResult.Fail("action_requires_client_confirmation", "This action requires the MCP client to call confirm_action after user approval.");

            if (!Approve(actionId, reviewContext, out var approvalMessage))
                return CopilotMcpToolCallResult.Fail("action_approval_failed", approvalMessage);

            return await ExecuteApprovedAsync(
                action.ActionId,
                action.ToolName,
                action.ArgumentsDigest,
                action.RequestContext.ResolveExecutionScope().WithWorkspace(reviewContext.WorkspacePath),
                cancellationToken);
        }

        internal bool BeginAgentFrameworkAction(
            string actionId,
            CopilotAgentRequest request,
            string currentWorkspacePath,
            string argumentsDigest,
            string agentCallId,
            CopilotExecutionScope? executionScope = null)
        {
            ArgumentNullException.ThrowIfNull(request);
            var action = Find(actionId);
            if (action == null)
                return false;

            executionScope = executionScope?.WithWorkspace(currentWorkspacePath);
            var expired = false;
            lock (_syncRoot)
            {
                if (!ValidateReviewContextNoLock(
                    action,
                    new CopilotConfirmationReviewContext(
                        request.ConversationId,
                        request.TaskId,
                        currentWorkspacePath),
                    out _))
                {
                    return false;
                }
                if (string.IsNullOrWhiteSpace(agentCallId)
                    || string.IsNullOrWhiteSpace(action.AgentCallId)
                    || !string.Equals(
                        action.AgentCallId,
                        agentCallId.Trim(),
                        StringComparison.Ordinal))
                {
                    return false;
                }
                if (!ArgumentsDigestsMatch(action.ArgumentsDigest, argumentsDigest))
                {
                    return false;
                }
                if (executionScope != null)
                {
                    var actionScope = action.RequestContext.ResolveExecutionScope();
                    if (!actionScope.MatchesAuthorizationScope(executionScope)
                        || !actionScope.MatchesOperationBinding(executionScope))
                    {
                        return false;
                    }
                }

                if ((action.Status == ConfirmableActionStatus.Pending || action.Status == ConfirmableActionStatus.Approved)
                    && action.ExpiresAt <= DateTimeOffset.UtcNow)
                {
                    action.Status = ConfirmableActionStatus.Expired;
                    action.CompletedAt = DateTimeOffset.UtcNow;
                    action.ReleaseExecutor();
                    expired = true;
                }
                else if (!action.ResumesAgentOnApproval || action.Status != ConfirmableActionStatus.Approved)
                {
                    return false;
                }

                if (!expired)
                    action.Status = ConfirmableActionStatus.Executing;
            }

            if (expired)
            {
                PublishExpired(action);
                return false;
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
                action.ReleaseExecutor();
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

        private static string Sanitize(string? value)
        {
            var text = CopilotMcpAuditLogger.RedactText(value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
            return text.Length <= 1000 ? text : text[..1000] + "...";
        }

        private static string NormalizeReviewDetails(string? value)
        {
            var text = value ?? string.Empty;
            if (text.Contains('\0'))
                throw new ArgumentException("Approval review details cannot contain NUL characters.", nameof(value));
            if (text.Length > MaximumReviewDetailsCharacters)
            {
                throw new ArgumentException(
                    $"Approval review details cannot exceed {MaximumReviewDetailsCharacters} characters.",
                    nameof(value));
            }

            return text;
        }

        private static CopilotConfirmationRequestContext NormalizeRequestContext(
            CopilotConfirmationRequestContext? context)
        {
            context ??= new CopilotConfirmationRequestContext();
            return new CopilotConfirmationRequestContext
            {
                Scope = context.ResolveExecutionScope(),
                SourceKind = Enum.IsDefined(context.SourceKind)
                    ? context.SourceKind
                    : CopilotApprovalSourceKind.Unknown,
                RequestSource = Sanitize(context.RequestSource),
                ConversationId = Sanitize(context.ConversationId),
                TaskId = Sanitize(context.TaskId),
                TaskLabel = Sanitize(context.TaskLabel),
                WorkspacePath = Sanitize(context.WorkspacePath),
                ImpactSummary = Sanitize(context.ImpactSummary),
                Reversibility = Enum.IsDefined(context.Reversibility)
                    ? context.Reversibility
                    : CopilotApprovalReversibility.Unknown,
                ReversibilitySummary = Sanitize(context.ReversibilitySummary),
            };
        }

        private static bool ValidateReviewContextNoLock(
            ConfirmableAction action,
            CopilotConfirmationReviewContext reviewContext,
            out string message)
        {
            var requestContext = action.RequestContext;
            var reviewConversationId = (reviewContext.ConversationId ?? string.Empty).Trim();
            var reviewTaskId = (reviewContext.TaskId ?? string.Empty).Trim();
            var reviewWorkspacePath = NormalizeWorkspaceForComparison(reviewContext.WorkspacePath);
            var actionWorkspacePath = NormalizeWorkspaceForComparison(requestContext.WorkspacePath);

            if (requestContext.SourceKind == CopilotApprovalSourceKind.InAppAgent
                && (string.IsNullOrWhiteSpace(requestContext.ConversationId)
                    || string.IsNullOrWhiteSpace(requestContext.TaskId)
                    || !string.Equals(requestContext.ConversationId, reviewConversationId, StringComparison.Ordinal)
                    || !string.Equals(requestContext.TaskId, reviewTaskId, StringComparison.Ordinal)))
            {
                message = "This approval belongs to a different or no-longer-active Copilot task.";
                return false;
            }

            if (requestContext.SourceKind is CopilotApprovalSourceKind.InAppAgent or CopilotApprovalSourceKind.ExternalMcp
                && !string.Equals(actionWorkspacePath, reviewWorkspacePath, StringComparison.OrdinalIgnoreCase))
            {
                message = "The active workspace changed after this approval request was created.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        private static string NormalizeWorkspaceForComparison(string? workspacePath)
        {
            var normalized = (workspacePath ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalized))
                return string.Empty;

            try
            {
                return System.IO.Path.GetFullPath(normalized)
                    .TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
            }
            catch
            {
                return normalized.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
            }
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
