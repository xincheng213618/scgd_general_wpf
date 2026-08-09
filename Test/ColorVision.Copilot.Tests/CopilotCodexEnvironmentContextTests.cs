using ColorVision.Copilot;
using System;
using System.IO;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotCodexEnvironmentContextTests
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
                include_environment_context = true

                [projects.'{projectRoot}']
                trust_level = "trusted"
                """);
            string projectConfigDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(projectConfigDirectory);
            string projectConfigPath = Path.Combine(projectConfigDirectory, "config.toml");
            File.WriteAllText(projectConfigPath, "include_environment_context = false");

            var submittedContext = new CopilotAgentHostContextSnapshot(
                activeDocumentPath: null,
                projectRoot,
                attachments: null,
                liveContext: null,
                conversationHistory: null,
                additionalReadRootPaths: null,
                globalInstructionRootPath: globalRoot);
            var submittedPlan = CopilotAgentRequestFactory.Prepare(
                "Inspect the current workspace.",
                CopilotAgentMode.Code,
                submittedContext);
            var submittedRequest = CopilotAgentRequestFactory.Create(
                submittedPlan,
                new CopilotAgentRequestBuildInput
                {
                    Profile = CopilotProfileConfig.CreateDefault(),
                    AgentDefaults = new CopilotAgentDefaultsConfig(),
                });
            File.WriteAllText(projectConfigPath, "include_environment_context = true");
            var refreshed = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot, projectRoot);

            var submitted = submittedContext.ProjectInstructionDiscoveryOptions;
            Assert.False(submitted.ConfiguredIncludeEnvironmentContext);
            Assert.True(submitted.HasIncludeEnvironmentContextOverride);
            Assert.Equal(
                CopilotProjectInstructionConfigSources.TrustedProject,
                submitted.IncludeEnvironmentContextSource);
            Assert.False(submittedPlan.CodexIncludeEnvironmentContext);
            Assert.False(submittedRequest.CodexIncludeEnvironmentContext);
            Assert.True(refreshed.ConfiguredIncludeEnvironmentContext);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void DisabledSnapshotOmitsOnlyTheModelVisibleRuntimeEnvironmentBlock()
    {
        const string environmentMarker = "runtime-environment-marker";
        var environment = new CopilotAgentEnvironmentContext
        {
            WorkingDirectory = $"C:\\{environmentMarker}",
            Platform = "Windows",
            Architecture = "X64",
            Shell = "PowerShell",
            LocalDate = "2026-08-09",
            TimeZoneId = "Asia/Shanghai",
        };
        var enabledRequest = new CopilotAgentRequest
        {
            Profile = CopilotProfileConfig.CreateDefault(),
            UserText = "Explain the workspace.",
            Mode = CopilotAgentMode.Code,
        };
        var disabledRequest = new CopilotAgentRequest
        {
            Profile = CopilotProfileConfig.CreateDefault(),
            UserText = "Explain the workspace.",
            Mode = CopilotAgentMode.Code,
            CodexIncludeEnvironmentContext = false,
        };

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

        Assert.Contains("<runtime_environment>", enabledPrompt, StringComparison.Ordinal);
        Assert.Contains(environmentMarker, enabledPrompt, StringComparison.Ordinal);
        Assert.DoesNotContain("<runtime_environment>", disabledPrompt, StringComparison.Ordinal);
        Assert.DoesNotContain(environmentMarker, disabledPrompt, StringComparison.Ordinal);
        Assert.Contains("ColorVision Agent runtime", disabledPrompt, StringComparison.Ordinal);
        Assert.Contains("Treat fetched pages", disabledPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public void DiagnosticsExplainThePromptOnlyBoundary()
    {
        var options = CopilotProjectInstructionDiscoveryConfig.CreateDefault() with
        {
            ConfiguredIncludeEnvironmentContext = false,
            HasIncludeEnvironmentContextOverride = true,
            IncludeEnvironmentContextSource = CopilotProjectInstructionConfigSources.CodexHome,
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
            CodexIncludeEnvironmentContext = false,
            HasCodexIncludeEnvironmentContextOverride = true,
            CodexIncludeEnvironmentContextSourceLabel = options.IncludeEnvironmentContextSourceLabel,
        });
        string debugReport = CopilotEffectiveConfigDiagnostics.Format(
            new CopilotEffectiveConfigDiagnosticContext
            {
                Config = new CopilotConfig(),
                State = new CopilotChatState(),
                ComposerMode = CopilotAgentMode.Code,
                CodexConfigOptions = options,
            });

        Assert.Contains("Codex include_environment_context：false", memoryReport, StringComparison.Ordinal);
        Assert.Contains(options.IncludeEnvironmentContextSourceLabel, memoryReport, StringComparison.Ordinal);
        Assert.Contains("工具侧路径、沙箱与审批边界保持不变", memoryReport, StringComparison.Ordinal);
        Assert.Contains("运行环境上下文：省略", contextReport, StringComparison.Ordinal);
        Assert.Contains("runtime_environment", contextReport, StringComparison.Ordinal);
        Assert.Contains("Codex include_environment_context：false", debugReport, StringComparison.Ordinal);
        Assert.Contains("工具侧路径、沙箱与审批边界保持不变", debugReport, StringComparison.Ordinal);
    }

    [Fact]
    public void DisabledContextDoesNotResumeASessionThatPreviouslyReceivedEnvironmentData()
    {
        var profile = CopilotProfileConfig.CreateDefault();
        var capabilitySnapshot = CopilotCapabilityCatalog.Shared.GetSnapshot();
        var environment = CopilotAgentEnvironmentContext.Capture(new CopilotAgentRequest
        {
            Profile = profile,
            UserText = "Inspect the current workspace.",
            Mode = CopilotAgentMode.Code,
        });
        var checkpoint = CopilotAgentSessionCheckpoint.Create(
            profile,
            "{}",
            capabilitySnapshot,
            environmentContext: environment);

        var compatibility = checkpoint!.EvaluateFor(
            profile,
            capabilitySnapshot,
            environmentContext: null,
            requireEnvironmentContextMatch: true);

        Assert.Equal(CopilotAgentCheckpointCompatibilityKind.EnvironmentDrift, compatibility.Kind);
        Assert.True(compatibility.RequiresReplan);
        Assert.False(compatibility.CanResume);
    }

    [Fact]
    public void ContextFreeSessionResumesOnlyWhileEnvironmentDataRemainsDisabled()
    {
        var profile = CopilotProfileConfig.CreateDefault();
        var capabilitySnapshot = CopilotCapabilityCatalog.Shared.GetSnapshot();
        var checkpoint = CopilotAgentSessionCheckpoint.Create(
            profile,
            "{}",
            capabilitySnapshot,
            environmentContext: null);
        var environment = CopilotAgentEnvironmentContext.Capture(new CopilotAgentRequest
        {
            Profile = profile,
            UserText = "Inspect the current workspace.",
            Mode = CopilotAgentMode.Code,
        });

        var disabledCompatibility = checkpoint!.EvaluateFor(
            profile,
            capabilitySnapshot,
            environmentContext: null,
            requireEnvironmentContextMatch: true);
        var enabledCompatibility = checkpoint.EvaluateFor(
            profile,
            capabilitySnapshot,
            environmentContext: environment,
            requireEnvironmentContextMatch: true);

        Assert.Equal(CopilotAgentCheckpointCompatibilityKind.Compatible, disabledCompatibility.Kind);
        Assert.True(disabledCompatibility.CanResume);
        Assert.Equal(CopilotAgentCheckpointCompatibilityKind.EnvironmentSnapshotMissing, enabledCompatibility.Kind);
        Assert.True(enabledCompatibility.RequiresReplan);
    }

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"copilot-environment-context-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
