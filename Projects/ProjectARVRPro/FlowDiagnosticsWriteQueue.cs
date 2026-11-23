using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ProjectARVRPro;

/// <summary>Tracks sealed diagnostic writes on the owning window's dispatcher.</summary>
internal sealed class FlowDiagnosticsWriteQueue
{
    private const int PendingRunWaitThreshold = 4;
    internal static readonly TimeSpan WriteWaitTimeout = TimeSpan.FromSeconds(5);
    private readonly Queue<(string SerialNumber, Task Completion)> pending = new();

    internal Task Track(string serialNumber, Task completion)
    {
        while (pending.TryPeek(out var oldest) && oldest.Completion.IsCompleted)
            pending.Dequeue();

        pending.Enqueue((serialNumber, completion));
        return pending.Count >= PendingRunWaitThreshold ? pending.Peek().Completion : Task.CompletedTask;
    }

    internal Task WhenWritten(string serialNumber) => Task.WhenAll(pending
        .Where(run => string.Equals(run.SerialNumber, serialNumber, StringComparison.Ordinal))
        .Select(run => run.Completion));

    internal Task DrainAsync() => Task.WhenAll(pending.Select(run => run.Completion));

    internal static async Task<bool> WaitAsync(Task completion, TimeSpan timeout)
    {
        try
        {
            await completion.WaitAsync(timeout).ConfigureAwait(false);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }
}
