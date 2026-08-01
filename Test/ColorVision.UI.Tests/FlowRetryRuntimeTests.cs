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

public sealed class FlowRetryRuntimeTests
{
    [Fact]
    public void FailedResponsesRetryWithPairedMessageIdsAndPlannedBackoff()
    {
        RunInSta(async () =>
        {
            using var graph = CreateGraph();
            graph.Control.ConfigureRetryPolicies(
            [
                CreatePolicy(
                    graph.Sensor,
                    maxAttempts: 3,
                    initialDelayMs: 10,
                    backoff: 2,
                    FlowFailureKind.Business)
            ]);
            graph.Start.OnPublish = (action, attempt) =>
                Respond(
                    graph.Sensor,
                    action,
                    attempt < 3 ? 500 : 0);

            FlowEngineEventArgs completed = await StartAndWaitAsync(
                graph.Control,
                graph.Start.NodeName,
                "SN-RETRY-SUCCESS");
            await WaitUntilAsync(
                () => graph.NodeEnds.Count == 3);

            FlowEngineNodeRunEventArgs[] runs =
                graph.NodeRuns.ToArray();
            FlowEngineNodeEndEventArgs[] ends =
                graph.NodeEnds.ToArray();
            Assert.Equal(StatusTypeEnum.Completed, completed.Status);
            Assert.Equal(3, graph.Start.PublishCount);
            Assert.Equal(3, runs.Length);
            Assert.Equal(3, ends.Length);
            Assert.Equal(
                runs.Select(item => item.SendMsgId),
                ends.Select(item => item.RecvMsgId));
            Assert.Equal(
                new[] { 1, 2, 3 },
                runs.Select(item => item.AttemptNumber));
            Assert.True(ends[0].WillRetry);
            Assert.Equal(10, ends[0].RetryDelayMs);
            Assert.True(ends[1].WillRetry);
            Assert.Equal(20, ends[1].RetryDelayMs);
            Assert.False(ends[2].WillRetry);
            Assert.Equal(0, ends[2].RecvStatusCode);
            Assert.Equal(3, runs.Select(item => item.SendMsgId).Distinct().Count());
        });
    }

    [Fact]
    public void TimeoutsUseTotalAttemptsAndTerminateOnlyAfterExhaustion()
    {
        RunInSta(async () =>
        {
            using var graph = CreateGraph();
            graph.Sensor.MaxTime = 20;
            graph.Control.ConfigureRetryPolicies(
            [
                CreatePolicy(
                    graph.Sensor,
                    maxAttempts: 3,
                    initialDelayMs: 0,
                    backoff: 2,
                    FlowFailureKind.Timeout)
            ]);

            FlowEngineEventArgs completed = await StartAndWaitAsync(
                graph.Control,
                graph.Start.NodeName,
                "SN-RETRY-TIMEOUT");
            await WaitUntilAsync(
                () => graph.NodeEnds.Count == 3);

            FlowEngineNodeRunEventArgs[] runs =
                graph.NodeRuns.ToArray();
            FlowEngineNodeEndEventArgs[] ends =
                graph.NodeEnds.ToArray();
            Assert.Equal(StatusTypeEnum.OverTime, completed.Status);
            Assert.Equal(3, graph.Start.PublishCount);
            Assert.Equal(3, runs.Length);
            Assert.Equal(3, ends.Length);
            Assert.Equal(
                runs.Select(item => item.SendMsgId),
                ends.Select(item => item.RecvMsgId));
            Assert.All(
                ends,
                item => Assert.Equal(
                    FlowFailureKind.Timeout,
                    item.FailureKind));
            Assert.True(ends[0].WillRetry);
            Assert.True(ends[1].WillRetry);
            Assert.False(ends[2].WillRetry);
        });
    }

    [Fact]
    public void ContinueOnFailWinsOverRetryAndUsesNormalOutput()
    {
        RunInSta(async () =>
        {
            using var graph = CreateGraph();
            graph.Sensor.ContinueOnFail = true;
            graph.Control.ConfigureRetryPolicies(
            [
                CreatePolicy(
                    graph.Sensor,
                    maxAttempts: 4,
                    initialDelayMs: 0,
                    backoff: 1,
                    FlowFailureKind.Business)
            ]);
            graph.Start.OnPublish = (action, _) =>
                Respond(graph.Sensor, action, 500);

            FlowEngineEventArgs completed = await StartAndWaitAsync(
                graph.Control,
                graph.Start.NodeName,
                "SN-CONTINUE-ON-FAIL");
            await WaitUntilAsync(
                () => graph.NodeEnds.Count == 1);

            FlowEngineNodeEndEventArgs end =
                Assert.Single(graph.NodeEnds);
            Assert.Equal(StatusTypeEnum.Completed, completed.Status);
            Assert.Equal(1, graph.Start.PublishCount);
            Assert.False(end.WillRetry);
            Assert.False(end.FailureHandled);
            Assert.Equal(0, end.RecvStatusCode);
            Assert.Empty(completed.HandledFailures);
            Assert.Equal(1, graph.NormalEnd.RunningCount);
        });
    }

