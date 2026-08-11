namespace ColorVision.SocketProtocol
{
    internal sealed class SocketManagerApplicationLifetime
    {
        private readonly object _lock = new();
        private SocketManager? _instance;
        private bool _shutdownStarted;

        public SocketManager GetOrCreate(Func<SocketManager> createManager)
        {
            lock (_lock)
            {
                if (_shutdownStarted && _instance == null)
                    throw new InvalidOperationException("SocketManager cannot be created after application shutdown has started.");
                return _instance ??= createManager();
            }
        }

        public bool ShutdownExisting(TimeSpan timeout)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(timeout, TimeSpan.Zero);
            SocketManager? manager;
            lock (_lock)
            {
                _shutdownStarted = true;
                manager = _instance;
            }

            return manager?.Shutdown(timeout) ?? true;
        }
    }
}
