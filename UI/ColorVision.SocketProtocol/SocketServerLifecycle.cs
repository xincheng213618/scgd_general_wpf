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

        public bool ConflictsWith(SocketServerSettings other)
        {
            if (ServerPort == 0 || other.ServerPort == 0 || ServerPort != other.ServerPort)
                return false;

            if (!System.Net.IPAddress.TryParse(IPAddress, out System.Net.IPAddress? address)
                || !System.Net.IPAddress.TryParse(other.IPAddress, out System.Net.IPAddress? otherAddress))
                return string.Equals(IPAddress, other.IPAddress, StringComparison.OrdinalIgnoreCase);

            return address.Equals(otherAddress)
                || address.Equals(System.Net.IPAddress.Any)
                || address.Equals(System.Net.IPAddress.IPv6Any)
                || otherAddress.Equals(System.Net.IPAddress.Any)
                || otherAddress.Equals(System.Net.IPAddress.IPv6Any);
        }
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
        private sealed record DeferredStart(long Version, SocketServerSettings Settings);

        private readonly object _stateLock = new();
        private readonly object _shutdownLock = new();
        private readonly ISocketServerListenerFactory _listenerFactory;
        private readonly Action<Action> _queueWork;
        private readonly Action<Action> _queueShutdownWork;
        private readonly SocketWorkerTracker _workerTracker;
        private readonly Action<SocketServerTransition> _stateChanged;
        private readonly Action<SocketServerClient> _clientAccepted;
        private readonly Action<SocketServerClient> _clientClosed;
        private readonly HashSet<SocketServerSession> _sessions = new();
        private readonly HashSet<SocketServerSession> _listenerReleasePending = new();
        private SocketServerSession? _currentSession;
        private DeferredStart? _deferredStart;
        private SocketWorkerLease? _shutdownCoordinatorLease;
        private Exception? _shutdownException;
        private SocketServerState _state;
        private bool _shutdownAttemptRunning;
        private bool _shutdownCompleted;
        private bool _shutdownRetryRequested;
        private int _shutdownStarted;
        private long _intentVersion;
        private long _sessionVersion;
        private long _transitionSequence;

        public SocketServerLifecycle(
            SocketServerState initialState,
            ISocketServerListenerFactory listenerFactory,
            Action<Action> queueWork,
            SocketWorkerTracker workerTracker,
            Action<SocketServerTransition> stateChanged,
            Action<SocketServerClient> clientAccepted,
            Action<SocketServerClient> clientClosed,
            Action<Action>? queueShutdownWork = null)
        {
            _state = initialState;
            _listenerFactory = listenerFactory;
            _queueWork = queueWork;
            _workerTracker = workerTracker;
            _stateChanged = stateChanged;
            _clientAccepted = clientAccepted;
            _clientClosed = clientClosed;
            _queueShutdownWork = queueShutdownWork ?? (action => _ = Task.Run(action));
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
                long intentVersion = _intentVersion;
                if (!runInline)
                {
                    intentVersion = ++_intentVersion;
                    _deferredStart = null;
                }
                if (Volatile.Read(ref _shutdownStarted) != 0
                    || _state is SocketServerState.Starting or SocketServerState.Running)
                    return false;
                if (HasPendingListenerLocked(settings))
                {
                    if (!runInline)
                        _deferredStart = new DeferredStart(intentVersion, settings);
                    return false;
                }
                if (!_workerTracker.TryRegister(out SocketWorkerLease? registeredLease))
                    return false;
                if (runInline)
                {
                    ++_intentVersion;
                    _deferredStart = null;
                }

                session = new SocketServerSession(++_sessionVersion, settings);
                _sessions.Add(session);
                _currentSession = session;
                transition = ChangeStateLocked(SocketServerState.Starting, settings);
                workerLease = registeredLease;
            }

            RunStartOperation(session, transition, workerLease, runInline);
            return true;
        }

        private void RunStartOperation(
            SocketServerSession session,
            SocketServerTransition transition,
            SocketWorkerLease workerLease,
            bool runInline)
        {
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
                ++_intentVersion;
                _deferredStart = null;
                if (Volatile.Read(ref _shutdownStarted) != 0 || _state == SocketServerState.Stopping)
                    return false;
                if (!_workerTracker.TryRegister(out SocketWorkerLease? registeredLease))
                    return false;

                session = _currentSession ?? _listenerReleasePending.FirstOrDefault();
                session?.RequestStop();
                if (session != null)
                    _listenerReleasePending.Add(session);
                _currentSession = null;
                stopVersion = ++_sessionVersion;
                transition = ChangeStateLocked(SocketServerState.Stopping);
                workerLease = registeredLease;
            }

            try
            {
                if (session != null)
                {
                    CloseSessionResources(session);
                    MarkListenerReleased(session);
                }
            }
            catch (Exception exception)
            {
                listenerStopException = exception;
            }

            QueueTrackedWork(
                workerLease,
                () => StopSession(session, stopVersion, isServerEnabled, listenerStopException));
            _stateChanged(transition);
            return true;
        }

        public void BeginShutdown()
        {
            if (Volatile.Read(ref _shutdownStarted) == 0)
            {
                if (!_workerTracker.TryRegister(out SocketWorkerLease? coordinatorLease))
                    return;

                bool ownsShutdown = false;
                lock (_shutdownLock)
                {
                    if (Volatile.Read(ref _shutdownStarted) == 0)
                    {
                        // The coordinator lease is installed before the terminal flag
                        // becomes visible, so no waiter can observe shutdown without an
                        // incomplete tracker. It remains owned across failed attempts.
                        _shutdownCoordinatorLease = coordinatorLease;
                        _shutdownAttemptRunning = true;
                        Volatile.Write(ref _shutdownStarted, 1);
                        ownsShutdown = true;
                    }
                }

                if (ownsShutdown)
                {
                    _workerTracker.BeginShutdown();
                    QueueShutdownAttempt();
                    return;
                }

                coordinatorLease.Dispose();
            }

            TryQueueShutdownRetry();
        }

        private void TryQueueShutdownRetry()
        {
            lock (_shutdownLock)
            {
                if (_shutdownCompleted || _shutdownCoordinatorLease == null)
                    return;
                if (_shutdownAttemptRunning)
                {
                    _shutdownRetryRequested = true;
                    return;
                }

                _shutdownAttemptRunning = true;
            }

            QueueShutdownAttempt();
        }

        private void QueueShutdownAttempt()
        {
            try
            {
                _queueShutdownWork(RunShutdownAttempt);
            }
            catch (Exception exception)
            {
                CompleteShutdownAttempt(exception, resourcesReleased: false);
            }
        }

        private void RunShutdownAttempt()
        {
            Exception? failure;
            try
            {
                failure = ShutdownSessions();
            }
            catch (Exception exception)
            {
                failure = exception;
            }

            bool resourcesReleased;
            lock (_stateLock)
                resourcesReleased = _sessions.Count == 0;
            CompleteShutdownAttempt(failure, resourcesReleased);
        }

        private void CompleteShutdownAttempt(Exception? failure, bool resourcesReleased)
        {
            SocketWorkerLease? completedLease = null;
            bool queueRetry = false;
            lock (_shutdownLock)
            {
                if (failure == null && resourcesReleased)
                {
                    Volatile.Write(ref _shutdownException, null);
                    _shutdownCompleted = true;
                    completedLease = _shutdownCoordinatorLease;
                    _shutdownCoordinatorLease = null;
                    _shutdownRetryRequested = false;
                    _shutdownAttemptRunning = false;
                }
                else
                {
                    Volatile.Write(
                        ref _shutdownException,
                        failure ?? new InvalidOperationException("Socket shutdown left resources pending."));
                    queueRetry = _shutdownRetryRequested;
                    _shutdownRetryRequested = false;
                    _shutdownAttemptRunning = queueRetry;
                }
            }

            // Error/success publication precedes lease completion. A waiter that sees
            // the tracker finish therefore also sees the final cleanup result.
            completedLease?.Dispose();
            if (queueRetry)
                QueueShutdownAttempt();
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
                    bool resourcesReleased = false;
                    try
                    {
                        CloseSessionResources(session);
                        resourcesReleased = true;
                        MarkListenerReleased(session);
                    }
                    catch (Exception exception)
                    {
                        failure ??= exception;
                        MarkListenerReleasePending(session);
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
                        if (resourcesReleased)
                        {
                            RemoveSession(session);
                            TryStartDeferred();
                        }
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
            bool resourcesReleased = false;
            try
            {
                if (session != null)
                {
                    CloseSessionResources(session);
                    resourcesReleased = true;
                    MarkListenerReleased(session);
                }
            }
            catch (Exception exception)
            {
                failure ??= exception;
                if (session != null)
                    MarkListenerReleasePending(session);
            }
            finally
            {
                if (session != null && resourcesReleased)
                    RemoveSession(session);
            }

            if (failure == null)
                TryCompleteStop(stopVersion, isServerEnabled ? SocketServerState.Stopped : SocketServerState.Disabled);
            else
                TryFailStop(stopVersion, failure);

            if (resourcesReleased)
                TryStartDeferred();
        }

        private void TryStartDeferred()
        {
            SocketServerSession session;
            SocketServerTransition transition;
            SocketWorkerLease workerLease;
            lock (_stateLock)
            {
                DeferredStart? deferred = _deferredStart;
                if (deferred == null
                    || deferred.Version != _intentVersion
                    || Volatile.Read(ref _shutdownStarted) != 0
                    || _state is SocketServerState.Starting or SocketServerState.Running
                    || HasPendingListenerLocked(deferred.Settings))
                    return;
                if (!_workerTracker.TryRegister(out SocketWorkerLease? registeredLease))
                    return;

                _deferredStart = null;
                session = new SocketServerSession(++_sessionVersion, deferred.Settings);
                _sessions.Add(session);
                _currentSession = session;
                transition = ChangeStateLocked(SocketServerState.Starting, deferred.Settings);
                workerLease = registeredLease;
            }

            RunStartOperation(session, transition, workerLease, runInline: false);
        }

        private void CloseSessionResources(SocketServerSession session)
        {
            using SocketSessionCleanupClaim cleanup = session.ClaimCleanup();
            if (cleanup.IsCompleted)
                return;

            Exception? firstException = null;
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

            try
            {
                session.StopListener();
            }
            catch (Exception exception)
            {
                firstException ??= exception;
            }

            if (firstException != null)
                throw firstException;

            cleanup.Complete();
        }

        private Exception? CleanupForShutdown(SocketServerSession session)
        {
            bool resourcesReleased = false;
            try
            {
                CloseSessionResources(session);
                resourcesReleased = true;
                MarkListenerReleased(session);
                return null;
            }
            catch (Exception exception)
            {
                MarkListenerReleasePending(session);
                return exception;
            }
            finally
            {
                if (resourcesReleased)
                    RemoveSession(session);
            }
        }

        private Exception? ShutdownSessions()
        {
            SocketServerSession[] sessions;
            lock (_stateLock)
            {
                ++_intentVersion;
                _deferredStart = null;
                _currentSession = null;
                ++_sessionVersion;
                _state = SocketServerState.Stopping;
                sessions = _sessions.ToArray();
                foreach (SocketServerSession session in sessions)
                    session.RequestStop();
            }

            var cleanupTasks = new List<Task<Exception?>>(sessions.Length);
            Exception? firstFailure = null;
            foreach (SocketServerSession session in sessions)
            {
                try
                {
                    cleanupTasks.Add(Task.Run(() => CleanupForShutdown(session)));
                }
                catch (Exception exception)
                {
                    firstFailure ??= exception;
                }
            }

            try
            {
                Exception?[] failures = Task.WhenAll(cleanupTasks).GetAwaiter().GetResult();
                firstFailure ??= failures.FirstOrDefault(exception => exception != null);
            }
            catch (Exception exception)
            {
                firstFailure ??= exception;
            }

            return firstFailure;
        }

        private void RemoveSession(SocketServerSession session)
        {
            lock (_stateLock)
            {
                _sessions.Remove(session);
                _listenerReleasePending.Remove(session);
            }
        }

        private bool HasPendingListenerLocked(SocketServerSettings settings) =>
            _listenerReleasePending.Any(session => session.Settings.ConflictsWith(settings));

        private void MarkListenerReleasePending(SocketServerSession session)
        {
            lock (_stateLock)
                _listenerReleasePending.Add(session);
        }

        private void MarkListenerReleased(SocketServerSession session)
        {
            lock (_stateLock)
                _listenerReleasePending.Remove(session);
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
        private readonly object _cleanupLock = new();
        private readonly HashSet<SocketServerClient> _clients = new();
        private readonly TaskCompletionSource<bool> _resourcesReleased = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private ISocketServerListener? _listener;
        private bool _cleanupCompleted;
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

        public void StopListener()
        {
            ISocketServerListener? listener;
            lock (_resourceLock)
                listener = _listener;
            if (listener == null)
                return;

            listener.Stop();
            lock (_resourceLock)
            {
                if (ReferenceEquals(_listener, listener))
                    _listener = null;
            }
        }

        public Task ResourcesReleased => _resourcesReleased.Task;

        public SocketSessionCleanupClaim ClaimCleanup()
        {
            Monitor.Enter(_cleanupLock);
            return new SocketSessionCleanupClaim(this, _cleanupCompleted);
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

        internal void CompleteCleanup()
        {
            _cleanupCompleted = true;
            _resourcesReleased.TrySetResult(true);
        }

        internal void ReleaseCleanupClaim() => Monitor.Exit(_cleanupLock);
    }

    internal sealed class SocketSessionCleanupClaim : IDisposable
    {
        private SocketServerSession? _session;

        internal SocketSessionCleanupClaim(SocketServerSession session, bool isCompleted)
        {
            _session = session;
            IsCompleted = isCompleted;
        }

        public bool IsCompleted { get; }

        public void Complete()
        {
            SocketServerSession session = _session
                ?? throw new ObjectDisposedException(nameof(SocketSessionCleanupClaim));
            session.CompleteCleanup();
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref _session, null)?.ReleaseCleanupClaim();
        }
    }
}
