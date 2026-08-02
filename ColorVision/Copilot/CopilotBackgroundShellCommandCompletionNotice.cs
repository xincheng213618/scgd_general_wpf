using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Copilot
{
    internal sealed record CopilotBackgroundShellCommandCompletionNotice(
        string ConversationId,
        string BackgroundId,
        string Text);

    internal static class CopilotBackgroundShellCommandCompletionNoticePolicy
    {
        private const int MaximumTitleLength = 120;

        public static CopilotBackgroundShellCommandCompletionNotice? Create(
            CopilotBackgroundShellCommandSnapshot? snapshot,
            CopilotConversationRecord? conversation,
            string? selectedConversationId,
            int pendingCount = 1)
        {
            if (snapshot == null
                || conversation == null
                || conversation.IsArchived
                || !string.Equals(
                    snapshot.ConversationId,
                    conversation.Id,
                    StringComparison.Ordinal)
                || snapshot.State is CopilotBackgroundShellCommandState.Running
                    or CopilotBackgroundShellCommandState.Stopped)
            {
                return null;
            }

            var count = Math.Max(1, pendingCount);
            var prefix = string.Equals(
                    conversation.Id,
                    selectedConversationId,
                    StringComparison.Ordinal)
                ? string.Empty
                : NormalizeTitle(conversation.Title) + " · ";
            var status = count > 1
                ? $"{count:N0} 条后台命令待查看"
                : FormatStatus(snapshot);
            return new CopilotBackgroundShellCommandCompletionNotice(
                conversation.Id,
                snapshot.Id,
                prefix + status);
        }

        private static string FormatStatus(
            CopilotBackgroundShellCommandSnapshot snapshot)
        {
            var exitCode = snapshot.ExitCode.HasValue
                ? $"（退出码 {snapshot.ExitCode.Value:N0}）"
                : string.Empty;
            return snapshot.State switch
            {
                CopilotBackgroundShellCommandState.Completed =>
                    "后台命令已完成" + exitCode,
                CopilotBackgroundShellCommandState.Failed =>
                    "后台命令失败" + exitCode,
                CopilotBackgroundShellCommandState.Expired =>
                    "后台命令已到期" + exitCode,
                _ => "后台命令已结束" + exitCode,
            };
        }

        private static string NormalizeTitle(string? title)
        {
            var normalized = string.Join(
                " ",
                (title ?? string.Empty).Split(
                    (char[]?)null,
                    StringSplitOptions.RemoveEmptyEntries));
            if (normalized.Length == 0)
                return CopilotUiText.NewConversationTitle;
            if (normalized.Length <= MaximumTitleLength)
                return normalized;

            var retainedLength = MaximumTitleLength;
            if (char.IsHighSurrogate(normalized[retainedLength - 1])
                && char.IsLowSurrogate(normalized[retainedLength]))
            {
                retainedLength--;
            }
            return normalized[..retainedLength].TrimEnd() + "...";
        }
    }

    internal sealed class CopilotBackgroundShellCommandCompletionNoticeTracker
    {
        private readonly List<CopilotBackgroundShellCommandSnapshot> _pending = new();

        public bool Capture(
            CopilotBackgroundShellCommandSnapshot snapshot,
            CopilotConversationRecord? conversation,
            string? selectedConversationId)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            if (CopilotBackgroundShellCommandCompletionNoticePolicy.Create(
                    snapshot,
                    conversation,
                    selectedConversationId) == null)
            {
                return false;
            }

            _pending.RemoveAll(item =>
                string.Equals(item.Id, snapshot.Id, StringComparison.Ordinal));
            _pending.Add(snapshot);
            while (_pending.Count
                > CopilotBackgroundShellCommandRegistry.MaximumRetainedCommands)
            {
                _pending.RemoveAt(0);
            }
            return true;
        }

        public CopilotBackgroundShellCommandCompletionNotice? GetCurrent(
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
                var conversation = conversationList.First(item =>
                    string.Equals(
                        item.Id,
                        snapshot.ConversationId,
                        StringComparison.Ordinal));
                var pendingCount = _pending.Count(item => string.Equals(
                    item.ConversationId,
                    snapshot.ConversationId,
                    StringComparison.Ordinal));
                var notice =
                    CopilotBackgroundShellCommandCompletionNoticePolicy.Create(
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

        public bool AcknowledgeConversation(string? conversationId)
        {
            var normalized = (conversationId ?? string.Empty).Trim();
            return normalized.Length > 0
                && _pending.RemoveAll(item => string.Equals(
                    item.ConversationId,
                    normalized,
                    StringComparison.Ordinal)) > 0;
        }

        public bool AcknowledgeBackground(string? backgroundId)
        {
            var normalized = (backgroundId ?? string.Empty).Trim();
            return normalized.Length > 0
                && _pending.RemoveAll(item => string.Equals(
                    item.Id,
                    normalized,
                    StringComparison.Ordinal)) > 0;
        }
    }
}
