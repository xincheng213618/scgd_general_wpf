using System.Net;
using System.Net.Sockets;

namespace ColorVision.SocketProtocol
{
    internal sealed record SocketServerSettings(
        string IPAddress,
        int ServerPort,
        int SocketBufferSize,
        SocketPhraseType SocketPhraseType,
        bool IsServerEnabled)
    {
        public static SocketServerSettings Capture(SocketConfig config) => new(
            config.IPAddress,
            config.ServerPort,
            config.SocketBufferSize,
            config.SocketPhraseType,
            config.IsServerEnabled);

        public string ListenAddress => $"{IPAddress}:{ServerPort}";
    }

    internal enum SocketServerFailureStage
    {
        Start,
        Stop
    }

    internal sealed record SocketServerTransition(
        long Sequence,
        SocketServerState State,
        SocketServerSettings? Settings = null,
        Exception? Exception = null,
        SocketServerFailureStage? FailureStage = null);

    internal interface ISocketServerListener
    {
        void Start();
        TcpClient AcceptTcpClient();
        void Stop();
    }

    internal interface ISocketServerListenerFactory
    {
        ISocketServerListener Create(SocketServerSettings settings);
    }

    internal sealed class TcpSocketServerListenerFactory : ISocketServerListenerFactory
    {
        public static TcpSocketServerListenerFactory Instance { get; } = new();

        private TcpSocketServerListenerFactory()
        {
        }

        public ISocketServerListener Create(SocketServerSettings settings)
        {
            var listener = new TcpListener(IPAddress.Parse(settings.IPAddress), settings.ServerPort);
            return new TcpSocketServerListener(listener);
        }

        private sealed class TcpSocketServerListener(TcpListener listener) : ISocketServerListener
        {
            public void Start() => listener.Start();
            public TcpClient AcceptTcpClient() => listener.AcceptTcpClient();
            public void Stop() => listener.Stop();
        }
    }

    internal sealed class SocketServerClient
    {
        private int _closed;

        internal SocketServerClient(SocketServerSession session, TcpClient client)
        {
            Session = session;
            Client = client;
        }

        internal SocketServerSession Session { get; }
        public TcpClient Client { get; }
        public SocketServerSettings Settings => Session.Settings;
        public bool IsClosed => Volatile.Read(ref _closed) != 0;

        internal bool TryMarkClosed() => Interlocked.Exchange(ref _closed, 1) == 0;
    }

    /// <summary>
    /// Owns the server generation and lifecycle state. SocketManager only projects
    /// the ordered transitions onto UI-bound properties.
    /// </summary>
    internal sealed class SocketServerLifecycle
    {
        private readonly object _stateLock = new();
        private readonly ISocketServerListenerFactory _listenerFactory;
        private readonly Action<Action> _queueWork;
        private readonly SocketWorkerTracker _workerTracker;
        private readonly Action<SocketServerTransition> _stateChanged;
        private readonly Action<SocketServerClient> _clientAccepted;
        private readonly Action<SocketServerClient> _clientClosed;
        private readonly HashSet<SocketServerSession> _sessions = new();
        private SocketServerSession? _currentSession;
        private Exception? _shutdownException;
        private SocketServerState _state;
        private bool _shutdownStarted;
        private long _sessionVersion;
        private long _transitionSequence;

        public SocketServerLifecycle(
            SocketServerState initialState,
            ISocketServerListenerFactory listenerFactory,
            Action<Action> queueWork,
            SocketWorkerTracker workerTracker,
            Action<SocketServerTransition> stateChanged,
            Action<SocketServerClient> clientAccepted,
            Action<SocketServerClient> clientClosed)
        {
            _state = initialState;
            _listenerFactory = listenerFactory;
            _queueWork = queueWork;
            _workerTracker = workerTracker;
            _stateChanged = stateChanged;
            _clientAccepted = clientAccepted;
            _clientClosed = clientClosed;
        }

        public SocketServerState State
        {
            get
            {
                lock (_stateLock)
                    return _state;
            }
        }

