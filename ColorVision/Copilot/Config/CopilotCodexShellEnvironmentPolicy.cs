using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Copilot
{
    internal enum CopilotCodexShellEnvironmentInherit
    {
        All,
        Core,
        None,
    }

    internal enum CopilotCodexShellEnvironmentFilter
    {
        Include,
        Exclude,
    }

    internal sealed record CopilotCodexShellEnvironmentPolicy
    {
        private static readonly string[] WindowsCoreEnvironmentVariables =
        [
            "PATH", "PATHEXT", "SHELL", "COMSPEC", "SYSTEMROOT", "SYSTEMDRIVE",
            "USERNAME", "USERDOMAIN", "USERPROFILE", "HOMEDRIVE", "HOMEPATH",
            "PROGRAMFILES", "PROGRAMFILES(X86)", "PROGRAMW6432", "PROGRAMDATA",
            "LOCALAPPDATA", "APPDATA", "TEMP", "TMP", "TMPDIR", "POWERSHELL", "PWSH",
        ];
        private static readonly string[] DefaultExcludePatterns =
            ["*KEY*", "*SECRET*", "*TOKEN*"];
        private static readonly string[] NonInheritableEnvironmentVariables =
            ["OPENAI_FEDERATION_RULE_ID", "OPENAI_IDENTITY_TOKEN_FILE"];

        public static CopilotCodexShellEnvironmentPolicy Default { get; } = new();

        public static CopilotCodexShellEnvironmentPolicy LockedDown { get; } = new()
        {
            Inherit = CopilotCodexShellEnvironmentInherit.None,
            IgnoreDefaultExcludes = false,
        };

        public CopilotCodexShellEnvironmentInherit Inherit { get; init; } =
            CopilotCodexShellEnvironmentInherit.All;

        public bool IgnoreDefaultExcludes { get; init; } = true;

        public IReadOnlyList<string> Exclude { get; init; } = Array.Empty<string>();

        public IReadOnlyDictionary<string, string> Set { get; init; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<string> IncludeOnly { get; init; } = Array.Empty<string>();

        public CopilotCodexShellEnvironmentPolicy CreateSnapshot() => new()
        {
            Inherit = Inherit,
            IgnoreDefaultExcludes = IgnoreDefaultExcludes,
            Exclude = Exclude.ToArray(),
            Set = new Dictionary<string, string>(Set, StringComparer.OrdinalIgnoreCase),
            IncludeOnly = IncludeOnly.ToArray(),
        };

        public IReadOnlyDictionary<string, string> CreateEnvironmentVariables(
            string? conversationId)
        {
            var variables = Environment.GetEnvironmentVariables()
                .Cast<DictionaryEntry>()
                .Where(entry => entry.Key is string && entry.Value is string)
                .Select(entry => new KeyValuePair<string, string>(
                    (string)entry.Key,
                    (string)entry.Value!));
            return CreateEnvironmentVariables(variables, conversationId);
        }

        internal IReadOnlyDictionary<string, string> CreateEnvironmentVariables(
            IEnumerable<KeyValuePair<string, string>> variables,
            string? conversationId)
        {
            ArgumentNullException.ThrowIfNull(variables);
            var environment = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (Inherit != CopilotCodexShellEnvironmentInherit.None)
            {
                foreach (var pair in variables)
                {
                    if (!IsValidEnvironmentVariableName(pair.Key)
                        || pair.Value.IndexOf('\0') >= 0
                        || (Inherit == CopilotCodexShellEnvironmentInherit.Core
                            && !WindowsCoreEnvironmentVariables.Contains(
                                pair.Key,
                                StringComparer.OrdinalIgnoreCase)))
                    {
                        continue;
                    }
                    environment[pair.Key] = pair.Value;
                }
            }

            if (!IgnoreDefaultExcludes)
                RemoveMatches(environment, DefaultExcludePatterns);
            RemoveMatches(environment, Exclude);

            foreach (var pair in Set)
            {
                if (IsValidEnvironmentVariableName(pair.Key)
                    && pair.Value.IndexOf('\0') < 0)
                {
                    environment[pair.Key] = pair.Value;
                }
            }

            if (IncludeOnly.Count > 0)
            {
                foreach (var name in environment.Keys
                    .Where(name => !MatchesAny(name, IncludeOnly))
                    .ToArray())
                {
                    environment.Remove(name);
                }
            }

            var normalizedConversationId = (conversationId ?? string.Empty).Trim();
            if (normalizedConversationId.Length is > 0 and <= 32_767
                && normalizedConversationId.IndexOf('\0') < 0)
            {
                environment["CODEX_THREAD_ID"] = normalizedConversationId;
            }

            foreach (var name in NonInheritableEnvironmentVariables)
                environment.Remove(name);
            if (!environment.ContainsKey("PATHEXT"))
                environment["PATHEXT"] = ".COM;.EXE;.BAT;.CMD";
            return environment;
        }

        internal string BuildRedactedSummary() =>
            $"inherit={Inherit.ToString().ToLowerInvariant()}, "
            + $"default_sensitive_filter={(IgnoreDefaultExcludes ? "off" : "on")}, "
            + $"exclude={Exclude.Count}, set={Set.Count}, include_only={IncludeOnly.Count}";

        internal static bool IsValidEnvironmentVariableName(string? value) =>
            !string.IsNullOrWhiteSpace(value)
            && value.Length <= 32_767
            && value.IndexOfAny(['=', '\0']) < 0;

        internal static bool IsValidPattern(string? value) =>
            value != null && value.Length <= 512 && value.IndexOf('\0') < 0;

        internal static bool IsNonInheritableEnvironmentVariable(string? value) =>
            value != null && NonInheritableEnvironmentVariables.Contains(
                value,
                StringComparer.OrdinalIgnoreCase);

        private static void RemoveMatches(
            Dictionary<string, string> environment,
            IReadOnlyList<string> patterns)
        {
            if (patterns.Count == 0)
                return;
            foreach (var name in environment.Keys
                .Where(name => MatchesAny(name, patterns))
                .ToArray())
            {
                environment.Remove(name);
            }
        }

        private static bool MatchesAny(string name, IReadOnlyList<string> patterns) =>
            patterns.Any(pattern => WildcardMatches(name, pattern));

        private static bool WildcardMatches(string value, string pattern)
        {
            var valueIndex = 0;
            var patternIndex = 0;
            var starIndex = -1;
            var retryValueIndex = -1;
            while (valueIndex < value.Length)
            {
                if (patternIndex < pattern.Length
                    && (pattern[patternIndex] == '?'
                        || char.ToUpperInvariant(pattern[patternIndex])
                            == char.ToUpperInvariant(value[valueIndex])))
                {
                    valueIndex++;
                    patternIndex++;
                    continue;
                }
                if (patternIndex < pattern.Length && pattern[patternIndex] == '*')
                {
                    starIndex = patternIndex++;
                    retryValueIndex = valueIndex;
                    continue;
                }
                if (starIndex >= 0)
                {
                    patternIndex = starIndex + 1;
                    valueIndex = ++retryValueIndex;
                    continue;
                }
                return false;
            }
            while (patternIndex < pattern.Length && pattern[patternIndex] == '*')
                patternIndex++;
            return patternIndex == pattern.Length;
        }
    }

    internal sealed record CopilotCodexShellEnvironmentPolicyLayer
    {
        public bool HasAssignment { get; init; }

        public bool IsValid { get; init; } = true;

        public string ErrorMessage { get; init; } = string.Empty;

        public CopilotCodexShellEnvironmentInherit? Inherit { get; init; }

        public bool? IgnoreDefaultExcludes { get; init; }

        public bool HasLegacyExclude { get; init; }

        public IReadOnlyList<string> LegacyExclude { get; init; } = Array.Empty<string>();

        public bool HasLegacyIncludeOnly { get; init; }

        public IReadOnlyList<string> LegacyIncludeOnly { get; init; } = Array.Empty<string>();

        public IReadOnlyDictionary<string, CopilotCodexShellEnvironmentFilter> Filters { get; init; } =
            new Dictionary<string, CopilotCodexShellEnvironmentFilter>(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyDictionary<string, string> Set { get; init; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static CopilotCodexShellEnvironmentPolicyLayer Empty { get; } = new();
    }

    internal static class CopilotCodexShellEnvironmentPolicyMerge
    {
        public static CopilotCodexShellEnvironmentPolicy Apply(
            CopilotCodexShellEnvironmentPolicy current,
            CopilotCodexShellEnvironmentPolicyLayer layer)
        {
            ArgumentNullException.ThrowIfNull(current);
            ArgumentNullException.ThrowIfNull(layer);
            if (!layer.HasAssignment)
                return current;
            if (!layer.IsValid)
                return CopilotCodexShellEnvironmentPolicy.LockedDown;

            var exclude = (layer.HasLegacyExclude
                    ? layer.LegacyExclude
                    : current.Exclude)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var includeOnly = (layer.HasLegacyIncludeOnly
                    ? layer.LegacyIncludeOnly
                    : current.IncludeOnly)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            foreach (var pair in layer.Filters)
            {
                exclude.RemoveAll(pattern => string.Equals(
                    pattern,
                    pair.Key,
                    StringComparison.OrdinalIgnoreCase));
                includeOnly.RemoveAll(pattern => string.Equals(
                    pattern,
                    pair.Key,
                    StringComparison.OrdinalIgnoreCase));
                (pair.Value == CopilotCodexShellEnvironmentFilter.Exclude
                    ? exclude
                    : includeOnly).Add(pair.Key);
            }

            var set = new Dictionary<string, string>(current.Set, StringComparer.OrdinalIgnoreCase);
            foreach (var pair in layer.Set)
                set[pair.Key] = pair.Value;
            return new CopilotCodexShellEnvironmentPolicy
            {
                Inherit = layer.Inherit ?? current.Inherit,
                IgnoreDefaultExcludes = layer.IgnoreDefaultExcludes
                    ?? current.IgnoreDefaultExcludes,
                Exclude = exclude.ToArray(),
                IncludeOnly = includeOnly.ToArray(),
                Set = set,
            };
        }
    }
}
