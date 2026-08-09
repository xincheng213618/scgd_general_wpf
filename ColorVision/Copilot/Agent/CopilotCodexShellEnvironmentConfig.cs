using System;
using System.Collections.Generic;
using System.Text;

namespace ColorVision.Copilot
{
    internal static partial class CopilotProjectInstructionDiscoveryConfig
    {
        private const int MaximumShellEnvironmentPatterns = 256;
        private const int MaximumShellEnvironmentSetEntries = 256;
        private const int MaximumShellEnvironmentLogicalValueLines = 256;
        private const int MaximumShellEnvironmentValueCharacters = 32_767;
        private const string InvalidShellEnvironmentPolicyMessage =
            "shell_environment_policy 格式无效；为防止环境变量意外继承，本层已切换到锁定环境。";

        private enum ShellEnvironmentPolicyTable
        {
            Root,
            Other,
            Policy,
            Set,
            Filters,
        }

        private static CopilotCodexShellEnvironmentPolicyLayer ParseShellEnvironmentPolicyLayer(
            string source)
        {
            var inherit = (CopilotCodexShellEnvironmentInherit?)null;
            var ignoreDefaultExcludes = (bool?)null;
            var legacyExclude = Array.Empty<string>();
            var legacyIncludeOnly = Array.Empty<string>();
            var hasLegacyExclude = false;
            var hasLegacyIncludeOnly = false;
            var filters = new Dictionary<string, CopilotCodexShellEnvironmentFilter>(
                StringComparer.OrdinalIgnoreCase);
            var set = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var seenPolicyFields = new HashSet<string>(StringComparer.Ordinal);
            var hasAssignment = false;
            var hasCanonicalFilters = false;
            var valid = true;
            var table = ShellEnvironmentPolicyTable.Root;
            var lines = NormalizeLines(source);

            for (var index = 0; index < lines.Length && valid; index++)
            {
                var line = StripComment(lines[index]).Trim();
                if (line.Length == 0)
                    continue;
                if (line[0] == '[')
                {
                    table = IsExactTableHeader(line, "shell_environment_policy")
                        ? ShellEnvironmentPolicyTable.Policy
                        : IsExactTableHeader(line, "shell_environment_policy.set")
                            ? ShellEnvironmentPolicyTable.Set
                            : IsExactTableHeader(line, "shell_environment_policy.filters")
                                ? ShellEnvironmentPolicyTable.Filters
                                : ShellEnvironmentPolicyTable.Other;
                    continue;
                }

                var equalsIndex = FindTomlAssignmentEquals(line);
                if (equalsIndex <= 0)
                    continue;
                var rawKey = line[..equalsIndex].Trim();
                var value = line[(equalsIndex + 1)..].Trim();
                var effectiveTable = table;
                var key = string.Empty;
                if (table == ShellEnvironmentPolicyTable.Root)
                {
                    if (!TryMapDottedShellEnvironmentKey(
                        rawKey,
                        out effectiveTable,
                        out key))
                    {
                        continue;
                    }
                }
                else if (table == ShellEnvironmentPolicyTable.Other)
                {
                    continue;
                }
                else if (!TryParseTomlKey(rawKey, out key))
                {
                    valid = false;
                    break;
                }

                if (effectiveTable == ShellEnvironmentPolicyTable.Policy
                    && key is "exclude" or "include_only"
                    && value.StartsWith('[')
                    && !HasClosedArray(value))
                {
                    value = ReadContinuedShellEnvironmentValue(
                        lines,
                        ref index,
                        value,
                        HasClosedArray);
                }
                else if (effectiveTable == ShellEnvironmentPolicyTable.Policy
                    && key is "set" or "filters"
                    && value.StartsWith('{')
                    && !HasClosedInlineTable(value))
                {
                    value = ReadContinuedShellEnvironmentValue(
                        lines,
                        ref index,
                        value,
                        HasClosedInlineTable);
                }

                switch (effectiveTable)
                {
                    case ShellEnvironmentPolicyTable.Policy:
                        if (key == "inherit")
                        {
                            hasAssignment = true;
                            valid = seenPolicyFields.Add(key)
                                && TryParseShellEnvironmentInherit(value, out inherit);
                        }
                        else if (key == "ignore_default_excludes")
                        {
                            hasAssignment = true;
                            var parsedIgnoreDefaultExcludes = false;
                            valid = seenPolicyFields.Add(key)
                                && TryParseTomlBoolean(value, out parsedIgnoreDefaultExcludes);
                            if (valid)
                                ignoreDefaultExcludes = parsedIgnoreDefaultExcludes;
                        }
                        else if (key == "exclude")
                        {
                            hasAssignment = true;
                            hasLegacyExclude = true;
                            valid = seenPolicyFields.Add(key)
                                && TryParseShellEnvironmentPatternArray(value, out legacyExclude);
                        }
                        else if (key == "include_only")
                        {
                            hasAssignment = true;
                            hasLegacyIncludeOnly = true;
                            valid = seenPolicyFields.Add(key)
                                && TryParseShellEnvironmentPatternArray(value, out legacyIncludeOnly);
                        }
                        else if (key == "set")
                        {
                            hasAssignment = true;
                            valid = seenPolicyFields.Add(key)
                                && TryParseShellEnvironmentStringTable(
                                    value,
                                    set,
                                    validateEnvironmentNames: true);
                        }
                        else if (key == "filters")
                        {
                            hasAssignment = true;
                            hasCanonicalFilters = true;
                            valid = seenPolicyFields.Add(key)
                                && TryParseShellEnvironmentFilterTable(value, filters);
                        }
                        break;

                    case ShellEnvironmentPolicyTable.Set:
                        hasAssignment = true;
                        valid = TryParseShellEnvironmentSetEntry(key, value, set);
                        break;

                    case ShellEnvironmentPolicyTable.Filters:
                        hasAssignment = true;
                        hasCanonicalFilters = true;
                        valid = TryParseShellEnvironmentFilterEntry(key, value, filters);
                        break;
                }
            }

            if (hasCanonicalFilters && (hasLegacyExclude || hasLegacyIncludeOnly))
                valid = false;
            if (!hasAssignment)
                return CopilotCodexShellEnvironmentPolicyLayer.Empty;
            if (!valid)
            {
                return new CopilotCodexShellEnvironmentPolicyLayer
                {
                    HasAssignment = true,
                    IsValid = false,
                    ErrorMessage = InvalidShellEnvironmentPolicyMessage,
                };
            }
            return new CopilotCodexShellEnvironmentPolicyLayer
            {
                HasAssignment = true,
                Inherit = inherit,
                IgnoreDefaultExcludes = ignoreDefaultExcludes,
                HasLegacyExclude = hasLegacyExclude,
                LegacyExclude = legacyExclude,
                HasLegacyIncludeOnly = hasLegacyIncludeOnly,
                LegacyIncludeOnly = legacyIncludeOnly,
                Filters = filters,
                Set = set,
            };
        }