        internal long OperationVersion => Volatile.Read(ref _sessionVersion);

        public Exception? ShutdownException => Volatile.Read(ref _shutdownException);

        public bool Start(SocketServerSettings settings) => Start(settings, runInline: false);

        public bool StartInline(SocketServerSettings settings) => Start(settings, runInline: true);

        private bool Start(SocketServerSettings settings, bool runInline)
        {
            SocketServerSession session;
            SocketServerTransition transition;
            SocketWorkerLease workerLease;
            lock (_stateLock)
            {
                if (_shutdownStarted || _state is SocketServerState.Starting or SocketServerState.Running)
                    return false;
                if (!_workerTracker.TryRegister(out SocketWorkerLease? registeredLease))
                    return false;

                session = new SocketServerSession(++_sessionVersion, settings);
                _sessions.Add(session);
                _currentSession = session;
                transition = ChangeStateLocked(SocketServerState.Starting, settings);
                workerLease = registeredLease;
            }

            if (runInline)
            {
                try
                {
                    _stateChanged(transition);
                }
                catch
                {
                    CancelUnstartedSession(session);
                    workerLease.Dispose();
                    throw;
                }
                RunTracked(workerLease, () => RunSession(session));
            }
            else
            {
                QueueTrackedWork(workerLease, () => RunSession(session));
                _stateChanged(transition);
            }
            return true;
        }

        public bool Stop(bool isServerEnabled)
        {
            SocketServerSession? session;
            Exception? listenerStopException = null;
            long stopVersion;
            SocketServerTransition transition;
            SocketWorkerLease workerLease;
            lock (_stateLock)
            {
                if (_shutdownStarted || _state == SocketServerState.Stopping)
                    return false;
                if (!_workerTracker.TryRegister(out SocketWorkerLease? registeredLease))
                    return false;

                session = _currentSession;
                session?.RequestStop();
                _currentSession = null;
                stopVersion = ++_sessionVersion;
                transition = ChangeStateLocked(SocketServerState.Stopping);

                try
                {
                    session?.StopListener();
                }
                catch (Exception exception)
                {
                    listenerStopException = exception;
                }
                workerLease = registeredLease;
            }

            QueueTrackedWork(
                workerLease,
                () => StopSession(session, stopVersion, isServerEnabled, listenerStopException));
            _stateChanged(transition);
            return true;
        }

        public void BeginShutdown()
        {
            List<(SocketServerSession Session, SocketWorkerLease Lease)> cleanupWork = new();
            lock (_stateLock)
            {
                if (_shutdownStarted)
                    return;

                _shutdownStarted = true;
                _currentSession = null;
                ++_sessionVersion;
                _state = SocketServerState.Stopping;
                foreach (SocketServerSession session in _sessions)
                {
                    session.RequestStop();
                    if (_workerTracker.TryRegister(out SocketWorkerLease? lease))
                        cleanupWork.Add((session, lease));
                }
                _workerTracker.BeginShutdown();
            }

            foreach ((SocketServerSession session, SocketWorkerLease lease) in cleanupWork)
            {
                try
                {
                    _ = Task.Run(() => RunTracked(lease, () => CleanupForShutdown(session)));
                }
                catch (Exception exception)
                {
                    lease.Dispose();
                    RecordShutdownException(exception);
                }
            }
        }

        public void ReleaseClient(SocketServerClient connection)
        {
            if (connection.Session.RemoveClient(connection))
                CloseClient(connection);
        }

