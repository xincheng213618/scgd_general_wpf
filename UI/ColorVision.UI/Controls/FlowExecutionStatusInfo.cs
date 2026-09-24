using System.Globalization;
using System.Text.RegularExpressions;

namespace ColorVision.UI.Controls;

public enum FlowExecutionStatusKind { Idle, Running, Completed, Failed, Canceled }

public sealed record FlowExecutionStatusInfo(
    FlowExecutionStatusKind Kind, string Label, string Message, string ElapsedText, string Details)
{
    public static FlowExecutionStatusInfo Idle { get; } = new(FlowExecutionStatusKind.Idle, "待执行", "等待开始测试", "", "尚未开始测试。");

    public static FlowExecutionStatusInfo Notice(string message, bool isError = false) => new(
        isError ? FlowExecutionStatusKind.Failed : FlowExecutionStatusKind.Idle,
        isError ? "失败" : "提示", SingleLine(message), "", message);

    public FlowExecutionStatusInfo WithAdditionalMessage(string source, string message) => this with
    {
        Message = $"{Message} · {source}：{SingleLine(message)}",
        Details = $"{Details}\n{source}：{message}",
    };

    public static FlowExecutionStatusInfo Preparing(string? flowName) => new(
        FlowExecutionStatusKind.Running, "准备中", "正在准备流程", "", $"流程：{flowName}\n正在准备流程与预处理。");

    public static FlowExecutionStatusInfo Running(string? flowName, string? nodeName, long elapsedMilliseconds, long lastMilliseconds)
    {
        string message = string.IsNullOrWhiteSpace(nodeName) ? "正在执行流程" : $"正在执行：{SingleLine(nodeName)}";
        string details = $"流程：{flowName}\n当前节点：{nodeName}\n已用时间：{elapsedMilliseconds:N0} ms";
        if (lastMilliseconds > 0)
        {
            details += $"\n上次执行：{lastMilliseconds:N0} ms";
            if (lastMilliseconds > elapsedMilliseconds)
                details += $"\n预计剩余：{lastMilliseconds - elapsedMilliseconds:N0} ms（参考上次执行）";
        }
        return new(FlowExecutionStatusKind.Running, "运行中", message, $"已用 {Math.Max(0, elapsedMilliseconds)} ms", details);
    }

    public static FlowExecutionStatusInfo Finished(string? flowName, string? eventName, string? reason, long? elapsedMilliseconds)
    {
        var (kind, label, fallback) = eventName switch
        {
            "Completed" => (FlowExecutionStatusKind.Completed, "完成", "流程执行完成"),
            "OverTime" => (FlowExecutionStatusKind.Failed, "超时", "流程执行超时"),
            "Canceled" => (FlowExecutionStatusKind.Canceled, "已取消", "测试已取消"),
            _ => (FlowExecutionStatusKind.Failed, "失败", "流程执行失败"),
        };
        string message = kind == FlowExecutionStatusKind.Completed || string.IsNullOrWhiteSpace(reason) || reason == eventName ? fallback : reason.Trim();
        message = message switch
        {
            "PictureSwitchFailed" => "切图失败",
            "PreProcessFailed" => "预处理失败",
            "FlowStartRejected" => "流程未能启动",
            _ => message,
        };
        string elapsedText = elapsedMilliseconds.HasValue ? $"用时 {Math.Max(0, elapsedMilliseconds.Value)} ms" : "";
        string details = $"流程：{flowName}\n状态：{label}（{eventName}）\n提示：{message}";
        if (!string.IsNullOrWhiteSpace(reason) && reason.Trim() != message)
            details += $"\n原始提示：{reason}";
        if (elapsedMilliseconds.HasValue)
            details += $"\n执行耗时：{elapsedMilliseconds.Value.ToString("N0", CultureInfo.CurrentCulture)} ms";
        return new(kind, label, SingleLine(message), elapsedText, details);
    }

    private static string SingleLine(string text) => Regex.Replace(text.Trim(), @"\s+", " ");
}
