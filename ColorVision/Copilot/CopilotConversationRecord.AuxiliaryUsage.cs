using Newtonsoft.Json;
using System;

namespace ColorVision.Copilot
{
    public sealed class CopilotConversationAuxiliaryUsage
    {
        public int RequestCount { get; set; }

        public int InputTokens { get; set; }

        public int OutputTokens { get; set; }

        public int TotalTokens { get; set; }

        public int? CachedInputTokens { get; set; }

        public DateTimeOffset? LastRequestAtUtc { get; set; }

        [JsonIgnore]
        public CopilotTokenUsage Usage => new(
            InputTokens,
            OutputTokens,
            TotalTokens,
            CachedInputTokens);

        [JsonIgnore]
        public bool HasData => RequestCount > 0 || Usage.HasAny || LastRequestAtUtc.HasValue;

        public bool ShouldSerializeInputTokens() => InputTokens > 0;

        public bool ShouldSerializeOutputTokens() => OutputTokens > 0;

        public bool ShouldSerializeTotalTokens() => TotalTokens > 0;

        public bool ShouldSerializeCachedInputTokens() => CachedInputTokens.HasValue;

        public bool ShouldSerializeLastRequestAtUtc() => LastRequestAtUtc.HasValue;

        public void Record(CopilotTokenUsage usage, DateTimeOffset completedAtUtc)
        {
            RequestCount = AddClamped(RequestCount, 1);
            var total = Usage.Add(Normalize(usage));
            InputTokens = total.InputTokens;
            OutputTokens = total.OutputTokens;
            TotalTokens = total.EffectiveTotalTokens;
            CachedInputTokens = total.CachedInputTokens.HasValue
                ? total.EffectiveCachedInputTokens
                : null;
            LastRequestAtUtc = completedAtUtc == default
                ? DateTimeOffset.UtcNow
                : completedAtUtc.ToUniversalTime();
        }

        internal bool EnsureValid()
        {
            var changed = false;
            var normalized = Normalize(Usage);
            if (InputTokens != normalized.InputTokens)
            {
                InputTokens = normalized.InputTokens;
                changed = true;
            }
            if (OutputTokens != normalized.OutputTokens)
            {
                OutputTokens = normalized.OutputTokens;
                changed = true;
            }
            if (TotalTokens != normalized.EffectiveTotalTokens)
            {
                TotalTokens = normalized.EffectiveTotalTokens;
                changed = true;
            }
            int? normalizedCachedInputTokens = normalized.CachedInputTokens.HasValue
                ? normalized.EffectiveCachedInputTokens
                : null;
            if (CachedInputTokens != normalizedCachedInputTokens)
            {
                CachedInputTokens = normalizedCachedInputTokens;
                changed = true;
            }
            if (RequestCount < 0)
            {
                RequestCount = 0;
                changed = true;
            }
            if (RequestCount == 0 && (normalized.HasAny || LastRequestAtUtc.HasValue))
            {
                RequestCount = 1;
                changed = true;
            }
            if (LastRequestAtUtc.HasValue && LastRequestAtUtc.Value == default)
            {
                LastRequestAtUtc = null;
                changed = true;
            }
            else if (LastRequestAtUtc.HasValue && LastRequestAtUtc.Value.Offset != TimeSpan.Zero)
            {
                LastRequestAtUtc = LastRequestAtUtc.Value.ToUniversalTime();
                changed = true;
            }

            return changed;
        }

        internal CopilotConversationAuxiliaryUsage Copy()
        {
            return new CopilotConversationAuxiliaryUsage
            {
                RequestCount = RequestCount,
                InputTokens = InputTokens,
                OutputTokens = OutputTokens,
                TotalTokens = TotalTokens,
                CachedInputTokens = CachedInputTokens,
                LastRequestAtUtc = LastRequestAtUtc,
            };
        }

        private static CopilotTokenUsage Normalize(CopilotTokenUsage usage)
        {
            return new CopilotTokenUsage(
                Math.Max(0, usage.InputTokens),
                Math.Max(0, usage.OutputTokens),
                usage.EffectiveTotalTokens,
                usage.CachedInputTokens.HasValue ? usage.EffectiveCachedInputTokens : null);
        }

        private static int AddClamped(int left, int right)
        {
            return (int)Math.Min(
                int.MaxValue,
                Math.Max(0L, left) + Math.Max(0L, right));
        }
    }

    public sealed partial class CopilotConversationRecord
    {
        public CopilotConversationAuxiliaryUsage? CompactionUsage { get; set; }

        public CopilotConversationAuxiliaryUsage? TitleGenerationUsage { get; set; }

        public bool ShouldSerializeCompactionUsage() => CompactionUsage?.HasData == true;

        public bool ShouldSerializeTitleGenerationUsage() => TitleGenerationUsage?.HasData == true;

        internal void RecordCompactionUsage(CopilotTokenUsage usage, DateTimeOffset completedAtUtc)
        {
            CompactionUsage ??= new CopilotConversationAuxiliaryUsage();
            CompactionUsage.Record(usage, completedAtUtc);
            OnPropertyChanged(nameof(CompactionUsage));
        }

        internal void RecordTitleGenerationUsage(CopilotTokenUsage usage, DateTimeOffset completedAtUtc)
        {
            TitleGenerationUsage ??= new CopilotConversationAuxiliaryUsage();
            TitleGenerationUsage.Record(usage, completedAtUtc);
            OnPropertyChanged(nameof(TitleGenerationUsage));
        }

        internal bool EnsureAuxiliaryUsageValid()
        {
            var changed = false;
            if (CompactionUsage != null)
            {
                changed |= CompactionUsage.EnsureValid();
                if (!CompactionUsage.HasData)
                {
                    CompactionUsage = null;
                    changed = true;
                }
            }
            if (TitleGenerationUsage != null)
            {
                changed |= TitleGenerationUsage.EnsureValid();
                if (!TitleGenerationUsage.HasData)
                {
                    TitleGenerationUsage = null;
                    changed = true;
                }
            }

            return changed;
        }
    }
}