    [Fact]
    public void ExhaustedRetryUsesRuntimeErrorRouteInsteadOfNormalOutput()
    {
        RunInSta(async () =>
        {
            using var graph = CreateGraph(new CountingEndNode());
            CountingEndNode errorEnd =
                Assert.IsType<CountingEndNode>(graph.ErrorEnd);
            graph.Control.ConfigureRetryPolicies(
            [
                CreatePolicy(
                    graph.Sensor,
                    maxAttempts: 2,
                    initialDelayMs: 0,
                    backoff: 1,
                    FlowFailureKind.Business)
            ]);
            graph.Control.ConfigureFailureRoutes(
            [
                new FlowErrorRoute
                {
                    SourceNodeId = graph.Sensor.NodeID,
                    TargetNodeId = errorEnd.NodeID,
                    TargetInputIndex = 0,
                    FailureKinds =
                    [
                        FlowFailureKind.Business
                    ]
                }
            ]);
            graph.Start.OnPublish = (action, _) =>
                Respond(graph.Sensor, action, 500);

            FlowEngineEventArgs completed = await StartAndWaitAsync(
                graph.Control,
                graph.Start.NodeName,
                "SN-ERROR-ROUTE");
            await WaitUntilAsync(
                () => graph.NodeEnds.Count == 2);

            FlowEngineNodeEndEventArgs[] ends =
                graph.NodeEnds.ToArray();
            Assert.Equal(StatusTypeEnum.Completed, completed.Status);
            Assert.Equal(2, graph.Start.PublishCount);
            Assert.True(ends[0].WillRetry);
            Assert.False(ends[0].FailureHandled);
            Assert.False(ends[1].WillRetry);
            Assert.True(ends[1].FailureHandled);
            Assert.Equal(errorEnd.NodeID, ends[1].FailureRouteTargetNodeId);
            Assert.Equal(0, graph.NormalEnd.RunningCount);
            Assert.Equal(1, errorEnd.RunningCount);
            FlowHandledFailure handled =
                Assert.Single(completed.HandledFailures);
            Assert.Equal(errorEnd.NodeID, handled.TargetNodeId);
            Assert.Equal(
                FlowFailureKind.Business,
                handled.Failure.Kind);
        });
    }

    [Fact]
    public void ErrorRouteDispatchFailureFallsBackToTerminalFailure()
    {
        RunInSta(async () =>
        {
            var throwingTarget =
                new ThrowingFailureTargetNode();
            using var graph = CreateGraph(throwingTarget);
            graph.Control.ConfigureFailureRoutes(
            [
                new FlowErrorRoute
                {
                    SourceNodeId = graph.Sensor.NodeID,
                    TargetNodeId = throwingTarget.Guid.ToString(),
                    TargetInputIndex = 0,
                    FailureKinds =
                    [
                        FlowFailureKind.Business
                    ]
                }
            ]);
            graph.Start.OnPublish = (action, _) =>
                Respond(graph.Sensor, action, 500);

            FlowEngineEventArgs completed = await StartAndWaitAsync(
                graph.Control,
                graph.Start.NodeName,
                "SN-ERROR-ROUTE-THROWS");
            await WaitUntilAsync(
                () => graph.NodeEnds.Count == 1);

            FlowEngineNodeEndEventArgs end =
                Assert.Single(graph.NodeEnds);
            Assert.Equal(StatusTypeEnum.Failed, completed.Status);
            Assert.False(end.FailureHandled);
            Assert.Empty(completed.HandledFailures);
            Assert.Contains(
                "错误分支运行失败",
                end.RecvStatusMessage,
                StringComparison.Ordinal);
            Assert.Equal(0, graph.NormalEnd.RunningCount);
        });
    }

