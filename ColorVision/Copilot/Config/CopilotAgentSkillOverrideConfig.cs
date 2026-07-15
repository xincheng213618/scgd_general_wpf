using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Copilot
{
    public enum CopilotAgentSkillOverrideState
    {
        Auto,
        NameOnly,
        UserInvocableOnly,
        Off,
    }

    public sealed class CopilotAgentSkillOverrideConfig
    {
        public const int MaxEntries = 128;

        public string Name { get; set; } = string.Empty;

        public CopilotAgentSkillOverrideState State { get; set; }

        public CopilotAgentSkillOverrideConfig Clone()
        {
            return new CopilotAgentSkillOverrideConfig { Name = Name, State = State };
        }

        public static string NormalizeName(string? name)
        {
            var normalized = (name ?? string.Empty).Trim().ToLowerInvariant();
            if (normalized.Length is < 1 or > 64
                || normalized.Any(character => character is not (>= 'a' and <= 'z' or >= '0' and <= '9' or '-')))
            {
                return string.Empty;
            }
            return normalized;
        }

        public static IReadOnlyList<CopilotAgentSkillOverrideConfig> Normalize(IEnumerable<CopilotAgentSkillOverrideConfig>? entries)
        {
            var normalized = new Dictionary<string, CopilotAgentSkillOverrideState>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in entries ?? Array.Empty<CopilotAgentSkillOverrideConfig>())
            {
                var name = NormalizeName(entry?.Name);
                if (name.Length == 0 || entry == null || !Enum.IsDefined(entry.State))
                    continue;
                if (entry.State == CopilotAgentSkillOverrideState.Auto)
                    normalized.Remove(name);
                else
                    normalized[name] = entry.State;
            }

            return normalized
                .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
                .Take(MaxEntries)
                .Select(item => new CopilotAgentSkillOverrideConfig { Name = item.Key, State = item.Value })
                .ToArray();
        }
    }
}
