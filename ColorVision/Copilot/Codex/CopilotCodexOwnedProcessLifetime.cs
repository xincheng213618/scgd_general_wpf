using Microsoft.Win32.SafeHandles;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    // Only the Process created for this connection belongs here, never a discovered Codex process.
    internal sealed class CopilotCodexOwnedProcessLifetime
    {
        internal static readonly TimeSpan GracefulExitTimeout = TimeSpan.FromMilliseconds(500);
        internal static readonly TimeSpan ForcedExitTimeout = TimeSpan.FromSeconds(2);
        private readonly Process _process;
        private readonly SafeFileHandle _standardInputHandle;
        private readonly object _gate = new();
        private Task<bool>? _stopTask;

        // Capture immediately after Start, before any stdin writes: obtaining SafeFileHandle can
        // flush FileStream's buffer, so discovering it during shutdown would recreate the hang.
        internal CopilotCodexOwnedProcessLifetime(Process process, StreamWriter standardInput)
        {
            ArgumentNullException.ThrowIfNull(process);
            ArgumentNullException.ThrowIfNull(standardInput);
            _process = process;
            _standardInputHandle = standardInput.BaseStream is FileStream stream
                ? stream.SafeFileHandle
                : throw new ArgumentException("The owned process must use redirected standard input.", nameof(standardInput));
        }

        // False means exit could not be confirmed (for example, access to the process was lost).
        // Both waits are bounded even when termination fails; no background Close worker is created.
        internal Task<bool> StopAsync()
        {
            lock (_gate)
                return _stopTask ??= StopCoreAsync();
        }

        internal bool Stop() => StopAsync().GetAwaiter().GetResult();

        private async Task<bool> StopCoreAsync()
        {
            try
            {
                // StreamWriter.Close flushes synchronously and can wait forever on a full pipe.
                // Completed requests are already AutoFlush'ed; shutdown must not send buffered ones.
                // Closing the cached handle sends EOF without flushing or blocking on an active write.
                _standardInputHandle.Dispose();
            }
            catch (Exception exception) when (IsExpectedProcessFailure(exception))
            {
            }

            if (await WaitForExitAsync(GracefulExitTimeout).ConfigureAwait(false))
                return true;

            try
            {
                // Also closes inherited pipe readers in descendants, releasing an in-flight write.
                _process.Kill(entireProcessTree: true);
            }
            catch (Exception exception) when (IsExpectedProcessFailure(exception))
            {
            }

            return await WaitForExitAsync(ForcedExitTimeout).ConfigureAwait(false);
        }

        private async Task<bool> WaitForExitAsync(TimeSpan timeout)
        {
            using var cancellation = new CancellationTokenSource(timeout);
            try
            {
                await _process.WaitForExitAsync(cancellation.Token).ConfigureAwait(false);
                return true;
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                return false;
            }
            catch (Exception exception) when (IsExpectedProcessFailure(exception))
            {
                return false;
            }
        }

        internal static bool IsExpectedProcessFailure(Exception exception)
        {
            if (exception is AggregateException aggregate)
            {
                var failures = aggregate.Flatten().InnerExceptions;
                return failures.Count > 0 && failures.All(IsExpectedProcessFailure);
            }
            return exception is InvalidOperationException or IOException or Win32Exception;
        }
    }
}
