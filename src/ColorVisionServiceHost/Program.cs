using System.ServiceProcess;

namespace ColorVisionServiceHost;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (args.Any(arg => string.Equals(arg, "--run", StringComparison.OrdinalIgnoreCase)))
        {
            return await RunConsoleAsync().ConfigureAwait(false);
        }

        if (args.Length >= 2 && string.Equals(args[0], "--send", StringComparison.OrdinalIgnoreCase))
        {
            return await SendCommandAsync(args[1]).ConfigureAwait(false);
        }

        if (!Environment.UserInteractive)
        {
            ServiceBase.Run(new ColorVisionServiceHostService());
            return 0;
        }

        Console.WriteLine("ColorVisionServiceHost demo");
        Console.WriteLine("  --run                 Run in console mode");
        Console.WriteLine("  --send ping           Send a demo command to the running service");
        Console.WriteLine();
        Console.WriteLine("Install/start/stop/uninstall is intentionally handled by ColorVision.");
        return 0;
    }

    private static async Task<int> RunConsoleAsync()
    {
        using CancellationTokenSource cts = new();
        ApplicationUpdateScanProtectionService scanProtection = ApplicationUpdateScanProtectionService.Default;
        ServiceHostPipeServer? server = null;
        ConsoleCancelEventHandler cancelHandler = (_, e) =>
        {
            e.Cancel = true;
            ObserveConsoleShutdownFailure(BeginConsoleShutdown(cts, scanProtection));
        };

        try
        {
            _ = scanProtection.Start();
            Console.CancelKeyPress += cancelHandler;
            ServiceHostLog.Write("Starting console host.");
            server = new ServiceHostPipeServer(new ServiceHostCommandHandler());
            Task runTask = server.RunAsync(cts.Token);

            Console.WriteLine("ColorVisionServiceHost is running in console mode.");
            Console.WriteLine($"Pipe: {ServiceHostConstants.PipeName}");
            Console.WriteLine("Press Ctrl+C to stop.");
            await runTask.ConfigureAwait(false);
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
            Task scanStopTask = BeginConsoleShutdown(cts, scanProtection);
            Task serverStopTask = server == null
                ? Task.CompletedTask
                : InvokeStop(server.StopAsync);
            try
            {
                await Task.WhenAll(serverStopTask, scanStopTask).ConfigureAwait(false);
            }
            finally
            {
                try
                {
                    server?.Dispose();
                }
                finally
                {
                    scanProtection.Dispose();
                }
            }
        }

        ServiceHostLog.Write("Console host stopped.");
        return 0;
    }

    internal static Task BeginConsoleShutdown(
        CancellationTokenSource pipeCancellation,
        IApplicationUpdateScanProtectionLifetime scanProtection)
    {
        ArgumentNullException.ThrowIfNull(pipeCancellation);
        ArgumentNullException.ThrowIfNull(scanProtection);
        Task pipeCancellationTask;
        try
        {
            pipeCancellation.Cancel();
            pipeCancellationTask = Task.CompletedTask;
        }
        catch (Exception ex)
        {
            pipeCancellationTask = Task.FromException(ex);
        }

        return Task.WhenAll(
            pipeCancellationTask,
            InvokeStop(scanProtection.StopAsync));
    }

    private static Task InvokeStop(Func<Task> stop)
    {
        try
        {
            return stop() ?? Task.FromException(
                new InvalidOperationException("A console shutdown component returned a null task."));
        }
        catch (Exception ex)
        {
            return Task.FromException(ex);
        }
    }

    private static void ObserveConsoleShutdownFailure(Task shutdownTask)
    {
        _ = shutdownTask.ContinueWith(
            static failedTask =>
                ServiceHostLog.Write($"Console shutdown request failed: {failedTask.Exception!.Flatten()}"),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }

    private static async Task<int> SendCommandAsync(string command)
    {
        try
        {
            ServiceHostResponse response = await ServiceHostPipeClient.SendAsync(command, TimeSpan.FromSeconds(3)).ConfigureAwait(false);
            Console.WriteLine(response.ToDisplayText());
            return response.Success ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }
}
