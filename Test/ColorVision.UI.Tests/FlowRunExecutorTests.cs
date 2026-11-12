#pragma warning disable CA1707
using ColorVision.Engine.FlowProcessing;
using ColorVision.Engine.MQTT;
using FlowEngineLib;
using FlowEngineLib.Base;
using FlowEngineLib.Start;
using ST.Library.UI.NodeEditor;
using System.Globalization;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public class FlowRunExecutorTests
{
    [Theory]
    [InlineData("SN-Completed", "SN-Completed")]
    [InlineData("SN?Completed", "SN_Completed")]
    public void EngineCompletionReturnsTheMatchingResult(string requestedSerialNumber, string executionSerialNumber)
    {
        RunInSta(() =>
        {
            using var fixture = new FlowFixture();
            TestStartNode start = fixture.AddConnectedStart("Selected");

            Task<FlowRunExecutionResult> execution = fixture.Executor.RunAsync(
                "Selected",
                requestedSerialNumber,
                executionTimeout: null);
            Assert.False(execution.IsCompleted);

            Assert.Equal(executionSerialNumber, fixture.FlowControl.SerialNumber);
            start.Complete(executionSerialNumber, StatusTypeEnum.Completed);
            FlowRunExecutionResult result = execution.WaitAsync(TimeSpan.FromSeconds(2))
                .GetAwaiter()
                .GetResult();

            Assert.True(result.Started);
            Assert.Equal(FlowRunTermination.EngineCompleted, result.Termination);
            Assert.Equal(StatusTypeEnum.Completed, result.Data.Status);
            Assert.Equal("Selected", result.Data.StartNodeName);
            Assert.Equal(executionSerialNumber, result.Data.SerialNumber);
            Assert.False(fixture.FlowControl.IsFlowRun);
        });
    }

    [Fact]
    public void CompletedEngineResultSurvivesStopBeforeTheUiNotification()
    {
        WpfTestHost.Invoke(() =>
        {
            using var fixture = new FlowFixture();
            using var cancellation = new CancellationTokenSource();
            TestStartNode start = fixture.AddConnectedStart("Selected");
            Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
            FlowControlData? notification = null;
            bool notifiedOnUi = false;
            fixture.FlowControl.FlowCompleted += (_, data) =>
            {
                notification = data;
                notifiedOnUi = dispatcher.CheckAccess();
            };

            const string serialNumber = "SN-Terminal-Before-Stop";
            Task<FlowRunExecutionResult> execution = fixture.Executor.RunAsync(
                "Selected", serialNumber, executionTimeout: null, cancellation.Token);
            Assert.True(fixture.FlowControl.IsFlowRun);

            // Keep the UI dispatcher occupied until the worker reports the terminal result.
            Task.Run(() => start.Complete(serialNumber, StatusTypeEnum.Failed))
                .WaitAsync(TimeSpan.FromSeconds(2)).GetAwaiter().GetResult();
            Assert.False(fixture.FlowControl.IsFlowRun);
            Assert.Null(notification);

            cancellation.Cancel();
            FlowRunExecutionResult result = execution.WaitAsync(TimeSpan.FromSeconds(2)).GetAwaiter().GetResult();
            Assert.Equal(FlowRunTermination.EngineCompleted, result.Termination);
            Assert.Equal(StatusTypeEnum.Failed, result.Data.Status);

            var frame = new DispatcherFrame();
            dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
            Assert.True(notifiedOnUi);
            Assert.Same(result.Data, notification);
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
    public void UnsupportedExecutionTimeoutIsRejectedBeforeTheGraphStarts()
    {
        RunInSta(() =>
        {
            using var fixture = new FlowFixture();
            TestStartNode start = fixture.AddConnectedStart("Selected");

            Assert.Throws<ArgumentOutOfRangeException>(() => fixture.Executor.RunAsync(
                "Selected", "SN-Oversized", TimeSpan.FromSeconds(4_294_968)).GetAwaiter().GetResult());

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
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("zh-Hans");
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
            Assert.Equal("未能从“Selected”启动流程。请检查该起点是否已连接后续节点、流程是否仍在运行，以及设备或服务连接是否就绪。", rejected.Data.Message);
            Assert.Equal(rejected.Data.Message, rejected.Data.Params);

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
    public void ServiceLookupFailureDoesNotLeaveTheFlowRunningOrBlockRetry()
    {
        RunInSta(() =>
        {
            var lookupError = new InvalidOperationException("service registry unavailable");
            int attempts = 0;
            using var fixture = new FlowFixture(() => ++attempts == 1 ? throw lookupError : []);
            TestStartNode start = fixture.AddConnectedStart("Selected");

            Assert.Same(lookupError, Assert.Throws<InvalidOperationException>(() => fixture.Executor.RunAsync(
                "Selected", "SN-Unavailable", executionTimeout: null).GetAwaiter().GetResult()));
            Assert.False(fixture.FlowControl.IsFlowRun);
            Assert.Null(fixture.FlowControl.SerialNumber);
            Assert.False(fixture.Engine.IsRunning);
            Assert.Equal(0, start.ActiveCount);

            Task<FlowRunExecutionResult> retry = fixture.Executor.RunAsync("Selected", "SN-Retry", executionTimeout: null);
            Assert.Equal(1, start.ActiveCount);
            start.Complete("SN-Retry", StatusTypeEnum.Completed);
            FlowRunExecutionResult result = retry.WaitAsync(TimeSpan.FromSeconds(2)).GetAwaiter().GetResult();
            Assert.Equal(FlowRunTermination.EngineCompleted, result.Termination);
            Assert.Equal("SN-Retry", result.Data.SerialNumber);
            Assert.False(fixture.FlowControl.IsFlowRun);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StartupDispatchFailureReleasesTheRunAndPreservesTheOriginalException(bool cleanupThrows)
    {
        RunInSta(() =>
        {
            using var fixture = new FlowFixture();
            TestStartNode start = fixture.AddStart("Selected");
            StartSinkNode sink = fixture.Connect(start);
            using var resource = new MemoryStream();
            var startupError = new InvalidOperationException("downstream startup failed");
            bool failStartup = true;
            int rejectedTransfers = 0;
            sink.Input.DataTransfer += (_, args) =>
            {
                if (args.TargetOption.Data is not CVStartCFC action)
                    return;
                if (action.IsRunning && failStartup)
                {
                    action.RuntimeResources.Set("probe", resource);
                    throw startupError;
                }
                if (action.FlowStatus == StatusTypeEnum.Failed)
                {
                    rejectedTransfers++;
                    if (cleanupThrows)
                        throw new InvalidOperationException("downstream cleanup failed");
                }
            };
            const string serial = "SN-DISPATCH-RETRY";

            Assert.Same(startupError, Assert.Throws<InvalidOperationException>(() => fixture.Executor.RunAsync(
                "Selected", serial, executionTimeout: null).GetAwaiter().GetResult()));

            Assert.False(fixture.Engine.IsRunning);
            Assert.False(fixture.FlowControl.IsFlowRun);
            Assert.Null(fixture.FlowControl.SerialNumber);
            Assert.Equal(0, start.ActiveCount);
            Assert.False(resource.CanRead);
            Assert.Equal(1, rejectedTransfers);

            failStartup = false;
            Task<FlowRunExecutionResult> retry = fixture.Executor.RunAsync("Selected", serial, executionTimeout: null);
            start.Complete(serial, StatusTypeEnum.Completed);
            Assert.Equal(FlowRunTermination.EngineCompleted, retry.WaitAsync(TimeSpan.FromSeconds(2)).GetAwaiter().GetResult().Termination);
        });
    }

    [Fact]
    public void ConcurrentRunIsRejectedWithoutStealingTheActiveCompletion()
    {
        RunInSta(() =>
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
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
            Assert.Equal("Could not start the flow from “Selected”: a flow is already running. Wait for it to finish or stop it before trying again.", rejected.Data.Message);

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

        public FlowFixture(Func<List<MQTTServiceInfo>>? serviceTokensProvider = null)
        {
            Editor = new STNodeEditor();
            Engine = new FlowEngineControl(Editor, false, _nodeManager);
            FlowControl = new FlowControl(MQTTControl.GetInstance(), Engine, serviceTokensProvider ?? (() => []));
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

        public StartSinkNode Connect(TestStartNode start)
        {
            var sink = new StartSinkNode();
            sink.Create();
            Editor.Nodes.Add(sink);
            Assert.Equal(ConnectionStatus.Connected, start.StartOutput.ConnectOption(sink.Input));
            return sink;
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
