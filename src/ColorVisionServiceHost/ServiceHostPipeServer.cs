using System.IO.Pipes;
using Newtonsoft.Json;
using System.Security.AccessControl;
using System.Security.Principal;

namespace ColorVisionServiceHost;

internal delegate bool ServiceHostCallerResolver(
    NamedPipeServerStream pipe,
    out ServiceHostRequestContext context,
    out string error);

internal enum ServiceHostPipeServerState
{
    Created,
    Running,
    Stopping,
    Stopped,
    Faulted
}

internal interface IServiceHostPipeServerLifetime : IDisposable
{
    Task RunAsync(CancellationToken cancellationToken);

    Task StopAsync();
}

internal sealed class ServiceHostPipeServer : IServiceHostPipeServerLifetime
{
    private readonly Func<ServiceHostRequest, ServiceHostRequestContext, ServiceHostResponse> _handleRequest;
    private readonly ServiceHostCallerResolver _resolveCaller;
    private readonly string _pipeName;
    private readonly Func<NamedPipeServerStream> _createPipe;
    private readonly Func<Task> _beforeCommandAdmission;
    private readonly ServiceHostCommandAdmissionGate _commandGate;
    private readonly CancellationTokenSource _shutdownCancellation = new();
    private readonly object _clientTasksSync = new();
    private readonly HashSet<Task> _clientTasks = [];
    private readonly object _lifecycleSync = new();
    private readonly TaskCompletionSource _completion = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private Task _commandDrainTask = Task.CompletedTask;
    private ServiceHostPipeServerState _state = ServiceHostPipeServerState.Created;
    private bool _runStarted;
    private int _disposed;

    public ServiceHostPipeServer(ServiceHostCommandHandler handler)
        : this(
            handler.Handle,
            ServiceHostCallerIdentity.TryResolve,
            ServiceHostConstants.PipeName)
    {
    }

    internal ServiceHostPipeServer(
        Func<ServiceHostRequest, ServiceHostRequestContext, ServiceHostResponse> handleRequest,
        ServiceHostCallerResolver resolveCaller,
        string pipeName,
        Func<NamedPipeServerStream>? createPipe = null,
        Func<Task>? beforeCommandAdmission = null,
        Action<Exception>? reportCommandFailure = null)
    {
        _handleRequest = handleRequest;
        _resolveCaller = resolveCaller;
        _pipeName = pipeName;
        _createPipe = createPipe ?? (() => CreatePipe(pipeName));
        _beforeCommandAdmission = beforeCommandAdmission ?? (() => Task.CompletedTask);
        _commandGate = new ServiceHostCommandAdmissionGate(
            reportCommandFailure ?? ReportCommandFailure);
    }

    internal ServiceHostPipeServerState State
    {
        get
        {
            lock (_lifecycleSync)
                return _state;
        }
    }

    public Task RunAsync(CancellationToken cancellationToken)
    {
        bool startRun = false;
        lock (_lifecycleSync)
        {
            if (!_runStarted && _state == ServiceHostPipeServerState.Created)
            {
                _runStarted = true;
                _state = ServiceHostPipeServerState.Running;
                startRun = true;
            }
        }

        if (startRun)
            _ = CompleteRunAsync(cancellationToken);

        return _completion.Task;
    }

    public Task StopAsync()
    {
        BeginStop();
        return _completion.Task;
    }

    public void Dispose()
    {
        if (!_completion.Task.IsCompleted)
            throw new InvalidOperationException("Stop and await the pipe server before disposing it.");

        if (Interlocked.Exchange(ref _disposed, 1) == 0)
            _shutdownCancellation.Dispose();
    }

    private async Task CompleteRunAsync(CancellationToken cancellationToken)
    {
        Exception? failure = null;
        try
        {
            using CancellationTokenRegistration registration = cancellationToken.Register(
                static state => ((ServiceHostPipeServer)state!).BeginStop(),
                this);
            ServiceHostLog.Write($"Pipe server listening: {_pipeName}");
            await AcceptClientsAsync(_shutdownCancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_shutdownCancellation.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            failure = ex;
            ServiceHostLog.Write($"Pipe listener failed: {ex}");
        }

        try
        {
            Task commandDrainTask = BeginStop();
            await commandDrainTask.ConfigureAwait(false);
            await DrainClientsAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            failure ??= ex;
            ServiceHostLog.Write($"Pipe server shutdown failed: {ex}");
        }

        Complete(failure);
    }

    private async Task AcceptClientsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            NamedPipeServerStream pipe = _createPipe();

            try
            {
                await pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                ServiceHostLog.Write("Pipe client connected.");
                TrackClient(HandleClientAsync(pipe, cancellationToken));
            }
            catch
            {
                await pipe.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }
    }

