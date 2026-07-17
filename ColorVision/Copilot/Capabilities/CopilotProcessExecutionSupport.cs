using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    /// <summary>
    /// Shared lifecycle support for approved Copilot child processes. All waits after
    /// cancellation are bounded so a failed process-tree kill or inherited pipe handle
    /// cannot keep the Agent task alive indefinitely.
    /// </summary>
    internal static class CopilotProcessExecutionSupport
    {
        public static TimeSpan ProcessTreeExitTimeout { get; } = TimeSpan.FromSeconds(5);

        private static TimeSpan OutputDrainTimeout { get; } = TimeSpan.FromSeconds(1);

        private static TimeSpan OutputCloseTimeout { get; } = TimeSpan.FromMilliseconds(250);

        public static async Task TerminateProcessTreeAsync(Process process, CopilotWindowsProcessJob? processJob)
        {
            ArgumentNullException.ThrowIfNull(process);
            processJob?.TryTerminate();
            TryKillProcessTree(process);

            var waits = new List<Task>(2)
            {
                TryWaitForExitAsync(process, ProcessTreeExitTimeout),
            };
            if (processJob != null)
                waits.Add(processJob.TryWaitForExitAsync(ProcessTreeExitTimeout));
            await Task.WhenAll(waits).ConfigureAwait(false);
        }

        public static async Task<(string StandardOutput, string StandardError)> DrainOutputAsync(
            Task<string> standardOutputTask,
            Task<string> standardErrorTask,
            CancellationTokenSource outputReadSource,
            StreamReader standardOutput,
            StreamReader standardError)
        {
            ArgumentNullException.ThrowIfNull(standardOutputTask);
            ArgumentNullException.ThrowIfNull(standardErrorTask);
            ArgumentNullException.ThrowIfNull(outputReadSource);
            ArgumentNullException.ThrowIfNull(standardOutput);
            ArgumentNullException.ThrowIfNull(standardError);

            var combined = Task.WhenAll(standardOutputTask, standardErrorTask);
            try
            {
                await combined.WaitAsync(OutputDrainTimeout).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                outputReadSource.Cancel();
                TryDispose(standardOutput);
                TryDispose(standardError);
                try
                {
                    await combined.WaitAsync(OutputCloseTimeout).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is TimeoutException or OperationCanceledException or IOException or ObjectDisposedException)
                {
                }
            }

            ObserveFailure(standardOutputTask);
            ObserveFailure(standardErrorTask);
            return (CompletedResultOrEmpty(standardOutputTask), CompletedResultOrEmpty(standardErrorTask));
        }

        public static async Task<string> ReadBoundedAsync(
            StreamReader reader,
            int maxCharacters,
            int headCharacters,
            string truncationMarker,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(reader);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCharacters);
            if (headCharacters < 0 || headCharacters > maxCharacters)
                throw new ArgumentOutOfRangeException(nameof(headCharacters));

            var head = new StringBuilder(headCharacters);
            var tail = new StringBuilder(maxCharacters - headCharacters);
            var buffer = new char[4_096];
            var truncated = false;
            try
            {
                while (true)
                {
                    var count = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
                    if (count == 0)
                        break;
                    var offset = 0;
                    if (head.Length < headCharacters)
                    {
                        var headCount = Math.Min(count, headCharacters - head.Length);
                        head.Append(buffer, 0, headCount);
                        offset = headCount;
                    }
                    if (offset < count)
                    {
                        tail.Append(buffer, offset, count - offset);
                        var maximumTail = maxCharacters - headCharacters;
                        if (tail.Length > maximumTail)
                        {
                            tail.Remove(0, tail.Length - maximumTail);
                            truncated = true;
                        }
                    }
                }
            }
            catch (Exception ex) when (cancellationToken.IsCancellationRequested
                && ex is OperationCanceledException or IOException or ObjectDisposedException)
            {
            }

            if (tail.Length == 0)
                return head.ToString();
            return truncated
                ? head.Append(truncationMarker).Append(tail).ToString()
                : head.Append(tail).ToString();
        }

        private static async Task TryWaitForExitAsync(Process process, TimeSpan timeout)
        {
            try
            {
                if (process.HasExited)
                    return;
                using var timeoutSource = new CancellationTokenSource(timeout);
                await process.WaitForExitAsync(timeoutSource.Token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is OperationCanceledException or InvalidOperationException or ObjectDisposedException or Win32Exception)
            {
            }
        }

        private static void TryKillProcessTree(Process process)
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException or Win32Exception or NotSupportedException)
            {
            }
        }

        private static void TryDispose(StreamReader reader)
        {
            try
            {
                reader.Dispose();
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException)
            {
            }
        }

        private static string CompletedResultOrEmpty(Task<string> task)
        {
            return task.Status == TaskStatus.RanToCompletion ? task.Result : string.Empty;
        }

        private static void ObserveFailure(Task task)
        {
            if (task.IsFaulted)
                _ = task.Exception;
            else if (!task.IsCompleted)
                _ = task.ContinueWith(completed => _ = completed.Exception, CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
    }
}