    [Fact]
    public void StopPairsActiveAttemptAsCanceledWithoutRetry()
    {
        RunInSta(async () =>
        {
            using var graph = CreateGraph();
            graph.Sensor.MaxTime = 2_000;
            graph.Control.ConfigureRetryPolicies(
            [
                CreatePolicy(
                    graph.Sensor,
                    maxAttempts: 3,
                    initialDelayMs: 0,
                    backoff: 2,
                    FlowFailureKind.Timeout)
            ]);
            var completion =
                new TaskCompletionSource<FlowEngineEventArgs>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
            graph.Control.Finished += (_, args) =>
                completion.TrySetResult(args);

            Assert.True(graph.Control.TryStartNode(
                graph.Start.NodeName,
                "SN-CANCEL-ACTIVE"));
            await WaitUntilAsync(
                () => graph.NodeRuns.Count == 1);
            graph.Control.StopNode(
                graph.Start.NodeName,
                "SN-CANCEL-ACTIVE");

            FlowEngineEventArgs completed =
                await completion.Task.WaitAsync(
                    TimeSpan.FromSeconds(2));
            await WaitUntilAsync(
                () => graph.NodeEnds.Count == 1);

            FlowEngineNodeRunEventArgs run =
                Assert.Single(graph.NodeRuns);
            FlowEngineNodeEndEventArgs end =
                Assert.Single(graph.NodeEnds);
            Assert.Equal(StatusTypeEnum.Canceled, completed.Status);
            Assert.Equal(run.SendMsgId, end.RecvMsgId);
            Assert.Equal(FlowFailureKind.Canceled, end.FailureKind);
            Assert.False(end.WillRetry);
            Assert.Equal(1, graph.Start.PublishCount);
        });
    }

    [Fact]
    public void StopDuringBackoffCancelsDelayedRetryAndCompletesOnce()
    {
        RunInSta(async () =>
        {
            using var graph = CreateGraph();
            graph.Control.ConfigureRetryPolicies(
            [
                CreatePolicy(
                    graph.Sensor,
                    maxAttempts: 3,
                    initialDelayMs: 200,
                    backoff: 2,
                    FlowFailureKind.Business)
            ]);
            graph.Start.OnPublish = (action, _) =>
                Respond(graph.Sensor, action, 500);
            var retryScheduled =
                new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
            graph.Sensor.nodeEndEvent += (_, args) =>
            {
                if (args.WillRetry)
                {
                    retryScheduled.TrySetResult(true);
                }
            };
            int completionCount = 0;
            var completion =
                new TaskCompletionSource<FlowEngineEventArgs>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
            graph.Control.Finished += (_, args) =>
            {
                Interlocked.Increment(ref completionCount);
                completion.TrySetResult(args);
            };

            Assert.True(graph.Control.TryStartNode(
                graph.Start.NodeName,
                "SN-CANCEL-BACKOFF"));
            await retryScheduled.Task.WaitAsync(
                TimeSpan.FromSeconds(2));

            graph.Control.StopNode(
                graph.Start.NodeName,
                "SN-CANCEL-BACKOFF");
            FlowEngineEventArgs completed =
                await completion.Task.WaitAsync(
                    TimeSpan.FromSeconds(2));
            await Task.Delay(350);

            Assert.Equal(StatusTypeEnum.Canceled, completed.Status);
            Assert.Equal(1, completionCount);
            Assert.Equal(1, graph.Start.PublishCount);
            Assert.Single(graph.NodeRuns);
            FlowEngineNodeEndEventArgs end =
                Assert.Single(graph.NodeEnds);
            Assert.True(end.WillRetry);
            Assert.Equal(
                graph.NodeRuns.Single().SendMsgId,
                end.RecvMsgId);
            Assert.False(graph.Control.IsRunning);
        });
    }

    private static FlowNodeRetryPolicy CreatePolicy(
        TempCommonSensorNode sensor,
        int maxAttempts,
        int initialDelayMs,
        double backoff,
        params FlowFailureKind[] failureKinds)
    {
        return new FlowNodeRetryPolicy
        {
            NodeId = sensor.NodeID,
            MaxAttempts = maxAttempts,
            InitialDelayMs = initialDelayMs,
            Backoff = backoff,
            MaxDelayMs = Math.Max(initialDelayMs, 1_000),
            RetryableKinds = failureKinds
        };
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
        return await completion.Task.WaitAsync(
            TimeSpan.FromSeconds(5));
    }

