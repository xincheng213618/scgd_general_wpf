using ColorVision.UI.Controls;

namespace ProjectKB;

public partial class ProjectKBWindow
{
    private bool _hasExecutionTiming;

    private void PrepareExecutionStatus()
    {
        Msg1 = string.Empty;
        _hasExecutionTiming = false;
        ExecutionStatus.Status = FlowExecutionStatusInfo.Preparing(FlowName);
    }

    private void ShowExecutionResult(string? eventName, string? reason)
    {
        if (_isDisposed) return;
        var status = FlowExecutionStatusInfo.Finished(FlowName, eventName, reason, _hasExecutionTiming ? stopwatch.ElapsedMilliseconds : null);
        if (!string.IsNullOrWhiteSpace(Msg1))
            status = status with { Details = status.Details + $"\n最后执行节点：{Msg1}" };
        if (LastFlowTime > 0)
            status = status with { Details = status.Details + $"\n上次执行：{LastFlowTime:N0} ms" };
        Dispatcher.Invoke(() => ExecutionStatus.Status = status);
    }
}
