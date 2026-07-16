using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ColorVision.Copilot.Mcp;

namespace ColorVision.Copilot
{
    public sealed record CopilotShellProcessCommand(
        CopilotShellKind Shell,
        string ExecutablePath,
        IReadOnlyList<string> Arguments,
        string WorkingDirectory,
        TimeSpan Timeout)
    {
        public IReadOnlyDictionary<string, string?>? EnvironmentOverrides { get; init; }
    }

    public sealed record CopilotShellProcessResult(
        int ExitCode,
        bool TimedOut,
        string StandardOutput,
        string StandardError,
        TimeSpan Duration)
    {
        public bool ProcessTreeContained { get; init; }
    }

    public interface ICopilotShellProcessRunner
    {
        Task<CopilotShellProcessResult> RunAsync(CopilotShellProcessCommand command, CancellationToken cancellationToken);
    }

    public sealed class CopilotShellCommandService
    {
        public const int MaximumCommandCharacters = 16_384;
        private readonly ICopilotShellProcessRunner _runner;
        private readonly Func<CopilotShellKind, string?> _executablePathProvider;

        public CopilotShellCommandService()
            : this(new CopilotShellProcessRunner(), FindTrustedShellExecutable)
        {
        }

        public CopilotShellCommandService(
            ICopilotShellProcessRunner runner,
            Func<CopilotShellKind, string?>? executablePathProvider = null)
        {
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));
            _executablePathProvider = executablePathProvider ?? FindTrustedShellExecutable;
        }

        public async Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput input,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            input ??= CopilotAgentToolInput.Empty;
            if (!TryResolveExecution(request, input, out var execution, out var validationFailure))
                return validationFailure!;

            var executablePath = _executablePathProvider(execution.Shell);
            if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
            {
                return Failure(CopilotToolFailureKind.NotFound,
                    $"{GetShellLabel(execution.Shell)} could not be located.",
                    "The selected Windows shell executable is not installed in a trusted system location.");
            }

            CopilotShellProcessResult processResult;
            try
            {
                processResult = await _runner.RunAsync(new CopilotShellProcessCommand(
                    execution.Shell,
                    Path.GetFullPath(executablePath),
                    BuildArguments(execution.Shell, execution.CommandText),
                    execution.WorkingDirectory,
                    TimeSpan.FromSeconds(execution.TimeoutSeconds)), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Win32Exception or InvalidOperationException)
            {
                return Failure(CopilotToolFailureKind.Internal,
                    "The shell process could not be started.",
                    CopilotMcpAuditLogger.RedactText(ex.Message));
            }

            if (processResult.TimedOut)
            {
                return Failure(CopilotToolFailureKind.Transient,
                    $"The {GetShellLabel(execution.Shell)} command exceeded its {execution.TimeoutSeconds}-second timeout.",
                    BuildContent(execution.Shell, execution.WorkingDirectory, processResult));
            }

            return new CopilotToolResult
            {
                ToolName = "RunShellCommand",
                Success = true,
                Summary = processResult.ExitCode == 0
                    ? $"{GetShellLabel(execution.Shell)} command completed successfully."
                    : $"{GetShellLabel(execution.Shell)} command completed with exit code {processResult.ExitCode}.",
                Content = BuildContent(execution.Shell, execution.WorkingDirectory, processResult),
            };
        }

        internal static CopilotToolApprovalPresentation CreateApprovalPresentation(
            CopilotAgentRequest request,
            CopilotAgentToolInput input)
        {
            ArgumentNullException.ThrowIfNull(request);
            input ??= CopilotAgentToolInput.Empty;
            if (!TryResolveExecution(request, input, out var execution, out var validationFailure))
            {
                return new CopilotToolApprovalPresentation(
                    "Shell command cannot be approved",
                    validationFailure?.ErrorMessage ?? "The shell execution context could not be resolved.");
            }

            var shellLabel = GetShellLabel(execution.Shell);
            return new CopilotToolApprovalPresentation(
                $"Run {shellLabel} command",
                $"Shell: {shellLabel}\nWorking directory: {execution.WorkingDirectory}\nTimeout: {execution.TimeoutSeconds} seconds\nCommand:\n{execution.CommandText}");
        }

        internal static CopilotShellKind ResolveShell(CopilotShellKind requested, CopilotShellKind preferred)
        {
            if (requested != CopilotShellKind.Auto)
                return requested;
            return preferred == CopilotShellKind.CommandPrompt
                ? CopilotShellKind.CommandPrompt
                : CopilotShellKind.PowerShell;
        }

        internal static bool TryParseShell(string? value, out CopilotShellKind shell)
        {
            var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
            shell = normalized switch
            {
                "auto" => CopilotShellKind.Auto,
                "powershell" or "pwsh" => CopilotShellKind.PowerShell,
                "cmd" or "commandprompt" or "command-prompt" => CopilotShellKind.CommandPrompt,
                _ => (CopilotShellKind)(-1),
            };
            return Enum.IsDefined(shell);
        }

        internal static string GetShellLabel(CopilotShellKind shell) => shell == CopilotShellKind.CommandPrompt ? "CMD" : "PowerShell";

        internal static string ResolveDefaultWorkingDirectory(CopilotAgentRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            var candidate = request.WritableLocalRootPaths
                .Concat(request.SearchRootPaths)
                .FirstOrDefault(Directory.Exists)
                ?? AppContext.BaseDirectory;
            try
            {
                return Path.GetFullPath(candidate);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return AppContext.BaseDirectory;
            }
        }

        private static IReadOnlyList<string> BuildArguments(CopilotShellKind shell, string commandText)
        {
            return shell == CopilotShellKind.CommandPrompt
                ? ["/d", "/s", "/c", commandText]
                : ["-NoLogo", "-NoProfile", "-NonInteractive", "-Command",
                    "[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false); $OutputEncoding = [Console]::OutputEncoding; " + commandText];
        }

        private static bool TryResolveWorkingDirectory(
            CopilotAgentRequest request,
            CopilotAgentToolInput input,
            out string workingDirectory,
            out string error)
        {
            error = string.Empty;
            if (TryGetString(input, "workingDirectory", out var requestedDirectory)
                && !string.IsNullOrWhiteSpace(requestedDirectory))
            {
                try
                {
                    var normalizedDirectory = requestedDirectory.Trim();
                    workingDirectory = Path.IsPathFullyQualified(normalizedDirectory)
                        ? Path.GetFullPath(normalizedDirectory)
                        : Path.GetFullPath(normalizedDirectory, ResolveDefaultWorkingDirectory(request));
                }
                catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
                {
                    workingDirectory = string.Empty;
                    error = "Invalid working directory: " + ex.Message;
                    return false;
                }
                if (!Directory.Exists(workingDirectory))
                {
                    error = "The working directory does not exist: " + workingDirectory;
                    return false;
                }
                return true;
            }

            workingDirectory = ResolveDefaultWorkingDirectory(request);
            return true;
        }

        private static bool TryResolveExecution(
            CopilotAgentRequest request,
            CopilotAgentToolInput input,
            out CopilotShellExecution execution,
            out CopilotToolResult? failure)
        {
            execution = default;
            failure = null;
            if (!TryGetString(input, "command", out var commandText) || string.IsNullOrWhiteSpace(commandText))
            {
                failure = Failure(CopilotToolFailureKind.Validation, "The shell command is missing.", "command is required.");
                return false;
            }

            commandText = commandText.Trim();
            if (commandText.Length > MaximumCommandCharacters || commandText.Contains('\0'))
            {
                failure = Failure(CopilotToolFailureKind.Validation,
                    "The shell command is not valid.",
                    $"command must contain 1 through {MaximumCommandCharacters} characters and no NUL characters.");
                return false;
            }
            if (!TryGetOptionalString(input, "shell", "auto", out var requestedShell)
                || !TryParseShell(requestedShell, out var shell))
            {
                failure = Failure(CopilotToolFailureKind.Validation,
                    "The requested shell is not supported.",
                    "shell must be auto, powershell, or cmd.");
                return false;
            }
            shell = ResolveShell(shell, request.PreferredShell);
            if (!TryGetOptionalInt(input, "timeoutSeconds", 60, out var timeoutSeconds)
                || timeoutSeconds is < 5 or > 600)
            {
                failure = Failure(CopilotToolFailureKind.Validation,
                    "The shell command timeout is outside the allowed range.",
                    "timeoutSeconds must be an integer from 5 through 600.");
                return false;
            }
            if (!TryResolveWorkingDirectory(request, input, out var workingDirectory, out var workingDirectoryError))
            {
                failure = Failure(CopilotToolFailureKind.Validation,
                    "The shell working directory is not available.",
                    workingDirectoryError);
                return false;
            }

            execution = new CopilotShellExecution(commandText, shell, workingDirectory, timeoutSeconds);
            return true;
        }

        private static string? FindTrustedShellExecutable(CopilotShellKind shell)
        {
            var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var candidates = shell == CopilotShellKind.CommandPrompt
                ? new[]
                {
                    string.IsNullOrWhiteSpace(windows) ? string.Empty : Path.Combine(windows, "System32", "cmd.exe"),
                }
                : new[]
                {
                    string.IsNullOrWhiteSpace(programFiles) ? string.Empty : Path.Combine(programFiles, "PowerShell", "7", "pwsh.exe"),
                    string.IsNullOrWhiteSpace(windows) ? string.Empty : Path.Combine(windows, "System32", "WindowsPowerShell", "v1.0", "powershell.exe"),
                };
            return candidates.Where(path => !string.IsNullOrWhiteSpace(path)).Select(SafeFullPath).FirstOrDefault(File.Exists);
        }

        private static string BuildContent(CopilotShellKind shell, string workingDirectory, CopilotShellProcessResult result)
        {
            var builder = new StringBuilder();
            builder.AppendLine("[Shell Command]");
            builder.AppendLine($"shell: {GetShellLabel(shell)}");
            builder.AppendLine($"working_directory: {workingDirectory}");
            builder.AppendLine($"exit_code: {result.ExitCode}");
            builder.AppendLine($"outcome: {(result.TimedOut ? "timed_out" : result.ExitCode == 0 ? "completed" : "nonzero_exit")}");
            builder.AppendLine($"duration_ms: {Math.Max(0, (long)result.Duration.TotalMilliseconds)}");
            builder.AppendLine($"process_tree: {(result.ProcessTreeContained ? "windows_job_object" : "best_effort")}");
            builder.AppendLine("stdout:");
            builder.AppendLine(string.IsNullOrWhiteSpace(result.StandardOutput) ? "<empty>" : CopilotMcpAuditLogger.RedactText(result.StandardOutput).TrimEnd());
            builder.AppendLine("stderr:");
            builder.AppendLine(string.IsNullOrWhiteSpace(result.StandardError) ? "<empty>" : CopilotMcpAuditLogger.RedactText(result.StandardError).TrimEnd());
            return builder.ToString().TrimEnd();
        }

        private static bool TryGetString(CopilotAgentToolInput input, string name, out string value)
        {
            value = string.Empty;
            if (!input.Arguments.TryGetValue(name, out var raw) || raw == null)
                return false;
            if (raw is string text)
            {
                value = text;
                return true;
            }
            if (raw is JsonElement element && element.ValueKind == JsonValueKind.String)
            {
                value = element.GetString() ?? string.Empty;
                return true;
            }
            return false;
        }

        private static bool TryGetOptionalString(CopilotAgentToolInput input, string name, string defaultValue, out string value)
        {
            if (!input.Arguments.ContainsKey(name))
            {
                value = defaultValue;
                return true;
            }
            return TryGetString(input, name, out value);
        }

        private static bool TryGetOptionalInt(CopilotAgentToolInput input, string name, int defaultValue, out int value)
        {
            if (!input.Arguments.TryGetValue(name, out var raw) || raw == null)
            {
                value = defaultValue;
                return true;
            }
            if (raw is int intValue)
            {
                value = intValue;
                return true;
            }
            if (raw is long longValue && longValue is >= int.MinValue and <= int.MaxValue)
            {
                value = (int)longValue;
                return true;
            }
            if (raw is JsonElement element && element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out value))
                return true;
            value = 0;
            return false;
        }

        private static string SafeFullPath(string path)
        {
            try
            {
                return Path.GetFullPath(path);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static CopilotToolResult Failure(CopilotToolFailureKind kind, string summary, string error)
        {
            return new CopilotToolResult
            {
                ToolName = "RunShellCommand",
                Success = false,
                FailureKind = kind,
                Summary = summary,
                ErrorMessage = error,
            };
        }

        private readonly record struct CopilotShellExecution(
            string CommandText,
            CopilotShellKind Shell,
            string WorkingDirectory,
            int TimeoutSeconds);
    }

    public sealed class CopilotShellProcessRunner : ICopilotShellProcessRunner
    {
        private const int MaxStreamCharacters = 65_536;
        public async Task<CopilotShellProcessResult> RunAsync(CopilotShellProcessCommand command, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);
            var streamEncoding = GetStreamEncoding(command.Shell);
            var startInfo = new ProcessStartInfo
            {
                FileName = command.ExecutablePath,
                WorkingDirectory = command.WorkingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = streamEncoding,
                StandardErrorEncoding = streamEncoding,
            };
            foreach (var argument in command.Arguments)
                startInfo.ArgumentList.Add(argument);
            startInfo.Environment["NO_COLOR"] = "1";
            if (command.EnvironmentOverrides != null)
            {
                foreach (var pair in command.EnvironmentOverrides)
                {
                    if (string.IsNullOrWhiteSpace(pair.Key))
                        continue;
                    if (pair.Value == null)
                        startInfo.Environment.Remove(pair.Key);
                    else
                        startInfo.Environment[pair.Key] = pair.Value;
                }
            }

            using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            var stopwatch = Stopwatch.StartNew();
            if (!process.Start())
                throw new InvalidOperationException("The shell process did not start.");
            using var processJob = CopilotWindowsProcessJob.TryAssign(process);
            process.StandardInput.Close();

            using var outputReadSource = new CancellationTokenSource();
            var stdoutTask = CopilotProcessExecutionSupport.ReadBoundedAsync(
                process.StandardOutput, MaxStreamCharacters, 16_384, "\n...<shell output truncated>...\n", outputReadSource.Token);
            var stderrTask = CopilotProcessExecutionSupport.ReadBoundedAsync(
                process.StandardError, MaxStreamCharacters, 16_384, "\n...<shell output truncated>...\n", outputReadSource.Token);
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(command.Timeout);
            var timedOut = false;
            var cancelledByCaller = false;
            try
            {
                await process.WaitForExitAsync(timeoutSource.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                timedOut = true;
            }
            catch (OperationCanceledException)
            {
                cancelledByCaller = true;
            }

            // A successful root-shell exit must not leave approved background descendants alive.
            // Terminating the job before draining output also closes inherited pipe handles.
            await CopilotProcessExecutionSupport.TerminateProcessTreeAsync(process, processJob);
            var (standardOutput, standardError) = await CopilotProcessExecutionSupport.DrainOutputAsync(
                stdoutTask, stderrTask, outputReadSource, process.StandardOutput, process.StandardError);
            stopwatch.Stop();
            if (cancelledByCaller)
                throw new OperationCanceledException(cancellationToken);
            return new CopilotShellProcessResult(
                timedOut ? -1 : process.ExitCode,
                timedOut,
                standardOutput,
                standardError,
                stopwatch.Elapsed)
            {
                ProcessTreeContained = processJob != null,
            };
        }

        private static Encoding GetStreamEncoding(CopilotShellKind shell)
        {
            if (shell != CopilotShellKind.CommandPrompt)
                return Encoding.UTF8;

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            try
            {
                return Encoding.GetEncoding((int)GetOEMCP());
            }
            catch (ArgumentException)
            {
                return Encoding.Default;
            }
        }

        [DllImport("kernel32.dll")]
        private static extern uint GetOEMCP();

    }
}
