using ColorVision.Copilot;
using System;
using System.IO;
using System.Linq;

namespace ColorVision.UI.Tests;

public sealed class CopilotCodexMentionsV2FeatureTests
{
    [Fact]
    public void ClosestTrustedValueControlsTheComposerCatalog()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                $"""
                [features]
                mentions_v2 = false

                [projects.'{projectRoot}']
                trust_level = "trusted"
                """);
            string projectConfigDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(projectConfigDirectory);
            File.WriteAllText(
                Path.Combine(projectConfigDirectory, "config.toml"),
                "features.mentions_v2 = true");

            var options = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot, projectRoot);

            Assert.True(options.ConfiguredMentionsV2Enabled);
            Assert.True(options.HasMentionsV2EnabledOverride);
            Assert.Equal(
                CopilotProjectInstructionConfigSources.TrustedProject,
                options.MentionsV2EnabledSource);
            Assert.True(CopilotComposerReferenceCatalog.CanIncludeReferenceKind(
                CopilotComposerReferenceKind.Template,
                options.ConfiguredMentionsV2Enabled));
            Assert.True(CopilotComposerReferenceCatalog.CanIncludeReferenceKind(
                CopilotComposerReferenceKind.Menu,
                options.ConfiguredMentionsV2Enabled));
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void UntrustedAndInvalidValuesCannotBroadenTheCodexHomeCatalog()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                $"""
                [features]
                mentions_v2 = false

                [projects.'{projectRoot}']
                trust_level = "untrusted"
                """);
            string projectConfigDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(projectConfigDirectory);
            File.WriteAllText(
                Path.Combine(projectConfigDirectory, "config.toml"),
                "[features]" + Environment.NewLine + "mentions_v2 = true");

            var untrusted = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot, projectRoot);

            Assert.False(untrusted.ConfiguredMentionsV2Enabled);
            Assert.Equal(
                CopilotProjectInstructionConfigSources.CodexHome,
                untrusted.MentionsV2EnabledSource);
            Assert.Empty(untrusted.AppliedProjectConfigFilePaths);
            Assert.False(CopilotComposerReferenceCatalog.CanIncludeReferenceKind(
                CopilotComposerReferenceKind.Template,
                untrusted.ConfiguredMentionsV2Enabled));
            Assert.False(CopilotComposerReferenceCatalog.CanIncludeReferenceKind(
                CopilotComposerReferenceKind.Menu,
                untrusted.ConfiguredMentionsV2Enabled));
            Assert.True(CopilotComposerReferenceCatalog.CanIncludeReferenceKind(
                CopilotComposerReferenceKind.File,
                untrusted.ConfiguredMentionsV2Enabled));

            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                "[features]" + Environment.NewLine + "mentions_v2 = \"false\"");
            var invalid = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot);

            Assert.True(invalid.ConfiguredMentionsV2Enabled);
            Assert.False(invalid.HasMentionsV2EnabledOverride);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void DisabledCatalogStillReturnsFileCandidates()
    {
        string root = CreateTemporaryDirectory();
        string filePath = Path.Combine(root, "CopilotMentionsLegacyFile.cs");
        try
        {
            File.WriteAllText(filePath, "// legacy @ file candidate");

            var candidates = CopilotComposerReferenceCatalog.SearchImmediate(
                "CopilotMentionsLegacyFile",
                filePath,
                includeUnifiedReferences: false);

            var file = Assert.Single(candidates);
            Assert.Equal(CopilotComposerReferenceKind.File, file.Kind);
            Assert.Equal(Path.GetFullPath(filePath), file.Value);
            Assert.DoesNotContain(candidates, candidate =>
                candidate.Kind is CopilotComposerReferenceKind.Template or CopilotComposerReferenceKind.Menu);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void DiagnosticsExplainTheLegacyFileFallback()
    {
        var options = CopilotProjectInstructionDiscoveryConfig.CreateDefault() with
        {
            ConfiguredMentionsV2Enabled = false,
            HasMentionsV2EnabledOverride = true,
            MentionsV2EnabledSource = CopilotProjectInstructionConfigSources.CodexHome,
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
            ProfileLabel = "Profile",
            Mode = CopilotAgentMode.Code,
            CodexMentionsV2Enabled = false,
            HasCodexMentionsV2EnabledOverride = true,
            CodexMentionsV2EnabledSourceLabel = options.MentionsV2EnabledSourceLabel,
        });
        string debugReport = CopilotEffectiveConfigDiagnostics.Format(
            new CopilotEffectiveConfigDiagnosticContext
            {
                Config = new CopilotConfig(),
                State = new CopilotChatState(),
                ComposerMode = CopilotAgentMode.Code,
                CodexConfigOptions = options,
            });

        Assert.Contains("Codex features.mentions_v2：false", memoryReport, StringComparison.Ordinal);
        Assert.Contains("回退为旧版文件候选", memoryReport, StringComparison.Ordinal);
        Assert.Contains("features.mentions_v2=false", contextReport, StringComparison.Ordinal);
        Assert.Contains("已有附件与上下文不受影响", contextReport, StringComparison.Ordinal);
        Assert.Contains("features.mentions_v2：false", debugReport, StringComparison.Ordinal);
        Assert.Contains(options.MentionsV2EnabledSourceLabel, debugReport, StringComparison.Ordinal);
    }

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"copilot-codex-mentions-v2-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
