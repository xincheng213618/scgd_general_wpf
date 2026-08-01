using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Copilot
{
    internal sealed record CopilotSubagentCompletionSnapshot(
        string ConversationId,
        string RunId,
        string RoleId,
        CopilotAgentStopReason StopReason);

    internal sealed record CopilotSubagentCompletionNotice(
        string ConversationId,
        string RunId,
        string Text);

    internal static class CopilotSubagentCompletionNoticePolicy
    {
        private const int MaximumTitleLength = 120;
        private const int MaximumRoleLabelLength = 64;
        private const int MaximumRunIdLength = 120;

        public static CopilotSubagentCompletionSnapshot? CreateSnapshot(
            CopilotToolResult? result,
            string? conversationId)
        {
            var normalizedConversationId = (conversationId ?? string.Empty).Trim();
            var usage = result?.DelegatedRunUsage;
            if (normalizedConversationId.Length == 0
                || usage == null
                || !IsValidRunId(usage.RunId))
            {
                return null;
            }

            return new CopilotSubagentCompletionSnapshot(
                normalizedConversationId,
                usage.RunId,
                usage.RoleId,
                usage.StopReason);
        }

        public static CopilotSubagentCompletionNotice? Create(
            CopilotSubagentCompletionSnapshot? snapshot,
            CopilotConversationRecord? conversation,
            string? selectedConversationId,
            int pendingCount = 1,
            CopilotSubagentRoleCatalog? catalog = null)
        {
            if (snapshot == null
                || conversation == null
                || conversation.IsArchived
                || !string.Equals(
                    snapshot.ConversationId,
                    conversation.Id,
                    StringComparison.Ordinal)
                || !IsValidRunId(snapshot.RunId))
            {
                return null;
            }

            var count = Math.Max(1, pendingCount);
            var prefix = string.Equals(
                    conversation.Id,
                    selectedConversationId,
                    StringComparison.Ordinal)
                ? string.Empty
                : NormalizeLabel(
                    conversation.Title,
                    MaximumTitleLength,
                    CopilotUiText.NewConversationTitle) + " · ";
            var status = count > 1
                ? $"{count:N0} 个子代理结果待查看"
                : FormatSingleStatus(snapshot, catalog ?? CopilotSubagentRoleCatalog.Default);
            return new CopilotSubagentCompletionNotice(
                conversation.Id,
                snapshot.RunId,
                prefix + status);
        }

        private static string FormatSingleStatus(
            CopilotSubagentCompletionSnapshot snapshot,
            CopilotSubagentRoleCatalog catalog)
        {
            var role = catalog.Roles.FirstOrDefault(candidate => string.Equals(
                candidate.Id,
                snapshot.RoleId,
                StringComparison.OrdinalIgnoreCase));
            var roleLabel = NormalizeLabel(
                role?.DisplayName,
                MaximumRoleLabelLength,
                "子代理");
            var subject = string.Equals(roleLabel, "子代理", StringComparison.Ordinal)
                ? roleLabel
                : roleLabel + " 子代理";
            return subject + FormatStatus(snapshot.StopReason);
        }

        private static string FormatStatus(CopilotAgentStopReason stopReason)
        {
            return stopReason switch
            {
                CopilotAgentStopReason.Completed => "已完成",
                CopilotAgentStopReason.AwaitingUser => "等待回复",
                CopilotAgentStopReason.ApprovalDenied => "审批未通过",
                CopilotAgentStopReason.BudgetExhausted => "预算耗尽",
                CopilotAgentStopReason.TaskPassLimit => "达到轮次上限",
                CopilotAgentStopReason.Blocked => "任务受阻",
                CopilotAgentStopReason.Paused => "已暂停",
                CopilotAgentStopReason.Cancelled => "已取消",
                CopilotAgentStopReason.IncompleteOutput => "输出不完整",
                CopilotAgentStopReason.ProviderFailure => "模型连接中断",
                CopilotAgentStopReason.Interrupted => "应用中断",
                _ => "已结束",
            };
        }

        private static bool IsValidRunId(string? runId)
        {
            return runId is { Length: >= 1 and <= MaximumRunIdLength }
                && runId.All(character =>
                    char.IsAsciiLetterOrDigit(character) || character == '-');
        }

        private static string NormalizeLabel(
            string? value,
            int maximumLength,
            string fallback)
        {
            var normalized = string.Join(
                " ",
                (value ?? string.Empty).Split(
                    (char[]?)null,
                    StringSplitOptions.RemoveEmptyEntries));
            if (normalized.Length == 0)
                return fallback;
            if (normalized.Length <= maximumLength)
                return normalized;

            var retainedLength = maximumLength;
            if (char.IsHighSurrogate(normalized[retainedLength - 1])
                && char.IsLowSurrogate(normalized[retainedLength]))
            {
                retainedLength--;
            }
            return normalized[..retainedLength].TrimEnd() + "...";
        }
    }

    internal sealed class CopilotSubagentCompletionNoticeTracker
    {
        private const int MaximumPendingNotices = 64;
        private readonly List<CopilotSubagentCompletionSnapshot> _pending = new();

        public bool Capture(
            CopilotSubagentCompletionSnapshot snapshot,
            CopilotConversationRecord? conversation,
            string? selectedConversationId)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            if (CopilotSubagentCompletionNoticePolicy.Create(
                    snapshot,
                    conversation,
                    selectedConversationId) == null)
            {
                return false;
            }

            _pending.RemoveAll(item => IsSameRun(item, snapshot));
            _pending.Add(snapshot);
            while (_pending.Count > MaximumPendingNotices)
                _pending.RemoveAt(0);
            return true;
        }

        public CopilotSubagentCompletionNotice? GetCurrent(
            IEnumerable<CopilotConversationRecord>? conversations,
            string? selectedConversationId)
        {
            var conversationList = (conversations
                    ?? Array.Empty<CopilotConversationRecord>())
                .Where(item => item != null)
                .ToArray();
            _pending.RemoveAll(snapshot =>
                !conversationList.Any(conversation => string.Equals(
                    conversation.Id,
                    snapshot.ConversationId,
                    StringComparison.Ordinal)));
            while (_pending.Count > 0)
            {
                var snapshot = _pending.LastOrDefault(item => string.Equals(
                        item.ConversationId,
                        selectedConversationId,
                        StringComparison.Ordinal))
                    ?? _pending[^1];
                var conversation = conversationList.First(item => string.Equals(
                    item.Id,
                    snapshot.ConversationId,
                    StringComparison.Ordinal));
                var pendingCount = _pending.Count(item => string.Equals(
                    item.ConversationId,
                    snapshot.ConversationId,
                    StringComparison.Ordinal));
                var notice = CopilotSubagentCompletionNoticePolicy.Create(
                    snapshot,
                    conversation,
                    selectedConversationId,
                    pendingCount);
                if (notice != null)
                    return notice;
                _pending.Remove(snapshot);
            }
            return null;
        }

        public bool AcknowledgeRun(string? conversationId, string? runId)
        {
            var normalizedConversationId = (conversationId ?? string.Empty).Trim();
            var normalizedRunId = (runId ?? string.Empty).Trim();
            return normalizedConversationId.Length > 0
                && normalizedRunId.Length > 0
                && _pending.RemoveAll(item =>
                    string.Equals(
                        item.ConversationId,
                        normalizedConversationId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        item.RunId,
                        normalizedRunId,
                        StringComparison.Ordinal)) > 0;
        }

        public bool AcknowledgeConversation(string? conversationId)
        {
            var normalized = (conversationId ?? string.Empty).Trim();
            return normalized.Length > 0
                && _pending.RemoveAll(item => string.Equals(
                    item.ConversationId,
                    normalized,
                    StringComparison.Ordinal)) > 0;
        }

        private static bool IsSameRun(
            CopilotSubagentCompletionSnapshot left,
            CopilotSubagentCompletionSnapshot right)
        {
            return string.Equals(
                    left.ConversationId,
                    right.ConversationId,
                    StringComparison.Ordinal)
                && string.Equals(left.RunId, right.RunId, StringComparison.Ordinal);
        }
    }
}
