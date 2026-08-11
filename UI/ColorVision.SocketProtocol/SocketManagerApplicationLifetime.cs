using System.Diagnostics;

namespace ColorVision.SocketProtocol
{
    internal readonly struct SocketShutdownDeadline
    {
        private readonly long _startedAt;
        private readonly TimeSpan _timeout;

        private SocketShutdownDeadline(TimeSpan timeout)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(timeout, TimeSpan.Zero);
            _timeout = timeout;
            _startedAt = Stopwatch.GetTimestamp();
        }

        public TimeSpan Elapsed => Stopwatch.GetElapsedTime(_startedAt);

        public TimeSpan Remaining
        {
            get
            {
                TimeSpan remaining = _timeout - Elapsed;
                return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
            }
        }

        public static SocketShutdownDeadline Start(TimeSpan timeout) => new(timeout);
    }

    internal sealed class SocketManagerApplicationLifetime
    {
        private sealed class ManagerCreation
        {
            public TaskCompletionSource<SocketManager> Completion { get; } = new(
                TaskCreationOptions.RunContinuationsAsynchronously);
        }

        private readonly object _lock = new();
        private ManagerCreation? _creation;
        private SocketManager? _instance;
        private int _shutdownStarted;

        public SocketManager GetOrCreate(Func<SocketManager> createManager)
        {
            ArgumentNullException.ThrowIfNull(createManager);
            ManagerCreation creation;
            bool ownsCreation;
            lock (_lock)
            {
                ThrowIfShutdownStarted();
                if (_instance != null)
                    return _instance;

                ownsCreation = _creation == null;
                creation = _creation ??= new ManagerCreation();
            }

            if (!ownsCreation)
                return creation.Completion.Task.GetAwaiter().GetResult();

            SocketManager manager;
            try
            {
                manager = createManager()
                    ?? throw new InvalidOperationException("SocketManager factory returned null.");
            }
            catch (Exception exception)
            {
                lock (_lock)
                {
                    if (ReferenceEquals(_creation, creation))
                        _creation = null;
                }
                creation.Completion.TrySetException(exception);
                throw;
            }

            bool published;
            lock (_lock)
            {
                published = Volatile.Read(ref _shutdownStarted) == 0;
                // Retain a late result as the terminal instance as well. GetOrCreate
                // rejects it after shutdown, while repeated ShutdownExisting calls can
                // still wait for its resource retirement instead of reporting no service.
                Volatile.Write(ref _instance, manager);
                if (ReferenceEquals(_creation, creation))
                    _creation = null;
            }

            if (!published)
            {
                manager.BeginShutdown();
                var exception = new InvalidOperationException(
                    "SocketManager cannot be created after application shutdown has started.");
                creation.Completion.TrySetException(exception);
                throw exception;
            }

            // Shutdown can linearize immediately after the publication check. In that
            // race either ShutdownExisting observes this instance or this recheck closes it.
            if (Volatile.Read(ref _shutdownStarted) != 0)
                manager.BeginShutdown();
            creation.Completion.TrySetResult(manager);
            return manager;
        }

        public bool ShutdownExisting(TimeSpan timeout)
        {
            SocketShutdownDeadline deadline = SocketShutdownDeadline.Start(timeout);
            Interlocked.Exchange(ref _shutdownStarted, 1);

            SocketManager? manager = null;
            bool creationInProgress = false;
            bool lockTaken = false;
            try
            {
                lockTaken = Monitor.TryEnter(_lock, deadline.Remaining);
                if (lockTaken)
                {
                    manager = _instance;
                    creationInProgress = _creation != null;
                }
                else
                    manager = Volatile.Read(ref _instance);
            }
            finally
            {
                if (lockTaken)
                    Monitor.Exit(_lock);
            }

            if (manager != null)
                return manager.Shutdown(deadline);

            // If publication itself consumed the entire budget, its post-publication
            // shutdown check still closes the candidate, but this call cannot certify it.
            return lockTaken && !creationInProgress;
        }

        private void ThrowIfShutdownStarted()
        {
            if (Volatile.Read(ref _shutdownStarted) != 0)
                throw new InvalidOperationException(
                    "SocketManager cannot be created after application shutdown has started.");
        }
    }
}
