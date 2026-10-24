using ColorVision.Engine;
using ColorVision.Engine.FlowProcessing;
using ColorVision.UI;
using ColorVision.UI.Views;
using FlowEngineLib;
using FlowEngineLib.Base;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class FlowExecutionSessionProgressTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TimerUsesConfiguredDurationForProgressEvenWithoutHistoryOrAfterTimingChanges(bool hasHistory)
    {
        WithSession(fixture =>
        {
            Type type = typeof(CVCommonNode).Assembly.GetType("FlowEngineLib.simulator.Timer", true)!;
            var timer = (CVCommonNode)Activator.CreateInstance(type)!;
            timer.Create();
            type.GetProperty("Time")!.SetValue(timer, 120000);
            if (hasHistory) fixture.ExpectedDurations[timer.NodeID] = 250;
            FlowEngineNodeRunEventArgs? started = null;
            timer.nodeRunEvent += (_, args) => { started = args; fixture.StartNodeProgress(timer, args.ExpectedDurationMs); };
            var action = new CVStartCFC("timer-progress") { FlowStatus = StatusTypeEnum.Runing };
            try
            {
                timer.GetAllInputOptions()[0].ReceiveData(action);
                Assert.NotNull(started);
                Assert.Equal(120000, fixture.ExpectedDurations[timer.NodeID]);
                Assert.Equal(0f, timer.TitleProgress);
            }
            finally { action.RuntimeResources.Dispose(); }
        });
    }

    [Fact]
    public void QueuedProgressCannotOverwriteTerminalSummaryOrCompletedPercentage()
    {
        WithSession(fixture =>
        {
            fixture.Begin("SN-COMPLETE", "Old node");
            fixture.RequestProgress();
            Assert.Equal(1, fixture.PendingUiUpdate);

            // The finalizer stops timing before publishing the final summary; node-end events may still be draining.
            fixture.Stopwatch.Stop();
            fixture.Manager.FlowControl.IsFlowRun = false;
            fixture.RunningNodeNames["pending-node"] = "Old node";
            fixture.Manager.BatchProgress = 100;
            fixture.View.ShowExecutionSummary("Completed: final results");
            FlushDispatcher();

            Assert.Equal("Completed: final results", fixture.View.logTextBox.Text);
            Assert.Equal(100, fixture.Manager.BatchProgress);
            Assert.Equal(0, fixture.PendingUiUpdate);
        });
    }

    [Fact]
    public void StopRejectsQueuedProgressAndNewTicksWhileEngineIsStillStopping()
    {
        WithSession(fixture =>
        {
            fixture.Begin("SN-STOP", "Stopping node");
            fixture.RequestProgress();
            fixture.Session.StopFlow();
            string summary = fixture.View.logTextBox.Text;
            fixture.Manager.BatchProgress = 37;
            FlushDispatcher();

            Assert.Equal(summary, fixture.View.logTextBox.Text);
            Assert.Equal(37, fixture.Manager.BatchProgress);
            Assert.Equal(0, fixture.PendingUiUpdate);
            Assert.True(fixture.Manager.FlowControl.IsFlowRun);
            fixture.RequestProgress();
            Assert.Equal(0, fixture.PendingUiUpdate);
            FlushDispatcher();
            Assert.Equal(summary, fixture.View.logTextBox.Text);
        });
    }

    [Fact]
    public void QueuedOldRunProgressCannotReplaceNewRunAndDoesNotBlockItsNextUpdate()
    {
        WithSession(fixture =>
        {
            fixture.Begin("SN-OLD", "Old node");
            fixture.RequestProgress();
            fixture.Session.StopFlow();
            fixture.Lifecycle.Complete("SN-OLD");
            fixture.Manager.FlowControl.IsFlowRun = false;
            fixture.Begin("SN-NEW", "New node");
            fixture.Manager.BatchProgress = 0;
            fixture.View.ShowExecutionSummary("Run new flow");
            FlushDispatcher();

            Assert.Equal("Run new flow", fixture.View.logTextBox.Text);
            Assert.Equal(0, fixture.Manager.BatchProgress);
            Assert.Equal(0, fixture.PendingUiUpdate);

            fixture.RequestProgress();
            Assert.Equal(1, fixture.PendingUiUpdate);
            FlushDispatcher();
            Assert.Contains("New node", fixture.View.logTextBox.Text);
            Assert.DoesNotContain("Old node", fixture.View.logTextBox.Text);
        });
    }

    private static void WithSession(Action<ProgressFixture> test)
    {
        WpfTestHost.Invoke(() =>
        {
            var previousConfig = ConfigService.Instance;
            ConfigService.SetInstance(new ConfigHandler());
            var theme = new ResourceDictionary { Source = new Uri("/ColorVision.Themes;component/Themes/Theme.xaml", UriKind.Relative) };
            Application.Current.Resources.MergedDictionaries.Add(theme);
            try
            {
                using var fixture = new ProgressFixture();
                test(fixture);
            }
            finally
            {
                Application.Current.Resources.MergedDictionaries.Remove(theme);
                ConfigService.SetInstance(previousConfig);
            }
        });
    }

    private static void FlushDispatcher()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private sealed class ProgressFixture : IDisposable
    {
        private readonly List<CancellationTokenSource> cancellations = [];
        public FlowEngineManager Manager { get; } = new();
        public ViewFlow View => Manager.View;
        public FlowExecutionSession Session { get; }
        public FlowRunLifecycleGate Lifecycle => GetField<FlowRunLifecycleGate>("_runLifecycle");
        public Stopwatch Stopwatch => GetField<Stopwatch>("_stopwatch");
        public ConcurrentDictionary<string, string> RunningNodeNames => GetField<ConcurrentDictionary<string, string>>("_runningNodeNames");
        public ConcurrentDictionary<string, long> ExpectedDurations => GetField<ConcurrentDictionary<string, long>>("_nodeExpectedDurations");
        public int PendingUiUpdate => GetField<int>("_pendingUiUpdate");

        public ProgressFixture()
        {
            Session = (FlowExecutionSession)typeof(ViewFlow).GetField("_executionSession", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(View)!;
        }

        public void Begin(string serialNumber, string nodeName)
        {
            var cancellation = new CancellationTokenSource();
            cancellations.Add(cancellation);
            Assert.True(Lifecycle.TryBegin(serialNumber, cancellation, Manager.FlowControl.IsFlowRun));
            Manager.Batch = new MeasureBatchModel { Code = serialNumber };
            SetField("_lastFlowTime", 1000L);
            SetField("_currentNodeName", nodeName);
            Invoke("InvalidateExecutionPresentation");
            Stopwatch.Restart();
            Manager.FlowControl.IsFlowRun = true;
        }

        public void RequestProgress() => Invoke("UpdateRuntimeProgress", [null]);
        public void StartNodeProgress(CVCommonNode node, long? duration) => Invoke("StartNodeTitleProgress", [node, duration]);

        public void Dispose()
        {
            View.Dispose();
            Manager.DisplayFlow.Dispose();
            Manager.FlowEngineControl.Dispose();
            DockViewManager.GetInstance().RemoveView(View);
            (Manager.Copilot.GetType().GetField("_copilotAgentExtensionRegistration", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Manager.Copilot) as IDisposable)?.Dispose();
            Stopwatch.Stop();
            FlushDispatcher();
            foreach (CancellationTokenSource cancellation in cancellations)
                cancellation.Dispose();
        }

        private T GetField<T>(string name) => (T)typeof(FlowExecutionSession).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Session)!;
        private void SetField(string name, object value) => typeof(FlowExecutionSession).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Session, value);
        private void Invoke(string name, object?[]? arguments = null) => typeof(FlowExecutionSession).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(Session, arguments);
    }
}
