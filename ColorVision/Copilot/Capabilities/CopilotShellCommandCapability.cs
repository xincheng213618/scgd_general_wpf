using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ColorVision.Copilot.Mcp;

namespace ColorVision.Copilot
{
    internal sealed record CopilotShellProcessCommand(
        CopilotShellKind Shell,
        string ExecutablePath,
        IReadOnlyList<string> Arguments,
        string WorkingDirectory,
        TimeSpan Timeout)
    {
        public IReadOnlyDictionary<string, string?>? EnvironmentOverrides { get; init; }

        public Action<string>? StandardOutputReceived { get; init; }

        public Action<string>? StandardErrorReceived { get; init; }
    }

    internal sealed record CopilotShellProcessResult(
        int ExitCode,
        bool TimedOut,
        string StandardOutput,
        string StandardError,
        TimeSpan Duration)
    {
        public bool ProcessTreeContained { get; init; }

        public long ObservedStandardOutputCharacters { get; init; } =
            StandardOutput.Length;

        public long ObservedStandardErrorCharacters { get; init; } =
            StandardError.Length;

        public bool StandardOutputTruncated { get; init; }

        public bool StandardErrorTruncated { get; init; }
    }

    internal interface ICopilotShellProcessRunner
    {
        Task<CopilotShellProcessResult> RunAsync(CopilotShellProcessCommand command, CancellationToken cancellationToken);
    }

    internal sealed class CopilotShellCommandService
    {
        public const int MaximumCommandCharacters = 16_384;
        internal const string NonzeroExitFailureCode = "shell_nonzero_exit";
        internal const string TimedOutFailureCode = "shell_timed_out";

        private readonly ICopilotShellProcessRunner _runner;
        private readonly Func<CopilotShellKind, string?> _executablePathProvider;
        private readonly CopilotShellCommandOutputArchiveRegistry
            _outputArchiveRegistry;

        public CopilotShellCommandService()
            : this(
                new CopilotShellProcessRunner(),
                FindTrustedShellExecutable,
                CopilotShellCommandOutputArchiveRegistry.Shared)
        {
        }

