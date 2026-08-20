using System.ServiceProcess;

namespace ColorVisionServiceHost;

internal class ColorVisionServiceHostService : ServiceBase
{
    private static readonly TimeSpan NormalStopBudget = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan StopProgressInterval = TimeSpan.FromSeconds(5);
    private const int StopWaitHintMilliseconds = 15000;

    private readonly object _lifecycleSync = new();
    private readonly Func<IServiceHostPipeServerLifetime> _serverFactory;
    private readonly IApplicationUpdateScanProtectionLifetime _scanProtection;
    private readonly IApplicationStartupIntegrityMonitorLifetime _startupIntegrityMonitor;
    private readonly TimeProvider _timeProvider;
    private readonly Func<Task, TimeSpan, bool>? _waitForCompletion;
    private readonly Action<int> _requestAdditionalTime;
    private readonly Action<TimeSpan> _reportOverBudget;
    private readonly Action<Exception> _reportShutdownFailure;
    private IServiceHostPipeServerLifetime? _server;
    private Task? _runTask;
    private Task? _shutdownTask;

    public ColorVisionServiceHostService()
        : this(
            static () => new ServiceHostPipeServer(new ServiceHostCommandHandler()),
            ApplicationUpdateScanProtectionService.Default,
            startupIntegrityMonitor: ApplicationStartupIntegrityMonitor.Default)
    {
    }

    internal ColorVisionServiceHostService(
        Func<IServiceHostPipeServerLifetime> serverFactory,
        IApplicationUpdateScanProtectionLifetime scanProtection,
        TimeProvider? timeProvider = null,
        Func<Task, TimeSpan, bool>? waitForCompletion = null,
        Action<int>? requestAdditionalTime = null,
        Action<TimeSpan>? reportOverBudget = null,
        Action<Exception>? reportShutdownFailure = null,
        IApplicationStartupIntegrityMonitorLifetime? startupIntegrityMonitor = null)
    {
        _serverFactory = serverFactory ?? throw new ArgumentNullException(nameof(serverFactory));
        _scanProtection = scanProtection ?? throw new ArgumentNullException(nameof(scanProtection));
        _startupIntegrityMonitor = startupIntegrityMonitor ?? NullApplicationStartupIntegrityMonitor.Instance;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _waitForCompletion = waitForCompletion;
        _requestAdditionalTime = requestAdditionalTime ?? RequestAdditionalTime;
        _reportOverBudget = reportOverBudget ?? (elapsed =>
            ServiceHostLog.Write(
                $"Service stop exceeded the normal {NormalStopBudget} budget after {elapsed}; remaining in STOP_PENDING until owned work drains."));
        _reportShutdownFailure = reportShutdownFailure ?? (failure =>
            ServiceHostLog.Write($"Service shutdown component failed after stop admission closed: {failure}"));

        ServiceName = ServiceHostConstants.ServiceName;
        CanStop = true;
        CanPauseAndContinue = false;
        AutoLog = true;
    }

    protected override void OnStart(string[] args)
    {
        ServiceHostLog.Write("Service starting.");
        lock (_lifecycleSync)
        {
            if (_server != null || _shutdownTask != null)
                throw new InvalidOperationException("The service host lifecycle has already started.");
        }

        IServiceHostPipeServerLifetime server = _serverFactory()
            ?? throw new InvalidOperationException("The pipe server factory returned null.");
        try
        {
            Task startupCleanupTask = _scanProtection.Start()
                ?? throw new InvalidOperationException("Scan-protection startup returned a null task.");
            ObserveBackgroundFailure(startupCleanupTask, "Scan-protection startup cleanup");
            Task startupIntegrityTask = _startupIntegrityMonitor.Start()
                ?? throw new InvalidOperationException("Startup-integrity monitoring returned a null startup task.");
            ObserveBackgroundFailure(startupIntegrityTask, "Application startup-integrity monitoring");

            Task runTask = server.RunAsync(CancellationToken.None)
                ?? throw new InvalidOperationException("The pipe server returned a null run task.");
            if (runTask.IsCompleted)
            {
                runTask.GetAwaiter().GetResult();
                throw new InvalidOperationException("The pipe server exited during service startup.");
            }

            lock (_lifecycleSync)
            {
                _server = server;
                _runTask = runTask;
            }

            ObserveRunFailure(runTask);
            ServiceHostLog.Write("Service started.");
        }
        catch (Exception ex)
        {
            ServiceHostLog.Write($"Service start failed; draining partially started resources: {ex}");
            Task rollbackTask = CompleteShutdownAsync(
                server,
                InvokeStop(server.StopAsync),
                InvokeStop(_scanProtection.StopAsync),
                InvokeStop(_startupIntegrityMonitor.StopAsync));
            WaitForTerminalShutdown(rollbackTask);
            throw;
        }
    }