        private void RunSession(SocketServerSession session)
        {
            Exception? failure = null;
            try
            {
                if (session.IsStopRequested)
                    return;

                ISocketServerListener listener = _listenerFactory.Create(session.Settings);
                if (!session.TryStart(listener))
                {
                    SafeStop(listener);
                    return;
                }

                if (!TryChangeSessionState(session, SocketServerState.Running))
                    return;

                while (!session.IsStopRequested)
                {
                    TcpClient client = listener.AcceptTcpClient();
                    var connection = new SocketServerClient(session, client);
                    if (!session.TryRegisterClient(connection))
                    {
                        CloseClient(connection);
                        break;
                    }

                    _clientAccepted(connection);
                }
            }
            catch (ObjectDisposedException) when (session.IsStopRequested)
            {
            }
            catch (SocketException) when (session.IsStopRequested)
            {
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            finally
            {
                if (!session.IsStopRequested)
                {
                    try
                    {
                        CloseSessionResources(session);
                    }
                    catch (Exception exception)
                    {
                        failure ??= exception;
                    }

                    try
                    {
                        if (failure == null)
                        {
                            TryCompleteSession(
                                session,
                                session.Settings.IsServerEnabled ? SocketServerState.Stopped : SocketServerState.Disabled);
                        }
                        else
                        {
                            TryFailSession(session, failure);
                        }
                    }
                    finally
                    {
                        RemoveSession(session);
                    }
                }
            }
        }

        private void StopSession(
            SocketServerSession? session,
            long stopVersion,
            bool isServerEnabled,
            Exception? listenerStopException)
        {
            Exception? failure = listenerStopException;
            try
            {
                if (session != null)
                    CloseSessionResources(session);
            }
            catch (Exception exception)
            {
                failure ??= exception;
            }
            finally
            {
                if (session != null)
                    RemoveSession(session);
            }

            if (failure == null)
                TryCompleteStop(stopVersion, isServerEnabled ? SocketServerState.Stopped : SocketServerState.Disabled);
            else
                TryFailStop(stopVersion, failure);
        }

        private void CloseSessionResources(SocketServerSession session)
        {
            Exception? firstException = null;
            ISocketServerListener? listener = session.TakeListener();
            if (listener != null)
            {
                try
                {
                    listener.Stop();
                }
                catch (Exception exception)
                {
                    firstException = exception;
                }
            }

            foreach (SocketServerClient client in session.TakeClients())
            {
                try
                {
                    CloseClient(client);
                }
                catch (Exception exception)
                {
                    firstException ??= exception;
                }
            }

            if (firstException != null)
                throw firstException;
        }

        private void CleanupForShutdown(SocketServerSession session)
        {
            try
            {
                CloseSessionResources(session);
            }
            catch (Exception exception)
            {
                RecordShutdownException(exception);
            }
            finally
            {
                RemoveSession(session);
            }
        }

        private void RemoveSession(SocketServerSession session)
        {
            lock (_stateLock)
                _sessions.Remove(session);
        }

        private void CancelUnstartedSession(SocketServerSession session)
        {
            lock (_stateLock)
            {
                _sessions.Remove(session);
                if (!ReferenceEquals(_currentSession, session) || session.Version != _sessionVersion)
                    return;

                _currentSession = null;
                _state = session.Settings.IsServerEnabled
                    ? SocketServerState.Stopped
                    : SocketServerState.Disabled;
            }
        }

        private void RecordShutdownException(Exception exception)
        {
            Interlocked.CompareExchange(ref _shutdownException, exception, null);
        }

        private void QueueTrackedWork(SocketWorkerLease lease, Action action)
        {
            try
            {
                _queueWork(() => RunTracked(lease, action));
            }
            catch
            {
                if (lease.IsDisposed)
                    throw;

                try
                {
                    _ = Task.Run(() => RunTracked(lease, action));
                }
                catch
                {
                    lease.Dispose();
                    throw;
                }
            }
        }

        private static void RunTracked(SocketWorkerLease lease, Action action)
        {
            using (lease)
                action();
        }

        private bool TryChangeSessionState(SocketServerSession session, SocketServerState state)
        {
            SocketServerTransition transition;
            lock (_stateLock)
            {
                if (!ReferenceEquals(_currentSession, session) || session.Version != _sessionVersion)
                    return false;

                transition = ChangeStateLocked(state, session.Settings);
            }

            _stateChanged(transition);
            return true;
        }

        private bool TryFailSession(SocketServerSession session, Exception exception)
        {
            SocketServerTransition transition;
            lock (_stateLock)
            {
                if (!ReferenceEquals(_currentSession, session) || session.Version != _sessionVersion)
                    return false;

                _currentSession = null;
                transition = ChangeStateLocked(
                    SocketServerState.Error,
                    session.Settings,
                    exception,
                    SocketServerFailureStage.Start);
            }

            _stateChanged(transition);
            return true;
        }

        private void TryCompleteSession(SocketServerSession session, SocketServerState state)
        {
            SocketServerTransition transition;
            lock (_stateLock)
            {
                if (!ReferenceEquals(_currentSession, session) || session.Version != _sessionVersion)
                    return;

                _currentSession = null;
                transition = ChangeStateLocked(state, session.Settings);
            }

            _stateChanged(transition);
        }

        private void TryCompleteStop(long stopVersion, SocketServerState state)
        {
            SocketServerTransition transition;
            lock (_stateLock)
            {
                if (stopVersion != _sessionVersion || _currentSession != null)
                    return;

                transition = ChangeStateLocked(state);
            }

            _stateChanged(transition);
        }

        private void TryFailStop(long stopVersion, Exception exception)
        {
            SocketServerTransition transition;
            lock (_stateLock)
            {
                if (stopVersion != _sessionVersion || _currentSession != null)
                    return;

                transition = ChangeStateLocked(
                    SocketServerState.Error,
                    exception: exception,
                    failureStage: SocketServerFailureStage.Stop);
            }

            _stateChanged(transition);
        }

        private SocketServerTransition ChangeStateLocked(
            SocketServerState state,
            SocketServerSettings? settings = null,
            Exception? exception = null,
            SocketServerFailureStage? failureStage = null)
        {
            _state = state;
            return new SocketServerTransition(++_transitionSequence, state, settings, exception, failureStage);
        }

        private static void SafeStop(ISocketServerListener listener)
        {
            try
            {
                listener.Stop();
            }
            catch
            {
            }
        }

        private void CloseClient(SocketServerClient connection)
        {
            if (connection.TryMarkClosed())
                _clientClosed(connection);
        }
    }