        public CopilotShellCommandService(
            ICopilotShellProcessRunner runner,
            Func<CopilotShellKind, string?>? executablePathProvider = null,
            CopilotShellCommandOutputArchiveRegistry? outputArchiveRegistry =
                null)
        {
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));
            _executablePathProvider = executablePathProvider ?? FindTrustedShellExecutable;
            _outputArchiveRegistry = outputArchiveRegistry
                ?? CopilotShellCommandOutputArchiveRegistry.Shared;
        }

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput input,
            CancellationToken cancellationToken)
        {
            return ExecuteCoreAsync(request, input, progress: null, cancellationToken);
        }

        public Task<CopilotToolResult> ExecuteWithProgressAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput input,
            CopilotToolProgressContext progress,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(progress);
            return ExecuteCoreAsync(request, input, progress, cancellationToken);
        }

        private async Task<CopilotToolResult> ExecuteCoreAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput input,
            CopilotToolProgressContext? progress,
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
            CopilotShellCommandOutputArchiveSnapshot? outputArchive = null;
            CopilotShellCommandOutputCapture? outputCapture = new();
            try
            {
                var shellLabel = GetShellLabel(execution.Shell);
                progress?.Report($"正在启动 {shellLabel} 命令");
                processResult = await _runner.RunAsync(new CopilotShellProcessCommand(
                    execution.Shell,
                    Path.GetFullPath(executablePath),
                    BuildArguments(execution.Shell, execution.CommandText),
                    execution.WorkingDirectory,
                    TimeSpan.FromSeconds(execution.TimeoutSeconds))
                {
                    StandardOutputReceived = chunk =>
                    {
                        outputCapture?.AppendStandardOutput(chunk);
                        CopilotProcessExecutionSupport.ReportLatestOutput(
                            progress,
                            shellLabel,
                            chunk,
                            isError: false);
                    },
                    StandardErrorReceived = chunk =>
                    {
                        outputCapture?.AppendStandardError(chunk);
                        CopilotProcessExecutionSupport.ReportLatestOutput(
                            progress,
                            shellLabel,
                            chunk,
                            isError: true);
                    },
                }, cancellationToken);
                outputCapture.EnsureCaptured(
                    processResult.StandardOutput,
                    processResult.StandardError);
                outputCapture.Complete();
                outputArchive = _outputArchiveRegistry.Retain(
                    request.ConversationId,
                    outputCapture,
                    processResult);
                outputCapture = null;
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
            finally
            {
                outputCapture?.Dispose();
            }

            if (processResult.TimedOut)
            {
                return new CopilotToolResult
                {
                    ToolName = "RunShellCommand",
                    Success = false,
                    Summary = $"The {GetShellLabel(execution.Shell)} command exceeded its {execution.TimeoutSeconds}-second timeout.",
                    Content = BuildContent(
                        execution.Shell,
                        execution.WorkingDirectory,
                        processResult,
                        outputArchive),
                    ErrorMessage = $"The command did not finish within {execution.TimeoutSeconds} seconds; inspect the captured shell output.",
                    FailureKind = CopilotToolFailureKind.Transient,
                    FailureCode = TimedOutFailureCode,
                };
            }

            var succeeded = processResult.ExitCode == 0;
            return new CopilotToolResult
            {
                ToolName = "RunShellCommand",
                Success = succeeded,
                Summary = succeeded
                    ? $"{GetShellLabel(execution.Shell)} command completed successfully."
                    : $"{GetShellLabel(execution.Shell)} command completed with exit code {processResult.ExitCode}.",
                Content = BuildContent(
                    execution.Shell,
                    execution.WorkingDirectory,
                    processResult,
                    outputArchive),
                ErrorMessage = succeeded
                    ? string.Empty
                    : $"The command returned exit code {processResult.ExitCode}; inspect the captured shell output.",
                FailureKind = succeeded ? CopilotToolFailureKind.None : CopilotToolFailureKind.Unspecified,
                FailureCode = succeeded ? string.Empty : NonzeroExitFailureCode,
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
                $"Review the complete {shellLabel} command and resolved working directory before approving.",
                ImpactSummary: $"将在工作目录 {execution.WorkingDirectory} 中执行一条 {shellLabel} 命令；其影响取决于命令内容。ColorVision 会捕获脱敏预览，并仅在预览截断时临时保留自动删除的脱敏输出存档。",
                Reversibility: CopilotApprovalReversibility.NotReversible,
                ReversibilitySummary: "Copilot 不会自动撤销命令产生的文件、进程、网络或系统状态变化。")
            {
                ReviewDetails = BuildApprovalReviewDetails(execution),
            };
        }

        private static string BuildApprovalReviewDetails(CopilotShellExecution execution)
        {
            var commandDigest = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(execution.CommandText))).ToLowerInvariant();
            var builder = new StringBuilder();
            builder.AppendLine($"Shell: {GetShellLabel(execution.Shell)}");
            builder.Append("Working directory: ");
            CopilotApprovalReviewTextEncoder.Append(builder, execution.WorkingDirectory);
            builder.AppendLine();
            builder.AppendLine($"Timeout: {execution.TimeoutSeconds} seconds");
            builder.AppendLine($"Command characters: {execution.CommandText.Length}");
            builder.AppendLine($"Command SHA-256: {commandDigest}");
            builder.AppendLine(@"Review encoding: backslashes are doubled; line endings, tabs, Unicode format, and invisible control characters are escaped.");
            builder.AppendLine();
            builder.AppendLine("Complete command (review-escaped):");
            CopilotApprovalReviewTextEncoder.Append(builder, execution.CommandText);
            return builder.ToString();
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

        internal static IReadOnlyList<string> BuildArguments(CopilotShellKind shell, string commandText)
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

        internal static bool TryResolveExecution(
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

        internal static string? FindTrustedShellExecutable(CopilotShellKind shell)
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

        private static string BuildContent(
            CopilotShellKind shell,
            string workingDirectory,
            CopilotShellProcessResult result,
            CopilotShellCommandOutputArchiveSnapshot? outputArchive)
        {
            var builder = new StringBuilder();
            builder.AppendLine("[Shell Command]");
            builder.AppendLine($"shell: {GetShellLabel(shell)}");
            builder.AppendLine($"working_directory: {workingDirectory}");
            builder.AppendLine($"exit_code: {result.ExitCode}");
            builder.AppendLine($"outcome: {(result.TimedOut ? "timed_out" : result.ExitCode == 0 ? "completed" : "nonzero_exit")}");
            builder.AppendLine($"duration_ms: {Math.Max(0, (long)result.Duration.TotalMilliseconds)}");
            builder.AppendLine($"process_tree: {(result.ProcessTreeContained ? "windows_job_object" : "best_effort")}");
            builder.AppendLine(
                $"stdout_observed_characters: {result.ObservedStandardOutputCharacters}");
            builder.AppendLine(
                $"stderr_observed_characters: {result.ObservedStandardErrorCharacters}");
            builder.AppendLine(
                $"stdout_preview_truncated: {(result.StandardOutputTruncated ? "true" : "false")}");
            builder.AppendLine(
                $"stderr_preview_truncated: {(result.StandardErrorTruncated ? "true" : "false")}");
            if (outputArchive != null)
            {
                builder.AppendLine($"output_archive_id: {outputArchive.Id}");
                builder.AppendLine(
                    $"stdout_archive_available: {(outputArchive.StandardOutputArchiveAvailable ? "true" : "false")}");
                builder.AppendLine(
                    $"stdout_archived_characters: {outputArchive.ArchivedStandardOutputCharacters}");
                builder.AppendLine(
                    $"stdout_archive_truncated: {(outputArchive.StandardOutputArchiveTruncated ? "true" : "false")}");
                builder.AppendLine(
                    $"stderr_archive_available: {(outputArchive.StandardErrorArchiveAvailable ? "true" : "false")}");
                builder.AppendLine(
                    $"stderr_archived_characters: {outputArchive.ArchivedStandardErrorCharacters}");
                builder.AppendLine(
                    $"stderr_archive_truncated: {(outputArchive.StandardErrorArchiveTruncated ? "true" : "false")}");
            }
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

        internal readonly record struct CopilotShellExecution(
            string CommandText,
            CopilotShellKind Shell,
            string WorkingDirectory,
            int TimeoutSeconds);
    }

    internal sealed class CopilotShellProcessRunner : ICopilotShellProcessRunner
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
            long observedStandardOutputCharacters = 0;
            long observedStandardErrorCharacters = 0;
            var stdoutTask = CopilotProcessExecutionSupport.ReadBoundedAsync(
                process.StandardOutput,
                MaxStreamCharacters,
                16_384,
                "\n...<shell output truncated>...\n",
                outputReadSource.Token,
                chunk =>
                {
                    observedStandardOutputCharacters =
                        SaturatingAdd(
                            observedStandardOutputCharacters,
                            chunk.Length);
                    command.StandardOutputReceived?.Invoke(chunk);
                });
            var stderrTask = CopilotProcessExecutionSupport.ReadBoundedAsync(
                process.StandardError,
                MaxStreamCharacters,
                16_384,
                "\n...<shell output truncated>...\n",
                outputReadSource.Token,
                chunk =>
                {
                    observedStandardErrorCharacters =
                        SaturatingAdd(
                            observedStandardErrorCharacters,
                            chunk.Length);
                    command.StandardErrorReceived?.Invoke(chunk);
                });
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
                ObservedStandardOutputCharacters =
                    observedStandardOutputCharacters,
                ObservedStandardErrorCharacters =
                    observedStandardErrorCharacters,
                StandardOutputTruncated =
                    observedStandardOutputCharacters
                    > MaxStreamCharacters,
                StandardErrorTruncated =
                    observedStandardErrorCharacters
                    > MaxStreamCharacters,
            };
        }

        private static long SaturatingAdd(long value, int increment) =>
            value > long.MaxValue - increment
                ? long.MaxValue
                : value + increment;

        internal static Encoding GetStreamEncoding(CopilotShellKind shell)
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