    private Task BeginStop()
    {
        bool cancelIo = false;
        bool completeWithoutRun = false;
        Task commandDrainTask;

        lock (_lifecycleSync)
        {
            if (_state is ServiceHostPipeServerState.Created or ServiceHostPipeServerState.Running)
            {
                _state = ServiceHostPipeServerState.Stopping;
                _commandDrainTask = _commandGate.CloseAndDrainAsync();
                cancelIo = true;
                completeWithoutRun = !_runStarted;
            }

            commandDrainTask = _commandDrainTask;
        }

        if (cancelIo)
            _shutdownCancellation.Cancel();

        if (completeWithoutRun)
            _ = CompleteStopWithoutRunAsync(commandDrainTask);

        return commandDrainTask;
    }

    private async Task CompleteStopWithoutRunAsync(Task commandDrainTask)
    {
        Exception? failure = null;
        try
        {
            await commandDrainTask.ConfigureAwait(false);
            await DrainClientsAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            failure = ex;
            ServiceHostLog.Write($"Pipe server shutdown failed: {ex}");
        }

        Complete(failure);
    }

    private void Complete(Exception? failure)
    {
        lock (_lifecycleSync)
            _state = failure == null
                ? ServiceHostPipeServerState.Stopped
                : ServiceHostPipeServerState.Faulted;

        if (failure == null)
        {
            ServiceHostLog.Write("Pipe server stopped.");
            _completion.TrySetResult();
        }
        else
        {
            _completion.TrySetException(failure);
        }
    }

