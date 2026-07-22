using System.ServiceProcess;

namespace ColorVisionServiceHost;

internal sealed class ColorVisionServiceHostService : ServiceBase
{
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _runTask;

    public ColorVisionServiceHostService()
    {
        ServiceName = ServiceHostConstants.ServiceName;
        CanStop = true;
        CanPauseAndContinue = false;
        AutoLog = true;
    }

    protected override void OnStart(string[] args)
    {
        ServiceHostLog.Write("Service starting.");
        ApplicationUpdateScanProtectionService.Default.Start();
        _cancellationTokenSource = new CancellationTokenSource();
        ServiceHostPipeServer server = new(new ServiceHostCommandHandler());
        _runTask = Task.Run(() => server.RunAsync(_cancellationTokenSource.Token));
        ServiceHostLog.Write("Service started.");
    }

    protected override void OnStop()
    {
        ServiceHostLog.Write("Service stopping.");
        ApplicationUpdateScanProtectionService.Default.Dispose();
        _cancellationTokenSource?.Cancel();
        try
        {
            _runTask?.Wait(TimeSpan.FromSeconds(10));
        }
        catch (AggregateException ex) when (ex.InnerExceptions.All(inner => inner is OperationCanceledException))
        {
        }
        finally
        {
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            _runTask = null;
        }

        ServiceHostLog.Write("Service stopped.");
    }
}
