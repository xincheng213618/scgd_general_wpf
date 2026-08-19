using ColorVision.Copilot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotCodexSandboxModeTests
{
    [Fact]
    public void ClosestTrustedReadOnlyValueIsFrozenIntoSubmittedAndQueuedTurnSnapshots()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                $"""
                sandbox_mode = "workspace-write"

                [projects.'{projectRoot}']
                trust_level = "trusted"
                """);
            string projectConfigDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(projectConfigDirectory);
            string projectConfigPath = Path.Combine(projectConfigDirectory, "config.toml");
            File.WriteAllText(projectConfigPath, "sandbox_mode = \"read-only\"");
            string requestText = $"Edit the workspace source directory {projectRoot} and run its tests.";

            var submittedContext = new CopilotAgentHostContextSnapshot(
                activeDocumentPath: null,
                projectRoot,
                attachments: null,
                liveContext: null,
                conversationHistory: null,
                additionalReadRootPaths: null,
                globalInstructionRootPath: globalRoot);
            var submittedPlan = CopilotAgentRequestFactory.Prepare(
                requestText,
                CopilotAgentMode.Code,
                submittedContext);
            var submittedRequest = CopilotAgentRequestFactory.Create(
                submittedPlan,
                new CopilotAgentRequestBuildInput
                {
                    Profile = CopilotProfileConfig.CreateDefault(),
                    AgentDefaults = new CopilotAgentDefaultsConfig(),
                });
            File.WriteAllText(projectConfigPath, "sandbox_mode = \"workspace-write\"");
            var refreshedContext = new CopilotAgentHostContextSnapshot(
                activeDocumentPath: null,
                projectRoot,
                attachments: null,
                liveContext: null,
                conversationHistory: null,
                additionalReadRootPaths: null,
                globalInstructionRootPath: globalRoot);
            var refreshedPlan = CopilotAgentRequestFactory.Prepare(
                requestText,
                CopilotAgentMode.Code,
                refreshedContext);
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

            var submittedOptions = submittedContext.ProjectInstructionDiscoveryOptions;
            Assert.True(submittedOptions.HasSandboxModeOverride);
            Assert.Equal(CopilotCodexSandboxMode.ReadOnly, submittedOptions.ConfiguredSandboxMode);
            Assert.Equal(CopilotProjectInstructionConfigSources.TrustedProject, submittedOptions.SandboxModeSource);
            Assert.Equal(CopilotCodexSandboxMode.ReadOnly, submittedPlan.CodexSandboxMode);
            Assert.Equal(CopilotCodexSandboxMode.ReadOnly, submittedRequest.CodexSandboxMode);
            Assert.Empty(submittedPlan.WritableLocalRootPaths);
            Assert.Empty(submittedPlan.WritableLocalFilePaths);
            Assert.Empty(submittedRequest.WritableLocalRootPaths);
            Assert.Empty(submittedRequest.WritableLocalFilePaths);
            Assert.Contains(
                submittedPlan.ReadableLocalDirectoryPaths,
                path => string.Equals(path, projectRoot, StringComparison.OrdinalIgnoreCase));
            Assert.Equal(
                CopilotCodexSandboxMode.WorkspaceWrite,
                refreshedContext.ProjectInstructionDiscoveryOptions.ConfiguredSandboxMode);
            Assert.NotEmpty(refreshedPlan.WritableLocalRootPaths);
            Assert.Equal(
                CopilotCodexSandboxMode.ReadOnly,
                queuedContext.ProjectInstructionDiscoveryOptions.ConfiguredSandboxMode);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void UntrustedAndInvalidValuesCannotBroadenTheCodexHomeReadOnlyBoundary()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                $"""
                sandbox_mode = "read-only"

                [projects.'{projectRoot}']
                trust_level = "untrusted"
                """);
            string projectConfigDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(projectConfigDirectory);
            File.WriteAllText(
                Path.Combine(projectConfigDirectory, "config.toml"),
                "sandbox_mode = \"danger-full-access\"");

            var untrusted = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot, projectRoot);

            Assert.Equal(CopilotCodexSandboxMode.ReadOnly, untrusted.ConfiguredSandboxMode);
            Assert.True(untrusted.HasSandboxModeOverride);
            Assert.Equal(CopilotProjectInstructionConfigSources.CodexHome, untrusted.SandboxModeSource);
            Assert.Empty(untrusted.AppliedProjectConfigFilePaths);

            File.WriteAllText(Path.Combine(globalRoot, "config.toml"), "sandbox_mode = \"unknown\"");
            var invalid = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot);

            Assert.False(invalid.HasSandboxModeOverride);
            Assert.Equal(CopilotCodexSandboxMode.Unspecified, invalid.ConfiguredSandboxMode);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ReadOnlySnapshotHidesWriteToolsAndRejectsInjectedCallsBeforeExecution()
    {
        var readTool = new RecordingTool("ReadProbe", CopilotToolAccess.ReadOnly);
        var writeTool = new RecordingTool("WriteProbe", CopilotToolAccess.Write);
        var registry = new CopilotToolRegistry([readTool, writeTool]);
        var request = new CopilotAgentRequest
        {
            Mode = CopilotAgentMode.Code,
            UserText = "Edit the workspace source file and run its tests.",
            TaskIntentText = "Edit the workspace source file and run its tests.",
            CodexSandboxMode = CopilotCodexSandboxMode.ReadOnly,
            WritableLocalRootPaths = [Path.GetTempPath()],
        };

        var availableTools = registry.FindTools(request);
        var outcome = await new CopilotToolExecutor(Array.Empty<ICopilotToolExecutionHook>()).ExecuteAsync(
            new CopilotToolInvocation
            {
                CallId = "stale-write-call",
                Round = 1,
                RuntimeName = "codex-sandbox-test",
                Tool = writeTool,
                AgentRequest = request,
                ToolInput = CopilotAgentToolInput.Empty,
            },
            _ => { },
            CancellationToken.None);
        string harness = CopilotMicrosoftAgentFrameworkRuntime.BuildHarnessInstructions(
            request,
            availableTools,
            new CopilotAgentEnvironmentContext(),
            taskLedgerEnabled: false,
            agentModeEnabled: true);

        Assert.Contains(availableTools, tool => tool.Name == readTool.Name);
        Assert.DoesNotContain(availableTools, tool => tool.Name == writeTool.Name);
        Assert.False(CopilotToolRegistry.IsAllowedForCodexSandboxPolicy(writeTool, request));
        Assert.Equal(CopilotToolExecutionState.Denied, outcome.Execution.State);
        Assert.Equal(CopilotToolFailureKind.Authorization, outcome.Result.FailureKind);
        Assert.Equal("codex_read_only_sandbox", outcome.Result.FailureCode);
        Assert.Equal(0, writeTool.ExecutionCount);
        Assert.Contains("sandbox_mode=read-only is frozen", harness, StringComparison.Ordinal);
        Assert.Contains("Never request write approval", harness, StringComparison.Ordinal);
    }

    [Fact]
    public void BroaderCodexModesDoNotGrantBeyondNativeModeAndDiagnosticsSaySo()
    {
        var writeTool = new RecordingTool("WriteProbe", CopilotToolAccess.Write);
        var registry = new CopilotToolRegistry([writeTool]);
        foreach (var sandboxMode in new[]
        {
            CopilotCodexSandboxMode.WorkspaceWrite,
            CopilotCodexSandboxMode.DangerFullAccess,
        })
        {
            var planRequest = new CopilotAgentRequest
            {
                Mode = CopilotAgentMode.Plan,
                UserText = "Edit the workspace source file.",
                CodexSandboxMode = sandboxMode,
                WritableLocalRootPaths = [Path.GetTempPath()],
            };
            var explicitReadOnlyRequest = new CopilotAgentRequest
            {
                Mode = CopilotAgentMode.Code,
                UserText = "Read-only inspection; do not edit any file.",
                CodexSandboxMode = sandboxMode,
                WritableLocalRootPaths = [Path.GetTempPath()],
            };

            Assert.Empty(registry.FindTools(planRequest));
            Assert.Empty(registry.FindTools(explicitReadOnlyRequest));
        }

        var options = CopilotProjectInstructionDiscoveryConfig.CreateDefault() with
        {
            ConfiguredSandboxMode = CopilotCodexSandboxMode.DangerFullAccess,
            HasSandboxModeOverride = true,
            SandboxModeSource = CopilotProjectInstructionConfigSources.CodexHome,
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
            CodexSandboxMode = CopilotCodexSandboxMode.DangerFullAccess,
            HasCodexSandboxModeOverride = true,
            CodexSandboxModeSourceLabel = options.SandboxModeSourceLabel,
        });
        string debugReport = CopilotEffectiveConfigDiagnostics.Format(
            new CopilotEffectiveConfigDiagnosticContext
            {
                Config = new CopilotConfig(),
                State = new CopilotChatState(),
                ComposerMode = CopilotAgentMode.Code,
                CodexConfigOptions = options,
            });

        Assert.Contains("Codex sandbox_mode：danger-full-access", memoryReport, StringComparison.Ordinal);
        Assert.Contains("不映射为提权", memoryReport, StringComparison.Ordinal);
        Assert.Contains("执行沙箱：danger-full-access", contextReport, StringComparison.Ordinal);
        Assert.Contains("原生访问与审批边界", contextReport, StringComparison.Ordinal);
        Assert.Contains("Codex sandbox_mode：danger-full-access", debugReport, StringComparison.Ordinal);
        Assert.Contains("不映射为提权", debugReport, StringComparison.Ordinal);
    }

    private sealed class RecordingTool : ICopilotTool
    {
        private int _executionCount;

        public RecordingTool(string name, CopilotToolAccess access)
        {
            Name = name;
            Capability = access == CopilotToolAccess.ReadOnly
                ? CopilotToolCapabilityDescriptor.ReadOnly()
                : new CopilotToolCapabilityDescriptor
                {
                    Access = CopilotToolAccess.Write,
                    RiskLevel = CopilotToolRiskLevel.Medium,
                    ApprovalMode = CopilotToolApprovalMode.Never,
                    Idempotency = CopilotToolIdempotency.NonIdempotent,
                    ConcurrencyMode = CopilotToolConcurrencyMode.Exclusive,
                    EvidenceMode = CopilotToolEvidenceMode.None,
                };
        }

        public int ExecutionCount => Volatile.Read(ref _executionCount);

        public string Name { get; }

        public string Description => "Records whether the sandbox test reached tool execution.";

        public CopilotToolCapabilityDescriptor Capability { get; }

        public bool CanHandle(CopilotAgentRequest request) => true;

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _executionCount);
            return Task.FromResult(new CopilotToolResult
            {
                ToolName = Name,
                Success = true,
                Summary = "Tool executed.",
            });
        }
    }

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"copilot-sandbox-mode-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
