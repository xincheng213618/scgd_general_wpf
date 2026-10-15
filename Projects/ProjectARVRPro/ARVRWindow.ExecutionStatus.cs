using ColorVision.UI.Controls;
using System.ComponentModel;

namespace ProjectARVRPro;

public partial class ARVRWindow
{
    private void FlowControl_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(flowControl.IsFlowRun))
            UpdateTestButtonState();
    }

    private void UpdateTestButtonState()
    {
        if (_isDisposed || _isRunAllRunning) return;
        Dispatcher.Invoke(() =>
        {
            bool canStop = flowControl.IsFlowRun;
            TestButton.Content = DisplayText.Get(canStop ? "停止" : "测试");
            TestButton.IsEnabled = !_isFlowCompletionPending
                && (canStop || !_isFlowStartPending && !_isFlowLifecycleActive && !IsResultExportPending);
        });
    }

    private void PrepareExecutionStatus()
    {
        _runningFlowNodes.Reset(null);
        Dispatcher.Invoke(() =>
        {
            ExecutionStatus.Status = FlowExecutionStatusInfo.Preparing(FlowName);
            SetStepProgress(CurrentFlowResult?.TestType ?? -1);
        });
    }

    private void ShowExecutionResult(string? eventName, string? reason)
    {
        if (_isDisposed) return;
        string lastStartedNode = _runningFlowNodes.CompleteRun();
        long? elapsed = CurrentFlowResult?.FlowStartedAt != null ? stopwatch.ElapsedMilliseconds : null;
        var status = FlowExecutionStatusInfo.Finished(FlowName, eventName, reason, elapsed);
        if (!string.IsNullOrWhiteSpace(lastStartedNode))
            status = status with { Details = status.Details + $"\n最后执行节点：{lastStartedNode}" };
        if (LastFlowTime > 0)
            status = status with { Details = status.Details + $"\n上次执行：{LastFlowTime:N0} ms" };
        Dispatcher.Invoke(() =>
        {
            ExecutionStatus.Status = status;
            int stepIndex = Process.ProcessManager.GetEnabledStepIndex(ProcessMetas, CurrentFlowResult?.TestType ?? -1);
            stepBar.Finish(stepIndex, eventName == "Completed");
        });
    }
}
