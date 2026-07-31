using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Copilot
{
    internal enum CopilotCompletionNoticeKind
    {
        BackgroundCommand,
        Subagent,
    }

    internal sealed record CopilotCompletionNotice(
        CopilotCompletionNoticeKind Kind,
        string ConversationId,
        string ItemId,
        string Text);

    internal sealed class CopilotCompletionNoticeCenter
    {
        private readonly CopilotBackgroundShellCommandCompletionNoticeTracker _backgroundTracker = new();
        private readonly CopilotSubagentCompletionNoticeTracker _subagentTracker = new();
        private readonly Dictionary<NoticeKey, long> _captureOrder = new();
        private long _nextCaptureOrder;

        public bool CaptureBackgroundCommand(
            CopilotBackgroundShellCommandSnapshot snapshot,
            CopilotConversationRecord? conversation,
            string? selectedConversationId)
        {
            if (!_backgroundTracker.Capture(snapshot, conversation, selectedConversationId))
                return false;

            MarkCaptured(CopilotCompletionNoticeKind.BackgroundCommand, snapshot.ConversationId, snapshot.Id);
            return true;
        }

        public bool CaptureSubagent(
            CopilotSubagentCompletionSnapshot snapshot,
            CopilotConversationRecord? conversation,
            string? selectedConversationId)
        {
            if (!_subagentTracker.Capture(snapshot, conversation, selectedConversationId))
                return false;

            MarkCaptured(CopilotCompletionNoticeKind.Subagent, snapshot.ConversationId, snapshot.RunId);
            return true;
        }

        public CopilotCompletionNotice? GetCurrent(
            IEnumerable<CopilotConversationRecord>? conversations,
            string? selectedConversationId)
        {
            var conversationList = (conversations ?? Array.Empty<CopilotConversationRecord>())
                .Where(conversation => conversation != null)
                .ToArray();
            PruneRemovedConversations(conversationList);
            var candidates = new List<CopilotCompletionNotice>(2);

            var background = _backgroundTracker.GetCurrent(conversationList, selectedConversationId);
            if (background != null)
            {
                candidates.Add(new CopilotCompletionNotice(
                    CopilotCompletionNoticeKind.BackgroundCommand,
                    background.ConversationId,
                    background.BackgroundId,
                    background.Text));
            }

            var subagent = _subagentTracker.GetCurrent(conversationList, selectedConversationId);
            if (subagent != null)
            {
                candidates.Add(new CopilotCompletionNotice(
                    CopilotCompletionNoticeKind.Subagent,
                    subagent.ConversationId,
                    subagent.RunId,
                    subagent.Text));
            }

            return candidates
                .OrderByDescending(notice => string.Equals(
                    notice.ConversationId,
                    selectedConversationId,
                    StringComparison.Ordinal))
                .ThenByDescending(GetCaptureOrder)
                .FirstOrDefault();
        }

        public bool Acknowledge(
            CopilotCompletionNoticeKind kind,
            string? conversationId,
            string? itemId)
        {
            var removed = kind switch
            {
                CopilotCompletionNoticeKind.BackgroundCommand =>
                    _backgroundTracker.AcknowledgeBackground(itemId),
                CopilotCompletionNoticeKind.Subagent =>
                    _subagentTracker.AcknowledgeRun(conversationId, itemId),
                _ => false,
            };
            if (removed)
                _captureOrder.Remove(new NoticeKey(kind, Normalize(conversationId), Normalize(itemId)));
            return removed;
        }

        public bool AcknowledgeConversation(
            CopilotCompletionNoticeKind kind,
            string? conversationId)
        {
            var removed = kind switch
            {
                CopilotCompletionNoticeKind.BackgroundCommand =>
                    _backgroundTracker.AcknowledgeConversation(conversationId),
                CopilotCompletionNoticeKind.Subagent =>
                    _subagentTracker.AcknowledgeConversation(conversationId),
                _ => false,
            };
            if (removed)
                RemoveCaptureOrder(kind, conversationId);
            return removed;
        }

        public bool AcknowledgeConversation(string? conversationId)
        {
            var backgroundRemoved = _backgroundTracker.AcknowledgeConversation(conversationId);
            var subagentRemoved = _subagentTracker.AcknowledgeConversation(conversationId);
            if (backgroundRemoved || subagentRemoved)
                RemoveCaptureOrder(kind: null, conversationId);
            return backgroundRemoved || subagentRemoved;
        }

        private void MarkCaptured(
            CopilotCompletionNoticeKind kind,
            string? conversationId,
            string? itemId)
        {
            _captureOrder[new NoticeKey(kind, Normalize(conversationId), Normalize(itemId))] = ++_nextCaptureOrder;
        }

        private long GetCaptureOrder(CopilotCompletionNotice notice)
        {
            return _captureOrder.TryGetValue(
                new NoticeKey(notice.Kind, notice.ConversationId, notice.ItemId),
                out var order)
                ? order
                : 0;
        }

        private void PruneRemovedConversations(
            IReadOnlyCollection<CopilotConversationRecord> conversations)
        {
            var conversationIds = conversations
                .Select(conversation => conversation.Id)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var key in _captureOrder.Keys
                .Where(key => !conversationIds.Contains(key.ConversationId))
                .ToArray())
            {
                _captureOrder.Remove(key);
            }
        }

        private void RemoveCaptureOrder(
            CopilotCompletionNoticeKind? kind,
            string? conversationId)
        {
            var normalizedConversationId = Normalize(conversationId);
            foreach (var key in _captureOrder.Keys
                .Where(key => (!kind.HasValue || key.Kind == kind.Value)
                    && string.Equals(key.ConversationId, normalizedConversationId, StringComparison.Ordinal))
                .ToArray())
            {
                _captureOrder.Remove(key);
            }
        }

        private static string Normalize(string? value) => (value ?? string.Empty).Trim();

        private readonly record struct NoticeKey(
            CopilotCompletionNoticeKind Kind,
            string ConversationId,
            string ItemId);
    }
}
