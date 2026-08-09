using System;
using System.Globalization;

namespace ColorVision.NativeLogging;

internal static class NativeLogText
{
    private static bool IsChinese => string.Equals(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, "zh", StringComparison.OrdinalIgnoreCase);

    public static string Title => IsChinese ? "原生日志" : "Native Logs";
    public static string Start => IsChinese ? "开始捕获" : "Start capture";
    public static string Stop => IsChinese ? "停止捕获" : "Stop capture";
    public static string Pause => IsChinese ? "暂停显示" : "Pause display";
    public static string Resume => IsChinese ? "继续显示" : "Resume display";
    public static string Off => IsChinese ? "捕获已关闭；Native 侧仅保留极小的开关判断。" : "Capture is off; the native side keeps only a near-zero enable check.";
    public static string Capturing => IsChinese ? "正在捕获" : "Capturing";
    public static string Paused => IsChinese ? "显示已暂停" : "Display paused";
    public static string Pending => IsChinese ? "待显示" : "pending";
    public static string Dropped => IsChinese ? "已丢弃" : "dropped";
    public static string StartFailed => IsChinese ? "无法启动 Native 日志" : "Unable to start native logging";
    public static string LevelFailed => IsChinese ? "无法更新日志级别" : "Unable to update log level";
}
