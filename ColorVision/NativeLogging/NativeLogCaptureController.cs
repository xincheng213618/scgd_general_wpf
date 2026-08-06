using ColorVision.Core;
using System;

namespace ColorVision.NativeLogging;

internal sealed class NativeLogCaptureController : INativeLogCaptureController
{
    public event Action<NativeLogDisplayEntry>? LogReceived;

    public NativeLogCaptureController()
    {
        NativeLogBridge.LogReceived += OnLogReceived;
    }

    public bool IsEnabled => NativeLogBridge.IsEnabled;

    public NativeLogOperationResult Start(NativeLogSeverity level)
    {
        NativeLogLevel nativeLevel = ToNativeLevel(level);
        NativeLogInitializationResult initialization = NativeLogBridge.InitializeWithResult(
            sink: null,
            level: nativeLevel,
            enableLogs: true,
            enableNativeSink: false);

        if (!initialization.AnySourceAvailable)
        {
            NativeLogBridge.SetEnabled(false);
            return NativeLogOperationResult.Failed(initialization.Summary);
        }

        if (!NativeLogBridge.IsEnabled)
        {
            return NativeLogOperationResult.Failed("The native logging API did not enter the enabled state.");
        }

        return NativeLogOperationResult.Succeeded(initialization.Summary);
    }

    public NativeLogOperationResult SetLevel(NativeLogSeverity level)
    {
        return NativeLogBridge.SetLevel(ToNativeLevel(level))
            ? NativeLogOperationResult.Succeeded()
            : NativeLogOperationResult.Failed(NativeLogBridge.LastInitializationResult.Summary);
    }

    public void Stop()
    {
        NativeLogBridge.SetEnabled(false);
    }

    public void Dispose()
    {
        NativeLogBridge.LogReceived -= OnLogReceived;
        LogReceived = null;
    }

    private void OnLogReceived(NativeLogRecord record)
    {
        NativeLogDisplayEntry entry = new(
            record.Timestamp,
            record.ManagedThreadId,
            GetSourceName(record.Source),
            ToDisplayLevel(record.Level),
            record.Message);

        try
        {
            LogReceived?.Invoke(entry);
        }
        catch
        {
            // A diagnostics consumer must never throw through the native callback.
        }
    }

    private static NativeLogLevel ToNativeLevel(NativeLogSeverity level)
    {
        return level switch
        {
            NativeLogSeverity.Trace => NativeLogLevel.Trace,
            NativeLogSeverity.Debug => NativeLogLevel.Debug,
            NativeLogSeverity.Info => NativeLogLevel.Info,
            NativeLogSeverity.Warning => NativeLogLevel.Warn,
            NativeLogSeverity.Error => NativeLogLevel.Error,
            _ => NativeLogLevel.Info,
        };
    }

    private static NativeLogSeverity ToDisplayLevel(NativeLogLevel level)
    {
        return level switch
        {
            NativeLogLevel.Trace => NativeLogSeverity.Trace,
            NativeLogLevel.Debug => NativeLogSeverity.Debug,
            NativeLogLevel.Info => NativeLogSeverity.Info,
            NativeLogLevel.Warn => NativeLogSeverity.Warning,
            NativeLogLevel.Error => NativeLogSeverity.Error,
            _ => NativeLogSeverity.Info,
        };
    }

    private static string GetSourceName(NativeLogSource source)
    {
        return source switch
        {
            NativeLogSource.OpencvHelper => "opencv_helper",
            NativeLogSource.OpencvCuda => "opencv_cuda",
            _ => "native",
        };
    }
}
