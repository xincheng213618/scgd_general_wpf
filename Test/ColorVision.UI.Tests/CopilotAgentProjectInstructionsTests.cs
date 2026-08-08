using ColorVision.Copilot;
using System;
using System.IO;
using System.Linq;

namespace ColorVision.UI.Tests;

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

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"copilot-instructions-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
