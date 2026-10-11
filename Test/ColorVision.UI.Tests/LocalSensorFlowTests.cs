using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Engine.Services.Devices.Sensor;
using ColorVision.Engine.Services.Devices.Sensor.Local;
using FlowEngineLib;
using FlowEngineLib.Base;
using FlowEngineLib.End;
using ST.Library.UI.NodeContainer;
using ST.Library.UI.NodeEditor;
using System.Net;
using System.Net.Sockets;

namespace ColorVision.UI.Tests;

public sealed class LocalSensorFlowTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(1, true)]
    public void LegacySensorNodesCompleteWithoutMqtt(int kind, bool fail)
    {
        StaTest.Run(() =>
        {
            var previousPredicate = FlowLocalExecution.CanExecuteLocally;
            var previousFactory = FlowLocalExecution.CreateForNode;
            try
            {
                FlowLocalExecution.CanExecuteLocally = LocalSensorFlowExecution.Supports;
                int completed = 0;
                FlowLocalExecution.CreateForNode = (_, request) =>
                {
                    Assert.Equal("ExecCmd", request.EventName);
                    return new FakeExecution(fail, () => completed++);
                };
                using var container = new CVNodeContainer();
                var start = new RuntimeTestStartNode();
                CVBaseServerNode sensor = kind switch { 0 => new CommonSensorNode(), 1 => new TempCommonSensorNode(), _ => new RealCommonSensorNode() };
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
    [InlineData(false)]
    [InlineData(true)]
    public async Task TemplateExecutionConnectsOnlyWhenRequestedAndReusesConnection(bool autoConnect)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        ColorVision.UI.IConfigService? previousConfig = null;
        DeviceSensor? device = null;
        try
        {
            WpfTestHost.Invoke(() =>
            {
                previousConfig = ColorVision.UI.ConfigService.Instance;
                ColorVision.UI.ConfigService.SetInstance(new ColorVision.UI.ConfigHandler { IsAutoSave = false });
                device = new DeviceSensor(new()
                {
                    Id = -2,
                    Code = "sensor-flow-" + Guid.NewGuid(),
                    Value = Newtonsoft.Json.JsonConvert.SerializeObject(new ConfigSensor
                    {
                        IsNet = true, Addr = "127.0.0.1", Port = ((IPEndPoint)listener.LocalEndpoint).Port
                    })
                });
            });
            var action = new CVStartCFC();
            using var resources = action.RuntimeResources;
            using var execution = new LocalSensorFlowExecution(device!, "ExecCmd", new
            {
                Cmd = new { CmdType = MQTTMessageLib.Sensor.SensorCmdType.Ascii, Request = "Q", Response = "OK", Timeout = 5000 }
            }, autoConnect);
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
            using var peer = await listener.AcceptTcpClientAsync().WaitAsync(TimeSpan.FromSeconds(5));
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
