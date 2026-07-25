using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.FloatingBall
{
    internal enum DesktopPetCopilotActivityKind
    {
        NeedsInput,
        Blocked,
        Ready,
        Running,
    }

    internal enum DesktopPetCopilotCompletionKind
    {
        Ready,
        Blocked,
        Paused,
        Cancelled,
    }

    internal sealed record DesktopPetCopilotActivity(
        string ConversationId,
        DesktopPetCopilotActivityKind Kind,
        DateTimeOffset UpdatedAtUtc)
    {
        public DesktopPetActivityState PetState => Kind switch
        {
            DesktopPetCopilotActivityKind.NeedsInput => DesktopPetActivityState.Waiting,
            DesktopPetCopilotActivityKind.Blocked => DesktopPetActivityState.Failed,
            DesktopPetCopilotActivityKind.Ready => DesktopPetActivityState.Review,
            _ => DesktopPetActivityState.Running,
        };

        public string StatusLabel => Kind switch
        {
            DesktopPetCopilotActivityKind.NeedsInput => "需要输入",
            DesktopPetCopilotActivityKind.Blocked => "任务受阻",
            DesktopPetCopilotActivityKind.Ready => "待查看",
            _ => "运行中",
        };

        public string ConversationLabel
        {
            get
            {
                var normalized = ConversationId.Trim();
                return normalized.Length <= 8 ? normalized : normalized[^8..];
            }
        }
    }

    internal sealed class DesktopPetCopilotActivityTracker
    {
        public const int MaximumActivities = 16;
        private readonly Dictionary<string, DesktopPetCopilotActivity> _activities = new(StringComparer.Ordinal);

        public IReadOnlyList<DesktopPetCopilotActivity> Snapshot()
        {
            return _activities.Values
                .OrderBy(activity => GetPriority(activity.Kind))
                .ThenByDescending(activity => activity.UpdatedAtUtc)
                .ThenBy(activity => activity.ConversationId, StringComparer.Ordinal)
                .ToArray();
        }

        public void ReconcileActive(
            string? activeConversationId,
            bool needsInput,
            DateTimeOffset? updatedAtUtc = null)
        {
            var activeId = NormalizeConversationId(activeConversationId);
            foreach (var conversationId in _activities
                .Where(pair => IsOperational(pair.Value.Kind)
                    && !string.Equals(pair.Key, activeId, StringComparison.Ordinal))
                .Select(pair => pair.Key)
                .ToArray())
            {
                _activities.Remove(conversationId);
            }

            if (activeId.Length == 0)
                return;

            Upsert(
                activeId,
                needsInput ? DesktopPetCopilotActivityKind.NeedsInput : DesktopPetCopilotActivityKind.Running,
                updatedAtUtc ?? DateTimeOffset.UtcNow);
        }

        public void RecordCompletion(
            string? conversationId,
            DesktopPetCopilotCompletionKind completionKind,
            DateTimeOffset? updatedAtUtc = null)
        {
            var normalizedId = NormalizeConversationId(conversationId);
            if (normalizedId.Length == 0)
                return;

            if (completionKind == DesktopPetCopilotCompletionKind.Cancelled)
            {
                _activities.Remove(normalizedId);
                return;
            }

            var kind = completionKind switch
            {
                DesktopPetCopilotCompletionKind.Blocked => DesktopPetCopilotActivityKind.Blocked,
                DesktopPetCopilotCompletionKind.Paused => DesktopPetCopilotActivityKind.NeedsInput,
                _ => DesktopPetCopilotActivityKind.Ready,
            };
            Upsert(normalizedId, kind, updatedAtUtc ?? DateTimeOffset.UtcNow);
        }

        public bool MarkViewed(string? conversationId)
        {
            var normalizedId = NormalizeConversationId(conversationId);
            if (!_activities.TryGetValue(normalizedId, out var activity)
                || activity.Kind is not (DesktopPetCopilotActivityKind.Ready or DesktopPetCopilotActivityKind.Blocked))
            {
                return false;
            }

            return _activities.Remove(normalizedId);
        }

        public void Clear() => _activities.Clear();

        private void Upsert(string conversationId, DesktopPetCopilotActivityKind kind, DateTimeOffset updatedAtUtc)
        {
            _activities[conversationId] = new DesktopPetCopilotActivity(conversationId, kind, updatedAtUtc);
            if (_activities.Count <= MaximumActivities)
                return;

            foreach (var staleId in Snapshot()
                .Skip(MaximumActivities)
                .Select(activity => activity.ConversationId)
                .ToArray())
            {
                _activities.Remove(staleId);
            }
        }

        private static int GetPriority(DesktopPetCopilotActivityKind kind)
        {
            return kind switch
            {
                DesktopPetCopilotActivityKind.NeedsInput => 0,
                DesktopPetCopilotActivityKind.Blocked => 1,
                DesktopPetCopilotActivityKind.Ready => 2,
                _ => 3,
            };
        }

        private static bool IsOperational(DesktopPetCopilotActivityKind kind)
            => kind == DesktopPetCopilotActivityKind.Running;

        private static string NormalizeConversationId(string? conversationId)
            => string.IsNullOrWhiteSpace(conversationId) ? string.Empty : conversationId.Trim();
    }
}
