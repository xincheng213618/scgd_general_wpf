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
        TimeSpan Timeout);

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
            if (!TryGetString(input, "command", out var commandText) || string.IsNullOrWhiteSpace(commandText))
                return Failure(CopilotToolFailureKind.Validation, "The shell command is missing.", "command is required.");

            commandText = commandText.Trim();
            if (commandText.Length > MaximumCommandCharacters || commandText.Contains('\0'))
            {
                return Failure(CopilotToolFailureKind.Validation,
                    "The shell command is not valid.",
                    $"command must contain 1 through {MaximumCommandCharacters} characters and no NUL characters.");
            }
            if (!TryGetOptionalString(input, "shell", "auto", out var requestedShell)
                || !TryParseShell(requestedShell, out var shell))
            {
                return Failure(CopilotToolFailureKind.Validation,
                    "The requested shell is not supported.",
                    "shell must be auto, powershell, or cmd.");
            }
            shell = ResolveShell(shell, request.PreferredShell);
            if (!TryGetOptionalInt(input, "timeoutSeconds", 60, out var timeoutSeconds)
                || timeoutSeconds is < 5 or > 600)
            {
                return Failure(CopilotToolFailureKind.Validation,
                    "The shell command timeout is outside the allowed range.",
                    "timeoutSeconds must be an integer from 5 through 600.");
            }
            if (!TryResolveWorkingDirectory(request, input, out var workingDirectory, out var workingDirectoryError))
            {
                return Failure(CopilotToolFailureKind.Validation,
                    "The shell working directory is not available.",
                    workingDirectoryError);
            }

            var executablePath = _executablePathProvider(shell);
            if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
            {
                return Failure(CopilotToolFailureKind.NotFound,
                    $"{GetShellLabel(shell)} could not be located.",
                    "The selected Windows shell executable is not installed in a trusted system location.");
            }

            CopilotShellProcessResult processResult;
            try
            {
                processResult = await _runner.RunAsync(new CopilotShellProcessCommand(
                    shell,
                    Path.GetFullPath(executablePath),
                    BuildArguments(shell, commandText),
                    workingDirectory,
                    TimeSpan.FromSeconds(timeoutSeconds)), cancellationToken);
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
                    $"The {GetShellLabel(shell)} command exceeded its {timeoutSeconds}-second timeout.",
                    BuildContent(shell, workingDirectory, processResult));
            }

            return new CopilotToolResult
            {
                ToolName = "RunShellCommand",
                Success = true,
                Summary = processResult.ExitCode == 0
                    ? $"{GetShellLabel(shell)} command completed successfully."
                    : $"{GetShellLabel(shell)} command completed with exit code {processResult.ExitCode}.",
                Content = BuildContent(shell, workingDirectory, processResult),
            };
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
                    workingDirectory = Path.GetFullPath(requestedDirectory.Trim());
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

            using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            var stopwatch = Stopwatch.StartNew();
            if (!process.Start())
                throw new InvalidOperationException("The shell process did not start.");
            using var processJob = CopilotWindowsProcessJob.TryAssign(process);
            process.StandardInput.Close();

            var stdoutTask = ReadBoundedAsync(process.StandardOutput, MaxStreamCharacters);
            var stderrTask = ReadBoundedAsync(process.StandardError, MaxStreamCharacters);
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(command.Timeout);
            var timedOut = false;
            try
            {
                await process.WaitForExitAsync(timeoutSource.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                timedOut = true;
                processJob?.TryTerminate();
                TryKillProcessTree(process);
                await process.WaitForExitAsync(CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
                processJob?.TryTerminate();
                TryKillProcessTree(process);
                await process.WaitForExitAsync(CancellationToken.None);
                throw;
            }

            // A successful root-shell exit must not leave approved background descendants alive.
            // Terminating the job before draining output also closes inherited pipe handles.
            processJob?.TryTerminate();

            var standardOutput = await stdoutTask;
            var standardError = await stderrTask;
            stopwatch.Stop();
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

        private static async Task<string> ReadBoundedAsync(StreamReader reader, int maxCharacters)
        {
            const int headCharacters = 16_384;
            var head = new StringBuilder(headCharacters);
            var tail = new StringBuilder(maxCharacters - headCharacters);
            var buffer = new char[4_096];
            while (true)
            {
                var count = await reader.ReadAsync(buffer);
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
                        tail.Remove(0, tail.Length - maximumTail);
                }
            }
            if (tail.Length == 0)
                return head.ToString();
            return head.Append("\n...<shell output truncated>...\n").Append(tail).ToString();
        }

        private static void TryKillProcessTree(Process process)
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
            {
            }
        }
    }
}
