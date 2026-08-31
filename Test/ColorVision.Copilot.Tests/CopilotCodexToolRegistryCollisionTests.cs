using ColorVision.Copilot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotCodexToolRegistryCollisionTests
{
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
    public void RelaxedPolicyTreatsTrimmedNamesAsTheSameCapability()
    {
        var events = new List<CopilotAgentEvent>();
        var first = new RecordingTool("CollisionProbe");
        var duplicate = new RecordingTool(" CollisionProbe ");

        var merged = CopilotMicrosoftAgentFrameworkRuntime.MergeAvailableTools(
            CreateAgentRequest(errorOnToolCollisions: false),
            [first],
            [duplicate],
            events.Add);

        Assert.Same(first, Assert.Single(merged));
        Assert.Single(events);
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
    public void ProviderToolSurfaceUsesCanonicalOrderIndependentOfRegistration()
    {
        var alpha = new RecordingTool("AlphaProbe");
        var mike = new RecordingTool("MikeProbe");
        var zulu = new RecordingTool("ZuluProbe");

        var first = CopilotMicrosoftAgentFrameworkRuntime.MergeAvailableTools(
            CreateAgentRequest(errorOnToolCollisions: false),
            [zulu, alpha],
            [mike],
            _ => { });
        var second = CopilotMicrosoftAgentFrameworkRuntime.MergeAvailableTools(
            CreateAgentRequest(errorOnToolCollisions: false),
            [alpha, zulu],
            [mike],
            _ => { });

        Assert.Equal(["AlphaProbe", "MikeProbe", "ZuluProbe"], first.Select(tool => tool.Name));
        Assert.Equal(first.Select(tool => tool.Name), second.Select(tool => tool.Name));
    }

    [Fact]
    public void ProviderFunctionNamesAreBoundedAndCollisionSafe()
    {
        string longToolName = new('A', 100);
        string[] toolNames = ["FooBar", "Foo_Bar", longToolName];

        var first = CopilotMicrosoftAgentFrameworkRuntime.HarnessToolBridge.BuildFunctionNameMap(toolNames);
        var second = CopilotMicrosoftAgentFrameworkRuntime.HarnessToolBridge.BuildFunctionNameMap(toolNames.Reverse());

        Assert.Equal("colorvision_read_local_file", CopilotMicrosoftAgentFrameworkRuntime.HarnessToolBridge.ToFunctionName("ReadLocalFile"));
        Assert.Equal(toolNames.Length, first.Values.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(first.Values, functionName => Assert.InRange(functionName.Length, 1, 64));
        Assert.NotEqual(first["FooBar"], first["Foo_Bar"]);
        Assert.Equal(first["FooBar"], second["FooBar"]);
        Assert.Equal(first["Foo_Bar"], second["Foo_Bar"]);
        Assert.Equal(first[longToolName], second[longToolName]);
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
