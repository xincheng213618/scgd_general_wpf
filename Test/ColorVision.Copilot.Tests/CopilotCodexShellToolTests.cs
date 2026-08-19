using ColorVision.Copilot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotCodexShellToolTests
{
    [Fact]
    public void ClosestTrustedValueIsFrozenIntoSubmittedAndQueuedTurnSnapshots()
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
                shell_tool = true

                [projects.'{projectRoot}']
                trust_level = "trusted"
                """);
            string projectConfigDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(projectConfigDirectory);
            string projectConfigPath = Path.Combine(projectConfigDirectory, "config.toml");
            File.WriteAllText(projectConfigPath, "features.shell_tool = false");

            var submittedContext = new CopilotAgentHostContextSnapshot(
                activeDocumentPath: null,
                projectRoot,
                attachments: null,
                liveContext: null,
                conversationHistory: null,
                additionalReadRootPaths: null,
                globalInstructionRootPath: globalRoot);
            var submittedPlan = CopilotAgentRequestFactory.Prepare(
                "Run the workspace tests.",
                CopilotAgentMode.Code,
                submittedContext);
            var submittedRequest = CopilotAgentRequestFactory.Create(
                submittedPlan,
                new CopilotAgentRequestBuildInput
                {
                    Profile = CopilotProfileConfig.CreateDefault(),
                    AgentDefaults = new CopilotAgentDefaultsConfig(),
                });
            File.WriteAllText(projectConfigPath, "features.shell_tool = true");
            var refreshed = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot, projectRoot);
            var queuedFollowUp = new CopilotQueuedFollowUp(
                "run-1",
                "conversation-1",
                "Conversation",
                "Continue the work.",
                CopilotAgentMode.Code,
                CopilotProfileConfig.CreateDefault(),
                submittedContext);
            var queuedContext = queuedFollowUp.CreateExecutionContext(
                CopilotConversationHistorySnapshot.Empty);

            var submitted = submittedContext.ProjectInstructionDiscoveryOptions;
            Assert.False(submitted.ConfiguredShellToolEnabled);
            Assert.True(submitted.HasShellToolEnabledOverride);
            Assert.Equal(
                CopilotProjectInstructionConfigSources.TrustedProject,
                submitted.ShellToolEnabledSource);
            Assert.False(submittedPlan.CodexShellToolEnabled);
            Assert.False(submittedRequest.CodexShellToolEnabled);
            Assert.True(refreshed.ConfiguredShellToolEnabled);
            Assert.False(queuedContext.ProjectInstructionDiscoveryOptions.ConfiguredShellToolEnabled);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void UntrustedAndInvalidValuesCannotBroadenTheCodexHomeContract()
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
                shell_tool = false

                [projects.'{projectRoot}']
                trust_level = "untrusted"
                """);
            string projectConfigDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(projectConfigDirectory);
            File.WriteAllText(
                Path.Combine(projectConfigDirectory, "config.toml"),
                "[features]\nshell_tool = true");

            var untrusted = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot, projectRoot);

            Assert.False(untrusted.ConfiguredShellToolEnabled);
            Assert.True(untrusted.HasShellToolEnabledOverride);
            Assert.Equal(
                CopilotProjectInstructionConfigSources.CodexHome,
                untrusted.ShellToolEnabledSource);
            Assert.Empty(untrusted.AppliedProjectConfigFilePaths);

            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                "[features]\nshell_tool = \"false\"");
            var invalid = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot);

            Assert.True(invalid.ConfiguredShellToolEnabled);
            Assert.False(invalid.HasShellToolEnabledOverride);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public async Task DisabledSnapshotHidesShellStartsAndRejectsInjectedCallsBeforeApproval()
    {
        var shellTool = new CopilotShellCommandTool();
        var backgroundTool = new CopilotStartBackgroundShellCommandTool();
        var fixedDiagnostic = new CopilotInspectWindowsSystemTool();
        var request = new CopilotAgentRequest
        {
            Profile = CopilotProfileConfig.CreateDefault(),
            Mode = CopilotAgentMode.Code,
            UserText = "Run a PowerShell command to print the current directory.",
            TaskIntentText = "Run a PowerShell command to print the current directory.",
            CodexShellToolEnabled = false,
        };
        var registry = new CopilotToolRegistry([shellTool, backgroundTool, fixedDiagnostic]);

        var availableTools = registry.FindTools(request);
        var outcome = await new CopilotToolExecutor(Array.Empty<ICopilotToolExecutionHook>()).ExecuteAsync(
            new CopilotToolInvocation
            {
                CallId = "stale-shell-call",
                Round = 1,
                RuntimeName = "codex-shell-tool-test",
                Tool = shellTool,
                AgentRequest = request,
                ToolInput = new CopilotAgentToolInput
                {
                    Arguments = new Dictionary<string, object?>
                    {
                        ["command"] = "Get-Location",
                    },
                },
            },
            _ => { },
            CancellationToken.None);
        string prompt = new CopilotAgentContextBuilder().BuildPreparedUserMessageContent(
            request,
            Array.Empty<CopilotToolResult>());

        Assert.DoesNotContain(availableTools, tool => tool is CopilotShellCommandTool);
        Assert.False(CopilotToolRegistry.IsAllowedForCodexShellToolPolicy(shellTool, request));
        Assert.False(CopilotToolRegistry.IsAllowedForCodexShellToolPolicy(backgroundTool, request));
        Assert.True(CopilotToolRegistry.IsAllowedForCodexShellToolPolicy(fixedDiagnostic, request));
        Assert.Equal(CopilotToolExecutionState.Denied, outcome.Execution.State);
        Assert.Equal(CopilotToolFailureKind.Authorization, outcome.Result.FailureKind);
        Assert.Equal("codex_shell_tool_disabled", outcome.Result.FailureCode);
        Assert.Contains("features.shell_tool=false applies", prompt, StringComparison.Ordinal);
        Assert.Contains("do not claim that a command or script was executed", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void DiagnosticsExposeTheEffectiveValueSourceAndFailClosedBoundary()
    {
        var options = CopilotProjectInstructionDiscoveryConfig.CreateDefault() with
        {
            ConfiguredShellToolEnabled = false,
            HasShellToolEnabledOverride = true,
            ShellToolEnabledSource = CopilotProjectInstructionConfigSources.CodexHome,
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
            CodexShellToolEnabled = false,
            HasCodexShellToolEnabledOverride = true,
            CodexShellToolEnabledSourceLabel = options.ShellToolEnabledSourceLabel,
        });
        string debugReport = CopilotEffectiveConfigDiagnostics.Format(
            new CopilotEffectiveConfigDiagnosticContext
            {
                Config = new CopilotConfig(),
                State = new CopilotChatState(),
                ComposerMode = CopilotAgentMode.Code,
                CodexConfigOptions = options,
            });

        Assert.Contains("Codex features.shell_tool：false", memoryReport, StringComparison.Ordinal);
        Assert.Contains(options.ShellToolEnabledSourceLabel, memoryReport, StringComparison.Ordinal);
        Assert.Contains("拒绝旧计划", memoryReport, StringComparison.Ordinal);
        Assert.Contains("命令工具：关闭", contextReport, StringComparison.Ordinal);
        Assert.Contains("旧调用也会拒绝", contextReport, StringComparison.Ordinal);
        Assert.Contains("Codex features.shell_tool：false", debugReport, StringComparison.Ordinal);
        Assert.Contains("注入调用", debugReport, StringComparison.Ordinal);
    }

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"copilot-shell-tool-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
