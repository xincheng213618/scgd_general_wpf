using System;
using System.Collections.Generic;
using System.Linq;

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
            if (SourceKind is not (CopilotApprovalSourceKind.InAppAgent or CopilotApprovalSourceKind.ColorVisionUi)
                || string.IsNullOrWhiteSpace(ConversationId))
            {
                return true;
            }

            return string.Equals(
                ConversationId,
                (conversationId ?? string.Empty).Trim(),
                StringComparison.Ordinal);
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
}