    protected override void OnStop()
    {
        long stopStartedTimestamp = _timeProvider.GetTimestamp();
        ServiceHostLog.Write("Service stopping.");
        Task shutdownTask = BeginShutdown();

        try
        {
            WaitForShutdown(
                shutdownTask,
                NormalStopBudget,
                StopProgressInterval,
                StopWaitHintMilliseconds,
                _requestAdditionalTime,
                _reportOverBudget,
                _timeProvider,
                stopStartedTimestamp,
                _waitForCompletion);
        }
        catch (Exception ex)
        {
            // Once command admission and cleanup scheduling are closed, throwing would make
            // ServiceBase restore SCM to Running while the service can no longer serve requests.
            ReportShutdownFailure(ex);
            WaitForTerminalShutdown(shutdownTask);
        }

        ServiceHostLog.Write("Service stopped.");
    }

    private Task BeginShutdown()
    {
        lock (_lifecycleSync)
        {
            if (_shutdownTask != null)
                return _shutdownTask;

            IServiceHostPipeServerLifetime? server = _server;
            Task serverStopTask = server == null
                ? Task.CompletedTask
                : InvokeStop(server.StopAsync);
            Task scanStopTask = InvokeStop(_scanProtection.StopAsync);
            Task startupIntegrityStopTask = InvokeStop(_startupIntegrityMonitor.StopAsync);
            _shutdownTask = CompleteShutdownAsync(
                server,
                serverStopTask,
                scanStopTask,
                startupIntegrityStopTask);
            return _shutdownTask;
        }
    }

    private async Task CompleteShutdownAsync(
        IServiceHostPipeServerLifetime? server,
        Task serverStopTask,
        Task scanStopTask,
        Task startupIntegrityStopTask)
    {
        // Keep all post-admission cleanup inside the tracked task even when both
        // component stop tasks have already completed synchronously.
        await Task.Yield();

        try
        {
            await Task.WhenAll(serverStopTask, scanStopTask, startupIntegrityStopTask).ConfigureAwait(false);
        }
        catch
        {
            ReportTaskFailure(serverStopTask, "Pipe server shutdown");
            ReportTaskFailure(scanStopTask, "Scan-protection shutdown");
            ReportTaskFailure(startupIntegrityStopTask, "Application startup-integrity monitoring shutdown");
        }

        TryDispose(server, "Pipe server");
        TryDispose(_scanProtection, "Scan-protection service");
        TryDispose(_startupIntegrityMonitor, "Application startup-integrity monitor");

        lock (_lifecycleSync)
        {
            if (ReferenceEquals(_server, server))
                _server = null;
            _runTask = null;
        }
    }

    private static Task InvokeStop(Func<Task>? stop)
    {
        if (stop == null)
            return Task.CompletedTask;

        try
        {
            return stop() ?? Task.FromException(
                new InvalidOperationException("A service shutdown component returned a null task."));
        }
        catch (Exception ex)
        {
            return Task.FromException(ex);
        }
    }

    private void TryDispose(IDisposable? disposable, string component)
    {
        if (disposable == null)
            return;

        try
        {
            disposable.Dispose();
        }
        catch (Exception ex)
        {
            ReportShutdownFailure(new InvalidOperationException($"{component} disposal failed.", ex));
        }
    }

    private void ReportTaskFailure(Task task, string component)
    {
        if (task.IsFaulted)
        {
            ReportShutdownFailure(new InvalidOperationException(
                $"{component} failed.",
                task.Exception!.Flatten()));
        }
        else if (task.IsCanceled)
        {
            ReportShutdownFailure(new TaskCanceledException($"{component} was canceled."));
        }
    }

    private void ReportShutdownFailure(Exception failure)
    {
        try
        {
            _reportShutdownFailure(failure);
        }
        catch (Exception observerFailure)
        {
            ServiceHostLog.Write($"Service shutdown failure observer failed: {observerFailure}");
        }
    }

