using System;
using System.IO;

namespace ColorVision.Copilot
{
    public sealed class CopilotAgentSkillReference
    {
        internal const int MaximumPathCharacters = 2_048;

        public string Name { get; set; } = string.Empty;

        public string SkillFilePath { get; set; } = string.Empty;

        public bool IsStructurallyValid()
        {
            var name = CopilotAgentSkillOverrideConfig.NormalizeName(Name);
            if (name.Length == 0
                || !string.Equals(name, Name, StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(SkillFilePath)
                || SkillFilePath.Length > MaximumPathCharacters
                || !Path.IsPathFullyQualified(SkillFilePath)
                || !string.Equals(Path.GetFileName(SkillFilePath), "SKILL.md", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            try
            {
                return string.Equals(Path.GetFullPath(SkillFilePath), SkillFilePath, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        public bool IsExplicitlyInvokedBy(string? text)
        {
            if (!IsStructurallyValid() || string.IsNullOrWhiteSpace(text))
                return false;

            return ContainsInvocation(text, '$') || ContainsInvocation(text, '/');
        }

        internal bool Matches(string? name, string? skillFilePath)
        {
            return IsStructurallyValid()
                && string.Equals(Name, name, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(skillFilePath)
                && PathsEqual(SkillFilePath, skillFilePath);
        }

        public CopilotAgentSkillReference CreateSnapshot() => new()
        {
            Name = CopilotAgentSkillOverrideConfig.NormalizeName(Name),
            SkillFilePath = NormalizePath(SkillFilePath),
        };

        internal static CopilotAgentSkillReference? FromCatalogItem(CopilotAgentSkillCatalogItem? item)
        {
            if (item == null)
                return null;

            var reference = new CopilotAgentSkillReference
            {
                Name = CopilotAgentSkillOverrideConfig.NormalizeName(item.Name),
                SkillFilePath = NormalizePath(item.SkillFilePath),
            };
            return reference.IsStructurallyValid() ? reference : null;
        }

        private bool ContainsInvocation(string text, char prefix)
        {
            var invocation = prefix + Name;
            var startIndex = 0;
            while (startIndex < text.Length)
            {
                var index = text.IndexOf(invocation, startIndex, StringComparison.OrdinalIgnoreCase);
                if (index < 0)
                    return false;

                var hasValidStart = index == 0 || !IsSkillNameCharacter(text[index - 1]);
                var endIndex = index + invocation.Length;
                var hasValidEnd = endIndex == text.Length || !IsSkillNameCharacter(text[endIndex]);
                if (hasValidStart && hasValidEnd)
                    return true;
                startIndex = index + 1;
            }
            return false;
        }

        private static bool IsSkillNameCharacter(char value) =>
            value is >= 'a' and <= 'z'
                or >= 'A' and <= 'Z'
                or >= '0' and <= '9'
                or '-';

        private static bool PathsEqual(string left, string right)
        {
            try
            {
                return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static string NormalizePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            try
            {
                return Path.GetFullPath(path.Trim());
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
