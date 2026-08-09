using ColorVision.Copilot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.UI.Tests;

public sealed class CopilotCodexInterruptMessageTests
{
    [Fact]
    public void ClosestTrustedValueIsFrozenWhileUntrustedAndInvalidValuesAreIgnored()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                $"""
                [agents]
                interrupt_message = true

                [projects.'{projectRoot}']
                trust_level = "trusted"
                """);
            string projectConfigDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(projectConfigDirectory);
            string projectConfigPath = Path.Combine(projectConfigDirectory, "config.toml");
            File.WriteAllText(projectConfigPath, "agents.interrupt_message = false");

            var submittedContext = new CopilotAgentHostContextSnapshot(
                activeDocumentPath: null,
                projectRoot,
                attachments: null,
                liveContext: null,
                conversationHistory: null,
                additionalReadRootPaths: null,
                globalInstructionRootPath: globalRoot);
            var submittedPlan = CopilotAgentRequestFactory.Prepare(
                "Delegate a bounded workspace investigation.",
                CopilotAgentMode.Auto,
                submittedContext);
            var submittedRequest = CopilotAgentRequestFactory.Create(
                submittedPlan,
                new CopilotAgentRequestBuildInput
                {
                    Profile = CopilotProfileConfig.CreateDefault(),
                    AgentDefaults = new CopilotAgentDefaultsConfig(),
                });
            File.WriteAllText(projectConfigPath, "agents.interrupt_message = true");
            var childRequest = CopilotSubagentRunner.CreateChildRequest(
                submittedRequest,
                CopilotSubagentRoleCatalog.Default.GetRequired(CopilotSubagentRoleCatalog.ExploreRoleId),
                new CopilotSubagentRunRequest
                {
                    RunId = "interrupt-config-child",
                    Task = "Inspect the bounded workspace evidence.",
                    RequestTokenBudget = 16_384,
                });

            Assert.False(submittedContext.ProjectInstructionDiscoveryOptions.ConfiguredInterruptMessageEnabled);
            Assert.True(submittedContext.ProjectInstructionDiscoveryOptions.HasInterruptMessageOverride);
            Assert.Equal(
                CopilotProjectInstructionConfigSources.TrustedProject,
                submittedContext.ProjectInstructionDiscoveryOptions.InterruptMessageSource);
            Assert.False(submittedPlan.CodexInterruptMessageEnabled);
            Assert.False(submittedRequest.CodexInterruptMessageEnabled);
            Assert.False(childRequest.CodexInterruptMessageEnabled);

            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                $"""
                [agents]
                interrupt_message = true

                [projects.'{projectRoot}']
                trust_level = "untrusted"
                """);
            File.WriteAllText(projectConfigPath, "agents.interrupt_message = false");
            var untrusted = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot, projectRoot);

            Assert.True(untrusted.ConfiguredInterruptMessageEnabled);
            Assert.Equal(
                CopilotProjectInstructionConfigSources.CodexHome,
                untrusted.InterruptMessageSource);
            Assert.Empty(untrusted.AppliedProjectConfigFilePaths);

            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                "[agents]\ninterrupt_message = \"false\"");
            var invalid = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot);

            Assert.True(invalid.ConfiguredInterruptMessageEnabled);
            Assert.False(invalid.HasInterruptMessageOverride);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ActualCancellationKeepsLocalAuditButHonorsModelVisibility(bool interruptMessageEnabled)
    {
        var runner = new BlockingSubagentRunner();
        var tool = new CopilotDelegateExploreTool(runner);
        var request = new CopilotAgentRequest
        {
            ConversationId = "interrupt-message-" + Guid.NewGuid().ToString("N"),
            UserText = "Delegate a bounded workspace investigation.",
            TaskIntentText = "Delegate a bounded workspace investigation.",
            Profile = CopilotProfileConfig.CreateDefault(),
            CodexInterruptMessageEnabled = interruptMessageEnabled,
        };
        var progress = new CopilotToolProgressContext();
        using var testCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        Task<CopilotToolResult> executionTask = tool.ExecuteWithProgressAsync(
            request,
            new CopilotAgentToolInput
            {
                Arguments = new Dictionary<string, object?>
                {
                    ["task"] = "Inspect the bounded workspace evidence.",
                },
            },
            progress,
            testCancellation.Token);
        await runner.Started.Task.WaitAsync(testCancellation.Token);
        string runId = Assert.IsType<CopilotDelegatedRunProgress>(
            progress.LatestSnapshot?.DelegatedRun).RunId;

        Assert.Equal(
            CopilotSubagentCancelResult.Requested,
            CopilotSubagentCoordination.RequestCancelActiveRun(request.ConversationId, runId));
        CopilotToolResult result = await executionTask.WaitAsync(testCancellation.Token);
        var outcome = new CopilotToolExecutionOutcome
        {
            Invocation = new CopilotToolInvocation
            {
                CallId = "interrupt-message-call",
                Round = 1,
                Tool = tool,
                AgentRequest = request,
                ToolCall = new CopilotToolCall { ToolName = tool.Name },
            },
            Result = result,
            Execution = new CopilotToolExecutionInfo
            {
                CallId = "interrupt-message-call",
                Round = 1,
                ToolName = tool.Name,
                State = CopilotToolExecutionState.Cancelled,
                FailureKind = CopilotToolFailureKind.Cancelled,
            },
        };
        string modelOutput = CopilotFrameworkToolResultFormatter.Format(outcome);
        string recoveryObservations = new CopilotAgentContextBuilder().BuildObservationSummary(
            [outcome.StepRecord],
            maxSteps: 4,
            maxContentChars: 2_000,
            includeContent: true);

        Assert.False(result.Success);
        Assert.Equal(CopilotToolFailureKind.Cancelled, result.FailureKind);
        Assert.Equal(CopilotAgentStopReason.Cancelled, result.DelegatedRunUsage?.StopReason);
        Assert.Equal(
            result.DelegatedRunUsage?.RequestTokenBudget,
            result.DelegatedRunUsage?.ConsumedTokens);
        Assert.True(result.DelegatedRunUsage?.UsedEstimatedUsage);
        Assert.NotEmpty(result.Summary);
        Assert.NotEmpty(result.ErrorMessage);
        Assert.Equal(!interruptMessageEnabled, result.SuppressModelOutput);
        if (interruptMessageEnabled)
        {
            Assert.Contains("stopped by the user", modelOutput, StringComparison.Ordinal);
            Assert.Contains("\"includes_estimates\":true", modelOutput, StringComparison.Ordinal);
            Assert.Contains(tool.Name, recoveryObservations, StringComparison.Ordinal);
        }
        else
        {
            Assert.Equal(string.Empty, modelOutput);
            Assert.Equal("- None", recoveryObservations);
        }
    }

    [Fact]
    public void DiagnosticsExposeInterruptMessageValueSourceAndBoundary()
    {
        var options = CopilotProjectInstructionDiscoveryConfig.CreateDefault() with
        {
            ConfiguredInterruptMessageEnabled = false,
            HasInterruptMessageOverride = true,
            InterruptMessageSource = CopilotProjectInstructionConfigSources.TrustedProject,
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
            CodexInterruptMessageEnabled = false,
            HasCodexInterruptMessageOverride = true,
            CodexInterruptMessageSourceLabel = options.InterruptMessageSourceLabel,
        });
        string debugReport = CopilotEffectiveConfigDiagnostics.Format(
            new CopilotEffectiveConfigDiagnosticContext
            {
                Config = new CopilotConfig(),
                State = new CopilotChatState(),
                ComposerMode = CopilotAgentMode.Code,
                CodexConfigOptions = options,
            });

        Assert.Contains("Codex agents.interrupt_message：false", memoryReport, StringComparison.Ordinal);
        Assert.Contains(options.InterruptMessageSourceLabel, memoryReport, StringComparison.Ordinal);
        Assert.Contains("模型工具输出为空", memoryReport, StringComparison.Ordinal);
        Assert.Contains("子代理中断消息：仅保留本地审计", contextReport, StringComparison.Ordinal);
        Assert.Contains("Codex agents.interrupt_message：false", debugReport, StringComparison.Ordinal);
        Assert.Contains("UI、事件与审计仍保留", debugReport, StringComparison.Ordinal);
    }

    private sealed class BlockingSubagentRunner : ICopilotSubagentRunner
    {
        public TaskCompletionSource Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<CopilotSubagentResult> RunAsync(
            CopilotAgentRequest parentRequest,
            CopilotSubagentRoleDescriptor role,
            CopilotSubagentRunRequest runRequest,
            CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new CopilotSubagentResult();
        }
    }

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"copilot-interrupt-message-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
