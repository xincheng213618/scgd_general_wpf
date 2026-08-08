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
        public IReadOnlyList<string> ProjectRootMarkers { get; init; } =
            CopilotProjectInstructionDiscoveryConfig.DefaultProjectRootMarkers;

        public bool HasProjectRootMarkersOverride { get; init; }

        public IReadOnlyList<string> AppliedProjectConfigFilePaths { get; init; } = Array.Empty<string>();

        public string DeveloperInstructions { get; init; } = string.Empty;

        public bool HasDeveloperInstructionsOverride { get; init; }

        public CopilotProjectInstructionConfigSources DeveloperInstructionsSource { get; init; } =
            CopilotProjectInstructionConfigSources.None;

        public bool UsesCodexConfig => ConfigSources != CopilotProjectInstructionConfigSources.None
            || HasMaximumBytesOverride
            || HasFallbackFileNamesOverride
            || HasProjectRootMarkersOverride
            || HasDeveloperInstructionsOverride;

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

        public string DeveloperInstructionsSourceLabel => DeveloperInstructionsSource switch
        {
            CopilotProjectInstructionConfigSources.CodexHome => "Codex Home config.toml",
            CopilotProjectInstructionConfigSources.TrustedProject => "受信项目 .codex/config.toml",
            _ => string.Empty,
        };

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

    internal sealed record CopilotCodexHomeConfigSnapshot(
        string Source,
        CopilotProjectInstructionDiscoveryOptions Options);

    internal static class CopilotProjectInstructionDiscoveryConfig
    {
        internal const int DefaultMaximumBytes = 32 * 1024;
        internal const int MinimumMaximumBytes = 0;
        internal const int MaximumMaximumBytes = 64 * 1024;
        internal static IReadOnlyList<string> DefaultProjectRootMarkers { get; } =
            Array.AsReadOnly([".git"]);

        private const int MaximumConfigBytes = 256 * 1024;
        private const int MaximumFallbackFileNames = 16;
        private const int MaximumFallbackFileNameCharacters = 128;
        internal const int MaximumProjectRootMarkers = 16;
        private const int MaximumProjectRootMarkerCharacters = 128;
        private const int MaximumLogicalValueLines = 64;
        internal const int MaximumDeveloperInstructionCharacters = 64 * 1024;
        private const int MaximumDeveloperInstructionLines = 512;
        private const string ConfigFileName = "config.toml";
        private const string MaximumBytesKey = "project_doc_max_bytes";
        private const string FallbackFileNamesKey = "project_doc_fallback_filenames";
        private const string ProjectRootMarkersKey = "project_root_markers";
        private const string DeveloperInstructionsKey = "developer_instructions";
        private const string ProjectsTablePrefix = "projects.";
        private const string TrustLevelKey = "trust_level";

        public static CopilotProjectInstructionDiscoveryOptions Load(string? globalInstructionRootPath)
            => LoadCodexHome(globalInstructionRootPath).Options;

        public static CopilotProjectInstructionDiscoveryOptions Load(
            string? globalInstructionRootPath,
            string? trustedProjectRootPath)
            => LoadTrustedProjectLayer(LoadCodexHome(globalInstructionRootPath), trustedProjectRootPath);

        internal static CopilotCodexHomeConfigSnapshot LoadCodexHome(string? globalInstructionRootPath)
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
                    CopilotProjectInstructionConfigSources.CodexHome,
                    includeProjectRootMarkers: true);
            }

            return new CopilotCodexHomeConfigSnapshot(globalSource, options);
        }

        internal static CopilotProjectInstructionDiscoveryOptions LoadTrustedProjectLayer(
            CopilotCodexHomeConfigSnapshot codexHome,
            string? trustedProjectRootPath)
            => LoadTrustedProjectLayers(codexHome, trustedProjectRootPath, trustedProjectRootPath);

        internal static CopilotProjectInstructionDiscoveryOptions LoadTrustedProjectLayers(
            CopilotCodexHomeConfigSnapshot codexHome,
            string? trustedProjectRootPath,
            string? workingDirectoryPath)
        {
            ArgumentNullException.ThrowIfNull(codexHome);
            var options = codexHome.Options;
            var normalizedProjectRoot = NormalizeTrustedProjectRootPath(trustedProjectRootPath);
            if (normalizedProjectRoot.Length == 0)
                return options;

            var projectTrustLevel = ResolveProjectTrustLevel(codexHome.Source, normalizedProjectRoot);
            options = options with { ProjectTrustLevel = projectTrustLevel };
            if (!options.AllowsProjectCodexConfig)
                return options;

            var appliedConfigFilePaths = new List<string>();
            foreach (var directoryPath in EnumerateProjectConfigDirectories(
                normalizedProjectRoot,
                workingDirectoryPath))
            {
                var configPath = Path.Combine(directoryPath, ".codex", ConfigFileName);
                if (!TryReadConfigSource(normalizedProjectRoot, configPath, out var projectSource)
                    || !TryParseInstructionLayer(projectSource, out var projectLayer)
                    || !HasApplicableOverrides(projectLayer, includeProjectRootMarkers: false))
                {
                    continue;
                }

                options = ApplyLayer(
                    options,
                    projectLayer,
                    CopilotProjectInstructionConfigSources.TrustedProject,
                    includeProjectRootMarkers: false);
                appliedConfigFilePaths.Add(Path.GetFullPath(configPath));
            }

            return options with
            {
                AppliedProjectConfigFilePaths = appliedConfigFilePaths.ToArray(),
            };
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
            CopilotProjectInstructionConfigSources source,
            bool includeProjectRootMarkers)
        {
            var hasProjectRootMarkersOverride = includeProjectRootMarkers
                && layer.HasProjectRootMarkersOverride;
            if (!HasApplicableOverrides(layer, includeProjectRootMarkers))
                return current;

            return current with
            {
                MaximumBytes = layer.HasMaximumBytesOverride ? layer.MaximumBytes : current.MaximumBytes,
                FallbackFileNames = layer.HasFallbackFileNamesOverride ? layer.FallbackFileNames : current.FallbackFileNames,
                HasMaximumBytesOverride = current.HasMaximumBytesOverride || layer.HasMaximumBytesOverride,
                HasFallbackFileNamesOverride = current.HasFallbackFileNamesOverride || layer.HasFallbackFileNamesOverride,
                ConfigSources = current.ConfigSources | source,
                ProjectRootMarkers = hasProjectRootMarkersOverride
                    ? layer.ProjectRootMarkers
                    : current.ProjectRootMarkers,
                HasProjectRootMarkersOverride = current.HasProjectRootMarkersOverride
                    || hasProjectRootMarkersOverride,
                DeveloperInstructions = layer.HasDeveloperInstructionsOverride
                    ? layer.DeveloperInstructions
                    : current.DeveloperInstructions,
                HasDeveloperInstructionsOverride = current.HasDeveloperInstructionsOverride
                    || layer.HasDeveloperInstructionsOverride,
                DeveloperInstructionsSource = layer.HasDeveloperInstructionsOverride
                    ? source
                    : current.DeveloperInstructionsSource,
            };
        }

        private static bool HasApplicableOverrides(
            ProjectInstructionConfigLayer layer,
            bool includeProjectRootMarkers)
        {
            return layer.HasMaximumBytesOverride
                || layer.HasFallbackFileNamesOverride
                || layer.HasDeveloperInstructionsOverride
                || (includeProjectRootMarkers && layer.HasProjectRootMarkersOverride);
        }

        private static IReadOnlyList<string> EnumerateProjectConfigDirectories(
            string normalizedProjectRoot,
            string? workingDirectoryPath)
        {
            var normalizedWorkingDirectory = NormalizeProjectWorkingDirectoryPath(
                workingDirectoryPath,
                normalizedProjectRoot);
            var directories = new List<string>();
            try
            {
                var current = new DirectoryInfo(normalizedWorkingDirectory);
                while (current != null)
                {
                    var currentPath = Path.TrimEndingDirectorySeparator(current.FullName);
                    if (!CopilotWorkspaceSearchSupport.IsPathWithinRoots(currentPath, [normalizedProjectRoot]))
                        return [normalizedProjectRoot];

                    directories.Add(currentPath);
                    if (string.Equals(currentPath, normalizedProjectRoot, StringComparison.OrdinalIgnoreCase))
                        break;
                    current = current.Parent;
                }
            }
            catch
            {
                return [normalizedProjectRoot];
            }

            if (directories.Count == 0
                || !string.Equals(directories[^1], normalizedProjectRoot, StringComparison.OrdinalIgnoreCase))
            {
                return [normalizedProjectRoot];
            }

            directories.Reverse();
            return directories;
        }

        private static string NormalizeProjectWorkingDirectoryPath(
            string? path,
            string normalizedProjectRoot)
        {
            if (string.IsNullOrWhiteSpace(path) || path.Length > 2_048)
                return normalizedProjectRoot;

            try
            {
                var fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path.Trim()));
                return fullPath.Length <= 2_048
                    && Directory.Exists(fullPath)
                    && !CopilotWorkspaceSearchSupport.HasReparsePointInPath(fullPath)
                    && CopilotWorkspaceSearchSupport.IsPathWithinRoots(fullPath, [normalizedProjectRoot])
                        ? fullPath
                        : normalizedProjectRoot;
            }
            catch
            {
                return normalizedProjectRoot;
            }
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
            var projectRootMarkers = Array.Empty<string>();
            var developerInstructions = string.Empty;
            var hasMaximumBytesOverride = false;
            var hasFallbackFileNamesOverride = false;
            var hasProjectRootMarkersOverride = false;
            var hasDeveloperInstructionsOverride = false;
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

                if (string.Equals(assignment.Key, FallbackFileNamesKey, StringComparison.Ordinal))
                {
                    if (!TryParseFallbackFileNames(assignment.Value, out var configuredFallbackFileNames))
                        continue;
                    fallbackFileNames = configuredFallbackFileNames;
                    hasFallbackFileNamesOverride = true;
                    continue;
                }

                if (string.Equals(assignment.Key, DeveloperInstructionsKey, StringComparison.Ordinal))
                {
                    if (!TryParseDeveloperInstructions(assignment.Value, out var configuredDeveloperInstructions))
                        continue;
                    developerInstructions = configuredDeveloperInstructions;
                    hasDeveloperInstructionsOverride = true;
                    continue;
                }

                if (!string.Equals(assignment.Key, ProjectRootMarkersKey, StringComparison.Ordinal)
                    || !TryParseProjectRootMarkers(assignment.Value, out var configuredProjectRootMarkers))
                {
                    continue;
                }

                projectRootMarkers = configuredProjectRootMarkers;
                hasProjectRootMarkersOverride = true;
            }

            layer = new ProjectInstructionConfigLayer(
                maximumBytes,
                fallbackFileNames,
                projectRootMarkers,
                developerInstructions,
                hasMaximumBytesOverride,
                hasFallbackFileNamesOverride,
                hasProjectRootMarkersOverride,
                hasDeveloperInstructionsOverride);
            return hasMaximumBytesOverride
                || hasFallbackFileNamesOverride
                || hasProjectRootMarkersOverride
                || hasDeveloperInstructionsOverride;
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
                    && !string.Equals(key, FallbackFileNamesKey, StringComparison.Ordinal)
                    && !string.Equals(key, ProjectRootMarkersKey, StringComparison.Ordinal)
                    && !string.Equals(key, DeveloperInstructionsKey, StringComparison.Ordinal))
                {
                    continue;
                }

                var value = line[(equalsIndex + 1)..].Trim();
                if ((string.Equals(key, FallbackFileNamesKey, StringComparison.Ordinal)
                        || string.Equals(key, ProjectRootMarkersKey, StringComparison.Ordinal))
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
                else if (string.Equals(key, DeveloperInstructionsKey, StringComparison.Ordinal)
                    && TryGetMultilineStringDelimiter(value, out var delimiter)
                    && !HasClosedMultilineString(value, delimiter))
                {
                    var builder = new StringBuilder(value);
                    for (var logicalLine = 1;
                        logicalLine < MaximumDeveloperInstructionLines && index + 1 < lines.Length;
                        logicalLine++)
                    {
                        index++;
                        builder.Append('\n').Append(lines[index]);
                        if (HasClosedMultilineString(builder.ToString(), delimiter))
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

        private static bool TryParseProjectRootMarkers(string value, out string[] projectRootMarkers)
        {
            projectRootMarkers = Array.Empty<string>();
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
                    projectRootMarkers = results.ToArray();
                    return true;
                }
                if (!expectValue
                    || !TryReadTomlString(value, ref index, out var candidate)
                    || results.Count >= MaximumProjectRootMarkers)
                {
                    return false;
                }

                var normalized = NormalizeProjectRootMarker(candidate);
                if (normalized.Length == 0)
                    return false;
                if (!results.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                    results.Add(normalized);

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

        private static bool TryParseDeveloperInstructions(
            string value,
            out string developerInstructions)
        {
            developerInstructions = string.Empty;
            var index = 0;
            SkipWhitespace(value, ref index);
            string parsed;
            if (TryGetMultilineStringDelimiter(value[index..], out var delimiter))
            {
                if (!TryReadTomlMultilineString(value, ref index, delimiter, out parsed))
                    return false;
            }
            else if (!TryReadTomlString(value, ref index, out parsed))
            {
                return false;
            }

            var trailing = StripComment(value[index..]).Trim();
            if (trailing.Length > 0)
                return false;

            parsed = parsed.Trim();
            if (parsed.Length > MaximumDeveloperInstructionCharacters)
                return false;
            developerInstructions = parsed;
            return true;
        }

        private static bool TryGetMultilineStringDelimiter(string value, out string delimiter)
        {
            delimiter = string.Empty;
            var normalized = value.AsSpan().TrimStart();
            if (normalized.StartsWith("\"\"\"", StringComparison.Ordinal))
            {
                delimiter = "\"\"\"";
                return true;
            }
            if (normalized.StartsWith("'''", StringComparison.Ordinal))
            {
                delimiter = "'''";
                return true;
            }
            return false;
        }

        private static bool HasClosedMultilineString(string value, string delimiter)
        {
            var start = value.IndexOf(delimiter, StringComparison.Ordinal);
            return start >= 0
                && FindClosingMultilineString(value, delimiter, start + delimiter.Length) >= 0;
        }

        private static bool TryReadTomlMultilineString(
            string value,
            ref int index,
            string delimiter,
            out string result)
        {
            result = string.Empty;
            if (!value.AsSpan(index).StartsWith(delimiter, StringComparison.Ordinal))
                return false;

            index += delimiter.Length;
            var closingIndex = FindClosingMultilineString(value, delimiter, index);
            if (closingIndex < 0)
                return false;

            var content = value[index..closingIndex];
            if (content.StartsWith('\n'))
                content = content[1..];
            if (delimiter[0] == '\'')
            {
                result = content;
            }
            else if (!TryDecodeTomlBasicMultilineString(content, out result))
            {
                return false;
            }

            index = closingIndex + delimiter.Length;
            return true;
        }

        private static int FindClosingMultilineString(
            string value,
            string delimiter,
            int startIndex)
        {
            for (var index = startIndex; index <= value.Length - delimiter.Length; index++)
            {
                if (!value.AsSpan(index).StartsWith(delimiter, StringComparison.Ordinal))
                    continue;
                if (delimiter[0] == '\'' || CountPrecedingBackslashes(value, index) % 2 == 0)
                    return index;
            }
            return -1;
        }

        private static int CountPrecedingBackslashes(string value, int index)
        {
            var count = 0;
            while (index > 0 && value[--index] == '\\')
                count++;
            return count;
        }

        private static bool TryDecodeTomlBasicMultilineString(string value, out string result)
        {
            var builder = new StringBuilder(value.Length);
            for (var index = 0; index < value.Length; index++)
            {
                var current = value[index];
                if (current != '\\')
                {
                    if (current == '\0')
                    {
                        result = string.Empty;
                        return false;
                    }
                    builder.Append(current);
                    continue;
                }

                if (++index >= value.Length)
                {
                    result = string.Empty;
                    return false;
                }
                var escaped = value[index];
                if (escaped == '\n')
                {
                    while (index + 1 < value.Length && char.IsWhiteSpace(value[index + 1]))
                        index++;
                    continue;
                }

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
                        index++;
                        if (!TryReadUnicodeEscape(value, ref index, 4, builder))
                        {
                            result = string.Empty;
                            return false;
                        }
                        index--;
                        break;
                    case 'U':
                        index++;
                        if (!TryReadUnicodeEscape(value, ref index, 8, builder))
                        {
                            result = string.Empty;
                            return false;
                        }
                        index--;
                        break;
                    default:
                        result = string.Empty;
                        return false;
                }
            }
            result = builder.ToString();
            return true;
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

        internal static string NormalizeProjectRootMarker(string? value)
        {
            var normalized = (value ?? string.Empty).Trim();
            if (normalized.Length == 0
                || normalized.Length > MaximumProjectRootMarkerCharacters
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
            IReadOnlyList<string> ProjectRootMarkers,
            string DeveloperInstructions,
            bool HasMaximumBytesOverride,
            bool HasFallbackFileNamesOverride,
            bool HasProjectRootMarkersOverride,
            bool HasDeveloperInstructionsOverride);
    }
}
