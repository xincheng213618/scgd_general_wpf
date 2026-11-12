using ColorVision.Engine;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Motor;
using ColorVision.Engine.Services.Types;
using ColorVision.UI;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ColorVision.UI.Tests;

public sealed class CopilotDeviceDiagnosisHeartbeatWorkflowTests
{
    [Theory]
    [InlineData(null, "Unknown", "unknown")]
    [InlineData(false, "Offline", "no")]
    [InlineData(true, "Online", "yes")]
    public void DiagnosisCommandAttachesTheObservedHeartbeatStateAndIndependentCachedOperationalStatus(
        bool? observation, string expectedDeviceStatus, string expectedHeartbeatStatus)
    {
        WpfTestHost.Invoke(() =>
        {
            var previousService = CopilotServiceRegistry.Current;
            var previousContext = CopilotLiveContextRegistry.Current;
            var device = new DiagnosisDevice();
            if (observation.HasValue) device.IsAlive = observation.Value;
            var service = new RecordingService();
            CopilotServiceRegistry.Register(service);
            try
            {
                device.AskCopilotCommand.Execute(null);

                var request = Assert.IsType<CopilotPromptRequest>(service.Request);
                Assert.Equal(CopilotPromptMode.Diagnose, request.Mode);
                Assert.True(request.AttachContextSnapshot);
                Assert.True(request.SendNow);
                Assert.Equal(1, device.CaptureCount);
                var item = Assert.Single(request.ContextItems);
                Assert.Contains($"Device status: {expectedDeviceStatus}", item.Content, StringComparison.Ordinal);
                Assert.Contains($"Heartbeat status: {expectedHeartbeatStatus}", item.Content, StringComparison.Ordinal);
                Assert.Contains("OperationalStatus: Opened", item.Content, StringComparison.Ordinal);
                Assert.Contains("Diagnosis focus: Heartbeat probe.", request.Prompt, StringComparison.Ordinal);
                Assert.Equal(item.Content, Assert.Single(CopilotLiveContextRegistry.Current!.SnapshotItems).Content);
                Assert.Equal(DeviceStatusType.Opened, device.CachedService.DeviceStatus);
                Assert.Equal(observation == true, device.IsAlive);
                Assert.Equal(default, device.LastAliveTime);
                if (observation == null)
                {
                    Assert.DoesNotContain("Device status: Offline", item.Content, StringComparison.Ordinal);
                    Assert.DoesNotContain("Heartbeat status: no", item.Content, StringComparison.Ordinal);
                }
            }
            finally
            {
                if (previousContext != null) CopilotLiveContextRegistry.Publish(previousContext);
                else CopilotLiveContextRegistry.Clear(CopilotDeviceContextFactory.GetSourceId(device));
                typeof(CopilotServiceRegistry).GetField("_current", BindingFlags.Static | BindingFlags.NonPublic)!
                    .SetValue(null, previousService);
            }
        });
    }

    private sealed class DiagnosisDevice : DeviceService<ConfigMotor>
    {
        internal MQTTServiceBase CachedService { get; }
        internal int CaptureCount { get; private set; }

        internal DiagnosisDevice() : base(new SysResourceModel
        {
            Id = 41, Code = "heartbeat-probe", Name = "Heartbeat probe", Type = (int)ServiceTypes.Motor, Value = "{}",
        })
        {
            CachedService = (MQTTServiceBase)RuntimeHelpers.GetUninitializedObject(typeof(MQTTServiceBase));
            CachedService.DeviceStatus = DeviceStatusType.Opened;
        }

        public override MQTTServiceBase GetMQTTService() => CachedService;

        public override CopilotBusinessContextBundle CaptureCopilotContext()
        {
            CaptureCount++;
            return base.CaptureCopilotContext();
        }
    }

    private sealed class RecordingService : ICopilotService
    {
        public bool IsAvailable => true;
        internal CopilotPromptRequest? Request { get; private set; }
        public void ShowPanel() { }
        public bool Ask(CopilotPromptRequest request) { Request = request; return true; }
    }
}
