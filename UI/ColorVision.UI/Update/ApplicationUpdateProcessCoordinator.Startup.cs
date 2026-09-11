using System.Diagnostics;
using System.ComponentModel;
using System.IO;
using ColorVision.UI.ServiceHost;

namespace ColorVision.Update;

public static partial class ApplicationUpdateProcessCoordinator
{
    public static StartupReplacement PrepareStartupReplacement()
    {
        string executablePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Unable to resolve the current ColorVision executable path.");
        using Process currentProcess = Process.GetCurrentProcess();
        return PrepareStartupReplacement(executablePath, currentProcess.Id, currentProcess.SessionId, currentProcess.StartTime.ToUniversalTime());
    }

    internal static StartupReplacement PrepareStartupReplacement(string executablePath, int processId, int sessionId, DateTime startTimeUtc)
    {
        string normalizedPath = Path.GetFullPath(executablePath);
        return new(normalizedPath, FindEarlierApplicationProcesses(normalizedPath, processId, sessionId, startTimeUtc));
    }

    /// <summary>Owns a verified snapshot of older processes for one interactive startup attempt.</summary>
    public sealed class StartupReplacement : IDisposable
    {
        private readonly List<Process> _processes;
        private readonly string _executablePath;

        internal StartupReplacement(string executablePath, List<Process> processes)
        {
            _executablePath = executablePath;
            _processes = processes;
        }

        public IReadOnlyList<int> ProcessIds => _processes.Select(process => process.Id).ToArray();

        public async Task ForceCloseAsync(IProgress<string> progress, CancellationToken cancellationToken)
        {
            foreach (Process process in _processes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!IsRunning(process))
                    continue;
                progress.Report($"正在结束旧进程（PID {process.Id}）");
                try
                {
                    try
                    {
                        if (!PinEarlierProcess(process))
                            continue;
                        cancellationToken.ThrowIfCancellationRequested();
                        if (!process.HasExited)
                        {
                            log.Warn($"Terminating earlier ColorVision process {process.Id} for single-instance startup.");
                            process.Kill();
                        }
                    }
                    catch (Win32Exception exception) when (exception.NativeErrorCode == 5 && IsRunning(process))
                    {
                        await ColorVisionServiceHostClient.Default.TerminateProcessAsync(
                            process.Id, process.StartTime.ToUniversalTime(), _executablePath, progress, cancellationToken).ConfigureAwait(false);
                    }
                    cancellationToken.ThrowIfCancellationRequested();
                    await WaitForForcedExitAsync(process, DefaultForcedShutdownTimeout, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or ArgumentException)
                {
                    if (!IsRunning(process))
                        continue;
                    throw new InvalidOperationException($"无法结束旧进程（PID {process.Id}）：{exception.Message}", exception);
                }
            }
        }

        public void Dispose() => DisposeProcesses(_processes);
    }

    internal static async Task WaitForForcedExitAsync(Process process, TimeSpan timeout, CancellationToken cancellationToken)
    {
        // Kill only initiates termination. Confirm actual exit before continuing startup.
        using var exitCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        exitCancellation.CancelAfter(timeout);
        try
        {
            await process.WaitForExitAsync(exitCancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            if (IsRunning(process))
                throw new TimeoutException($"已发送强制结束请求，但旧进程（PID {process.Id}）在 {timeout.TotalSeconds:0.###} 秒后仍未退出。");
        }
    }
}
