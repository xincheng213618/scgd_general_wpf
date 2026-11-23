using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.Services.RC;
using ColorVision.Engine.Services.Terminal;
using ColorVision.Engine.Services.Types;
using ColorVision.UI;
using MQTTMessageLib;
using MQTTnet;
using MQTTnet.Packets;
using Newtonsoft.Json;
using System.Buffers;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class RcSnapshotApplicationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QueuedSnapshotsAreDiscardedWhenTransportChanges(bool reconnect)
    {
        using var managerScope = new OfflineManagerScope();
        var (control, client) = CreateTransport();
        using var rc = new MqttRCService(control, token => Task.Delay(Timeout.Infinite, token), startKeepAlive: false);
        int notifications = 0;
        rc.ServiceTokensUpdated += (_, _) => notifications++;

        // Queue the old callbacks and change connection before the dispatcher processes them.
        await RunAndDrainDispatcher(async () =>
        {
            Assert.True(await control.Connect());
            await ReceiveSnapshots(rc, client, "old/send", DeviceStatusType.Busy);
            await control.DisconnectAsyncClient();
            if (reconnect)
                Assert.True(await control.Connect());
        });

        Assert.Empty(rc.ServiceTokens);
        Assert.Equal(0, notifications);
        AssertPendingSnapshots(rc, "original", DeviceStatusType.UnInit);

        if (reconnect)
        {
            await RunAndDrainDispatcher(() => ReceiveSnapshots(rc, client, "fresh/send", DeviceStatusType.Opened));
            Assert.False(rc.IsConnect); // The RC has not sent a Startup token yet.
            Assert.Equal("fresh/send", Assert.Single(rc.ServiceTokens).PublishTopic);
            Assert.Equal(1, notifications);
            AssertPendingSnapshots(rc, "fresh/send", DeviceStatusType.Opened);
        }

        await control.DisconnectAsyncClient();
    }

    [Fact]
    public async Task BufferedSnapshotsAreDiscardedOnDisconnectBeforeServiceManagerStarts()
    {
        using var managerScope = new OfflineManagerScope();
        var (control, client) = CreateTransport();
        using var rc = new MqttRCService(control, token => Task.Delay(Timeout.Infinite, token), startKeepAlive: false);
        await RunAndDrainDispatcher(async () =>
        {
            Assert.True(await control.Connect());
            await ReceiveSnapshots(rc, client, "old/send", DeviceStatusType.Busy);
        });
        Assert.Equal("old/send", Assert.Single(rc.ServiceTokens).PublishTopic);

        await control.DisconnectAsyncClient();
        Assert.True(await control.Connect());

        Assert.Empty(rc.ServiceTokens);
        AssertPendingSnapshots(rc, "original", DeviceStatusType.UnInit);
        await control.DisconnectAsyncClient();
    }

    [Fact]
    public async Task DelayedRegistrationFromPreviousTransportCannotRegisterAfterReconnect()
    {
        var (control, client) = CreateTransport();
        Queue<(CancellationToken Token, TaskCompletionSource Delay)> registrations = new();
        using var rc = new MqttRCService(control, token =>
        {
            var delay = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            registrations.Enqueue((token, delay));
            return delay.Task;
        }, startKeepAlive: false);

        await RunAndDrainDispatcher(async () =>
        {
            Assert.True(await control.Connect());
            var old = registrations.Dequeue();
            await control.DisconnectAsyncClient();
            Assert.True(old.Token.IsCancellationRequested);
            Assert.True(await control.Connect());
            var current = registrations.Dequeue();
            Assert.False(current.Token.IsCancellationRequested);

            // Successful delay completion can win a race with cancellation before its continuation runs.
            old.Delay.SetResult();
            current.Delay.SetResult();
        });

        Assert.Contains("/Regist/", Assert.Single(client.PublishedTopics));
        await control.DisconnectAsyncClient();
    }

    [Fact]
    public async Task QueuedDeviceRestartChecksItsOwnerAtPublishAndKeepsTheExactCommandTarget()
    {
        var (control, client) = CreateTransport();
        using var rc = new MqttRCService(control, token => Task.Delay(Timeout.Infinite, token), startKeepAlive: false);
        Assert.True(await control.Connect());
        await client.ReceiveAsync(rc.SubscribeTopic, new MQTTNodeServiceStartupRequest
        {
            EventName = "Startup",
            Data = new() { Token = new() { AccessToken = "offline-rc-token" } },
        });
        Assert.True(rc.IsConnect);

        const string blockedTopic = "rc-test/publish-gate";
        const string adminTopic = "MQTTRCService/Admin/RC_local";
        var releasePublish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var ownerRejected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int ownsRequest = 1;

        client.BeforePublish = message => message.Topic == blockedTopic ? releasePublish.Task : Task.CompletedTask;
        Task<bool> blockedPublish = control.TryPublishOnConnectionAsync(client.Options!, blockedTopic, "{}", () => true);
        try
        {
            Assert.Contains(blockedTopic, client.PublishedTopics);
            Assert.True(rc.TryRestartServices("Camera", "original-service", "original-device", () =>
            {
                if (Volatile.Read(ref ownsRequest) != 0) return true;
                ownerRejected.TrySetResult();
                return false;
            }));
            Volatile.Write(ref ownsRequest, 0);
            releasePublish.TrySetResult();
            Assert.True(await blockedPublish.WaitAsync(TimeSpan.FromSeconds(5)));
            await ownerRejected.Task.WaitAsync(TimeSpan.FromSeconds(5));

            var adminPublished = new TaskCompletionSource<MqttApplicationMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            client.BeforePublish = message =>
            {
                if (message.Topic == adminTopic) adminPublished.TrySetResult(message);
                return Task.CompletedTask;
            };
            Assert.True(rc.TryRestartServices("Camera", "current-service", "current-device"));
            MqttApplicationMessage admin = await adminPublished.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var body = Newtonsoft.Json.Linq.JObject.Parse(System.Text.Encoding.UTF8.GetString(admin.Payload.ToArray()));
            Assert.Equal("ResetAppNodes", (string?)body["EventName"]);
            Assert.Equal("current-service", (string?)body["ServiceCode"]);
            Assert.Equal("current-device", (string?)body["DeviceCode"]);
            Assert.Single(client.PublishedTopics, topic => topic == adminTopic);
        }
        finally
        {
            releasePublish.TrySetResult();
            await control.DisconnectAsyncClient();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TerminalReloadPublishesOnlyExplicitDeviceTargets(bool retireAfterFirst)
    {
        var (control, client) = CreateTransport();
        using var rc = new MqttRCService(control, token => Task.Delay(Timeout.Infinite, token), startKeepAlive: false);
        Assert.True(await control.Connect());
        await client.ReceiveAsync(rc.SubscribeTopic, new MQTTNodeServiceStartupRequest
        {
            EventName = "Startup",
            Data = new() { Token = new() { AccessToken = "offline-rc-token" } },
        });
        Assert.True(rc.IsConnect);

        const string adminTopic = "MQTTRCService/Admin/RC_local";
        const string catalogTopic = "MQTTRCService/Public/RC_local";
        var commands = new List<(string? Event, string? Type, string? Service, string? Device)>();
        var settled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int refreshes = 0;
        int owning = 1;
        client.BeforePublish = message =>
        {
            if (message.Topic == adminTopic)
            {
                var body = Newtonsoft.Json.Linq.JObject.Parse(System.Text.Encoding.UTF8.GetString(message.Payload.ToArray()));
                commands.Add(((string?)body["EventName"], (string?)body["ServiceType"], (string?)body["ServiceCode"], (string?)body["DeviceCode"]));
                if (retireAfterFirst) Volatile.Write(ref owning, 0);
            }
            else if (message.Topic == catalogTopic)
            {
                refreshes++;
                settled.TrySetResult();
            }
            return Task.CompletedTask;
        };

        try
        {
            Assert.False(rc.TryReloadServiceDevices("Camera", "service-1", []));
            Assert.False(rc.TryReloadServiceDevices("Camera", "service-1", ["device-1", " "]));
            Assert.False(rc.TryReloadServiceDevices("", "service-1", ["device-1"]));
            Assert.False(rc.TryReloadServiceDevices("Camera", "", ["device-1"]));
            Assert.False(rc.TryRestartServices("Camera", "service-1", ""));

            Assert.True(rc.TryReloadServiceDevices("Camera", "service-1", ["device-1", "device-2", "device-1"], () =>
            {
                if (Volatile.Read(ref owning) != 0) return true;
                settled.TrySetResult();
                return false;
            }));
            await settled.Task.WaitAsync(TimeSpan.FromSeconds(6));

            string[] expectedDevices = retireAfterFirst ? ["device-1"] : ["device-1", "device-2"];
            Assert.Equal(expectedDevices.Select(device => ("ResetAppNodes", "Camera", "service-1", device)), commands);
            Assert.Equal(retireAfterFirst ? 0 : 1, refreshes);
        }
        finally
        {
            await control.DisconnectAsyncClient();
        }
    }

    [Fact]
    public void UnmappedServiceTypesAndIncompleteEntriesDoNotInterruptDirectoryRefresh()
    {
        WpfTestHost.Invoke(() =>
        {
            var camera = Terminal("camera-service");
            var flowTemplate = Terminal("template-service");
            var manager = OfflineManager();
            manager.TypeServices =
            [
                new TypeService { SysDictionaryModel = new() { Value = (int)ServiceTypes.Camera }, VisualChildren = [camera] },
                new TypeService { SysDictionaryModel = new() { Value = (int)ServiceTypes.FlowTemp }, VisualChildren = [flowTemplate] },
            ];
            Dictionary<CVServiceType, List<MQTTNodeService>> services = new()
            {
                [CVServiceType.Client] = [new() { ServiceCode = "rc-client", Devices = null! }],
                [CVServiceType.Sensor] = null!,
                [(CVServiceType)21] = [new() { ServiceCode = "template-service", UpChannel = "unmapped" }],
                [CVServiceType.Camera] =
                [
                    null!,
                    new()
                    {
                        ServiceCode = "camera-service", UpChannel = "camera/send", DownChannel = "camera/receive",
                        Devices = new() { ["unassigned"] = new(), ["valid"] = new() { Code = "Device.Camera" } },
                    },
                ],
            };

            var snapshot = MqttRCService.CreateServiceTokenSnapshot(services);
            Assert.Empty(Assert.Single(snapshot, item => item.ServiceCode == "rc-client").Devices);
            var cameraInfo = Assert.Single(snapshot, item => item.ServiceCode == "camera-service");
            Assert.Equal("Device.Camera", Assert.Single(cameraInfo.Devices).Key);

            MqttRCService.ApplyServices(manager, services);

            Assert.Equal("camera/send", camera.Config.SendTopic);
            Assert.Equal("camera/receive", camera.Config.SubscribeTopic);
            Assert.Equal("original", flowTemplate.Config.SendTopic);
        });
    }

    [Fact]
    public void UnknownOrMissingStatusesKeepPreviousStateAndValidDevicesStillUpdate()
    {
        var missing = new OfflineDevice("Device.Missing");
        var unknown = new OfflineDevice("Device.Unknown");
        var unrecognizedNumber = new OfflineDevice("Device.Numeric");
        var valid = new OfflineDevice("Device.Valid");
        foreach (var device in new[] { missing, unknown, unrecognizedNumber, valid })
            device.Status.DeviceStatus = DeviceStatusType.Busy;
        var missingList = Terminal("missing-list");
        missingList.VisualChildren = [missing];
        var reported = Terminal("reported");
        reported.VisualChildren = [unknown, unrecognizedNumber, valid];
        var manager = OfflineManager();
        manager.TerminalServices = [missingList, reported];

        MqttRCService.ApplyServiceStatus(manager,
        [
            new() { ServiceCode = "missing-list" },
            new()
            {
                ServiceCode = "reported",
                DeviceList =
                [
                    null!,
                    new() { Code = "Device.Unknown", Status = "NewServerState" },
                    new() { Code = "Device.Numeric", Status = "42" },
                    new() { Code = "Device.Valid", Status = nameof(DeviceStatusType.Opened) },
                ],
            },
        ]);

        Assert.Equal(DeviceStatusType.Busy, missing.Status.DeviceStatus);
        Assert.Equal(DeviceStatusType.Busy, unknown.Status.DeviceStatus);
        Assert.Equal(DeviceStatusType.Busy, unrecognizedNumber.Status.DeviceStatus);
        Assert.Equal(DeviceStatusType.Opened, valid.Status.DeviceStatus);
    }

    // Use inert service objects so no registration, broker connection, or database query is started.
    private static ServiceManager OfflineManager() => (ServiceManager)RuntimeHelpers.GetUninitializedObject(typeof(ServiceManager));

    private static (MQTTControl Control, TransportClient Client) CreateTransport()
    {
        IMqttClient client = DispatchProxy.Create<IMqttClient, TransportClient>();
        return (new MQTTControl(client), (TransportClient)(object)client);
    }

    private static Task RunAndDrainDispatcher(Func<Task> run) => WpfTestHost.Invoke(async () =>
    {
        await run();
        await Dispatcher.Yield(DispatcherPriority.Background);
    });

    private static async Task ReceiveSnapshots(MqttRCService rc, TransportClient client, string sendTopic, DeviceStatusType status)
    {
        await client.ReceiveAsync(rc.SubscribeTopic, new MQTTRCServicesQueryResponse
        {
            EventName = "QueryServices",
            Data = new() { [CVServiceType.Camera] = [new() { ServiceCode = "camera-service", UpChannel = sendTopic }] },
        });
        await client.ReceiveAsync(rc.SubscribeTopic, new MQTTRCServiceStatusQueryResponse
        {
            EventName = "QueryServiceStatus",
            Data = [new() { ServiceCode = "camera-service", DeviceList = [new() { Code = "Device.Camera", Status = status.ToString() }] }],
        });
    }

    private static void AssertPendingSnapshots(MqttRCService rc, string sendTopic, DeviceStatusType status)
    {
        WpfTestHost.Invoke(() =>
        {
            using var mqttDevice = new MQTTServiceBase(rc.MQTTControl) { DeviceStatus = DeviceStatusType.UnInit };
            var device = new OfflineDevice("Device.Camera", mqttDevice);
            var terminal = Terminal("camera-service");
            terminal.VisualChildren = [device];
            var manager = OfflineManager();
            manager.TypeServices = [new TypeService { SysDictionaryModel = new() { Value = (int)ServiceTypes.Camera }, VisualChildren = [terminal] }];
            manager.TerminalServices = [terminal];

            rc.ApplyPendingServiceUpdates(manager);

            Assert.Equal(sendTopic, terminal.Config.SendTopic);
            Assert.Equal(status, device.Status.DeviceStatus);
        });
    }

    public class TransportClient : MQTTControlTests.ClientProxy
    {
        private Func<MqttApplicationMessageReceivedEventArgs, Task>? _messageReceived;
        public List<string> PublishedTopics { get; } = [];
        public Func<MqttApplicationMessage, Task>? BeforePublish { get; set; }

        public Task ReceiveAsync(string topic, object response)
        {
            var message = new MqttApplicationMessageBuilder().WithTopic(topic).WithPayload(JsonConvert.SerializeObject(response)).Build();
            return _messageReceived!(new("rc-test", message, new MqttPublishPacket(), (_, _) => Task.CompletedTask));
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            switch (targetMethod!.Name)
            {
                case "add_ApplicationMessageReceivedAsync":
                    _messageReceived += (Func<MqttApplicationMessageReceivedEventArgs, Task>)args![0]!;
                    return null;
                case "PublishAsync":
                    return PublishAsync((MqttApplicationMessage)args![0]!);
                default:
                    return base.Invoke(targetMethod, args);
            }
        }

        private async Task<MqttClientPublishResult> PublishAsync(MqttApplicationMessage message)
        {
            PublishedTopics.Add(message.Topic);
            if (BeforePublish is { } beforePublish) await beforePublish(message);
            return null!;
        }
    }

    private sealed class OfflineManagerScope : IDisposable
    {
        private readonly FieldInfo _instance = typeof(ServiceManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly object? _previous;

        public OfflineManagerScope()
        {
            _previous = _instance.GetValue(null);
            _instance.SetValue(null, null);
        }

        public void Dispose() => _instance.SetValue(null, _previous);
    }

    private static TerminalService Terminal(string code)
    {
        var terminal = (TerminalService)RuntimeHelpers.GetUninitializedObject(typeof(TerminalService));
        terminal.SysResourceModel = new() { Code = code };
        terminal.Config = new() { SendTopic = "original" };
        terminal.MQTTServiceTerminalBase = (MQTTServiceTerminalBase)RuntimeHelpers.GetUninitializedObject(typeof(MQTTServiceTerminalBase));
        terminal.VisualChildren = [];
        return terminal;
    }

    private sealed class OfflineDevice(string code, MQTTServiceBase? mqttService = null) : DeviceService
    {
        private readonly DeviceServiceConfig _config = new() { Code = code };
        public MQTTServiceBase Status { get; } = mqttService ?? (MQTTServiceBase)RuntimeHelpers.GetUninitializedObject(typeof(MQTTServiceBase));
        public override object GetConfig() => _config;
        public override MQTTServiceBase GetMQTTService() => Status;
        public override CopilotBusinessContextBundle CaptureCopilotContext() => throw new NotSupportedException();
    }
}
