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
    internal sealed record CopilotWorkspaceValidationCommand(
        string ExecutablePath,
        IReadOnlyList<string> Arguments,
        string WorkingDirectory,
        TimeSpan Timeout)
    {
        public IReadOnlyDictionary<string, string>? EnvironmentVariables { get; init; }

        public Action<string>? StandardOutputReceived { get; init; }

        public Action<string>? StandardErrorReceived { get; init; }
    }

    internal sealed record CopilotWorkspaceValidationProcessResult(
        int? ExitCode,
        bool TimedOut,
        string StandardOutput,
        string StandardError,
        TimeSpan Duration);

    internal interface ICopilotWorkspaceValidationRunner
    {
        Task<CopilotWorkspaceValidationProcessResult> RunAsync(
            CopilotWorkspaceValidationCommand command,
            CancellationToken cancellationToken);
    }

    internal sealed class CopilotWorkspaceValidationService
    {
        internal const string ValidationFailedFailureCode = "workspace_validation_failed";
        internal const string ValidationTimedOutFailureCode = "workspace_validation_timed_out";

        private static readonly HashSet<string> AllowedTargetExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".sln", ".slnx", ".csproj", ".fsproj", ".vbproj",
        };
        private readonly ICopilotWorkspaceValidationRunner _runner;
        private readonly Func<string?> _dotnetPathProvider;

        public CopilotWorkspaceValidationService()
            : this(new CopilotWorkspaceValidationProcessRunner(), FindTrustedDotnetHost)
        {
        }

        public CopilotWorkspaceValidationService(
            ICopilotWorkspaceValidationRunner runner,
            Func<string?>? dotnetPathProvider = null)
        {
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));
            _dotnetPathProvider = dotnetPathProvider ?? FindTrustedDotnetHost;
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
            if (!TryGetString(input, "task", out var task)
                || string.IsNullOrWhiteSpace(input.Path))
            {
                return Failure(CopilotToolFailureKind.Validation,
                    "Workspace validation arguments are incomplete.",
                    "task and path are required.");
            }
            task = task.Trim().ToLowerInvariant();
            if (task is not "build" and not "test")
            {
                return Failure(CopilotToolFailureKind.Validation,
                    "The workspace validation task is not allowed.",
                    "task must be exactly 'build' or 'test'.");
            }
            if (!TryGetOptionalString(input, "configuration", "Debug", out var configuration)
                || configuration is not "Debug" and not "Release")
            {
                return Failure(CopilotToolFailureKind.Validation,
                    "The workspace validation configuration is not allowed.",
                    "configuration must be exactly 'Debug' or 'Release'.");
            }
            if (!TryGetOptionalString(input, "platform", string.Empty, out var requestedPlatform)
                || !TryNormalizePlatform(requestedPlatform, out var platform))
            {
                return Failure(CopilotToolFailureKind.Validation,
                    "The workspace validation platform is not allowed.",
                    "platform must be omitted or exactly one of 'x64', 'x86', 'AnyCPU', or 'ARM64'.");
            }
            if (!TryGetOptionalInt(input, "timeoutSeconds", 300, out var timeoutSeconds)
                || timeoutSeconds is < 10 or > 600)
            {
                return Failure(CopilotToolFailureKind.Validation,
                    "The workspace validation timeout is outside the allowed range.",
                    "timeoutSeconds must be an integer from 10 through 600.");
            }
            if (!TryResolveTarget(request, input.Path, out var targetPath, out var workspaceRoot, out var targetError))
            {
                return Failure(CopilotToolFailureKind.Authorization,
                    "The validation target is outside the current workspace boundary.",
                    targetError);
            }

            var dotnetPath = _dotnetPathProvider();
            if (string.IsNullOrWhiteSpace(dotnetPath) || !File.Exists(dotnetPath))
            {
                return Failure(CopilotToolFailureKind.NotFound,
                    "A trusted dotnet host could not be located.",
                    "Install the .NET SDK under the standard Program Files dotnet directory.");
            }

            var arguments = new List<string>
            {
                task,
                targetPath,
                "--configuration", configuration,
                "--no-restore",
                "--nologo",
                "--verbosity:minimal",
            };
            if (platform.Length > 0)
                arguments.Add($"-p:Platform={platform}");
            CopilotWorkspaceValidationProcessResult processResult;
            try
            {
                var processLabel = $"dotnet {task}";
                progress?.Report($"正在运行 {processLabel}");
                processResult = await _runner.RunAsync(new CopilotWorkspaceValidationCommand(
                    Path.GetFullPath(dotnetPath),
                    arguments,
                    workspaceRoot,
                    TimeSpan.FromSeconds(timeoutSeconds))
                {
                    EnvironmentVariables = request.CodexShellEnvironmentPolicy
                        .CreateEnvironmentVariables(request.ConversationId),
                    StandardOutputReceived = chunk => CopilotProcessExecutionSupport.ReportLatestOutput(
                        progress, processLabel, chunk, isError: false),
                    StandardErrorReceived = chunk => CopilotProcessExecutionSupport.ReportLatestOutput(
                        progress, processLabel, chunk, isError: true),
                }, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Win32Exception or InvalidOperationException)
            {
                return Failure(CopilotToolFailureKind.Internal,
                    "The workspace validation process could not be started.",
                    CopilotMcpAuditLogger.RedactText(ex.Message));
            }

            if (processResult.TimedOut)
            {
                return new CopilotToolResult
                {
                    ToolName = "RunWorkspaceValidation",
                    Success = false,
                    Summary = $"Workspace {task} exceeded its {timeoutSeconds}-second timeout.",
                    Content = BuildContent(task, targetPath, configuration, platform, processResult),
                    ErrorMessage = $"dotnet {task} did not finish within {timeoutSeconds} seconds; inspect the captured validation output.",
                    FailureKind = CopilotToolFailureKind.Transient,
                    FailureCode = ValidationTimedOutFailureCode,
                    ProcessOperation = task,
                    ProcessExitCode = processResult.ExitCode,
                    ProcessTimedOut = true,
                };
            }

            if (!processResult.ExitCode.HasValue)
            {
                return Failure(
                    CopilotToolFailureKind.Internal,
                    $"Workspace {task} ended without an exit code.",
                    $"The managed dotnet {task} process completed, but its exit code was unavailable.");
            }

            var passed = processResult.ExitCode == 0;
            return new CopilotToolResult
            {
                ToolName = "RunWorkspaceValidation",
                Success = passed,
                Summary = passed
                    ? $"Workspace {task} completed successfully."
                    : $"Workspace {task} completed with exit code {processResult.ExitCode}.",
                Content = BuildContent(task, targetPath, configuration, platform, processResult),
                ErrorMessage = passed
                    ? string.Empty
                    : $"dotnet {task} returned exit code {processResult.ExitCode}; inspect the captured validation output.",
                FailureKind = passed ? CopilotToolFailureKind.None : CopilotToolFailureKind.Validation,
                FailureCode = passed ? string.Empty : ValidationFailedFailureCode,
                ProcessOperation = task,
                ProcessExitCode = processResult.ExitCode,
            };
        }

        private static bool TryResolveTarget(
            CopilotAgentRequest request,
            string requestedPath,
            out string targetPath,
            out string workspaceRoot,
            out string error)
        {
            targetPath = string.Empty;
            workspaceRoot = string.Empty;
            error = string.Empty;
            var writableRoots = CopilotWorkspaceSearchSupport.NormalizeSearchRoots(request.WritableLocalRootPaths);
            if (!CopilotWorkspaceSearchSupport.TryResolveExistingFileWithinRoots(
                requestedPath, writableRoots, out targetPath, out var resolutionError))
            {
                error = "The validation target could not be resolved: " + resolutionError;
                return false;
            }
            if (!AllowedTargetExtensions.Contains(Path.GetExtension(targetPath)))
            {
                error = "The validation target must be a .sln, .slnx, .csproj, .fsproj, or .vbproj file.";
                return false;
            }

            try
            {
                var resolvedTarget = targetPath;
                workspaceRoot = writableRoots.FirstOrDefault(root => IsWithinRoot(resolvedTarget, root)) ?? string.Empty;
                if (workspaceRoot.Length == 0)
                {
                    error = "Validation targets must be inside the current writable workspace root.";
                    return false;
                }
                if (ContainsReparsePoint(workspaceRoot, targetPath))
                {
                    error = "Validation through a file-system reparse point is not allowed.";
                    return false;
                }
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                error = "The validation target could not be checked safely: " + ex.Message;
                return false;
            }
        }

        private static bool IsWithinRoot(string path, string root)
        {
            var relative = Path.GetRelativePath(root, path);
            return !Path.IsPathRooted(relative)
                && !string.Equals(relative, "..", StringComparison.Ordinal)
                && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
        }

        private static bool ContainsReparsePoint(string root, string target)
        {
            var current = root;
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                return true;
            foreach (var segment in Path.GetRelativePath(root, target)
                .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries))
            {
                current = Path.Combine(current, segment);
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    return true;
            }
            return false;
        }

        private static string? FindTrustedDotnetHost()
        {
            var candidates = new List<string>();
            try
            {
                var runtimeDirectory = RuntimeEnvironment.GetRuntimeDirectory();
                var dotnetRoot = Directory.GetParent(runtimeDirectory)?.Parent?.Parent?.FullName;
                if (!string.IsNullOrWhiteSpace(dotnetRoot))
                    candidates.Add(Path.Combine(dotnetRoot, "dotnet.exe"));
            }
            catch
            {
            }
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (!string.IsNullOrWhiteSpace(programFiles))
                candidates.Add(Path.Combine(programFiles, "dotnet", "dotnet.exe"));
            return candidates.Select(SafeFullPath).FirstOrDefault(File.Exists);
        }

        private static string BuildContent(
            string task,
            string targetPath,
            string configuration,
            string platform,
            CopilotWorkspaceValidationProcessResult result)
        {
            var builder = new StringBuilder();
            builder.AppendLine("[Workspace Validation]");
            builder.AppendLine($"task: {task}");
            builder.AppendLine($"target: {targetPath}");
            builder.AppendLine($"configuration: {configuration}");
            builder.AppendLine($"platform: {(platform.Length == 0 ? "project_default" : platform)}");
            builder.AppendLine($"exit_code: {result.ExitCode?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "unknown"}");
            builder.AppendLine($"outcome: {(result.TimedOut ? "timed_out" : result.ExitCode == 0 ? "passed" : "failed")}");
            builder.AppendLine($"duration_ms: {Math.Max(0, (long)result.Duration.TotalMilliseconds)}");
            builder.AppendLine("stdout:");
            builder.AppendLine(string.IsNullOrWhiteSpace(result.StandardOutput)
                ? "<empty>"
                : CopilotMcpAuditLogger.RedactText(result.StandardOutput).TrimEnd());
            builder.AppendLine("stderr:");
            builder.AppendLine(string.IsNullOrWhiteSpace(result.StandardError)
                ? "<empty>"
                : CopilotMcpAuditLogger.RedactText(result.StandardError).TrimEnd());
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

        private static bool TryNormalizePlatform(string value, out string platform)
        {
            platform = value switch
            {
                "" => string.Empty,
                "x64" => "x64",
                "x86" => "x86",
                "AnyCPU" => "AnyCPU",
                "ARM64" => "ARM64",
                _ => string.Empty,
            };
            return value.Length == 0 || platform.Length > 0;
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
                ToolName = "RunWorkspaceValidation",
                Success = false,
                FailureKind = kind,
                Summary = summary,
                ErrorMessage = error,
            };
        }
    }

    internal sealed class CopilotWorkspaceValidationProcessRunner : ICopilotWorkspaceValidationRunner
    {
        private const int MaxStreamCharacters = 32_768;
        private readonly Func<Process, CopilotWindowsProcessJob?> _tryAssignProcessJob;

        public CopilotWorkspaceValidationProcessRunner()
            : this(CopilotWindowsProcessJob.TryAssign)
        {
        }

        internal CopilotWorkspaceValidationProcessRunner(
            Func<Process, CopilotWindowsProcessJob?> tryAssignProcessJob)
        {
            _tryAssignProcessJob = tryAssignProcessJob
                ?? throw new ArgumentNullException(nameof(tryAssignProcessJob));
        }

        public async Task<CopilotWorkspaceValidationProcessResult> RunAsync(
            CopilotWorkspaceValidationCommand command,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);
            var stopwatch = Stopwatch.StartNew();
            using var launchedProcess = await CopilotSuspendedProcessLauncher.LaunchAsync(
                    command.ExecutablePath,
                    command.Arguments,
                    command.WorkingDirectory,
                    CreateEnvironmentVariables(command),
                    Encoding.UTF8,
                    _tryAssignProcessJob,
                    cancellationToken)
                .ConfigureAwait(false);
            var process = launchedProcess.Process;
            var processJob = launchedProcess.ProcessJob;

            using var outputReadSource = new CancellationTokenSource();
            var stdoutTask = CopilotProcessExecutionSupport.ReadBoundedAsync(
                launchedProcess.StandardOutput,
                MaxStreamCharacters,
                8_192,
                "\n...<validation output truncated>...\n",
                outputReadSource.Token,
                command.StandardOutputReceived);
            var stderrTask = CopilotProcessExecutionSupport.ReadBoundedAsync(
                launchedProcess.StandardError,
                MaxStreamCharacters,
                8_192,
                "\n...<validation output truncated>...\n",
                outputReadSource.Token,
                command.StandardErrorReceived);
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

            // dotnet may launch compiler/test-host descendants. Keep those processes inside
            // the same bounded lifecycle as the approved validation command.
            await CopilotProcessExecutionSupport.TerminateProcessTreeAsync(process, processJob);
            var (standardOutput, standardError) = await CopilotProcessExecutionSupport.DrainOutputAsync(
                stdoutTask,
                stderrTask,
                outputReadSource,
                launchedProcess.StandardOutput,
                launchedProcess.StandardError);
            stopwatch.Stop();
            if (cancelledByCaller)
                throw new OperationCanceledException(cancellationToken);
            return new CopilotWorkspaceValidationProcessResult(
                CopilotProcessExecutionSupport.TryGetExitCode(process),
                timedOut,
                standardOutput,
                standardError,
                stopwatch.Elapsed);
        }

        private static Dictionary<string, string> CreateEnvironmentVariables(
            CopilotWorkspaceValidationCommand command)
        {
            var environment = command.EnvironmentVariables == null
                ? Environment.GetEnvironmentVariables()
                    .Cast<System.Collections.DictionaryEntry>()
                    .Where(entry => entry.Key is string && entry.Value is string)
                    .ToDictionary(
                        entry => (string)entry.Key,
                        entry => (string)entry.Value!,
                        StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(
                    command.EnvironmentVariables,
                    StringComparer.OrdinalIgnoreCase);
            environment["DOTNET_NOLOGO"] = "1";
            environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
            foreach (var name in environment.Keys
                .Where(CopilotCodexShellEnvironmentPolicy.IsNonInheritableEnvironmentVariable)
                .ToArray())
            {
                environment.Remove(name);
            }
            return environment;
        }

    }
}
