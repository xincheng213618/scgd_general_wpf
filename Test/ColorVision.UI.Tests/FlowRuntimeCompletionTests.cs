using FlowEngineLib;
using FlowEngineLib.Base;
using FlowEngineLib.End;
using FlowEngineLib.MQTT;
using FlowEngineLib.Runtime;
using FlowEngineLib.Start;
using Newtonsoft.Json;
using ST.Library.UI.NodeContainer;
using ST.Library.UI.NodeEditor;
using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;

namespace ColorVision.UI.Tests;

public sealed class FlowRuntimeCompletionTests
{
    [Theory]
    [InlineData(0, StatusTypeEnum.Completed)]
    [InlineData(500, StatusTypeEnum.Failed)]
    public void TerminalNodeEndIsPublishedBeforeFlowCompletion(
        int responseCode,
        StatusTypeEnum expectedStatus)
    {
        RunInSta(async () =>
        {
            using var graph = CreateGraph();
            int nodeEndCountAtCompletion = -1;
            graph.Control.Finished += (_, _) =>
                Volatile.Write(
                    ref nodeEndCountAtCompletion,
                    graph.NodeEnds.Count);
            graph.Start.OnPublish = action =>
                Respond(graph.Sensor, action, responseCode);

            FlowEngineEventArgs completed = await StartAndWaitAsync(
                graph.Control,
                graph.Start.NodeName,
                $"SN-END-BEFORE-COMPLETION-{responseCode}");

            Assert.Equal(expectedStatus, completed.Status);
            Assert.Single(graph.NodeEnds);
            Assert.Equal(1, Volatile.Read(ref nodeEndCountAtCompletion));
        });
    }

    [Fact]
    public void ContinueOnFailUsesNormalOutput()
    {
        RunInSta(async () =>
        {
            using var graph = CreateGraph();
            graph.Sensor.ContinueOnFail = true;
            graph.Start.OnPublish = action =>
                Respond(graph.Sensor, action, 500);

            FlowEngineEventArgs completed = await StartAndWaitAsync(
                graph.Control,
                graph.Start.NodeName,
                "SN-CONTINUE-ON-FAIL");

            FlowEngineNodeEndEventArgs end =
                Assert.Single(graph.NodeEnds);
            Assert.Equal(StatusTypeEnum.Completed, completed.Status);
            Assert.Equal(1, graph.Start.PublishCount);
            Assert.Equal(0, end.RecvStatusCode);
            Assert.Equal(1, graph.NormalEnd.RunningCount);
        });
    }

    [Fact]
    public void StopPairsActiveNodeAsCanceled()
    {
        RunInSta(async () =>
        {
            using var graph = CreateGraph();
            graph.Sensor.MaxTime = 2_000;
            var completion =
                new TaskCompletionSource<FlowEngineEventArgs>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
            graph.Control.Finished += (_, args) =>
                completion.TrySetResult(args);

            Assert.True(graph.Control.TryStartNode(
                graph.Start.NodeName,
                "SN-CANCEL-ACTIVE"));
            await WaitUntilAsync(() => graph.NodeRuns.Count == 1);
            graph.Control.StopNode(
                graph.Start.NodeName,
                "SN-CANCEL-ACTIVE");

            FlowEngineEventArgs completed =
                await completion.Task.WaitAsync(TimeSpan.FromSeconds(2));
            await WaitUntilAsync(() => graph.NodeEnds.Count == 1);

            FlowEngineNodeRunEventArgs run =
                Assert.Single(graph.NodeRuns);
            FlowEngineNodeEndEventArgs end =
                Assert.Single(graph.NodeEnds);
            Assert.Equal(StatusTypeEnum.Canceled, completed.Status);
            Assert.Equal(run.SendMsgId, end.RecvMsgId);
            Assert.Equal(FlowFailureKind.Canceled, end.FailureKind);
            Assert.Equal(1, graph.Start.PublishCount);
        });
    }

