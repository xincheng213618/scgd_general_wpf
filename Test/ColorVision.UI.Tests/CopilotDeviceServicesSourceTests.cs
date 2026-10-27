using ColorVision.Engine;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Motor;
using ColorVision.UI;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class CopilotDeviceServicesSourceTests
{
    [Fact]
    public void FullInventoryUsesObjectReferencesEvenWhenResourceNamesCodesAndIdsRepeatWithoutCallingVirtualReaders()
    {
        WpfTestHost.Invoke(() =>
        {
            using var source = new CurrentManagerScope();
            var devices = Enumerable.Range(0, 75).Select(_ => new TrapDevice()).ToArray();
            source.Manager.DeviceServices = new(devices);
            var reader = new CopilotDeviceServicesReader();

            var snapshot = reader.Capture(source.Manager, CancellationToken.None);
            var repeated = new CopilotDeviceServicesReader().Capture(source.Manager, CancellationToken.None);

            Assert.Equal(75, snapshot.Devices.Count);
            Assert.Equal(75, snapshot.Devices.Select(device => device.DeviceRef).Distinct(StringComparer.Ordinal).Count());
            Assert.All(snapshot.Devices, device =>
            {
                Assert.Matches("^[a-f0-9]{32}$", device.DeviceRef);
                Assert.Equal(-7, device.ResourceId);
                Assert.Equal("duplicate-code", device.Code);
                Assert.Equal("duplicate-name", device.Name);
                Assert.Equal(7319, device.ServiceTypeCode);
                Assert.Null(device.StatusCode);
                Assert.Equal(CopilotDeviceServiceStatusReadState.Unsupported, device.StatusReadState);
            });
            Assert.All(devices, device => Assert.Equal(0, device.VirtualReads));
            Assert.Matches("^[a-f0-9]{32}$", snapshot.SourceInstanceId);
            Assert.Matches("^[a-f0-9]{64}$", snapshot.InventoryRevision);
            Assert.Equal(TimeSpan.Zero, snapshot.ObservedAtUtc.Offset);
            Assert.Equal(snapshot.SourceInstanceId, repeated.SourceInstanceId);
            Assert.Equal(snapshot.InventoryRevision, repeated.InventoryRevision);
            Assert.Equal(snapshot.Devices.Select(device => device.DeviceRef), repeated.Devices.Select(device => device.DeviceRef));
            Assert.Equal(devices.Cast<DeviceService>(), source.Manager.DeviceServices);
            Assert.Throws<NotSupportedException>(() => Assert.IsAssignableFrom<IList<CopilotDeviceServiceObservation>>(snapshot.Devices)[0] = snapshot.Devices[1]);
        });
    }

    [Fact]
    public void MembershipRevisionIgnoresOrderingMetadataAndCachedStatusButChangesForReplacementObjects()
    {
        WpfTestHost.Invoke(() =>
        {
            using var source = new CurrentManagerScope();
            var first = new TrapDevice();
            var (motor, mqtt) = CreateMotor((DeviceStatusType)7319);
            source.Manager.DeviceServices = [first, motor];
            var reader = new CopilotDeviceServicesReader();
            var before = reader.Capture(source.Manager, CancellationToken.None);

            first.SysResourceModel.Name = "renamed metadata";
            SetMotorStatus(mqtt, DeviceStatusType.Closed);
            source.Manager.DeviceServices.Move(0, 1);
            var changedMetadata = reader.Capture(source.Manager, CancellationToken.None);

            Assert.Equal(before.InventoryRevision, changedMetadata.InventoryRevision);
            Assert.Equal(before.SourceInstanceId, changedMetadata.SourceInstanceId);
            Assert.Contains(changedMetadata.Devices, device => device.Name == "renamed metadata");
            Assert.Contains(changedMetadata.Devices, device => device.StatusCode == (int)DeviceStatusType.Closed);
            var firstRef = before.Devices.Single(device => device.Code == "duplicate-code").DeviceRef;
            var replacement = new TrapDevice();
            source.Manager.DeviceServices[source.Manager.DeviceServices.IndexOf(first)] = replacement;
            var replaced = reader.Capture(source.Manager, CancellationToken.None);

            Assert.NotEqual(before.InventoryRevision, replaced.InventoryRevision);
            Assert.Equal(before.SourceInstanceId, replaced.SourceInstanceId);
            Assert.DoesNotContain(replaced.Devices, device => device.DeviceRef == firstRef);
            Assert.Equal(0, first.VirtualReads);
            Assert.Equal(0, replacement.VirtualReads);
        });
    }

    [Fact]
    public void CachedStatusReadsOnlyAuditedExistingServiceTypesAndPreservesUnknownCodes()
    {
        WpfTestHost.Invoke(() =>
        {
            using var source = new CurrentManagerScope();
            var (motor, _) = CreateMotor((DeviceStatusType)7319);
            var missing = (DeviceMotor)RuntimeHelpers.GetUninitializedObject(typeof(DeviceMotor));
            missing.SysResourceModel = new() { Code = "missing-existing-service" };
            var unsupported = (DeviceMotor)RuntimeHelpers.GetUninitializedObject(typeof(DeviceMotor));
            unsupported.SysResourceModel = new() { Code = "unsupported-service-subclass" };
            var trapService = (TrapMotorService)RuntimeHelpers.GetUninitializedObject(typeof(TrapMotorService));
            unsupported.DService = trapService;
            var failed = (DeviceCamera)RuntimeHelpers.GetUninitializedObject(typeof(DeviceCamera));
            failed.SysResourceModel = new() { Code = "unavailable-cached-state" };
            failed.DService = (MQTTCamera)RuntimeHelpers.GetUninitializedObject(typeof(MQTTCamera));
            failed.DService.Device = failed; // The absent cached backend must be reported, never constructed or probed.
            source.Manager.DeviceServices = [motor, missing, unsupported, failed];

            var snapshot = new CopilotDeviceServicesReader().Capture(source.Manager, CancellationToken.None);

            var cached = snapshot.Devices.Single(device => device.Code == "cached-motor");
            Assert.Equal(7319, cached.StatusCode);
            Assert.Equal(CopilotDeviceServiceStatusReadState.Cached, cached.StatusReadState);
            Assert.Equal(CopilotDeviceServiceStatusReadState.NoExistingService,
                snapshot.Devices.Single(device => device.Code == "missing-existing-service").StatusReadState);
            Assert.Equal(CopilotDeviceServiceStatusReadState.Unsupported,
                snapshot.Devices.Single(device => device.Code == "unsupported-service-subclass").StatusReadState);
            Assert.Equal(CopilotDeviceServiceStatusReadState.ReadFailed,
                snapshot.Devices.Single(device => device.Code == "unavailable-cached-state").StatusReadState);
            Assert.All(snapshot.Devices.Where(device => device != cached), device => Assert.Null(device.StatusCode));
            Assert.Equal(0, trapService.Reads);
            Assert.Null(missing.DService);
            Assert.Null(motor.Config);
            Assert.Null(failed.Config);
        });
    }

    [Fact]
    public void MissingMetadataIsExplicitAndDoesNotUseVirtualNameOrCodeFallbacks()
    {
        WpfTestHost.Invoke(() =>
        {
            using var source = new CurrentManagerScope();
            var device = new TrapDevice { SysResourceModel = null! };
            source.Manager.DeviceServices = [device];

            var snapshot = new CopilotDeviceServicesReader().Capture(source.Manager, CancellationToken.None);

            var result = Assert.Single(snapshot.Devices);
            Assert.Null(result.ResourceId);
            Assert.Null(result.ServiceTypeCode);
            Assert.Empty(result.Code);
            Assert.Empty(result.Name);
            Assert.Equal(0, device.VirtualReads);
        });
    }

    [Fact]
    public void FixedOwnerCaptureRejectsAReplacementManagerAndWrongThreadAccess()
    {
        WpfTestHost.Invoke(() =>
        {
            using var source = new CurrentManagerScope();
            source.Manager.DeviceServices = [new TrapDevice()];
            var reader = new CopilotDeviceServicesReader();
            var before = reader.Capture(source.Manager, CancellationToken.None);
            var offThread = Task.Run(() => Record.Exception(() => reader.Capture(source.Manager, CancellationToken.None))).GetAwaiter().GetResult();
            Assert.IsType<InvalidOperationException>(offThread);
            var replacement = CurrentManagerScope.CreateManager();
            replacement.DeviceServices = source.Manager.DeviceServices;
            source.Install(replacement);

            Assert.Throws<CopilotDeviceServicesSourceChangedException>(() => reader.Capture(source.Manager, CancellationToken.None));
            var after = reader.Capture(replacement, CancellationToken.None);

            Assert.NotEqual(before.SourceInstanceId, after.SourceInstanceId);
            Assert.Equal(before.InventoryRevision, after.InventoryRevision);
            Assert.Equal(before.Devices.Select(device => device.DeviceRef), after.Devices.Select(device => device.DeviceRef));
        });
    }

    [Fact]
    public void AsyncCaptureFreezesTheExistingOwnerBeforeQueuingItsUiCallback()
    {
        WpfTestHost.Invoke(() =>
        {
            using var source = new CurrentManagerScope();
            var trap = new TrapDevice();
            source.Manager.DeviceServices = [trap];
            var reader = new CopilotDeviceServicesReader();
            using var queued = new ManualResetEventSlim();
            var pending = Task.Run(async () =>
            {
                Task<CopilotDeviceServicesSnapshot?> operation;
                try { operation = reader.CaptureCurrentAsync(CancellationToken.None); }
                finally { queued.Set(); }
                return await operation;
            });
            // The UI stays occupied until the asynchronous source has frozen its owner and queued the capture.
            queued.Wait();
            source.Install(CurrentManagerScope.CreateManager());
            PumpUntilCompleted(pending);

            Assert.Throws<CopilotDeviceServicesSourceChangedException>(() => pending.GetAwaiter().GetResult());
            Assert.Equal(0, trap.VirtualReads);
        });
    }

    [Theory]
    [InlineData("duplicate_object")]
    [InlineData("null_device")]
    [InlineData("null_collection")]
    public void InvalidInventoriesAreRejectedWithoutChoosingAnArbitraryDevice(string invalidShape)
    {
        WpfTestHost.Invoke(() =>
        {
            using var source = new CurrentManagerScope();
            var device = new TrapDevice();
            source.Manager.DeviceServices = invalidShape switch
            {
                "duplicate_object" => [device, device],
                "null_device" => [device, null!],
                _ => null!,
            };

            Assert.Throws<CopilotDeviceServicesSourceInvalidException>(() => new CopilotDeviceServicesReader().Capture(source.Manager, CancellationToken.None));
            Assert.Equal(0, device.VirtualReads);
        });
    }

    [Fact]
    public void UninitializedCurrentAndPreCancellationDoNotCreateOrReadServices()
    {
        WpfTestHost.Invoke(() =>
        {
            using var source = new CurrentManagerScope();
            source.Install(null);
            var reader = new CopilotDeviceServicesReader();

            Assert.Null(reader.CaptureCurrentAsync(CancellationToken.None).GetAwaiter().GetResult());
            Assert.Null(ServiceManager.Current);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            Assert.ThrowsAny<OperationCanceledException>(() => reader.CaptureCurrentAsync(cancellation.Token).GetAwaiter().GetResult());
            Assert.Null(ServiceManager.Current);
        });
    }

    private static (DeviceMotor Device, MQTTMotor Service) CreateMotor(DeviceStatusType status)
    {
        var motor = (DeviceMotor)RuntimeHelpers.GetUninitializedObject(typeof(DeviceMotor));
        motor.SysResourceModel = new() { Id = 7, Code = "cached-motor", Name = "Cached motor", Type = 17 };
        motor.DService = (MQTTMotor)RuntimeHelpers.GetUninitializedObject(typeof(MQTTMotor));
        SetMotorStatus(motor.DService, status);
        return (motor, motor.DService);
    }

    private static void SetMotorStatus(MQTTMotor service, DeviceStatusType status) =>
        typeof(MQTTDeviceService<ConfigMotor>).GetField("_DeviceStatus", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(service, status);

    private static void PumpUntilCompleted(Task task)
    {
        if (task.IsCompleted) return;
        var frame = new DispatcherFrame();
        var dispatcher = Dispatcher.CurrentDispatcher;
        _ = task.ContinueWith(_ => { dispatcher.BeginInvoke(new Action(() => frame.Continue = false)); },
            CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        Dispatcher.PushFrame(frame);
    }

    private sealed class CurrentManagerScope : IDisposable
    {
        private static readonly FieldInfo InstanceField = typeof(ServiceManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly ServiceManager? _previous = ServiceManager.Current;
        public ServiceManager Manager { get; } = CreateManager();

        public CurrentManagerScope() => Install(Manager);
        public void Install(ServiceManager? manager) => InstanceField.SetValue(null, manager);
        public static ServiceManager CreateManager()
        {
            var manager = (ServiceManager)RuntimeHelpers.GetUninitializedObject(typeof(ServiceManager));
            manager.DeviceServices = [];
            return manager;
        }
        public void Dispose() => Install(_previous);
    }

    private sealed class TrapDevice : DeviceService
    {
        public int VirtualReads { get; private set; }
        public TrapDevice() => SysResourceModel = new() { Id = -7, Code = "duplicate-code", Name = "duplicate-name", Type = 7319 };
        private T Trap<T>() { VirtualReads++; throw new InvalidOperationException("This virtual member must not be read."); }
        public override string Code { get => Trap<string>(); set => throw new InvalidOperationException(); }
        public override string Name { get => Trap<string>(); set => throw new InvalidOperationException(); }
        public override string SendTopic { get => Trap<string>(); set => throw new InvalidOperationException(); }
        public override string SubscribeTopic { get => Trap<string>(); set => throw new InvalidOperationException(); }
        public override bool IsAlive { get => Trap<bool>(); set => throw new InvalidOperationException(); }
        public override DateTime LastAliveTime { get => Trap<DateTime>(); set => throw new InvalidOperationException(); }
        public override object GetConfig() => Trap<object>();
        public override MQTTServiceBase? GetMQTTService() => Trap<MQTTServiceBase>();
        public override UserControl GetDisplayControl() => Trap<UserControl>();
        public override UserControl GetDeviceInfo() => Trap<UserControl>();
        public override CopilotBusinessContextBundle CaptureCopilotContext() => Trap<CopilotBusinessContextBundle>();
        public override void Save() => Trap<object>();
        public override void Dispose() => Trap<object>();
    }

    private sealed class TrapMotorService : MQTTMotor
    {
        public int Reads { get; private set; }
        private TrapMotorService() : base(new ConfigMotor()) { }
        public override DeviceStatusType DeviceStatus
        {
            get { Reads++; throw new InvalidOperationException("An unknown service override must not be invoked."); }
            set => throw new InvalidOperationException();
        }
    }
}
