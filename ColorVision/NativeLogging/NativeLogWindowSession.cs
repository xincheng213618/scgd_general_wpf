using System;
using System.Threading;

namespace ColorVision.NativeLogging;

internal interface INativeLogCaptureController : IDisposable
{
    event Action<NativeLogDisplayEntry>? LogReceived;

    bool IsEnabled { get; }

    NativeLogOperationResult Start(NativeLogSeverity level);

    NativeLogOperationResult SetLevel(NativeLogSeverity level);

    void Stop();
}

internal readonly record struct NativeLogOperationResult(bool Success, string Message)
{
    public static NativeLogOperationResult Succeeded(string message = "") => new(true, message);

    public static NativeLogOperationResult Failed(string message) => new(false, message);
}

internal sealed class NativeLogWindowSession : IDisposable
{
    private readonly INativeLogCaptureController _controller;
    private readonly NativeLogPendingBuffer _buffer;
    private int _isCapturing;
    private int _isPaused;
    private int _isDisposed;

    public NativeLogWindowSession(INativeLogCaptureController controller, int pendingCapacity)
    {
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        _buffer = new NativeLogPendingBuffer(pendingCapacity);
        _controller.LogReceived += OnLogReceived;
    }

    public bool IsCapturing => Volatile.Read(ref _isCapturing) != 0;

    public bool IsPaused
    {
        get => Volatile.Read(ref _isPaused) != 0;
        set => Volatile.Write(ref _isPaused, value ? 1 : 0);
    }

    public NativeLogOperationResult Start(NativeLogSeverity level)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _isDisposed) != 0, this);

        // Enable the managed queue before registering/enabling native callbacks so
        // messages produced during initialization are not lost.
        Volatile.Write(ref _isCapturing, 1);
        NativeLogOperationResult result;
        try
        {
            result = _controller.Start(level);
        }
        catch (Exception ex)
        {
            result = NativeLogOperationResult.Failed(ex.Message);
        }

        Volatile.Write(ref _isCapturing, result.Success && _controller.IsEnabled ? 1 : 0);
        return result;
    }

    public NativeLogOperationResult SetLevel(NativeLogSeverity level)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _isDisposed) != 0, this);

        try
        {
            return _controller.SetLevel(level);
        }
        catch (Exception ex)
        {
            return NativeLogOperationResult.Failed(ex.Message);
        }
    }

    public void Stop()
    {
        Volatile.Write(ref _isCapturing, 0);
        try
        {
            _controller.Stop();
        }
        catch
        {
            // Diagnostics must never interfere with shutdown or window close.
        }
    }

    public NativeLogDrainBatch Drain(int maxEntries)
    {
        if (IsPaused)
        {
            NativeLogBufferSnapshot snapshot = _buffer.GetSnapshot();
            return new NativeLogDrainBatch([], snapshot.PendingCount, snapshot.DroppedCount);
        }

        return _buffer.Drain(maxEntries);
    }

    public NativeLogBufferSnapshot GetBufferSnapshot() => _buffer.GetSnapshot();

    public void Clear() => _buffer.Clear();

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
        {
            return;
        }

        Stop();
        _controller.LogReceived -= OnLogReceived;
        _controller.Dispose();
    }

    private void OnLogReceived(NativeLogDisplayEntry entry)
    {
        if (IsCapturing)
        {
            _buffer.Enqueue(entry);
        }
    }
}
