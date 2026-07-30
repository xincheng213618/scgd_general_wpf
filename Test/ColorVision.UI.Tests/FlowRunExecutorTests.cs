#pragma warning disable CA1707
using ColorVision.Engine.FlowProcessing;
using ColorVision.Engine.MQTT;
using FlowEngineLib;
using FlowEngineLib.Base;
using FlowEngineLib.Start;
using ST.Library.UI.NodeEditor;
using System.Runtime.ExceptionServices;

namespace ColorVision.UI.Tests;

public class FlowRunExecutorTests
{
    [Fact]
    public void EngineCompletionReturnsTheMatchingResult()
    {
        RunInSta(() =>
        {
            using var fixture = new FlowFixture();
            TestStartNode start = fixture.AddConnectedStart("Selected");

            Task<FlowRunExecutionResult> execution = fixture.Executor.RunAsync(
                "Selected",
                "SN-Completed",
                executionTimeout: null);
            Assert.False(execution.IsCompleted);

            start.Complete("SN-Completed", StatusTypeEnum.Completed);
            FlowRunExecutionResult result = execution.WaitAsync(TimeSpan.FromSeconds(2))
                .GetAwaiter()
                .GetResult();

            Assert.True(result.Started);
            Assert.Equal(FlowRunTermination.EngineCompleted, result.Termination);
            Assert.Equal(StatusTypeEnum.Completed, result.Data.Status);
            Assert.Equal("Selected", result.Data.StartNodeName);
            Assert.Equal("SN-Completed", result.Data.SerialNumber);
            Assert.False(fixture.FlowControl.IsFlowRun);
        });
    }

    [Fact]
    public void CancellationAfterReadinessButBeforeStartDoesNotStartTheFlow()
    {
        RunInSta(() =>
        {
            using var fixture = new FlowFixture();
            using var cancellation = new CancellationTokenSource();
            TestStartNode start = fixture.AddConnectedStart("Selected");
            start.RequiresReady = true;
            start.EnsureReadyHandler = _ =>
            {
                start.Ready = true;
                cancellation.Cancel();
                return Task.FromResult(true);
            };

            FlowRunExecutionResult result = fixture.Executor.RunAsync(
                    "Selected",
                    "SN-Canceled-Before-Start",
                    executionTimeout: null,
                    cancellation.Token)
                .WaitAsync(TimeSpan.FromSeconds(2))
                .GetAwaiter()
                .GetResult();

            Assert.False(result.Started);
            Assert.Equal(FlowRunTermination.Canceled, result.Termination);
            Assert.Equal(StatusTypeEnum.Canceled, result.Data.Status);
            Assert.Equal(0, start.ActiveCount);
            Assert.False(fixture.FlowControl.IsFlowRun);
            Assert.False(fixture.Engine.IsRunning);
        });
    }

    [Fact]
    public void CancellationStopsTheSelectedStartNode()
    {
        RunInSta(() =>
        {
            using var fixture = new FlowFixture();
            TestStartNode first = fixture.AddConnectedStart("First");
            TestStartNode selected = fixture.AddConnectedStart("Selected");
            using var cancellation = new CancellationTokenSource();

            Task<FlowRunExecutionResult> execution = fixture.Executor.RunAsync(
                "Selected",
                "SN-Canceled",
                executionTimeout: null,
                cancellation.Token);
            Assert.Equal(1, selected.ActiveCount);
            Assert.Equal(0, first.ActiveCount);

            cancellation.Cancel();
            FlowRunExecutionResult result = execution.WaitAsync(TimeSpan.FromSeconds(2))
                .GetAwaiter()
                .GetResult();

            Assert.True(result.Started);
            Assert.Equal(FlowRunTermination.Canceled, result.Termination);
            Assert.Equal(StatusTypeEnum.Canceled, result.Data.Status);
            Assert.Equal(0, selected.ActiveCount);
            Assert.Equal(0, first.ActiveCount);
            Assert.False(fixture.FlowControl.IsFlowRun);
            Assert.False(fixture.Engine.IsRunning);
        });
    }

