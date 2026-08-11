using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ProjectARVRPro.Services;

internal enum SocketRelayWriteStatus
{
    Sent,
    NoConnection,
    Failed
}

internal readonly record struct SocketRelayWriteResult(SocketRelayWriteStatus Status, Exception? Error = null);

internal readonly record struct SocketRelayStopResult(bool Completed, int RemainingWorkerCount);

/// <summary>
/// Owns every socket and worker created by one server start. No worker reads manager-level
/// socket state, so a stopped generation cannot adopt sockets created by a later start.
/// </summary>
internal sealed class SocketRelayGeneration : IDisposable
{
    private static long _nextGenerationId;
    private static long _nextConnectionId;

    private readonly object _connectionLock = new();
    private readonly CancellationTokenSource _stopCancellation = new();
    private readonly TcpListener _listener;
    private readonly List<SocketRelayConnection> _activeConnections = [];
    private readonly List<Thread> _readerThreads = [];
    private readonly Thread _listenerThread;
    private SocketRelayConnection? _currentConnection;
    private IPEndPoint? _listeningEndpoint;
    private int _isListening;
    private int _started;
    private int _stopRequested;
    private int _disposed;

    private readonly object _sensorResetLock = new();
    private bool _sensorResetCompleted;

    internal SocketRelayGeneration(IPAddress address, int port)
    {
        Id = Interlocked.Increment(ref _nextGenerationId);
        _listener = new TcpListener(address, port);
        _listenerThread = new Thread(ListenLoop)
        {
            IsBackground = true,
            Name = $"RelayServerListener-{Id}"
        };
    }

    internal long Id { get; }

    internal bool IsListening => Volatile.Read(ref _isListening) != 0;

    internal IPEndPoint? ListeningEndpoint => Volatile.Read(ref _listeningEndpoint);

    internal long? CurrentConnectionId
    {
        get
        {
            lock (_connectionLock)
            {
                return _currentConnection?.Id;
            }
        }
    }

    internal int ActiveConnectionCount
    {
        get
        {
            lock (_connectionLock)
            {
                return _activeConnections.Count;
            }
        }
    }

    internal event Action<SocketRelayGeneration>? Listening;
    internal event Action<SocketRelayGeneration>? ListeningStopped;
    internal event Action<SocketRelayGeneration, SocketRelayConnection>? FlowConnected;
    internal event Action<SocketRelayGeneration, SocketRelayConnection>? FlowDisconnected;
    internal event Action<SocketRelayGeneration, SocketRelayConnection, string>? FlowMessageReceived;
    internal event Action<SocketRelayGeneration, Exception>? ListenerError;
    internal event Action<SocketRelayGeneration, SocketRelayConnection, Exception>? FlowReadError;

    internal void Start()
    {
        if (Interlocked.Exchange(ref _started, 1) != 0)
        {
            throw new InvalidOperationException("Socket relay generation can only be started once.");
        }

        _listenerThread.Start();
    }

    internal SocketRelayWriteResult WriteToCurrent(string message)
    {
        if (_stopCancellation.IsCancellationRequested)
        {
            return new SocketRelayWriteResult(SocketRelayWriteStatus.NoConnection);
        }

        SocketRelayConnection? connection;
        lock (_connectionLock)
        {
            connection = _currentConnection;
        }

        if (connection == null)
        {
            return new SocketRelayWriteResult(SocketRelayWriteStatus.NoConnection);
        }

        byte[] bytes = Encoding.UTF8.GetBytes(message);
        return connection.TryWrite(bytes, _stopCancellation.Token);
    }

    internal bool IsCurrentConnection(SocketRelayConnection connection)
    {
        lock (_connectionLock)
        {
            return ReferenceEquals(_currentConnection, connection);
        }
    }

