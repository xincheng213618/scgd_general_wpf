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

    internal enum CopilotCodexWebSearchMode
    {
        Unspecified,
        Disabled,
        Cached,
        Indexed,
        Live,
    }

    internal static class CopilotCodexWebSearchModeSelection
    {
        public static bool TryParse(string? value, out CopilotCodexWebSearchMode mode)
        {
            mode = (value ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "disabled" => CopilotCodexWebSearchMode.Disabled,
                "cached" => CopilotCodexWebSearchMode.Cached,
                "indexed" => CopilotCodexWebSearchMode.Indexed,
                "live" => CopilotCodexWebSearchMode.Live,
                _ => CopilotCodexWebSearchMode.Unspecified,
            };
            return mode != CopilotCodexWebSearchMode.Unspecified;
        }

        public static string GetConfigToken(CopilotCodexWebSearchMode mode) => mode switch
        {
            CopilotCodexWebSearchMode.Disabled => "disabled",
            CopilotCodexWebSearchMode.Cached => "cached",
            CopilotCodexWebSearchMode.Indexed => "indexed",
            CopilotCodexWebSearchMode.Live => "live",
            _ => "未配置",
        };

        public static bool AllowsLiveSearch(CopilotCodexWebSearchMode mode) => mode is
            CopilotCodexWebSearchMode.Unspecified or CopilotCodexWebSearchMode.Live;

        public static string GetEffectiveLabel(CopilotCodexWebSearchMode mode) => mode switch
        {
            CopilotCodexWebSearchMode.Disabled => "已禁用实时公网检索",
            CopilotCodexWebSearchMode.Cached => "不支持 cached 后端；已保守禁用实时公网检索",
            CopilotCodexWebSearchMode.Indexed => "不支持 indexed 后端；已保守禁用实时公网检索",
            CopilotCodexWebSearchMode.Live => "已允许按请求意图实时公网检索",
            _ => "未配置；保留 ColorVision 按请求意图实时检索",
        };
    }

    internal enum CopilotModelAutoCompactTokenLimitScope
    {
        Unspecified,
        Total,
        BodyAfterPrefix,
    }

    internal static class CopilotModelAutoCompactTokenLimitScopeSelection
    {
        public static bool TryParse(
            string? value,
            out CopilotModelAutoCompactTokenLimitScope scope)
        {
            scope = (value ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "total" => CopilotModelAutoCompactTokenLimitScope.Total,
                "body_after_prefix" => CopilotModelAutoCompactTokenLimitScope.BodyAfterPrefix,
                _ => CopilotModelAutoCompactTokenLimitScope.Unspecified,
            };
            return scope != CopilotModelAutoCompactTokenLimitScope.Unspecified;
        }

        public static string GetConfigToken(CopilotModelAutoCompactTokenLimitScope scope) => scope switch
        {
            CopilotModelAutoCompactTokenLimitScope.BodyAfterPrefix => "body_after_prefix",
            _ => "total",
        };
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

        public CopilotResponsePersonality ConfiguredPersonality { get; init; } =
            CopilotResponsePersonality.None;

        public bool HasPersonalityOverride { get; init; }

        public CopilotProjectInstructionConfigSources PersonalitySource { get; init; } =
            CopilotProjectInstructionConfigSources.None;

        public CopilotCodexWebSearchMode ConfiguredWebSearchMode { get; init; } =
            CopilotCodexWebSearchMode.Unspecified;

        public bool HasWebSearchModeOverride { get; init; }

        public CopilotProjectInstructionConfigSources WebSearchModeSource { get; init; } =
            CopilotProjectInstructionConfigSources.None;

        public int ConfiguredModelContextWindowTokens { get; init; }

        public bool HasModelContextWindowOverride { get; init; }

        public CopilotProjectInstructionConfigSources ModelContextWindowSource { get; init; } =
            CopilotProjectInstructionConfigSources.None;

        public int ResolveContextWindowTokens(int fallbackTokens)
        {
            return HasModelContextWindowOverride
                ? ConfiguredModelContextWindowTokens
                : Math.Clamp(
                    fallbackTokens,
                    CopilotAgentTokenBudget.MinimumContextWindowTokens,
                    CopilotAgentTokenBudget.MaximumContextWindowTokens);
        }

        public int ConfiguredModelAutoCompactTokenLimit { get; init; }

        public bool HasModelAutoCompactTokenLimitOverride { get; init; }

        public CopilotProjectInstructionConfigSources ModelAutoCompactTokenLimitSource { get; init; } =
            CopilotProjectInstructionConfigSources.None;

        public CopilotModelAutoCompactTokenLimitScope ConfiguredModelAutoCompactTokenLimitScope { get; init; } =
            CopilotModelAutoCompactTokenLimitScope.Unspecified;

        public bool HasModelAutoCompactTokenLimitScopeOverride { get; init; }

        public CopilotProjectInstructionConfigSources ModelAutoCompactTokenLimitScopeSource { get; init; } =
            CopilotProjectInstructionConfigSources.None;

        public CopilotModelAutoCompactTokenLimitScope EffectiveModelAutoCompactTokenLimitScope =>
            HasModelAutoCompactTokenLimitScopeOverride
                ? ConfiguredModelAutoCompactTokenLimitScope
                : CopilotModelAutoCompactTokenLimitScope.Total;

        internal string ConfiguredModelInstructionsFileContent { get; init; } = string.Empty;

        internal bool HasModelInstructionsFileOverride { get; init; }

        internal CopilotProjectInstructionConfigSources ModelInstructionsFileSource { get; init; } =
            CopilotProjectInstructionConfigSources.None;

        internal string ConfiguredModelInstructionsSourceFilePath { get; init; } = string.Empty;

        public string ModelInstructions => ConfiguredModelInstructionsFileContent.Trim();

        public bool HasEffectiveModelInstructions => ModelInstructions.Length > 0;

        public string ModelInstructionsSourceFilePath => HasEffectiveModelInstructions
            ? ConfiguredModelInstructionsSourceFilePath
            : string.Empty;

        internal string ConfiguredCompactPrompt { get; init; } = string.Empty;

        internal bool HasCompactPromptInlineOverride { get; init; }

        internal CopilotProjectInstructionConfigSources CompactPromptInlineSource { get; init; } =
            CopilotProjectInstructionConfigSources.None;

        internal string ConfiguredCompactPromptFileContent { get; init; } = string.Empty;

        internal bool HasCompactPromptFileOverride { get; init; }

        internal CopilotProjectInstructionConfigSources CompactPromptFileSource { get; init; } =
            CopilotProjectInstructionConfigSources.None;

        internal string ConfiguredCompactPromptSourceFilePath { get; init; } = string.Empty;

        public string CompactPrompt
        {
            get
            {
                var inline = ConfiguredCompactPrompt.Trim();
                if (inline.Length > 0)
                    return inline;
                return ConfiguredCompactPromptFileContent.Trim();
            }
        }

        public bool HasCompactPromptOverride => HasCompactPromptInlineOverride
            || HasCompactPromptFileOverride;

        public CopilotProjectInstructionConfigSources CompactPromptSource
        {
            get
            {
                if (ConfiguredCompactPrompt.Trim().Length > 0)
                    return CompactPromptInlineSource;
                if (ConfiguredCompactPromptFileContent.Trim().Length > 0)
                    return CompactPromptFileSource;
                if (HasCompactPromptFileOverride)
                    return CompactPromptFileSource;
                return HasCompactPromptInlineOverride
                    ? CompactPromptInlineSource
                    : CopilotProjectInstructionConfigSources.None;
            }
        }

        public string CompactPromptSourceFilePath =>
            CompactPromptUsesFile
                ? ConfiguredCompactPromptSourceFilePath
                : string.Empty;

        public bool CompactPromptUsesFile => ConfiguredCompactPrompt.Trim().Length == 0
            && HasCompactPromptFileOverride;

        public bool UsesCodexConfig => ConfigSources != CopilotProjectInstructionConfigSources.None
            || HasMaximumBytesOverride
            || HasFallbackFileNamesOverride
            || HasProjectRootMarkersOverride
            || HasDeveloperInstructionsOverride
            || HasPersonalityOverride
            || HasWebSearchModeOverride
            || HasModelContextWindowOverride
            || HasModelAutoCompactTokenLimitOverride
            || HasModelAutoCompactTokenLimitScopeOverride
            || HasModelInstructionsFileOverride
            || HasCompactPromptOverride;

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

        public string PersonalitySourceLabel => PersonalitySource switch
        {
            CopilotProjectInstructionConfigSources.CodexHome => "Codex Home config.toml personality",
            CopilotProjectInstructionConfigSources.TrustedProject => "受信项目 .codex/config.toml personality",
            _ => string.Empty,
        };

        public string WebSearchModeSourceLabel => WebSearchModeSource switch
        {
            CopilotProjectInstructionConfigSources.CodexHome => "Codex Home config.toml web_search",
            CopilotProjectInstructionConfigSources.TrustedProject => "受信项目 .codex/config.toml web_search",
            _ => string.Empty,
        };

        public string ModelContextWindowSourceLabel => ModelContextWindowSource switch
        {
            CopilotProjectInstructionConfigSources.CodexHome => "Codex Home config.toml model_context_window",
            CopilotProjectInstructionConfigSources.TrustedProject => "受信项目 .codex/config.toml model_context_window",
            _ => string.Empty,
        };

        public string ModelAutoCompactTokenLimitSourceLabel => ModelAutoCompactTokenLimitSource switch
        {
            CopilotProjectInstructionConfigSources.CodexHome => "Codex Home config.toml model_auto_compact_token_limit",
            CopilotProjectInstructionConfigSources.TrustedProject => "受信项目 .codex/config.toml model_auto_compact_token_limit",
            _ => string.Empty,
        };

        public string ModelAutoCompactTokenLimitScopeSourceLabel => ModelAutoCompactTokenLimitScopeSource switch
        {
            CopilotProjectInstructionConfigSources.CodexHome => "Codex Home config.toml model_auto_compact_token_limit_scope",
            CopilotProjectInstructionConfigSources.TrustedProject => "受信项目 .codex/config.toml model_auto_compact_token_limit_scope",
            _ => string.Empty,
        };

        public string ModelInstructionsSourceLabel => ModelInstructionsFileSource switch
        {
            CopilotProjectInstructionConfigSources.CodexHome => "Codex Home config.toml model_instructions_file",
            CopilotProjectInstructionConfigSources.TrustedProject => "受信项目 .codex/config.toml model_instructions_file",
            _ => string.Empty,
        };

        public string CompactPromptSourceLabel
        {
            get
            {
                var layer = CompactPromptSource switch
                {
                    CopilotProjectInstructionConfigSources.CodexHome => "Codex Home config.toml",
                    CopilotProjectInstructionConfigSources.TrustedProject => "受信项目 .codex/config.toml",
                    _ => string.Empty,
                };
                if (layer.Length == 0)
                    return string.Empty;
                return layer + (CompactPromptUsesFile
                    ? " experimental_compact_prompt_file"
                    : " compact_prompt");
            }
        }

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
        private const int MaximumPersonalityCharacters = 32;
        private const int MaximumLogicalValueLines = 64;
        internal const int MaximumDeveloperInstructionCharacters = 64 * 1024;
        private const int MaximumConfiguredTextLines = 512;
        internal const int MaximumModelInstructionCharacters = 64 * 1024;
        private const int MaximumModelInstructionBytes = 256 * 1024;
        internal const int MaximumCompactPromptCharacters = 32 * 1024;
        private const int MaximumCompactPromptBytes = 128 * 1024;
        private const int MaximumConfigReferencedPathCharacters = 2_048;
        private const string ConfigFileName = "config.toml";
        private const string MaximumBytesKey = "project_doc_max_bytes";
        private const string FallbackFileNamesKey = "project_doc_fallback_filenames";
        private const string ProjectRootMarkersKey = "project_root_markers";
        private const string DeveloperInstructionsKey = "developer_instructions";
        private const string PersonalityKey = "personality";
        private const string WebSearchKey = "web_search";
        private const string ModelContextWindowKey = "model_context_window";
        private const string ModelAutoCompactTokenLimitKey = "model_auto_compact_token_limit";
        private const string ModelAutoCompactTokenLimitScopeKey = "model_auto_compact_token_limit_scope";
        private const string ModelInstructionsFileKey = "model_instructions_file";
        private const string CompactPromptKey = "compact_prompt";
        private const string ExperimentalCompactPromptFileKey = "experimental_compact_prompt_file";
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
            var globalConfigPath = Path.Combine(normalizedRoot, ConfigFileName);
            if (normalizedRoot.Length > 0
                && TryReadConfigSource(normalizedRoot, globalConfigPath, out globalSource)
                && TryParseInstructionLayer(globalSource, out var globalLayer))
            {
                globalLayer = ResolveCompactPromptFile(
                    globalLayer,
                    globalConfigPath,
                    normalizedRoot,
                    allowOutsideRoot: true);
                globalLayer = ResolveModelInstructionsFile(
                    globalLayer,
                    globalConfigPath,
                    normalizedRoot,
                    allowOutsideRoot: true);
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
                    || !TryParseInstructionLayer(projectSource, out var projectLayer))
                {
                    continue;
                }
                projectLayer = ResolveCompactPromptFile(
                    projectLayer,
                    configPath,
                    normalizedProjectRoot,
                    allowOutsideRoot: false);
                projectLayer = ResolveModelInstructionsFile(
                    projectLayer,
                    configPath,
                    normalizedProjectRoot,
                    allowOutsideRoot: false);
                if (!HasApplicableOverrides(projectLayer, includeProjectRootMarkers: false))
                    continue;

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
                ConfiguredPersonality = layer.HasPersonalityOverride
                    ? layer.Personality
                    : current.ConfiguredPersonality,
                HasPersonalityOverride = current.HasPersonalityOverride
                    || layer.HasPersonalityOverride,
                PersonalitySource = layer.HasPersonalityOverride
                    ? source
                    : current.PersonalitySource,
                ConfiguredWebSearchMode = layer.HasWebSearchModeOverride
                    ? layer.WebSearchMode
                    : current.ConfiguredWebSearchMode,
                HasWebSearchModeOverride = current.HasWebSearchModeOverride
                    || layer.HasWebSearchModeOverride,
                WebSearchModeSource = layer.HasWebSearchModeOverride
                    ? source
                    : current.WebSearchModeSource,
                ConfiguredModelContextWindowTokens = layer.HasModelContextWindowOverride
                    ? layer.ModelContextWindowTokens
                    : current.ConfiguredModelContextWindowTokens,
                HasModelContextWindowOverride = current.HasModelContextWindowOverride
                    || layer.HasModelContextWindowOverride,
                ModelContextWindowSource = layer.HasModelContextWindowOverride
                    ? source
                    : current.ModelContextWindowSource,
                ConfiguredModelAutoCompactTokenLimit = layer.HasModelAutoCompactTokenLimitOverride
                    ? layer.ModelAutoCompactTokenLimit
                    : current.ConfiguredModelAutoCompactTokenLimit,
                HasModelAutoCompactTokenLimitOverride = current.HasModelAutoCompactTokenLimitOverride
                    || layer.HasModelAutoCompactTokenLimitOverride,
                ModelAutoCompactTokenLimitSource = layer.HasModelAutoCompactTokenLimitOverride
                    ? source
                    : current.ModelAutoCompactTokenLimitSource,
                ConfiguredModelAutoCompactTokenLimitScope = layer.HasModelAutoCompactTokenLimitScopeOverride
                    ? layer.ModelAutoCompactTokenLimitScope
                    : current.ConfiguredModelAutoCompactTokenLimitScope,
                HasModelAutoCompactTokenLimitScopeOverride = current.HasModelAutoCompactTokenLimitScopeOverride
                    || layer.HasModelAutoCompactTokenLimitScopeOverride,
                ModelAutoCompactTokenLimitScopeSource = layer.HasModelAutoCompactTokenLimitScopeOverride
                    ? source
                    : current.ModelAutoCompactTokenLimitScopeSource,
                ConfiguredModelInstructionsFileContent = layer.HasModelInstructionsFileOverride
                    ? layer.ModelInstructionsFileContent
                    : current.ConfiguredModelInstructionsFileContent,
                HasModelInstructionsFileOverride = current.HasModelInstructionsFileOverride
                    || layer.HasModelInstructionsFileOverride,
                ModelInstructionsFileSource = layer.HasModelInstructionsFileOverride
                    ? source
                    : current.ModelInstructionsFileSource,
                ConfiguredModelInstructionsSourceFilePath = layer.HasModelInstructionsFileOverride
                    ? layer.ModelInstructionsSourceFilePath
                    : current.ConfiguredModelInstructionsSourceFilePath,
                ConfiguredCompactPrompt = layer.HasCompactPromptOverride
                    ? layer.CompactPrompt
                    : current.ConfiguredCompactPrompt,
                HasCompactPromptInlineOverride = current.HasCompactPromptInlineOverride
                    || layer.HasCompactPromptOverride,
                CompactPromptInlineSource = layer.HasCompactPromptOverride
                    ? source
                    : current.CompactPromptInlineSource,
                ConfiguredCompactPromptFileContent = layer.HasCompactPromptFileOverride
                    ? layer.CompactPromptFileContent
                    : current.ConfiguredCompactPromptFileContent,
                HasCompactPromptFileOverride = current.HasCompactPromptFileOverride
                    || layer.HasCompactPromptFileOverride,
                CompactPromptFileSource = layer.HasCompactPromptFileOverride
                    ? source
                    : current.CompactPromptFileSource,
                ConfiguredCompactPromptSourceFilePath = layer.HasCompactPromptFileOverride
                    ? layer.CompactPromptSourceFilePath
                    : current.ConfiguredCompactPromptSourceFilePath,
            };
        }

        private static bool HasApplicableOverrides(
            ProjectInstructionConfigLayer layer,
            bool includeProjectRootMarkers)
        {
            return layer.HasMaximumBytesOverride
                || layer.HasFallbackFileNamesOverride
                || layer.HasDeveloperInstructionsOverride
                || layer.HasPersonalityOverride
                || layer.HasWebSearchModeOverride
                || layer.HasModelContextWindowOverride
                || layer.HasModelAutoCompactTokenLimitOverride
                || layer.HasModelAutoCompactTokenLimitScopeOverride
                || layer.HasModelInstructionsFileOverride
                || layer.HasCompactPromptOverride
                || layer.HasCompactPromptFileOverride
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

        private static ProjectInstructionConfigLayer ResolveCompactPromptFile(
            ProjectInstructionConfigLayer layer,
            string configPath,
            string allowedRootPath,
            bool allowOutsideRoot)
        {
            if (!layer.HasCompactPromptFileOverride)
                return layer;

            if (!TryReadCompactPromptFile(
                configPath,
                layer.ConfiguredCompactPromptFilePath,
                allowedRootPath,
                allowOutsideRoot,
                out var promptFilePath,
                out var compactPrompt))
            {
                return layer;
            }

            return layer with
            {
                CompactPromptFileContent = compactPrompt,
                CompactPromptSourceFilePath = promptFilePath,
            };
        }

        private static ProjectInstructionConfigLayer ResolveModelInstructionsFile(
            ProjectInstructionConfigLayer layer,
            string configPath,
            string allowedRootPath,
            bool allowOutsideRoot)
        {
            if (!layer.HasModelInstructionsFileOverride)
                return layer;

            if (!TryReadConfigReferencedTextFile(
                configPath,
                layer.ConfiguredModelInstructionsFilePath,
                allowedRootPath,
                allowOutsideRoot,
                MaximumModelInstructionBytes,
                MaximumModelInstructionCharacters,
                out var instructionsFilePath,
                out var modelInstructions))
            {
                return layer;
            }

            return layer with
            {
                ModelInstructionsFileContent = modelInstructions,
                ModelInstructionsSourceFilePath = instructionsFilePath,
            };
        }

        private static bool TryReadCompactPromptFile(
            string configPath,
            string configuredPath,
            string allowedRootPath,
            bool allowOutsideRoot,
            out string promptFilePath,
            out string compactPrompt) =>
            TryReadConfigReferencedTextFile(
                configPath,
                configuredPath,
                allowedRootPath,
                allowOutsideRoot,
                MaximumCompactPromptBytes,
                MaximumCompactPromptCharacters,
                out promptFilePath,
                out compactPrompt);

        private static bool TryReadConfigReferencedTextFile(
            string configPath,
            string configuredPath,
            string allowedRootPath,
            bool allowOutsideRoot,
            int maximumBytes,
            int maximumCharacters,
            out string sourceFilePath,
            out string content)
        {
            sourceFilePath = string.Empty;
            content = string.Empty;
            var normalizedConfiguredPath = (configuredPath ?? string.Empty).Trim();
            if (normalizedConfiguredPath.Length == 0
                || normalizedConfiguredPath.Length > MaximumConfigReferencedPathCharacters
                || normalizedConfiguredPath.StartsWith("\\\\", StringComparison.Ordinal)
                || normalizedConfiguredPath.StartsWith("//", StringComparison.Ordinal))
            {
                return false;
            }

            try
            {
                var configDirectory = Path.GetDirectoryName(Path.GetFullPath(configPath));
                if (string.IsNullOrWhiteSpace(configDirectory))
                    return false;
                var candidatePath = Path.IsPathFullyQualified(normalizedConfiguredPath)
                    ? Path.GetFullPath(normalizedConfiguredPath)
                    : Path.GetFullPath(normalizedConfiguredPath, configDirectory);
                if (!allowOutsideRoot
                    && !CopilotWorkspaceSearchSupport.IsPathWithinRoots(candidatePath, [allowedRootPath]))
                {
                    return false;
                }

                var file = new FileInfo(candidatePath);
                if (!file.Exists
                    || file.Length < 0
                    || file.Length > maximumBytes
                    || (file.Attributes & FileAttributes.ReparsePoint) != 0
                    || CopilotWorkspaceSearchSupport.HasReparsePointInPath(candidatePath))
                {
                    return false;
                }

                using var stream = new FileStream(
                    candidatePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
                if (stream.Length < 0 || stream.Length > maximumBytes)
                    return false;
                using var reader = new StreamReader(
                    stream,
                    Encoding.UTF8,
                    detectEncodingFromByteOrderMarks: true);
                var buffer = new char[maximumCharacters + 1];
                var count = reader.ReadBlock(buffer, 0, buffer.Length);
                if (count > maximumCharacters || !reader.EndOfStream)
                    return false;
                var normalizedContent = new string(buffer, 0, count).Trim();
                if (normalizedContent.Contains('\0'))
                    return false;

                sourceFilePath = candidatePath;
                content = normalizedContent;
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
            var personality = CopilotResponsePersonality.None;
            var webSearchMode = CopilotCodexWebSearchMode.Unspecified;
            var modelContextWindowTokens = 0;
            var modelAutoCompactTokenLimit = 0;
            var modelAutoCompactTokenLimitScope = CopilotModelAutoCompactTokenLimitScope.Unspecified;
            var modelInstructionsFilePath = string.Empty;
            var compactPrompt = string.Empty;
            var compactPromptFilePath = string.Empty;
            var hasMaximumBytesOverride = false;
            var hasFallbackFileNamesOverride = false;
            var hasProjectRootMarkersOverride = false;
            var hasDeveloperInstructionsOverride = false;
            var hasPersonalityOverride = false;
            var hasWebSearchModeOverride = false;
            var hasModelContextWindowOverride = false;
            var hasModelAutoCompactTokenLimitOverride = false;
            var hasModelAutoCompactTokenLimitScopeOverride = false;
            var hasModelInstructionsFileOverride = false;
            var hasCompactPromptOverride = false;
            var hasCompactPromptFileOverride = false;
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

                if (string.Equals(assignment.Key, PersonalityKey, StringComparison.Ordinal))
                {
                    if (!TryParseConfiguredText(
                        assignment.Value,
                        MaximumPersonalityCharacters,
                        out var configuredPersonality)
                        || !CopilotResponsePersonalitySelection.TryParse(
                            configuredPersonality,
                            out personality))
                    {
                        continue;
                    }
                    hasPersonalityOverride = true;
                    continue;
                }

                if (string.Equals(assignment.Key, WebSearchKey, StringComparison.Ordinal))
                {
                    if (!TryParseConfiguredText(
                        assignment.Value,
                        MaximumPersonalityCharacters,
                        out var configuredWebSearchMode)
                        || !CopilotCodexWebSearchModeSelection.TryParse(
                            configuredWebSearchMode,
                            out webSearchMode))
                    {
                        continue;
                    }
                    hasWebSearchModeOverride = true;
                    continue;
                }

                if (string.Equals(assignment.Key, ModelContextWindowKey, StringComparison.Ordinal))
                {
                    if (!TryParseModelContextWindowTokens(
                        assignment.Value,
                        out modelContextWindowTokens))
                    {
                        continue;
                    }
                    hasModelContextWindowOverride = true;
                    continue;
                }

                if (string.Equals(assignment.Key, ModelAutoCompactTokenLimitKey, StringComparison.Ordinal))
                {
                    if (!TryParsePositiveTokenLimit(
                        assignment.Value,
                        out modelAutoCompactTokenLimit))
                    {
                        continue;
                    }
                    hasModelAutoCompactTokenLimitOverride = true;
                    continue;
                }

                if (string.Equals(assignment.Key, ModelAutoCompactTokenLimitScopeKey, StringComparison.Ordinal))
                {
                    if (!TryParseConfiguredText(
                        assignment.Value,
                        MaximumPersonalityCharacters,
                        out var configuredModelAutoCompactTokenLimitScope)
                        || !CopilotModelAutoCompactTokenLimitScopeSelection.TryParse(
                            configuredModelAutoCompactTokenLimitScope,
                            out modelAutoCompactTokenLimitScope))
                    {
                        continue;
                    }
                    hasModelAutoCompactTokenLimitScopeOverride = true;
                    continue;
                }

                if (string.Equals(assignment.Key, CompactPromptKey, StringComparison.Ordinal))
                {
                    if (!TryParseConfiguredText(
                        assignment.Value,
                        MaximumCompactPromptCharacters,
                        out var configuredCompactPrompt))
                    {
                        continue;
                    }
                    compactPrompt = configuredCompactPrompt;
                    hasCompactPromptOverride = true;
                    continue;
                }

                if (string.Equals(assignment.Key, ModelInstructionsFileKey, StringComparison.Ordinal))
                {
                    if (!TryParseConfiguredText(
                        assignment.Value,
                        MaximumConfigReferencedPathCharacters,
                        out var configuredModelInstructionsFilePath)
                        || configuredModelInstructionsFilePath.IndexOfAny(['\r', '\n', '\0']) >= 0)
                    {
                        continue;
                    }
                    modelInstructionsFilePath = configuredModelInstructionsFilePath;
                    hasModelInstructionsFileOverride = true;
                    continue;
                }

                if (string.Equals(assignment.Key, ExperimentalCompactPromptFileKey, StringComparison.Ordinal))
                {
                    if (!TryParseConfiguredText(
                        assignment.Value,
                        MaximumConfigReferencedPathCharacters,
                        out var configuredCompactPromptFilePath)
                        || configuredCompactPromptFilePath.IndexOfAny(['\r', '\n', '\0']) >= 0)
                    {
                        continue;
                    }
                    compactPromptFilePath = configuredCompactPromptFilePath;
                    hasCompactPromptFileOverride = true;
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
                hasDeveloperInstructionsOverride)
            {
                CompactPrompt = compactPrompt,
                ConfiguredModelInstructionsFilePath = modelInstructionsFilePath,
                ConfiguredCompactPromptFilePath = compactPromptFilePath,
                HasModelInstructionsFileOverride = hasModelInstructionsFileOverride,
                Personality = personality,
                HasPersonalityOverride = hasPersonalityOverride,
                WebSearchMode = webSearchMode,
                HasWebSearchModeOverride = hasWebSearchModeOverride,
                ModelContextWindowTokens = modelContextWindowTokens,
                HasModelContextWindowOverride = hasModelContextWindowOverride,
                ModelAutoCompactTokenLimit = modelAutoCompactTokenLimit,
                HasModelAutoCompactTokenLimitOverride = hasModelAutoCompactTokenLimitOverride,
                ModelAutoCompactTokenLimitScope = modelAutoCompactTokenLimitScope,
                HasModelAutoCompactTokenLimitScopeOverride = hasModelAutoCompactTokenLimitScopeOverride,
                HasCompactPromptOverride = hasCompactPromptOverride,
                HasCompactPromptFileOverride = hasCompactPromptFileOverride,
            };
            return hasMaximumBytesOverride
                || hasFallbackFileNamesOverride
                || hasProjectRootMarkersOverride
                || hasDeveloperInstructionsOverride
                || hasPersonalityOverride
                || hasWebSearchModeOverride
                || hasModelContextWindowOverride
                || hasModelAutoCompactTokenLimitOverride
                || hasModelAutoCompactTokenLimitScopeOverride
                || hasModelInstructionsFileOverride
                || hasCompactPromptOverride
                || hasCompactPromptFileOverride;
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
                    && !string.Equals(key, DeveloperInstructionsKey, StringComparison.Ordinal)
                    && !string.Equals(key, PersonalityKey, StringComparison.Ordinal)
                    && !string.Equals(key, WebSearchKey, StringComparison.Ordinal)
                    && !string.Equals(key, ModelContextWindowKey, StringComparison.Ordinal)
                    && !string.Equals(key, ModelAutoCompactTokenLimitKey, StringComparison.Ordinal)
                    && !string.Equals(key, ModelAutoCompactTokenLimitScopeKey, StringComparison.Ordinal)
                    && !string.Equals(key, ModelInstructionsFileKey, StringComparison.Ordinal)
                    && !string.Equals(key, CompactPromptKey, StringComparison.Ordinal)
                    && !string.Equals(key, ExperimentalCompactPromptFileKey, StringComparison.Ordinal))
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
                else if ((string.Equals(key, DeveloperInstructionsKey, StringComparison.Ordinal)
                        || string.Equals(key, CompactPromptKey, StringComparison.Ordinal))
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

        private static bool TryParseModelContextWindowTokens(
            string value,
            out int contextWindowTokens)
        {
            contextWindowTokens = 0;
            var normalized = (value ?? string.Empty)
                .Replace("_", string.Empty, StringComparison.Ordinal)
                .Trim();
            return int.TryParse(
                    normalized,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out contextWindowTokens)
                && contextWindowTokens >= CopilotAgentTokenBudget.MinimumContextWindowTokens
                && contextWindowTokens <= CopilotAgentTokenBudget.MaximumContextWindowTokens;
        }

        private static bool TryParsePositiveTokenLimit(
            string value,
            out int tokenLimit)
        {
            tokenLimit = 0;
            var normalized = (value ?? string.Empty)
                .Replace("_", string.Empty, StringComparison.Ordinal)
                .Trim();
            return int.TryParse(
                    normalized,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out tokenLimit)
                && tokenLimit > 0;
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
            => TryParseConfiguredText(
                value,
                MaximumDeveloperInstructionCharacters,
                out developerInstructions);

        private static bool TryParseConfiguredText(
            string value,
            int maximumCharacters,
            out string configuredText)
        {
            configuredText = string.Empty;
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
            if (parsed.Length > maximumCharacters || parsed.IndexOf('\0') >= 0)
                return false;
            configuredText = parsed;
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
            bool HasDeveloperInstructionsOverride)
        {
            public string ConfiguredModelInstructionsFilePath { get; init; } = string.Empty;

            public string ModelInstructionsFileContent { get; init; } = string.Empty;

            public string ModelInstructionsSourceFilePath { get; init; } = string.Empty;

            public bool HasModelInstructionsFileOverride { get; init; }

            public CopilotResponsePersonality Personality { get; init; } =
                CopilotResponsePersonality.None;

            public bool HasPersonalityOverride { get; init; }

            public CopilotCodexWebSearchMode WebSearchMode { get; init; } =
                CopilotCodexWebSearchMode.Unspecified;

            public bool HasWebSearchModeOverride { get; init; }

            public int ModelContextWindowTokens { get; init; }

            public bool HasModelContextWindowOverride { get; init; }

            public int ModelAutoCompactTokenLimit { get; init; }

            public bool HasModelAutoCompactTokenLimitOverride { get; init; }

            public CopilotModelAutoCompactTokenLimitScope ModelAutoCompactTokenLimitScope { get; init; } =
                CopilotModelAutoCompactTokenLimitScope.Unspecified;

            public bool HasModelAutoCompactTokenLimitScopeOverride { get; init; }

            public string CompactPrompt { get; init; } = string.Empty;

            public string ConfiguredCompactPromptFilePath { get; init; } = string.Empty;

            public string CompactPromptFileContent { get; init; } = string.Empty;

            public string CompactPromptSourceFilePath { get; init; } = string.Empty;

            public bool HasCompactPromptOverride { get; init; }

            public bool HasCompactPromptFileOverride { get; init; }
        }
    }
}
