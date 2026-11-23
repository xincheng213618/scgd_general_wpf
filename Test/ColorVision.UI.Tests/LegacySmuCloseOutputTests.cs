using FlowEngineLib;
using FlowEngineLib.Base;
using FlowEngineLib.MQTT;
using FlowEngineLib.Node.SMU;
using FlowEngineLib.Start;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ST.Library.UI.NodeContainer;
using ST.Library.UI.NodeEditor;
using System.Collections.Concurrent;

namespace ColorVision.UI.Tests;

public sealed class LegacySmuCloseOutputTests
{
    [Fact]
    public void NormalReleaseClosesTheMeasuredChannelOnce() => StaTest.Run(() =>
    {
        using var delay = new ControlledDelay();
        var node = new DelayedPointNode(delay);
        using var graph = new TestGraph(node);
        node.Channel = SMUChannelType.B;
        var run = graph.StartRun("LEGACY-CLOSE");
        CVTransAction transaction = node.GetTransaction(run.Request.SerialNumber);
        Assert.Equal(SMUChannelType.B, ReadChannel(run.Request));
        node.Channel = SMUChannelType.A;

        graph.Complete(run, PointResult());
        node.ResetAgain(transaction);
        Assert.Empty(graph.Closures);

        delay.ReleaseAndWait();
        CVMQTTRequest close = ReadRequest(Assert.Single(graph.Closures));
        Assert.Equal(run.Request.SerialNumber, close.SerialNumber);
        Assert.Equal(run.Request.DeviceCode, close.DeviceCode);
        Assert.Equal(SMUChannelType.B, ReadChannel(close));
    });

    [Fact]
    public void ResetWithoutAnAttemptDoesNotScheduleAClosure() => StaTest.Run(() =>
    {
        using var delay = new ControlledDelay();
        var node = new DelayedPointNode(delay);
        using var graph = new TestGraph(node);
        var unmeasured = new CVStartCFC(graph.Start, ActionTypeEnum.Start, "LEGACY-NOT-ATTEMPTED");
        using var resources = unmeasured.RuntimeResources;

        node.ResetAgain(unmeasured);
        node.ResetAgain(new CVTransAction(unmeasured));

        Assert.Empty(delay.Tasks);
        Assert.Empty(graph.Start.Published);
    });