        private static bool TryMapDottedShellEnvironmentKey(
            string rawKey,
            out ShellEnvironmentPolicyTable table,
            out string key)
        {
            const string policyPrefix = "shell_environment_policy.";
            const string setPrefix = "shell_environment_policy.set.";
            const string filtersPrefix = "shell_environment_policy.filters.";
            table = ShellEnvironmentPolicyTable.Other;
            key = string.Empty;
            if (rawKey.StartsWith(setPrefix, StringComparison.Ordinal))
            {
                table = ShellEnvironmentPolicyTable.Set;
                return TryParseTomlKey(rawKey[setPrefix.Length..], out key);
            }
            if (rawKey.StartsWith(filtersPrefix, StringComparison.Ordinal))
            {
                table = ShellEnvironmentPolicyTable.Filters;
                return TryParseTomlKey(rawKey[filtersPrefix.Length..], out key);
            }
            if (!rawKey.StartsWith(policyPrefix, StringComparison.Ordinal))
                return false;
            table = ShellEnvironmentPolicyTable.Policy;
            return TryParseTomlKey(rawKey[policyPrefix.Length..], out key);
        }

        private static bool TryParseShellEnvironmentInherit(
            string value,
            out CopilotCodexShellEnvironmentInherit? inherit)
        {
            inherit = null;
            if (!TryParseShellEnvironmentStringValue(value, 16, out var token))
                return false;
            inherit = token switch
            {
                "all" => CopilotCodexShellEnvironmentInherit.All,
                "core" => CopilotCodexShellEnvironmentInherit.Core,
                "none" => CopilotCodexShellEnvironmentInherit.None,
                _ => null,
            };
            return inherit.HasValue;
        }

