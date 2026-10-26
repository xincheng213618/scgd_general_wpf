using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.PG;
using ColorVision.Engine.Services.Devices.PG.Local;
using ColorVision.Engine.Services.Devices.Sensor;
using ColorVision.Engine.Services.Devices.Sensor.Local;
using FlowEngineLib;
using FlowEngineLib.Base;
using FlowEngineLib.End;
using FlowEngineLib.Node.PG;
using FlowEngineLib.PG;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ST.Library.UI.NodeContainer;
using ST.Library.UI.NodeEditor;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ColorVision.UI.Tests;

// Protect old flow serialization/routing and the shared connection's framing, cancellation and ownership boundaries.
public sealed class LocalPGTests
{
    [Theory]
    [InlineData("CM_StartPG", "POWER,ON")]
    [InlineData("CM_StopPG", "POWER,OFF")]
    [InlineData("CM_SwitchUpPG", "PATTERN,BACK")]
    [InlineData("CM_SwitchDownPG", "PATTERN,NEXT")]
    [InlineData("CM_SwitchFramePG", "PATTERN,INDEX,7")]
    [InlineData("KeyReset", "KEY,RESET")]
    [InlineData("KeyNext", "KEY,NEXT")]
    [InlineData("KeyBack", "KEY,BACK")]
    [InlineData("KeyAuto", "KEY,AUTO")]
    [InlineData("KeyEnter", "KEY,ENTER")]
    [InlineData("KeyRepeat", "KEY,REPEAT")]
    [InlineData("KeyUp", "KEY,UP")]
    [InlineData("KeyDown", "KEY,DOWN")]
    [InlineData("DEMURA.ERASE.START", "DEMURA,ERASE,START")]
    [InlineData("DEMURA.WRITE.START", "DEMURA,WRITE,START")]
    [InlineData("DEMURA.ON", "DEMURA,ON")]
    [InlineData("DEMURA.OFF", "DEMURA,OFF")]
    [InlineData("EFUSE.DieId", "EFUSE,DIEID,ABC123")]
    [InlineData("OSC.TRIM", "OSC,TRIM")]
    public void GecsCommandsUseTheServiceWireContractAndWaitForCompleteMatchingFrames(string name, string suffix)
    {
        var command = Assert.Single(Protocol().Build(new[] { new { Name = name, @params = new { index = "7", DieId = "ABC123" } } }));
        string request = "PG,01," + suffix;
        Assert.Equal(LocalPGProtocol.Packet(request), command.Encode(command.Request));
        var parser = command.CreateResponseParser!();
        string expected = name is "DEMURA.ERASE.START" or "DEMURA.WRITE.START" ? request[..^6] + ",END,OK" : request + ",END,OK";
        byte[] response = LocalPGProtocol.Packet(expected);
        byte[] prefix = LocalPGProtocol.Packet("ALIVE").Concat(LocalPGProtocol.Packet(request + ",processing")).Concat(response[..^1]).ToArray();
        Assert.Null(parser.Receive(prefix.AsSpan(0, 3)));
        Assert.Null(parser.Receive(prefix.AsSpan(3)));
        Assert.Equal(response, parser.Receive(response.AsSpan(response.Length - 1)));
    }

    [Theory]
    [InlineData("PG,01,POWER,ON,END,NG")]
    [InlineData("PG,02,POWER,ON,END,OK")]
    [InlineData("PG,01,POWER,OFF,END,OK")]
    public void InvalidOrUnrelatedResponsesNeverCompleteSuccessfully(string payload)
    {
        var command = Protocol().Build(new[] { new { Name = "CM_StartPG" } }).Single();
        Assert.Throws<System.IO.InvalidDataException>(() => command.CreateResponseParser!().Receive(LocalPGProtocol.Packet(payload)));
        byte[] malformed = LocalPGProtocol.Packet("PG,01,POWER,ON,END,OK");
        malformed[^1] = 0;
        Assert.Throws<System.IO.InvalidDataException>(() => command.CreateResponseParser!().Receive(malformed));
    }

