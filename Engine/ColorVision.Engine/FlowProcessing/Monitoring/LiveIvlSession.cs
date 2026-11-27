using System;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Engine.FlowProcessing.Monitoring;

// Node notifications arriving during a read request one more read. No periodic polling is needed.
internal sealed class LiveIvlSession(Func<MeasureBatchModel, CancellationToken, Task<LiveIvlSnapshot>> read) : IDisposable
{
    private readonly CancellationTokenSource lifetime = new();
    private int generation;
    private bool busy, pending, disposed;
    internal MeasureBatchModel? Batch { get; private set; }
    internal LiveIvlSnapshot? Snapshot { get; private set; }
    internal string? Error { get; private set; }
    internal event EventHandler? Updated;

    internal bool SelectBatch(MeasureBatchModel? batch)
    {
        if (batch is not { Id: > 0 }) batch = null;
        if (ReferenceEquals(Batch, batch)) return false;
        generation++;
        Batch = batch;
        Snapshot = null;
        Error = null;
        pending = false;
        return true;
    }

    internal async Task<bool> RefreshAsync()
    {
        if (disposed || Batch == null) return false;
        pending = true;
        if (busy) return false;
        busy = true;
        int updatedGeneration = -1;
        try
        {
            while (pending && !disposed && Batch is { } batch)
            {
                pending = false;
                int version = generation;
                bool accepted = false;
                try
                {
                    var next = await read(batch, lifetime.Token);
                    if (disposed || version != generation || next.BatchId != batch.Id) continue;
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