    [Fact]
    public void ExecutionTimeoutStopsTheActiveRun()
    {
        RunInSta(() =>
        {
            using var fixture = new FlowFixture();
            TestStartNode start = fixture.AddConnectedStart("Selected");

            FlowRunExecutionResult result = fixture.Executor.RunAsync(
                    "Selected",
                    "SN-Timeout",
                    TimeSpan.FromMilliseconds(100))
                .WaitAsync(TimeSpan.FromSeconds(2))
                .GetAwaiter()
                .GetResult();

            Assert.True(result.Started);
            Assert.Equal(FlowRunTermination.TimedOut, result.Termination);
            Assert.Equal(StatusTypeEnum.OverTime, result.Data.Status);
            Assert.Equal(0, start.ActiveCount);
            Assert.False(fixture.FlowControl.IsFlowRun);
            Assert.False(fixture.Engine.IsRunning);
        });
    }

    [Fact]
    public void RejectedStartDoesNotLeakIntoTheNextRun()
    {
        RunInSta(() =>
        {
            using var fixture = new FlowFixture();
            TestStartNode start = fixture.AddStart("Selected");

            FlowRunExecutionResult rejected = fixture.Executor.RunAsync(
                    "Selected",
                    "SN-Rejected",
                    executionTimeout: null)
                .WaitAsync(TimeSpan.FromSeconds(2))
                .GetAwaiter()
                .GetResult();

            Assert.False(rejected.Started);
            Assert.Equal(FlowRunTermination.StartRejected, rejected.Termination);

            fixture.Connect(start);
            Task<FlowRunExecutionResult> execution = fixture.Executor.RunAsync(
                "Selected",
                "SN-Next",
                executionTimeout: null);
            start.Complete("SN-Next", StatusTypeEnum.Completed);
            FlowRunExecutionResult completed = execution.WaitAsync(TimeSpan.FromSeconds(2))
                .GetAwaiter()
                .GetResult();

            Assert.True(completed.Started);
            Assert.Equal(FlowRunTermination.EngineCompleted, completed.Termination);
            Assert.Equal("SN-Next", completed.Data.SerialNumber);
        });
    }

    [Fact]
    public void ConcurrentRunIsRejectedWithoutStealingTheActiveCompletion()
    {
        RunInSta(() =>
        {
            using var fixture = new FlowFixture();
            TestStartNode start = fixture.AddConnectedStart("Selected");

            Task<FlowRunExecutionResult> firstExecution = fixture.Executor.RunAsync(
                "Selected",
                "SN-First",
                executionTimeout: null);
            FlowRunExecutionResult rejected = fixture.Executor.RunAsync(
                    "Selected",
                    "SN-Second",
                    executionTimeout: null)
                .WaitAsync(TimeSpan.FromSeconds(2))
                .GetAwaiter()
                .GetResult();

            Assert.False(rejected.Started);
            Assert.Equal(FlowRunTermination.StartRejected, rejected.Termination);

            start.Complete("SN-First", StatusTypeEnum.Completed);
            FlowRunExecutionResult completed = firstExecution.WaitAsync(TimeSpan.FromSeconds(2))
                .GetAwaiter()
                .GetResult();
            Assert.Equal(FlowRunTermination.EngineCompleted, completed.Termination);
            Assert.Equal("SN-First", completed.Data.SerialNumber);
        });
    }

    [Fact]
    public void LateCompletionFromCanceledRunCannotCompleteTheNextRun()
    {
        RunInSta(() =>
        {
            using var fixture = new FlowFixture();
            TestStartNode start = fixture.AddConnectedStart("Selected");
            using var cancellation = new CancellationTokenSource();

            Task<FlowRunExecutionResult> canceledExecution = fixture.Executor.RunAsync(
                "Selected",
                "SN-Old",
                executionTimeout: null,
                cancellation.Token);
            cancellation.Cancel();
            FlowRunExecutionResult canceled = canceledExecution.WaitAsync(TimeSpan.FromSeconds(2))
                .GetAwaiter()
                .GetResult();
            Assert.Equal(FlowRunTermination.Canceled, canceled.Termination);

            Task<FlowRunExecutionResult> nextExecution = fixture.Executor.RunAsync(
                "Selected",
                "SN-New",
                executionTimeout: null);
            start.RaiseFinished("SN-Old", StatusTypeEnum.Completed);
            Assert.False(nextExecution.IsCompleted);

            start.Complete("SN-New", StatusTypeEnum.Completed);
            FlowRunExecutionResult completed = nextExecution.WaitAsync(TimeSpan.FromSeconds(2))
                .GetAwaiter()
                .GetResult();
            Assert.Equal(FlowRunTermination.EngineCompleted, completed.Termination);
            Assert.Equal("SN-New", completed.Data.SerialNumber);
        });
    }