        private static bool TryParseShellEnvironmentPatternArray(
            string value,
            out string[] patterns)
        {
            patterns = Array.Empty<string>();
            var index = 0;
            SkipWhitespace(value, ref index);
            if (!TryConsume(value, ref index, '['))
                return false;
            var results = new List<string>();
            while (true)
            {
                SkipWhitespace(value, ref index);
                if (TryConsume(value, ref index, ']'))
                {
                    SkipWhitespace(value, ref index);
                    if (index != value.Length)
                        return false;
                    patterns = results.ToArray();
                    return true;
                }
                if (results.Count >= MaximumShellEnvironmentPatterns
                    || !TryReadTomlString(value, ref index, out var pattern)
                    || !CopilotCodexShellEnvironmentPolicy.IsValidPattern(pattern))
                {
                    return false;
                }
                results.Add(pattern);
                SkipWhitespace(value, ref index);
                if (TryConsume(value, ref index, ','))
                    continue;
                if (index >= value.Length || value[index] != ']')
                    return false;
            }
        }

        private static bool TryParseShellEnvironmentStringTable(
            string value,
            Dictionary<string, string> destination,
            bool validateEnvironmentNames)
        {
            var index = 0;
            SkipWhitespace(value, ref index);
            if (!TryConsume(value, ref index, '{'))
                return false;
            while (true)
            {
                SkipWhitespace(value, ref index);
                if (TryConsume(value, ref index, '}'))
                {
                    SkipWhitespace(value, ref index);
                    return index == value.Length;
                }
                if (!TryReadTomlKey(value, ref index, out var key)
                    || (validateEnvironmentNames
                        && !CopilotCodexShellEnvironmentPolicy.IsValidEnvironmentVariableName(key))
                    || destination.Count >= MaximumShellEnvironmentSetEntries
                    || destination.ContainsKey(key))
                {
                    return false;
                }
                SkipWhitespace(value, ref index);
                if (!TryConsume(value, ref index, '='))
                    return false;
                SkipWhitespace(value, ref index);
                if (!TryReadTomlString(value, ref index, out var configuredValue)
                    || configuredValue.Length > MaximumShellEnvironmentValueCharacters
                    || configuredValue.IndexOf('\0') >= 0)
                {
                    return false;
                }
                destination.Add(key, configuredValue);
                SkipWhitespace(value, ref index);
                if (TryConsume(value, ref index, ','))
                    continue;
                if (index >= value.Length || value[index] != '}')
                    return false;
            }
        }

        private static bool TryParseShellEnvironmentFilterTable(
            string value,
            Dictionary<string, CopilotCodexShellEnvironmentFilter> destination)
        {
            var entries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!TryParseShellEnvironmentStringTable(
                value,
                entries,
                validateEnvironmentNames: false))
            {
                return false;
            }
            foreach (var pair in entries)
            {
                if (!TryAddShellEnvironmentFilter(pair.Key, pair.Value, destination))
                    return false;
            }
            return true;
        }