    private static PipeSecurity CreatePipeSecurity()
    {
        PipeSecurity security = new();
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            PipeAccessRights.FullControl,
            AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            PipeAccessRights.FullControl,
            AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.InteractiveSid, null),
            PipeAccessRights.ReadWrite,
            AccessControlType.Allow));
        return security;
    }

    private static NamedPipeServerStream CreatePipe(string pipeName)
    {
        return NamedPipeServerStreamAcl.Create(
            pipeName,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            0,
            0,
            CreatePipeSecurity());
    }

    private async Task HandleClientAsync(
        NamedPipeServerStream pipe,
        CancellationToken ioCancellationToken)
    {
        await using (pipe.ConfigureAwait(false))
        {
            using StreamReader reader = new(pipe, ServiceHostJson.Encoding, false, leaveOpen: true);
            using StreamWriter writer = new(pipe, ServiceHostJson.Encoding, leaveOpen: true) { AutoFlush = true };

            try
            {
                ServiceHostLog.Write("Reading pipe request.");
                string? requestJson = await reader
                    .ReadLineAsync(ioCancellationToken)
                    .ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(requestJson))
                    return;

                // Windows requires the server to read from the pipe before impersonating its client.
                if (!_resolveCaller(pipe, out ServiceHostRequestContext context, out string identityError))
                {
                    ServiceHostLog.Write($"Pipe caller rejected: {identityError}");
                    await WriteResponseAsync(
                        writer,
                        ServiceHostResponse.FromObject(string.Empty, false, "untrusted_pipe_client"),
                        ioCancellationToken).ConfigureAwait(false);
                    return;
                }

                ServiceHostRequest? request = JsonConvert.DeserializeObject<ServiceHostRequest>(requestJson, ServiceHostJson.Settings);
                if (request == null || string.IsNullOrWhiteSpace(request.Command))
                {
                    await WriteResponseAsync(
                        writer,
                        ServiceHostResponse.FromObject(string.Empty, false, "Invalid request."),
                        ioCancellationToken).ConfigureAwait(false);
                    return;
                }

                await _beforeCommandAdmission().ConfigureAwait(false);
                if (!_commandGate.TryRun(
                        () => _handleRequest(request, context),
                        out Task<ServiceHostResponse>? commandTask))
                {
                    ServiceHostLog.Write("Pipe request rejected because service shutdown has started.");
                    return;
                }

                ServiceHostResponse response = await commandTask!.ConfigureAwait(false);
                await WriteResponseAsync(writer, response, ioCancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ioCancellationToken.IsCancellationRequested)
            {
                ServiceHostLog.Write("Pipe client I/O canceled.");
            }
            catch (Exception ex)
            {
                ServiceHostLog.Write($"Pipe client failed: {ex}");
            }
        }
    }

    private static Task WriteResponseAsync(
        StreamWriter writer,
        ServiceHostResponse response,
        CancellationToken cancellationToken)
    {
        string responseJson = JsonConvert.SerializeObject(response, ServiceHostJson.Settings);
        return writer.WriteLineAsync(responseJson.AsMemory(), cancellationToken);
    }

    private void TrackClient(Task clientTask)
    {
        lock (_clientTasksSync)
            _clientTasks.Add(clientTask);

        _ = clientTask.ContinueWith(
            static (completedTask, state) =>
                ((ServiceHostPipeServer)state!).ObserveAndRemoveClient(completedTask),
            this,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private void ObserveAndRemoveClient(Task clientTask)
    {
        if (clientTask.IsFaulted)
            ServiceHostLog.Write($"Pipe client task failed: {clientTask.Exception!.Flatten()}");

        lock (_clientTasksSync)
            _clientTasks.Remove(clientTask);
    }

    private async Task DrainClientsAsync()
    {
        Task[] clients;
        lock (_clientTasksSync)
            clients = _clientTasks.ToArray();

        if (clients.Length == 0)
            return;

        ServiceHostLog.Write($"Draining {clients.Length} pipe client(s).");
        try
        {
            await Task.WhenAll(clients).ConfigureAwait(false);
        }
        catch
        {
            // ObserveAndRemoveClient logs and observes every individual client fault.
        }
        ServiceHostLog.Write("Pipe clients drained.");
    }

    private static void ReportCommandFailure(Exception failure)
    {
        ServiceHostLog.Write($"Privileged pipe command failed: {failure}");
    }
}

internal sealed class ServiceHostCommandAdmissionGate
{
    private readonly object _sync = new();
    private readonly HashSet<Task> _activeCommands = [];
    private readonly Action<Exception> _reportFailure;
    private readonly Action _afterAcceptCheckBeforeRegistration;
    private readonly Action _beforeCloseLock;
    private TaskCompletionSource? _drainCompletion;
    private bool _accepting = true;

    public ServiceHostCommandAdmissionGate(
        Action<Exception> reportFailure,
        Action? afterAcceptCheckBeforeRegistration = null,
        Action? beforeCloseLock = null)
    {
        _reportFailure = reportFailure;
        _afterAcceptCheckBeforeRegistration = afterAcceptCheckBeforeRegistration ?? (() => { });
        _beforeCloseLock = beforeCloseLock ?? (() => { });
    }

    public bool TryRun<TResult>(Func<TResult> command, out Task<TResult>? commandTask)
    {
        lock (_sync)
        {
            if (!_accepting)
            {
                commandTask = null;
                return false;
            }

            _afterAcceptCheckBeforeRegistration();
            commandTask = Task.Run(command, CancellationToken.None);
            _activeCommands.Add(commandTask);
            _ = commandTask.ContinueWith(
                static (completedTask, state) =>
                    ((ServiceHostCommandAdmissionGate)state!).ObserveAndRemove(completedTask),
                this,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            return true;
        }
    }

    public Task CloseAndDrainAsync()
    {
        _beforeCloseLock();
        lock (_sync)
        {
            if (_accepting)
                _accepting = false;

            if (_activeCommands.Count == 0)
                return Task.CompletedTask;

            _drainCompletion ??= new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            return _drainCompletion.Task;
        }
    }

    private void ObserveAndRemove(Task commandTask)
    {
        if (commandTask.IsFaulted)
        {
            Exception failure = commandTask.Exception!.Flatten();
            try
            {
                _reportFailure(failure);
            }
            catch (Exception observerFailure)
            {
                ServiceHostLog.Write($"Command failure observer failed: {observerFailure}");
            }
        }

        TaskCompletionSource? drainCompletion = null;
        lock (_sync)
        {
            _activeCommands.Remove(commandTask);
            if (!_accepting && _activeCommands.Count == 0)
                drainCompletion = _drainCompletion;
        }

        drainCompletion?.TrySetResult();
    }
}
