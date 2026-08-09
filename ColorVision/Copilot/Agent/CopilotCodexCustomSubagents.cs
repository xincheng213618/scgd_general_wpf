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

        public int? ContextWindowTokens { get; init; }

        public int? ToolOutputTokenLimit { get; init; }

        public CopilotCodexSandboxMode SandboxMode { get; init; } =
            CopilotCodexSandboxMode.Unspecified;

        public CopilotCodexReasoningEffort ReasoningEffort { get; init; } =
            CopilotCodexReasoningEffort.Unspecified;

        public CopilotCodexReasoningSummary ReasoningSummary { get; init; } =
            CopilotCodexReasoningSummary.Unspecified;

        public bool? SupportsReasoningSummaries { get; init; }

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
        private const int MaximumCustomSubagentDiscoveryDirectories = 128;
        private const string CustomAgentsDirectoryName = "agents";
        private const string CustomAgentNameKey = "name";
        private const string CustomAgentDescriptionKey = "description";
        private const string CustomAgentDeveloperInstructionsKey = "developer_instructions";
        private const string CustomAgentConfigFileKey = "config_file";
        private const string CustomAgentNicknameCandidatesKey = "nickname_candidates";
        private const string CustomAgentModelKey = "model";
        private const string CustomAgentContextWindowKey = "model_context_window";
        private const string CustomAgentToolOutputTokenLimitKey = "tool_output_token_limit";
        private const string CustomAgentSandboxModeKey = "sandbox_mode";
        private const string CustomAgentReasoningEffortKey = "model_reasoning_effort";
        private const string CustomAgentReasoningSummaryKey = "model_reasoning_summary";
        private const string CustomAgentSupportsReasoningSummariesKey = "model_supports_reasoning_summaries";
        private const string CustomAgentServiceTierKey = "service_tier";
        private const string CustomAgentModelVerbosityKey = "model_verbosity";

        private static IReadOnlyList<CopilotCodexCustomSubagentDefinition> DiscoverCodexHomeCustomSubagents(
            string normalizedCodexHomePath,
            string configSource,
            string configPath,
            out IReadOnlyList<CopilotCodexCustomSubagentDiscoveryIssue> discoveryIssues)
        {
            var issues = new List<CopilotCodexCustomSubagentDiscoveryIssue>();
            discoveryIssues = Array.Empty<CopilotCodexCustomSubagentDiscoveryIssue>();
            if (normalizedCodexHomePath.Length == 0)
                return Array.Empty<CopilotCodexCustomSubagentDefinition>();

            var definitions = new Dictionary<string, CopilotCodexCustomSubagentDefinition>(
                StringComparer.OrdinalIgnoreCase);
            ApplyCustomSubagentLayer(
                definitions,
                normalizedCodexHomePath,
                configPath,
                configSource,
                Path.Combine(normalizedCodexHomePath, CustomAgentsDirectoryName),
                CopilotProjectInstructionConfigSources.CodexHome,
                issues,
                allowOutsideRoot: true);
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
                var configPath = Path.Combine(directoryPath, ".codex", ConfigFileName);
                TryReadConfigSource(normalizedProjectRoot, configPath, out var configSource);
                ApplyCustomSubagentLayer(
                    definitions,
                    normalizedProjectRoot,
                    configPath,
                    configSource,
                    Path.Combine(directoryPath, ".codex", CustomAgentsDirectoryName),
                    CopilotProjectInstructionConfigSources.TrustedProject,
                    issues,
                    allowOutsideRoot: false);
            }
            discoveryIssues = issues.ToArray();
            return CreateCustomSubagentSnapshot(definitions);
        }

        private static void ApplyCustomSubagentLayer(
            Dictionary<string, CopilotCodexCustomSubagentDefinition> definitions,
            string allowedRootPath,
            string configPath,
            string configSource,
            string agentsDirectoryPath,
            CopilotProjectInstructionConfigSources source,
            List<CopilotCodexCustomSubagentDiscoveryIssue> issues,
            bool allowOutsideRoot)
        {
            var declaredFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var declaredNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var inheritedDefinitions = definitions.ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase);
            foreach (var declaration in ParseCustomSubagentDeclarations(configSource))
            {
                if (!declaration.IsValid)
                {
                    AddCustomSubagentDiscoveryIssue(
                        issues,
                        configPath,
                        source,
                        CopilotCodexCustomSubagentDiscoveryIssueKind.InvalidDefinition);
                    continue;
                }

                if (!TryCreateDeclaredCustomSubagent(
                    declaration,
                    configPath,
                    allowedRootPath,
                    allowOutsideRoot,
                    source,
                    out var definition,
                    out var declaredFilePath,
                    out var issueKind))
                {
                    AddCustomSubagentDiscoveryIssue(
                        issues,
                        declaredFilePath.Length > 0 ? declaredFilePath : configPath,
                        source,
                        issueKind);
                    continue;
                }

                inheritedDefinitions.TryGetValue(definition.Name, out var inheritedDefinition);
                definition = MergeDeclaredCustomSubagent(
                    definition,
                    inheritedDefinition,
                    inheritRoleConfig: !declaration.HasConfigFile);
                if (definition.Description.Length == 0)
                {
                    AddCustomSubagentDiscoveryIssue(
                        issues,
                        definition.SourceFilePath,
                        source,
                        CopilotCodexCustomSubagentDiscoveryIssueKind.InvalidDefinition);
                    continue;
                }
                if (declaredFilePath.Length > 0)
                    declaredFiles.Add(declaredFilePath);
                if (!declaredNames.Add(definition.Name))
                {
                    AddCustomSubagentDiscoveryIssue(
                        issues,
                        definition.SourceFilePath,
                        source,
                        CopilotCodexCustomSubagentDiscoveryIssueKind.DuplicateName);
                    continue;
                }
                if (!definitions.ContainsKey(definition.Name)
                    && definitions.Count >= MaximumCustomSubagents)
                {
                    AddCustomSubagentDiscoveryIssue(
                        issues,
                        definition.SourceFilePath,
                        source,
                        CopilotCodexCustomSubagentDiscoveryIssueKind.LimitExceeded);
                    continue;
                }
                definitions[definition.Name] = definition;
            }

            ApplyCustomSubagentDirectory(
                definitions,
                allowedRootPath,
                agentsDirectoryPath,
                source,
                issues,
                declaredFiles,
                declaredNames);
        }

        private static CopilotCodexCustomSubagentDefinition MergeDeclaredCustomSubagent(
            CopilotCodexCustomSubagentDefinition current,
            CopilotCodexCustomSubagentDefinition? inherited,
            bool inheritRoleConfig)
        {
            if (inherited == null)
                return current;

            return current with
            {
                Description = current.Description.Length > 0
                    ? current.Description
                    : inherited.Description,
                DeveloperInstructions = inheritRoleConfig
                    ? inherited.DeveloperInstructions
                    : current.DeveloperInstructions,
                Model = inheritRoleConfig ? inherited.Model : current.Model,
                ContextWindowTokens = inheritRoleConfig
                    ? inherited.ContextWindowTokens
                    : current.ContextWindowTokens,
                ToolOutputTokenLimit = inheritRoleConfig
                    ? inherited.ToolOutputTokenLimit
                    : current.ToolOutputTokenLimit,
                SandboxMode = inheritRoleConfig
                    ? inherited.SandboxMode
                    : current.SandboxMode,
                ReasoningEffort = inheritRoleConfig
                    ? inherited.ReasoningEffort
                    : current.ReasoningEffort,
                ReasoningSummary = inheritRoleConfig
                    ? inherited.ReasoningSummary
                    : current.ReasoningSummary,
                SupportsReasoningSummaries = inheritRoleConfig
                    ? inherited.SupportsReasoningSummaries
                    : current.SupportsReasoningSummaries,
                ServiceTier = inheritRoleConfig
                    ? inherited.ServiceTier
                    : current.ServiceTier,
                ModelVerbosity = inheritRoleConfig
                    ? inherited.ModelVerbosity
                    : current.ModelVerbosity,
                HasIgnoredSettings = current.HasIgnoredSettings
                    || (inheritRoleConfig && inherited.HasIgnoredSettings),
            };
        }

        private static bool TryCreateDeclaredCustomSubagent(
            CustomSubagentDeclaration declaration,
            string configPath,
            string allowedRootPath,
            bool allowOutsideRoot,
            CopilotProjectInstructionConfigSources source,
            out CopilotCodexCustomSubagentDefinition definition,
            out string declaredFilePath,
            out CopilotCodexCustomSubagentDiscoveryIssueKind issueKind)
        {
            definition = new CopilotCodexCustomSubagentDefinition();
            declaredFilePath = string.Empty;
            issueKind = CopilotCodexCustomSubagentDiscoveryIssueKind.InvalidDefinition;

            var description = string.Empty;
            if (declaration.HasDescription
                && (!TryParseConfiguredText(
                        declaration.DescriptionValue,
                        CopilotCodexCustomSubagentDefinition.MaximumDescriptionCharacters,
                        out description)
                    || description.Length == 0))
            {
                return false;
            }

            if (!declaration.HasConfigFile)
            {
                definition = new CopilotCodexCustomSubagentDefinition
                {
                    Name = declaration.Name,
                    Description = description,
                    Source = source,
                    SourceFilePath = Path.GetFullPath(configPath),
                    HasIgnoredSettings = declaration.HasIgnoredSettings,
                };
                return true;
            }

            if (!TryParseConfiguredText(
                    declaration.ConfigFileValue,
                    MaximumConfigReferencedPathCharacters,
                    out var configuredPath)
                || configuredPath.Length == 0)
            {
                return false;
            }
            if (!TryReadConfigReferencedTextFile(
                    configPath,
                    configuredPath,
                    allowedRootPath,
                    allowOutsideRoot,
                    MaximumConfigBytes,
                    MaximumConfigBytes,
                    out var resolvedRoleFilePath,
                    out var roleSource))
            {
                declaredFilePath = configuredPath;
                issueKind = CopilotCodexCustomSubagentDiscoveryIssueKind.UnreadableOrUnsafe;
                return false;
            }
            declaredFilePath = resolvedRoleFilePath;
            if (!TryParseCustomSubagent(
                    roleSource,
                    source,
                    declaredFilePath,
                    declaration.Name,
                    description,
                    requireDescription: false,
                    requireDeveloperInstructions: false,
                    hasIgnoredSettings: declaration.HasIgnoredSettings,
                    out definition))
            {
                return false;
            }
            return true;
        }

        private static void ApplyCustomSubagentDirectory(
            Dictionary<string, CopilotCodexCustomSubagentDefinition> definitions,
            string allowedRootPath,
            string directoryPath,
            CopilotProjectInstructionConfigSources source,
            List<CopilotCodexCustomSubagentDiscoveryIssue> issues,
            IReadOnlySet<string> excludedFilePaths,
            IReadOnlySet<string> reservedNames)
        {
            var namesInDirectory = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var filePath in EnumerateCustomSubagentFiles(allowedRootPath, directoryPath))
            {
                if (excludedFilePaths.Contains(Path.GetFullPath(filePath)))
                    continue;
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
                if (reservedNames.Contains(definition.Name))
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

                var maximumFiles = MaximumCustomSubagents + MaximumCustomSubagentDiscoveryIssues;
                var files = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
                var pendingDirectories = new Queue<string>();
                pendingDirectories.Enqueue(fullDirectoryPath);
                var enqueuedDirectories = 1;
                while (pendingDirectories.Count > 0)
                {
                    var currentDirectoryPath = pendingDirectories.Dequeue();
                    try
                    {
                        foreach (var filePath in Directory.EnumerateFiles(
                            currentDirectoryPath,
                            "*.toml",
                            SearchOption.TopDirectoryOnly))
                        {
                            files.Add(Path.GetFullPath(filePath));
                            if (files.Count > maximumFiles)
                                files.Remove(files.Max!);
                        }

                        if (enqueuedDirectories >= MaximumCustomSubagentDiscoveryDirectories)
                            continue;
                        foreach (var childDirectoryPath in Directory.EnumerateDirectories(
                            currentDirectoryPath,
                            "*",
                            SearchOption.TopDirectoryOnly)
                            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
                        {
                            if (enqueuedDirectories >= MaximumCustomSubagentDiscoveryDirectories)
                                break;
                            var fullChildDirectoryPath = Path.TrimEndingDirectorySeparator(
                                Path.GetFullPath(childDirectoryPath));
                            var childDirectory = new DirectoryInfo(fullChildDirectoryPath);
                            if (!childDirectory.Exists
                                || (childDirectory.Attributes & FileAttributes.ReparsePoint) != 0
                                || CopilotWorkspaceSearchSupport.HasReparsePointInPath(fullChildDirectoryPath)
                                || !CopilotWorkspaceSearchSupport.IsPathWithinRoots(
                                    fullChildDirectoryPath,
                                    [allowedRootPath]))
                            {
                                continue;
                            }
                            pendingDirectories.Enqueue(fullChildDirectoryPath);
                            enqueuedDirectories++;
                        }
                    }
                    catch
                    {
                    }
                }
                return files.ToArray();
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
            out CopilotCodexCustomSubagentDefinition definition) =>
            TryParseCustomSubagent(
                sourceText,
                source,
                sourceFilePath,
                string.Empty,
                string.Empty,
                requireDescription: true,
                requireDeveloperInstructions: true,
                hasIgnoredSettings: false,
                out definition);

        private static bool TryParseCustomSubagent(
            string sourceText,
            CopilotProjectInstructionConfigSources source,
            string sourceFilePath,
            string nameHint,
            string descriptionFallback,
            bool requireDescription,
            bool requireDeveloperInstructions,
            bool hasIgnoredSettings,
            out CopilotCodexCustomSubagentDefinition definition)
        {
            definition = new CopilotCodexCustomSubagentDefinition();
            var assignments = new Dictionary<string, string>(StringComparer.Ordinal);
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

            var name = (nameHint ?? string.Empty).Trim();
            if (assignments.TryGetValue(CustomAgentNameKey, out var nameValue))
            {
                if (!TryParseConfiguredText(
                        nameValue,
                        CopilotCodexCustomSubagentDefinition.MaximumNameCharacters,
                        out name))
                {
                    return false;
                }
            }
            if (!IsValidCustomSubagentName(name))
                return false;

            var description = (descriptionFallback ?? string.Empty).Trim();
            if (assignments.TryGetValue(CustomAgentDescriptionKey, out var descriptionValue))
            {
                if (!TryParseConfiguredText(
                        descriptionValue,
                        CopilotCodexCustomSubagentDefinition.MaximumDescriptionCharacters,
                        out description))
                {
                    return false;
                }
            }
            if (requireDescription && description.Length == 0)
                return false;

            var developerInstructions = string.Empty;
            if (assignments.TryGetValue(CustomAgentDeveloperInstructionsKey, out var instructionsValue))
            {
                if (!TryParseConfiguredText(
                        instructionsValue,
                        CopilotCodexCustomSubagentDefinition.MaximumDeveloperInstructionCharacters,
                        out developerInstructions)
                    || developerInstructions.Length == 0)
                {
                    return false;
                }
            }
            else if (requireDeveloperInstructions)
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

            int? contextWindowTokens = null;
            if (assignments.TryGetValue(CustomAgentContextWindowKey, out var contextWindowValue))
            {
                if (!TryParseModelContextWindowTokens(contextWindowValue, out var configuredContextWindowTokens))
                    return false;
                contextWindowTokens = configuredContextWindowTokens;
            }

            int? toolOutputTokenLimit = null;
            if (assignments.TryGetValue(CustomAgentToolOutputTokenLimitKey, out var toolOutputTokenLimitValue))
            {
                if (!TryParseToolOutputTokenLimit(toolOutputTokenLimitValue, out var configuredToolOutputTokenLimit))
                    return false;
                toolOutputTokenLimit = configuredToolOutputTokenLimit;
            }

            var sandboxMode = CopilotCodexSandboxMode.Unspecified;
            if (assignments.TryGetValue(CustomAgentSandboxModeKey, out var sandboxModeValue)
                && (!TryParseConfiguredText(sandboxModeValue, 32, out var configuredSandboxMode)
                    || !CopilotCodexSandboxModeSelection.TryParse(configuredSandboxMode, out sandboxMode)))
            {
                return false;
            }
            if (sandboxMode is CopilotCodexSandboxMode.WorkspaceWrite or CopilotCodexSandboxMode.DangerFullAccess)
                hasIgnoredSettings = true;

            var reasoningSummary = CopilotCodexReasoningSummary.Unspecified;
            if (assignments.TryGetValue(CustomAgentReasoningSummaryKey, out var summaryValue)
                && (!TryParseConfiguredText(summaryValue, 32, out var configuredSummary)
                    || !CopilotCodexReasoningSummarySelection.TryParse(configuredSummary, out reasoningSummary)))
            {
                return false;
            }

            bool? supportsReasoningSummaries = null;
            if (assignments.TryGetValue(CustomAgentSupportsReasoningSummariesKey, out var supportsSummaryValue))
            {
                if (!TryParseTomlBoolean(supportsSummaryValue, out var configuredSupportsReasoningSummaries))
                    return false;
                supportsReasoningSummaries = configuredSupportsReasoningSummaries;
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
                ContextWindowTokens = contextWindowTokens,
                ToolOutputTokenLimit = toolOutputTokenLimit,
                SandboxMode = sandboxMode,
                ReasoningEffort = reasoningEffort,
                ReasoningSummary = reasoningSummary,
                SupportsReasoningSummaries = supportsReasoningSummaries,
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

        private static IReadOnlyList<CustomSubagentDeclaration> ParseCustomSubagentDeclarations(string source)
        {
            var declarations = new List<CustomSubagentDeclaration>();
            var assignments = new Dictionary<string, string>(StringComparer.Ordinal);
            var currentName = string.Empty;
            var currentIsValid = true;
            var hasIgnoredSettings = false;

            void FlushCurrent()
            {
                if (currentName.Length == 0 && currentIsValid)
                    return;
                declarations.Add(new CustomSubagentDeclaration(
                    currentName,
                    assignments.GetValueOrDefault(CustomAgentDescriptionKey, string.Empty),
                    assignments.ContainsKey(CustomAgentDescriptionKey),
                    assignments.GetValueOrDefault(CustomAgentConfigFileKey, string.Empty),
                    assignments.ContainsKey(CustomAgentConfigFileKey),
                    hasIgnoredSettings,
                    currentIsValid));
                assignments.Clear();
                currentName = string.Empty;
                currentIsValid = true;
                hasIgnoredSettings = false;
            }

            var lines = NormalizeLines(source);
            for (var index = 0; index < lines.Length; index++)
            {
                var line = StripComment(lines[index]).Trim();
                if (line.Length == 0)
                    continue;
                if (line[0] == '[')
                {
                    FlushCurrent();
                    if (TryParseCustomSubagentRoleTableHeader(line, out var roleName))
                    {
                        currentName = roleName;
                    }
                    else if (line.StartsWith("[agents.", StringComparison.Ordinal))
                    {
                        currentIsValid = false;
                    }
                    continue;
                }
                if (currentName.Length == 0)
                    continue;

                var equalsIndex = line.IndexOf('=');
                if (equalsIndex <= 0)
                {
                    currentIsValid = false;
                    continue;
                }
                var key = line[..equalsIndex].Trim();
                var value = line[(equalsIndex + 1)..].Trim();
                if (string.Equals(key, CustomAgentDescriptionKey, StringComparison.Ordinal)
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

                if (string.Equals(key, CustomAgentNicknameCandidatesKey, StringComparison.Ordinal))
                {
                    hasIgnoredSettings = true;
                    continue;
                }
                if (!string.Equals(key, CustomAgentDescriptionKey, StringComparison.Ordinal)
                    && !string.Equals(key, CustomAgentConfigFileKey, StringComparison.Ordinal))
                {
                    hasIgnoredSettings = true;
                    continue;
                }
                if (!assignments.TryAdd(key, value))
                    currentIsValid = false;
            }
            FlushCurrent();
            return declarations;
        }

        private static bool HasValidCustomSubagentDeclarations(string source) =>
            ParseCustomSubagentDeclarations(source).Any(declaration => declaration.IsValid);

        private static bool TryParseCustomSubagentRoleTableHeader(string line, out string roleName)
        {
            roleName = string.Empty;
            if (line.StartsWith("[[", StringComparison.Ordinal)
                || !line.StartsWith("[agents.", StringComparison.Ordinal)
                || !line.EndsWith(']'))
            {
                return false;
            }
            roleName = line[8..^1].Trim();
            if (!IsValidCustomSubagentName(roleName))
            {
                roleName = string.Empty;
                return false;
            }
            return true;
        }

        private static bool IsSupportedCustomSubagentKey(string key) =>
            string.Equals(key, CustomAgentNameKey, StringComparison.Ordinal)
            || string.Equals(key, CustomAgentDescriptionKey, StringComparison.Ordinal)
            || string.Equals(key, CustomAgentDeveloperInstructionsKey, StringComparison.Ordinal)
            || string.Equals(key, CustomAgentModelKey, StringComparison.Ordinal)
            || string.Equals(key, CustomAgentContextWindowKey, StringComparison.Ordinal)
            || string.Equals(key, CustomAgentToolOutputTokenLimitKey, StringComparison.Ordinal)
            || string.Equals(key, CustomAgentSandboxModeKey, StringComparison.Ordinal)
            || string.Equals(key, CustomAgentReasoningEffortKey, StringComparison.Ordinal)
            || string.Equals(key, CustomAgentReasoningSummaryKey, StringComparison.Ordinal)
            || string.Equals(key, CustomAgentSupportsReasoningSummariesKey, StringComparison.Ordinal)
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

        private sealed record CustomSubagentDeclaration(
            string Name,
            string DescriptionValue,
            bool HasDescription,
            string ConfigFileValue,
            bool HasConfigFile,
            bool HasIgnoredSettings,
            bool IsValid);

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
                    .Append(" · context ")
                    .Append(definition.ContextWindowTokens?.ToString() ?? "inherited")
                    .Append(" · tool_output ")
                    .Append(definition.ToolOutputTokenLimit?.ToString() ?? "inherited")
                    .Append(" · sandbox ")
                    .Append(definition.SandboxMode == CopilotCodexSandboxMode.Unspecified
                        ? "inherited"
                        : CopilotCodexSandboxModeSelection.GetConfigToken(definition.SandboxMode))
                    .Append("→read-only")
                    .Append(" · reasoning ")
                    .Append(definition.ReasoningEffort == CopilotCodexReasoningEffort.Unspecified
                        ? "inherited"
                        : CopilotCodexReasoningEffortSelection.GetConfigToken(definition.ReasoningEffort))
                    .Append(" · summary ")
                    .Append(definition.ReasoningSummary == CopilotCodexReasoningSummary.Unspecified
                        ? "inherited"
                        : CopilotCodexReasoningSummarySelection.GetConfigToken(definition.ReasoningSummary))
                    .Append(" · summary_support ")
                    .Append(CopilotCodexReasoningSummarySupportSelection.GetConfigToken(
                        definition.SupportsReasoningSummaries))
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
