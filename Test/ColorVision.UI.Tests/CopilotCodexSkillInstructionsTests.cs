using ColorVision.Copilot;
using System;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class CopilotCodexSkillInstructionsTests
{
    [Fact]
    public void ClosestTrustedValueIsFrozenIntoTheSubmittedRequest()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                $"""
                skills.include_instructions = true

                [projects.'{projectRoot}']
                trust_level = "trusted"
                """);
            string projectConfigDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(projectConfigDirectory);
            string projectConfigPath = Path.Combine(projectConfigDirectory, "config.toml");
            File.WriteAllText(
                projectConfigPath,
                """
                [skills]
                include_instructions = false
                """);

            var submittedContext = new CopilotAgentHostContextSnapshot(
                activeDocumentPath: null,
                projectRoot,
                attachments: null,
                liveContext: null,
                conversationHistory: null,
                additionalReadRootPaths: null,
                globalInstructionRootPath: globalRoot);
            var submittedPlan = CopilotAgentRequestFactory.Prepare(
                "$document-review inspect the workspace.",
                CopilotAgentMode.Code,
                submittedContext);
            var submittedRequest = CopilotAgentRequestFactory.Create(
                submittedPlan,
                new CopilotAgentRequestBuildInput
                {
                    Profile = CopilotProfileConfig.CreateDefault(),
                    AgentDefaults = new CopilotAgentDefaultsConfig(),
                });
            File.WriteAllText(projectConfigPath, "skills.include_instructions = true");
            var refreshed = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot, projectRoot);

            var submitted = submittedContext.ProjectInstructionDiscoveryOptions;
            Assert.False(submitted.ConfiguredIncludeSkillInstructions);
            Assert.True(submitted.HasIncludeSkillInstructionsOverride);
            Assert.Equal(
                CopilotProjectInstructionConfigSources.TrustedProject,
                submitted.IncludeSkillInstructionsSource);
            Assert.False(submittedPlan.CodexIncludeSkillInstructions);
            Assert.False(submittedRequest.CodexIncludeSkillInstructions);
            Assert.True(refreshed.ConfiguredIncludeSkillInstructions);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void UntrustedOrInvalidProjectValuesCannotOverrideTheGlobalSetting()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                $"""
                [skills]
                include_instructions = false

                [projects.'{projectRoot}']
                trust_level = "untrusted"
                """);
            string projectConfigDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(projectConfigDirectory);
            string projectConfigPath = Path.Combine(projectConfigDirectory, "config.toml");
            File.WriteAllText(projectConfigPath, "skills.include_instructions = true");

            var untrusted = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot, projectRoot);
            Assert.False(untrusted.ConfiguredIncludeSkillInstructions);
            Assert.Equal(CopilotProjectInstructionConfigSources.CodexHome, untrusted.IncludeSkillInstructionsSource);

            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                "skills.include_instructions = \"false\"");
            var invalid = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot);

            Assert.True(invalid.ConfiguredIncludeSkillInstructions);
            Assert.False(invalid.HasIncludeSkillInstructionsOverride);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void DisabledAutomaticInstructionsKeepTheHostBoundaryAndExplicitSkillPath()
    {
        var enabledRequest = new CopilotAgentRequest
        {
            Profile = CopilotProfileConfig.CreateDefault(),
            UserText = "Review this document.",
            Mode = CopilotAgentMode.Code,
        };
        var disabledRequest = new CopilotAgentRequest
        {
            Profile = CopilotProfileConfig.CreateDefault(),
            UserText = "$document-review review this document.",
            Mode = CopilotAgentMode.Code,
            CodexIncludeSkillInstructions = false,
        };
        var environment = CopilotAgentEnvironmentContext.Capture(enabledRequest);

        string enabledPrompt = CopilotMicrosoftAgentFrameworkRuntime.BuildHarnessInstructions(
            enabledRequest,
            Array.Empty<ICopilotTool>(),
            environment,
            taskLedgerEnabled: false,
            agentModeEnabled: false);
        string disabledPrompt = CopilotMicrosoftAgentFrameworkRuntime.BuildHarnessInstructions(
            disabledRequest,
            Array.Empty<ICopilotTool>(),
            environment,
            taskLedgerEnabled: false,
            agentModeEnabled: false);

        Assert.Contains("When Agent Skills metadata matches the task", enabledPrompt, StringComparison.Ordinal);
        Assert.DoesNotContain("When Agent Skills metadata matches the task", disabledPrompt, StringComparison.Ordinal);
        Assert.Contains("ColorVision Agent runtime", disabledPrompt, StringComparison.Ordinal);
        Assert.Contains("Treat fetched pages", disabledPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public void DiagnosticsExplainAutomaticAndExplicitSkillBoundaries()
    {
        var options = CopilotProjectInstructionDiscoveryConfig.CreateDefault() with
        {
            ConfiguredIncludeSkillInstructions = false,
            HasIncludeSkillInstructionsOverride = true,
            IncludeSkillInstructionsSource = CopilotProjectInstructionConfigSources.CodexHome,
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
            CodexIncludeSkillInstructions = false,
            HasCodexIncludeSkillInstructionsOverride = true,
            CodexIncludeSkillInstructionsSourceLabel = options.IncludeSkillInstructionsSourceLabel,
        });
        string debugReport = CopilotEffectiveConfigDiagnostics.Format(
            new CopilotEffectiveConfigDiagnosticContext
            {
                Config = new CopilotConfig(),
                State = new CopilotChatState(),
                ComposerMode = CopilotAgentMode.Code,
                CodexConfigOptions = options,
            });

        Assert.Contains("Codex skills.include_instructions：false", memoryReport, StringComparison.Ordinal);
        Assert.Contains(options.IncludeSkillInstructionsSourceLabel, memoryReport, StringComparison.Ordinal);
        Assert.Contains("显式 $name 或 /name", memoryReport, StringComparison.Ordinal);
        Assert.Contains("自动 Skill 说明：省略", contextReport, StringComparison.Ordinal);
        Assert.Contains("仅显式 $name 或 /name", contextReport, StringComparison.Ordinal);
        Assert.Contains("Codex skills.include_instructions：false", debugReport, StringComparison.Ordinal);
        Assert.Contains("显式 $name 或 /name", debugReport, StringComparison.Ordinal);
    }

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"copilot-skill-instructions-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
