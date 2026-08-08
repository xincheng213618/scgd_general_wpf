using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ColorVision.Copilot
{
    internal enum CopilotCodexCustomSubagentDiscoveryIssueKind
    {
        UnreadableOrUnsafe,
        InvalidDefinition,
        DuplicateName,
        LimitExceeded,
    }

    internal sealed record CopilotCodexCustomSubagentDiscoveryIssue(
        string FileName,
        CopilotProjectInstructionConfigSources Source,
        CopilotCodexCustomSubagentDiscoveryIssueKind Kind);

    internal sealed record CopilotCodexCustomSubagentDefinition
    {
        internal const int MaximumNameCharacters = 64;
        internal const int MaximumDescriptionCharacters = 1_200;
        internal const int MaximumDeveloperInstructionCharacters = 64 * 1024;

        public string Name { get; init; } = string.Empty;

        public string Description { get; init; } = string.Empty;

        public string DeveloperInstructions { get; init; } = string.Empty;

        public string Model { get; init; } = string.Empty;

        public CopilotCodexReasoningEffort ReasoningEffort { get; init; } =
            CopilotCodexReasoningEffort.Unspecified;

        public CopilotCodexReasoningSummary ReasoningSummary { get; init; } =
            CopilotCodexReasoningSummary.Unspecified;

        public string ServiceTier { get; init; } = string.Empty;

        public CopilotCodexModelVerbosity ModelVerbosity { get; init; } =
            CopilotCodexModelVerbosity.Unspecified;

        public CopilotProjectInstructionConfigSources Source { get; init; }

        public string SourceFilePath { get; init; } = string.Empty;

        public bool HasIgnoredSettings { get; init; }

        public CopilotCodexCustomSubagentDefinition CreateSnapshot() => this with { };
    }

    internal static partial class CopilotProjectInstructionDiscoveryConfig
    {
        private const int MaximumCustomSubagents = 24;
        private const int MaximumCustomSubagentDiscoveryIssues = 32;
        private const string CustomAgentsDirectoryName = "agents";
        private const string CustomAgentNameKey = "name";
        private const string CustomAgentDescriptionKey = "description";
        private const string CustomAgentDeveloperInstructionsKey = "developer_instructions";
        private const string CustomAgentModelKey = "model";
        private const string CustomAgentReasoningEffortKey = "model_reasoning_effort";
        private const string CustomAgentReasoningSummaryKey = "model_reasoning_summary";
        private const string CustomAgentServiceTierKey = "service_tier";
        private const string CustomAgentModelVerbosityKey = "model_verbosity";

        private static IReadOnlyList<CopilotCodexCustomSubagentDefinition> DiscoverCodexHomeCustomSubagents(
            string normalizedCodexHomePath,
            out IReadOnlyList<CopilotCodexCustomSubagentDiscoveryIssue> discoveryIssues)
        {
            var issues = new List<CopilotCodexCustomSubagentDiscoveryIssue>();
            discoveryIssues = Array.Empty<CopilotCodexCustomSubagentDiscoveryIssue>();
            if (normalizedCodexHomePath.Length == 0)
                return Array.Empty<CopilotCodexCustomSubagentDefinition>();

            var definitions = new Dictionary<string, CopilotCodexCustomSubagentDefinition>(
                StringComparer.OrdinalIgnoreCase);
            ApplyCustomSubagentDirectory(
                definitions,
                normalizedCodexHomePath,
                Path.Combine(normalizedCodexHomePath, CustomAgentsDirectoryName),
                CopilotProjectInstructionConfigSources.CodexHome,
                issues);
            discoveryIssues = issues.ToArray();
            return CreateCustomSubagentSnapshot(definitions);
        }

        private static IReadOnlyList<CopilotCodexCustomSubagentDefinition> ApplyTrustedProjectCustomSubagents(
            IReadOnlyList<CopilotCodexCustomSubagentDefinition> current,
            IReadOnlyList<CopilotCodexCustomSubagentDiscoveryIssue> currentIssues,
            string normalizedProjectRoot,
            IReadOnlyList<string> projectConfigDirectories,
            out IReadOnlyList<CopilotCodexCustomSubagentDiscoveryIssue> discoveryIssues)
        {
            var definitions = (current ?? Array.Empty<CopilotCodexCustomSubagentDefinition>())
                .Where(definition => definition != null)
                .ToDictionary(
                    definition => definition.Name,
                    definition => definition.CreateSnapshot(),
                    StringComparer.OrdinalIgnoreCase);
            var issues = (currentIssues ?? Array.Empty<CopilotCodexCustomSubagentDiscoveryIssue>())
                .Where(issue => issue != null)
                .Take(MaximumCustomSubagentDiscoveryIssues)
                .ToList();
            foreach (var directoryPath in projectConfigDirectories ?? Array.Empty<string>())
            {
                ApplyCustomSubagentDirectory(
                    definitions,
                    normalizedProjectRoot,
                    Path.Combine(directoryPath, ".codex", CustomAgentsDirectoryName),
                    CopilotProjectInstructionConfigSources.TrustedProject,
                    issues);
            }
            discoveryIssues = issues.ToArray();
            return CreateCustomSubagentSnapshot(definitions);
        }

        private static void ApplyCustomSubagentDirectory(
            Dictionary<string, CopilotCodexCustomSubagentDefinition> definitions,
            string allowedRootPath,
            string directoryPath,
            CopilotProjectInstructionConfigSources source,
            List<CopilotCodexCustomSubagentDiscoveryIssue> issues)
        {
            var namesInDirectory = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var filePath in EnumerateCustomSubagentFiles(allowedRootPath, directoryPath))
            {
                if (!TryReadConfigSource(allowedRootPath, filePath, out var configSource))
                {
                    AddCustomSubagentDiscoveryIssue(
                        issues,
                        filePath,
                        source,
                        CopilotCodexCustomSubagentDiscoveryIssueKind.UnreadableOrUnsafe);
                    continue;
                }
                if (!TryParseCustomSubagent(configSource, source, filePath, out var definition))
                {
                    AddCustomSubagentDiscoveryIssue(
                        issues,
                        filePath,
                        source,
                        CopilotCodexCustomSubagentDiscoveryIssueKind.InvalidDefinition);
                    continue;
                }
                if (!namesInDirectory.Add(definition.Name))
                {
                    AddCustomSubagentDiscoveryIssue(
                        issues,
                        filePath,
                        source,
                        CopilotCodexCustomSubagentDiscoveryIssueKind.DuplicateName);
                    continue;
                }

                if (!definitions.ContainsKey(definition.Name)
                    && definitions.Count >= MaximumCustomSubagents)
                {
                    AddCustomSubagentDiscoveryIssue(
                        issues,
                        filePath,
                        source,
                        CopilotCodexCustomSubagentDiscoveryIssueKind.LimitExceeded);
                    continue;
                }
                definitions[definition.Name] = definition;
            }
        }

        private static void AddCustomSubagentDiscoveryIssue(
            List<CopilotCodexCustomSubagentDiscoveryIssue> issues,
            string filePath,
            CopilotProjectInstructionConfigSources source,
            CopilotCodexCustomSubagentDiscoveryIssueKind kind)
        {
            if (issues.Count >= MaximumCustomSubagentDiscoveryIssues)
                return;
            var fileName = Path.GetFileName(filePath);
            if (string.IsNullOrWhiteSpace(fileName))
                fileName = "unknown.toml";
            issues.Add(new CopilotCodexCustomSubagentDiscoveryIssue(
                fileName.Length <= 160 ? fileName : fileName[..160],
                source,
                kind));
        }

        private static IReadOnlyList<string> EnumerateCustomSubagentFiles(
            string allowedRootPath,
            string directoryPath)
        {
            try
            {
                var fullDirectoryPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directoryPath));
                var directory = new DirectoryInfo(fullDirectoryPath);
                if (!directory.Exists
                    || (directory.Attributes & FileAttributes.ReparsePoint) != 0
                    || CopilotWorkspaceSearchSupport.HasReparsePointInPath(fullDirectoryPath)
                    || !CopilotWorkspaceSearchSupport.IsPathWithinRoots(fullDirectoryPath, [allowedRootPath]))
                {
                    return Array.Empty<string>();
                }

                return Directory.EnumerateFiles(fullDirectoryPath, "*.toml", SearchOption.TopDirectoryOnly)
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .Take(MaximumCustomSubagents + MaximumCustomSubagentDiscoveryIssues)
                    .ToArray();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        private static bool TryParseCustomSubagent(
            string sourceText,
            CopilotProjectInstructionConfigSources source,
            string sourceFilePath,
            out CopilotCodexCustomSubagentDefinition definition)
        {
            definition = new CopilotCodexCustomSubagentDefinition();
            var assignments = new Dictionary<string, string>(StringComparer.Ordinal);
            var hasIgnoredSettings = false;
            foreach (var assignment in EnumerateCustomSubagentAssignments(sourceText))
            {
                if (!IsSupportedCustomSubagentKey(assignment.Key))
                {
                    hasIgnoredSettings = true;
                    continue;
                }
                if (!assignments.TryAdd(assignment.Key, assignment.Value))
                    return false;
            }

            if (!assignments.TryGetValue(CustomAgentNameKey, out var nameValue)
                || !TryParseConfiguredText(nameValue, CopilotCodexCustomSubagentDefinition.MaximumNameCharacters, out var name)
                || !IsValidCustomSubagentName(name)
                || !assignments.TryGetValue(CustomAgentDescriptionKey, out var descriptionValue)
                || !TryParseConfiguredText(descriptionValue, CopilotCodexCustomSubagentDefinition.MaximumDescriptionCharacters, out var description)
                || description.Length == 0
                || !assignments.TryGetValue(CustomAgentDeveloperInstructionsKey, out var instructionsValue)
                || !TryParseConfiguredText(instructionsValue, CopilotCodexCustomSubagentDefinition.MaximumDeveloperInstructionCharacters, out var developerInstructions)
                || developerInstructions.Length == 0)
            {
                return false;
            }

            var model = string.Empty;
            if (assignments.TryGetValue(CustomAgentModelKey, out var modelValue)
                && (!TryParseConfiguredText(modelValue, CopilotConfiguredModelSelection.MaximumModelCharacters, out var configuredModel)
                    || !CopilotConfiguredModelSelection.TryNormalize(configuredModel, out model)))
            {
                return false;
            }

            var reasoningEffort = CopilotCodexReasoningEffort.Unspecified;
            if (assignments.TryGetValue(CustomAgentReasoningEffortKey, out var effortValue)
                && (!TryParseConfiguredText(effortValue, 32, out var configuredEffort)
                    || !CopilotCodexReasoningEffortSelection.TryParse(configuredEffort, out reasoningEffort)))
            {
                return false;
            }

            var reasoningSummary = CopilotCodexReasoningSummary.Unspecified;
            if (assignments.TryGetValue(CustomAgentReasoningSummaryKey, out var summaryValue)
                && (!TryParseConfiguredText(summaryValue, 32, out var configuredSummary)
                    || !CopilotCodexReasoningSummarySelection.TryParse(configuredSummary, out reasoningSummary)))
            {
                return false;
            }

            var serviceTier = string.Empty;
            if (assignments.TryGetValue(CustomAgentServiceTierKey, out var serviceTierValue)
                && (!TryParseConfiguredText(
                        serviceTierValue,
                        CopilotCodexServiceTierSelection.MaximumCharacters,
                        out var configuredServiceTier)
                    || !CopilotCodexServiceTierSelection.TryNormalize(configuredServiceTier, out serviceTier)))
            {
                return false;
            }

            var modelVerbosity = CopilotCodexModelVerbosity.Unspecified;
            if (assignments.TryGetValue(CustomAgentModelVerbosityKey, out var verbosityValue)
                && (!TryParseConfiguredText(verbosityValue, 32, out var configuredVerbosity)
                    || !CopilotCodexModelVerbositySelection.TryParse(configuredVerbosity, out modelVerbosity)))
            {
                return false;
            }

            definition = new CopilotCodexCustomSubagentDefinition
            {
                Name = name,
                Description = description,
                DeveloperInstructions = developerInstructions,
                Model = model,
                ReasoningEffort = reasoningEffort,
                ReasoningSummary = reasoningSummary,
                ServiceTier = serviceTier,
                ModelVerbosity = modelVerbosity,
                Source = source,
                SourceFilePath = Path.GetFullPath(sourceFilePath),
                HasIgnoredSettings = hasIgnoredSettings,
            };
            return true;
        }

        private static IEnumerable<CustomSubagentTomlAssignment> EnumerateCustomSubagentAssignments(string source)
        {
            var lines = NormalizeLines(source);
            var inRootTable = true;
            for (var index = 0; index < lines.Length; index++)
            {
                var line = StripComment(lines[index]).Trim();
                if (line.Length == 0)
                    continue;
                if (line[0] == '[')
                {
                    inRootTable = false;
                    yield return new CustomSubagentTomlAssignment(string.Empty, string.Empty);
                    continue;
                }
                if (!inRootTable)
                    continue;

                var equalsIndex = line.IndexOf('=');
                if (equalsIndex <= 0)
                    continue;
                var key = line[..equalsIndex].Trim();
                var value = line[(equalsIndex + 1)..].Trim();
                if ((string.Equals(key, CustomAgentDescriptionKey, StringComparison.Ordinal)
                        || string.Equals(key, CustomAgentDeveloperInstructionsKey, StringComparison.Ordinal))
                    && TryGetMultilineStringDelimiter(value, out var delimiter)
                    && !HasClosedMultilineString(value, delimiter))
                {
                    var builder = new StringBuilder(value);
                    for (var logicalLine = 1;
                        logicalLine < MaximumConfiguredTextLines && index + 1 < lines.Length;
                        logicalLine++)
                    {
                        index++;
                        builder.Append('\n').Append(lines[index]);
                        if (HasClosedMultilineString(builder.ToString(), delimiter))
                            break;
                    }
                    value = builder.ToString();
                }
                yield return new CustomSubagentTomlAssignment(key, value);
            }
        }

        private static bool IsSupportedCustomSubagentKey(string key) =>
            string.Equals(key, CustomAgentNameKey, StringComparison.Ordinal)
            || string.Equals(key, CustomAgentDescriptionKey, StringComparison.Ordinal)
            || string.Equals(key, CustomAgentDeveloperInstructionsKey, StringComparison.Ordinal)
            || string.Equals(key, CustomAgentModelKey, StringComparison.Ordinal)
            || string.Equals(key, CustomAgentReasoningEffortKey, StringComparison.Ordinal)
            || string.Equals(key, CustomAgentReasoningSummaryKey, StringComparison.Ordinal)
            || string.Equals(key, CustomAgentServiceTierKey, StringComparison.Ordinal)
            || string.Equals(key, CustomAgentModelVerbosityKey, StringComparison.Ordinal);

        private static bool IsValidCustomSubagentName(string name)
        {
            if (name.Length is 0 or > CopilotCodexCustomSubagentDefinition.MaximumNameCharacters
                || !char.IsAsciiLetter(name[0]))
            {
                return false;
            }
            return name.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');
        }

        private static IReadOnlyList<CopilotCodexCustomSubagentDefinition> CreateCustomSubagentSnapshot(
            Dictionary<string, CopilotCodexCustomSubagentDefinition> definitions) =>
            definitions.Values
                .OrderBy(definition => definition.Name, StringComparer.OrdinalIgnoreCase)
                .Select(definition => definition.CreateSnapshot())
                .ToArray();

        private readonly record struct CustomSubagentTomlAssignment(string Key, string Value);
    }

    internal static class CopilotCodexCustomSubagentSelection
    {
        public static bool TryNormalizeName(string? value, out string name)
        {
            name = (value ?? string.Empty).Trim();
            if (name.Length is 0 or > CopilotCodexCustomSubagentDefinition.MaximumNameCharacters
                || !char.IsAsciiLetter(name[0]))
            {
                name = string.Empty;
                return false;
            }
            if (!name.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_'))
            {
                name = string.Empty;
                return false;
            }
            return true;
        }

        public static CopilotCodexCustomSubagentDefinition? Find(
            IReadOnlyList<CopilotCodexCustomSubagentDefinition>? definitions,
            string? name)
        {
            var normalizedName = (name ?? string.Empty).Trim();
            if (normalizedName.Length == 0)
                return null;
            return (definitions ?? Array.Empty<CopilotCodexCustomSubagentDefinition>())
                .FirstOrDefault(definition => definition != null
                    && string.Equals(definition.Name, normalizedName, StringComparison.OrdinalIgnoreCase));
        }
    }

    internal static class CopilotCodexCustomSubagentDiagnostics
    {
        public static string Format(
            IReadOnlyList<CopilotCodexCustomSubagentDefinition>? definitions)
        {
            var available = (definitions ?? Array.Empty<CopilotCodexCustomSubagentDefinition>())
                .Where(definition => definition != null)
                .Take(24)
                .ToArray();
            if (available.Length == 0)
                return string.Empty;

            var builder = new StringBuilder()
                .Append("Codex custom agents：")
                .Append(available.Length)
                .AppendLine(" · 提交快照；只附加指令与运行默认值，不扩大 Explore/Scout 的只读工具和权限边界");
            foreach (var definition in available)
            {
                builder.Append("  - ")
                    .Append(definition.Name)
                    .Append(" · ")
                    .Append(CollapseDiagnosticText(definition.Description, 160))
                    .Append(" · 来源 ")
                    .Append(definition.Source == CopilotProjectInstructionConfigSources.TrustedProject
                        ? "受信项目"
                        : "Codex Home")
                    .Append(" · model ")
                    .Append(string.IsNullOrWhiteSpace(definition.Model) ? "inherited" : definition.Model)
                    .Append(" · reasoning ")
                    .Append(definition.ReasoningEffort == CopilotCodexReasoningEffort.Unspecified
                        ? "inherited"
                        : CopilotCodexReasoningEffortSelection.GetConfigToken(definition.ReasoningEffort))
                    .Append(" · summary ")
                    .Append(definition.ReasoningSummary == CopilotCodexReasoningSummary.Unspecified
                        ? "inherited"
                        : CopilotCodexReasoningSummarySelection.GetConfigToken(definition.ReasoningSummary))
                    .Append(" · verbosity ")
                    .Append(definition.ModelVerbosity == CopilotCodexModelVerbosity.Unspecified
                        ? "inherited"
                        : CopilotCodexModelVerbositySelection.GetConfigToken(definition.ModelVerbosity))
                    .Append(" · service_tier ")
                    .Append(string.IsNullOrWhiteSpace(definition.ServiceTier)
                        ? "inherited"
                        : definition.ServiceTier);
                if (definition.HasIgnoredSettings)
                    builder.Append(" · 未支持设置已忽略");
                builder.AppendLine();
            }
            return builder.ToString().TrimEnd();
        }

        public static string FormatDiscoveryIssues(
            IReadOnlyList<CopilotCodexCustomSubagentDiscoveryIssue>? issues)
        {
            var available = (issues ?? Array.Empty<CopilotCodexCustomSubagentDiscoveryIssue>())
                .Where(issue => issue != null)
                .Take(32)
                .ToArray();
            if (available.Length == 0)
                return string.Empty;

            var builder = new StringBuilder()
                .Append("Codex custom agent 发现问题：")
                .Append(available.Length)
                .AppendLine(" · 仅本地诊断；不会注入模型提示");
            foreach (var issue in available)
            {
                builder.Append("  - ")
                    .Append(CollapseDiagnosticText(issue.FileName, 160))
                    .Append(" · 来源 ")
                    .Append(issue.Source == CopilotProjectInstructionConfigSources.TrustedProject
                        ? "受信项目"
                        : "Codex Home")
                    .Append(" · ")
                    .AppendLine(issue.Kind switch
                    {
                        CopilotCodexCustomSubagentDiscoveryIssueKind.UnreadableOrUnsafe => "文件不可读、过大或不满足安全路径约束，已跳过",
                        CopilotCodexCustomSubagentDiscoveryIssueKind.DuplicateName => "同目录已有同名 Agent，保留按文件名排序后的首个定义",
                        CopilotCodexCustomSubagentDiscoveryIssueKind.LimitExceeded => "超过 24 个有效 Agent 上限，已跳过",
                        _ => "定义无效或缺少必填字段，已跳过",
                    });
            }
            return builder.ToString().TrimEnd();
        }

        private static string CollapseDiagnosticText(string? value, int maximumCharacters)
        {
            var normalized = string.Join(" ", (value ?? string.Empty).Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            normalized = new string(normalized
                .Select(character => char.IsControl(character) ? ' ' : character)
                .ToArray());
            return normalized.Length <= maximumCharacters
                ? normalized
                : normalized[..maximumCharacters] + "…";
        }
    }
}