    [Fact]
    public void TemplateReleaseUsesTheChannelReturnedByTheService() => StaTest.Run(() =>
    {
        using var delay = new ControlledDelay();
        var node = new DelayedModelNode(delay);
        using var graph = new TestGraph(node);
        var run = graph.StartRun("LEGACY-TEMPLATE");
        Assert.Equal("ModelGetData", run.Request.EventName);

        var result = PointResult();
        result["ResultData"] = new JObject { ["V"] = 2, ["I"] = 1 };
        result["ScanRequestParam"] = JObject.FromObject(new
        {
            Channel = SMUChannelType.B, IsSourceV = true, BeginValue = 0, EndValue = 5, LimitValue = 1, Points = 1
        });
        graph.Complete(run, result);
        node.ResetChannelToA();
        delay.ReleaseAndWait();

        Assert.Equal(SMUChannelType.B, ReadChannel(ReadRequest(Assert.Single(graph.Closures))));
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DelayedClosureCannotTargetANewerRunOrReconnectedTransport(bool reconnect) => StaTest.Run(() =>
    {
        using var delay = new ControlledDelay();
        var node = new DelayedPointNode(delay);
        using var graph = new TestGraph(node);
        var oldRun = graph.StartRun("LEGACY-OLD");
        graph.Complete(oldRun, PointResult());

        if (reconnect)
        {
            graph.Start.Reconnect();
        }
        else
        {
            node.IsCloseOutput = false;
            var newerRun = graph.StartRun("LEGACY-NEW");
            Assert.Equal(oldRun.Request.DeviceCode, newerRun.Request.DeviceCode);
            Assert.Equal(ReadChannel(oldRun.Request), ReadChannel(newerRun.Request));
            graph.Complete(newerRun, PointResult());
        }

        delay.ReleaseAndWait();
        Assert.Empty(graph.Closures);
    });

    [Fact]
    public void NewerRunsOnAnotherChannelOrDevicePreserveTheOriginalClosure() => StaTest.Run(() =>
    {
        using var delay = new ControlledDelay();
        var node = new DelayedPointNode(delay);
        using var graph = new TestGraph(node);
        node.Channel = SMUChannelType.B;
        var original = graph.StartRun("LEGACY-ORIGINAL");
        graph.Complete(original, PointResult());

        node.IsCloseOutput = false;
        node.Channel = SMUChannelType.A;
        var otherChannel = graph.StartRun("LEGACY-OTHER-CHANNEL");
        Assert.Equal(original.Request.DeviceCode, otherChannel.Request.DeviceCode);
        Assert.Equal(SMUChannelType.A, ReadChannel(otherChannel.Request));
        graph.Complete(otherChannel, PointResult());

        node.DeviceCode = "LEGACY-SMU-SECOND-FAKE";
        node.Channel = SMUChannelType.B;
        var otherDevice = graph.StartRun("LEGACY-OTHER-DEVICE");
        Assert.NotEqual(original.Request.DeviceCode, otherDevice.Request.DeviceCode);
        Assert.Equal(SMUChannelType.B, ReadChannel(otherDevice.Request));
        graph.Complete(otherDevice, PointResult());

        delay.ReleaseAndWait();
        CVMQTTRequest close = ReadRequest(Assert.Single(graph.Closures));
        Assert.Equal(original.Request.SerialNumber, close.SerialNumber);
        Assert.Equal(original.Request.DeviceCode, close.DeviceCode);
        Assert.Equal(SMUChannelType.B, ReadChannel(close));
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LaterSweepOnTheSameTransportRetiresTheEarlierPointClosure(bool useTemplate) => StaTest.Run(() =>
    {
        using var delay = new ControlledDelay();
        var publisher = new object();
        var node = new DelayedPointNode(delay);
        using var previous = new TestGraph(node, publisher);
        node.Channel = SMUChannelType.B;
        var oldRun = previous.StartRun("LEGACY-SWEEP-OLD");
        previous.Complete(oldRun, PointResult());

        CVBaseServerNode sweep = useTemplate ? new RemoteSweepModelNode() : new RemoteSweepNode();
        using var next = new TestGraph(sweep, publisher);
        if (sweep is SMUSweepNode inline) inline.Channel = SMUChannelType.B;
        if (sweep is SMUSweepModelNode template) template.ModelName = "ServerTemplate";
        var newRun = next.StartRun("LEGACY-SWEEP-NEW");
        Assert.Equal("Scan", newRun.Request.EventName);
        Assert.Equal(oldRun.Request.DeviceCode, newRun.Request.DeviceCode);

        delay.ReleaseAndWait();
        Assert.Empty(previous.Closures);
        Assert.Empty(next.Closures);
    });

    private static JObject PointResult() => new() { ["V"] = 2, ["I"] = 1, ["MasterId"] = 123, ["MasterResultType"] = 7 };

    private static CVMQTTRequest ReadRequest(MQActionEvent action) =>
        JsonConvert.DeserializeObject<CVMQTTRequest>(action.Message)!;

    private static SMUChannelType ReadChannel(CVMQTTRequest request) =>
        (SMUChannelType)Assert.IsType<JObject>(request.Data).Value<int>("Channel");

    private sealed class DelayedPointNode(ControlledDelay delay) : SMUNode
    {
        internal CVTransAction GetTransaction(string serialNumber)
        {
            CVTransAction transaction = null!;
            Assert.True(HasTransAction(serialNumber, ref transaction));
            return transaction;
        }

        internal void ResetAgain(CVStartCFC action) => Reset(action);
        internal void ResetAgain(CVTransAction transaction) => Reset(transaction);

        protected override FlowLocalExecution CreateLocalExecution(CVMQTTRequest request) => null!;
        protected override Task WaitForCloseOutputDelayAsync() => delay.Release.Task;
        protected override void ObserveCloseOutputTask(Task task) => delay.Tasks.Enqueue(task);
    }

    private sealed class DelayedModelNode(ControlledDelay delay) : SMUModelNode
    {
        internal void ResetChannelToA() => _channel = SMUChannelType.A;

        protected override FlowLocalExecution CreateLocalExecution(CVMQTTRequest request) => null!;
        protected override Task WaitForCloseOutputDelayAsync() => delay.Release.Task;
        protected override void ObserveCloseOutputTask(Task task) => delay.Tasks.Enqueue(task);
    }

    private sealed class RemoteSweepNode : SMUSweepNode
    {
        protected override FlowLocalExecution CreateLocalExecution(CVMQTTRequest request) => null!;
    }

    private sealed class RemoteSweepModelNode : SMUSweepModelNode
    {
        protected override FlowLocalExecution CreateLocalExecution(CVMQTTRequest request) => null!;
    }

    private sealed class ControlledDelay : IDisposable
    {
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal ConcurrentQueue<Task> Tasks { get; } = new();

        internal void ReleaseAndWait()
        {
            Release.TrySetResult();
            Assert.Single(Tasks).WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
        }

        public void Dispose() => Release.TrySetResult();
    }

    private sealed class RecordingStartNode : BaseStartNode
    {
        private int connectionGeneration;
        private readonly object publisher;

        internal RecordingStartNode(object? publisher) : base("Legacy SMU test start")
        {
            this.publisher = publisher ?? this;
            NodeName = "LegacySmuStart";
        }

        internal ConcurrentQueue<MQActionEvent> Published { get; } = new();
        internal void Reconnect() => Interlocked.Increment(ref connectionGeneration);

        public override void DoPublish(MQActionEvent action) => Published.Enqueue(action);

        internal override void PublishWithDeferredCleanup(MQActionEvent action, Func<object, Action<MQActionEvent, Func<bool>>, bool> onPublish)
        {
            int originalGeneration = Volatile.Read(ref connectionGeneration);
            if (onPublish(publisher, (cleanup, canPublish) =>
            {
                if (originalGeneration == Volatile.Read(ref connectionGeneration) && canPublish())
                    DoPublish(cleanup);
            })) DoPublish(action);
        }
    }

    private sealed class TestGraph : IDisposable
    {
        private readonly CVNodeContainer container = new();
        private readonly FlowEngineControl control;
        private readonly CVBaseServerNode node;

        internal TestGraph(CVBaseServerNode node, object? publisher = null)
        {
            this.node = node;
            Start = new RecordingStartNode(publisher);
            var end = new RuntimeTestEndNode();
            foreach (STNode item in new STNode[] { Start, node, end })
            {
                item.Create();
                container.Nodes.Add(item);
            }
            node.DeviceCode = "LEGACY-SMU-FAKE";
            if (node is SMUBaseNode point) point.IsCloseOutput = true;
            Assert.Equal(ConnectionStatus.Connected, Start.m_op_start.ConnectOption(node.GetAllInputOptions()[0], false));
            Assert.Equal(ConnectionStatus.Connected, node.GetAllOutputOptions()[0].ConnectOption(end.m_in_start, false));
            control = new FlowEngineControl(container, false, new FlowNodeManager()) { PersistResults = false };
        }

        internal RecordingStartNode Start { get; }
        internal IEnumerable<MQActionEvent> Closures => Start.Published.Where(message => message.EventName == "CloseOutput");

        internal (CVStartCFC Action, CVMQTTRequest Request) StartRun(string serialNumber)
        {
            Assert.True(control.TryStartNode(Start.NodeName, serialNumber));
            CVStartCFC action = Assert.IsType<CVStartCFC>(Start.GetCFC(serialNumber));
            CVMQTTRequest request = Assert.Single(Start.Published.Where(message => message.EventName != "CloseOutput")
                .Select(ReadRequest), message => message.SerialNumber == serialNumber);
            return (action, request);
        }

        internal void Complete((CVStartCFC Action, CVMQTTRequest Request) run, JObject result)
        {
            CVMQTTRequest request = run.Request;
            var finished = new TaskCompletionSource<FlowEngineEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
            FlowEngineEventHandler handler = (_, args) =>
            {
                if (args.SerialNumber == request.SerialNumber) finished.TrySetResult(args);
            };
            control.Finished += handler;
            try
            {
                Assert.True(node.DoServerStatusRecv(new CVBaseDataFlowResp
                {
                    Code = 0, Message = "ok", Version = request.Version, ServiceName = request.ServiceCode,
                    DeviceNodeCode = node.NodeID, EventName = request.EventName, SerialNumber = request.SerialNumber,
                    MsgID = request.MsgID, Data = result, SendTime = DateTime.Now, ZIndex = request.ZIndex,
                }));
                Assert.Equal(StatusTypeEnum.Completed, finished.Task.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult().Status);
            }
            finally { control.Finished -= handler; }
        }

        public void Dispose()
        {
            control.Dispose();
            container.Dispose();
        }
    }
}
