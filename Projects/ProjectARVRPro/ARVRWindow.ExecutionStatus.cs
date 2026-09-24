using ColorVision.UI.Controls;

namespace ProjectARVRPro;

public partial class ARVRWindow
{
    private void PrepareExecutionStatus()
    {
        Msg1 = string.Empty;
        Dispatcher.Invoke(() => ExecutionStatus.Status = FlowExecutionStatusInfo.Preparing(FlowName));
    }

    private void ShowExecutionResult(string? eventName, string? reason)
    {
        if (_isDisposed) return;
        long? elapsed = CurrentFlowResult?.FlowStartedAt != null ? stopwatch.ElapsedMilliseconds : null;
        var status = FlowExecutionStatusInfo.Finished(FlowName, eventName, reason, elapsed);
        if (!string.IsNullOrWhiteSpace(Msg1))
            status = status with { Details = status.Details + $"\n最后执行节点：{Msg1}" };
        if (LastFlowTime > 0)
            status = status with { Details = status.Details + $"\n上次执行：{LastFlowTime:N0} ms" };
        Dispatcher.Invoke(() => ExecutionStatus.Status = status);
    }
}
