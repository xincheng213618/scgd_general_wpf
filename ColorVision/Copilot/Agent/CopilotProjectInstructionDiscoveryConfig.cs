using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace ColorVision.Copilot
{
    [Flags]
    internal enum CopilotProjectInstructionConfigSources
    {
        None = 0,
        CodexHome = 1,
        TrustedProject = 2,
    }

    internal enum CopilotCodexProjectTrustLevel
    {
        Unspecified,
        Trusted,
        Untrusted,
        Invalid,
    }

    internal sealed record CopilotProjectInstructionDiscoveryOptions(
        int MaximumBytes,
        IReadOnlyList<string> FallbackFileNames,
        bool HasMaximumBytesOverride,
        bool HasFallbackFileNamesOverride,
        CopilotProjectInstructionConfigSources ConfigSources = CopilotProjectInstructionConfigSources.None,
        CopilotCodexProjectTrustLevel ProjectTrustLevel = CopilotCodexProjectTrustLevel.Unspecified)
    {
        public bool UsesCodexConfig => ConfigSources != CopilotProjectInstructionConfigSources.None
            || HasMaximumBytesOverride
            || HasFallbackFileNamesOverride;

        public string ConfigSourceLabel => ConfigSources switch
        {
            CopilotProjectInstructionConfigSources.CodexHome => "Codex Home config.toml",
            CopilotProjectInstructionConfigSources.TrustedProject => "受信项目 .codex/config.toml",
            CopilotProjectInstructionConfigSources.CodexHome | CopilotProjectInstructionConfigSources.TrustedProject =>
                "Codex Home + 受信项目 .codex/config.toml",
            _ => UsesCodexConfig ? "Codex config.toml" : "ColorVision 默认",
        };

        public bool AllowsProjectCodexConfig => ProjectTrustLevel is not (
            CopilotCodexProjectTrustLevel.Untrusted or CopilotCodexProjectTrustLevel.Invalid);

        public string ProjectTrustLabel => ProjectTrustLevel switch
        {
            CopilotCodexProjectTrustLevel.Trusted => "Codex Home trust_level=trusted",
            CopilotCodexProjectTrustLevel.Untrusted =>
                "Codex Home trust_level=untrusted；已跳过项目 .codex/config.toml",
            CopilotCodexProjectTrustLevel.Invalid =>
                "Codex Home trust_level 无效；已保守跳过项目 .codex/config.toml",
            _ => string.Empty,
        };
    }

    internal static class CopilotProjectInstructionDiscoveryConfig
    {
        internal const int DefaultMaximumBytes = 32 * 1024;
        internal const int MinimumMaximumBytes = 0;
        internal const int MaximumMaximumBytes = 64 * 1024;

        private const int MaximumConfigBytes = 256 * 1024;
        private const int MaximumFallbackFileNames = 16;
        private const int MaximumFallbackFileNameCharacters = 128;
        private const int MaximumLogicalValueLines = 64;
        private const string ConfigFileName = "config.toml";
        private const string MaximumBytesKey = "project_doc_max_bytes";
        private const string FallbackFileNamesKey = "project_doc_fallback_filenames";
        private const string ProjectsTablePrefix = "projects.";
        private const string TrustLevelKey = "trust_level";

        public static CopilotProjectInstructionDiscoveryOptions Load(string? globalInstructionRootPath)
            => Load(globalInstructionRootPath, trustedProjectRootPath: null);

        public static CopilotProjectInstructionDiscoveryOptions Load(
            string? globalInstructionRootPath,
            string? trustedProjectRootPath)
        {
            var options = CreateDefault();
            var normalizedRoot = CopilotAgentProjectInstructions.NormalizeGlobalInstructionRootPath(globalInstructionRootPath);
            var globalSource = string.Empty;
            if (normalizedRoot.Length > 0
                && TryReadConfigSource(
                    normalizedRoot,
                    Path.Combine(normalizedRoot, ConfigFileName),
                    out globalSource)
                && TryParseInstructionLayer(globalSource, out var globalLayer))
            {
                options = ApplyLayer(
                    options,
                    globalLayer,
                    CopilotProjectInstructionConfigSources.CodexHome);
            }

            var normalizedProjectRoot = NormalizeTrustedProjectRootPath(trustedProjectRootPath);
            if (normalizedProjectRoot.Length == 0)
                return options;

            var projectTrustLevel = ResolveProjectTrustLevel(globalSource, normalizedProjectRoot);
            options = options with { ProjectTrustLevel = projectTrustLevel };
            if (options.AllowsProjectCodexConfig
                && TryReadConfigSource(
                    normalizedProjectRoot,
                    Path.Combine(normalizedProjectRoot, ".codex", ConfigFileName),
                    out var projectSource)
                && TryParseInstructionLayer(projectSource, out var projectLayer))
            {
                options = ApplyLayer(
                    options,
                    projectLayer,
                    CopilotProjectInstructionConfigSources.TrustedProject);
            }

            return options;
        }

        public static CopilotProjectInstructionDiscoveryOptions CreateDefault() =>
            new(
                DefaultMaximumBytes,
                Array.Empty<string>(),
                HasMaximumBytesOverride: false,
                HasFallbackFileNamesOverride: false);

        private static CopilotProjectInstructionDiscoveryOptions ApplyLayer(
            CopilotProjectInstructionDiscoveryOptions current,
            ProjectInstructionConfigLayer layer,
            CopilotProjectInstructionConfigSources source)
        {
            return new CopilotProjectInstructionDiscoveryOptions(
                layer.HasMaximumBytesOverride ? layer.MaximumBytes : current.MaximumBytes,
                layer.HasFallbackFileNamesOverride ? layer.FallbackFileNames : current.FallbackFileNames,
                current.HasMaximumBytesOverride || layer.HasMaximumBytesOverride,
                current.HasFallbackFileNamesOverride || layer.HasFallbackFileNamesOverride,
                current.ConfigSources | source,
                current.ProjectTrustLevel);
        }

        private static bool TryReadConfigSource(
            string allowedRootPath,
            string configPath,
            out string source)
        {
            source = string.Empty;
            try
            {
                configPath = Path.GetFullPath(configPath);
                var file = new FileInfo(configPath);
                if (!file.Exists
                    || file.Length <= 0
                    || file.Length > MaximumConfigBytes
                    || (file.Attributes & FileAttributes.ReparsePoint) != 0
                    || CopilotWorkspaceSearchSupport.HasReparsePointInPath(configPath)
                    || !CopilotWorkspaceSearchSupport.IsPathWithinRoots(configPath, [allowedRootPath]))
                {
                    return false;
                }

                using (var stream = new FileStream(configPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
                {
                    if (stream.Length <= 0 || stream.Length > MaximumConfigBytes)
                        return false;
                    var buffer = new char[MaximumConfigBytes + 1];
                    var count = reader.ReadBlock(buffer, 0, buffer.Length);
                    if (count > MaximumConfigBytes || !reader.EndOfStream)
                        return false;
                    source = new string(buffer, 0, count);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryParseInstructionLayer(
            string source,
            out ProjectInstructionConfigLayer layer)
        {
            var maximumBytes = DefaultMaximumBytes;
            var fallbackFileNames = Array.Empty<string>();
            var hasMaximumBytesOverride = false;
            var hasFallbackFileNamesOverride = false;
            foreach (var assignment in EnumerateTopLevelAssignments(source))
            {
                if (string.Equals(assignment.Key, MaximumBytesKey, StringComparison.Ordinal))
                {
                    if (!TryParseMaximumBytes(assignment.Value, out var configuredMaximumBytes))
                        continue;
                    maximumBytes = configuredMaximumBytes;
                    hasMaximumBytesOverride = true;
                    continue;
                }

                if (!string.Equals(assignment.Key, FallbackFileNamesKey, StringComparison.Ordinal)
                    || !TryParseFallbackFileNames(assignment.Value, out var configuredFallbackFileNames))
                {
                    continue;
                }

                fallbackFileNames = configuredFallbackFileNames;
                hasFallbackFileNamesOverride = true;
            }

            layer = new ProjectInstructionConfigLayer(
                maximumBytes,
                fallbackFileNames,
                hasMaximumBytesOverride,
                hasFallbackFileNamesOverride);
            return hasMaximumBytesOverride || hasFallbackFileNamesOverride;
        }

        private static CopilotCodexProjectTrustLevel ResolveProjectTrustLevel(
            string source,
            string normalizedProjectRoot)
        {
            var currentTableMatches = false;
            var hasTrustLevel = false;
            var result = CopilotCodexProjectTrustLevel.Unspecified;
            foreach (var rawLine in NormalizeLines(source))
            {
                var line = StripComment(rawLine).Trim();
                if (line.Length == 0)
                    continue;
                if (line[0] == '[')
                {
                    currentTableMatches = TryParseProjectTableHeader(line, out var configuredProjectPath)
                        && string.Equals(configuredProjectPath, normalizedProjectRoot, StringComparison.OrdinalIgnoreCase);
                    continue;
                }
                if (!currentTableMatches)
                    continue;

                var equalsIndex = line.IndexOf('=');
                if (equalsIndex <= 0
                    || !string.Equals(line[..equalsIndex].Trim(), TrustLevelKey, StringComparison.Ordinal))
                {
                    continue;
                }
                if (hasTrustLevel)
                    return CopilotCodexProjectTrustLevel.Invalid;
                hasTrustLevel = true;

                var value = line[(equalsIndex + 1)..].Trim();
                var index = 0;
                if (!TryReadTomlString(value, ref index, out var trustLevel))
                    return CopilotCodexProjectTrustLevel.Invalid;
                SkipWhitespace(value, ref index);
                if (index != value.Length)
                    return CopilotCodexProjectTrustLevel.Invalid;

                result = trustLevel switch
                {
                    "trusted" => CopilotCodexProjectTrustLevel.Trusted,
                    "untrusted" => CopilotCodexProjectTrustLevel.Untrusted,
                    _ => CopilotCodexProjectTrustLevel.Invalid,
                };
                if (result == CopilotCodexProjectTrustLevel.Invalid)
                    return result;
            }

            return result;
        }

        private static bool TryParseProjectTableHeader(string line, out string normalizedProjectPath)
        {
            normalizedProjectPath = string.Empty;
            if (line.Length < 4
                || line[0] != '['
                || line[^1] != ']'
                || line[1] == '[')
            {
                return false;
            }

            var tableName = line[1..^1].Trim();
            if (!tableName.StartsWith(ProjectsTablePrefix, StringComparison.Ordinal))
                return false;

            var index = ProjectsTablePrefix.Length;
            if (!TryReadTomlString(tableName, ref index, out var configuredProjectPath))
                return false;
            SkipWhitespace(tableName, ref index);
            if (index != tableName.Length)
                return false;

            normalizedProjectPath = NormalizeConfiguredProjectPath(configuredProjectPath);
            return normalizedProjectPath.Length > 0;
        }

        private static string NormalizeConfiguredProjectPath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || path.Length > 2_048)
                return string.Empty;

            try
            {
                var trimmed = path.Trim();
                if (!Path.IsPathFullyQualified(trimmed))
                    return string.Empty;
                return Path.TrimEndingDirectorySeparator(Path.GetFullPath(trimmed));
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string NormalizeTrustedProjectRootPath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || path.Length > 2_048)
                return string.Empty;

            try
            {
                var trimmed = path.Trim();
                if (!Path.IsPathFullyQualified(trimmed))
                    return string.Empty;
                var fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(trimmed));
                return fullPath.Length <= 2_048
                    && Directory.Exists(fullPath)
                    && !CopilotWorkspaceSearchSupport.HasReparsePointInPath(fullPath)
                        ? fullPath
                        : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static IEnumerable<TomlAssignment> EnumerateTopLevelAssignments(string source)
        {
            var lines = NormalizeLines(source);
            for (var index = 0; index < lines.Length; index++)
            {
                var line = StripComment(lines[index]).Trim();
                if (line.Length == 0)
                    continue;
                if (line[0] == '[')
                    yield break;

                var equalsIndex = line.IndexOf('=');
                if (equalsIndex <= 0)
                    continue;
                var key = line[..equalsIndex].Trim();
                if (!string.Equals(key, MaximumBytesKey, StringComparison.Ordinal)
                    && !string.Equals(key, FallbackFileNamesKey, StringComparison.Ordinal))
                {
                    continue;
                }

                var value = line[(equalsIndex + 1)..].Trim();
                if (string.Equals(key, FallbackFileNamesKey, StringComparison.Ordinal)
                    && value.StartsWith('[')
                    && !HasClosedArray(value))
                {
                    var builder = new StringBuilder(value);
                    for (var logicalLine = 1;
                        logicalLine < MaximumLogicalValueLines && index + 1 < lines.Length;
                        logicalLine++)
                    {
                        index++;
                        var continuation = StripComment(lines[index]).Trim();
                        if (continuation.Length > 0)
                            builder.Append(' ').Append(continuation);
                        if (HasClosedArray(builder.ToString()))
                            break;
                    }
                    value = builder.ToString();
                }

                yield return new TomlAssignment(key, value);
            }
        }

        private static string[] NormalizeLines(string source) =>
            (source ?? string.Empty)
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Split('\n');

        private static string StripComment(string line)
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
                if (current == '#')
                    return line[..index];
            }
            return line;
        }

        private static bool HasClosedArray(string value)
        {
            var quote = '\0';
            var escaped = false;
            var depth = 0;
            foreach (var current in value)
            {
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
                if (current == '[')
                    depth++;
                else if (current == ']' && depth > 0 && --depth == 0)
                    return true;
            }
            return false;
        }

        private static bool TryParseMaximumBytes(string value, out int maximumBytes)
        {
            maximumBytes = DefaultMaximumBytes;
            var normalized = (value ?? string.Empty).Replace("_", string.Empty, StringComparison.Ordinal).Trim();
            if (!int.TryParse(normalized, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
                || parsed < MinimumMaximumBytes
                || parsed > MaximumMaximumBytes)
            {
                return false;
            }
            maximumBytes = parsed;
            return true;
        }

        private static bool TryParseFallbackFileNames(string value, out string[] fallbackFileNames)
        {
            fallbackFileNames = Array.Empty<string>();
            var index = 0;
            SkipWhitespace(value, ref index);
            if (!TryConsume(value, ref index, '['))
                return false;

            var results = new List<string>();
            var expectValue = true;
            while (index < value.Length)
            {
                SkipWhitespace(value, ref index);
                if (TryConsume(value, ref index, ']'))
                {
                    SkipWhitespace(value, ref index);
                    if (index != value.Length)
                        return false;
                    fallbackFileNames = results.ToArray();
                    return true;
                }
                if (!expectValue || !TryReadTomlString(value, ref index, out var candidate))
                    return false;

                var normalized = NormalizeFallbackFileName(candidate);
                if (normalized.Length > 0
                    && !results.Contains(normalized, StringComparer.OrdinalIgnoreCase)
                    && results.Count < MaximumFallbackFileNames)
                {
                    results.Add(normalized);
                }

                SkipWhitespace(value, ref index);
                if (TryConsume(value, ref index, ','))
                {
                    expectValue = true;
                    continue;
                }
                expectValue = false;
            }
            return false;
        }

        private static bool TryReadTomlString(string value, ref int index, out string result)
        {
            result = string.Empty;
            if (index >= value.Length || value[index] is not ('"' or '\''))
                return false;

            var quote = value[index++];
            var builder = new StringBuilder();
            while (index < value.Length)
            {
                var current = value[index++];
                if (current == quote)
                {
                    result = builder.ToString();
                    return true;
                }
                if (quote == '\'' || current != '\\')
                {
                    builder.Append(current);
                    continue;
                }
                if (index >= value.Length)
                    return false;

                var escaped = value[index++];
                switch (escaped)
                {
                    case 'b': builder.Append('\b'); break;
                    case 't': builder.Append('\t'); break;
                    case 'n': builder.Append('\n'); break;
                    case 'f': builder.Append('\f'); break;
                    case 'r': builder.Append('\r'); break;
                    case '"': builder.Append('"'); break;
                    case '\\': builder.Append('\\'); break;
                    case 'u':
                        if (!TryReadUnicodeEscape(value, ref index, 4, builder))
                            return false;
                        break;
                    case 'U':
                        if (!TryReadUnicodeEscape(value, ref index, 8, builder))
                            return false;
                        break;
                    default:
                        return false;
                }
            }
            return false;
        }

        private static bool TryReadUnicodeEscape(string value, ref int index, int digits, StringBuilder builder)
        {
            if (index + digits > value.Length
                || !int.TryParse(value.AsSpan(index, digits), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var codePoint)
                || !Rune.IsValid(codePoint))
            {
                return false;
            }
            builder.Append(new Rune(codePoint).ToString());
            index += digits;
            return true;
        }

        private static string NormalizeFallbackFileName(string? value)
        {
            var normalized = (value ?? string.Empty).Trim();
            if (normalized.Length == 0
                || normalized.Length > MaximumFallbackFileNameCharacters
                || normalized is "." or ".."
                || Path.IsPathRooted(normalized)
                || normalized.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                || normalized.Contains(Path.DirectorySeparatorChar)
                || normalized.Contains(Path.AltDirectorySeparatorChar))
            {
                return string.Empty;
            }
            return normalized;
        }

        private static void SkipWhitespace(string value, ref int index)
        {
            while (index < value.Length && char.IsWhiteSpace(value[index]))
                index++;
        }

        private static bool TryConsume(string value, ref int index, char expected)
        {
            if (index >= value.Length || value[index] != expected)
                return false;
            index++;
            return true;
        }

        private sealed record TomlAssignment(string Key, string Value);

        private sealed record ProjectInstructionConfigLayer(
            int MaximumBytes,
            IReadOnlyList<string> FallbackFileNames,
            bool HasMaximumBytesOverride,
            bool HasFallbackFileNamesOverride);
    }
}
