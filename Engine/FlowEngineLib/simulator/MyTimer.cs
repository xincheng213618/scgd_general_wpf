using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using FlowEngineLib.Base;
using FlowEngineLib.Runtime;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.simulator;

internal sealed class MyTimer : IDisposable
{
    private readonly int interval;
    private readonly STNodeOption output;
    private readonly DateTime startTime = DateTime.Now;
    private readonly long startedAt = Stopwatch.GetTimestamp();
    private readonly Action<MyTimer, FlowFailureKind?, string> onCompleted;
    private readonly CancellationTokenSource stopped;
    private readonly CancellationToken token;
    private int disposed;
    private int completed;

    internal CVStartCFC Action { get; }
    internal string MessageId { get; } = Guid.NewGuid().ToString("N");
    internal int Interval => interval;
    internal long ElapsedMs => (long)Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;

    internal MyTimer(int interval, STNodeOption output, CVStartCFC action, Action<MyTimer, FlowFailureKind?, string> onCompleted)
    {
        this.interval = interval;
        this.output = output;
        Action = action;
        this.onCompleted = onCompleted;
        stopped = CancellationTokenSource.CreateLinkedTokenSource(action.RuntimeResources.StopToken);
        token = stopped.Token;
    }

    internal Task WaitAsync() => Task.Delay(interval, token);

    internal void TransferData()
    {
        lock (output)
        {
            if (token.IsCancellationRequested || Action.RuntimeResources.IsDisposed) return;
            Action.NormalizeStopStatus();
            if (!Action.IsRunning) return;
            // Finish the timer before forwarding, so downstream work is not charged to the delay.
            if (!Complete()) return;
            Action.AddTTL(startTime);
            output.TransferData(Action);
        }
    }

    internal bool Complete(FlowFailureKind? failureKind = null, string message = "Finish")
    {
        if (Interlocked.Exchange(ref completed, 1) != 0) return false;
        onCompleted(this, failureKind, message);
        return true;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        stopped.Cancel();
        stopped.Dispose();
        Complete(FlowFailureKind.Canceled, "Timer canceled");
    }
}