    [Fact]
    public void MeasurementRequiresAllRailsAndParsesInvariantNumbers()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            var command = Protocol().Build(new[] { new { Name = "POWER.MEASURE" } }).Single();
            byte[] packet = LocalPGProtocol.Packet(MeasurePayload);
            Assert.Equal(packet, command.CreateResponseParser!().Receive(packet));
            JObject result = LocalPGProtocol.Measurement(MeasurePayload);
            Assert.Equal(1, result.Value<int>("Channel"));
            Assert.Equal(9, ((JArray)result["Voltages"]!).Count);
            Assert.Equal(9, ((JArray)result["Currents"]!).Count);
            Assert.Equal(1.25, result["Voltages"]![0]!.Value<double>("Value"));
            Assert.Throws<System.IO.InvalidDataException>(() => command.CreateResponseParser!().Receive(LocalPGProtocol.Packet(MeasurePayload + ",END,NG")));
            Assert.Throws<System.IO.InvalidDataException>(() => LocalPGProtocol.Measurement(MeasurePayload.Replace("1.25V", "NaNV")));
        }
        finally { CultureInfo.CurrentCulture = original; }
    }

    [Fact]
    public void TextCommandsPreserveTemplateBytesAndUnsupportedCommandsFailBeforeSending()
    {
        var protocol = new LocalPGProtocol(new ConfigPG { Category = "COMM.SkyCode" }, 1000,
            new Dictionary<string, string> { ["CM_StartPG"] = "start\r", ["CM_SwitchFramePG"] = "frame {0}\r" });
        var commands = protocol.Build(new[] { new { Name = "CM_StartPG", @params = new { index = 0 } }, new { Name = "CM_SwitchFramePG", @params = new { index = 7 } } });
        Assert.Equal("start\r"u8.ToArray(), commands[0].Encode(commands[0].Request));
        Assert.Equal("frame 7\r"u8.ToArray(), commands[1].Encode(commands[1].Request));
        Assert.All(commands, command => Assert.Equal(0, command.Timeout));
        Assert.Throws<NotSupportedException>(() => protocol.Build(new[] { new { Name = "DEMURA.ON" } }));
        Assert.Throws<NotSupportedException>(() => Protocol().Build(new[] { new { Name = "CM_ReSetPG" } }));
        Assert.Throws<ArgumentException>(() => Protocol().Build(new[] { new { Name = "EFUSE.DieId", @params = new { DieId = "X,POWER,ON" } } }));
    }

    [Fact]
    public void GecsChannelUsesTheServiceFieldAndDefaultWithoutReusingTheI2CRegister()
    {
        var config = JsonConvert.DeserializeObject<ConfigPG>("{\"Category\":\"GECS.V2.4\",\"RegisterAddress\":27,\"IsNet\":true,\"Addr\":\"127.0.0.1\",\"Port\":5555}")!;
        Assert.Equal("01", config.Channel);
        var display = new DisplayPGConfig { UseLocalPG = true };
        var draft = new PGConfigurationDraft(config, display);
        draft.Config.Channel = "02";
        Assert.True(draft.TryApply(config, out string error, display), error);
        Assert.Equal(27, config.RegisterAddress);
        var protocol = new LocalPGProtocol(config, 5000, new Dictionary<string, string>());
        var command = protocol.Build(new[] { new { Name = "CM_StartPG" } }).Single();
        Assert.Equal("PG,02,POWER,ON", LocalPGProtocol.Payload(command.Encode(command.Request)));
    }

    [Fact]
    public void LocalPreferenceIsOptInAndDraftValidationDoesNotMutateEitherStoreOnFailure()
    {
        var config = new ConfigPG { Category = "CH431.I2C", Addr = "0", Port = 8 };
        var display = new DisplayPGConfig();
        Assert.Equal("01", config.Channel);
        Assert.False(display.UseLocalPG);
        var draft = new PGConfigurationDraft(config, display);
        draft.Config.Name = "changed";
        draft.DisplayConfig.UseLocalPG = true;
        Assert.False(draft.TryApply(config, out string error, display));
        Assert.Contains("CH431", error);
        Assert.NotEqual("changed", config.Name);
        Assert.False(display.UseLocalPG);
        draft.Config.Category = "GECS.V2.4";
        draft.Config.IsNet = true;
        draft.NetworkAddress = "127.0.0.1";
        draft.NetworkPort = 5555;
        Assert.True(draft.TryApply(config, out error, display), error);
        Assert.True(display.UseLocalPG);
        Assert.Null(JObject.FromObject(config)["UseLocalPG"]);
    }

    [Theory]
    [InlineData(0, "PG,01,POWER,ON")]
    [InlineData(1, "PG,01,PATTERN,INDEX,7")]
    [InlineData(2, "PG,01,POWER,MEASURE")]
    [InlineData(3, "PG,01,KEY,BACK")]
    [InlineData(4, "PG,01,DEMURA,WRITE,START")]
    public async Task EveryLegacyPGNodeRunsLocallyWithoutPublishingMqtt(int kind, string expected)
    {
        using var listener = Listen();
        using var scope = new DeviceScope(((IPEndPoint)listener.LocalEndpoint).Port);
        Task peer = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync();
            Assert.Equal(expected, await ReadPacket(client));
            string response = kind == 2 ? MeasurePayload : kind == 4 ? "PG,01,DEMURA,WRITE,END,OK" : expected + ",END,OK";
            await client.GetStream().WriteAsync(LocalPGProtocol.Packet(response));
            // Keep the shared device connection alive until the test disposes it.
            Assert.Equal(0, await client.GetStream().ReadAsync(new byte[1]));
        });
        var completion = new TaskCompletionSource<FlowEngineEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        CVNodeContainer? container = null;
        FlowEngineControl? control = null;
        RuntimeTestStartNode? start = null;
        try
        {
            WpfTestHost.Invoke(() =>
            {
                container = new CVNodeContainer();
                start = new RuntimeTestStartNode();
                CVBaseServerNode node = kind switch
                {
                    0 => new PGNode(), 1 => new PGLoopNode(), 2 => new PGGECSNode(), 3 => new PGGECS_KeyNode(), _ => new PGGECS_DemuraNode()
                };
                var end = new CVEndNode();
                foreach (var item in new STNode[] { start, node, end }) { item.Create(); container.Nodes.Add(item); }
                node.DeviceCode = scope.Device.Code;
                if (node is PGLoopNode loop) loop.Params = [new PGNodeProperty { Cmd = PGCommCmdType.指定, Data = new PGParamData { IndexFrame = 7 } }];
                if (node is PGGECSNode gecs) gecs.PGCmd = PGGECSCommCmdType.电压电流;
                if (node is PGGECS_KeyNode key) key.PGCmd = PGGECSKeyCmdType.KeyBack;
                if (node is PGGECS_DemuraNode demura) demura.PGCmd = PGGECSDemuraCmdType.WriteStart;
                var request = new CVMQTTRequest("SVR.PG.Default", scope.Device.Code, "ExecCmd", "routing", new[] { new { Name = "CM_StartPG" } }, "", -1);
                scope.Device.DisplayConfig.UseLocalPG = false;
                Assert.True(node.RequiresRemoteService);
                Assert.Null(FlowLocalExecution.CreateForNode(node, request));
                scope.Device.DisplayConfig.UseLocalPG = true;
                Assert.False(node.RequiresRemoteService);
                Assert.True(LocalPGFlowExecution.Supports(node));
                Assert.Equal(ConnectionStatus.Connected, start.m_op_start.ConnectOption(node.GetAllInputOptions()[0], false));
                Assert.Equal(ConnectionStatus.Connected, node.GetAllOutputOptions()[0].ConnectOption(end.m_in_start, false));
                control = new FlowEngineControl(container, false, new FlowNodeManager()) { PersistResults = false };
                control.Finished += (_, result) => completion.TrySetResult(result);
                Assert.True(control.TryStartNode(start.NodeName, "local-pg"));
            });
            var result = await completion.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(StatusTypeEnum.Completed, result.Status);
            Assert.Equal(0, start!.PublishCount);
        }
        finally
        {
            await scope.Device.LocalSession.CloseAsync();
            WpfTestHost.Invoke(() => { control?.Dispose(); container?.Dispose(); });
        }
        await peer.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ConnectionIsSharedWithManualControlsAndRemainsBoundUntilClosed()
    {
        using var listener = Listen();
        using var scope = new DeviceScope(((IPEndPoint)listener.LocalEndpoint).Port);
        var open = WpfTestHost.Invoke(() => scope.Device.DService.Open(CommunicateType.Tcp, "127.0.0.1", scope.Device.Config.Port));
        using var client = await listener.AcceptTcpClientAsync().WaitAsync(TimeSpan.FromSeconds(5));
        await WaitRecord(open);
        Assert.True(scope.Device.LocalSession.IsOpen);
        // Editing preferences/endpoint while connected cannot reroute commands to a different device.
        scope.Device.Config.Channel = "02";
        scope.Device.Config.Category = "CH431.I2C";
        scope.Device.DisplayConfig.UseLocalPG = false;
        Assert.True(scope.Device.PGBackend.OpensLocally);
        for (int index = 0; index < 2; index++)
        {
            var running = scope.Device.ExecuteLocalPGAsync("SetParam", new[] { new { Name = "CM_StartPG" } }, scope.Device.SnapshotLocalPGConfig(), default, true);
            Assert.Equal("PG,01,POWER,ON", await ReadPacket(client));
            await client.GetStream().WriteAsync(LocalPGProtocol.Packet("PG,01,POWER,ON,END,OK"));
            await running.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(listener.Pending());
        }
        var measurementRun = scope.Device.ExecuteLocalPGAsync("ExecCmd", new[] { new { Name = "POWER.MEASURE" } }, scope.Device.SnapshotLocalPGConfig(), default, true);
        Assert.Equal("PG,01,POWER,MEASURE", await ReadPacket(client));
        await client.GetStream().WriteAsync(LocalPGProtocol.Packet(MeasurePayload));
        JObject measured = JObject.FromObject((await measurementRun.WaitAsync(TimeSpan.FromSeconds(5)))!);
        Assert.Equal("POWER.MEASURE", measured.Value<string>("CmdType"));
        Assert.Equal(1, JObject.Parse(measured.Value<string>("ResultData")!).Value<int>("Channel"));
        var close = WpfTestHost.Invoke(() => scope.Device.DService.Close());
        await WaitRecord(close);
        Assert.False(scope.Device.PGBackend.OpensLocally);
        Assert.False(scope.Device.LocalSession.IsOpen);
    }

    [Theory]
    [InlineData("cancel")]
    [InlineData("close")]
    [InlineData("ng")]
    [InlineData("disconnect")]
    public async Task FailedOrCanceledExchangeClosesTheConnection(string failure)
    {
        using var listener = Listen();
        using var scope = new DeviceScope(((IPEndPoint)listener.LocalEndpoint).Port);
        using var cancellation = new CancellationTokenSource();
        var running = scope.Device.ExecuteLocalPGAsync("ExecCmd", new[] { new { Name = "DEMURA.ON" } }, scope.Device.SnapshotLocalPGConfig(), cancellation.Token, true);
        using var client = await listener.AcceptTcpClientAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("PG,01,DEMURA,ON", await ReadPacket(client));
        if (failure == "cancel") cancellation.Cancel();
        else if (failure == "close") await scope.Device.LocalSession.CloseAsync();
        else if (failure == "disconnect") client.Dispose();
        else await client.GetStream().WriteAsync(LocalPGProtocol.Packet("PG,01,DEMURA,ON,END,NG"));
        await Assert.ThrowsAnyAsync<Exception>(() => running.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.False(scope.Device.LocalSession.IsOpen);
        Assert.False(listener.Pending());
    }

    [Fact]
    public void SensorAndPGServiceOwnershipBlocksLocalConnectionOnTheSameEndpoint()
    {
        using var scope = new DeviceScope(5555);
        WpfTestHost.Invoke(() =>
        {
            using var sensor = new DeviceSensor(new() { Id = -3, Code = "pg-conflict-" + Guid.NewGuid(), Value = JsonConvert.SerializeObject(new ConfigSensor { IsNet = true, Addr = "127.0.0.1", Port = 5555 }) });
            ServiceManager.Current!.DeviceServices.Add(sensor);
            try
            {
                sensor.SensorBackend.ObserveService(DeviceStatusType.Opened);
                lock (scope.Device.PGBackend.Sync) Assert.Throws<InvalidOperationException>(() => LocalCommunicationOwnership.EnsureAvailable(scope.Device, true, scope.Device.LocalEndpoint));
                sensor.SensorBackend.ObserveService(DeviceStatusType.OffLine);
                Assert.Throws<InvalidOperationException>(() => LocalCommunicationOwnership.EnsureAvailable(scope.Device, true, scope.Device.LocalEndpoint));
                sensor.SensorBackend.ObserveService(DeviceStatusType.Closed);
                LocalCommunicationOwnership.EnsureAvailable(scope.Device, true, scope.Device.LocalEndpoint);
                scope.Device.PGBackend.SetLocalStatus(DeviceStatusType.Opened);
                Assert.Throws<InvalidOperationException>(() => sensor.EnsureOtherSensorBackends(false, LocalSensorSession.EndpointKey(sensor.Config)));
            }
            finally { ServiceManager.Current.DeviceServices.Remove(sensor); DisplayConfigManager.Instance.Configs.TryRemove(sensor.Code, out _); }
        });
    }

    private static LocalPGProtocol Protocol() => new(new ConfigPG { Category = "GECS.V2.4", RegisterAddress = 1 }, 5000, new Dictionary<string, string>());
    private const string MeasurePayload = "PG,01,POWER,MEASURE,1.25V,2V,3V,4V,5V,6V,7V,8V,9V,1mA,2mA,3mA,4mA,5mA,6mA,7mA,8mA,9mA";
    private static TcpListener Listen() { var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); return listener; }
    private static async Task<string> ReadPacket(TcpClient client)
    {
        byte[] header = new byte[6];
        await client.GetStream().ReadExactlyAsync(header).AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        int length = int.Parse(System.Text.Encoding.ASCII.GetString(header, 2, 4), NumberStyles.HexNumber);
        byte[] body = new byte[length + 1];
        await client.GetStream().ReadExactlyAsync(body).AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(3, body[^1]);
        return System.Text.Encoding.ASCII.GetString(body, 0, length);
    }
    private static async Task WaitRecord(ColorVision.Engine.Messages.MsgRecord record)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Changed(object? sender, ColorVision.Engine.Messages.MsgRecordState state)
        {
            if (state == ColorVision.Engine.Messages.MsgRecordState.Success) completion.TrySetResult();
            else if (state is ColorVision.Engine.Messages.MsgRecordState.Fail or ColorVision.Engine.Messages.MsgRecordState.Timeout) completion.TrySetException(new InvalidOperationException(record.MsgReturn?.Message));
        }
        record.MsgRecordStateChanged += Changed;
        try { Changed(record, record.MsgRecordState); await completion.Task.WaitAsync(TimeSpan.FromSeconds(5)); }
        finally { record.MsgRecordStateChanged -= Changed; }
    }
    private sealed class DeviceScope : IDisposable
    {
        private readonly FieldInfo managerField = typeof(ServiceManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly ServiceManager? previousManager = ServiceManager.Current;
        private readonly ColorVision.UI.IConfigService previousConfig = ColorVision.UI.ConfigService.Instance;
        internal DevicePG Device { get; }
        internal DeviceScope(int port)
        {
            Device = WpfTestHost.Invoke(() =>
            {
                ColorVision.UI.ConfigService.SetInstance(new ColorVision.UI.ConfigHandler { IsAutoSave = false });
                var manager = (ServiceManager)RuntimeHelpers.GetUninitializedObject(typeof(ServiceManager));
                manager.DeviceServices = [];
                managerField.SetValue(null, manager);
                var device = new DevicePG(new() { Id = -2, Code = "local-pg-" + Guid.NewGuid(), Value = JsonConvert.SerializeObject(new ConfigPG { Category = "GECS.V2.4", RegisterAddress = 1, IsNet = true, Addr = "127.0.0.1", Port = port }) });
                device.DisplayConfig.UseLocalPG = true;
                manager.DeviceServices.Add(device);
                return device;
            });
        }
        public void Dispose() => WpfTestHost.Invoke(() =>
        {
            Device.Dispose();
            DisplayConfigManager.Instance.Configs.TryRemove(Device.Code, out _);
            managerField.SetValue(null, previousManager);
            ColorVision.UI.ConfigService.SetInstance(previousConfig);
        });
    }
}
