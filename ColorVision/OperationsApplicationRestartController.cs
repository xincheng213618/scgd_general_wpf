using ColorVision.UI.Desktop.Operations;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace ColorVision
{
    internal sealed class OperationsApplicationRestartController : IOperationsApplicationRestartController
    {
        private const string WaitForProcessArgument = "--wait-for-process";
        private const string RestartJobArgument = "--operations-restart-job";
        private static readonly TimeSpan RestartResponseDelay = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan EarlierProcessExitTimeout = TimeSpan.FromSeconds(30);

        private readonly Application _application;
        private readonly IOperationsFlowRuntimeStatusProvider _flowRuntimeStatus;
        private readonly OperationsWorkStore _workStore;
        private readonly OperationsApplicationRestartHandoff _handoff;
        private readonly Action _markReplacementActive;
        private int _restartScheduled;

        internal static string? RestartJobId { get; private set; }

        public OperationsApplicationRestartController(
            Application application,
            IOperationsFlowRuntimeStatusProvider flowRuntimeStatus,
            OperationsWorkStore workStore,
            OperationsApplicationRestartHandoff handoff,
            Action markReplacementActive)
        {
            _application = application ?? throw new ArgumentNullException(nameof(application));
            _flowRuntimeStatus = flowRuntimeStatus ?? throw new ArgumentNullException(nameof(flowRuntimeStatus));
            _workStore = workStore ?? throw new ArgumentNullException(nameof(workStore));
            _handoff = handoff ?? throw new ArgumentNullException(nameof(handoff));
            _markReplacementActive = markReplacementActive
                ?? throw new ArgumentNullException(nameof(markReplacementActive));
        }

        public OperationsApplicationRestartResult RequestRestart(string jobId)
        {
            OperationsFlowRuntimeStatus flowStatus;
            try
            {
                flowStatus = _flowRuntimeStatus.Capture();
            }
            catch
            {
                return new(false, "application_restart:flow_status_unavailable");
            }

            if (!flowStatus.Available)
                return new(false, "application_restart:flow_status_unavailable");
            if (flowStatus.IsActive)
                return new(false, "application_restart:flow_active");
            if (_application.Dispatcher.HasShutdownStarted || _application.Dispatcher.HasShutdownFinished)
                return new(false, "application_restart:application_shutting_down");

            string? executablePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(executablePath)
                || !File.Exists(executablePath)
                || !string.Equals(Path.GetFileName(executablePath), "ColorVision.exe", StringComparison.OrdinalIgnoreCase))
                return new(false, "application_restart:executable_unavailable");
            if (Interlocked.CompareExchange(ref _restartScheduled, 1, 0) != 0)
                return new(false, "application_restart:already_scheduled");

            try
            {
                _handoff.Prepare(jobId);
                _ = RestartAfterResponseAsync(jobId, executablePath);
                return new(true, "application_restart:scheduled");
            }
            catch
            {
                Interlocked.Exchange(ref _restartScheduled, 0);
                _handoff.Clear(jobId);
                return new(false, "application_restart:handoff_failed");
            }
        }

        private async Task RestartAfterResponseAsync(string jobId, string executablePath)
        {
            try
            {
                await Task.Delay(RestartResponseDelay).ConfigureAwait(false);
                await _application.Dispatcher.InvokeAsync(() =>
                {
                    OperationsFlowRuntimeStatus flowStatus = _flowRuntimeStatus.Capture();
                    if (!flowStatus.Available)
                        throw new InvalidOperationException("application_restart_flow_status_unavailable");
                    if (flowStatus.IsActive)
                        throw new InvalidOperationException("application_restart_flow_active");

                    ProcessStartInfo startInfo = new(executablePath)
                    {
                        UseShellExecute = false,
                        WorkingDirectory = Path.GetDirectoryName(executablePath) ?? AppContext.BaseDirectory,
                    };
                    startInfo.ArgumentList.Add("-r");
                    startInfo.ArgumentList.Add(WaitForProcessArgument);
                    startInfo.ArgumentList.Add(Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    startInfo.ArgumentList.Add(RestartJobArgument);
                    startInfo.ArgumentList.Add(jobId);
                    if (Process.Start(startInfo) == null)
                        throw new InvalidOperationException("application_restart_process_not_started");
                    _markReplacementActive();
                    _application.Shutdown();
                }, DispatcherPriority.Send);
            }
            catch (Exception ex)
            {
                _handoff.Clear(jobId);
                _workStore.CompleteJob(jobId, false,
                    $"application_restart:{NormalizeFailure(ex)}");
                Interlocked.Exchange(ref _restartScheduled, 0);
            }
        }

        internal static string[] WaitForEarlierProcessAndRemoveHandoffArguments(string[] args)
        {
            ArgumentNullException.ThrowIfNull(args);
            RestartJobId = null;
            List<string> applicationArguments = [];
            for (int index = 0; index < args.Length; index++)
            {
                if (string.Equals(args[index], RestartJobArgument, StringComparison.OrdinalIgnoreCase))
                {
                    if (index + 1 < args.Length && IsValidJobId(args[index + 1]))
                        RestartJobId = args[++index];
                    continue;
                }
                if (!string.Equals(args[index], WaitForProcessArgument, StringComparison.OrdinalIgnoreCase))
                {
                    applicationArguments.Add(args[index]);
                    continue;
                }

                if (index + 1 >= args.Length
                    || !int.TryParse(args[index + 1], out int processId)
                    || processId <= 0
                    || processId == Environment.ProcessId)
                    continue;

                index++;
                try
                {
                    using Process earlierProcess = Process.GetProcessById(processId);
                    if (!earlierProcess.WaitForExit((int)EarlierProcessExitTimeout.TotalMilliseconds))
                        Environment.Exit(-1);
                }
                catch (ArgumentException)
                {
                }
                catch (InvalidOperationException)
                {
                }
            }
            return applicationArguments.ToArray();
        }

        private static bool IsValidJobId(string jobId) =>
            jobId.Length == 32 && jobId.All(char.IsLetterOrDigit);

        private static string NormalizeFailure(Exception exception)
        {
            string message = exception is InvalidOperationException
                ? exception.Message
                : exception.GetType().Name;
            return message.Length <= 80
                && message.All(character => char.IsLetterOrDigit(character) || character is '_' or '-')
                    ? message
                    : "restart_failed";
        }
    }
}
