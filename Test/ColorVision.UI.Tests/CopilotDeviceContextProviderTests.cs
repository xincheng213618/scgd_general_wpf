using ColorVision.Engine.Services;
using ColorVision.UI;

namespace ColorVision.UI.Tests;

public sealed class CopilotDeviceContextProviderTests
{
    [Fact]
    public async Task DeviceProvider_CapturesFreshContextForEachRelevantRequest()
    {
        var deviceName = "Camera A";
        var captureCount = 0;
        var provider = new CopilotDeviceContextProvider((_, _) =>
        {
            captureCount++;
            return Task.FromResult<CopilotBusinessContextBundle?>(CreateBundle(deviceName));
        });

        var first = await provider.CaptureAsync(CreateRequest("检查当前设备状态"), CancellationToken.None);
        deviceName = "Camera B";
        var second = await provider.CaptureAsync(CreateRequest("diagnose the device heartbeat"), CancellationToken.None);

        Assert.Equal(2, captureCount);
        Assert.Contains("Camera A", Assert.IsType<CopilotContextItem>(first).Content, StringComparison.Ordinal);
        Assert.Contains("Camera B", Assert.IsType<CopilotContextItem>(second).Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeviceProvider_SkipsUnrelatedAgentTurnsButSupportsCurrentSurfaceAndDiagnose()
    {
        var captureCount = 0;
        var isCurrentSurface = false;
        var provider = new CopilotDeviceContextProvider(
            (_, _) =>
            {
                captureCount++;
                return Task.FromResult<CopilotBusinessContextBundle?>(CreateBundle("Camera A"));
            },
            isCurrentSurface: () => isCurrentSurface);

        Assert.Null(await provider.CaptureAsync(CreateRequest("解释这段代码"), CancellationToken.None));
        Assert.Equal(0, captureCount);

        isCurrentSurface = true;
        Assert.NotNull(await provider.CaptureAsync(CreateRequest("它现在怎么样？"), CancellationToken.None));
        isCurrentSurface = false;
        Assert.NotNull(await provider.CaptureAsync(CreateRequest("继续排查", CopilotContextScope.Diagnose), CancellationToken.None));
        Assert.Equal(2, captureCount);
    }

    [Fact]
    public async Task DeviceProvider_FailsClosedWhenInactiveOrContextIsEmpty()
    {
        var active = false;
        var captureCount = 0;
        var provider = new CopilotDeviceContextProvider(
            (_, _) =>
            {
                captureCount++;
                return Task.FromResult<CopilotBusinessContextBundle?>(new CopilotBusinessContextBundle());
            },
            () => active);

        Assert.False(provider.CanProvide(CopilotContextScope.Agent));
        Assert.Null(await provider.CaptureAsync(CreateRequest("检查设备"), CancellationToken.None));
        Assert.Equal(0, captureCount);

        active = true;
        Assert.True(provider.CanProvide(CopilotContextScope.Diagnose));
        Assert.Null(await provider.CaptureAsync(CreateRequest("检查设备"), CancellationToken.None));
        Assert.Equal(1, captureCount);
    }

    [Fact]
    public async Task DeviceProvider_DropsSnapshotWhenSourceBecomesInactiveDuringCapture()
    {
        var active = true;
        var provider = new CopilotDeviceContextProvider(
            (_, _) =>
            {
                active = false;
                return Task.FromResult<CopilotBusinessContextBundle?>(CreateBundle("Removed camera"));
            },
            () => active);

        var result = await provider.CaptureAsync(CreateRequest("检查设备"), CancellationToken.None);

        Assert.Null(result);
        Assert.False(provider.CanProvide(CopilotContextScope.Agent));
    }

    [Fact]
    public void DeviceExtension_UsesStableSourceMetadataAndRegistrationLifetime()
    {
        var registry = new CopilotAgentExtensionRegistry();
        var provider = new CopilotDeviceContextProvider((_, _) => Task.FromResult<CopilotBusinessContextBundle?>(null));

        using (CopilotDeviceAgentExtension.Register(registry, provider, "2.0.0"))
        {
            var extension = Assert.Single(registry.GetSnapshot().Extensions);
            Assert.Equal(CopilotDeviceAgentExtension.SourceId, extension.SourceId);
            Assert.Equal("Device Services", extension.SourceName);
            Assert.Equal("2.0.0", extension.SourceVersion);
            Assert.Same(provider, Assert.Single(extension.ContextProviders));
            Assert.Empty(extension.Tools);
        }

        Assert.Empty(registry.GetSnapshot().Extensions);
    }

    private static CopilotContextRequest CreateRequest(string userText, CopilotContextScope scope = CopilotContextScope.Agent)
    {
        return new CopilotContextRequest { Scope = scope, UserText = userText };
    }

    private static CopilotBusinessContextBundle CreateBundle(string deviceName)
    {
        var item = new CopilotContextItem
        {
            Id = "device:test",
            Title = deviceName,
            Summary = "Online",
            Content = $"Device name: {deviceName}",
        };
        return CopilotBusinessContextBundle.FromItem("device:test", item);
    }
}
