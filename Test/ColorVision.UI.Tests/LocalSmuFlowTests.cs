using ColorVision.Engine.Services.Devices.SMU.Local;
using FlowEngineLib;
using FlowEngineLib.Base;
using FlowEngineLib.End;
using FlowEngineLib.Node.Global;
using FlowEngineLib.Node.SMU;
using ST.Library.UI.NodeContainer;
using ST.Library.UI.NodeEditor;
using Channel = ColorVision.Engine.Services.Devices.SMU.Dao.SMUChannelType;

namespace ColorVision.UI.Tests;

public sealed class LocalSmuFlowTests
{
    // Keep saved legacy nodes on the local result and cleanup paths without MQTT.
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void LegacySmuNodesCompleteAndCloseWithoutPublishing(int kind)
    {
        StaTest.Run(() =>
        {
            var previousPredicate = FlowLocalExecution.CanExecuteLocally;
            var previousFactory = FlowLocalExecution.CreateForNode;
            try
            {
                FlowLocalExecution.CanExecuteLocally = LocalSmuFlowExecution.Supports;
                int completed = 0, closed = 0;
                CVStartCFC? completedAction = null;
                FlowLocalExecution.CreateForNode = (_, request) => new FakeExecution(
                    request.EventName == "CloseOutput" ? () =>
                    {
                        if (kind == 5) Assert.Equal(1, Newtonsoft.Json.Linq.JObject.FromObject(request.Data).Value<int>("Channel"));
                        closed++;
                    } : () => { }, action => { completed++; completedAction = action; }, request.EventName == "ModelGetData");
                using var container = new CVNodeContainer();
                var start = new RuntimeTestStartNode();
                CVBaseServerNode node = kind switch { 0 => new SMUNode(), 1 => new SMUSweepNode(), 2 => new SMUSweepModelNode(),
                    3 => new SMUReaderNode(), 4 => new PhyDeviceControlNode(), _ => new SMUModelNode() };
                var end = new CVEndNode();
                foreach (var item in new STNode[] { start, node, end }) { item.Create(); container.Nodes.Add(item); }
                if (node is PhyDeviceControlNode physical) physical.DeviceType = CVDeviceType.SMU;
                if (node is SMUBaseNode smu) smu.IsCloseOutput = true;
                Assert.True(LocalSmuFlowExecution.Supports(node));
                Assert.Equal(ConnectionStatus.Connected, start.m_op_start.ConnectOption(node.GetAllInputOptions()[0], false));
                Assert.Equal(ConnectionStatus.Connected, node.GetAllOutputOptions()[0].ConnectOption(end.m_in_start, false));
                using var control = new FlowEngineControl(container, false, new FlowNodeManager()) { PersistResults = false };
                var completion = new TaskCompletionSource<FlowEngineEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
                control.Finished += (_, result) => completion.TrySetResult(result);
                Assert.False(node.RequiresRemoteService);
                Assert.True(control.TryStartNode(start.NodeName, "local-smu"));
                var result = completion.Task.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
                Assert.Equal(StatusTypeEnum.Completed, result.Status);
                Assert.Equal(0, start.PublishCount);
                Assert.Equal(1, completed);
                Assert.Equal(kind is 0 or 5 ? 1 : 0, closed);
                if (kind == 5) Assert.Equal(FlowEngineLib.SMUChannelType.B, Assert.IsType<SMUResultData>(completedAction!.Data["SMUResult"]).Channel);
            }
            finally { FlowLocalExecution.CanExecuteLocally = previousPredicate; FlowLocalExecution.CreateForNode = previousFactory; }
        });
    }

    private sealed class FakeExecution(Action execute, Action<CVStartCFC> complete, bool model) : FlowLocalExecution
    {
        public override void Execute() => execute();
        public override object Complete(CVStartCFC action)
        {
            complete(action);
            var result = LocalSmuResultService.Response(new LocalSmuCapture(new LocalSmuParameters { Channel = Channel.A }, [5], [10], 1, false), 0);
            if (model)
            {
                result["ResultData"] = new Newtonsoft.Json.Linq.JObject { ["V"] = 5, ["I"] = 10 };
                result["ScanRequestParam"] = Newtonsoft.Json.Linq.JObject.FromObject(new LocalSmuParameters
                    { Channel = Channel.B, IsSourceV = true, BeginValue = 0, EndValue = 5, LimitValue = 1, Points = 5 });
            }
            return result;
        }
        public override void Dispose() { }
    }
}
