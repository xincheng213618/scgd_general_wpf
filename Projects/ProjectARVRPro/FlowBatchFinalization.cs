using System.ComponentModel;

namespace ProjectARVRPro;

/// <summary>Tracks the database write separately from the continuation on the window dispatcher.</summary>
internal sealed class FlowBatchFinalization
{
    internal bool IsCompleting { get; private set; }
    internal bool CloseRequested { get; private set; }
    internal Task? PendingWrite { get; private set; }

    internal void BeginCompletion() => IsCompleting = true;

    internal bool EndCompletion()
    {
        if (!IsCompleting)
            return false;

        IsCompleting = false;
        return CloseRequested;
    }

    internal void OnClosing(CancelEventArgs e)
    {
        if (!IsCompleting)
        {
            CloseRequested = false;
        }
        else if (!e.Cancel)
        {
            CloseRequested = true;
            e.Cancel = true;
        }
    }

    internal async Task RunAsync(Action write)
    {
        if (PendingWrite != null)
            throw new InvalidOperationException("A batch write is already pending.");

        Task command = Task.Run(write);
        PendingWrite = command;
        try
        {
            await command;
        }
        finally
        {
            PendingWrite = null;
        }
    }
}
