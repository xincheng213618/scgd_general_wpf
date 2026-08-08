using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ColorVision.Copilot
{
    public enum CopilotAgentSkillOverrideState
    {
        Auto,
        NameOnly,
        UserInvocableOnly,
        Off,
        On,
    }

    public sealed class CopilotAgentSkillOverrideConfig
    {
        public const int MaxEntries = 128;

        public const int MaximumPathCharacters = 2_048;

        public string Name { get; set; } = string.Empty;

        public string SkillFilePath { get; set; } = string.Empty;

        public CopilotAgentSkillOverrideState State { get; set; }

        public CopilotAgentSkillOverrideConfig Clone()
        {
            return new CopilotAgentSkillOverrideConfig
            {
                Name = Name,
                SkillFilePath = SkillFilePath,
                State = State,
            };
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
            var normalized = new Dictionary<string, CopilotAgentSkillOverrideConfig>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in entries ?? Array.Empty<CopilotAgentSkillOverrideConfig>())
            {
                var name = NormalizeName(entry?.Name);
                if (name.Length == 0 || entry == null || !Enum.IsDefined(entry.State))
                    continue;

                var skillFilePath = NormalizeSkillFilePath(entry.SkillFilePath);
                if (!string.IsNullOrWhiteSpace(entry.SkillFilePath) && skillFilePath.Length == 0)
                    continue;
                var identity = BuildIdentity(name, skillFilePath);
                if (entry.State == CopilotAgentSkillOverrideState.Auto)
                    normalized.Remove(identity);
                else
                {
                    normalized[identity] = new CopilotAgentSkillOverrideConfig
                    {
                        Name = name,
                        SkillFilePath = skillFilePath,
                        State = entry.State,
                    };
                }
            }

            return normalized.Values
                .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.SkillFilePath, StringComparer.OrdinalIgnoreCase)
                .Take(MaxEntries)
                .ToArray();
        }

        internal static string NormalizeSkillFilePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || path.Length > MaximumPathCharacters)
                return string.Empty;

            try
            {
                var trimmed = path.Trim();
                if (!Path.IsPathFullyQualified(trimmed))
                    return string.Empty;
                var fullPath = Path.GetFullPath(trimmed);
                return fullPath.Length <= MaximumPathCharacters
                    && Path.IsPathFullyQualified(fullPath)
                    && string.Equals(Path.GetFileName(fullPath), "SKILL.md", StringComparison.OrdinalIgnoreCase)
                        ? fullPath
                        : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        internal static CopilotAgentSkillOverrideState ResolveState(
            string? name,
            string? skillFilePath,
            IReadOnlyDictionary<string, CopilotAgentSkillOverrideState>? nameOverrides,
            IReadOnlyDictionary<string, CopilotAgentSkillOverrideState>? pathOverrides)
        {
            var normalizedPath = NormalizeSkillFilePath(skillFilePath);
            if (normalizedPath.Length > 0
                && pathOverrides?.TryGetValue(normalizedPath, out var pathState) == true
                && Enum.IsDefined(pathState))
            {
                return pathState;
            }

            var normalizedName = NormalizeName(name);
            return normalizedName.Length > 0
                && nameOverrides?.TryGetValue(normalizedName, out var nameState) == true
                && Enum.IsDefined(nameState)
                    ? nameState
                    : CopilotAgentSkillOverrideState.Auto;
        }

        private static string BuildIdentity(string name, string skillFilePath) =>
            skillFilePath.Length == 0 ? "name\0" + name : "path\0" + skillFilePath;
    }
}
