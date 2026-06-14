#pragma warning disable CA1707
using ColorVision.Core;
using System;
using System.Reflection;

namespace ColorVision.UI.Tests;

public sealed class NativeLogBridgeTests
{
    [Fact]
    public void OnNativeLog_DoesNotThrow_WhenSinkThrows()
    {
        using var scope = NativeLogSinkScope.Set((_, _, _) => throw new InvalidOperationException("boom"));

        InvokeOnNativeLog((int)NativeLogSource.OpencvHelper, (int)NativeLogLevel.Error, IntPtr.Zero);
    }

    [Fact]
    public void OnNativeLog_MapsUnknownValuesAndNullMessage()
    {
        NativeLogSource capturedSource = NativeLogSource.OpencvHelper;
        NativeLogLevel capturedLevel = NativeLogLevel.Error;
        string? capturedMessage = "not set";
        using var scope = NativeLogSinkScope.Set((source, level, message) =>
        {
            capturedSource = source;
            capturedLevel = level;
            capturedMessage = message;
        });

        InvokeOnNativeLog(999, 999, IntPtr.Zero);

        Assert.Equal(NativeLogSource.Unknown, capturedSource);
        Assert.Equal(NativeLogLevel.Info, capturedLevel);
        Assert.Equal(string.Empty, capturedMessage);
    }

    private static void InvokeOnNativeLog(int source, int level, IntPtr messagePtr)
    {
        MethodInfo callback = typeof(NativeLogBridge).GetMethod(
            "OnNativeLog",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException(nameof(NativeLogBridge), "OnNativeLog");

        callback.Invoke(null, new object[] { source, level, messagePtr });
    }

    private sealed class NativeLogSinkScope : IDisposable
    {
        private static readonly FieldInfo SinkField = typeof(NativeLogBridge).GetField(
            "_sink",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingFieldException(nameof(NativeLogBridge), "_sink");

        private readonly object? previousSink;

        private NativeLogSinkScope(Action<NativeLogSource, NativeLogLevel, string> sink)
        {
            previousSink = SinkField.GetValue(null);
            SinkField.SetValue(null, sink);
        }

        public static NativeLogSinkScope Set(Action<NativeLogSource, NativeLogLevel, string> sink)
        {
            return new NativeLogSinkScope(sink);
        }

        public void Dispose()
        {
            SinkField.SetValue(null, previousSink);
        }
    }
}
