using ColorVision.SocketProtocol;
using Newtonsoft.Json;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace ProjectARVRPro.Services;

internal sealed record ExternalImageSwitchResult(string MsgID, string SerialNumber, long AcknowledgementMs, int DelayMs);

/// <summary>One outstanding image switch on the existing project control connection.</summary>
internal sealed class ExternalImageSwitchService
{
    internal const string RequestEvent = "AoiSwitchPG";
    internal const string CompletionEvent = "AOITestSwitchImageComplete";
    internal static ExternalImageSwitchService Instance { get; } = new(() => SocketControl.Current.Stream, LogSent);

    private readonly object sync = new();
    private readonly Func<Stream?> currentStream;
    private readonly Action<SocketResponse>? sent;
    private PendingSwitch? pending;

    internal ExternalImageSwitchService(Func<Stream?> currentStream, Action<SocketResponse>? sent = null)
    {
        this.currentStream = currentStream;
        this.sent = sent;
    }

    internal async Task<ExternalImageSwitchResult> ExecuteAsync(string serialNumber, int timeoutMs, int delayMs,
        bool requireMatchingMsgId, CancellationToken cancellationToken)
    {
        if (timeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(timeoutMs), DisplayText.Get("ExternalSwitch_InvalidTimeout"));
        if (delayMs < 0) throw new ArgumentOutOfRangeException(nameof(delayMs), DisplayText.Get("ExternalSwitch_InvalidDelay"));
        cancellationToken.ThrowIfCancellationRequested();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeoutMs);
        PendingSwitch operation;
        lock (sync)
        {
            if (pending != null) throw new InvalidOperationException(DisplayText.Get("ExternalSwitch_Busy"));
            Stream stream = currentStream() ?? throw new IOException(DisplayText.Get("ExternalSwitch_NotConnected"));
            if (!stream.CanWrite) throw new IOException(DisplayText.Get("ExternalSwitch_NotConnected"));
            operation = new PendingSwitch(stream, serialNumber, requireMatchingMsgId, deadline.Token);
            pending = operation; // Register before writing: an immediate acknowledgement must not be lost.
        }

        var elapsed = Stopwatch.StartNew();
        try
        {
            var request = new SocketResponse
            {
                Version = "1.0", MsgID = operation.MsgID, SerialNumber = serialNumber,
                EventName = RequestEvent, Code = 0, Msg = RequestEvent
            };
            await operation.Stream.WriteAsync(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(request)), deadline.Token).ConfigureAwait(false);
            sent?.Invoke(request);
            await WaitForCompletionAsync(operation, deadline.Token).ConfigureAwait(false);
            deadline.CancelAfter(Timeout.Infinite);
            long acknowledgementMs = elapsed.ElapsedMilliseconds;
            if (delayMs > 0) await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            EnsureConnection(operation);
            return new ExternalImageSwitchResult(operation.MsgID, serialNumber, acknowledgementMs, delayMs);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested && deadline.IsCancellationRequested)
        {
            throw new TimeoutException(DisplayText.Format($"ExternalSwitch_Timeout: {timeoutMs}"), ex);
        }
        finally
        {
            lock (sync)
            {
                if (ReferenceEquals(pending, operation)) pending = null;
            }
        }
    }

    internal bool TryComplete(Stream stream, SocketRequest request)
    {
        lock (sync)
        {
            PendingSwitch? operation = pending;
            if (operation == null || operation.Token.IsCancellationRequested
                || request.EventName != CompletionEvent || !ReferenceEquals(operation.Stream, stream)
                || !ReferenceEquals(currentStream(), stream) || !stream.CanWrite)
                return false;
            if (operation.RequireMatchingMsgId && !string.Equals(request.MsgID, operation.MsgID, StringComparison.Ordinal))
                return false;
            if (!string.IsNullOrEmpty(request.SerialNumber) && !string.IsNullOrEmpty(operation.SerialNumber)
                && !string.Equals(request.SerialNumber, operation.SerialNumber, StringComparison.Ordinal))
                return false;
            return operation.Completion.TrySetResult();
        }
    }

    private async Task WaitForCompletionAsync(PendingSwitch operation, CancellationToken token)
    {
        // SocketManager owns reads and closes disconnected streams. Observe it without another reader
        // or another device connection; also fail if a different client takes over the control stream.
        using var monitor = CancellationTokenSource.CreateLinkedTokenSource(token);
        try
        {
            while (!operation.Completion.Task.IsCompleted)
            {
                token.ThrowIfCancellationRequested();
                EnsureConnection(operation);
                await Task.WhenAny(operation.Completion.Task, Task.Delay(100, monitor.Token)).ConfigureAwait(false);
            }
            token.ThrowIfCancellationRequested();
            EnsureConnection(operation);
            await operation.Completion.Task.ConfigureAwait(false);
        }
        finally { monitor.Cancel(); }
    }

    private void EnsureConnection(PendingSwitch operation)
    {
        if (!ReferenceEquals(currentStream(), operation.Stream) || !operation.Stream.CanWrite)
            throw new IOException(DisplayText.Get("ExternalSwitch_ConnectionChanged"));
    }

    private static void LogSent(SocketResponse request) => SocketMessageManager.GetInstance().AddMessage(new SocketMessage
    {
        Direction = SocketMessageDirection.Sent, Content = JsonConvert.SerializeObject(request),
        MessageTime = DateTime.Now, EventName = request.EventName, MsgID = request.MsgID, ResponseCode = request.Code
    });

    private sealed class PendingSwitch(Stream stream, string serialNumber, bool requireMatchingMsgId, CancellationToken token)
    {
        public Stream Stream { get; } = stream;
        public string SerialNumber { get; } = serialNumber;
        public string MsgID { get; } = Guid.NewGuid().ToString("N");
        public bool RequireMatchingMsgId { get; } = requireMatchingMsgId;
        public CancellationToken Token { get; } = token;
        public TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
