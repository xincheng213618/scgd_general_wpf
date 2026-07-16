#pragma warning disable CA1707
using ColorVision.Engine.Templates.Flow;
using ColorVision.UI;

namespace ColorVision.UI.Tests;

public sealed class CopilotFlowContextProviderTests
{
    [Fact]
    public async Task CaptureAsync_ReadsTheCurrentFlowOnEveryRequestInsteadOfCachingSelection()
    {
        var flowName = "Flow A";
        var captureCount = 0;
        var provider = new CopilotFlowContextProvider(
            _ =>
            {
                captureCount++;
                return Task.FromResult<CopilotFlowContextSnapshot?>(CreateSnapshot(flowName));
            });
        var request = new CopilotContextRequest
        {
            Scope = CopilotContextScope.Agent,
            UserText = "检查当前流程",
        };

        var first = await provider.CaptureAsync(request, CancellationToken.None);
        flowName = "Flow B";
        var second = await provider.CaptureAsync(request, CancellationToken.None);

        Assert.Equal(2, captureCount);
        Assert.Contains("Flow A", first?.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("Flow A", second?.Content, StringComparison.Ordinal);
        Assert.Contains("Flow B", second?.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CaptureAsync_SkipsUnrelatedAgentTurnsButKeepsDiagnoseAndActiveSurfaceFollowUps()
    {
        var captureCount = 0;
        var currentSurface = false;
        var provider = new CopilotFlowContextProvider(
            _ =>
            {
                captureCount++;
                return Task.FromResult<CopilotFlowContextSnapshot?>(CreateSnapshot("Inspection"));
            },
            isCurrentSurface: () => currentSurface);

        var unrelated = await provider.CaptureAsync(
            new CopilotContextRequest { Scope = CopilotContextScope.Agent, UserText = "解释这个设置" },
            CancellationToken.None);
        currentSurface = true;
        var followUp = await provider.CaptureAsync(
            new CopilotContextRequest { Scope = CopilotContextScope.Agent, UserText = "现在呢" },
            CancellationToken.None);
        currentSurface = false;
        var diagnose = await provider.CaptureAsync(
            new CopilotContextRequest { Scope = CopilotContextScope.Diagnose, UserText = "帮我分析" },
            CancellationToken.None);

        Assert.Null(unrelated);
        Assert.NotNull(followUp);
        Assert.NotNull(diagnose);
        Assert.Equal(2, captureCount);
    }

    [Fact]
    public async Task CaptureAsync_DropsEmptyOrInactiveFlowState()
    {
        var active = true;
        var captureCount = 0;
        var provider = new CopilotFlowContextProvider(
            _ =>
            {
                captureCount++;
                return Task.FromResult<CopilotFlowContextSnapshot?>(new CopilotFlowContextSnapshot { Status = "Ready" });
            },
            () => active);
        var request = new CopilotContextRequest { Scope = CopilotContextScope.Diagnose, UserText = "diagnose" };

        var empty = await provider.CaptureAsync(request, CancellationToken.None);
        active = false;
        var inactive = await provider.CaptureAsync(request, CancellationToken.None);

        Assert.Null(empty);
        Assert.Null(inactive);
        Assert.Equal(1, captureCount);
        Assert.False(provider.CanProvide(CopilotContextScope.Agent));
    }

    [Fact]
    public void AgentExtension_UsesStableSourceAndRegistrationLifetime()
    {
        var registry = new CopilotAgentExtensionRegistry();
        var provider = new CopilotFlowContextProvider(_ => Task.FromResult<CopilotFlowContextSnapshot?>(CreateSnapshot("Inspection")));

        var registration = CopilotFlowAgentExtension.Register(registry, provider, "1.2.3");

        var extension = Assert.Single(registry.GetSnapshot().Extensions);
        Assert.Equal(CopilotFlowAgentExtension.SourceId, extension.SourceId);
        Assert.Equal("Flow Engine", extension.SourceName);
        Assert.Equal("1.2.3", extension.SourceVersion);
        Assert.Same(provider, Assert.Single(extension.ContextProviders));

        registration.Dispose();

        Assert.Empty(registry.GetSnapshot().Extensions);
    }

    private static CopilotFlowContextSnapshot CreateSnapshot(string flowName)
    {
        return new CopilotFlowContextSnapshot
        {
            SourceId = CopilotFlowAgentExtension.SourceId,
            Revision = flowName,
            FlowName = flowName,
            Status = "Ready",
            Nodes =
            [
                new CopilotFlowNodeContextSnapshot
                {
                    InstanceId = "node-1",
                    Title = "Capture",
                    NodeType = "Camera",
                },
            ],
        };
    }
}
