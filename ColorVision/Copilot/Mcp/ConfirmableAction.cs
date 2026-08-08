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

        public bool IsUserReviewVisible { get; init; } = true;

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

        public string ApprovalDecisionSource { get; internal set; } = string.Empty;

        public string ApprovalDecisionReason { get; internal set; } = string.Empty;

        internal bool HasAutomaticReviewRetryOverride { get; private set; }

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

        internal bool TryMarkAutomaticReviewRetryOverride()
        {
            if (Status != ConfirmableActionStatus.Pending
                || !ResumesAgentOnApproval
                || RequestContext.SourceKind != CopilotApprovalSourceKind.InAppAgent
                || HasAutomaticReviewRetryOverride)
            {
                return false;
            }

            HasAutomaticReviewRetryOverride = true;
            return true;
        }

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

}
