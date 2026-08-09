using ColorVision.Copilot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.UI.Tests;

public sealed class CopilotCodexToolRegistryCollisionTests
{
    [Fact]
    public void ClosestTrustedValueIsFrozenAcrossContextPlanRequestAndQueuedFollowUp()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                $"""
                [features.tool_registry]
                error_on_tool_collisions = false

                [projects.'{projectRoot}']
                trust_level = "trusted"
                """);
            string projectConfigDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(projectConfigDirectory);
            string projectConfigPath = Path.Combine(projectConfigDirectory, "config.toml");
            File.WriteAllText(
                projectConfigPath,
                "[features.tool_registry]" + Environment.NewLine + "error_on_tool_collisions = true");

            var submittedContext = CreateHostContext(globalRoot, projectRoot);
            var submittedPlan = CopilotAgentRequestFactory.Prepare(
                "Inspect the active tool surface.",
                CopilotAgentMode.Code,
                submittedContext);
            var submittedRequest = CopilotAgentRequestFactory.Create(
                submittedPlan,
                new CopilotAgentRequestBuildInput
                {
                    Profile = CopilotProfileConfig.CreateDefault(),
                    AgentDefaults = new CopilotAgentDefaultsConfig(),
                });
            File.WriteAllText(
                projectConfigPath,
                "[features.tool_registry]" + Environment.NewLine + "error_on_tool_collisions = false");
            var refreshed = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot, projectRoot);
            var queuedFollowUp = new CopilotQueuedFollowUp(
                "run-1",
                "conversation-1",
                "Conversation",
                "Continue inspecting the tool surface.",
                CopilotAgentMode.Code,
                CopilotProfileConfig.CreateDefault(),
                submittedContext);
            var queuedContext = queuedFollowUp.CreateExecutionContext(
                CopilotConversationHistorySnapshot.Empty);
            var options = submittedContext.ProjectInstructionDiscoveryOptions;

            Assert.True(options.ConfiguredErrorOnToolCollisions);
            Assert.True(options.HasErrorOnToolCollisionsOverride);
            Assert.Equal(
                CopilotProjectInstructionConfigSources.TrustedProject,
                options.ErrorOnToolCollisionsSource);
            Assert.True(submittedPlan.CodexErrorOnToolCollisions);
            Assert.True(submittedRequest.CodexErrorOnToolCollisions);
            Assert.True(queuedContext.ProjectInstructionDiscoveryOptions.ConfiguredErrorOnToolCollisions);
            Assert.False(refreshed.ConfiguredErrorOnToolCollisions);
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
                [features.tool_registry]
                error_on_tool_collisions = false

                [projects.'{projectRoot}']
                trust_level = "untrusted"
                """);
            string projectConfigDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(projectConfigDirectory);
            File.WriteAllText(
                Path.Combine(projectConfigDirectory, "config.toml"),
                "[features.tool_registry]" + Environment.NewLine + "error_on_tool_collisions = true");

            var untrusted = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot, projectRoot);

            Assert.False(untrusted.ConfiguredErrorOnToolCollisions);
            Assert.True(untrusted.HasErrorOnToolCollisionsOverride);
            Assert.Equal(
                CopilotProjectInstructionConfigSources.CodexHome,
                untrusted.ErrorOnToolCollisionsSource);
            Assert.Empty(untrusted.AppliedProjectConfigFilePaths);

            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                "[features.tool_registry]" + Environment.NewLine + "error_on_tool_collisions = \"true\"");
            var invalid = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot);

            Assert.False(invalid.ConfiguredErrorOnToolCollisions);
            Assert.False(invalid.HasErrorOnToolCollisionsOverride);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void RelaxedPolicyKeepsFirstToolAndEmitsDuplicateDiagnostic()
    {
        var events = new List<CopilotAgentEvent>();
        var first = new RecordingTool("CollisionProbe");
        var duplicate = new RecordingTool("collisionprobe");

        var merged = CopilotMicrosoftAgentFrameworkRuntime.MergeAvailableTools(
            CreateAgentRequest(errorOnToolCollisions: false),
            [first],
            [duplicate],
            events.Add);

        Assert.Same(first, Assert.Single(merged));
        var diagnostic = Assert.Single(events);
        Assert.Equal(CopilotAgentEventType.RuntimeDiagnostic, diagnostic.Type);
        Assert.Contains("skipped duplicate tool name collisionprobe", diagnostic.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void StrictPolicyRejectsDuplicateBeforeTheToolSurfaceCanBeUsed()
    {
        var events = new List<CopilotAgentEvent>();
        var exception = Assert.Throws<InvalidOperationException>(() =>
            CopilotMicrosoftAgentFrameworkRuntime.MergeAvailableTools(
                CreateAgentRequest(errorOnToolCollisions: true),
                [new RecordingTool("CollisionProbe")],
                [new RecordingTool("collisionprobe")],
                events.Add));

        Assert.Equal("duplicate tool: functions.collisionprobe", exception.Message);
        Assert.Empty(events);
    }

    [Fact]
    public void DiagnosticsExposeTheFrozenCollisionPolicy()
    {
        var options = CopilotProjectInstructionDiscoveryConfig.CreateDefault() with
        {
            ConfiguredErrorOnToolCollisions = true,
            HasErrorOnToolCollisionsOverride = true,
            ErrorOnToolCollisionsSource = CopilotProjectInstructionConfigSources.CodexHome,
        };
        string instructionReport = CopilotProjectInstructionDiagnostics.Format(
            new CopilotProjectInstructionSnapshot(
                string.Empty,
                string.Empty,
                string.Empty,
                options,
                Array.Empty<CopilotProjectInstructionDocument>()),
            hasActiveAgentRun: false);
        string debugReport = CopilotEffectiveConfigDiagnostics.Format(
            new CopilotEffectiveConfigDiagnosticContext
            {
                Config = new CopilotConfig(),
                State = new CopilotChatState(),
                ComposerMode = CopilotAgentMode.Code,
                CodexConfigOptions = options,
            });

        Assert.Contains("features.tool_registry.error_on_tool_collisions：true", instructionReport, StringComparison.Ordinal);
        Assert.Contains("模型请求前终止本轮", instructionReport, StringComparison.Ordinal);
        Assert.Contains("features.tool_registry.error_on_tool_collisions：true", debugReport, StringComparison.Ordinal);
        Assert.Contains(options.ErrorOnToolCollisionsSourceLabel, debugReport, StringComparison.Ordinal);
    }

    private static CopilotAgentHostContextSnapshot CreateHostContext(
        string globalRoot,
        string projectRoot) => new(
        activeDocumentPath: null,
        projectRoot,
        attachments: null,
        liveContext: null,
        conversationHistory: null,
        additionalReadRootPaths: null,
        globalInstructionRootPath: globalRoot);

    private static CopilotAgentRequest CreateAgentRequest(bool errorOnToolCollisions) => new()
    {
        Profile = CopilotProfileConfig.CreateDefault(),
        ConversationId = "tool-registry-collision-conversation",
        TaskId = "tool-registry-collision-task",
        UserText = "Inspect the active tool surface.",
        TaskIntentText = "Inspect the active tool surface.",
        Mode = CopilotAgentMode.Code,
        CodexErrorOnToolCollisions = errorOnToolCollisions,
    };

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"copilot-codex-tool-registry-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class RecordingTool(string name) : ICopilotTool
    {
        public string Name { get; } = name;

        public string Description => "A read-only tool used to verify tool registry collision handling.";

        public CopilotToolCapabilityDescriptor Capability { get; } =
            CopilotToolCapabilityDescriptor.ReadOnly();

        public bool CanHandle(CopilotAgentRequest request) => true;

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new CopilotToolResult
            {
                ToolName = Name,
                Success = true,
                Summary = "Recording tool executed.",
            });
        }
    }
}
