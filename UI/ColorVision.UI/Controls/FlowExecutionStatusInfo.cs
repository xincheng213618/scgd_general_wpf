using System.Globalization;
using System.Text.RegularExpressions;
using Resources = ColorVision.UI.Properties.Resources;

namespace ColorVision.UI.Controls;

public enum FlowExecutionStatusKind { Idle, Running, Completed, Failed, Canceled }

public sealed record FlowExecutionStatusInfo(
    FlowExecutionStatusKind Kind, string Label, string Message, string ElapsedText, string Details)
{
    public static FlowExecutionStatusInfo Idle => new(FlowExecutionStatusKind.Idle, Resources.FlowStatusIdle, Resources.FlowStatusWaiting, "", Resources.FlowStatusNotStarted);

    public static FlowExecutionStatusInfo Notice(string message, bool isError = false) => new(
        isError ? FlowExecutionStatusKind.Failed : FlowExecutionStatusKind.Idle,
        isError ? Resources.FlowStatusFailed : Resources.FlowStatusNotice, SingleLine(message), "", message);

    public FlowExecutionStatusInfo WithAdditionalMessage(string source, string message) => this with
    {
        Message = string.Format(CultureInfo.CurrentCulture, Resources.FlowStatusAdditionalMessage, Message, source, SingleLine(message)),
        Details = string.Format(CultureInfo.CurrentCulture, Resources.FlowStatusAdditionalDetails, Details, source, message),
    };

    public static FlowExecutionStatusInfo Preparing(string? flowName) => new(
        FlowExecutionStatusKind.Running, Resources.FlowStatusPreparing, Resources.FlowStatusPreparingMessage, "",
        string.Format(CultureInfo.CurrentCulture, Resources.FlowStatusPreparingDetails, flowName));

    public static FlowExecutionStatusInfo Running(string? flowName, string? nodeName, long elapsedMilliseconds, long lastMilliseconds)
    {
        string message = string.IsNullOrWhiteSpace(nodeName) ? Resources.FlowStatusRunningMessage : string.Format(CultureInfo.CurrentCulture, Resources.FlowStatusExecutingNode, SingleLine(nodeName));
        string details = string.Format(CultureInfo.CurrentCulture, Resources.FlowStatusRunningDetails, flowName, nodeName, elapsedMilliseconds);
        if (lastMilliseconds > 0)
        {
            details += string.Format(CultureInfo.CurrentCulture, Resources.FlowStatusLastExecution, lastMilliseconds);
            if (lastMilliseconds > elapsedMilliseconds)
                details += string.Format(CultureInfo.CurrentCulture, Resources.FlowStatusRemaining, lastMilliseconds - elapsedMilliseconds);
        }
        return new(FlowExecutionStatusKind.Running, Resources.FlowStatusRunning, message, $"{Math.Max(0, elapsedMilliseconds)} ms", details);
    }

    public static FlowExecutionStatusInfo Finished(string? flowName, string? eventName, string? reason, long? elapsedMilliseconds)
    {
        var (kind, label, fallback) = eventName switch
        {
            "Completed" => (FlowExecutionStatusKind.Completed, Resources.FlowStatusCompleted, Resources.FlowStatusCompletedMessage),
            "OverTime" => (FlowExecutionStatusKind.Failed, Resources.FlowStatusTimedOut, Resources.FlowStatusTimedOutMessage),
            "Canceled" => (FlowExecutionStatusKind.Canceled, Resources.FlowStatusCanceled, Resources.FlowStatusCanceledMessage),
            _ => (FlowExecutionStatusKind.Failed, Resources.FlowStatusFailed, Resources.FlowStatusFailedMessage),
        };
        string message = kind == FlowExecutionStatusKind.Completed || string.IsNullOrWhiteSpace(reason) || reason == eventName ? fallback : reason.Trim();
        message = message switch
        {
            "PictureSwitchFailed" => Resources.FlowStatusPictureSwitchFailed,
            "PreProcessFailed" => Resources.FlowStatusPreprocessFailed,
            "FlowStartRejected" => Resources.FlowStatusStartRejected,
            _ => message,
        };
        string elapsedText = elapsedMilliseconds.HasValue ? $"{Math.Max(0, elapsedMilliseconds.Value)} ms" : "";
        string details = string.Format(CultureInfo.CurrentCulture, Resources.FlowStatusFinishedDetails, flowName, label, eventName, message);
        if (!string.IsNullOrWhiteSpace(reason) && reason.Trim() != message)
            details += string.Format(CultureInfo.CurrentCulture, Resources.FlowStatusOriginalMessage, reason);
        if (elapsedMilliseconds.HasValue)
            details += string.Format(CultureInfo.CurrentCulture, Resources.FlowStatusElapsed, elapsedMilliseconds.Value);
        return new(kind, label, SingleLine(message), elapsedText, details);
    }

    private static string SingleLine(string text) => Regex.Replace(text.Trim(), @"\s+", " ");
}
