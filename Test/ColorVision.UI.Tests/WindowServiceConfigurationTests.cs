using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Types;
using ColorVision.Engine.Services.Terminal;
using ColorVision.Engine.Services.Devices;

namespace ColorVision.UI.Tests;

public sealed class WindowServiceConfigurationTests
{
    [Fact]
    public void Snapshot_IgnoresSelectionAndHeartbeatButDetectsEditsAndReverts()
    {
        WpfTestHost.Invoke(() =>
        {
            var type = new TypeService { Name = "Camera" };
            var terminal = new TerminalService(new ColorVision.Engine.SysResourceModel { Name = "Service", Code = "test" });
            var device = new TestDevice();
            type.VisualChildren.Add(terminal);
            terminal.VisualChildren.Add(device);
            var types = new[] { type };
            var initial = WindowService.CaptureConfiguration(types);
            device.IsSelected = true;
            device.IsAlive = true;
            device.HeartbeatTime = 200;
            Assert.Equal(initial, WindowService.CaptureConfiguration(types));
            device.Configuration.Name = "Changed";
            Assert.NotEqual(initial, WindowService.CaptureConfiguration(types));
            device.Configuration.Name = "Camera";
            Assert.Equal(initial, WindowService.CaptureConfiguration(types));
            terminal.VisualChildren[0] = new TestDevice();
            Assert.NotEqual(initial, WindowService.CaptureConfiguration(types));
            terminal.VisualChildren.Clear();
            Assert.NotEqual(initial, WindowService.CaptureConfiguration(types));
        });
    }

    private sealed class TestDevice : DeviceService
    {
        public DeviceServiceConfig Configuration { get; } = new() { Name = "Camera" };
        public override object GetConfig() => Configuration;
        public override ColorVision.UI.CopilotBusinessContextBundle CaptureCopilotContext() => throw new NotSupportedException();
    }
}