    internal sealed class SocketServerSession
    {
        private readonly object _resourceLock = new();
        private readonly HashSet<SocketServerClient> _clients = new();
        private ISocketServerListener? _listener;
        private int _stopRequested;

        public SocketServerSession(long version, SocketServerSettings settings)
        {
            Version = version;
            Settings = settings;
        }

        public long Version { get; }
        public SocketServerSettings Settings { get; }
        public bool IsStopRequested => Volatile.Read(ref _stopRequested) != 0;

        public int ClientCount
        {
            get
            {
                lock (_resourceLock)
                    return _clients.Count;
            }
        }

        public void RequestStop() => Interlocked.Exchange(ref _stopRequested, 1);

        public bool TryStart(ISocketServerListener listener)
        {
            lock (_resourceLock)
            {
                if (IsStopRequested)
                    return false;

                _listener = listener;
                listener.Start();
                return true;
            }
        }

        public ISocketServerListener? TakeListener()
        {
            lock (_resourceLock)
            {
                ISocketServerListener? listener = _listener;
                _listener = null;
                return listener;
            }
        }

        public void StopListener()
        {
            ISocketServerListener? listener = TakeListener();
            listener?.Stop();
        }

        public bool TryRegisterClient(SocketServerClient connection)
        {
            lock (_resourceLock)
            {
                if (IsStopRequested)
                    return false;
                return _clients.Add(connection);
            }
        }

        public bool RemoveClient(SocketServerClient connection)
        {
            lock (_resourceLock)
                return _clients.Remove(connection);
        }

        public IReadOnlyList<SocketServerClient> TakeClients()
        {
            lock (_resourceLock)
            {
                SocketServerClient[] clients = _clients.ToArray();
                _clients.Clear();
                return clients;
            }
        }
    }
}
