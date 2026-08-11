using ColorVision.Copilot;
using System;
using System.IO;
using System.Linq;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotAgentProjectInstructionsTests
{
    [Fact]
    public void DiscoversClaudeInstructionsWhenAgentsInstructionsAreMissing()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            string claudePath = Path.Combine(root, "CLAUDE.md");
            File.WriteAllText(claudePath, "# Claude project instructions");

            var documents = CopilotAgentProjectInstructions.Discover([root], activeDocumentPath: null);

            CopilotProjectInstructionDocument document = Assert.Single(documents);
            Assert.Equal(claudePath, document.Path, ignoreCase: true);
            Assert.Contains("Claude project instructions", document.Content, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void DiscoversClaudeInstructionsFromTheClaudeProjectDirectory()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            string claudeDirectory = Path.Combine(root, ".claude");
            Directory.CreateDirectory(claudeDirectory);
            string claudePath = Path.Combine(claudeDirectory, "CLAUDE.md");
            File.WriteAllText(claudePath, "# Claude directory instructions");

            var documents = CopilotAgentProjectInstructions.Discover([root], activeDocumentPath: null);

            CopilotProjectInstructionDocument document = Assert.Single(documents);
            Assert.Equal(claudePath, document.Path, ignoreCase: true);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void AgentsInstructionsWinOverClaudeFallbackInTheSameDirectory()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            string agentsPath = Path.Combine(root, "AGENTS.md");
            File.WriteAllText(agentsPath, "# Agents instructions");
            File.WriteAllText(Path.Combine(root, "CLAUDE.md"), "# Claude instructions");

            var documents = CopilotAgentProjectInstructions.Discover([root], activeDocumentPath: null);

            CopilotProjectInstructionDocument document = Assert.Single(documents);
            Assert.Equal(agentsPath, document.Path, ignoreCase: true);
            Assert.DoesNotContain("Claude instructions", document.Content, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void AgentsOverrideWinsOverOtherInstructionFallbacks()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            string overridePath = Path.Combine(root, "AGENTS.override.md");
            File.WriteAllText(overridePath, "# Override instructions");
            File.WriteAllText(Path.Combine(root, "AGENTS.md"), "# Agents instructions");
            File.WriteAllText(Path.Combine(root, "CLAUDE.md"), "# Claude instructions");

            var documents = CopilotAgentProjectInstructions.Discover([root], activeDocumentPath: null);

            CopilotProjectInstructionDocument document = Assert.Single(documents);
            Assert.Equal(overridePath, document.Path, ignoreCase: true);
            Assert.Contains("Override instructions", document.Content, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void EmptyAgentsOverrideFallsBackToAgentsInstructions()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            File.WriteAllText(Path.Combine(root, "AGENTS.override.md"), "  ");
            string agentsPath = Path.Combine(root, "AGENTS.md");
            File.WriteAllText(agentsPath, "# Agents instructions");
            File.WriteAllText(Path.Combine(root, "CLAUDE.md"), "# Claude instructions");

            CopilotProjectInstructionDocument document = Assert.Single(
                CopilotAgentProjectInstructions.Discover([root], activeDocumentPath: null));

            Assert.Equal(agentsPath, document.Path, ignoreCase: true);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void GlobalAgentsInstructionsPrecedeTheProjectInstructionChain()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        string nested = Path.Combine(projectRoot, "src");
        Directory.CreateDirectory(nested);
        string activeDocument = Path.Combine(nested, "Feature.cs");
        File.WriteAllText(activeDocument, "namespace Feature;");
        try
        {
            string globalPath = Path.Combine(globalRoot, "AGENTS.md");
            string projectPath = Path.Combine(projectRoot, "AGENTS.md");
            string nestedPath = Path.Combine(nested, "AGENTS.override.md");
            File.WriteAllText(globalPath, "# Personal instructions");
            File.WriteAllText(projectPath, "# Project instructions");
            File.WriteAllText(nestedPath, "# Nested instructions");

            var documents = CopilotAgentProjectInstructions.DiscoverWithGlobal(
                [projectRoot],
                activeDocument,
                additionalTargetFilePaths: null,
                globalInstructionRootPath: globalRoot);

            Assert.Equal(
                [globalPath, projectPath, nestedPath],
                documents.Select(document => document.Path).ToArray(),
                StringComparer.OrdinalIgnoreCase);
            Assert.Equal(globalRoot, CopilotAgentProjectInstructions.ResolveGlobalInstructionRootPath(globalRoot));
            Assert.Equal(string.Empty, CopilotAgentProjectInstructions.NormalizeGlobalInstructionRootPath("relative\\.codex"));
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void EmptyGlobalOverrideFallsBackToGlobalAgentsInstructions()
    {
        string globalRoot = CreateTemporaryDirectory();
        try
        {
            File.WriteAllText(Path.Combine(globalRoot, "AGENTS.override.md"), "<!-- empty -->");
            string globalPath = Path.Combine(globalRoot, "AGENTS.md");
            File.WriteAllText(globalPath, "# Personal instructions");

            CopilotProjectInstructionDocument document = Assert.Single(
                CopilotAgentProjectInstructions.DiscoverWithGlobal(
                    searchRootPaths: null,
                    activeDocumentPath: null,
                    additionalTargetFilePaths: null,
                    globalInstructionRootPath: globalRoot));

            Assert.Equal(globalPath, document.Path, ignoreCase: true);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
        }
    }

    [Fact]
    public void CodexConfigAddsSafeFallbackNamesAndUtf8Budget()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                """
                project_doc_max_bytes = 4_096
                project_doc_fallback_filenames = [
                  "TEAM_GUIDE.md",
                  '.agents.md',
                  "../outside.md",
                  "nested/guide.md",
                  "TEAM_GUIDE.md", # duplicate
                ]

                [model]
                name = "ignored"
                project_doc_max_bytes = 65536
                """);
            string configuredPath = Path.Combine(projectRoot, "TEAM_GUIDE.md");
            File.WriteAllText(configuredPath, "# Configured instructions");
            File.WriteAllText(Path.Combine(projectRoot, "CLAUDE.md"), "# Compatibility instructions");

            var options = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot);
            CopilotProjectInstructionDocument document = Assert.Single(
                CopilotAgentProjectInstructions.DiscoverWithGlobal(
                    [projectRoot],
                    activeDocumentPath: null,
                    additionalTargetFilePaths: null,
                    globalInstructionRootPath: globalRoot,
                    discoveryOptions: options));

            Assert.Equal(4_096, options.MaximumBytes);
            Assert.Equal(["TEAM_GUIDE.md", ".agents.md"], options.FallbackFileNames);
            Assert.True(options.HasMaximumBytesOverride);
            Assert.True(options.HasFallbackFileNamesOverride);
            Assert.Equal(configuredPath, document.Path, ignoreCase: true);
            var report = CopilotProjectInstructionDiagnostics.Format(
                new CopilotProjectInstructionSnapshot(
                    projectRoot,
                    string.Empty,
                    globalRoot,
                    options,
                    [document]),
                hasActiveAgentRun: false);
            Assert.Contains("4,096 UTF-8", report, StringComparison.Ordinal);
            Assert.Contains("TEAM_GUIDE.md", report, StringComparison.Ordinal);
            Assert.Contains("Codex Home config.toml 请求快照", report, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void TrustedProjectConfigOverridesTheCodexHomeLayer()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            string configuredProjectPath = projectRoot
                .ToUpperInvariant()
                .Replace("\\", "\\\\", StringComparison.Ordinal);
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                $"""
                project_doc_max_bytes = 4096
                project_doc_fallback_filenames = ["GLOBAL_GUIDE.md"]

                [projects."{configuredProjectPath}"]
                trust_level = "trusted"
                """);
            string projectConfigDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(projectConfigDirectory);
            File.WriteAllText(
                Path.Combine(projectConfigDirectory, "config.toml"),
                "project_doc_fallback_filenames = [\"PROJECT_GUIDE.md\"]");
            string configuredPath = Path.Combine(projectRoot, "PROJECT_GUIDE.md");
            File.WriteAllText(configuredPath, "# Project-specific instructions");

            var options = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot, projectRoot);
            var document = Assert.Single(CopilotAgentProjectInstructions.DiscoverWithGlobal(
                [projectRoot],
                activeDocumentPath: null,
                additionalTargetFilePaths: null,
                globalInstructionRootPath: globalRoot,
                discoveryOptions: options));

            Assert.Equal(4096, options.MaximumBytes);
            Assert.Equal(["PROJECT_GUIDE.md"], options.FallbackFileNames);
            Assert.Equal(
                CopilotProjectInstructionConfigSources.CodexHome
                    | CopilotProjectInstructionConfigSources.TrustedProject,
                options.ConfigSources);
            Assert.Equal(CopilotCodexProjectTrustLevel.Trusted, options.ProjectTrustLevel);
            Assert.Equal(configuredPath, document.Path, ignoreCase: true);
            var report = CopilotProjectInstructionDiagnostics.Format(
                new CopilotProjectInstructionSnapshot(
                    projectRoot,
                    string.Empty,
                    globalRoot,
                    options,
                    [document]),
                hasActiveAgentRun: false);
            Assert.Contains("Codex Home + 受信项目 .codex/config.toml 请求快照", report, StringComparison.Ordinal);
            Assert.Contains("Codex Home trust_level=trusted", report, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void ExplicitlyUntrustedProjectSkipsProjectConfigAndReportsIt()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                $"""
                project_doc_fallback_filenames = ["GLOBAL_GUIDE.md"]

                [projects.'{projectRoot}']
                trust_level = "untrusted"
                """);
            string projectConfigDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(projectConfigDirectory);
            File.WriteAllText(
                Path.Combine(projectConfigDirectory, "config.toml"),
                "project_doc_fallback_filenames = [\"PROJECT_GUIDE.md\"]");
            string globalGuidePath = Path.Combine(projectRoot, "GLOBAL_GUIDE.md");
            File.WriteAllText(globalGuidePath, "# Global fallback instructions");
            File.WriteAllText(Path.Combine(projectRoot, "PROJECT_GUIDE.md"), "# Project instructions");

            var options = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot, projectRoot);
            var document = Assert.Single(CopilotAgentProjectInstructions.DiscoverWithGlobal(
                [projectRoot],
                activeDocumentPath: null,
                additionalTargetFilePaths: null,
                globalInstructionRootPath: globalRoot,
                discoveryOptions: options));

            Assert.Equal(CopilotCodexProjectTrustLevel.Untrusted, options.ProjectTrustLevel);
            Assert.False(options.AllowsProjectCodexConfig);
            Assert.Equal(["GLOBAL_GUIDE.md"], options.FallbackFileNames);
            Assert.Equal(CopilotProjectInstructionConfigSources.CodexHome, options.ConfigSources);
            Assert.Equal(globalGuidePath, document.Path, ignoreCase: true);
            var report = CopilotProjectInstructionDiagnostics.Format(
                new CopilotProjectInstructionSnapshot(
                    projectRoot,
                    string.Empty,
                    globalRoot,
                    options,
                    [document]),
                hasActiveAgentRun: false);
            Assert.Contains("trust_level=untrusted", report, StringComparison.Ordinal);
            Assert.Contains("已跳过项目 .codex/config.toml", report, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void UndecidedProjectFailsClosedUntilTrustIsPersisted()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                "project_doc_fallback_filenames = [\"GLOBAL_GUIDE.md\"]");
            string projectConfigDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(projectConfigDirectory);
            File.WriteAllText(
                Path.Combine(projectConfigDirectory, "config.toml"),
                "project_doc_fallback_filenames = [\"PROJECT_GUIDE.md\"]");

            var undecided = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot, projectRoot);

            Assert.Equal(CopilotCodexProjectTrustLevel.Unspecified, undecided.ProjectTrustLevel);
            Assert.False(undecided.AllowsProjectCodexConfig);
            Assert.Equal(["GLOBAL_GUIDE.md"], undecided.FallbackFileNames);
            Assert.Empty(undecided.AppliedProjectConfigFilePaths);
            Assert.True(CopilotCodexProjectTrustPersistence.RequiresDecision(projectRoot, undecided));
            Assert.Contains("信任未决定", undecided.ProjectTrustLabel, StringComparison.Ordinal);

            Assert.True(
                CopilotCodexProjectTrustPersistence.TryTrustProject(globalRoot, projectRoot, out var error),
                error);
            var trusted = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot, projectRoot);

            Assert.Equal(CopilotCodexProjectTrustLevel.Trusted, trusted.ProjectTrustLevel);
            Assert.True(trusted.AllowsProjectCodexConfig);
            Assert.Equal(["PROJECT_GUIDE.md"], trusted.FallbackFileNames);
            Assert.False(CopilotCodexProjectTrustPersistence.RequiresDecision(projectRoot, trusted));
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void SubmissionAdmissionRecapturesOnlyAfterTrustIsPersisted()
    {
        var initialSnapshot = new CopilotAgentHostContextSnapshot(
            activeDocumentPath: "initial.cs",
            solutionDirectoryPath: "initial-workspace",
            attachments: null);
        var refreshedSnapshot = new CopilotAgentHostContextSnapshot(
            activeDocumentPath: "refreshed.cs",
            solutionDirectoryPath: "refreshed-workspace",
            attachments: null);
        var recaptureCount = 0;
        CopilotAgentHostContextSnapshot Recapture()
        {
            recaptureCount++;
            return refreshedSnapshot;
        }

        Assert.False(CopilotProjectTrustSubmissionAdmission.TryResolve(
            initialSnapshot,
            _ => new CopilotProjectTrustAdmissionDecision(IsAllowed: false, TrustPersisted: false),
            Recapture,
            out var rejected));
        Assert.Same(initialSnapshot, rejected);
        Assert.Equal(0, recaptureCount);

        Assert.True(CopilotProjectTrustSubmissionAdmission.TryResolve(
            initialSnapshot,
            _ => new CopilotProjectTrustAdmissionDecision(IsAllowed: true, TrustPersisted: false),
            Recapture,
            out var alreadyDecided));
        Assert.Same(initialSnapshot, alreadyDecided);
        Assert.Equal(0, recaptureCount);

        Assert.True(CopilotProjectTrustSubmissionAdmission.TryResolve(
            initialSnapshot,
            _ => new CopilotProjectTrustAdmissionDecision(IsAllowed: true, TrustPersisted: true),
            Recapture,
            out var trusted));
        Assert.Same(refreshedSnapshot, trusted);
        Assert.Equal(1, recaptureCount);
    }

    [Fact]
    public void TrustPersistencePreservesExistingProjectTableAndRefusesExplicitDecision()
    {
        string globalRoot = CreateTemporaryDirectory();
        string trustedProjectRoot = CreateTemporaryDirectory();
        string untrustedProjectRoot = CreateTemporaryDirectory();
        string headerOnlyProjectRoot = CreateTemporaryDirectory();
        try
        {
            string configPath = Path.Combine(globalRoot, "config.toml");
            File.WriteAllText(
                configPath,
                $"""
                model = "gpt-5"

                [projects.'{trustedProjectRoot}']
                custom_value = "preserved"

                [projects.'{untrustedProjectRoot}']
                trust_level = "untrusted"
                """);

            Assert.True(
                CopilotCodexProjectTrustPersistence.TryTrustProject(
                    globalRoot,
                    trustedProjectRoot,
                    out var trustError),
                trustError);
            string persisted = File.ReadAllText(configPath);
            Assert.Contains("custom_value = \"preserved\"", persisted, StringComparison.Ordinal);
            Assert.Contains("model = \"gpt-5\"", persisted, StringComparison.Ordinal);
            Assert.Equal(
                CopilotCodexProjectTrustLevel.Trusted,
                CopilotProjectInstructionDiscoveryConfig.Load(globalRoot, trustedProjectRoot).ProjectTrustLevel);

            Assert.False(
                CopilotCodexProjectTrustPersistence.TryTrustProject(
                    globalRoot,
                    untrustedProjectRoot,
                    out var refusal));
            Assert.Contains("未覆盖现有决定", refusal, StringComparison.Ordinal);
            Assert.Equal(
                CopilotCodexProjectTrustLevel.Untrusted,
                CopilotProjectInstructionDiscoveryConfig.Load(globalRoot, untrustedProjectRoot).ProjectTrustLevel);

            File.WriteAllText(configPath, $"[projects.'{headerOnlyProjectRoot}']");
            Assert.True(
                CopilotCodexProjectTrustPersistence.TryTrustProject(
                    globalRoot,
                    headerOnlyProjectRoot,
                    out var headerOnlyError),
                headerOnlyError);
            Assert.Contains(
                $"[projects.'{headerOnlyProjectRoot}']{Environment.NewLine}trust_level = \"trusted\"",
                File.ReadAllText(configPath),
                StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(trustedProjectRoot, recursive: true);
            Directory.Delete(untrustedProjectRoot, recursive: true);
            Directory.Delete(headerOnlyProjectRoot, recursive: true);
        }
    }

    [Fact]
    public void InvalidProjectTrustLevelFailsClosedForProjectConfig()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                $"""
                project_doc_fallback_filenames = ["GLOBAL_GUIDE.md"]

                [projects.'{projectRoot}']
                trust_level = "maybe"
                """);
            string projectConfigDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(projectConfigDirectory);
            File.WriteAllText(
                Path.Combine(projectConfigDirectory, "config.toml"),
                "project_doc_fallback_filenames = [\"PROJECT_GUIDE.md\"]");

            var options = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot, projectRoot);

            Assert.Equal(CopilotCodexProjectTrustLevel.Invalid, options.ProjectTrustLevel);
            Assert.False(options.AllowsProjectCodexConfig);
            Assert.Equal(["GLOBAL_GUIDE.md"], options.FallbackFileNames);
            Assert.Equal(CopilotProjectInstructionConfigSources.CodexHome, options.ConfigSources);
            Assert.Contains("trust_level 无效", options.ProjectTrustLabel, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void DuplicateProjectTrustLevelFailsClosedForProjectConfig()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                $"[projects.'{projectRoot}']\ntrust_level = \"untrusted\"\ntrust_level = \"trusted\"");
            string projectConfigDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(projectConfigDirectory);
            File.WriteAllText(
                Path.Combine(projectConfigDirectory, "config.toml"),
                "project_doc_fallback_filenames = [\"PROJECT_GUIDE.md\"]");

            var options = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot, projectRoot);

            Assert.Equal(CopilotCodexProjectTrustLevel.Invalid, options.ProjectTrustLevel);
            Assert.False(options.AllowsProjectCodexConfig);
            Assert.Equal(CopilotProjectInstructionConfigSources.None, options.ConfigSources);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void RequestFactoryKeepsTheProjectTrustDecisionTakenAtSubmission()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            string globalConfigPath = Path.Combine(globalRoot, "config.toml");
            File.WriteAllText(
                globalConfigPath,
                $"[projects.'{projectRoot}']\ntrust_level = \"trusted\"");
            string projectConfigDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(projectConfigDirectory);
            File.WriteAllText(
                Path.Combine(projectConfigDirectory, "config.toml"),
                "project_doc_fallback_filenames = [\"PROJECT_GUIDE.md\"]");
            string guidePath = Path.Combine(projectRoot, "PROJECT_GUIDE.md");
            string activeDocument = Path.Combine(projectRoot, "Feature.cs");
            File.WriteAllText(guidePath, "# Trusted project instructions");
            File.WriteAllText(activeDocument, "namespace Feature;");
            var submittedContext = new CopilotAgentHostContextSnapshot(
                activeDocument,
                projectRoot,
                attachments: null,
                liveContext: null,
                conversationHistory: null,
                additionalReadRootPaths: null,
                globalInstructionRootPath: globalRoot);

            File.WriteAllText(
                globalConfigPath,
                $"[projects.'{projectRoot}']\ntrust_level = \"untrusted\"");

            var submittedPlan = CopilotAgentRequestFactory.Prepare(
                $"Inspect the local implementation in {activeDocument}",
                CopilotAgentMode.Auto,
                submittedContext);
            var refreshedContext = new CopilotAgentHostContextSnapshot(
                activeDocument,
                projectRoot,
                attachments: null,
                liveContext: null,
                conversationHistory: null,
                additionalReadRootPaths: null,
                globalInstructionRootPath: globalRoot);
            var refreshedPlan = CopilotAgentRequestFactory.Prepare(
                $"Inspect the local implementation in {activeDocument}",
                CopilotAgentMode.Auto,
                refreshedContext);

            Assert.Equal(CopilotCodexProjectTrustLevel.Trusted, submittedContext.ProjectInstructionDiscoveryOptions.ProjectTrustLevel);
            Assert.Contains(submittedPlan.ProjectInstructions, document =>
                string.Equals(document.Path, guidePath, StringComparison.OrdinalIgnoreCase));
            Assert.Equal(CopilotCodexProjectTrustLevel.Untrusted, refreshedContext.ProjectInstructionDiscoveryOptions.ProjectTrustLevel);
            Assert.DoesNotContain(refreshedPlan.ProjectInstructions, document =>
                string.Equals(document.Path, guidePath, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void RequestFactoryKeepsTheTrustedProjectConfigSnapshotTakenAtSubmission()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            string projectConfigDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(projectConfigDirectory);
            string configPath = Path.Combine(projectConfigDirectory, "config.toml");
            string firstPath = Path.Combine(projectRoot, "FIRST.md");
            string secondPath = Path.Combine(projectRoot, "SECOND.md");
            string activeDocument = Path.Combine(projectRoot, "Feature.cs");
            File.WriteAllText(configPath, "project_doc_fallback_filenames = [\"FIRST.md\"]");
            File.WriteAllText(firstPath, "# First instructions");
            File.WriteAllText(secondPath, "# Second instructions");
            File.WriteAllText(activeDocument, "namespace Feature;");
            TrustProject(globalRoot, projectRoot);
            var submittedContext = new CopilotAgentHostContextSnapshot(
                activeDocument,
                projectRoot,
                attachments: null,
                liveContext: null,
                conversationHistory: null,
                additionalReadRootPaths: null,
                globalInstructionRootPath: globalRoot);

            File.WriteAllText(configPath, "project_doc_fallback_filenames = [\"SECOND.md\"]");

            var submittedPlan = CopilotAgentRequestFactory.Prepare(
                $"Inspect the local implementation in {activeDocument}",
                CopilotAgentMode.Auto,
                submittedContext);
            var refreshedContext = new CopilotAgentHostContextSnapshot(
                activeDocument,
                projectRoot,
                attachments: null,
                liveContext: null,
                conversationHistory: null,
                additionalReadRootPaths: null,
                globalInstructionRootPath: globalRoot);
            var refreshedPlan = CopilotAgentRequestFactory.Prepare(
                $"Inspect the local implementation in {activeDocument}",
                CopilotAgentMode.Auto,
                refreshedContext);

            Assert.Contains(submittedPlan.ProjectInstructions, document =>
                string.Equals(document.Path, firstPath, StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(submittedPlan.ProjectInstructions, document =>
                string.Equals(document.Path, secondPath, StringComparison.OrdinalIgnoreCase));
            Assert.Contains(refreshedPlan.ProjectInstructions, document =>
                string.Equals(document.Path, secondPath, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void AdditionalReadRootsDoNotBecomeProjectConfigRoots()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        string additionalRoot = CreateTemporaryDirectory();
        try
        {
            string additionalConfigDirectory = Path.Combine(additionalRoot, ".codex");
            Directory.CreateDirectory(additionalConfigDirectory);
            File.WriteAllText(
                Path.Combine(additionalConfigDirectory, "config.toml"),
                "project_doc_fallback_filenames = [\"UNTRUSTED_GUIDE.md\"]");
            File.WriteAllText(Path.Combine(additionalRoot, "UNTRUSTED_GUIDE.md"), "# Not a project config root");
            string activeDocument = Path.Combine(projectRoot, "Feature.cs");
            File.WriteAllText(activeDocument, "namespace Feature;");

            var hostContext = new CopilotAgentHostContextSnapshot(
                activeDocument,
                projectRoot,
                attachments: null,
                liveContext: null,
                conversationHistory: null,
                additionalReadRootPaths: [additionalRoot],
                globalInstructionRootPath: globalRoot);
            var plan = CopilotAgentRequestFactory.Prepare(
                $"Inspect the local implementation in {activeDocument}",
                CopilotAgentMode.Auto,
                hostContext);

            Assert.False(hostContext.ProjectInstructionDiscoveryOptions.UsesCodexConfig);
            Assert.Equal([projectRoot], plan.TrustedProjectRootPaths, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain(plan.ProjectInstructions, document =>
                document.Path.StartsWith(additionalRoot, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
            Directory.Delete(additionalRoot, recursive: true);
        }
    }

    [Fact]
    public void ProjectConfigThroughAReparsePointIsRejected()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        string outsideRoot = CreateTemporaryDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(outsideRoot, "config.toml"),
                "project_doc_fallback_filenames = [\"OUTSIDE.md\"]");
            Directory.CreateSymbolicLink(Path.Combine(projectRoot, ".codex"), outsideRoot);

            var options = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot, projectRoot);

            Assert.False(options.UsesCodexConfig);
            Assert.Empty(options.FallbackFileNames);
        }
        finally
        {
            Directory.Delete(projectRoot, recursive: true);
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(outsideRoot, recursive: true);
        }
    }

    [Fact]
    public void PublicHostContextDoesNotImplicitlyTrustProjectConfig()
    {
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            string projectConfigDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(projectConfigDirectory);
            File.WriteAllText(
                Path.Combine(projectConfigDirectory, "config.toml"),
                "project_doc_fallback_filenames = [\"PROJECT_GUIDE.md\"]");
            string activeDocument = Path.Combine(projectRoot, "Feature.cs");
            File.WriteAllText(activeDocument, "namespace Feature;");

            var hostContext = new CopilotAgentHostContextSnapshot(
                activeDocument,
                projectRoot,
                attachments: null);

            Assert.False(hostContext.ProjectInstructionDiscoveryOptions.UsesCodexConfig);
            Assert.Empty(hostContext.ProjectInstructionDiscoveryOptions.FallbackFileNames);
        }
        finally
        {
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StandaloneFileUsesNearestGitRootForProjectContext(bool markerIsFile)
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            string gitMarkerPath = Path.Combine(projectRoot, ".git");
            if (markerIsFile)
                File.WriteAllText(gitMarkerPath, "gitdir: worktree-metadata");
            else
                Directory.CreateDirectory(gitMarkerPath);
            string projectConfigDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(projectConfigDirectory);
            File.WriteAllText(
                Path.Combine(projectConfigDirectory, "config.toml"),
                "project_doc_fallback_filenames = [\"ROOT_GUIDE.md\"]");
            string rootInstructions = Path.Combine(projectRoot, "ROOT_GUIDE.md");
            File.WriteAllText(rootInstructions, "# Repository instructions");
            string sourceDirectory = Path.Combine(projectRoot, "src", "feature");
            Directory.CreateDirectory(sourceDirectory);
            string nestedInstructions = Path.Combine(sourceDirectory, "AGENTS.md");
            string activeDocument = Path.Combine(sourceDirectory, "Feature.cs");
            File.WriteAllText(nestedInstructions, "# Feature instructions");
            File.WriteAllText(activeDocument, "namespace Feature;");
            TrustProject(globalRoot, projectRoot);
            var hostContext = new CopilotAgentHostContextSnapshot(
                activeDocument,
                solutionDirectoryPath: null,
                attachments: null,
                liveContext: null,
                conversationHistory: null,
                additionalReadRootPaths: null,
                globalInstructionRootPath: globalRoot);

            var plan = CopilotAgentRequestFactory.Prepare(
                $"Inspect the local implementation in {activeDocument}",
                CopilotAgentMode.Auto,
                hostContext);

            Assert.Equal(projectRoot, hostContext.PrimaryTrustedProjectRootPath, ignoreCase: true);
            Assert.Equal([projectRoot], plan.TrustedProjectRootPaths, StringComparer.OrdinalIgnoreCase);
            Assert.Equal(
                CopilotProjectInstructionConfigSources.TrustedProject,
                hostContext.ProjectInstructionDiscoveryOptions.ConfigSources);
            Assert.Equal(
                [rootInstructions, nestedInstructions],
                plan.ProjectInstructions.Select(document => document.Path).ToArray(),
                StringComparer.OrdinalIgnoreCase);
            Assert.Empty(plan.WritableLocalRootPaths);
            Assert.DoesNotContain(plan.SearchRootPaths, path =>
                string.Equals(path, projectRoot, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void HostContextKeepsTheProjectRootDecisionTakenAtSubmission()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            string projectConfigDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(projectConfigDirectory);
            File.WriteAllText(
                Path.Combine(projectConfigDirectory, "config.toml"),
                "project_doc_fallback_filenames = [\"ROOT_GUIDE.md\"]");
            string rootInstructions = Path.Combine(projectRoot, "ROOT_GUIDE.md");
            File.WriteAllText(rootInstructions, "# Repository instructions");
            string sourceDirectory = Path.Combine(projectRoot, "src", "feature");
            Directory.CreateDirectory(sourceDirectory);
            string activeDocument = Path.Combine(sourceDirectory, "Feature.cs");
            File.WriteAllText(activeDocument, "namespace Feature;");
            TrustProject(globalRoot, sourceDirectory);
            var submittedContext = new CopilotAgentHostContextSnapshot(
                activeDocument,
                solutionDirectoryPath: null,
                attachments: null,
                liveContext: null,
                conversationHistory: null,
                additionalReadRootPaths: null,
                globalInstructionRootPath: globalRoot);

            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            TrustProject(globalRoot, projectRoot);

            var submittedPlan = CopilotAgentRequestFactory.Prepare(
                $"Inspect the local implementation in {activeDocument}",
                CopilotAgentMode.Auto,
                submittedContext);
            var refreshedContext = new CopilotAgentHostContextSnapshot(
                activeDocument,
                solutionDirectoryPath: null,
                attachments: null,
                liveContext: null,
                conversationHistory: null,
                additionalReadRootPaths: null,
                globalInstructionRootPath: globalRoot);
            var refreshedPlan = CopilotAgentRequestFactory.Prepare(
                $"Inspect the local implementation in {activeDocument}",
                CopilotAgentMode.Auto,
                refreshedContext);

            Assert.Equal(sourceDirectory, submittedContext.PrimaryTrustedProjectRootPath, ignoreCase: true);
            Assert.Equal([sourceDirectory], submittedPlan.TrustedProjectRootPaths, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain(submittedPlan.ProjectInstructions, document =>
                string.Equals(document.Path, rootInstructions, StringComparison.OrdinalIgnoreCase));
            Assert.Equal(projectRoot, refreshedContext.PrimaryTrustedProjectRootPath, ignoreCase: true);
            Assert.Equal([projectRoot], refreshedPlan.TrustedProjectRootPaths, StringComparer.OrdinalIgnoreCase);
            Assert.Contains(refreshedPlan.ProjectInstructions, document =>
                string.Equals(document.Path, rootInstructions, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void ConfiguredProjectRootMarkersControlProjectRootDiscovery()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                """
                project_root_markers = [
                  ".workspace",
                  ".hg",
                ]
                """);
            File.WriteAllText(Path.Combine(projectRoot, ".workspace"), string.Empty);
            string sourceDirectory = Path.Combine(projectRoot, "src");
            Directory.CreateDirectory(Path.Combine(sourceDirectory, ".git"));
            string activeDirectory = Path.Combine(sourceDirectory, "feature");
            Directory.CreateDirectory(activeDirectory);
            string activeDocument = Path.Combine(activeDirectory, "Feature.cs");
            File.WriteAllText(activeDocument, "namespace Feature;");
            string projectConfigDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(projectConfigDirectory);
            File.WriteAllText(
                Path.Combine(projectConfigDirectory, "config.toml"),
                "project_doc_fallback_filenames = [\"ROOT_GUIDE.md\"]");
            string rootInstructions = Path.Combine(projectRoot, "ROOT_GUIDE.md");
            File.WriteAllText(rootInstructions, "# Repository instructions");
            TrustProject(globalRoot, projectRoot);

            var hostContext = new CopilotAgentHostContextSnapshot(
                activeDocument,
                solutionDirectoryPath: null,
                attachments: null,
                liveContext: null,
                conversationHistory: null,
                additionalReadRootPaths: null,
                globalInstructionRootPath: globalRoot);
            var plan = CopilotAgentRequestFactory.Prepare(
                $"Inspect the local implementation in {activeDocument}",
                CopilotAgentMode.Auto,
                hostContext);

            Assert.Equal(projectRoot, hostContext.PrimaryTrustedProjectRootPath, ignoreCase: true);
            Assert.Equal([".workspace", ".hg"], hostContext.ProjectInstructionDiscoveryOptions.ProjectRootMarkers);
            Assert.True(hostContext.ProjectInstructionDiscoveryOptions.HasProjectRootMarkersOverride);
            Assert.Equal(
                CopilotProjectInstructionConfigSources.CodexHome
                    | CopilotProjectInstructionConfigSources.TrustedProject,
                hostContext.ProjectInstructionDiscoveryOptions.ConfigSources);
            Assert.Contains(plan.ProjectInstructions, document =>
                string.Equals(document.Path, rootInstructions, StringComparison.OrdinalIgnoreCase));
            var report = CopilotProjectInstructionDiagnostics.Format(
                new CopilotProjectInstructionSnapshot(
                    projectRoot,
                    activeDocument,
                    globalRoot,
                    hostContext.ProjectInstructionDiscoveryOptions,
                    plan.ProjectInstructions),
                hasActiveAgentRun: false);
            Assert.Contains("项目根标记：.workspace、.hg", report, StringComparison.Ordinal);
            Assert.Contains("Codex Home 请求快照", report, StringComparison.Ordinal);
            var contextReport = CopilotContextDiagnostics.Format(new CopilotContextDiagnosticSnapshot
            {
                AgentContextEnabled = true,
                ProjectInstructionUsesCodexConfig = true,
                ProjectInstructionConfigSourceLabel =
                    hostContext.ProjectInstructionDiscoveryOptions.ConfigSourceLabel,
                ProjectInstructionRootMarkers =
                    hostContext.ProjectInstructionDiscoveryOptions.ProjectRootMarkers,
                ProjectInstructionHasRootMarkersOverride = true,
                TrustedProjectRootPaths = plan.TrustedProjectRootPaths,
            });
            Assert.Contains(
                "项目根标记：.workspace、.hg（Codex Home 请求快照）",
                contextReport,
                StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void EmptyProjectRootMarkersKeepTheCurrentWorkingDirectoryAsRoot()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            File.WriteAllText(Path.Combine(globalRoot, "config.toml"), "project_root_markers = []");
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            string sourceDirectory = Path.Combine(projectRoot, "src", "feature");
            Directory.CreateDirectory(sourceDirectory);
            string activeDocument = Path.Combine(sourceDirectory, "Feature.cs");
            File.WriteAllText(activeDocument, "namespace Feature;");
            var hostContext = new CopilotAgentHostContextSnapshot(
                activeDocument,
                solutionDirectoryPath: null,
                attachments: null,
                liveContext: null,
                conversationHistory: null,
                additionalReadRootPaths: null,
                globalInstructionRootPath: globalRoot);

            Assert.Equal(sourceDirectory, hostContext.PrimaryTrustedProjectRootPath, ignoreCase: true);
            Assert.Empty(hostContext.ProjectInstructionDiscoveryOptions.ProjectRootMarkers);
            Assert.True(hostContext.ProjectInstructionDiscoveryOptions.HasProjectRootMarkersOverride);
            Assert.Equal(
                CopilotProjectInstructionConfigSources.CodexHome,
                hostContext.ProjectInstructionDiscoveryOptions.ConfigSources);
            var report = CopilotProjectInstructionDiagnostics.Format(
                new CopilotProjectInstructionSnapshot(
                    sourceDirectory,
                    activeDocument,
                    globalRoot,
                    hostContext.ProjectInstructionDiscoveryOptions,
                    Array.Empty<CopilotProjectInstructionDocument>()),
                hasActiveAgentRun: false);
            Assert.Contains(
                "项目根标记：[]（Codex Home 请求快照；不向上搜索）",
                report,
                StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void ProjectConfigCannotChangeTheMarkersUsedToDiscoverItsOwnRoot()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            string projectConfigDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(projectConfigDirectory);
            File.WriteAllText(
                Path.Combine(projectConfigDirectory, "config.toml"),
                "project_root_markers = [\".workspace\"]");
            string sourceDirectory = Path.Combine(projectRoot, "src", "feature");
            Directory.CreateDirectory(sourceDirectory);
            string activeDocument = Path.Combine(sourceDirectory, "Feature.cs");
            File.WriteAllText(activeDocument, "namespace Feature;");

            var hostContext = new CopilotAgentHostContextSnapshot(
                activeDocument,
                solutionDirectoryPath: null,
                attachments: null,
                liveContext: null,
                conversationHistory: null,
                additionalReadRootPaths: null,
                globalInstructionRootPath: globalRoot);

            Assert.Equal(projectRoot, hostContext.PrimaryTrustedProjectRootPath, ignoreCase: true);
            Assert.Equal([".git"], hostContext.ProjectInstructionDiscoveryOptions.ProjectRootMarkers);
            Assert.False(hostContext.ProjectInstructionDiscoveryOptions.HasProjectRootMarkersOverride);
            Assert.Equal(
                CopilotProjectInstructionConfigSources.None,
                hostContext.ProjectInstructionDiscoveryOptions.ConfigSources);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void InvalidProjectRootMarkersFallBackToTheDefaultGitMarker()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                "project_root_markers = [\"../escape\"]");
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            string sourceDirectory = Path.Combine(projectRoot, "src", "feature");
            Directory.CreateDirectory(sourceDirectory);
            string activeDocument = Path.Combine(sourceDirectory, "Feature.cs");
            File.WriteAllText(activeDocument, "namespace Feature;");

            var hostContext = new CopilotAgentHostContextSnapshot(
                activeDocument,
                solutionDirectoryPath: null,
                attachments: null,
                liveContext: null,
                conversationHistory: null,
                additionalReadRootPaths: null,
                globalInstructionRootPath: globalRoot);

            Assert.Equal(projectRoot, hostContext.PrimaryTrustedProjectRootPath, ignoreCase: true);
            Assert.Equal([".git"], hostContext.ProjectInstructionDiscoveryOptions.ProjectRootMarkers);
            Assert.False(hostContext.ProjectInstructionDiscoveryOptions.HasProjectRootMarkersOverride);
            Assert.False(hostContext.ProjectInstructionDiscoveryOptions.UsesCodexConfig);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void HostContextKeepsTheProjectRootMarkerConfigTakenAtSubmission()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            string globalConfigPath = Path.Combine(globalRoot, "config.toml");
            File.WriteAllText(globalConfigPath, "project_root_markers = [\".workspace\"]");
            File.WriteAllText(Path.Combine(projectRoot, ".workspace"), string.Empty);
            string sourceDirectory = Path.Combine(projectRoot, "src", "feature");
            Directory.CreateDirectory(sourceDirectory);
            string activeDocument = Path.Combine(sourceDirectory, "Feature.cs");
            File.WriteAllText(activeDocument, "namespace Feature;");
            var submittedContext = new CopilotAgentHostContextSnapshot(
                activeDocument,
                solutionDirectoryPath: null,
                attachments: null,
                liveContext: null,
                conversationHistory: null,
                additionalReadRootPaths: null,
                globalInstructionRootPath: globalRoot);

            File.WriteAllText(globalConfigPath, "project_root_markers = []");

            var submittedPlan = CopilotAgentRequestFactory.Prepare(
                $"Inspect the local implementation in {activeDocument}",
                CopilotAgentMode.Auto,
                submittedContext);
            var refreshedContext = new CopilotAgentHostContextSnapshot(
                activeDocument,
                solutionDirectoryPath: null,
                attachments: null,
                liveContext: null,
                conversationHistory: null,
                additionalReadRootPaths: null,
                globalInstructionRootPath: globalRoot);
            var refreshedPlan = CopilotAgentRequestFactory.Prepare(
                $"Inspect the local implementation in {activeDocument}",
                CopilotAgentMode.Auto,
                refreshedContext);

            Assert.Equal([".workspace"], submittedContext.ProjectInstructionDiscoveryOptions.ProjectRootMarkers);
            Assert.Equal(projectRoot, submittedContext.PrimaryTrustedProjectRootPath, ignoreCase: true);
            Assert.Equal([projectRoot], submittedPlan.TrustedProjectRootPaths, StringComparer.OrdinalIgnoreCase);
            Assert.Empty(refreshedContext.ProjectInstructionDiscoveryOptions.ProjectRootMarkers);
            Assert.Equal(sourceDirectory, refreshedContext.PrimaryTrustedProjectRootPath, ignoreCase: true);
            Assert.Equal([sourceDirectory], refreshedPlan.TrustedProjectRootPaths, StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void NestedProjectConfigLayersApplyFromTheProjectRootToTheWorkingDirectory()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                $"""
                [projects.'{projectRoot}']
                trust_level = "trusted"
                """);
            string rootConfigDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(rootConfigDirectory);
            string rootConfigPath = Path.Combine(rootConfigDirectory, "config.toml");
            File.WriteAllText(
                rootConfigPath,
                "project_doc_max_bytes = 8192\nproject_doc_fallback_filenames = [\"ROOT_GUIDE.md\"]");
            string sourceDirectory = Path.Combine(projectRoot, "src");
            string sourceConfigDirectory = Path.Combine(sourceDirectory, ".codex");
            Directory.CreateDirectory(sourceConfigDirectory);
            string sourceConfigPath = Path.Combine(sourceConfigDirectory, "config.toml");
            File.WriteAllText(
                sourceConfigPath,
                "project_doc_max_bytes = 4096\nproject_doc_fallback_filenames = [\"SOURCE_GUIDE.md\"]");
            string featureDirectory = Path.Combine(sourceDirectory, "feature");
            string featureConfigDirectory = Path.Combine(featureDirectory, ".codex");
            Directory.CreateDirectory(featureConfigDirectory);
            string featureConfigPath = Path.Combine(featureConfigDirectory, "config.toml");
            File.WriteAllText(
                featureConfigPath,
                "project_doc_max_bytes = 2048\nproject_doc_fallback_filenames = [\"FEATURE_GUIDE.md\"]");
            string activeDocument = Path.Combine(featureDirectory, "Feature.cs");
            File.WriteAllText(activeDocument, "namespace Feature;");

            var hostContext = new CopilotAgentHostContextSnapshot(
                activeDocument,
                sourceDirectory,
                attachments: null,
                liveContext: null,
                conversationHistory: null,
                additionalReadRootPaths: null,
                globalInstructionRootPath: globalRoot);

            Assert.Equal(projectRoot, hostContext.PrimaryTrustedProjectRootPath, ignoreCase: true);
            Assert.Equal(sourceDirectory, hostContext.ProjectConfigWorkingDirectoryPath, ignoreCase: true);
            Assert.Equal(4096, hostContext.ProjectInstructionDiscoveryOptions.MaximumBytes);
            Assert.Equal(["SOURCE_GUIDE.md"], hostContext.ProjectInstructionDiscoveryOptions.FallbackFileNames);
            Assert.Equal(
                [rootConfigPath, sourceConfigPath],
                hostContext.ProjectInstructionDiscoveryOptions.AppliedProjectConfigFilePaths,
                StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain(
                hostContext.ProjectInstructionDiscoveryOptions.AppliedProjectConfigFilePaths,
                path => string.Equals(path, featureConfigPath, StringComparison.OrdinalIgnoreCase));
            var report = CopilotProjectInstructionDiagnostics.Format(
                new CopilotProjectInstructionSnapshot(
                    projectRoot,
                    activeDocument,
                    globalRoot,
                    hostContext.ProjectInstructionDiscoveryOptions,
                    Array.Empty<CopilotProjectInstructionDocument>()),
                hasActiveAgentRun: false);
            Assert.Contains("项目配置层：2 个（项目根 → 工作目录，后者优先）", report, StringComparison.Ordinal);
            Assert.Contains(Path.Combine("src", ".codex", "config.toml"), report, StringComparison.OrdinalIgnoreCase);
            var contextReport = CopilotContextDiagnostics.Format(new CopilotContextDiagnosticSnapshot
            {
                AgentContextEnabled = true,
                ProjectInstructionAppliedProjectConfigFilePaths =
                    hostContext.ProjectInstructionDiscoveryOptions.AppliedProjectConfigFilePaths,
            });
            Assert.Contains("项目配置层：2 个（项目根 → 工作目录，后者优先）", contextReport, StringComparison.Ordinal);
            Assert.Contains(sourceConfigPath, contextReport, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void DeveloperInstructionsUseTheNearestTrustedConfigLayer()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                $"""
                developer_instructions = "Global guidance."

                [projects.'{projectRoot}']
                trust_level = "trusted"
                """);
            string rootConfigDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(rootConfigDirectory);
            string rootConfigPath = Path.Combine(rootConfigDirectory, "config.toml");
            File.WriteAllText(rootConfigPath, "developer_instructions = \"Root guidance.\"");
            string sourceDirectory = Path.Combine(projectRoot, "src");
            string sourceConfigDirectory = Path.Combine(sourceDirectory, ".codex");
            Directory.CreateDirectory(sourceConfigDirectory);
            string sourceConfigPath = Path.Combine(sourceConfigDirectory, "config.toml");
            File.WriteAllText(
                sourceConfigPath,
                """"
                developer_instructions = """
                Use nested guidance.
                Keep evidence scoped.
                """
                """");
            string activeDocument = Path.Combine(sourceDirectory, "Feature.cs");
            File.WriteAllText(activeDocument, "namespace Feature;");

            var hostContext = new CopilotAgentHostContextSnapshot(
                activeDocument,
                sourceDirectory,
                attachments: null,
                liveContext: null,
                conversationHistory: null,
                additionalReadRootPaths: null,
                globalInstructionRootPath: globalRoot);
            var plan = CopilotAgentRequestFactory.Prepare(
                "Inspect the local implementation.",
                CopilotAgentMode.Auto,
                hostContext);
            var options = hostContext.ProjectInstructionDiscoveryOptions;

            Assert.Equal("Use nested guidance.\nKeep evidence scoped.", options.DeveloperInstructions);
            Assert.True(options.HasDeveloperInstructionsOverride);
            Assert.Equal(CopilotProjectInstructionConfigSources.TrustedProject, options.DeveloperInstructionsSource);
            Assert.Equal([rootConfigPath, sourceConfigPath], options.AppliedProjectConfigFilePaths, StringComparer.OrdinalIgnoreCase);
            Assert.Equal(options.DeveloperInstructions, plan.ConfiguredDeveloperInstructions);

            var request = new CopilotAgentRequest
            {
                Mode = CopilotAgentMode.Code,
                UserText = "Inspect the local implementation.",
                ConfiguredDeveloperInstructions = plan.ConfiguredDeveloperInstructions,
                ProjectInstructions =
                [
                    new CopilotProjectInstructionDocument
                    {
                        Path = Path.Combine(projectRoot, "AGENTS.md"),
                        Content = "# Workspace guidance",
                    },
                ],
            };
            var harness = CopilotMicrosoftAgentFrameworkRuntime.BuildHarnessInstructions(
                request,
                [],
                CopilotAgentEnvironmentContext.Capture(request),
                taskLedgerEnabled: false,
                agentModeEnabled: false);
            Assert.Contains("# Configured Codex developer instructions", harness, StringComparison.Ordinal);
            Assert.Contains("Use nested guidance.", harness, StringComparison.Ordinal);
            Assert.Contains("Keep evidence scoped.", harness, StringComparison.Ordinal);
            Assert.Contains("It never grants a tool, write, approval", harness, StringComparison.Ordinal);
            Assert.True(
                harness.IndexOf("# Configured Codex developer instructions", StringComparison.Ordinal)
                    < harness.IndexOf("Workspace AGENTS.override.md", StringComparison.Ordinal));

            var memoryReport = CopilotProjectInstructionDiagnostics.Format(
                new CopilotProjectInstructionSnapshot(
                    projectRoot,
                    activeDocument,
                    globalRoot,
                    options,
                    Array.Empty<CopilotProjectInstructionDocument>()),
                hasActiveAgentRun: false);
            Assert.Contains("Codex developer_instructions：", memoryReport, StringComparison.Ordinal);
            Assert.Contains("受信项目 .codex/config.toml 请求快照；独立开发者指令", memoryReport, StringComparison.Ordinal);
            Assert.DoesNotContain("Use nested guidance.", memoryReport, StringComparison.Ordinal);

            var contextReport = CopilotContextDiagnostics.Format(new CopilotContextDiagnosticSnapshot
            {
                AgentContextEnabled = true,
                ProjectInstructionDeveloperInstructionsCharacters = options.DeveloperInstructions.Length,
                ProjectInstructionDeveloperInstructionsSourceLabel = options.DeveloperInstructionsSourceLabel,
                ProjectInstructionHasDeveloperInstructionsOverride = options.HasDeveloperInstructionsOverride,
            });
            Assert.Contains("受信项目 .codex/config.toml 请求快照；独立开发者指令", contextReport, StringComparison.Ordinal);
            Assert.DoesNotContain("Use nested guidance.", contextReport, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void EmptyDeveloperInstructionsClearTheBroaderConfigLayer()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                $"""
                developer_instructions = "Global guidance."

                [projects.'{projectRoot}']
                trust_level = "trusted"
                """);
            string projectConfigDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(projectConfigDirectory);
            string projectConfigPath = Path.Combine(projectConfigDirectory, "config.toml");
            File.WriteAllText(projectConfigPath, "developer_instructions = \"\"");
            string activeDocument = Path.Combine(projectRoot, "Feature.cs");
            File.WriteAllText(activeDocument, "namespace Feature;");

            var hostContext = new CopilotAgentHostContextSnapshot(
                activeDocument,
                projectRoot,
                attachments: null,
                liveContext: null,
                conversationHistory: null,
                additionalReadRootPaths: null,
                globalInstructionRootPath: globalRoot);
            var plan = CopilotAgentRequestFactory.Prepare(
                "Inspect the local implementation.",
                CopilotAgentMode.Auto,
                hostContext);
            var options = hostContext.ProjectInstructionDiscoveryOptions;

            Assert.True(options.HasDeveloperInstructionsOverride);
            Assert.Empty(options.DeveloperInstructions);
            Assert.Equal(CopilotProjectInstructionConfigSources.TrustedProject, options.DeveloperInstructionsSource);
            Assert.Equal([projectConfigPath], options.AppliedProjectConfigFilePaths, StringComparer.OrdinalIgnoreCase);
            Assert.Empty(plan.ConfiguredDeveloperInstructions);

            var report = CopilotProjectInstructionDiagnostics.Format(
                new CopilotProjectInstructionSnapshot(
                    projectRoot,
                    activeDocument,
                    globalRoot,
                    options,
                    Array.Empty<CopilotProjectInstructionDocument>()),
                hasActiveAgentRun: false);
            Assert.Contains("Codex developer_instructions：0 字符", report, StringComparison.Ordinal);
            Assert.Contains("受信项目 .codex/config.toml 请求快照；显式清空", report, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void CompactPromptUsesTheNearestTrustedLayerAndRetainsHostIntegrity()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                $"""
                compact_prompt = "Global compact prompt."

                [projects.'{projectRoot}']
                trust_level = "trusted"
                """);
            string rootConfigDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(rootConfigDirectory);
            string rootConfigPath = Path.Combine(rootConfigDirectory, "config.toml");
            File.WriteAllText(rootConfigPath, "compact_prompt = \"Root compact prompt.\"");
            string sourceDirectory = Path.Combine(projectRoot, "src");
            string sourceConfigDirectory = Path.Combine(sourceDirectory, ".codex");
            Directory.CreateDirectory(sourceConfigDirectory);
            string sourceConfigPath = Path.Combine(sourceConfigDirectory, "config.toml");
            File.WriteAllText(
                sourceConfigPath,
                """"
                compact_prompt = """
                Preserve the nearest compact contract.
                Keep cited verification.
                """
                """");
            string activeDocument = Path.Combine(sourceDirectory, "Feature.cs");
            File.WriteAllText(activeDocument, "namespace Feature;");

            var hostContext = new CopilotAgentHostContextSnapshot(
                activeDocument,
                sourceDirectory,
                attachments: null,
                liveContext: null,
                conversationHistory: null,
                additionalReadRootPaths: null,
                globalInstructionRootPath: globalRoot);
            var options = hostContext.ProjectInstructionDiscoveryOptions;

            Assert.Equal(
                "Preserve the nearest compact contract.\nKeep cited verification.",
                options.CompactPrompt);
            Assert.True(options.HasCompactPromptOverride);
            Assert.Equal(CopilotProjectInstructionConfigSources.TrustedProject, options.CompactPromptSource);
            Assert.Empty(options.CompactPromptSourceFilePath);
            Assert.Equal([rootConfigPath, sourceConfigPath], options.AppliedProjectConfigFilePaths, StringComparer.OrdinalIgnoreCase);

            var request = CopilotConversationCompactionPrompt.BuildRequest(
                "Keep the current user focus.",
                options.CompactPrompt);
            Assert.StartsWith("Preserve the nearest compact contract.", request, StringComparison.Ordinal);
            Assert.DoesNotContain("Create a continuation summary", request, StringComparison.Ordinal);
            Assert.Contains("Additional focus from the user: Keep the current user focus.", request, StringComparison.Ordinal);
            Assert.Contains("ColorVision host integrity requirements", request, StringComparison.Ordinal);
            Assert.Contains("<assistant_response_interrupted>", request, StringComparison.Ordinal);
            Assert.Contains("<agent_turn_incomplete", request, StringComparison.Ordinal);

            var memoryReport = CopilotProjectInstructionDiagnostics.Format(
                new CopilotProjectInstructionSnapshot(
                    projectRoot,
                    activeDocument,
                    globalRoot,
                    options,
                    Array.Empty<CopilotProjectInstructionDocument>()),
                hasActiveAgentRun: false);
            Assert.Contains("受信项目 .codex/config.toml compact_prompt 请求快照", memoryReport, StringComparison.Ordinal);
            Assert.DoesNotContain("Preserve the nearest compact contract.", memoryReport, StringComparison.Ordinal);

            var contextReport = CopilotContextDiagnostics.Format(new CopilotContextDiagnosticSnapshot
            {
                HasConfiguredCompactPromptOverride = true,
                ConfiguredCompactPromptCharacters = options.CompactPrompt.Length,
                ConfiguredCompactPromptSourceLabel = options.CompactPromptSourceLabel,
            });
            Assert.Contains("Codex compact_prompt：", contextReport, StringComparison.Ordinal);
            Assert.Contains("终态完整性后缀仍由宿主强制保留", contextReport, StringComparison.Ordinal);
            Assert.DoesNotContain("Preserve the nearest compact contract.", contextReport, StringComparison.Ordinal);

            var debugReport = CopilotEffectiveConfigDiagnostics.Format(new CopilotEffectiveConfigDiagnosticContext
            {
                Config = new CopilotConfig(),
                State = new CopilotChatState(),
                CodexConfigOptions = options,
            });
            Assert.Contains("受信项目 .codex/config.toml compact_prompt", debugReport, StringComparison.Ordinal);
            Assert.DoesNotContain("Preserve the nearest compact contract.", debugReport, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void CompactPromptFileIsResolvedRelativeToItsOwningConfigLayer()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                $"""
                [projects.'{projectRoot}']
                trust_level = "trusted"
                """);
            string rootConfigDirectory = Path.Combine(projectRoot, ".codex");
            string rootPromptDirectory = Path.Combine(rootConfigDirectory, "prompts");
            Directory.CreateDirectory(rootPromptDirectory);
            string rootPromptPath = Path.Combine(rootPromptDirectory, "compact.md");
            File.WriteAllText(rootPromptPath, "Root file compact prompt.");
            string rootConfigPath = Path.Combine(rootConfigDirectory, "config.toml");
            File.WriteAllText(
                rootConfigPath,
                "experimental_compact_prompt_file = \"prompts/compact.md\"");

            string sourceDirectory = Path.Combine(projectRoot, "src");
            string sourceConfigDirectory = Path.Combine(sourceDirectory, ".codex");
            Directory.CreateDirectory(sourceConfigDirectory);
            string sourcePromptPath = Path.Combine(sourceConfigDirectory, "compact.md");
            File.WriteAllText(sourcePromptPath, "Nearest file compact prompt.");
            string sourceConfigPath = Path.Combine(sourceConfigDirectory, "config.toml");
            File.WriteAllText(
                sourceConfigPath,
                "compact_prompt = \"\"\nexperimental_compact_prompt_file = \"compact.md\"");
            string activeDocument = Path.Combine(sourceDirectory, "Feature.cs");
            File.WriteAllText(activeDocument, "namespace Feature;");

            var hostContext = new CopilotAgentHostContextSnapshot(
                activeDocument,
                sourceDirectory,
                attachments: null,
                liveContext: null,
                conversationHistory: null,
                additionalReadRootPaths: null,
                globalInstructionRootPath: globalRoot);
            var options = hostContext.ProjectInstructionDiscoveryOptions;

            Assert.Equal("Nearest file compact prompt.", options.CompactPrompt);
            Assert.Equal(sourcePromptPath, options.CompactPromptSourceFilePath, ignoreCase: true);
            Assert.Equal(CopilotProjectInstructionConfigSources.TrustedProject, options.CompactPromptSource);
            Assert.Contains("experimental_compact_prompt_file", options.CompactPromptSourceLabel, StringComparison.Ordinal);
            Assert.Equal([rootConfigPath, sourceConfigPath], options.AppliedProjectConfigFilePaths, StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void NonEmptyInlineCompactPromptWinsOverACloserPromptFile()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                $"""
                compact_prompt = "Global inline compact prompt."

                [projects.'{projectRoot}']
                trust_level = "trusted"
                """);
            string projectConfigDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(projectConfigDirectory);
            string promptPath = Path.Combine(projectConfigDirectory, "compact.md");
            File.WriteAllText(promptPath, "Project file compact prompt.");
            string projectConfigPath = Path.Combine(projectConfigDirectory, "config.toml");
            File.WriteAllText(
                projectConfigPath,
                "experimental_compact_prompt_file = \"compact.md\"");

            var options = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot, projectRoot);

            Assert.Equal("Global inline compact prompt.", options.CompactPrompt);
            Assert.Equal(CopilotProjectInstructionConfigSources.CodexHome, options.CompactPromptSource);
            Assert.False(options.CompactPromptUsesFile);
            Assert.Empty(options.CompactPromptSourceFilePath);
            Assert.Equal([projectConfigPath], options.AppliedProjectConfigFilePaths, StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void CodexHomeCompactPromptFileMayUseAnExplicitAbsoluteLocalPath()
    {
        string globalRoot = CreateTemporaryDirectory();
        string promptRoot = CreateTemporaryDirectory();
        try
        {
            string promptPath = Path.Combine(promptRoot, "compact.md");
            File.WriteAllText(promptPath, "Absolute Codex Home compact prompt.");
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                $"experimental_compact_prompt_file = '{promptPath}'");

            var options = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot);

            Assert.Equal("Absolute Codex Home compact prompt.", options.CompactPrompt);
            Assert.Equal(CopilotProjectInstructionConfigSources.CodexHome, options.CompactPromptSource);
            Assert.True(options.CompactPromptUsesFile);
            Assert.Equal(promptPath, options.CompactPromptSourceFilePath, ignoreCase: true);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(promptRoot, recursive: true);
        }
    }

    [Fact]
    public void EmptyCompactPromptClearsTheBroaderLayerAndUsesTheSafeDefaultBody()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                $"""
                compact_prompt = "Global compact prompt."

                [projects.'{projectRoot}']
                trust_level = "trusted"
                """);
            string projectConfigDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(projectConfigDirectory);
            string projectConfigPath = Path.Combine(projectConfigDirectory, "config.toml");
            File.WriteAllText(projectConfigPath, "compact_prompt = \"\"");

            var options = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot, projectRoot);

            Assert.True(options.HasCompactPromptOverride);
            Assert.Empty(options.CompactPrompt);
            Assert.Equal(CopilotProjectInstructionConfigSources.TrustedProject, options.CompactPromptSource);
            Assert.Equal([projectConfigPath], options.AppliedProjectConfigFilePaths, StringComparer.OrdinalIgnoreCase);
            var request = CopilotConversationCompactionPrompt.BuildRequest(null, options.CompactPrompt);
            Assert.StartsWith("Create a continuation summary", request, StringComparison.Ordinal);
            Assert.Contains("ColorVision host integrity requirements", request, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void TrustedProjectCompactPromptFileCannotEscapeTheProjectRoot()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        string outsideRoot = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            string outsidePromptPath = Path.Combine(outsideRoot, "compact.md");
            File.WriteAllText(outsidePromptPath, "Outside project prompt.");
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                $"""
                compact_prompt = "Global safe prompt."

                [projects.'{projectRoot}']
                trust_level = "trusted"
                """);
            string projectConfigDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(projectConfigDirectory);
            File.WriteAllText(
                Path.Combine(projectConfigDirectory, "config.toml"),
                $"experimental_compact_prompt_file = '{outsidePromptPath}'");

            var options = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot, projectRoot);

            Assert.Equal("Global safe prompt.", options.CompactPrompt);
            Assert.Equal(CopilotProjectInstructionConfigSources.CodexHome, options.CompactPromptSource);
            Assert.Empty(options.CompactPromptSourceFilePath);
            Assert.Single(options.AppliedProjectConfigFilePaths);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
            Directory.Delete(outsideRoot, recursive: true);
        }
    }

    [Fact]
    public void HostContextKeepsTheDeveloperInstructionsSnapshotTakenAtSubmission()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            string configDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(configDirectory);
            string configPath = Path.Combine(configDirectory, "config.toml");
            File.WriteAllText(
                configPath,
                "developer_instructions = \"First guidance.\"\ncompact_prompt = \"First compact prompt.\"");
            string activeDocument = Path.Combine(projectRoot, "Feature.cs");
            File.WriteAllText(activeDocument, "namespace Feature;");
            TrustProject(globalRoot, projectRoot);
            var submittedContext = new CopilotAgentHostContextSnapshot(
                activeDocument,
                projectRoot,
                attachments: null,
                liveContext: null,
                conversationHistory: null,
                additionalReadRootPaths: null,
                globalInstructionRootPath: globalRoot);

            File.WriteAllText(
                configPath,
                "developer_instructions = \"Second guidance.\"\ncompact_prompt = \"Second compact prompt.\"");

            var submittedPlan = CopilotAgentRequestFactory.Prepare(
                "Inspect the local implementation.",
                CopilotAgentMode.Auto,
                submittedContext);
            var refreshedContext = new CopilotAgentHostContextSnapshot(
                activeDocument,
                projectRoot,
                attachments: null,
                liveContext: null,
                conversationHistory: null,
                additionalReadRootPaths: null,
                globalInstructionRootPath: globalRoot);
            var refreshedPlan = CopilotAgentRequestFactory.Prepare(
                "Inspect the local implementation.",
                CopilotAgentMode.Auto,
                refreshedContext);

            Assert.Equal("First guidance.", submittedContext.ProjectInstructionDiscoveryOptions.DeveloperInstructions);
            Assert.Equal("First guidance.", submittedPlan.ConfiguredDeveloperInstructions);
            Assert.Equal("First compact prompt.", submittedContext.ProjectInstructionDiscoveryOptions.CompactPrompt);
            Assert.Equal("Second guidance.", refreshedContext.ProjectInstructionDiscoveryOptions.DeveloperInstructions);
            Assert.Equal("Second guidance.", refreshedPlan.ConfiguredDeveloperInstructions);
            Assert.Equal("Second compact prompt.", refreshedContext.ProjectInstructionDiscoveryOptions.CompactPrompt);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void StandaloneDocumentUsesItsDirectoryForNestedProjectConfigLayers()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            string sourceDirectory = Path.Combine(projectRoot, "src");
            string featureDirectory = Path.Combine(sourceDirectory, "feature");
            Directory.CreateDirectory(Path.Combine(projectRoot, ".codex"));
            Directory.CreateDirectory(Path.Combine(sourceDirectory, ".codex"));
            Directory.CreateDirectory(Path.Combine(featureDirectory, ".codex"));
            string rootConfigPath = Path.Combine(projectRoot, ".codex", "config.toml");
            string sourceConfigPath = Path.Combine(sourceDirectory, ".codex", "config.toml");
            string featureConfigPath = Path.Combine(featureDirectory, ".codex", "config.toml");
            File.WriteAllText(rootConfigPath, "project_doc_max_bytes = 8192");
            File.WriteAllText(sourceConfigPath, "project_doc_max_bytes = 4096");
            File.WriteAllText(featureConfigPath, "project_doc_max_bytes = 2048");
            string activeDocument = Path.Combine(featureDirectory, "Feature.cs");
            File.WriteAllText(activeDocument, "namespace Feature;");
            TrustProject(globalRoot, projectRoot);

            var hostContext = new CopilotAgentHostContextSnapshot(
                activeDocument,
                solutionDirectoryPath: null,
                attachments: null,
                liveContext: null,
                conversationHistory: null,
                additionalReadRootPaths: null,
                globalInstructionRootPath: globalRoot);

            Assert.Equal(featureDirectory, hostContext.ProjectConfigWorkingDirectoryPath, ignoreCase: true);
            Assert.Equal(2048, hostContext.ProjectInstructionDiscoveryOptions.MaximumBytes);
            Assert.Equal(
                [rootConfigPath, sourceConfigPath, featureConfigPath],
                hostContext.ProjectInstructionDiscoveryOptions.AppliedProjectConfigFilePaths,
                StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void ExplicitlyUntrustedProjectSkipsEveryNestedProjectConfigLayer()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                $"""
                project_doc_fallback_filenames = ["GLOBAL_GUIDE.md"]
                developer_instructions = "Global guidance."
                compact_prompt = "Global compact prompt."

                [projects.'{projectRoot}']
                trust_level = "untrusted"
                """);
            Directory.CreateDirectory(Path.Combine(projectRoot, ".codex"));
            File.WriteAllText(
                Path.Combine(projectRoot, ".codex", "config.toml"),
                "project_doc_fallback_filenames = [\"ROOT_GUIDE.md\"]\ndeveloper_instructions = \"Root guidance.\"\ncompact_prompt = \"Root compact prompt.\"");
            string sourceDirectory = Path.Combine(projectRoot, "src");
            Directory.CreateDirectory(Path.Combine(sourceDirectory, ".codex"));
            File.WriteAllText(
                Path.Combine(sourceDirectory, ".codex", "config.toml"),
                "project_doc_fallback_filenames = [\"SOURCE_GUIDE.md\"]\ndeveloper_instructions = \"Source guidance.\"\ncompact_prompt = \"Source compact prompt.\"");
            string activeDocument = Path.Combine(sourceDirectory, "Feature.cs");
            File.WriteAllText(activeDocument, "namespace Feature;");

            var hostContext = new CopilotAgentHostContextSnapshot(
                activeDocument,
                sourceDirectory,
                attachments: null,
                liveContext: null,
                conversationHistory: null,
                additionalReadRootPaths: null,
                globalInstructionRootPath: globalRoot);

            Assert.Equal(CopilotCodexProjectTrustLevel.Untrusted, hostContext.ProjectInstructionDiscoveryOptions.ProjectTrustLevel);
            Assert.Equal(["GLOBAL_GUIDE.md"], hostContext.ProjectInstructionDiscoveryOptions.FallbackFileNames);
            Assert.Equal("Global guidance.", hostContext.ProjectInstructionDiscoveryOptions.DeveloperInstructions);
            Assert.Equal(CopilotProjectInstructionConfigSources.CodexHome, hostContext.ProjectInstructionDiscoveryOptions.DeveloperInstructionsSource);
            Assert.Equal("Global compact prompt.", hostContext.ProjectInstructionDiscoveryOptions.CompactPrompt);
            Assert.Equal(CopilotProjectInstructionConfigSources.CodexHome, hostContext.ProjectInstructionDiscoveryOptions.CompactPromptSource);
            Assert.Empty(hostContext.ProjectInstructionDiscoveryOptions.AppliedProjectConfigFilePaths);
            Assert.Equal(CopilotProjectInstructionConfigSources.CodexHome, hostContext.ProjectInstructionDiscoveryOptions.ConfigSources);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void HostContextKeepsTheNestedProjectConfigStackTakenAtSubmission()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            string sourceDirectory = Path.Combine(projectRoot, "src");
            string configDirectory = Path.Combine(sourceDirectory, ".codex");
            Directory.CreateDirectory(configDirectory);
            string configPath = Path.Combine(configDirectory, "config.toml");
            File.WriteAllText(configPath, "project_doc_fallback_filenames = [\"FIRST.md\"]");
            string firstPath = Path.Combine(projectRoot, "FIRST.md");
            string secondPath = Path.Combine(projectRoot, "SECOND.md");
            File.WriteAllText(firstPath, "# First project instructions");
            File.WriteAllText(secondPath, "# Second project instructions");
            string activeDocument = Path.Combine(sourceDirectory, "Feature.cs");
            File.WriteAllText(activeDocument, "namespace Feature;");
            TrustProject(globalRoot, projectRoot);
            var submittedContext = new CopilotAgentHostContextSnapshot(
                activeDocument,
                sourceDirectory,
                attachments: null,
                liveContext: null,
                conversationHistory: null,
                additionalReadRootPaths: null,
                globalInstructionRootPath: globalRoot);

            File.WriteAllText(configPath, "project_doc_fallback_filenames = [\"SECOND.md\"]");

            var submittedPlan = CopilotAgentRequestFactory.Prepare(
                $"Inspect the local implementation in {activeDocument}",
                CopilotAgentMode.Auto,
                submittedContext);
            var refreshedContext = new CopilotAgentHostContextSnapshot(
                activeDocument,
                sourceDirectory,
                attachments: null,
                liveContext: null,
                conversationHistory: null,
                additionalReadRootPaths: null,
                globalInstructionRootPath: globalRoot);
            var refreshedPlan = CopilotAgentRequestFactory.Prepare(
                $"Inspect the local implementation in {activeDocument}",
                CopilotAgentMode.Auto,
                refreshedContext);

            Assert.Equal(["FIRST.md"], submittedContext.ProjectInstructionDiscoveryOptions.FallbackFileNames);
            Assert.Equal(["SECOND.md"], refreshedContext.ProjectInstructionDiscoveryOptions.FallbackFileNames);
            Assert.Contains(submittedPlan.ProjectInstructions, document =>
                string.Equals(document.Path, firstPath, StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(submittedPlan.ProjectInstructions, document =>
                string.Equals(document.Path, secondPath, StringComparison.OrdinalIgnoreCase));
            Assert.Contains(refreshedPlan.ProjectInstructions, document =>
                string.Equals(document.Path, secondPath, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void RequestFactoryKeepsTheInstructionConfigSnapshotTakenAtSubmission()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            string configPath = Path.Combine(globalRoot, "config.toml");
            string firstPath = Path.Combine(projectRoot, "FIRST.md");
            string secondPath = Path.Combine(projectRoot, "SECOND.md");
            string activeDocument = Path.Combine(projectRoot, "Feature.cs");
            File.WriteAllText(configPath, "project_doc_fallback_filenames = [\"FIRST.md\"]");
            File.WriteAllText(firstPath, "# First instructions");
            File.WriteAllText(secondPath, "# Second instructions");
            File.WriteAllText(activeDocument, "namespace Feature;");
            var submittedContext = new CopilotAgentHostContextSnapshot(
                activeDocument,
                projectRoot,
                attachments: null,
                liveContext: null,
                conversationHistory: null,
                additionalReadRootPaths: null,
                globalInstructionRootPath: globalRoot);

            File.WriteAllText(configPath, "project_doc_fallback_filenames = [\"SECOND.md\"]");

            var submittedPlan = CopilotAgentRequestFactory.Prepare(
                $"Inspect the local implementation in {activeDocument}",
                CopilotAgentMode.Auto,
                submittedContext);
            var refreshedContext = new CopilotAgentHostContextSnapshot(
                activeDocument,
                projectRoot,
                attachments: null,
                liveContext: null,
                conversationHistory: null,
                additionalReadRootPaths: null,
                globalInstructionRootPath: globalRoot);
            var refreshedPlan = CopilotAgentRequestFactory.Prepare(
                $"Inspect the local implementation in {activeDocument}",
                CopilotAgentMode.Auto,
                refreshedContext);

            Assert.Contains(submittedPlan.ProjectInstructions, document =>
                string.Equals(document.Path, firstPath, StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(submittedPlan.ProjectInstructions, document =>
                string.Equals(document.Path, secondPath, StringComparison.OrdinalIgnoreCase));
            Assert.Contains(refreshedPlan.ProjectInstructions, document =>
                string.Equals(document.Path, secondPath, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void Utf8InstructionBudgetDoesNotSplitUnicodeContent()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            File.WriteAllText(Path.Combine(globalRoot, "config.toml"), "project_doc_max_bytes = 4096");
            string instructionPath = Path.Combine(projectRoot, "AGENTS.md");
            File.WriteAllText(instructionPath, "# Unicode\n" + new string('界', 3_000));

            CopilotProjectInstructionDocument document = Assert.Single(
                CopilotAgentProjectInstructions.DiscoverWithGlobal(
                    [projectRoot],
                    activeDocumentPath: null,
                    additionalTargetFilePaths: null,
                    globalInstructionRootPath: globalRoot));

            Assert.Equal(instructionPath, document.Path, ignoreCase: true);
            Assert.True(document.IsTruncated);
            Assert.InRange(System.Text.Encoding.UTF8.GetByteCount(document.Content), 1, 4_096);
            Assert.False(char.IsHighSurrogate(document.Content[^1]));
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void UnsafeOrOutOfRangeCodexInstructionConfigFailsClosed()
    {
        string globalRoot = CreateTemporaryDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                """
                project_doc_max_bytes = 999999999
                project_doc_fallback_filenames = ["../secret.md", "nested/guide.md", "C:\\secret.md"]
                """);

            var options = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot);

            Assert.Equal(CopilotProjectInstructionDiscoveryConfig.DefaultMaximumBytes, options.MaximumBytes);
            Assert.Empty(options.FallbackFileNames);
            Assert.False(options.HasMaximumBytesOverride);
            Assert.True(options.HasFallbackFileNamesOverride);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
        }
    }

    [Fact]
    public void ConfiguredFallbackPreventsInitFromShadowingExistingInstructions()
    {
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            string configuredPath = Path.Combine(projectRoot, "TEAM_GUIDE.md");
            File.WriteAllText(configuredPath, "# Existing instructions");
            var options = new CopilotProjectInstructionDiscoveryOptions(
                CopilotProjectInstructionDiscoveryConfig.DefaultMaximumBytes,
                ["TEAM_GUIDE.md"],
                HasMaximumBytesOverride: false,
                HasFallbackFileNamesOverride: true);

            var plan = CopilotProjectInitialization.Create(projectRoot, options);

            Assert.False(plan.CanStart);
            Assert.Equal(configuredPath, plan.TargetPath, ignoreCase: true);
            Assert.Contains("TEAM_GUIDE.md", plan.Message, StringComparison.Ordinal);

            File.Delete(configuredPath);
            var readyPlan = CopilotProjectInitialization.Create(projectRoot, options);
            Assert.True(readyPlan.CanStart);
            Assert.Contains("TEAM_GUIDE.md", readyPlan.ModelPrompt, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void RequestFactorySnapshotsGlobalInstructionsWithoutGrantingFileAccess()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            string globalPath = Path.Combine(globalRoot, "AGENTS.md");
            string activeDocument = Path.Combine(projectRoot, "Feature.cs");
            File.WriteAllText(globalPath, "# Personal instructions");
            File.WriteAllText(activeDocument, "namespace Feature;");
            var hostContext = new CopilotAgentHostContextSnapshot(
                activeDocument,
                projectRoot,
                attachments: null,
                liveContext: null,
                conversationHistory: null,
                additionalReadRootPaths: null,
                globalInstructionRootPath: globalRoot);

            var plan = CopilotAgentRequestFactory.Prepare(
                $"Inspect the local implementation in {activeDocument}",
                CopilotAgentMode.Auto,
                hostContext);

            Assert.Contains(plan.ProjectInstructions, document =>
                string.Equals(document.Path, globalPath, StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(plan.SearchRootPaths, path =>
                string.Equals(path, globalRoot, StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(plan.ReadableLocalDirectoryPaths, path =>
                string.Equals(path, globalRoot, StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(plan.WritableLocalRootPaths, path =>
                string.Equals(path, globalRoot, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void GlobalInstructionDiagnosticsIdentifyAndBoundThePersonalSource()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        string outsideRoot = CreateTemporaryDirectory();
        try
        {
            string globalPath = Path.Combine(globalRoot, "AGENTS.md");
            string outsidePath = Path.Combine(outsideRoot, "AGENTS.md");
            File.WriteAllText(globalPath, "# Personal instructions");
            File.WriteAllText(outsidePath, "# Outside instructions");
            var document = Assert.Single(CopilotAgentProjectInstructions.DiscoverWithGlobal(
                [projectRoot],
                activeDocumentPath: null,
                additionalTargetFilePaths: null,
                globalInstructionRootPath: globalRoot));

            var report = CopilotProjectInstructionDiagnostics.Format(
                new CopilotProjectInstructionSnapshot(
                    projectRoot,
                    string.Empty,
                    globalRoot,
                    CopilotProjectInstructionDiscoveryConfig.CreateDefault(),
                    [document]),
                hasActiveAgentRun: false);

            Assert.Contains("Codex 全局指令", report, StringComparison.Ordinal);
            Assert.True(CopilotLocalFileLinkNavigator.IsAllowedFile(globalPath, projectRoot, [globalRoot]));
            Assert.False(CopilotLocalFileLinkNavigator.IsAllowedFile(outsidePath, projectRoot, [globalRoot]));
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
            Directory.Delete(outsideRoot, recursive: true);
        }
    }

    [Fact]
    public void EmptyAgentsInstructionsFallBackToClaudeInstructions()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            File.WriteAllText(Path.Combine(root, "AGENTS.override.md"), "<!-- empty after filtering -->");
            File.WriteAllText(Path.Combine(root, "AGENTS.md"), string.Empty);
            string claudePath = Path.Combine(root, "CLAUDE.md");
            File.WriteAllText(claudePath, "# Claude instructions");

            CopilotProjectInstructionDocument document = Assert.Single(
                CopilotAgentProjectInstructions.Discover([root], activeDocumentPath: null));

            Assert.Equal(claudePath, document.Path, ignoreCase: true);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ClaudeLocalInstructionsFollowTheSelectedSharedDocument()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            string agentsPath = Path.Combine(root, "AGENTS.md");
            string localPath = Path.Combine(root, "CLAUDE.local.md");
            File.WriteAllText(agentsPath, "# Shared agents instructions");
            File.WriteAllText(localPath, "# Private local instructions");

            var documents = CopilotAgentProjectInstructions.Discover([root], activeDocumentPath: null);

            Assert.Equal(2, documents.Count);
            Assert.Equal(agentsPath, documents[0].Path, ignoreCase: true);
            Assert.Equal(localPath, documents[1].Path, ignoreCase: true);
            Assert.Contains("Private local instructions", documents[1].Content, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ClaudeLocalInstructionsLoadWithoutASharedDocument()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            string localPath = Path.Combine(root, "CLAUDE.local.md");
            File.WriteAllText(localPath, "# Private local instructions");

            CopilotProjectInstructionDocument document = Assert.Single(
                CopilotAgentProjectInstructions.Discover([root], activeDocumentPath: null));

            Assert.Equal(localPath, document.Path, ignoreCase: true);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void EmptyClaudeLocalInstructionsDoNotReplaceOrConsumeTheSharedDocument()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            string agentsPath = Path.Combine(root, "AGENTS.md");
            File.WriteAllText(agentsPath, "# Shared agents instructions");
            File.WriteAllText(Path.Combine(root, "CLAUDE.local.md"), "<!-- private note only -->");

            CopilotProjectInstructionDocument document = Assert.Single(
                CopilotAgentProjectInstructions.Discover([root], activeDocumentPath: null));

            Assert.Equal(agentsPath, document.Path, ignoreCase: true);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void UnconditionalClaudeRulesLoadBetweenSharedAndLocalInstructions()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            string agentsPath = Path.Combine(root, "AGENTS.md");
            string rulesDirectory = Path.Combine(root, ".claude", "rules");
            string rulePath = Path.Combine(rulesDirectory, "testing.md");
            string localPath = Path.Combine(root, "CLAUDE.local.md");
            Directory.CreateDirectory(rulesDirectory);
            File.WriteAllText(agentsPath, "# Shared instructions");
            File.WriteAllText(rulePath, "# Testing rule");
            File.WriteAllText(localPath, "# Local instructions");

            var documents = CopilotAgentProjectInstructions.Discover([root], activeDocumentPath: null);

            Assert.Equal(
                new[] { agentsPath, rulePath, localPath },
                documents.Select(document => document.Path).ToArray(),
                StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void MatchingPathScopedClaudeRuleLoadsAndRemovesFrontmatter()
    {
        string root = CreateTemporaryDirectory();
        string activeDirectory = Path.Combine(root, "src", "api");
        Directory.CreateDirectory(activeDirectory);
        string activeDocument = Path.Combine(activeDirectory, "Handler.ts");
        File.WriteAllText(activeDocument, "export const handler = true;");
        try
        {
            string rulesDirectory = Path.Combine(root, ".claude", "rules");
            Directory.CreateDirectory(rulesDirectory);
            string rulePath = Path.Combine(rulesDirectory, "api.md");
            File.WriteAllText(
                rulePath,
                """
                ---
                paths:
                  - "src/api/**/*.ts"
                ---

                # API rule
                - Validate every request.
                """);

            CopilotProjectInstructionDocument document = Assert.Single(
                CopilotAgentProjectInstructions.Discover([root], activeDocument));

            Assert.Equal(rulePath, document.Path, ignoreCase: true);
            Assert.Contains("# API rule", document.Content, StringComparison.Ordinal);
            Assert.DoesNotContain("paths:", document.Content, StringComparison.Ordinal);
            Assert.DoesNotContain("---", document.Content, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void NonMatchingPathScopedClaudeRuleIsNotLoaded()
    {
        string root = CreateTemporaryDirectory();
        string activeDirectory = Path.Combine(root, "src", "ui");
        Directory.CreateDirectory(activeDirectory);
        string activeDocument = Path.Combine(activeDirectory, "View.tsx");
        File.WriteAllText(activeDocument, "export const view = true;");
        try
        {
            string rulesDirectory = Path.Combine(root, ".claude", "rules");
            Directory.CreateDirectory(rulesDirectory);
            File.WriteAllText(
                Path.Combine(rulesDirectory, "api.md"),
                """
                ---
                paths:
                  - "src/api/**/*.ts"
                ---
                # API only
                """);

            var documents = CopilotAgentProjectInstructions.Discover([root], activeDocument);

            Assert.Empty(documents);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ClaudeRulePathsSupportInlineListsAndBraceExpansion()
    {
        string root = CreateTemporaryDirectory();
        string activeDirectory = Path.Combine(root, "src", "components");
        Directory.CreateDirectory(activeDirectory);
        string activeDocument = Path.Combine(activeDirectory, "Button.tsx");
        File.WriteAllText(activeDocument, "export const button = true;");
        try
        {
            string rulesDirectory = Path.Combine(root, ".claude", "rules");
            Directory.CreateDirectory(rulesDirectory);
            string rulePath = Path.Combine(rulesDirectory, "frontend.md");
            File.WriteAllText(
                rulePath,
                """
                ---
                paths: ["src/**/*.{ts,tsx}", "tests/**/*.test.ts"]
                ---
                # Frontend rule
                """);

            CopilotProjectInstructionDocument document = Assert.Single(
                CopilotAgentProjectInstructions.Discover([root], activeDocument));

            Assert.Equal(rulePath, document.Path, ignoreCase: true);
            Assert.Equal("# Frontend rule", document.Content);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void PathScopedClaudeRuleDoesNotLoadWithoutAnActiveDocument()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            string rulesDirectory = Path.Combine(root, ".claude", "rules");
            Directory.CreateDirectory(rulesDirectory);
            File.WriteAllText(
                Path.Combine(rulesDirectory, "source.md"),
                """
                ---
                paths:
                  - "src/**/*.cs"
                ---
                # Source rule
                """);

            var documents = CopilotAgentProjectInstructions.Discover([root], activeDocumentPath: null);

            Assert.Empty(documents);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void PathScopedClaudeRuleLoadsForAnExplicitRequestTarget()
    {
        string root = CreateTemporaryDirectory();
        string sourceDirectory = Path.Combine(root, "src");
        Directory.CreateDirectory(sourceDirectory);
        string targetFile = Path.Combine(sourceDirectory, "Target.cs");
        try
        {
            string rulesDirectory = Path.Combine(root, ".claude", "rules");
            Directory.CreateDirectory(rulesDirectory);
            string rulePath = Path.Combine(rulesDirectory, "source.md");
            File.WriteAllText(
                rulePath,
                """
                ---
                paths:
                  - "src/**/*.cs"
                ---
                # Source rule
                """);

            CopilotProjectInstructionDocument document = Assert.Single(
                CopilotAgentProjectInstructions.Discover(
                    [root],
                    activeDocumentPath: null,
                    additionalTargetFilePaths: [targetFile]));

            Assert.Equal(rulePath, document.Path, ignoreCase: true);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ClaudeRulesAreDiscoveredRecursivelyInStableRelativePathOrder()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            string rulesDirectory = Path.Combine(root, ".claude", "rules");
            string nestedRulesDirectory = Path.Combine(rulesDirectory, "frontend");
            Directory.CreateDirectory(nestedRulesDirectory);
            string nestedRulePath = Path.Combine(nestedRulesDirectory, "a-style.md");
            string rootRulePath = Path.Combine(rulesDirectory, "z-testing.md");
            File.WriteAllText(rootRulePath, "# Root rule");
            File.WriteAllText(nestedRulePath, "# Nested rule");

            var documents = CopilotAgentProjectInstructions.Discover([root], activeDocumentPath: null);

            Assert.Equal(
                new[] { nestedRulePath, rootRulePath },
                documents.Select(document => document.Path).ToArray(),
                StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("../secrets/**")]
    [InlineData("/absolute/**")]
    [InlineData("C:/outside/**")]
    [InlineData("!src/generated/**")]
    public void UnsafeOrUnsupportedClaudeRulePathPatternsAreRejected(string pattern)
    {
        string root = CreateTemporaryDirectory();
        string sourceDirectory = Path.Combine(root, "src");
        Directory.CreateDirectory(sourceDirectory);
        string activeDocument = Path.Combine(sourceDirectory, "Target.cs");
        File.WriteAllText(activeDocument, "namespace Target;");
        try
        {
            string rulesDirectory = Path.Combine(root, ".claude", "rules");
            Directory.CreateDirectory(rulesDirectory);
            File.WriteAllText(
                Path.Combine(rulesDirectory, "unsafe.md"),
                $"---{Environment.NewLine}paths:{Environment.NewLine}  - \"{pattern}\"{Environment.NewLine}---{Environment.NewLine}# Unsafe rule");

            var documents = CopilotAgentProjectInstructions.Discover([root], activeDocument);

            Assert.Empty(documents);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void MalformedClaudeRuleFrontmatterFailsClosed()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            string rulesDirectory = Path.Combine(root, ".claude", "rules");
            Directory.CreateDirectory(rulesDirectory);
            File.WriteAllText(
                Path.Combine(rulesDirectory, "malformed.md"),
                """
                ---
                paths:
                  - "**/*.cs"
                # Missing closing delimiter
                """);

            var documents = CopilotAgentProjectInstructions.Discover([root], activeDocumentPath: null);

            Assert.Empty(documents);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void IndentedClaudeRulePathsFieldFailsClosedInsteadOfBecomingGlobal()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            string rulesDirectory = Path.Combine(root, ".claude", "rules");
            Directory.CreateDirectory(rulesDirectory);
            File.WriteAllText(
                Path.Combine(rulesDirectory, "indented.md"),
                """
                ---
                  paths:
                    - "**/*.cs"
                ---
                # Invalid scoped rule
                """);

            var documents = CopilotAgentProjectInstructions.Discover([root], activeDocumentPath: null);

            Assert.Empty(documents);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void NestedClaudeInstructionsFollowBroaderAgentsInstructions()
    {
        string root = CreateTemporaryDirectory();
        string nested = Path.Combine(root, "src", "feature");
        Directory.CreateDirectory(nested);
        string activeDocument = Path.Combine(nested, "Feature.cs");
        File.WriteAllText(activeDocument, "namespace Feature;");
        try
        {
            string rootInstructions = Path.Combine(root, "AGENTS.md");
            string nestedInstructions = Path.Combine(nested, "CLAUDE.md");
            File.WriteAllText(rootInstructions, "# Root instructions");
            File.WriteAllText(nestedInstructions, "# Feature instructions");

            var documents = CopilotAgentProjectInstructions.Discover([root], activeDocument);

            Assert.Equal(2, documents.Count);
            Assert.Equal(rootInstructions, documents[0].Path, ignoreCase: true);
            Assert.Equal(nestedInstructions, documents[1].Path, ignoreCase: true);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ExplicitTargetLoadsItsNestedInstructionsWithoutAnActiveDocument()
    {
        string root = CreateTemporaryDirectory();
        string nested = Path.Combine(root, "src", "feature");
        Directory.CreateDirectory(nested);
        string targetDocument = Path.Combine(nested, "Feature.cs");
        File.WriteAllText(targetDocument, "namespace Feature;");
        try
        {
            string rootInstructions = Path.Combine(root, "AGENTS.md");
            string nestedInstructions = Path.Combine(nested, "AGENTS.md");
            File.WriteAllText(rootInstructions, "# Root instructions");
            File.WriteAllText(nestedInstructions, "# Feature instructions");

            var documents = CopilotAgentProjectInstructions.Discover(
                [root],
                activeDocumentPath: null,
                [targetDocument]);

            Assert.Equal(2, documents.Count);
            Assert.Equal(rootInstructions, documents[0].Path, ignoreCase: true);
            Assert.Equal(nestedInstructions, documents[1].Path, ignoreCase: true);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void DocumentLimitRetainsTheMostSpecificInstructions()
    {
        string root = CreateTemporaryDirectory();
        string current = root;
        var instructionPaths = new string[CopilotAgentProjectInstructions.MaxDocuments + 2];
        try
        {
            for (int index = 0; index < instructionPaths.Length; index++)
            {
                if (index > 0)
                {
                    current = Path.Combine(current, $"level-{index}");
                    Directory.CreateDirectory(current);
                }

                instructionPaths[index] = Path.Combine(current, "AGENTS.md");
                File.WriteAllText(instructionPaths[index], $"# Scope {index}");
            }
            string activeDocument = Path.Combine(current, "Target.cs");
            File.WriteAllText(activeDocument, "namespace Target;");

            var documents = CopilotAgentProjectInstructions.Discover([root], activeDocument);

            Assert.Equal(CopilotAgentProjectInstructions.MaxDocuments, documents.Count);
            Assert.Equal(
                instructionPaths[^CopilotAgentProjectInstructions.MaxDocuments..],
                documents.Select(document => document.Path).ToArray(),
                StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain(documents, document => string.Equals(document.Path, instructionPaths[0], StringComparison.OrdinalIgnoreCase));
            Assert.Equal(instructionPaths[^1], documents[^1].Path, ignoreCase: true);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void DocumentChainKeepsLocalOverlaysWithTheirSharedDocuments()
    {
        string root = CreateTemporaryDirectory();
        string nested = Path.Combine(root, "src");
        string deepest = Path.Combine(nested, "feature");
        Directory.CreateDirectory(deepest);
        string activeDocument = Path.Combine(deepest, "Target.cs");
        File.WriteAllText(activeDocument, "namespace Target;");
        try
        {
            foreach (var directory in new[] { root, nested, deepest })
            {
                File.WriteAllText(Path.Combine(directory, "AGENTS.md"), "# Shared " + directory);
                File.WriteAllText(Path.Combine(directory, "CLAUDE.local.md"), "# Local " + directory);
            }

            var documents = CopilotAgentProjectInstructions.Discover([root], activeDocument);

            Assert.Equal(6, documents.Count);
            Assert.Equal(
                new[]
                {
                    Path.Combine(root, "AGENTS.md"),
                    Path.Combine(root, "CLAUDE.local.md"),
                    Path.Combine(nested, "AGENTS.md"),
                    Path.Combine(nested, "CLAUDE.local.md"),
                    Path.Combine(deepest, "AGENTS.md"),
                    Path.Combine(deepest, "CLAUDE.local.md"),
                },
                documents.Select(document => document.Path).ToArray(),
                StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Utf8ByteBudgetRetainsTheMostSpecificInstructions()
    {
        string root = CreateTemporaryDirectory();
        string nested = Path.Combine(root, "src");
        string deepest = Path.Combine(nested, "feature");
        Directory.CreateDirectory(deepest);
        string activeDocument = Path.Combine(deepest, "Target.cs");
        File.WriteAllText(activeDocument, "namespace Target;");
        try
        {
            string rootInstructions = Path.Combine(root, "AGENTS.md");
            string nestedInstructions = Path.Combine(nested, "AGENTS.md");
            string deepestInstructions = Path.Combine(deepest, "AGENTS.md");
            File.WriteAllText(rootInstructions, "# Root\n" + new string('R', CopilotAgentProjectInstructions.MaxDocumentCharacters));
            File.WriteAllText(nestedInstructions, "# Nested\n" + new string('N', CopilotAgentProjectInstructions.MaxDocumentCharacters));
            File.WriteAllText(deepestInstructions, "# Deepest\n" + new string('D', CopilotAgentProjectInstructions.MaxDocumentCharacters));

            var documents = CopilotAgentProjectInstructions.Discover([root], activeDocument);

            Assert.Equal(3, documents.Count);
            Assert.Equal(rootInstructions, documents[0].Path, ignoreCase: true);
            Assert.Equal(nestedInstructions, documents[1].Path, ignoreCase: true);
            Assert.Equal(deepestInstructions, documents[2].Path, ignoreCase: true);
            Assert.True(documents[0].IsTruncated);
            Assert.InRange(
                documents.Sum(document => System.Text.Encoding.UTF8.GetByteCount(document.Content)),
                1,
                CopilotProjectInstructionDiscoveryConfig.DefaultMaximumBytes);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ClaudeImportsAreNotExpandedDuringDiscovery()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            File.WriteAllText(Path.Combine(root, "private.md"), "PRIVATE_IMPORT_CONTENT");
            File.WriteAllText(Path.Combine(root, "CLAUDE.md"), "@private.md");

            CopilotProjectInstructionDocument document = Assert.Single(
                CopilotAgentProjectInstructions.Discover([root], activeDocumentPath: null));

            Assert.Equal("@private.md", document.Content);
            Assert.DoesNotContain("PRIVATE_IMPORT_CONTENT", document.Content, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void PromptBlockExplainsCompatibilityFallbackLocalOverlayAndSafetyBoundary()
    {
        string prompt = CopilotAgentProjectInstructions.BuildPromptBlock(
        [
            new CopilotProjectInstructionDocument
            {
                Path = @"C:\workspace\CLAUDE.md",
                Content = "# Build instructions",
            },
        ]);

        Assert.Contains("CLAUDE.md", prompt, StringComparison.Ordinal);
        Assert.Contains("compatibility fallback", prompt, StringComparison.Ordinal);
        Assert.Contains("CLAUDE.local.md", prompt, StringComparison.Ordinal);
        Assert.Contains("private project overlay", prompt, StringComparison.Ordinal);
        Assert.Contains(".claude/rules/**/*.md", prompt, StringComparison.Ordinal);
        Assert.Contains("paths frontmatter", prompt, StringComparison.Ordinal);
        Assert.Contains("never authorize a write", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void InlineModelInstructionsUseTheClosestTrustedConfigLayerAndKeepTheirSubmissionSnapshot()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                $"""
                instructions = "Global inline instructions."

                [projects.'{projectRoot}']
                trust_level = "trusted"
                """);
            string sourceDirectory = Path.Combine(projectRoot, "src");
            string projectConfigDirectory = Path.Combine(sourceDirectory, ".codex");
            Directory.CreateDirectory(projectConfigDirectory);
            string projectConfigPath = Path.Combine(projectConfigDirectory, "config.toml");
            File.WriteAllText(
                projectConfigPath,
                """"
                instructions = """
                Closest project inline instructions.
                Keep the host boundary.
                """
                """");
            string activeDocument = Path.Combine(sourceDirectory, "Feature.cs");
            File.WriteAllText(activeDocument, "namespace Feature;");

            var submittedContext = new CopilotAgentHostContextSnapshot(
                activeDocument,
                sourceDirectory,
                attachments: null,
                liveContext: null,
                conversationHistory: null,
                additionalReadRootPaths: null,
                globalInstructionRootPath: globalRoot);
            File.WriteAllText(projectConfigPath, "instructions = \"Changed after submission.\"");
            var refreshedContext = new CopilotAgentHostContextSnapshot(
                activeDocument,
                sourceDirectory,
                attachments: null,
                liveContext: null,
                conversationHistory: null,
                additionalReadRootPaths: null,
                globalInstructionRootPath: globalRoot);

            var submittedOptions = submittedContext.ProjectInstructionDiscoveryOptions;
            Assert.Equal(
                "Closest project inline instructions.\nKeep the host boundary.",
                submittedOptions.ModelInstructions);
            Assert.True(submittedOptions.HasModelInstructionsInlineOverride);
            Assert.True(submittedOptions.HasModelInstructionsOverride);
            Assert.True(submittedOptions.HasEffectiveModelInstructions);
            Assert.False(submittedOptions.ModelInstructionsUsesFile);
            Assert.Equal(CopilotProjectInstructionConfigSources.TrustedProject, submittedOptions.ModelInstructionsSource);
            Assert.Equal("受信项目 .codex/config.toml instructions", submittedOptions.ModelInstructionsSourceLabel);
            Assert.Empty(submittedOptions.ModelInstructionsSourceFilePath);
            Assert.Equal("Changed after submission.", refreshedContext.ProjectInstructionDiscoveryOptions.ModelInstructions);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void ModelInstructionsFileWinsOverInlineInstructionsAfterLayerMerge()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            string globalInstructionsPath = Path.Combine(globalRoot, "global-model.md");
            File.WriteAllText(globalInstructionsPath, "Global file instructions.");
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                $"""
                instructions = "Global inline instructions."
                model_instructions_file = "global-model.md"

                [projects.'{projectRoot}']
                trust_level = "trusted"
                """);
            string projectConfigDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(projectConfigDirectory);
            File.WriteAllText(
                Path.Combine(projectConfigDirectory, "config.toml"),
                "instructions = \"Project inline instructions.\"");

            var options = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot, projectRoot);

            Assert.Equal("Global file instructions.", options.ModelInstructions);
            Assert.True(options.HasModelInstructionsInlineOverride);
            Assert.True(options.HasModelInstructionsFileOverride);
            Assert.True(options.ModelInstructionsUsesFile);
            Assert.Equal(CopilotProjectInstructionConfigSources.CodexHome, options.ModelInstructionsSource);
            Assert.Equal(globalInstructionsPath, options.ModelInstructionsSourceFilePath, ignoreCase: true);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void UntrustedProjectCannotOverrideGlobalInlineModelInstructions()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                $"""
                instructions = "Global inline instructions."

                [projects.'{projectRoot}']
                trust_level = "untrusted"
                """);
            string projectConfigDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(projectConfigDirectory);
            File.WriteAllText(
                Path.Combine(projectConfigDirectory, "config.toml"),
                "instructions = \"Untrusted project instructions.\"");

            var options = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot, projectRoot);

            Assert.Equal("Global inline instructions.", options.ModelInstructions);
            Assert.Equal(CopilotProjectInstructionConfigSources.CodexHome, options.ModelInstructionsSource);
            Assert.Equal(CopilotCodexProjectTrustLevel.Untrusted, options.ProjectTrustLevel);
            Assert.Empty(options.AppliedProjectConfigFilePaths);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void ModelInstructionsFileUsesTheClosestTrustedConfigLayerAndKeepsItsSubmissionSnapshot()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                $"""
                model_instructions_file = "global-model.md"

                [projects.'{projectRoot}']
                trust_level = "trusted"
                """);
            File.WriteAllText(Path.Combine(globalRoot, "global-model.md"), "Global model instructions.");
            string sourceDirectory = Path.Combine(projectRoot, "src");
            string projectConfigDirectory = Path.Combine(sourceDirectory, ".codex");
            Directory.CreateDirectory(projectConfigDirectory);
            string modelInstructionsPath = Path.Combine(projectConfigDirectory, "model.md");
            File.WriteAllText(modelInstructionsPath, "Closest project model instructions.");
            File.WriteAllText(
                Path.Combine(projectConfigDirectory, "config.toml"),
                "model_instructions_file = \"model.md\"");
            string activeDocument = Path.Combine(sourceDirectory, "Feature.cs");
            File.WriteAllText(activeDocument, "namespace Feature;");

            var submittedContext = new CopilotAgentHostContextSnapshot(
                activeDocument,
                sourceDirectory,
                attachments: null,
                liveContext: null,
                conversationHistory: null,
                additionalReadRootPaths: null,
                globalInstructionRootPath: globalRoot);
            File.WriteAllText(modelInstructionsPath, "Changed after submission.");
            var refreshedContext = new CopilotAgentHostContextSnapshot(
                activeDocument,
                sourceDirectory,
                attachments: null,
                liveContext: null,
                conversationHistory: null,
                additionalReadRootPaths: null,
                globalInstructionRootPath: globalRoot);

            var submittedOptions = submittedContext.ProjectInstructionDiscoveryOptions;
            Assert.Equal("Closest project model instructions.", submittedOptions.ModelInstructions);
            Assert.True(submittedOptions.HasEffectiveModelInstructions);
            Assert.Equal(CopilotProjectInstructionConfigSources.TrustedProject, submittedOptions.ModelInstructionsFileSource);
            Assert.Equal(modelInstructionsPath, submittedOptions.ModelInstructionsSourceFilePath, ignoreCase: true);
            Assert.Equal("Changed after submission.", refreshedContext.ProjectInstructionDiscoveryOptions.ModelInstructions);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void CodexHomeModelInstructionsFileMayUseAnExplicitAbsoluteLocalPath()
    {
        string globalRoot = CreateTemporaryDirectory();
        string instructionsRoot = CreateTemporaryDirectory();
        try
        {
            string instructionsPath = Path.Combine(instructionsRoot, "model.md");
            File.WriteAllText(instructionsPath, "Absolute model instructions.");
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                $"model_instructions_file = '{instructionsPath}'");

            var options = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot);

            Assert.Equal("Absolute model instructions.", options.ModelInstructions);
            Assert.Equal(instructionsPath, options.ModelInstructionsSourceFilePath, ignoreCase: true);
            Assert.Equal(CopilotProjectInstructionConfigSources.CodexHome, options.ModelInstructionsFileSource);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(instructionsRoot, recursive: true);
        }
    }

    [Fact]
    public void TrustedProjectModelInstructionsFileCannotEscapeTheProjectRoot()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        string outsideRoot = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            File.WriteAllText(Path.Combine(globalRoot, "global-model.md"), "Global safe model instructions.");
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                $"""
                instructions = "Global inline model instructions."
                model_instructions_file = "global-model.md"

                [projects.'{projectRoot}']
                trust_level = "trusted"
                """);
            string outsideInstructionsPath = Path.Combine(outsideRoot, "model.md");
            File.WriteAllText(outsideInstructionsPath, "Outside project instructions.");
            string projectConfigDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(projectConfigDirectory);
            File.WriteAllText(
                Path.Combine(projectConfigDirectory, "config.toml"),
                $"model_instructions_file = '{outsideInstructionsPath}'");

            var options = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot, projectRoot);

            Assert.True(options.HasModelInstructionsInlineOverride);
            Assert.True(options.HasModelInstructionsFileOverride);
            Assert.True(options.ModelInstructionsUsesFile);
            Assert.False(options.HasEffectiveModelInstructions);
            Assert.Empty(options.ModelInstructions);
            Assert.Empty(options.ModelInstructionsSourceFilePath);
            Assert.Equal(CopilotProjectInstructionConfigSources.TrustedProject, options.ModelInstructionsFileSource);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
            Directory.Delete(outsideRoot, recursive: true);
        }
    }

    [Fact]
    public void UntrustedProjectCannotOverrideGlobalModelInstructionsFile()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            File.WriteAllText(Path.Combine(globalRoot, "global-model.md"), "Global model instructions.");
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                $"""
                model_instructions_file = "global-model.md"

                [projects.'{projectRoot}']
                trust_level = "untrusted"
                """);
            string projectConfigDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(projectConfigDirectory);
            File.WriteAllText(Path.Combine(projectConfigDirectory, "model.md"), "Untrusted project model instructions.");
            File.WriteAllText(
                Path.Combine(projectConfigDirectory, "config.toml"),
                "model_instructions_file = \"model.md\"");

            var options = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot, projectRoot);

            Assert.Equal("Global model instructions.", options.ModelInstructions);
            Assert.Equal(CopilotProjectInstructionConfigSources.CodexHome, options.ModelInstructionsFileSource);
            Assert.Equal(CopilotCodexProjectTrustLevel.Untrusted, options.ProjectTrustLevel);
            Assert.Empty(options.AppliedProjectConfigFilePaths);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void ConfiguredModelInstructionsReplaceTheSharedChatAndAgentProfileBodyButKeepHostAndPresentationRules()
    {
        var source = CopilotProfileConfig.CreateDefault();

        var requestProfile = CopilotResponsePresentationGuidance.CreateRequestProfile(
            source,
            CopilotResponsePersonality.Pragmatic,
            "Use the configured project persona.");

        Assert.StartsWith("Use the configured project persona.", requestProfile.EffectiveSystemPrompt, StringComparison.Ordinal);
        Assert.DoesNotContain(CopilotProfileConfig.DefaultSystemPrompt, requestProfile.EffectiveSystemPrompt, StringComparison.Ordinal);
        Assert.Contains("<colorvision_host_policy>", requestProfile.EffectiveSystemPrompt, StringComparison.Ordinal);
        Assert.Contains("do not grant tools", requestProfile.EffectiveSystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<response_personality>", requestProfile.EffectiveSystemPrompt, StringComparison.Ordinal);
        Assert.Contains("Respond in ", requestProfile.EffectiveSystemPrompt, StringComparison.Ordinal);
        Assert.Equal(CopilotProfileConfig.DefaultSystemPrompt, source.EffectiveSystemPrompt);

        var plan = CopilotAgentRequestFactory.Prepare(
            "Explain the current task.",
            CopilotAgentMode.Auto,
            new CopilotAgentHostContextSnapshot(null, null, attachments: null));
        var agentRequest = CopilotAgentRequestFactory.Create(plan, new CopilotAgentRequestBuildInput
        {
            Profile = requestProfile,
            AgentDefaults = new CopilotAgentDefaultsConfig(),
        });
        Assert.Same(requestProfile, agentRequest.Profile);
        Assert.Contains("Use the configured project persona.", agentRequest.Profile.EffectiveSystemPrompt, StringComparison.Ordinal);
        Assert.Contains("<colorvision_host_policy>", agentRequest.Profile.EffectiveSystemPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplicitProfileSystemPromptWinsOverConfiguredModelInstructions()
    {
        var source = CopilotProfileConfig.CreateDefault();
        source.UseSystemPromptOverride("Explicit runtime profile instructions.");

        var requestProfile = CopilotResponsePresentationGuidance.CreateRequestProfile(
            source,
            configuredModelInstructions: "Configured model instructions.");

        Assert.StartsWith("Explicit runtime profile instructions.", requestProfile.EffectiveSystemPrompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Configured model instructions.", requestProfile.EffectiveSystemPrompt, StringComparison.Ordinal);
        Assert.DoesNotContain("<colorvision_host_policy>", requestProfile.EffectiveSystemPrompt, StringComparison.Ordinal);
        Assert.Contains("Respond in ", requestProfile.EffectiveSystemPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public void EmptyOrOversizedModelInstructionsFileFallsBackToTheSafeDefaultBody()
    {
        string globalRoot = CreateTemporaryDirectory();
        try
        {
            string instructionsPath = Path.Combine(globalRoot, "model.md");
            File.WriteAllText(instructionsPath, string.Empty);
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                "model_instructions_file = \"model.md\"");

            var emptyOptions = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot);
            var emptyProfile = CopilotResponsePresentationGuidance.CreateRequestProfile(
                CopilotProfileConfig.CreateDefault(),
                configuredModelInstructions: emptyOptions.ModelInstructions);

            Assert.True(emptyOptions.HasModelInstructionsFileOverride);
            Assert.False(emptyOptions.HasEffectiveModelInstructions);
            Assert.StartsWith(CopilotProfileConfig.DefaultSystemPrompt, emptyProfile.EffectiveSystemPrompt, StringComparison.Ordinal);

            File.WriteAllText(
                instructionsPath,
                new string('M', CopilotProjectInstructionDiscoveryConfig.MaximumModelInstructionCharacters + 1));
            var oversizedOptions = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot);

            Assert.True(oversizedOptions.HasModelInstructionsFileOverride);
            Assert.False(oversizedOptions.HasEffectiveModelInstructions);
            Assert.Empty(oversizedOptions.ModelInstructions);
            Assert.Empty(oversizedOptions.ModelInstructionsSourceFilePath);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
        }
    }

    [Fact]
    public void EmptyOrOversizedInlineModelInstructionsFallBackToTheSafeDefaultBody()
    {
        string globalRoot = CreateTemporaryDirectory();
        try
        {
            string configPath = Path.Combine(globalRoot, "config.toml");
            File.WriteAllText(configPath, "instructions = \"\"");

            var emptyOptions = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot);
            var emptyProfile = CopilotResponsePresentationGuidance.CreateRequestProfile(
                CopilotProfileConfig.CreateDefault(),
                configuredModelInstructions: emptyOptions.ModelInstructions);

            Assert.True(emptyOptions.HasModelInstructionsInlineOverride);
            Assert.True(emptyOptions.HasModelInstructionsOverride);
            Assert.False(emptyOptions.HasEffectiveModelInstructions);
            Assert.StartsWith(CopilotProfileConfig.DefaultSystemPrompt, emptyProfile.EffectiveSystemPrompt, StringComparison.Ordinal);

            File.WriteAllText(
                configPath,
                $"instructions = \"{new string('I', CopilotProjectInstructionDiscoveryConfig.MaximumModelInstructionCharacters + 1)}\"");
            var oversizedOptions = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot);

            Assert.False(oversizedOptions.HasModelInstructionsInlineOverride);
            Assert.False(oversizedOptions.HasModelInstructionsOverride);
            Assert.False(oversizedOptions.HasEffectiveModelInstructions);
            Assert.Empty(oversizedOptions.ModelInstructions);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
        }
    }

    [Fact]
    public void ModelInstructionsDiagnosticsExposeOnlySourceCountAndHostBoundary()
    {
        string globalRoot = CreateTemporaryDirectory();
        try
        {
            const string secretBody = "MODEL_INSTRUCTIONS_BODY_MUST_NOT_LEAK";
            string instructionsPath = Path.Combine(globalRoot, "model.md");
            File.WriteAllText(instructionsPath, secretBody);
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                "model_instructions_file = \"model.md\"");
            var options = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot);

            string memoryReport = CopilotProjectInstructionDiagnostics.Format(
                new CopilotProjectInstructionSnapshot(
                    string.Empty,
                    string.Empty,
                    globalRoot,
                    options,
                    Array.Empty<CopilotProjectInstructionDocument>()),
                hasActiveAgentRun: false);
            string contextReport = CopilotContextDiagnostics.Format(new CopilotContextDiagnosticSnapshot
            {
                HasConfiguredModelInstructionsOverride = true,
                ConfiguredModelInstructionsCharacters = options.ModelInstructions.Length,
                ConfiguredModelInstructionsSourceLabel = options.ModelInstructionsSourceLabel,
                ConfiguredModelInstructionsUsesFile = options.ModelInstructionsUsesFile,
                ConfiguredModelInstructionsApplied = true,
            });
            string debugReport = CopilotEffectiveConfigDiagnostics.Format(new CopilotEffectiveConfigDiagnosticContext
            {
                Config = new CopilotConfig(),
                State = new CopilotChatState(),
                CodexConfigOptions = options,
            });

            Assert.Contains("Codex model_instructions_file：", memoryReport, StringComparison.Ordinal);
            Assert.Contains(instructionsPath, memoryReport, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("宿主安全规则", contextReport, StringComparison.Ordinal);
            Assert.Contains(options.ModelInstructionsSourceLabel, debugReport, StringComparison.Ordinal);
            Assert.Contains(instructionsPath, debugReport, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(secretBody, memoryReport, StringComparison.Ordinal);
            Assert.DoesNotContain(secretBody, contextReport, StringComparison.Ordinal);
            Assert.DoesNotContain(secretBody, debugReport, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
        }
    }

    [Fact]
    public void InlineModelInstructionsDiagnosticsExposeOnlySourceCountAndHostBoundary()
    {
        string globalRoot = CreateTemporaryDirectory();
        try
        {
            const string secretBody = "INLINE_MODEL_INSTRUCTIONS_BODY_MUST_NOT_LEAK";
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                $"instructions = \"{secretBody}\"");
            var options = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot);

            string memoryReport = CopilotProjectInstructionDiagnostics.Format(
                new CopilotProjectInstructionSnapshot(
                    string.Empty,
                    string.Empty,
                    globalRoot,
                    options,
                    Array.Empty<CopilotProjectInstructionDocument>()),
                hasActiveAgentRun: false);
            string contextReport = CopilotContextDiagnostics.Format(new CopilotContextDiagnosticSnapshot
            {
                HasConfiguredModelInstructionsOverride = options.HasModelInstructionsOverride,
                ConfiguredModelInstructionsCharacters = options.ModelInstructions.Length,
                ConfiguredModelInstructionsSourceLabel = options.ModelInstructionsSourceLabel,
                ConfiguredModelInstructionsUsesFile = options.ModelInstructionsUsesFile,
                ConfiguredModelInstructionsApplied = true,
            });
            string debugReport = CopilotEffectiveConfigDiagnostics.Format(new CopilotEffectiveConfigDiagnosticContext
            {
                Config = new CopilotConfig(),
                State = new CopilotChatState(),
                CodexConfigOptions = options,
            });

            Assert.Contains("Codex instructions：", memoryReport, StringComparison.Ordinal);
            Assert.Contains("Codex instructions：", contextReport, StringComparison.Ordinal);
            Assert.Contains("Codex instructions：", debugReport, StringComparison.Ordinal);
            Assert.Contains(options.ModelInstructionsSourceLabel, debugReport, StringComparison.Ordinal);
            Assert.Contains("宿主安全规则", contextReport, StringComparison.Ordinal);
            Assert.DoesNotContain(secretBody, memoryReport, StringComparison.Ordinal);
            Assert.DoesNotContain(secretBody, contextReport, StringComparison.Ordinal);
            Assert.DoesNotContain(secretBody, debugReport, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
        }
    }

    [Fact]
    public void PersonalityUsesTheClosestTrustedConfigLayerAndKeepsItsSubmissionSnapshot()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                $"""
                personality = "friendly"

                [projects.'{projectRoot}']
                trust_level = "trusted"
                """);
            string sourceDirectory = Path.Combine(projectRoot, "src");
            string configDirectory = Path.Combine(sourceDirectory, ".codex");
            Directory.CreateDirectory(configDirectory);
            string configPath = Path.Combine(configDirectory, "config.toml");
            File.WriteAllText(configPath, "personality = \"none\"");

            var submittedContext = new CopilotAgentHostContextSnapshot(
                activeDocumentPath: null,
                sourceDirectory,
                attachments: null,
                liveContext: null,
                conversationHistory: null,
                additionalReadRootPaths: null,
                globalInstructionRootPath: globalRoot);
            File.WriteAllText(configPath, "personality = \"pragmatic\"");
            var refreshedContext = new CopilotAgentHostContextSnapshot(
                activeDocumentPath: null,
                sourceDirectory,
                attachments: null,
                liveContext: null,
                conversationHistory: null,
                additionalReadRootPaths: null,
                globalInstructionRootPath: globalRoot);

            var submittedOptions = submittedContext.ProjectInstructionDiscoveryOptions;
            Assert.True(submittedOptions.HasPersonalityOverride);
            Assert.Equal(CopilotResponsePersonality.None, submittedOptions.ConfiguredPersonality);
            Assert.Equal(CopilotProjectInstructionConfigSources.TrustedProject, submittedOptions.PersonalitySource);
            Assert.Contains(configPath, submittedOptions.AppliedProjectConfigFilePaths, StringComparer.OrdinalIgnoreCase);
            Assert.Equal(
                CopilotResponsePersonality.Pragmatic,
                refreshedContext.ProjectInstructionDiscoveryOptions.ConfiguredPersonality);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void PersonalityFeatureUsesTheClosestTrustedLayerAndGatesConversationOverrides()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                $"""
                personality = "friendly"

                [features]
                personality = true

                [projects.'{projectRoot}']
                trust_level = "trusted"
                """);
            string configDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(configDirectory);
            string configPath = Path.Combine(configDirectory, "config.toml");
            File.WriteAllText(
                configPath,
                "personality = \"pragmatic\"\n\n[features]\npersonality = false");

            var submittedContext = new CopilotAgentHostContextSnapshot(
                activeDocumentPath: null,
                projectRoot,
                attachments: null,
                liveContext: null,
                conversationHistory: null,
                additionalReadRootPaths: null,
                globalInstructionRootPath: globalRoot);
            File.WriteAllText(
                configPath,
                "personality = \"friendly\"\n\n[features]\npersonality = true");
            var refreshed = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot, projectRoot);
            var submitted = submittedContext.ProjectInstructionDiscoveryOptions;
            var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
            conversation.ResponsePersonality = CopilotResponsePersonality.Friendly;
            conversation.HasResponsePersonalityOverride = true;

            var gated = CopilotResponsePersonalitySelection.Resolve(conversation, submitted);
            var enabled = CopilotResponsePersonalitySelection.Resolve(conversation, refreshed);
            var gatedProfile = CopilotResponsePresentationGuidance.CreateRequestProfile(
                CopilotProfileConfig.CreateDefault(),
                gated.Personality);
            var enabledProfile = CopilotResponsePresentationGuidance.CreateRequestProfile(
                CopilotProfileConfig.CreateDefault(),
                enabled.Personality);

            Assert.False(submitted.ConfiguredPersonalityEnabled);
            Assert.True(submitted.HasPersonalityEnabledOverride);
            Assert.Equal(CopilotProjectInstructionConfigSources.TrustedProject, submitted.PersonalityEnabledSource);
            Assert.Equal(CopilotResponsePersonality.None, gated.Personality);
            Assert.Contains("features.personality", gated.SourceLabel, StringComparison.Ordinal);
            Assert.DoesNotContain("<response_personality>", gatedProfile.EffectiveSystemPrompt, StringComparison.Ordinal);
            Assert.True(refreshed.ConfiguredPersonalityEnabled);
            Assert.Equal(CopilotResponsePersonality.Friendly, enabled.Personality);
            Assert.Contains("<response_personality>", enabledProfile.EffectiveSystemPrompt, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void EnabledPersonalityFeatureDefaultsToPragmaticWhileExplicitNoneStillWins()
    {
        var defaults = CopilotProjectInstructionDiscoveryConfig.CreateDefault();
        var defaultResolution = CopilotResponsePersonalitySelection.Resolve(
            conversation: null,
            defaults);
        var explicitNone = CopilotResponsePersonalitySelection.Resolve(
            conversation: null,
            defaults with
            {
                ConfiguredPersonality = CopilotResponsePersonality.None,
                HasPersonalityOverride = true,
                PersonalitySource = CopilotProjectInstructionConfigSources.CodexHome,
            });

        Assert.True(defaults.ConfiguredPersonalityEnabled);
        Assert.Equal(CopilotResponsePersonality.Pragmatic, defaultResolution.Personality);
        Assert.Equal("Codex features.personality 稳定功能默认值", defaultResolution.SourceLabel);
        Assert.Equal(CopilotResponsePersonality.None, explicitNone.Personality);
    }

    [Fact]
    public void UntrustedOrInvalidProjectPersonalityCannotOverrideTheCodexHomeDefault()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                $"""
                personality = "friendly"

                [features]
                personality = true

                [projects.'{projectRoot}']
                trust_level = "untrusted"
                """);
            string configDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(configDirectory);
            File.WriteAllText(
                Path.Combine(configDirectory, "config.toml"),
                "personality = \"pragmatic\"\n\n[features]\npersonality = false");

            var untrusted = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot, projectRoot);
            Assert.True(untrusted.ConfiguredPersonalityEnabled);
            Assert.Equal(CopilotProjectInstructionConfigSources.CodexHome, untrusted.PersonalityEnabledSource);
            Assert.Equal(CopilotResponsePersonality.Friendly, untrusted.ConfiguredPersonality);
            Assert.Equal(CopilotProjectInstructionConfigSources.CodexHome, untrusted.PersonalitySource);
            Assert.Empty(untrusted.AppliedProjectConfigFilePaths);

            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                "personality = \"unknown\"\n\n[features]\npersonality = \"disabled\"");
            var invalid = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot);
            Assert.True(invalid.ConfiguredPersonalityEnabled);
            Assert.False(invalid.HasPersonalityEnabledOverride);
            Assert.False(invalid.HasPersonalityOverride);
            Assert.Equal(CopilotResponsePersonality.None, invalid.ConfiguredPersonality);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void ExplicitConversationPersonalityWinsOverTheConfiguredDefaultIncludingNone()
    {
        var options = CopilotProjectInstructionDiscoveryConfig.CreateDefault() with
        {
            ConfiguredPersonality = CopilotResponsePersonality.Friendly,
            HasPersonalityOverride = true,
            PersonalitySource = CopilotProjectInstructionConfigSources.CodexHome,
        };
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");

        var configured = CopilotResponsePersonalitySelection.Resolve(conversation, options);
        Assert.Equal(CopilotResponsePersonality.Friendly, configured.Personality);
        Assert.Equal(options.PersonalitySourceLabel, configured.SourceLabel);
        var configuredProfile = CopilotResponsePresentationGuidance.CreateRequestProfile(
            CopilotProfileConfig.CreateDefault(),
            configured.Personality);
        Assert.Contains("<response_personality>", configuredProfile.EffectiveSystemPrompt, StringComparison.Ordinal);
        Assert.Contains("warm, collaborative", configuredProfile.EffectiveSystemPrompt, StringComparison.Ordinal);

        conversation.ResponsePersonality = CopilotResponsePersonality.None;
        conversation.HasResponsePersonalityOverride = true;
        var explicitNone = CopilotResponsePersonalitySelection.Resolve(conversation, options);
        Assert.Equal(CopilotResponsePersonality.None, explicitNone.Personality);
        Assert.Equal("会话覆盖", explicitNone.SourceLabel);
        var neutralProfile = CopilotResponsePresentationGuidance.CreateRequestProfile(
            CopilotProfileConfig.CreateDefault(),
            explicitNone.Personality);
        Assert.DoesNotContain("<response_personality>", neutralProfile.EffectiveSystemPrompt, StringComparison.Ordinal);

        conversation.HasResponsePersonalityOverride = false;
        conversation.ResponsePersonality = CopilotResponsePersonality.Pragmatic;
        var legacySelection = CopilotResponsePersonalitySelection.Resolve(conversation, options);
        Assert.Equal(CopilotResponsePersonality.Pragmatic, legacySelection.Personality);
        Assert.Equal("会话覆盖", legacySelection.SourceLabel);
    }

    [Fact]
    public void PersonalityDiagnosticsReportConfiguredAndEffectiveSourcesWithoutPromptContent()
    {
        var options = CopilotProjectInstructionDiscoveryConfig.CreateDefault() with
        {
            ConfiguredPersonality = CopilotResponsePersonality.Friendly,
            HasPersonalityOverride = true,
            PersonalitySource = CopilotProjectInstructionConfigSources.CodexHome,
        };
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.HasResponsePersonalityOverride = true;
        conversation.ResponsePersonality = CopilotResponsePersonality.None;
        string memoryReport = CopilotProjectInstructionDiagnostics.Format(
            new CopilotProjectInstructionSnapshot(
                string.Empty,
                string.Empty,
                string.Empty,
                options,
                Array.Empty<CopilotProjectInstructionDocument>()),
            hasActiveAgentRun: false);
        string contextReport = CopilotContextDiagnostics.Format(new CopilotContextDiagnosticSnapshot
        {
            ResponsePersonality = CopilotResponsePersonality.None,
            ResponsePersonalitySourceLabel = "会话覆盖",
        });
        string debugReport = CopilotEffectiveConfigDiagnostics.Format(new CopilotEffectiveConfigDiagnosticContext
        {
            Config = new CopilotConfig(),
            State = new CopilotChatState(),
            Conversation = conversation,
            CodexConfigOptions = options,
        });

        Assert.Contains("Codex personality：友好（friendly）", memoryReport, StringComparison.Ordinal);
        Assert.Contains(options.PersonalitySourceLabel, memoryReport, StringComparison.Ordinal);
        Assert.Contains("回答风格：无（none） · 来源 会话覆盖", contextReport, StringComparison.Ordinal);
        Assert.Contains("回答风格：无 · 来源 会话覆盖", debugReport, StringComparison.Ordinal);
        Assert.Contains("Codex personality 默认：友好", debugReport, StringComparison.Ordinal);
        Assert.Contains("会话覆盖优先", debugReport, StringComparison.Ordinal);
    }

    [Fact]
    public void DisabledPersonalityFeatureDiagnosticsExplainTheEffectiveNeutralStyle()
    {
        var options = CopilotProjectInstructionDiscoveryConfig.CreateDefault() with
        {
            ConfiguredPersonalityEnabled = false,
            HasPersonalityEnabledOverride = true,
            PersonalityEnabledSource = CopilotProjectInstructionConfigSources.CodexHome,
            ConfiguredPersonality = CopilotResponsePersonality.Friendly,
            HasPersonalityOverride = true,
            PersonalitySource = CopilotProjectInstructionConfigSources.CodexHome,
        };
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.HasResponsePersonalityOverride = true;
        conversation.ResponsePersonality = CopilotResponsePersonality.Pragmatic;
        var resolution = CopilotResponsePersonalitySelection.Resolve(conversation, options);
        string memoryReport = CopilotProjectInstructionDiagnostics.Format(
            new CopilotProjectInstructionSnapshot(
                string.Empty,
                string.Empty,
                string.Empty,
                options,
                Array.Empty<CopilotProjectInstructionDocument>()),
            hasActiveAgentRun: false);
        string contextReport = CopilotContextDiagnostics.Format(new CopilotContextDiagnosticSnapshot
        {
            ResponsePersonality = resolution.Personality,
            ResponsePersonalitySourceLabel = resolution.SourceLabel,
            CodexPersonalityEnabled = false,
            HasCodexPersonalityEnabledOverride = true,
            CodexPersonalityEnabledSourceLabel = options.PersonalityEnabledSourceLabel,
        });
        string debugReport = CopilotEffectiveConfigDiagnostics.Format(new CopilotEffectiveConfigDiagnosticContext
        {
            Config = new CopilotConfig(),
            State = new CopilotChatState(),
            Conversation = conversation,
            CodexConfigOptions = options,
        });

        Assert.Equal(CopilotResponsePersonality.None, resolution.Personality);
        Assert.Contains("Codex features.personality：false", memoryReport, StringComparison.Ordinal);
        Assert.Contains("被 features.personality=false 阻断", memoryReport, StringComparison.Ordinal);
        Assert.Contains("回答风格：无（none）", contextReport, StringComparison.Ordinal);
        Assert.Contains("Personality 功能：关闭", contextReport, StringComparison.Ordinal);
        Assert.Contains("Codex features.personality：false", debugReport, StringComparison.Ordinal);
        Assert.Contains("回答风格：无", debugReport, StringComparison.Ordinal);
    }

    [Fact]
    public void WebSearchModeUsesTheClosestTrustedLayerAndKeepsItsRequestSnapshot()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                $"""
                web_search = "live"

                [projects.'{projectRoot}']
                trust_level = "trusted"
                """);
            string sourceDirectory = Path.Combine(projectRoot, "src");
            string configDirectory = Path.Combine(sourceDirectory, ".codex");
            Directory.CreateDirectory(configDirectory);
            string configPath = Path.Combine(configDirectory, "config.toml");
            File.WriteAllText(configPath, "web_search = \"cached\"");

            var submittedContext = new CopilotAgentHostContextSnapshot(
                activeDocumentPath: null,
                sourceDirectory,
                attachments: null,
                liveContext: null,
                conversationHistory: null,
                additionalReadRootPaths: null,
                globalInstructionRootPath: globalRoot);
            File.WriteAllText(configPath, "web_search = \"live\"");
            var submittedPlan = CopilotAgentRequestFactory.Prepare(
                "search the web for current documentation",
                CopilotAgentMode.Auto,
                submittedContext);
            var submittedRequest = CopilotAgentRequestFactory.Create(
                submittedPlan,
                new CopilotAgentRequestBuildInput
                {
                    Profile = CopilotProfileConfig.CreateDefault(),
                    AgentDefaults = new CopilotAgentDefaultsConfig(),
                });
            var refreshedContext = new CopilotAgentHostContextSnapshot(
                activeDocumentPath: null,
                sourceDirectory,
                attachments: null,
                liveContext: null,
                conversationHistory: null,
                additionalReadRootPaths: null,
                globalInstructionRootPath: globalRoot);

            var submittedOptions = submittedContext.ProjectInstructionDiscoveryOptions;
            Assert.True(submittedOptions.HasWebSearchModeOverride);
            Assert.Equal(CopilotCodexWebSearchMode.Cached, submittedOptions.ConfiguredWebSearchMode);
            Assert.Equal(CopilotProjectInstructionConfigSources.TrustedProject, submittedOptions.WebSearchModeSource);
            Assert.Equal(CopilotCodexWebSearchMode.Cached, submittedPlan.CodexWebSearchMode);
            Assert.Equal(CopilotCodexWebSearchMode.Cached, submittedRequest.CodexWebSearchMode);
            Assert.Equal(
                CopilotCodexWebSearchMode.Live,
                refreshedContext.ProjectInstructionDiscoveryOptions.ConfiguredWebSearchMode);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void UntrustedOrInvalidProjectWebSearchModeCannotOverrideTheCodexHomeValue()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                $"""
                web_search = "live"

                [projects.'{projectRoot}']
                trust_level = "untrusted"
                """);
            string configDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(configDirectory);
            File.WriteAllText(Path.Combine(configDirectory, "config.toml"), "web_search = \"disabled\"");

            var untrusted = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot, projectRoot);
            Assert.Equal(CopilotCodexWebSearchMode.Live, untrusted.ConfiguredWebSearchMode);
            Assert.Equal(CopilotProjectInstructionConfigSources.CodexHome, untrusted.WebSearchModeSource);
            Assert.Empty(untrusted.AppliedProjectConfigFilePaths);

            File.WriteAllText(Path.Combine(globalRoot, "config.toml"), "web_search = \"unknown\"");
            var invalid = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot);
            Assert.False(invalid.HasWebSearchModeOverride);
            Assert.Equal(CopilotCodexWebSearchMode.Unspecified, invalid.ConfiguredWebSearchMode);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void LegacyWebSearchFeatureFlagsFollowCodexFallbackPrecedence()
    {
        string globalRoot = CreateTemporaryDirectory();
        string configPath = Path.Combine(globalRoot, "config.toml");
        try
        {
            File.WriteAllText(
                configPath,
                """
                [features]
                web_search = true
                """);
            var legacyAlias = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot);
            Assert.True(legacyAlias.HasWebSearchModeOverride);
            Assert.Equal(CopilotCodexWebSearchMode.Live, legacyAlias.ConfiguredWebSearchMode);
            Assert.Equal(
                CopilotCodexWebSearchConfigKey.FeaturesWebSearch,
                legacyAlias.WebSearchModeConfigKey);
            Assert.EndsWith(
                "features.web_search",
                legacyAlias.WebSearchModeSourceLabel,
                StringComparison.Ordinal);

            File.WriteAllText(
                configPath,
                """
                [features]
                web_search_request = true
                web_search_cached = true
                """);
            var cachedWins = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot);
            Assert.Equal(CopilotCodexWebSearchMode.Cached, cachedWins.ConfiguredWebSearchMode);
            Assert.Equal(
                CopilotCodexWebSearchConfigKey.FeaturesWebSearchCached,
                cachedWins.WebSearchModeConfigKey);
            Assert.EndsWith(
                "features.web_search_cached",
                cachedWins.WebSearchModeSourceLabel,
                StringComparison.Ordinal);

            File.WriteAllText(
                configPath,
                """
                web_search = "disabled"

                [features]
                web_search = true
                web_search_request = true
                web_search_cached = true
                """);
            var canonicalWins = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot);
            Assert.Equal(CopilotCodexWebSearchMode.Disabled, canonicalWins.ConfiguredWebSearchMode);
            Assert.Equal(
                CopilotCodexWebSearchConfigKey.WebSearch,
                canonicalWins.WebSearchModeConfigKey);
            Assert.EndsWith(
                "config.toml web_search",
                canonicalWins.WebSearchModeSourceLabel,
                StringComparison.Ordinal);

            File.WriteAllText(
                configPath,
                """
                [features]
                web_search = false
                web_search_request = false
                web_search_cached = false
                """);
            var disabledLegacyFlags = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot);
            Assert.False(disabledLegacyFlags.HasWebSearchModeOverride);
            Assert.Equal(
                CopilotCodexWebSearchMode.Unspecified,
                disabledLegacyFlags.ConfiguredWebSearchMode);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
        }
    }

    [Fact]
    public void WebSearchConfigMergesKeysAcrossTrustedLayersBeforeResolvingMode()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            string globalConfigPath = Path.Combine(globalRoot, "config.toml");
            string projectConfigDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(projectConfigDirectory);
            string projectConfigPath = Path.Combine(projectConfigDirectory, "config.toml");

            File.WriteAllText(
                globalConfigPath,
                $"""
                web_search = "disabled"

                [projects.'{projectRoot}']
                trust_level = "trusted"
                """);
            File.WriteAllText(
                projectConfigPath,
                """
                [features]
                web_search_cached = true
                """);
            var globalCanonicalWins = CopilotProjectInstructionDiscoveryConfig.Load(
                globalRoot,
                projectRoot);
            Assert.Equal(
                CopilotCodexWebSearchMode.Disabled,
                globalCanonicalWins.ConfiguredWebSearchMode);
            Assert.Equal(
                CopilotProjectInstructionConfigSources.CodexHome,
                globalCanonicalWins.WebSearchModeSource);
            Assert.Equal(
                CopilotCodexWebSearchConfigKey.WebSearch,
                globalCanonicalWins.WebSearchModeConfigKey);

            File.WriteAllText(
                globalConfigPath,
                $"""
                [features]
                web_search_request = true
                web_search_cached = true

                [projects.'{projectRoot}']
                trust_level = "trusted"
                """);
            File.WriteAllText(
                projectConfigPath,
                """
                [features]
                web_search_cached = false
                """);
            var projectClearsCachedFlag = CopilotProjectInstructionDiscoveryConfig.Load(
                globalRoot,
                projectRoot);
            Assert.Equal(
                CopilotCodexWebSearchMode.Live,
                projectClearsCachedFlag.ConfiguredWebSearchMode);
            Assert.Equal(
                CopilotProjectInstructionConfigSources.CodexHome,
                projectClearsCachedFlag.WebSearchModeSource);
            Assert.Equal(
                CopilotCodexWebSearchConfigKey.FeaturesWebSearchRequest,
                projectClearsCachedFlag.WebSearchModeConfigKey);

            File.WriteAllText(projectConfigPath, "web_search = \"disabled\"");
            var projectCanonicalWins = CopilotProjectInstructionDiscoveryConfig.Load(
                globalRoot,
                projectRoot);
            Assert.Equal(
                CopilotCodexWebSearchMode.Disabled,
                projectCanonicalWins.ConfiguredWebSearchMode);
            Assert.Equal(
                CopilotProjectInstructionConfigSources.TrustedProject,
                projectCanonicalWins.WebSearchModeSource);
            Assert.Equal(
                CopilotCodexWebSearchConfigKey.WebSearch,
                projectCanonicalWins.WebSearchModeConfigKey);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void UnsupportedOrDisabledWebSearchFailsClosedWithoutBlockingExplicitUrlFetch()
    {
        var registry = new CopilotToolRegistry(CopilotToolRegistry.CreateDefaultTools());
        foreach (var configuredMode in new[]
        {
            CopilotCodexWebSearchMode.Disabled,
            CopilotCodexWebSearchMode.Cached,
            CopilotCodexWebSearchMode.Indexed,
        })
        {
            var searchRequest = new CopilotAgentRequest
            {
                UserText = "search the web for current documentation",
                Mode = CopilotAgentMode.Auto,
                CodexWebSearchMode = configuredMode,
            };
            var searchTools = registry.FindTools(searchRequest).Select(tool => tool.Name).ToArray();

            Assert.DoesNotContain("WebSearch", searchTools, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain("DelegateScout", searchTools, StringComparer.OrdinalIgnoreCase);
            Assert.False(CopilotToolIntentPolicy.CanExposeExternalTool(
                searchRequest,
                "web_search",
                "search the public web"));
            var followUpRequest = new CopilotAgentRequest
            {
                UserText = "continue",
                Mode = CopilotAgentMode.Auto,
                History = [new CopilotRequestMessage("assistant", "https://example.com")],
                CodexWebSearchMode = configuredMode,
            };
            Assert.False(CopilotToolIntentPolicy.CanRetainForFollowUp(
                followUpRequest,
                new CopilotWebSearchTool()));
            string harness = CopilotMicrosoftAgentFrameworkRuntime.BuildHarnessInstructions(
                searchRequest,
                registry.FindTools(searchRequest),
                CopilotAgentEnvironmentContext.Capture(searchRequest),
                taskLedgerEnabled: false,
                agentModeEnabled: false);
            Assert.Contains(
                $"Codex web_search={CopilotCodexWebSearchModeSelection.GetConfigToken(configuredMode)}",
                harness,
                StringComparison.Ordinal);
            Assert.Contains("claim that a search ran", harness, StringComparison.Ordinal);

            var urlRequest = new CopilotAgentRequest
            {
                UserText = "read https://example.com/reference",
                Mode = CopilotAgentMode.Auto,
                CodexWebSearchMode = configuredMode,
            };
            var urlTools = registry.FindTools(urlRequest).Select(tool => tool.Name).ToArray();

            Assert.Contains("FetchUrl", urlTools, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("DelegateScout", urlTools, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain("WebSearch", urlTools, StringComparer.OrdinalIgnoreCase);
            Assert.True(CopilotToolIntentPolicy.CanExposeExternalTool(
                urlRequest,
                "fetch_url",
                "read web page"));
        }
    }

    [Fact]
    public void LiveOrUnspecifiedWebSearchPreservesIntentGatedSearchTools()
    {
        var registry = new CopilotToolRegistry(CopilotToolRegistry.CreateDefaultTools());
        foreach (var configuredMode in new[]
        {
            CopilotCodexWebSearchMode.Unspecified,
            CopilotCodexWebSearchMode.Live,
        })
        {
            var request = new CopilotAgentRequest
            {
                UserText = "search the web for current documentation",
                Mode = CopilotAgentMode.Auto,
                CodexWebSearchMode = configuredMode,
            };
            var toolNames = registry.FindTools(request).Select(tool => tool.Name).ToArray();

            Assert.Contains("WebSearch", toolNames, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("DelegateScout", toolNames, StringComparer.OrdinalIgnoreCase);
            Assert.True(CopilotToolIntentPolicy.CanExposeExternalTool(
                request,
                "web_search",
                "search the public web"));
        }
    }

    [Fact]
    public void WebSearchDiagnosticsExposeUnsupportedModesWithoutClaimingLiveAccess()
    {
        var options = CopilotProjectInstructionDiscoveryConfig.CreateDefault() with
        {
            ConfiguredWebSearchMode = CopilotCodexWebSearchMode.Cached,
            HasWebSearchModeOverride = true,
            WebSearchModeSource = CopilotProjectInstructionConfigSources.CodexHome,
        };
        string memoryReport = CopilotProjectInstructionDiagnostics.Format(
            new CopilotProjectInstructionSnapshot(
                string.Empty,
                string.Empty,
                string.Empty,
                options,
                Array.Empty<CopilotProjectInstructionDocument>()),
            hasActiveAgentRun: false);
        string contextReport = CopilotContextDiagnostics.Format(new CopilotContextDiagnosticSnapshot
        {
            CodexWebSearchMode = options.ConfiguredWebSearchMode,
            CodexWebSearchModeSourceLabel = options.WebSearchModeSourceLabel,
            HasCodexWebSearchModeOverride = true,
        });
        string debugReport = CopilotEffectiveConfigDiagnostics.Format(new CopilotEffectiveConfigDiagnosticContext
        {
            Config = new CopilotConfig(),
            State = new CopilotChatState(),
            CodexConfigOptions = options,
        });

        Assert.Contains("Codex web_search：cached", memoryReport, StringComparison.Ordinal);
        Assert.Contains("不支持 cached 后端", memoryReport, StringComparison.Ordinal);
        Assert.Contains("不支持 cached 后端", contextReport, StringComparison.Ordinal);
        Assert.Contains(options.WebSearchModeSourceLabel, contextReport, StringComparison.Ordinal);
        Assert.Contains("Codex web_search：cached", debugReport, StringComparison.Ordinal);
        Assert.DoesNotContain("已允许按请求意图实时公网检索", memoryReport, StringComparison.Ordinal);
        Assert.DoesNotContain("已允许按请求意图实时公网检索", contextReport, StringComparison.Ordinal);
        Assert.DoesNotContain("已允许按请求意图实时公网检索", debugReport, StringComparison.Ordinal);
    }

    [Fact]
    public void ModelContextWindowUsesTheClosestTrustedLayerAndKeepsItsRequestSnapshot()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                $"""
                model_context_window = 524_288

                [projects.'{projectRoot}']
                trust_level = "trusted"
                """);
            string sourceDirectory = Path.Combine(projectRoot, "src");
            string configDirectory = Path.Combine(sourceDirectory, ".codex");
            Directory.CreateDirectory(configDirectory);
            string configPath = Path.Combine(configDirectory, "config.toml");
            File.WriteAllText(configPath, "model_context_window = 131_072");

            var submittedContext = new CopilotAgentHostContextSnapshot(
                activeDocumentPath: null,
                sourceDirectory,
                attachments: null,
                liveContext: null,
                conversationHistory: null,
                additionalReadRootPaths: null,
                globalInstructionRootPath: globalRoot);
            File.WriteAllText(configPath, "model_context_window = 65_536");
            var submittedPlan = CopilotAgentRequestFactory.Prepare(
                "Inspect the current workspace.",
                CopilotAgentMode.Auto,
                submittedContext);
            var submittedRequest = CopilotAgentRequestFactory.Create(
                submittedPlan,
                new CopilotAgentRequestBuildInput
                {
                    Profile = CopilotProfileConfig.CreateDefault(),
                    AgentDefaults = new CopilotAgentDefaultsConfig
                    {
                        ContextWindowTokens = 262_144,
                    },
                });
            var updatedHistory = new CopilotConversationHistorySnapshot(
                [new CopilotRequestMessage("user", "After compaction")],
                [new CopilotRequestMessage("user", "After compaction")]);
            var updatedSubmittedContext = submittedContext.WithConversationHistory(updatedHistory);
            var refreshedContext = new CopilotAgentHostContextSnapshot(
                activeDocumentPath: null,
                sourceDirectory,
                attachments: null,
                liveContext: null,
                conversationHistory: null,
                additionalReadRootPaths: null,
                globalInstructionRootPath: globalRoot);

            var submittedOptions = submittedContext.ProjectInstructionDiscoveryOptions;
            Assert.True(submittedOptions.HasModelContextWindowOverride);
            Assert.Equal(131_072, submittedOptions.ConfiguredModelContextWindowTokens);
            Assert.Equal(CopilotProjectInstructionConfigSources.TrustedProject, submittedOptions.ModelContextWindowSource);
            Assert.Equal(131_072, submittedPlan.ModelContextWindowTokensOverride);
            Assert.Equal(131_072, submittedRequest.RunBudgetDefaults?.ContextWindowTokens);
            Assert.Equal(131_072, CopilotAgentRunBudget.Resolve(submittedRequest).ContextWindowTokens);
            Assert.Equal(131_072, updatedSubmittedContext.ProjectInstructionDiscoveryOptions.ConfiguredModelContextWindowTokens);
            Assert.Single(updatedSubmittedContext.ConversationHistory.ModelMessages);
            Assert.Equal(
                65_536,
                refreshedContext.ProjectInstructionDiscoveryOptions.ConfiguredModelContextWindowTokens);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void UntrustedOrOutOfRangeModelContextWindowCannotReplaceTheEffectiveValue()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                $"""
                model_context_window = 262_144

                [projects.'{projectRoot}']
                trust_level = "untrusted"
                """);
            string configDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(configDirectory);
            File.WriteAllText(Path.Combine(configDirectory, "config.toml"), "model_context_window = 65_536");

            var untrusted = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot, projectRoot);
            Assert.Equal(262_144, untrusted.ConfiguredModelContextWindowTokens);
            Assert.Equal(CopilotProjectInstructionConfigSources.CodexHome, untrusted.ModelContextWindowSource);
            Assert.Empty(untrusted.AppliedProjectConfigFilePaths);

            File.WriteAllText(Path.Combine(globalRoot, "config.toml"), "model_context_window = 16_384");
            var belowHostMinimum = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot);
            Assert.False(belowHostMinimum.HasModelContextWindowOverride);
            Assert.Equal(524_288, belowHostMinimum.ResolveContextWindowTokens(524_288));

            File.WriteAllText(Path.Combine(globalRoot, "config.toml"), "model_context_window = 2_097_152");
            var aboveHostMaximum = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot);
            Assert.False(aboveHostMaximum.HasModelContextWindowOverride);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void ModelContextWindowChangesTheSharedHistoryAndAutoCompactionBudget()
    {
        var options = CopilotProjectInstructionDiscoveryConfig.CreateDefault() with
        {
            ConfiguredModelContextWindowTokens = 65_536,
            HasModelContextWindowOverride = true,
            ModelContextWindowSource = CopilotProjectInstructionConfigSources.CodexHome,
        };
        int effectiveContextWindow = options.ResolveContextWindowTokens(524_288);
        var configuredLimits = CopilotConversationRequestBuilder.ResolveHistoryLimits(
            effectiveContextWindow,
            maxOutputTokens: 4_096);
        var applicationLimits = CopilotConversationRequestBuilder.ResolveHistoryLimits(
            contextWindowTokens: 524_288,
            maxOutputTokens: 4_096);
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, new string('x', 60_000)));
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, new string('y', 60_000)));

        var configuredDecision = CopilotConversationAutoCompactionPolicy.Evaluate(
            conversation,
            configuredLimits,
            pendingPrompt: "continue",
            new CopilotConversationAutoCompactionOptions(
                Enabled: true,
                ThresholdPercent: 85,
                ModelTokenLimit: null,
                ModelTokenLimitScope: CopilotModelAutoCompactTokenLimitScope.Total));
        var applicationDecision = CopilotConversationAutoCompactionPolicy.Evaluate(
            conversation,
            applicationLimits,
            pendingPrompt: "continue",
            new CopilotConversationAutoCompactionOptions(
                Enabled: true,
                ThresholdPercent: 85,
                ModelTokenLimit: null,
                ModelTokenLimitScope: CopilotModelAutoCompactTokenLimitScope.Total));

        Assert.Equal(65_536, effectiveContextWindow);
        Assert.True(configuredLimits.MaximumCharacters < applicationLimits.MaximumCharacters);
        Assert.True(configuredDecision.ShouldCompact);
        Assert.False(applicationDecision.ShouldCompact);
    }

    [Fact]
    public void ModelContextWindowDiagnosticsReportTheEffectiveSnapshotAndSource()
    {
        var options = CopilotProjectInstructionDiscoveryConfig.CreateDefault() with
        {
            ConfiguredModelContextWindowTokens = 131_072,
            HasModelContextWindowOverride = true,
            ModelContextWindowSource = CopilotProjectInstructionConfigSources.CodexHome,
        };
        string memoryReport = CopilotProjectInstructionDiagnostics.Format(
            new CopilotProjectInstructionSnapshot(
                string.Empty,
                string.Empty,
                string.Empty,
                options,
                Array.Empty<CopilotProjectInstructionDocument>()),
            hasActiveAgentRun: false);
        string contextReport = CopilotContextDiagnostics.Format(new CopilotContextDiagnosticSnapshot
        {
            HistoryContextWindowTokens = options.ConfiguredModelContextWindowTokens,
            HasModelContextWindowOverride = true,
            ModelContextWindowSourceLabel = options.ModelContextWindowSourceLabel,
        });
        string debugReport = CopilotEffectiveConfigDiagnostics.Format(new CopilotEffectiveConfigDiagnosticContext
        {
            Config = new CopilotConfig
            {
                AgentDefaults = new CopilotAgentDefaultsConfig
                {
                    ContextWindowTokens = 524_288,
                },
            },
            State = new CopilotChatState(),
            CodexConfigOptions = options,
        });

        Assert.Contains("Codex model_context_window：131,072 Token", memoryReport, StringComparison.Ordinal);
        Assert.Contains(options.ModelContextWindowSourceLabel, memoryReport, StringComparison.Ordinal);
        Assert.Contains("Codex model_context_window：131,072 Token", contextReport, StringComparison.Ordinal);
        Assert.Contains("同时约束聊天历史、发送校验、自动压缩和 Agent 上下文", contextReport, StringComparison.Ordinal);
        Assert.Contains("Codex model_context_window：131,072 tokens", debugReport, StringComparison.Ordinal);
        Assert.Contains("请求快照覆盖应用默认值", debugReport, StringComparison.Ordinal);
    }

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"copilot-instructions-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void TrustProject(string globalRoot, string projectRoot)
    {
        Assert.True(
            CopilotCodexProjectTrustPersistence.TryTrustProject(globalRoot, projectRoot, out var error),
            error);
    }
}
