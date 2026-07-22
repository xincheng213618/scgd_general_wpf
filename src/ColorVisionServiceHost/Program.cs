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
        ApplicationUpdateScanProtectionService.Default.Start();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        ServiceHostLog.Write("Starting console host.");
        ServiceHostPipeServer server = new(new ServiceHostCommandHandler());
        Task runTask = server.RunAsync(cts.Token);

        Console.WriteLine("ColorVisionServiceHost is running in console mode.");
        Console.WriteLine($"Pipe: {ServiceHostConstants.PipeName}");
        Console.WriteLine("Press Ctrl+C to stop.");

        try
        {
            await runTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }

        ServiceHostLog.Write("Console host stopped.");
        ApplicationUpdateScanProtectionService.Default.Dispose();
        return 0;
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