    private static async Task<FlowEngineEventArgs> StartAndWaitAsync(
        FlowEngineControl control,
        string startNodeName,
        string serialNumber)
    {
        var completion =
            new TaskCompletionSource<FlowEngineEventArgs>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        FlowEngineEventHandler? handler = null;
        handler = (_, args) =>
        {
            if (!string.Equals(
                    args.SerialNumber,
                    serialNumber,
                    StringComparison.Ordinal))
            {
                return;
            }
            control.Finished -= handler;
            completion.TrySetResult(args);
        };
        control.Finished += handler;
        if (!control.TryStartNode(startNodeName, serialNumber))
        {
            control.Finished -= handler;
            throw new InvalidOperationException(
                $"Flow did not start: {startNodeName}/{serialNumber}.");
        }
        return await completion.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    private static void Respond(
        TempCommonSensorNode sensor,
        MQActionEvent action,
        int code)
    {
        CVMQTTRequest request =
            JsonConvert.DeserializeObject<CVMQTTRequest>(action.Message)
            ?? throw new InvalidOperationException(
                "Flow test request could not be decoded.");
        Assert.True(sensor.DoServerStatusRecv(
            new CVBaseDataFlowResp
            {
                Code = code,
                Message = code == 0 ? "ok" : "simulated failure",
                Version = request.Version,
                ServiceName = request.ServiceCode,
                EventName = request.EventName,
                SerialNumber = request.SerialNumber,
                MsgID = request.MsgID,
                Data = null,
                SendTime = DateTime.Now,
                ZIndex = request.ZIndex
            }));
    }

    private static RuntimeTestGraph CreateGraph()
    {
        var container = new CVNodeContainer();
        var start = new RuntimeTestStartNode();
        var sensor = new TempCommonSensorNode();
        var normalEnd = new RuntimeTestEndNode();
        start.Create();
        sensor.Create();
        normalEnd.Create();
        container.Nodes.Add(start);
        container.Nodes.Add(sensor);
        container.Nodes.Add(normalEnd);

        STNodeOption sensorInput = sensor
            .GetAllInputOptions()
            .Single(option => option.DataType == typeof(CVStartCFC));
        STNodeOption sensorOutput = sensor
            .GetAllOutputOptions()
            .Single(option => option.DataType == typeof(CVStartCFC));
        Assert.Equal(
            ConnectionStatus.Connected,
            start.m_op_start.ConnectOption(
                sensorInput,
                isOwnerOfOwner: false));
        Assert.Equal(
            ConnectionStatus.Connected,
            sensorOutput.ConnectOption(
                normalEnd.m_in_start,
                isOwnerOfOwner: false));

        var control = new FlowEngineControl(
            container,
            isAutoStartName: false,
            new FlowNodeManager());
        var nodeRuns =
            new ConcurrentQueue<FlowEngineNodeRunEventArgs>();
        var nodeEnds =
            new ConcurrentQueue<FlowEngineNodeEndEventArgs>();
        sensor.nodeRunEvent += (_, args) => nodeRuns.Enqueue(args);
        sensor.nodeEndEvent += (_, args) => nodeEnds.Enqueue(args);
        return new RuntimeTestGraph(
            container,
            control,
            start,
            sensor,
            normalEnd,
            nodeRuns,
            nodeEnds);
    }

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(2);
        while (!predicate())
        {
            if (DateTime.UtcNow >= deadline)
                throw new TimeoutException("Timed out waiting for flow state.");
            await Task.Delay(10);
        }
    }

    private static void RunInSta(Func<Task> action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (exception != null)
            ExceptionDispatchInfo.Capture(exception).Throw();
    }

    private sealed class RuntimeTestGraph : IDisposable
    {
        public RuntimeTestGraph(
            CVNodeContainer container,
            FlowEngineControl control,
            RuntimeTestStartNode start,
            TempCommonSensorNode sensor,
            RuntimeTestEndNode normalEnd,
            ConcurrentQueue<FlowEngineNodeRunEventArgs> nodeRuns,
            ConcurrentQueue<FlowEngineNodeEndEventArgs> nodeEnds)
        {
            Container = container;
            Control = control;
            Start = start;
            Sensor = sensor;
            NormalEnd = normalEnd;
            NodeRuns = nodeRuns;
            NodeEnds = nodeEnds;
        }

        public CVNodeContainer Container { get; }

        public FlowEngineControl Control { get; }

        public RuntimeTestStartNode Start { get; }

        public TempCommonSensorNode Sensor { get; }

        public RuntimeTestEndNode NormalEnd { get; }

        public ConcurrentQueue<FlowEngineNodeRunEventArgs> NodeRuns { get; }

        public ConcurrentQueue<FlowEngineNodeEndEventArgs> NodeEnds { get; }

        public void Dispose()
        {
            Control.Dispose();
            Container.Dispose();
        }
    }
}

public sealed class RuntimeTestStartNode : BaseStartNode
{
    private int publishCount;

    public RuntimeTestStartNode()
        : base("Runtime test start")
    {
        NodeName = "RuntimeTestStart";
    }

    public int PublishCount => Volatile.Read(ref publishCount);

    public Action<MQActionEvent>? OnPublish { get; set; }

    public override void DoPublish(MQActionEvent action)
    {
        Interlocked.Increment(ref publishCount);
        OnPublish?.Invoke(action);
    }
}

public sealed class RuntimeTestEndNode : CVEndNode
{
    private int runningCount;

    public int RunningCount => Volatile.Read(ref runningCount);

    protected override void DoNodeEnded(CVStartCFC startAction)
    {
        if (startAction.IsRunning)
            Interlocked.Increment(ref runningCount);
        base.DoNodeEnded(startAction);
    }
}