    private void WaitForTerminalShutdown(Task shutdownTask)
    {
        while (!shutdownTask.IsCompleted)
        {
            try
            {
                _requestAdditionalTime(StopWaitHintMilliseconds);
            }
            catch (Exception ex)
            {
                ServiceHostLog.Write($"Unable to extend the service stop wait hint; shutdown will continue: {ex}");
            }

            try
            {
                shutdownTask.Wait(StopProgressInterval);
            }
            catch (AggregateException) when (shutdownTask.IsCompleted)
            {
                break;
            }
        }

        if (shutdownTask.IsFaulted)
            ReportShutdownFailure(shutdownTask.Exception!.Flatten());
        else if (shutdownTask.IsCanceled)
            ReportShutdownFailure(new TaskCanceledException("Service shutdown was canceled."));
    }

    internal static bool WaitForShutdown(
        Task shutdownTask,
        TimeSpan normalStopBudget,
        TimeSpan progressInterval,
        int waitHintMilliseconds,
        Action<int> requestAdditionalTime,
        Action<TimeSpan> reportOverBudget,
        TimeProvider timeProvider,
        long stopStartedTimestamp,
        Func<Task, TimeSpan, bool>? waitForCompletion = null)
    {
        ArgumentNullException.ThrowIfNull(shutdownTask);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(normalStopBudget, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(progressInterval, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(waitHintMilliseconds);
        ArgumentNullException.ThrowIfNull(requestAdditionalTime);
        ArgumentNullException.ThrowIfNull(reportOverBudget);
        ArgumentNullException.ThrowIfNull(timeProvider);
        waitForCompletion ??= static (task, timeout) => task.Wait(timeout);

        bool overBudget = false;
        while (!shutdownTask.IsCompleted)
        {
            TimeSpan elapsed = timeProvider.GetElapsedTime(stopStartedTimestamp);
            TimeSpan remaining = normalStopBudget - elapsed;
            if (!overBudget && remaining <= TimeSpan.Zero)
            {
                overBudget = true;
                TryReportOverBudget(reportOverBudget, elapsed);
            }

            int currentWaitHintMilliseconds = overBudget
                ? waitHintMilliseconds
                : (int)Math.Min(
                    waitHintMilliseconds,
                    Math.Max(1, Math.Ceiling(remaining.TotalMilliseconds)));
            try
            {
                requestAdditionalTime(currentWaitHintMilliseconds);
            }
            catch (Exception ex)
            {
                ServiceHostLog.Write($"Unable to extend the service stop wait hint; shutdown will continue: {ex}");
            }

            if (shutdownTask.IsCompleted)
                break;

            elapsed = timeProvider.GetElapsedTime(stopStartedTimestamp);
            remaining = normalStopBudget - elapsed;
            if (!overBudget && remaining <= TimeSpan.Zero)
            {
                overBudget = true;
                TryReportOverBudget(reportOverBudget, elapsed);
                continue;
            }

            TimeSpan waitDuration = overBudget || progressInterval < remaining
                ? progressInterval
                : remaining;
            try
            {
                if (waitForCompletion(shutdownTask, waitDuration))
                    break;
            }
            catch (AggregateException) when (shutdownTask.IsCompleted)
            {
                break;
            }

            if (!overBudget && waitDuration == remaining)
            {
                if (shutdownTask.IsCompleted)
                    break;

                overBudget = true;
                TryReportOverBudget(
                    reportOverBudget,
                    timeProvider.GetElapsedTime(stopStartedTimestamp));
            }
        }

        shutdownTask.GetAwaiter().GetResult();
        return overBudget;
    }

    private static void TryReportOverBudget(
        Action<TimeSpan> reportOverBudget,
        TimeSpan elapsed)
    {
        try
        {
            reportOverBudget(elapsed);
        }
        catch (Exception ex)
        {
            ServiceHostLog.Write($"Unable to report the service stop budget overrun: {ex}");
        }
    }

    private static void ObserveRunFailure(Task runTask)
    {
        ObserveBackgroundFailure(runTask, "Pipe server");
    }

    private static void ObserveBackgroundFailure(Task task, string component)
    {
        _ = task.ContinueWith(
            static (failedTask, state) =>
                ServiceHostLog.Write($"{state} task failed: {failedTask.Exception!.Flatten()}"),
            component,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }
}
