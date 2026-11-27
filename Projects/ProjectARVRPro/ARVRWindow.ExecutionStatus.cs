using ColorVision.Engine.FlowProcessing;
using ColorVision.UI.Controls;
using System.ComponentModel;
using System.Windows;
using System.Windows.Documents;

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
            bool deviceRunning = IsJsonVidRunning || _luxSession?.Request.SocketCode is "01" or "31";
            ExecutionToolbar.IsEnabled = !deviceRunning;
            ConfigurationActions.IsEnabled = !deviceRunning;
            bool canStop = flowControl.IsFlowRun;
            TestButton.Content = DisplayText.Get(canStop ? "停止" : "测试");
            TestButton.IsEnabled = !IsJsonVidRunning && !_isFlowCompletionPending
                && (canStop || _luxSession == null && !_isFlowStartPending && !_isFlowLifecycleActive && !IsResultExportPending);
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
        if (eventName == "Failed" && GetGridRecognitionMessage(reason) is { } recognitionMessage)
            status = status with { Message = recognitionMessage, Details = $"{recognitionMessage}\n{status.Details}" };
        if (!string.IsNullOrWhiteSpace(lastStartedNode))
            status = status with { Details = status.Details + $"\n最后执行节点：{lastStartedNode}" };
        if (LastFlowTime > 0)
            status = status with { Details = status.Details + $"\n上次执行：{LastFlowTime:N0} ms" };
        Dispatcher.Invoke(() =>
        {
            ExecutionStatus.Status = status;
            int stepIndex = Process.ProcessManager.GetEnabledStepIndex(ProcessMetas, CurrentFlowResult?.TestType ?? -1);
            stepBar.Finish(stepIndex, eventName == "Completed", status.Message);
        });
    }

    internal static Paragraph? CreateFailedResultParagraph(ProjectARVRReuslt result)
    {
        string? reason = result.Msg?.Trim();
        string? eventName = result.FlowStatus switch
        {
            FlowStatus.OverTime => "OverTime",
            FlowStatus.Canceled => "Canceled",
            FlowStatus.Failed when reason == "测试已取消" => "Canceled",
            FlowStatus.Failed => "Failed",
            _ => null,
        };
        if (eventName == null)
            return null;

        var status = FlowExecutionStatusInfo.Finished(result.Model, eventName, reason, null);
        string? recognitionMessage = eventName == "Failed" ? GetGridRecognitionMessage(reason) : null;
        var heading = new Run(recognitionMessage ?? status.Label) { FontWeight = FontWeights.SemiBold, FontSize = 16 };
        heading.SetResourceReference(TextElement.ForegroundProperty, "DangerBrush");
        var paragraph = new Paragraph(heading);
        paragraph.Inlines.Add(new LineBreak());
        paragraph.Inlines.Add(new Run(DisplayText.Format($"流程 {result.Model} · SN {result.SN} · {result.CreateTime:yyyy/MM/dd HH:mm:ss}")));
        paragraph.Inlines.Add(new LineBreak());
        paragraph.Inlines.Add(new LineBreak());

        string detail = reason switch
        {
            null or "" or "ARVR Test Fail" => DisplayText.Get("该记录未保存具体原因。"),
            "PictureSwitchFailed" or "PreProcessFailed" or "FlowStartRejected" or "测试已取消" => status.Message,
            _ when reason == eventName => DisplayText.Get("该记录未保存具体原因。"),
            _ => reason,
        };
        if (recognitionMessage != null)
        {
            paragraph.Inlines.Add(new Run(DisplayText.Get("原始错误")) { FontWeight = FontWeights.SemiBold });
            paragraph.Inlines.Add(new LineBreak());
        }
        paragraph.Inlines.Add(new Run(detail));
        return paragraph;
    }

    private static string? GetGridRecognitionMessage(string? reason) =>
        reason?.Split(['：', ':', '；', ';'], 3, StringSplitOptions.TrimEntries) is ["点阵畸变失败", "grid_not_found", ..]
            ? DisplayText.Get("点阵画面识别失败，未找到符合要求的完整点阵。")
            : null;
}
