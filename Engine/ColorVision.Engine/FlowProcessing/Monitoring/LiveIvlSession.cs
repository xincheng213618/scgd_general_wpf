using System;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Engine.FlowProcessing.Monitoring;

// Node notifications arriving during a read request one more read. No periodic polling is needed.
internal sealed class LiveIvlSession(Func<int, CancellationToken, Task<LiveIvlSnapshot>> read) : IDisposable
{
    private readonly CancellationTokenSource lifetime = new();
    private int generation;
    private bool busy, pending, disposed;
    internal int BatchId { get; private set; }
    internal LiveIvlSnapshot? Snapshot { get; private set; }
    internal string? Error { get; private set; }
    internal event EventHandler? Updated;

    internal void SelectBatch(int id)
    {
        if (BatchId == id) return;
        generation++; BatchId = id; Snapshot = null; Error = null; pending = false;
    }

    internal async Task<bool> RefreshAsync()
    {
        if (disposed || BatchId <= 0) return false;
        pending = true;
        if (busy) return false;
        busy = true;
        int updatedGeneration = -1;
        try
        {
            while (pending && !disposed && BatchId > 0)
            {
                pending = false;
                int version = generation, id = BatchId;
                bool accepted = false;
                try
                {
                    var next = await read(id, lifetime.Token);
                    if (disposed || version != generation || next.BatchId != id) continue;
                    Snapshot = next; Error = null; accepted = true;
                }
                catch (OperationCanceledException) when (disposed) { break; }
                catch (Exception ex)
                {
                    if (disposed || version != generation) continue;
                    Error = ex.Message; accepted = true;
                }
                if (accepted)
                {
                    updatedGeneration = version;
                    Updated?.Invoke(this, EventArgs.Empty);
                }
            }
            return updatedGeneration == generation && !disposed;
        }
        finally { busy = false; }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true; generation++; lifetime.Cancel(); lifetime.Dispose();
    }
}