    internal SocketRelayStopResult StopAndWait(TimeSpan timeout)
    {
        if (timeout < TimeSpan.Zero && timeout != Timeout.InfiniteTimeSpan)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        RequestStop();

        Stopwatch stopwatch = Stopwatch.StartNew();
        bool listenerExited = WaitForThread(_listenerThread, timeout, stopwatch);

        Thread[] readerThreads;
        lock (_connectionLock)
        {
            readerThreads = _readerThreads.ToArray();
        }

        foreach (Thread readerThread in readerThreads)
        {
            WaitForThread(readerThread, timeout, stopwatch);
        }

        int remainingWorkerCount = (_listenerThread.IsAlive ? 1 : 0) + readerThreads.Count(thread => thread.IsAlive);
        return new SocketRelayStopResult(listenerExited && remainingWorkerCount == 0, remainingWorkerCount);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        StopAndWait(Timeout.InfiniteTimeSpan);
        _stopCancellation.Dispose();
    }

    internal bool IsSensorResetCompleted
    {
        get
        {
            lock (_sensorResetLock)
            {
                return _sensorResetCompleted;
            }
        }
    }

    internal void CompleteSensorReset(bool completed)
    {
        lock (_sensorResetLock)
        {
            if (completed)
            {
                _sensorResetCompleted = true;
            }

        }
    }

    private void ListenLoop()
    {
        try
        {
            if (_stopCancellation.IsCancellationRequested)
            {
                return;
            }

            _listener.Start();
            Volatile.Write(ref _listeningEndpoint, (IPEndPoint)_listener.LocalEndpoint);
            Volatile.Write(ref _isListening, 1);

            if (_stopCancellation.IsCancellationRequested)
            {
                _listener.Stop();
                return;
            }

            NotifySafely(() => Listening?.Invoke(this));

            while (!_stopCancellation.IsCancellationRequested)
            {
                TcpClient client = _listener.AcceptTcpClient();
                if (!TryRegisterConnection(client, out SocketRelayConnection connection))
                {
                    client.Dispose();
                    break;
                }

                connection.ReaderThread.Start();
                TrackReaderThread(connection.ReaderThread);
                NotifySafely(() => FlowConnected?.Invoke(this, connection));
            }
        }
        catch (SocketException) when (_stopCancellation.IsCancellationRequested)
        {
        }
        catch (ObjectDisposedException) when (_stopCancellation.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            if (!_stopCancellation.IsCancellationRequested)
            {
                ReportListenerError(ex);
            }
        }
        finally
        {
            Volatile.Write(ref _isListening, 0);
            Volatile.Write(ref _listeningEndpoint, null);
            NotifySafely(() => ListeningStopped?.Invoke(this));
        }
    }

    private bool TryRegisterConnection(
        TcpClient client,
        out SocketRelayConnection connection)
    {
        connection = null!;

        SocketRelayConnection candidate;
        try
        {
            candidate = new SocketRelayConnection(
                Interlocked.Increment(ref _nextConnectionId),
                client,
                ReadFlowMessages);
        }
        catch
        {
            client.Dispose();
            throw;
        }

        SocketRelayConnection? observedConnection;
        lock (_connectionLock)
        {
            observedConnection = _currentConnection;
        }

        // Retire and close the old socket before publishing its replacement. Close/Shutdown
        // interrupts an in-flight write without making the listener wait on the send lock.
        observedConnection?.Close();

        lock (_connectionLock)
        {
            if (_stopCancellation.IsCancellationRequested)
            {
                candidate.Close();
                return false;
            }

            connection = candidate;
            _currentConnection = candidate;
            _activeConnections.Add(candidate);
            return true;
        }
    }

    private void ReadFlowMessages(SocketRelayConnection connection)
    {
        byte[] buffer = new byte[4096];
        try
        {
            while (!_stopCancellation.IsCancellationRequested)
            {
                int bytesRead = connection.Stream.Read(buffer, 0, buffer.Length);
                if (bytesRead == 0)
                {
                    break;
                }

                if (_stopCancellation.IsCancellationRequested || !IsCurrentConnection(connection))
                {
                    break;
                }

                string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                NotifySafely(() => FlowMessageReceived?.Invoke(this, connection, message));
            }
        }
        catch (Exception ex)
        {
            if (!_stopCancellation.IsCancellationRequested && IsCurrentConnection(connection))
            {
                NotifySafely(() => FlowReadError?.Invoke(this, connection, ex));
            }
        }
        finally
        {
            connection.Deactivate();

            bool wasCurrent;
            lock (_connectionLock)
            {
                _activeConnections.Remove(connection);
                wasCurrent = ReferenceEquals(_currentConnection, connection);
                if (wasCurrent)
                {
                    _currentConnection = null;
                }
            }

            connection.Close();
            if (wasCurrent)
            {
                NotifySafely(() => FlowDisconnected?.Invoke(this, connection));
            }
        }
    }

