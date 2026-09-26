using ColorVision.UI.Controls;

namespace ProjectARVRPro;

public partial class ARVRWindow
{
    private void PrepareExecutionStatus()
    {
        _runningFlowNodes.Reset(null);
        Dispatcher.Invoke(() => ExecutionStatus.Status = FlowExecutionStatusInfo.Preparing(FlowName));
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
        Dispatcher.Invoke(() => ExecutionStatus.Status = status);
    }
}
