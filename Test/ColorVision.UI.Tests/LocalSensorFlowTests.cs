using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Sensor;
using ColorVision.Engine.Services.Devices.Sensor.Local;
using FlowEngineLib;
using FlowEngineLib.Base;
using FlowEngineLib.End;
using ST.Library.UI.NodeContainer;
using ST.Library.UI.NodeEditor;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ColorVision.UI.Tests;

public sealed class LocalSensorFlowTests
{
    [Theory]
    [InlineData(0, false, false)]
    [InlineData(0, false, true)]
    [InlineData(1, false, false)]
    [InlineData(2, false, false)]
    [InlineData(1, true, false)]
    public void LegacySensorNodesCompleteWithoutMqtt(int kind, bool fail, bool ignoreErrors)
    {
        StaTest.Run(() =>
        {
            var previousPredicate = FlowLocalExecution.CanExecuteLocally;
            var previousFactory = FlowLocalExecution.CreateForNode;
            try
            {
                FlowLocalExecution.CanExecuteLocally = LocalSensorFlowExecution.Supports;
                int completed = 0;
                int created = 0;
                FlowLocalExecution.CreateForNode = (_, request) =>
                {
                    Interlocked.Increment(ref created);
                    Assert.Equal("ExecCmd", request.EventName);
                    return new FakeExecution(fail, () => completed++);
                };
                using var container = new CVNodeContainer();
                var start = new RuntimeTestStartNode();
                CVBaseServerNode sensor = kind switch { 0 => new CommonSensorNode { IgnoreErrors = ignoreErrors }, 1 => new TempCommonSensorNode(), _ => new RealCommonSensorNode() };
                var end = new CVEndNode();
                foreach (var node in new STNode[] { start, sensor, end }) { node.Create(); container.Nodes.Add(node); }
                Assert.True(LocalSensorFlowExecution.Supports(sensor));
                Assert.Equal(ConnectionStatus.Connected, start.m_op_start.ConnectOption(sensor.GetAllInputOptions()[0], false));
                Assert.Equal(ConnectionStatus.Connected, sensor.GetAllOutputOptions()[0].ConnectOption(end.m_in_start, false));
                using var control = new FlowEngineControl(container, false, new FlowNodeManager()) { PersistResults = false };
                var completion = new TaskCompletionSource<FlowEngineEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
                control.Finished += (_, result) => completion.TrySetResult(result);
                Assert.False(sensor.RequiresRemoteService);
                Assert.True(control.TryStartNode(start.NodeName, "local-sensor"));
                var result = completion.Task.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
                Assert.Equal(fail ? StatusTypeEnum.Failed : StatusTypeEnum.Completed, result.Status);
                Assert.Equal(0, start.PublishCount);
                Assert.Equal(1, Volatile.Read(ref created));
                Assert.Equal(fail ? 0 : 1, completed);
            }
            finally { FlowLocalExecution.CanExecuteLocally = previousPredicate; FlowLocalExecution.CreateForNode = previousFactory; }
        });
    }

    [Fact]
    public void LocalNodeStoresParametersAndHasNoTimeoutProperty()
    {
        StaTest.Run(() =>
        {
            var node = new LocalSensorNode { DeviceCode = "PG.1", TemplateName = "power-on" };
            node.Create();
            Assert.Single(node.GetAllInputOptions());
            Assert.Single(node.GetAllOutputOptions());
            string saved = System.Text.Encoding.UTF8.GetString(node.GetSaveData());
            Assert.Contains("PG.1", saved);
            Assert.Contains("power-on", saved);
            Assert.DoesNotContain("Timeout", saved);
            Assert.DoesNotContain(node.GetType().GetProperties(), property => property.Name.Contains("Timeout", StringComparison.OrdinalIgnoreCase));
        });
    }