    private void TrackReaderThread(Thread readerThread)
    {
        lock (_connectionLock)
        {
            _readerThreads.RemoveAll(thread => (thread.ThreadState & System.Threading.ThreadState.Stopped) != 0);
            _readerThreads.Add(readerThread);
        }
    }

    private void NotifySafely(Action notification)
    {
        try
        {
            notification();
        }
        catch (Exception ex)
        {
            ReportListenerError(ex);
        }
    }

    private void ReportListenerError(Exception error)
    {
        try
        {
            ListenerError?.Invoke(this, error);
        }
        catch
        {
        }
    }

    internal void RequestStop()
    {
        if (Interlocked.Exchange(ref _stopRequested, 1) != 0)
        {
            return;
        }

        _stopCancellation.Cancel();

        try
        {
            _listener.Stop();
        }
        catch (SocketException)
        {
        }

        SocketRelayConnection[] connections;
        lock (_connectionLock)
        {
            _currentConnection = null;
            connections = _activeConnections.ToArray();
        }

        foreach (SocketRelayConnection connection in connections)
        {
            connection.Close();
        }
    }

    private static bool WaitForThread(Thread thread, TimeSpan timeout, Stopwatch stopwatch)
    {
        if (ReferenceEquals(thread, Thread.CurrentThread) || !thread.IsAlive)
        {
            return !ReferenceEquals(thread, Thread.CurrentThread);
        }

        if (timeout == Timeout.InfiniteTimeSpan)
        {
            thread.Join();
            return true;
        }

        TimeSpan remaining = timeout - stopwatch.Elapsed;
        return thread.Join(remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero);
    }
}

internal sealed class SocketRelayConnection
{
    private readonly object _sendLock = new();
    private readonly TcpClient _client;
    private int _active = 1;
    private int _closed;

    internal SocketRelayConnection(long id, TcpClient client, Action<SocketRelayConnection> readLoop)
    {
        Id = id;
        _client = client;
        Stream = client.GetStream();
        ReaderThread = new Thread(() => readLoop(this))
        {
            IsBackground = true,
            Name = $"RelayFlowReader-{id}"
        };
    }

    internal long Id { get; }

    internal NetworkStream Stream { get; }

    internal Thread ReaderThread { get; }

    internal string RemoteEndpoint => _client.Client.RemoteEndPoint?.ToString() ?? "Unknown";

    internal SocketRelayWriteResult TryWrite(byte[] bytes, CancellationToken stopToken)
    {
        lock (_sendLock)
        {
            if (stopToken.IsCancellationRequested || Volatile.Read(ref _active) == 0 || Volatile.Read(ref _closed) != 0)
            {
                return new SocketRelayWriteResult(SocketRelayWriteStatus.NoConnection);
            }

            try
            {
                Stream.Write(bytes, 0, bytes.Length);
                Stream.Flush();
                return new SocketRelayWriteResult(SocketRelayWriteStatus.Sent);
            }
            catch (Exception ex)
            {
                return new SocketRelayWriteResult(SocketRelayWriteStatus.Failed, ex);
            }
        }
    }

    internal void Deactivate()
    {
        Volatile.Write(ref _active, 0);
    }

    internal void Close()
    {
        Deactivate();
        if (Interlocked.Exchange(ref _closed, 1) != 0)
        {
            return;
        }

        try
        {
            _client.Client.Shutdown(SocketShutdown.Both);
        }
        catch (SocketException)
        {
        }
        catch (ObjectDisposedException)
        {
        }

        try
        {
            Stream.Close();
        }
        catch (IOException)
        {
        }

        _client.Close();
    }
}
