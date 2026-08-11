#pragma warning disable CA1822
using Newtonsoft.Json;
using System;

namespace ColorVision.Copilot
{
    public sealed partial class CopilotChatMessage
    {
        public CopilotAgentMode RequestMode
        {
            get => _requestMode;
            set
            {
                if (SetProperty(ref _requestMode, value))
                {
                    OnPropertyChanged(nameof(ResponseInterruptionText));
                    OnPropertyChanged(nameof(RetryActionLabel));
                    OnPropertyChanged(nameof(RetryActionToolTip));
                    OnPropertyChanged(nameof(RefreshActionToolTip));
                    OnPropertyChanged(nameof(ShowsRefreshAction));
                    OnPropertyChanged(nameof(HasCompletedPlan));
                    OnPropertyChanged(nameof(HasAgentTaskState));
                    OnPropertyChanged(nameof(AgentTaskProgressLabel));
                    OnPropertyChanged(nameof(AgentStopReasonLabel));
                    OnPropertyChanged(nameof(AgentTaskSummaryToolTip));
                    OnAgentRunMetricsChanged();
                }
            }
        }
        private CopilotAgentMode _requestMode = CopilotAgentMode.Chat;

        public bool ShouldSerializeRequestMode() => RequestMode != CopilotAgentMode.Chat;

        public string AppliedCodexSandboxMode
        {
            get => _appliedCodexSandboxMode;
            set
            {
                var normalized = NormalizeAppliedCodexSandboxMode(value);
                if (SetProperty(ref _appliedCodexSandboxMode, normalized))
                    OnAgentRunMetricsChanged();
            }
        }
        private string _appliedCodexSandboxMode = string.Empty;

        public bool ShouldSerializeAppliedCodexSandboxMode() =>
            !IsUser
            && RequestMode != CopilotAgentMode.Chat
            && AppliedCodexSandboxMode.Length > 0;

        internal void CaptureAppliedCodexSandboxMode(CopilotCodexSandboxMode mode)
        {
            AppliedCodexSandboxMode = mode switch
            {
                CopilotCodexSandboxMode.ReadOnly => "read-only",
                CopilotCodexSandboxMode.WorkspaceWrite => "workspace-write",
                CopilotCodexSandboxMode.DangerFullAccess => "danger-full-access",
                _ => "unspecified",
            };
        }

        private static string NormalizeAppliedCodexSandboxMode(string? value)
        {
            var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
            return normalized is "unspecified" or "read-only" or "workspace-write" or "danger-full-access"
                ? normalized
                : string.Empty;
        }

        public CopilotWorkspaceReviewTargetContext? WorkspaceReviewTarget { get; set; }

        public bool ShouldSerializeWorkspaceReviewTarget() => WorkspaceReviewTarget != null;

        public CopilotAgentSkillReference? AgentSkillReference { get; set; }

        public bool ShouldSerializeAgentSkillReference() => AgentSkillReference != null;

        [JsonIgnore]
        public string RetryActionLabel => RequestMode == CopilotAgentMode.Chat
            ? Properties.Resources.CopilotRetry
            : "重新运行";

        [JsonIgnore]
        public string RetryActionToolTip => RequestMode == CopilotAgentMode.Chat
            ? "使用本轮已保存的文件、图片和网页上下文重新生成回答。"
            : "重新执行本轮 Agent，并重新读取图片、文件、工作区和工具状态；受保护写操作仍需再次审批。";

        [JsonIgnore]
        public string RefreshActionToolTip => RequestMode == CopilotAgentMode.Chat
            ? "重新读取本轮文件、图片和网页上下文后生成新回答。"
            : string.Empty;

        [JsonIgnore]
        public bool ShowsRefreshAction => RequestMode == CopilotAgentMode.Chat;

        public bool IsResponsePending
        {
            get => _isResponsePending;
            set
            {
                if (SetProperty(ref _isResponsePending, value))
                {
                    OnPropertyChanged(nameof(IsThinkingInProgress));
                    OnPropertyChanged(nameof(HasThinkingTrace));
                    OnPropertyChanged(nameof(ThinkingHeader));
                    OnPropertyChanged(nameof(ThinkingSummaryToolTip));
                    OnPropertyChanged(nameof(HasCompletedPlan));
                    OnPropertyChanged(nameof(HasAgentTaskState));
                }
            }
        }
        private bool _isResponsePending;

        public bool ShouldSerializeIsResponsePending() => IsResponsePending;

        public bool WasResponseInterrupted
        {
            get => _wasResponseInterrupted;
            set
            {
                if (SetProperty(ref _wasResponseInterrupted, value))
                {
                    OnPropertyChanged(nameof(HasResponseInterruption));
                    OnPropertyChanged(nameof(ResponseInterruptionText));
                    OnAgentTaskStateChanged();
                }
            }
        }
        private bool _wasResponseInterrupted;

        public bool ShouldSerializeWasResponseInterrupted() => WasResponseInterrupted;

        public string ResponseInterruptionDetail
        {
            get => _responseInterruptionDetail;
            set
            {
                var normalized = (value ?? string.Empty).Trim();
                if (normalized.Length > MaximumResponseInterruptionDetailLength)
                    normalized = normalized[..(MaximumResponseInterruptionDetailLength - 3)] + "...";
                if (SetProperty(ref _responseInterruptionDetail, normalized))
                    OnPropertyChanged(nameof(ResponseInterruptionText));
            }
        }
        private string _responseInterruptionDetail = string.Empty;

        public bool ShouldSerializeResponseInterruptionDetail() => WasResponseInterrupted && !string.IsNullOrWhiteSpace(ResponseInterruptionDetail);

        [JsonIgnore]
        public bool HasResponseInterruption => !IsUser && WasResponseInterrupted;

        [JsonIgnore]
        public string ResponseInterruptionText => !string.IsNullOrWhiteSpace(ResponseInterruptionDetail)
            ? ResponseInterruptionDetail
            : RequestMode == CopilotAgentMode.Chat
                ? "应用退出时该回答仍在生成；已保留现有内容，但回答可能不完整。"
                : "应用退出时该回答仍在生成，且没有可安全恢复的 Agent checkpoint；已保留现有内容。";

        public void MarkResponseInterrupted(string? detail = null)
        {
            ResponseInterruptionDetail = detail;
            WasResponseInterrupted = true;
        }

        [JsonIgnore]
        public string ModelContent
        {
            get
            {
                var content = IsContentDisplayOnly
                    ? string.Empty
                    : string.IsNullOrWhiteSpace(RequestContent) ? Content : RequestContent;
                if (IsUser)
                    return content;

                var modelContent = content;
                if (WasResponseInterrupted)
                    modelContent = AppendModelMarker(modelContent, ResponseInterruptionModelMarker);
                if (RequestMode != CopilotAgentMode.Chat
                    && AgentStopReason is not (CopilotAgentStopReason.None or CopilotAgentStopReason.Completed))
                {
                    var marker = IncompleteAgentOutcomeModelMarkerPrefix
                        + AgentStopReason
                        + IncompleteAgentOutcomeModelMarkerSuffix;
                    modelContent = AppendModelMarker(modelContent, marker);
                }
                return modelContent;
            }
        }

        private static string AppendModelMarker(string content, string marker) =>
            string.IsNullOrWhiteSpace(content)
                ? marker
                : content.TrimEnd() + "\n\n" + marker;

    }
}