    [Theory]
    [InlineData(false, null)]
    [InlineData(true, null)]
    [InlineData(true, 0)]
    [InlineData(true, 1)]
    [InlineData(true, 2)]
    public async Task TemplateExecutionConnectsOnlyWhenRequestedAndReusesConnection(bool autoConnect, int? legacyKind)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        ColorVision.UI.IConfigService? previousConfig = null;
        var managerField = typeof(ServiceManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)!;
        var previousManager = ServiceManager.Current;
        DeviceSensor? device = null;
        try
        {
            WpfTestHost.Invoke(() =>
            {
                previousConfig = ColorVision.UI.ConfigService.Instance;
                ColorVision.UI.ConfigService.SetInstance(new ColorVision.UI.ConfigHandler { IsAutoSave = false });
                var manager = (ServiceManager)RuntimeHelpers.GetUninitializedObject(typeof(ServiceManager));
                manager.DeviceServices = [];
                managerField.SetValue(null, manager);
                device = new DeviceSensor(new()
                {
                    Id = -2,
                    Code = "sensor-flow-" + Guid.NewGuid(),
                    Value = Newtonsoft.Json.JsonConvert.SerializeObject(new ConfigSensor
                    {
                        IsNet = true, Addr = "127.0.0.1", Port = ((IPEndPoint)listener.LocalEndpoint).Port
                    })
                });
                manager.DeviceServices.Add(device);
            });
            var action = new CVStartCFC();
            using var resources = action.RuntimeResources;
            var parameters = new
            {
                Cmd = new { CmdType = MQTTMessageLib.Sensor.SensorCmdType.Ascii, Request = "Q", Response = "OK", Timeout = 5000 }
            };
            using var execution = WpfTestHost.Invoke(() =>
            {
                if (legacyKind == null) return new LocalSensorFlowExecution(device!, "ExecCmd", parameters, autoConnect);
                CVBaseServerNode node = legacyKind switch { 0 => new CommonSensorNode(), 1 => new TempCommonSensorNode(), _ => new RealCommonSensorNode() };
                node.Create();
                node.DeviceCode = device!.Code;
                var request = new CVMQTTRequest("SVR.Sensor.Default", device.Code, "ExecCmd", "auto-connect", parameters, "", -1);
                device.DisplayConfig.UseLocalSensor = false;
                Assert.True(node.RequiresRemoteService);
                Assert.Null(FlowLocalExecution.CreateForNode(node, request));
                device.DisplayConfig.UseLocalSensor = true;
                Assert.False(node.RequiresRemoteService);
                return Assert.IsType<LocalSensorFlowExecution>(FlowLocalExecution.CreateForNode(node, request));
            });
            execution.Bind(action);
            if (!autoConnect)
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() => Task.Run(execution.Execute));
                Assert.Equal(ColorVision.Engine.Services.DeviceStatusType.Closed, device!.LocalSession.Status);
                Assert.False(listener.Pending());
                return;
            }

            // The first run connects; the second uses the same TCP connection.
            Task first = Task.Run(execution.Execute);
            var connected = listener.AcceptTcpClientAsync();
            await Task.WhenAny(first, connected).WaitAsync(TimeSpan.FromSeconds(5));
            if (first.IsFaulted) await first;
            using var peer = await connected.WaitAsync(TimeSpan.FromSeconds(5));
            byte[] request = new byte[1];
            for (int run = 0; run < 2; run++)
            {
                Task running = run == 0 ? first : Task.Run(execution.Execute);
                await peer.GetStream().ReadExactlyAsync(request).AsTask().WaitAsync(TimeSpan.FromSeconds(5));
                Assert.Equal((byte)'Q', request[0]);
                await peer.GetStream().WriteAsync("OK"u8.ToArray());
                await running.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.Equal("OK", Assert.IsAssignableFrom<IReadOnlyList<LocalSensorCommandResult>>(execution.Complete(action)).Single().ResponseText);
                Assert.False(listener.Pending());
            }

            await device!.LocalSession.CloseAsync();
            Task next = Task.Run(execution.Execute);
            using var reconnected = await listener.AcceptTcpClientAsync().WaitAsync(TimeSpan.FromSeconds(5));
            await reconnected.GetStream().ReadExactlyAsync(request).AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            await reconnected.GetStream().WriteAsync("OK"u8.ToArray());
            await next.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal("OK", Assert.IsAssignableFrom<IReadOnlyList<LocalSensorCommandResult>>(execution.Complete(action)).Single().ResponseText);
        }
        finally
        {
            WpfTestHost.Invoke(() =>
            {
                device?.Dispose();
                managerField.SetValue(null, previousManager);
                if (device != null) DisplayConfigManager.Instance.Configs.TryRemove(device.Code, out _);
                if (previousConfig != null) ColorVision.UI.ConfigService.SetInstance(previousConfig);
            });
        }
    }

    [Fact]
    public void SharedFlowResourcesCancelBoundWorkOnFinish()
    {
        var action = new CVStartCFC();
        var copy = new CVStartCFC(action);
        bool canceled = false;
        using var registration = copy.RuntimeResources.StopToken.Register(() => canceled = true);
        action.DoFinishing();
        Assert.True(canceled);
        Assert.True(copy.RuntimeResources.StopToken.IsCancellationRequested);
    }
    private sealed class FakeExecution(bool fail, Action complete) : FlowLocalExecution
    {
        public override bool UseNodeTimeout => false;
        public override void Execute() { if (fail) throw new InvalidOperationException("synthetic sensor failure"); }
        public override object Complete(CVStartCFC action) { complete(); return new { Message = "ok" }; }
        public override void Dispose() { }
    }
}