    [Fact]
    public void ThrowingSubscriberCannotPreventExecutorCompletion()
    {
        RunInSta(() =>
        {
            using var fixture = new FlowFixture();
            TestStartNode start = fixture.AddConnectedStart("Selected");
            int throwingSubscriberCalls = 0;
            fixture.FlowControl.FlowCompleted += (_, _) =>
            {
                throwingSubscriberCalls++;
                throw new InvalidOperationException("subscriber failure");
            };

            Task<FlowRunExecutionResult> execution = fixture.Executor.RunAsync(
                "Selected",
                "SN-Subscriber",
                executionTimeout: null);
            start.Complete("SN-Subscriber", StatusTypeEnum.Completed);

            FlowRunExecutionResult result = execution.WaitAsync(TimeSpan.FromSeconds(2))
                .GetAwaiter()
                .GetResult();

            Assert.Equal(1, throwingSubscriberCalls);
            Assert.Equal(FlowRunTermination.EngineCompleted, result.Termination);
            Assert.Equal("SN-Subscriber", result.Data.SerialNumber);
        });
    }

    private static void RunInSta(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        })
        {
            IsBackground = true
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)), "STA test thread did not finish.");
        if (exception != null)
            ExceptionDispatchInfo.Capture(exception).Throw();
    }

    private sealed class FlowFixture : IDisposable
    {
        private readonly FlowNodeManager _nodeManager = new();

        public FlowFixture()
        {
            Editor = new STNodeEditor();
            Engine = new FlowEngineControl(Editor, false, _nodeManager);
            FlowControl = new FlowControl(MQTTControl.GetInstance(), Engine, () => []);
            Executor = new FlowRunExecutor(FlowControl);
        }

        public STNodeEditor Editor { get; }

        public FlowEngineControl Engine { get; }

        public FlowControl FlowControl { get; }

        public FlowRunExecutor Executor { get; }

        public TestStartNode AddStart(string name)
        {
            var start = new TestStartNode(name);
            start.Create();
            Editor.Nodes.Add(start);
            return start;
        }

        public TestStartNode AddConnectedStart(string name)
        {
            TestStartNode start = AddStart(name);
            Connect(start);
            return start;
        }

        public void Connect(TestStartNode start)
        {
            var sink = new StartSinkNode();
            sink.Create();
            Editor.Nodes.Add(sink);
            Assert.Equal(ConnectionStatus.Connected, start.StartOutput.ConnectOption(sink.Input));
        }

        public void Dispose()
        {
            FlowControl.Stop();
            Engine.Dispose();
            Editor.Dispose();
        }
    }

    private sealed class TestStartNode : BaseStartNode
    {
        public TestStartNode(string name)
            : base("Test start")
        {
            NodeName = name;
        }

        public int ActiveCount => startActions.Count;

        public STNodeOption StartOutput => m_op_start;

        public bool RequiresReady { get; set; }

        public Func<CancellationToken, Task<bool>>? EnsureReadyHandler { get; set; }

        public override bool RequiresConnectionReady => RequiresReady;

        public override Task<bool> EnsureReadyAsync(CancellationToken cancellationToken = default)
        {
            return EnsureReadyHandler?.Invoke(cancellationToken)
                ?? base.EnsureReadyAsync(cancellationToken);
        }

        public void Complete(string serialNumber, StatusTypeEnum status)
        {
            CVStartCFC action = GetCFC(serialNumber)
                ?? throw new InvalidOperationException($"No active flow exists for {serialNumber}.");
            action.SetStatusType(status);
            action.DoFinishing();
            action.FireFinished();
        }

        public void RaiseFinished(string serialNumber, StatusTypeEnum status)
        {
            var action = new CVStartCFC(this, ActionTypeEnum.Start, serialNumber);
            action.SetStatusType(status);
            action.FireFinished();
        }
    }

    private sealed class StartSinkNode : STNode
    {
        public STNodeOption Input { get; private set; } = null!;

        protected override void OnCreate()
        {
            base.OnCreate();
            Input = InputOptions.Add("IN", typeof(CVStartCFC), true);
        }
    }
}