        private static bool TryParseShellEnvironmentSetEntry(
            string key,
            string value,
            Dictionary<string, string> destination)
        {
            if (!CopilotCodexShellEnvironmentPolicy.IsValidEnvironmentVariableName(key)
                || destination.Count >= MaximumShellEnvironmentSetEntries
                || destination.ContainsKey(key)
                || !TryParseShellEnvironmentStringValue(
                    value,
                    MaximumShellEnvironmentValueCharacters,
                    out var configuredValue))
            {
                return false;
            }
            destination.Add(key, configuredValue);
            return true;
        }

        private static bool TryParseShellEnvironmentStringValue(
            string value,
            int maximumCharacters,
            out string configuredValue)
        {
            configuredValue = string.Empty;
            var index = 0;
            SkipWhitespace(value, ref index);
            if (!TryReadTomlString(value, ref index, out var parsed)
                || parsed.Length > maximumCharacters
                || parsed.IndexOf('\0') >= 0)
            {
                return false;
            }
            SkipWhitespace(value, ref index);
            if (index != value.Length)
                return false;
            configuredValue = parsed;
            return true;
        }

        private static bool TryParseShellEnvironmentFilterEntry(
            string key,
            string value,
            Dictionary<string, CopilotCodexShellEnvironmentFilter> destination)
        {
            if (!TryParseShellEnvironmentStringValue(value, 16, out var action))
                return false;
            return TryAddShellEnvironmentFilter(key, action, destination);
        }

        private static bool TryAddShellEnvironmentFilter(
            string pattern,
            string action,
            Dictionary<string, CopilotCodexShellEnvironmentFilter> destination)
        {
            if (!CopilotCodexShellEnvironmentPolicy.IsValidPattern(pattern)
                || destination.Count >= MaximumShellEnvironmentPatterns
                || destination.ContainsKey(pattern))
            {
                return false;
            }
            var filter = action switch
            {
                "include" => CopilotCodexShellEnvironmentFilter.Include,
                "exclude" => CopilotCodexShellEnvironmentFilter.Exclude,
                _ => (CopilotCodexShellEnvironmentFilter?)null,
            };
            if (!filter.HasValue)
                return false;
            destination.Add(pattern, filter.Value);
            return true;
        }

        private static string ReadContinuedShellEnvironmentValue(
            string[] lines,
            ref int index,
            string value,
            Func<string, bool> isClosed)
        {
            var builder = new StringBuilder(value);
            for (var logicalLine = 1;
                logicalLine < MaximumShellEnvironmentLogicalValueLines
                    && index + 1 < lines.Length;
                logicalLine++)
            {
                index++;
                var continuation = StripComment(lines[index]).Trim();
                if (continuation.Length > 0)
                    builder.Append(' ').Append(continuation);
                if (isClosed(builder.ToString()))
                    break;
            }
            return builder.ToString();
        }

        private static int FindTomlAssignmentEquals(string line)
        {
            var quote = '\0';
            var escaped = false;
            for (var index = 0; index < line.Length; index++)
            {
                var current = line[index];
                if (quote == '"')
                {
                    if (escaped)
                    {
                        escaped = false;
                        continue;
                    }
                    if (current == '\\')
                    {
                        escaped = true;
                        continue;
                    }
                    if (current == quote)
                        quote = '\0';
                    continue;
                }
                if (quote == '\'')
                {
                    if (current == quote)
                        quote = '\0';
                    continue;
                }
                if (current is '"' or '\'')
                {
                    quote = current;
                    continue;
                }
                if (current == '=')
                    return index;
            }
            return -1;
        }

        private static bool TryParseTomlKey(string value, out string key)
        {
            var index = 0;
            if (!TryReadTomlKey(value, ref index, out key))
                return false;
            SkipWhitespace(value, ref index);
            return index == value.Length;
        }

        private static bool TryReadTomlKey(string value, ref int index, out string key)
        {
            SkipWhitespace(value, ref index);
            if (index < value.Length && value[index] is '"' or '\'')
                return TryReadTomlString(value, ref index, out key);
            return TryReadBareTomlKey(value, ref index, out key);
        }
    }
}
