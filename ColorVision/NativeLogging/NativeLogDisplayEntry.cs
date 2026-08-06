using ColorVision.UI.LogImp.Models;
using System;

namespace ColorVision.NativeLogging;

internal enum NativeLogSeverity
{
    Trace = 0,
    Debug = 1,
    Info = 2,
    Warning = 3,
    Error = 4,
}

internal readonly record struct NativeLogDisplayEntry(
    DateTimeOffset Timestamp,
    int ManagedThreadId,
    string Source,
    NativeLogSeverity Level,
    string Message)
{
    public LogEntry ToLogEntry()
    {
        string text = $"{Timestamp:yyyy-MM-dd HH:mm:ss,fff} [{ManagedThreadId}] {Level,-7} {Source}  {Message}";
        return new LogEntry(text, ToLogEntryLevel(Level));
    }

    private static LogEntryLevel ToLogEntryLevel(NativeLogSeverity level)
    {
        return level switch
        {
            NativeLogSeverity.Trace => LogEntryLevel.Trace,
            NativeLogSeverity.Debug => LogEntryLevel.Debug,
            NativeLogSeverity.Info => LogEntryLevel.Info,
            NativeLogSeverity.Warning => LogEntryLevel.Warning,
            NativeLogSeverity.Error => LogEntryLevel.Error,
            _ => LogEntryLevel.Unknown,
        };
    }
}
