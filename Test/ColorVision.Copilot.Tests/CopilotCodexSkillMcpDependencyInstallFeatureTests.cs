using ColorVision.Copilot;
using System;
using System.IO;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotCodexSkillMcpDependencyInstallFeatureTests
{
    [Fact]
    public void ClosestTrustedValueControlsSkillMcpDependencyPrompts()
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
                skill_mcp_dependency_install = true

                [projects.'{projectRoot}']
                trust_level = "trusted"
                """);
            string projectConfigDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(projectConfigDirectory);
            File.WriteAllText(
                Path.Combine(projectConfigDirectory, "config.toml"),
                "features.skill_mcp_dependency_install = false");

            var options = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot, projectRoot);

            Assert.False(options.ConfiguredSkillMcpDependencyInstallEnabled);
            Assert.True(options.HasSkillMcpDependencyInstallEnabledOverride);
            Assert.Equal(
                CopilotProjectInstructionConfigSources.TrustedProject,
                options.SkillMcpDependencyInstallEnabledSource);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void UntrustedAndInvalidValuesCannotEnableDependencyInstallation()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                $"""
                features.skill_mcp_dependency_install = false

                [projects.'{projectRoot}']
                trust_level = "untrusted"
                """);
            string projectConfigDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(projectConfigDirectory);
            File.WriteAllText(
                Path.Combine(projectConfigDirectory, "config.toml"),
                "features.skill_mcp_dependency_install = true");

            var untrusted = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot, projectRoot);

            Assert.False(untrusted.ConfiguredSkillMcpDependencyInstallEnabled);
            Assert.Equal(
                CopilotProjectInstructionConfigSources.CodexHome,
                untrusted.SkillMcpDependencyInstallEnabledSource);
            Assert.Empty(untrusted.AppliedProjectConfigFilePaths);

            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                "features.skill_mcp_dependency_install = \"false\"");
            var invalid = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot);

            Assert.True(invalid.ConfiguredSkillMcpDependencyInstallEnabled);
            Assert.False(invalid.HasSkillMcpDependencyInstallEnabledOverride);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void DiagnosticsExplainConfirmationAndDisabledBehavior()
    {
        var options = CopilotProjectInstructionDiscoveryConfig.CreateDefault() with
        {
            ConfiguredSkillMcpDependencyInstallEnabled = false,
            HasSkillMcpDependencyInstallEnabledOverride = true,
            SkillMcpDependencyInstallEnabledSource = CopilotProjectInstructionConfigSources.CodexHome,
        };
        string instructions = CopilotProjectInstructionDiagnostics.Format(
            new CopilotProjectInstructionSnapshot(
                string.Empty,
                string.Empty,
                string.Empty,
                options,
                Array.Empty<CopilotProjectInstructionDocument>()),
            hasActiveAgentRun: false);
        string effective = CopilotEffectiveConfigDiagnostics.Format(
            new CopilotEffectiveConfigDiagnosticContext
            {
                Config = new CopilotConfig(),
                State = new CopilotChatState(),
                ComposerMode = CopilotAgentMode.Code,
                CodexConfigOptions = options,
            });

        Assert.Contains("features.skill_mcp_dependency_install：false", instructions, StringComparison.Ordinal);
        Assert.Contains("不提示或写入", instructions, StringComparison.Ordinal);
        Assert.Contains("features.skill_mcp_dependency_install：false", effective, StringComparison.Ordinal);
        Assert.Contains(options.SkillMcpDependencyInstallEnabledSourceLabel, effective, StringComparison.Ordinal);
        Assert.Contains("已有外部 MCP 配置保持有效", effective, StringComparison.Ordinal);
    }

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"copilot-skill-mcp-feature-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
