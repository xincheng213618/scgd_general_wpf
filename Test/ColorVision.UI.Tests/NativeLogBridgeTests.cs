#pragma warning disable CA1707
using ColorVision.Core;
using System.Reflection;
using System.Runtime.InteropServices;

namespace ColorVision.UI.Tests;

public sealed class NativeLogBridgeTests
{
    [Fact]
    public void Initialize_DefaultsToDisabled()
    {
        MethodInfo initialize = typeof(NativeLogBridge).GetMethod(
            nameof(NativeLogBridge.Initialize),
            BindingFlags.Public | BindingFlags.Static)
            ?? throw new MissingMethodException(nameof(NativeLogBridge), nameof(NativeLogBridge.Initialize));

        ParameterInfo enableLogs = initialize.GetParameters()
            .Single(parameter => parameter.Name == "enableLogs");

        Assert.True(enableLogs.HasDefaultValue);
        Assert.Equal(false, enableLogs.DefaultValue);
    }

    [Fact]
    public void NativeApiBinding_UsesSourceSpecificExportPrefixes()
    {
        MethodInfo getExportPrefix = typeof(NativeLogBridge).GetMethod(
            "GetExportPrefix",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException(nameof(NativeLogBridge), "GetExportPrefix");

        Assert.Equal("M_", getExportPrefix.Invoke(null, [NativeLogSource.OpencvHelper]));
        Assert.Equal("CM_", getExportPrefix.Invoke(null, [NativeLogSource.OpencvCuda]));
    }

    [Theory]
    [InlineData(nameof(OpenCVCuda.M_Fusion))]
    [InlineData(nameof(OpenCVCuda.CM_Fusion))]
    [InlineData(nameof(OpenCVCuda.CM_Fusion_Async))]
    [InlineData(nameof(OpenCVCuda.CM_Fusion_Batch))]
    public void CudaEntryPoints_PrepareLoggingThroughManagedWrappers(string methodName)
    {
        MethodInfo method = typeof(OpenCVCuda).GetMethod(
            methodName,
            BindingFlags.Public | BindingFlags.Static)
            ?? throw new MissingMethodException(nameof(OpenCVCuda), methodName);

        Assert.Null(method.GetCustomAttribute<DllImportAttribute>());
    }

    [Fact]
    public void OnNativeLog_DoesNotThrow_WhenSinkThrows()
    {
        using NativeLogStateScope scope = NativeLogStateScope.EnableWithSink(
            (_, _, _) => throw new InvalidOperationException("boom"));
        using Utf8String message = new("sink failure is isolated");

        InvokeOnNativeLog((int)NativeLogSource.OpencvHelper, (int)NativeLogLevel.Error, message.Pointer);
    }

    [Fact]
    public void OnNativeLog_DecodesUtf8AndMapsUnknownValues()
    {
        NativeLogSource capturedSource = NativeLogSource.OpencvHelper;
        NativeLogLevel capturedLevel = NativeLogLevel.Error;
        string? capturedMessage = null;
        using NativeLogStateScope scope = NativeLogStateScope.EnableWithSink((source, level, message) =>
        {
            capturedSource = source;
            capturedLevel = level;
            capturedMessage = message;
        });
        using Utf8String utf8 = new("原生日志 UTF-8 ✓");

        InvokeOnNativeLog(999, 999, utf8.Pointer);

        Assert.Equal(NativeLogSource.Unknown, capturedSource);
        Assert.Equal(NativeLogLevel.Info, capturedLevel);
        Assert.Equal("原生日志 UTF-8 ✓", capturedMessage);
    }

    [Fact]
    public void OnNativeLog_IsolatesSubscribersAndPreservesRecordMetadata()
    {
        NativeLogRecord? captured = null;
        Action<NativeLogRecord> throwing = _ => throw new InvalidOperationException("subscriber failure");
        Action<NativeLogRecord> observer = record => captured = record;
        using NativeLogStateScope scope = NativeLogStateScope.EnableWithSink(null);
        NativeLogBridge.LogReceived += throwing;
        NativeLogBridge.LogReceived += observer;
        using Utf8String utf8 = new("callback payload");

        try
        {
            InvokeOnNativeLog((int)NativeLogSource.OpencvCuda, (int)NativeLogLevel.Warn, utf8.Pointer);
        }
        finally
        {
            NativeLogBridge.LogReceived -= throwing;
            NativeLogBridge.LogReceived -= observer;
        }

        Assert.True(captured.HasValue);
        Assert.Equal(NativeLogSource.OpencvCuda, captured.Value.Source);
        Assert.Equal(NativeLogLevel.Warn, captured.Value.Level);
        Assert.Equal("callback payload", captured.Value.Message);
        Assert.Equal(Environment.CurrentManagedThreadId, captured.Value.ManagedThreadId);
        Assert.InRange(captured.Value.Timestamp, DateTimeOffset.Now.AddMinutes(-1), DateTimeOffset.Now.AddMinutes(1));
    }

    [Fact]
    public void RealHelper_CallbackRoundTripsThroughManagedBridge()
    {
        NativeLogRecord? captured = null;
        Action<NativeLogRecord> observer = record => captured = record;
        NativeLogBridge.Shutdown();
        NativeLogBridge.LogReceived += observer;

        try
        {
            NativeLogInitializationResult initialization = NativeLogBridge.InitializeWithResult(
                level: NativeLogLevel.Debug,
                enableLogs: true,
                enableNativeSink: false);

            Assert.True(initialization.HelperAvailable, initialization.Summary);
            Assert.True(NativeLogBridge.IsEnabled);

            int result = OpenCVMediaHelper.M_Fusion("[]", out HImage output);

            Assert.Equal(-1, result);
            Assert.Equal(IntPtr.Zero, output.pData);
            Assert.True(captured.HasValue);
            Assert.Equal(NativeLogSource.OpencvHelper, captured.Value.Source);
            Assert.Equal(NativeLogLevel.Debug, captured.Value.Level);
            Assert.Contains("M_Fusion", captured.Value.Message, StringComparison.Ordinal);
        }
        finally
        {
            NativeLogBridge.LogReceived -= observer;
            NativeLogBridge.Shutdown();
        }
    }

    private static void InvokeOnNativeLog(int source, int level, IntPtr messagePtr)
    {
        MethodInfo callback = typeof(NativeLogBridge).GetMethod(
            "OnNativeLog",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException(nameof(NativeLogBridge), "OnNativeLog");

        callback.Invoke(null, [source, level, messagePtr]);
    }

    private sealed class NativeLogStateScope : IDisposable
    {
        private static readonly FieldInfo SinkField = GetField("_sink");
        private static readonly FieldInfo EnabledField = GetField("_isEnabled");
        private readonly object? _previousSink;
        private readonly object? _previousEnabled;

        private NativeLogStateScope(Action<NativeLogSource, NativeLogLevel, string>? sink)
        {
            _previousSink = SinkField.GetValue(null);
            _previousEnabled = EnabledField.GetValue(null);
            SinkField.SetValue(null, sink);
            EnabledField.SetValue(null, 1);
        }

        public static NativeLogStateScope EnableWithSink(
            Action<NativeLogSource, NativeLogLevel, string>? sink)
        {
            return new NativeLogStateScope(sink);
        }

        public void Dispose()
        {
            SinkField.SetValue(null, _previousSink);
            EnabledField.SetValue(null, _previousEnabled);
        }

        private static FieldInfo GetField(string name)
        {
            return typeof(NativeLogBridge).GetField(name, BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new MissingFieldException(nameof(NativeLogBridge), name);
        }
    }

    private sealed class Utf8String : IDisposable
    {
        public Utf8String(string value)
        {
            Pointer = Marshal.StringToCoTaskMemUTF8(value);
        }

        public IntPtr Pointer { get; }

        public void Dispose()
        {
            Marshal.FreeCoTaskMem(Pointer);
        }
    }
}
