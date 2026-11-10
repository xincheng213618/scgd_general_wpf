using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.ImageEditor.Tif;

internal sealed class BitmapOpenRequests
{
    private readonly object _sync = new();
    private readonly SemaphoreSlim _preparationGate = new(1, 1);
    private readonly HashSet<Task> _active = new();
    private Request? _latest;

    public Request Start(CancellationToken cancellationToken)
    {
        var request = new Request(this, cancellationToken);
        try
        {
            lock (_sync)
            {
                Request? previous = _latest;
                _latest = request;
                _active.Add(request.Completion.Task);
                // A cancellation callback can start another open. Install this request first.
                previous?.Cancellation.Cancel();
            }
            return request;
        }
        catch { request.Dispose(); throw; }
    }

    public static void VerifyTarget(EditorContext context, string filePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string? activePath = context.Config.GetProperties<string>(ImageViewPropertyKeys.FilePath);
        if (!string.Equals(activePath, filePath, StringComparison.OrdinalIgnoreCase))
            throw new OperationCanceledException("Image open request was superseded.", cancellationToken);
    }

    // Only decode and display-buffer preparation use the slot. Providers use private pixels.
    public async Task<T> PrepareAsync<T>(Func<Task<T>> prepare, CancellationToken cancellationToken)
    {
        await _preparationGate.WaitAsync(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await prepare();
        }
        finally { _preparationGate.Release(); }
    }

    public Task CancelAndDrain()
    {
        lock (_sync)
        {
            Task drain = Task.WhenAll(_active);
            Request? latest = _latest;
            _latest = null;
            latest?.Cancellation.Cancel();
            return drain;
        }
    }

    internal sealed class Request(BitmapOpenRequests owner, CancellationToken cancellationToken) : IDisposable
    {
        internal CancellationTokenSource Cancellation { get; } = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        internal TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public CancellationToken Token => Cancellation.Token;

        public void Dispose()
        {
            lock (owner._sync)
            {
                if (!owner._active.Remove(Completion.Task)) return;
                if (ReferenceEquals(owner._latest, this)) owner._latest = null;
                Completion.SetResult();
            }
            Cancellation.Dispose();
        }
    }
}