    private static void Respond(
        TempCommonSensorNode sensor,
        MQActionEvent action,
        int code)
    {
        CVMQTTRequest request =
            JsonConvert.DeserializeObject<CVMQTTRequest>(
                action.Message)
            ?? throw new InvalidOperationException(
                "Retry test request could not be decoded.");
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

    private static RetryTestGraph CreateGraph(
        STNode? errorEnd = null)
    {
        var container = new CVNodeContainer();
        var start = new RetryTestStartNode();
        var sensor = new TempCommonSensorNode();
        var normalEnd = new CountingEndNode();
        start.Create();
        sensor.Create();
        normalEnd.Create();
        errorEnd?.Create();
        container.Nodes.Add(start);
        container.Nodes.Add(sensor);
        container.Nodes.Add(normalEnd);
        if (errorEnd != null)
        {
            container.Nodes.Add(errorEnd);
        }

        STNodeOption sensorInput = sensor
            .GetAllInputOptions()
            .Single(option =>
                option.DataType == typeof(CVStartCFC));
        STNodeOption sensorOutput = sensor
            .GetAllOutputOptions()
            .Single(option =>
                option.DataType == typeof(CVStartCFC));
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
        sensor.nodeRunEvent += (_, args) =>
            nodeRuns.Enqueue(args);
        sensor.nodeEndEvent += (_, args) =>
            nodeEnds.Enqueue(args);
        return new RetryTestGraph(
            container,
            control,
            start,
            sensor,
            normalEnd,
            errorEnd,
            nodeRuns,
            nodeEnds);
    }

    private static async Task WaitUntilAsync(
        Func<bool> predicate)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(2);
        while (!predicate())
        {
            if (DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException(
                    "Timed out waiting for retry runtime state.");
            }
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
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }
    }

    private sealed class RetryTestGraph : IDisposable
    {
        public RetryTestGraph(
            CVNodeContainer container,
            FlowEngineControl control,
            RetryTestStartNode start,
            TempCommonSensorNode sensor,
            CountingEndNode normalEnd,
            STNode? errorEnd,
            ConcurrentQueue<FlowEngineNodeRunEventArgs> nodeRuns,
            ConcurrentQueue<FlowEngineNodeEndEventArgs> nodeEnds)
        {
            Container = container;
            Control = control;
            Start = start;
            Sensor = sensor;
            NormalEnd = normalEnd;
            ErrorEnd = errorEnd;
            NodeRuns = nodeRuns;
            NodeEnds = nodeEnds;
        }

        public CVNodeContainer Container { get; }

        public FlowEngineControl Control { get; }

        public RetryTestStartNode Start { get; }

        public TempCommonSensorNode Sensor { get; }

        public CountingEndNode NormalEnd { get; }

        public STNode? ErrorEnd { get; }

        public ConcurrentQueue<FlowEngineNodeRunEventArgs> NodeRuns { get; }

        public ConcurrentQueue<FlowEngineNodeEndEventArgs> NodeEnds { get; }

        public void Dispose()
        {
            Control.Dispose();
            Container.Dispose();
        }
    }
}

public sealed class RetryTestStartNode : BaseStartNode
{
    private int publishCount;

    public RetryTestStartNode()
        : base("Retry test start")
    {
        NodeName = "RetryTestStart";
    }

    public int PublishCount =>
        Volatile.Read(ref publishCount);

    public Action<MQActionEvent, int>? OnPublish { get; set; }

    public override void DoPublish(MQActionEvent action)
    {
        int attempt =
            Interlocked.Increment(ref publishCount);
        OnPublish?.Invoke(action, attempt);
    }
}

public sealed class CountingEndNode : CVEndNode
{
    private int completedCount;

    private int runningCount;

    public int CompletedCount =>
        Volatile.Read(ref completedCount);

    public int RunningCount =>
        Volatile.Read(ref runningCount);

    protected override void DoNodeEnded(CVStartCFC startAction)
    {
        if (startAction.IsRunning)
        {
            Interlocked.Increment(ref runningCount);
        }
        Interlocked.Increment(ref completedCount);
        base.DoNodeEnded(startAction);
    }
}

public sealed class ThrowingFailureTargetNode : STNode
{
    protected override void OnCreate()
    {
        base.OnCreate();
        STNodeOption input =
            InputOptions.Add(
                "IN",
                typeof(CVStartCFC),
                bSingle: true);
        input.DataTransfer += (_, _) =>
            throw new InvalidOperationException(
                "simulated error-route dispatch failure");
    }
}
