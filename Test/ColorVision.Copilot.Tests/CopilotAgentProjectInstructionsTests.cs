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
            string rootInstructions = Path.Combine(projectRoot, "AGENTS.md");
            File.WriteAllText(rootInstructions, "# Repository instructions");
            string sourceDirectory = Path.Combine(projectRoot, "src", "feature");
            Directory.CreateDirectory(sourceDirectory);
            string nestedInstructions = Path.Combine(sourceDirectory, "AGENTS.md");
            string activeDocument = Path.Combine(sourceDirectory, "Feature.cs");
            File.WriteAllText(nestedInstructions, "# Feature instructions");
            File.WriteAllText(activeDocument, "namespace Feature;");
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
            string rootInstructions = Path.Combine(projectRoot, "AGENTS.md");
            File.WriteAllText(rootInstructions, "# Repository instructions");
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

            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));

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
    public void Utf8InstructionBudgetDoesNotSplitUnicodeContent()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            string instructionPath = Path.Combine(projectRoot, "AGENTS.md");
            File.WriteAllText(instructionPath, "# Unicode\n" + new string('界', 3_000));

            CopilotProjectInstructionDocument document = Assert.Single(
                CopilotAgentProjectInstructions.DiscoverWithGlobal(
                    [projectRoot],
                    activeDocumentPath: null,
                    additionalTargetFilePaths: null,
                    globalInstructionRootPath: globalRoot,
                    discoveryOptions: CopilotProjectInstructionDiscoveryConfig.CreateDefault() with
                    {
                        MaximumBytes = 4_096,
                    }));

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
    public void EmptyModelInstructionsPreserveTheDefaultSystemPrompt()
    {
        var requestProfile = CopilotResponsePresentationGuidance.CreateRequestProfile(
            CopilotProfileConfig.CreateDefault(),
            configuredModelInstructions: string.Empty);

        Assert.StartsWith(CopilotProfileConfig.DefaultSystemPrompt, requestProfile.EffectiveSystemPrompt, StringComparison.Ordinal);
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ModelInstructionsDiagnosticsExposeOnlySourceCountAndHostBoundary(bool usesFile)
    {
        const string secretBody = "MODEL_INSTRUCTIONS_BODY_MUST_NOT_LEAK";
        string instructionsPath = Path.Combine(Path.GetTempPath(), "copilot-diagnostics", "model.md");
        var options = CopilotProjectInstructionDiscoveryConfig.CreateDefault() with
        {
            ConfiguredModelInstructions = usesFile ? string.Empty : secretBody,
            HasModelInstructionsInlineOverride = !usesFile,
            ModelInstructionsInlineSource = CopilotProjectInstructionConfigSources.CodexHome,
            ConfiguredModelInstructionsFileContent = usesFile ? secretBody : string.Empty,
            HasModelInstructionsFileOverride = usesFile,
            ModelInstructionsFileSource = CopilotProjectInstructionConfigSources.CodexHome,
            ConfiguredModelInstructionsSourceFilePath = usesFile ? instructionsPath : string.Empty,
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

        string settingLabel = usesFile ? "Codex model_instructions_file：" : "Codex instructions：";
        Assert.Contains(settingLabel, memoryReport, StringComparison.Ordinal);
        Assert.Contains(settingLabel, contextReport, StringComparison.Ordinal);
        Assert.Contains(settingLabel, debugReport, StringComparison.Ordinal);
        Assert.Contains(options.ModelInstructionsSourceLabel, debugReport, StringComparison.Ordinal);
        Assert.Contains("宿主安全规则", contextReport, StringComparison.Ordinal);
        if (usesFile)
        {
            Assert.Contains(instructionsPath, memoryReport, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(instructionsPath, debugReport, StringComparison.OrdinalIgnoreCase);
        }
        Assert.DoesNotContain(secretBody, memoryReport, StringComparison.Ordinal);
        Assert.DoesNotContain(secretBody, contextReport, StringComparison.Ordinal);
        Assert.DoesNotContain(secretBody, debugReport, StringComparison.Ordinal);
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
}
