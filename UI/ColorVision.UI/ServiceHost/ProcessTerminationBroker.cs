using System.Diagnostics;

namespace ColorVision.UI.ServiceHost;

internal sealed class ProcessTerminationBroker
{
    private const string Command = "process-terminate";
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);
    private readonly Func<string, object?, TimeSpan, CancellationToken, Task<ServiceHostResponse>> _sendAsync;

    internal ProcessTerminationBroker(Func<string, object?, TimeSpan, CancellationToken, Task<ServiceHostResponse>> sendAsync) => _sendAsync = sendAsync;

    public async Task TerminateAsync(int processId, DateTime startTimeUtc, string executablePath, IProgress<string> progress, CancellationToken cancellationToken)
    {
        try
        {
            ServiceHostResponse response = await SendTerminationAsync().ConfigureAwait(false);
            if (!response.Success && response.Message.StartsWith("Unsupported command:", StringComparison.OrdinalIgnoreCase))
            {
                // Reuse the broker's existing self-update route when the installed service predates this command.
                cancellationToken.ThrowIfCancellationRequested();
                progress.Report("正在更新权限服务，随后结束目标进程");
                ServiceHostResponse update = await _sendAsync("self-update", new { packageDirectory = ServiceHostProtocol.PackageDirectory }, RequestTimeout, CancellationToken.None).ConfigureAwait(false);
                if (!update.Success)
                    throw new InvalidOperationException($"权限服务版本过旧，自动更新未完成：{update.Message}");
                await WaitForUpdatedServiceAsync(cancellationToken).ConfigureAwait(false);
                response = await SendTerminationAsync().ConfigureAwait(false);
            }
            if (!response.Success)
                throw new InvalidOperationException(response.Message switch
                {
                    "process_still_running" => "权限服务已发送强制结束请求，但目标进程在 5 秒后仍未退出。",
                    "process_identity_mismatch" => "权限服务发现目标进程身份与请求不一致，已停止结束操作。",
                    "process_request_expired" => "权限服务请求已过期，请重试结束。",
                    "process_target_not_allowed" => "目标程序不在权限服务允许结束的程序清单中，请更新权限服务中的程序白名单。",
                    _ => $"权限服务未能结束目标进程：{response.Message}",
                });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            throw new InvalidOperationException($"无法通过权限服务结束进程（PID {processId}）：{exception.Message}", exception);
        }

        Task<ServiceHostResponse> SendTerminationAsync()
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress.Report($"正在通过权限服务结束进程（PID {processId}）");
            var data = new { processId, startTimeUtcTicks = startTimeUtc.ToUniversalTime().Ticks, executablePath, notAfterUtcTicks = DateTime.UtcNow.Add(RequestTimeout).Ticks };
            // A sent command cannot be withdrawn. Await its reply/deadline before honoring Direct open or Cancel.
            return _sendAsync(Command, data, RequestTimeout, CancellationToken.None);
        }
    }

    private async Task WaitForUpdatedServiceAsync(CancellationToken cancellationToken)
    {
        Stopwatch elapsed = Stopwatch.StartNew();
        while (elapsed.Elapsed < TimeSpan.FromSeconds(30))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                ServiceHostResponse status = await _sendAsync("status", null, TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
                if (status.Success && status.Data?["supportsProcessTermination"]?.ToObject<bool>() == true)
                    return;
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested) { }
            await Task.Delay(500, cancellationToken).ConfigureAwait(false);
        }
        throw new TimeoutException("权限服务更新后尚未就绪，请稍后重试或在帮助菜单中检查 ColorVision 服务主机。");
    }
}
