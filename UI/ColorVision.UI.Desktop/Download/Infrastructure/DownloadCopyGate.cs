namespace ColorVision.UI.Desktop.Download
{
    internal sealed class DownloadCopyGate
    {
        private readonly object _sync = new();
        private int _active;
        private TaskCompletionSource _changed = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<IDisposable> EnterAsync(Func<int> capacity, CancellationToken cancellationToken)
        {
            while (true)
            {
                Task changed;
                lock (_sync)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (_active < Math.Clamp(capacity(), 1, 16))
                    {
                        _active++;
                        return new Lease(this);
                    }
                    changed = _changed.Task;
                }
                await changed.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        public void NotifyCapacityChanged()
        {
            lock (_sync) Pulse();
        }

        private void Pulse()
        {
            var changed = _changed;
            _changed = new(TaskCreationOptions.RunContinuationsAsynchronously);
            changed.TrySetResult();
        }

        private sealed class Lease(DownloadCopyGate owner) : IDisposable
        {
            private int _disposed;
            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
                lock (owner._sync) { owner._active--; owner.Pulse(); }
            }
        }
    }
}
