using Newtonsoft.Json;
using System;

namespace ColorVision.Copilot
{
    public enum CopilotConversationActivityState
    {
        None,
        NeedsInput,
        Ready,
        Blocked,
    }

    public sealed class CopilotConversationActivity
    {
        public const int CurrentSchemaVersion = 1;
        internal const int MaximumSourceMessageIdCharacters = 128;

        public int SchemaVersion { get; init; } = CurrentSchemaVersion;

        public CopilotConversationActivityState State { get; init; }

        public string SourceMessageId { get; init; } = string.Empty;

        public DateTimeOffset UpdatedAtUtc { get; init; }

        [JsonIgnore]
        public string StatusLabel => State switch
        {
            CopilotConversationActivityState.NeedsInput => "需要输入",
            CopilotConversationActivityState.Ready => "待查看",
            CopilotConversationActivityState.Blocked => "任务受阻",
            _ => string.Empty,
        };

        [JsonIgnore]
        public bool IsAcknowledgedByViewing => State is
            CopilotConversationActivityState.Ready or CopilotConversationActivityState.Blocked;

        public bool IsStructurallyValid()
        {
            return SchemaVersion == CurrentSchemaVersion
                && State is (CopilotConversationActivityState.NeedsInput
                    or CopilotConversationActivityState.Ready
                    or CopilotConversationActivityState.Blocked)
                && !string.IsNullOrWhiteSpace(SourceMessageId)
                && SourceMessageId.Length <= MaximumSourceMessageIdCharacters
                && UpdatedAtUtc != default;
        }

        internal static CopilotConversationActivity Create(
            CopilotConversationActivityState state,
            string sourceMessageId,
            DateTimeOffset updatedAtUtc)
        {
            var activity = new CopilotConversationActivity
            {
                State = state,
                SourceMessageId = (sourceMessageId ?? string.Empty).Trim(),
                UpdatedAtUtc = (updatedAtUtc == default ? DateTimeOffset.UtcNow : updatedAtUtc).ToUniversalTime(),
            };
            if (!activity.IsStructurallyValid())
                throw new ArgumentException("A valid conversation activity is required.", nameof(state));
            return activity;
        }
    }
}
