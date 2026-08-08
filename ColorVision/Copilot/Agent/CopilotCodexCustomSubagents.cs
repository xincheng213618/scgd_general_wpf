using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ColorVision.Copilot
{
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

        public CopilotProjectInstructionConfigSources Source { get; init; }

        public string SourceFilePath { get; init; } = string.Empty;

        public bool HasIgnoredSettings { get; init; }

        public CopilotCodexCustomSubagentDefinition CreateSnapshot() => this with { };
    }

    internal static partial class CopilotProjectInstructionDiscoveryConfig
    {
        private const int MaximumCustomSubagents = 24;
        private const string CustomAgentsDirectoryName = "agents";
        private const string CustomAgentNameKey = "name";
        private const string CustomAgentDescriptionKey = "description";
        private const string CustomAgentDeveloperInstructionsKey = "developer_instructions";
        private const string CustomAgentModelKey = "model";
        private const string CustomAgentReasoningEffortKey = "model_reasoning_effort";

        private static IReadOnlyList<CopilotCodexCustomSubagentDefinition> DiscoverCodexHomeCustomSubagents(
            string normalizedCodexHomePath)
        {
            if (normalizedCodexHomePath.Length == 0)
                return Array.Empty<CopilotCodexCustomSubagentDefinition>();

            var definitions = new Dictionary<string, CopilotCodexCustomSubagentDefinition>(
                StringComparer.OrdinalIgnoreCase);
            ApplyCustomSubagentDirectory(
                definitions,
                normalizedCodexHomePath,
                Path.Combine(normalizedCodexHomePath, CustomAgentsDirectoryName),
                CopilotProjectInstructionConfigSources.CodexHome);
            return CreateCustomSubagentSnapshot(definitions);
        }

        private static IReadOnlyList<CopilotCodexCustomSubagentDefinition> ApplyTrustedProjectCustomSubagents(
            IReadOnlyList<CopilotCodexCustomSubagentDefinition> current,
            string normalizedProjectRoot,
            IReadOnlyList<string> projectConfigDirectories)
        {
            var definitions = (current ?? Array.Empty<CopilotCodexCustomSubagentDefinition>())
                .Where(definition => definition != null)
                .ToDictionary(
                    definition => definition.Name,
                    definition => definition.CreateSnapshot(),
                    StringComparer.OrdinalIgnoreCase);
            foreach (var directoryPath in projectConfigDirectories ?? Array.Empty<string>())
            {
                ApplyCustomSubagentDirectory(
                    definitions,
                    normalizedProjectRoot,
                    Path.Combine(directoryPath, ".codex", CustomAgentsDirectoryName),
                    CopilotProjectInstructionConfigSources.TrustedProject);
            }
            return CreateCustomSubagentSnapshot(definitions);
        }

        private static void ApplyCustomSubagentDirectory(
            Dictionary<string, CopilotCodexCustomSubagentDefinition> definitions,
            string allowedRootPath,
            string directoryPath,
            CopilotProjectInstructionConfigSources source)
        {
            foreach (var filePath in EnumerateCustomSubagentFiles(allowedRootPath, directoryPath))
            {
                if (!TryReadConfigSource(allowedRootPath, filePath, out var configSource)
                    || !TryParseCustomSubagent(configSource, source, filePath, out var definition))
                {
                    continue;
                }

                if (!definitions.ContainsKey(definition.Name)
                    && definitions.Count >= MaximumCustomSubagents)
                {
                    continue;
                }
                definitions[definition.Name] = definition;
            }
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
                    .Take(MaximumCustomSubagents)
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

            definition = new CopilotCodexCustomSubagentDefinition
            {
                Name = name,
                Description = description,
                DeveloperInstructions = developerInstructions,
                Model = model,
                ReasoningEffort = reasoningEffort,
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
            || string.Equals(key, CustomAgentReasoningEffortKey, StringComparison.Ordinal);

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
}
