using System.Diagnostics.CodeAnalysis;

namespace ColorVision.SocketProtocol
{
    internal sealed class SocketWorkerTracker
    {
        private readonly object _lock = new();
        private TaskCompletionSource<bool> _completed = CreateCompletedSource();
        private int _activeWorkers;
        private bool _shutdownStarted;

        public int ActiveWorkers
        {
            get
            {
                lock (_lock)
                    return _activeWorkers;
            }
        }

        public bool TryRegister([NotNullWhen(true)] out SocketWorkerLease? lease)
        {
            lock (_lock)
            {
                if (_shutdownStarted)
                {
                    lease = null;
                    return false;
                }

                if (++_activeWorkers == 1)
                    _completed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                lease = new SocketWorkerLease(this);
                return true;
            }
        }

        public void BeginShutdown()
        {
            lock (_lock)
            {
                _shutdownStarted = true;
                if (_activeWorkers == 0)
                    _completed.TrySetResult(true);
            }
        }

        public bool Wait(TimeSpan timeout)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(timeout, TimeSpan.Zero);
            Task completion;
            lock (_lock)
                completion = _completed.Task;
            return completion.Wait(timeout);
        }

        internal void CompleteWorker()
        {
            lock (_lock)
            {
                if (_activeWorkers <= 0)
                    throw new InvalidOperationException("Socket worker completion was reported without a matching registration.");

                if (--_activeWorkers == 0)
                    _completed.TrySetResult(true);
            }
        }

        private static TaskCompletionSource<bool> CreateCompletedSource()
        {
            var completed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            completed.SetResult(true);
            return completed;
        }
    }

    internal sealed class SocketWorkerLease : IDisposable
    {
        private SocketWorkerTracker? _owner;

        internal bool IsDisposed => Volatile.Read(ref _owner) == null;

        public SocketWorkerLease(SocketWorkerTracker owner)
        {
            _owner = owner;
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref _owner, null)?.CompleteWorker();
        }
    }
}
