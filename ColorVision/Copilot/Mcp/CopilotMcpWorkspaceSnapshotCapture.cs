using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace ColorVision.Copilot.Mcp
{
    internal static class CopilotMcpWorkspaceSnapshotCapture
    {
        internal static TimeSpan DefaultTimeout { get; } = TimeSpan.FromSeconds(2);

        public static CopilotMcpWorkspaceSnapshot Capture(
            Dispatcher? dispatcher,
            Func<CopilotMcpWorkspaceSnapshot> capture,
            TimeSpan? timeout = null)
        {
            ArgumentNullException.ThrowIfNull(capture);
            if (dispatcher == null || dispatcher.CheckAccess())
                return capture();
            if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
                return new CopilotMcpWorkspaceSnapshot();

            try
            {
                return dispatcher.Invoke(
                    capture,
                    DispatcherPriority.Background,
                    CancellationToken.None,
                    timeout ?? DefaultTimeout);
            }
            catch (TimeoutException)
            {
                return new CopilotMcpWorkspaceSnapshot();
            }
            catch (TaskCanceledException) when (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
            {
                return new CopilotMcpWorkspaceSnapshot();
            }
            catch (InvalidOperationException) when (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
            {
                return new CopilotMcpWorkspaceSnapshot();
            }
        }
    }
}
