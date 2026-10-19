using System;
using System.Threading;
using System.Threading.Tasks;
using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.simulator;

internal sealed class MyTimer : IDisposable
{
    private readonly int interval;
    private readonly STNodeOption output;
    private readonly DateTime startTime = DateTime.Now;
    private readonly CancellationTokenSource stopped;
    private readonly CancellationToken token;
    private int disposed;

    internal CVStartCFC Action { get; }

    internal MyTimer(int interval, STNodeOption output, CVStartCFC action)
    {
        this.interval = interval;
        this.output = output;
        Action = action;
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
            Action.AddTTL(startTime);
            output.TransferData(Action);
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        stopped.Cancel();
        stopped.Dispose();
    }
}
