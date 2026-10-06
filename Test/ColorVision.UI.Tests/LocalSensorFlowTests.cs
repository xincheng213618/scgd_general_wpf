using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Engine.Services.Devices.Sensor.Local;
using FlowEngineLib;
using FlowEngineLib.Base;
using FlowEngineLib.End;
using FlowEngineLib.Node.Global;
using ST.Library.UI.NodeContainer;
using ST.Library.UI.NodeEditor;

namespace ColorVision.UI.Tests;

public sealed class LocalSensorFlowTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, false)]
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
                    Assert.Equal(kind == 3 ? "Open" : "ExecCmd", request.EventName);
                    return new FakeExecution(fail, () => completed++);
                };
                using var container = new CVNodeContainer();
                var start = new RuntimeTestStartNode();
                CVBaseServerNode sensor = kind switch { 0 => new CommonSensorNode(), 1 => new TempCommonSensorNode(), 2 => new RealCommonSensorNode(), _ => new PhyDeviceControlNode() };
                var end = new CVEndNode();
                foreach (var node in new STNode[] { start, sensor, end }) { node.Create(); container.Nodes.Add(node); }
                if (sensor is PhyDeviceControlNode controlNode) controlNode.DeviceType = CVDeviceType.GeneralSensor;
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
            var node = new LocalSensorNode { DeviceCode = "PG.1", TemplateName = "power-on", Operation = LocalSensorOperation.ExecuteTemplate };
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
