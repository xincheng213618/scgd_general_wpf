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

namespace ColorVision.Copilot
{
    public sealed record CopilotWorkspaceValidationCommand(
        string ExecutablePath,
        IReadOnlyList<string> Arguments,
        string WorkingDirectory,
        TimeSpan Timeout);

    public sealed record CopilotWorkspaceValidationProcessResult(
        int ExitCode,
        bool TimedOut,
        string StandardOutput,
        string StandardError,
        TimeSpan Duration);

    public interface ICopilotWorkspaceValidationRunner
    {
        Task<CopilotWorkspaceValidationProcessResult> RunAsync(
            CopilotWorkspaceValidationCommand command,
            CancellationToken cancellationToken);
    }

    public sealed class CopilotWorkspaceValidationService
    {
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

        public async Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput input,
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

            var arguments = new[]
            {
                task,
                targetPath,
                "--configuration", configuration,
                "--no-restore",
                "--nologo",
                "--verbosity:minimal",
            };
            CopilotWorkspaceValidationProcessResult processResult;
            try
            {
                processResult = await _runner.RunAsync(new CopilotWorkspaceValidationCommand(
                    Path.GetFullPath(dotnetPath),
                    arguments,
                    workspaceRoot,
                    TimeSpan.FromSeconds(timeoutSeconds)), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Win32Exception or InvalidOperationException)
            {
                return Failure(CopilotToolFailureKind.Internal,
                    "The workspace validation process could not be started.",
                    ex.Message);
            }

            if (processResult.TimedOut)
            {
                return Failure(CopilotToolFailureKind.Transient,
                    $"Workspace {task} exceeded its {timeoutSeconds}-second timeout.",
                    BuildContent(task, targetPath, configuration, processResult));
            }

            var passed = processResult.ExitCode == 0;
            return new CopilotToolResult
            {
                ToolName = "RunWorkspaceValidation",
                Success = true,
                Summary = passed
                    ? $"Workspace {task} completed successfully."
                    : $"Workspace {task} completed with exit code {processResult.ExitCode}.",
                Content = BuildContent(task, targetPath, configuration, processResult),
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
            try
            {
                targetPath = Path.GetFullPath(requestedPath);
            }
            catch (Exception ex)
            {
                error = "Invalid validation target path: " + ex.Message;
                return false;
            }
            if (!File.Exists(targetPath))
            {
                error = "The validation target does not exist: " + targetPath;
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
                workspaceRoot = CopilotWorkspaceSearchSupport.NormalizeSearchRoots(request.WritableLocalRootPaths)
                    .FirstOrDefault(root => IsWithinRoot(resolvedTarget, root)) ?? string.Empty;
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
            CopilotWorkspaceValidationProcessResult result)
        {
            var builder = new StringBuilder();
            builder.AppendLine("[Workspace Validation]");
            builder.AppendLine($"task: {task}");
            builder.AppendLine($"target: {targetPath}");
            builder.AppendLine($"configuration: {configuration}");
            builder.AppendLine($"exit_code: {result.ExitCode}");
            builder.AppendLine($"outcome: {(result.TimedOut ? "timed_out" : result.ExitCode == 0 ? "passed" : "failed")}");
            builder.AppendLine($"duration_ms: {Math.Max(0, (long)result.Duration.TotalMilliseconds)}");
            builder.AppendLine("stdout:");
            builder.AppendLine(string.IsNullOrWhiteSpace(result.StandardOutput) ? "<empty>" : result.StandardOutput.TrimEnd());
            builder.AppendLine("stderr:");
            builder.AppendLine(string.IsNullOrWhiteSpace(result.StandardError) ? "<empty>" : result.StandardError.TrimEnd());
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
                ToolName = "RunWorkspaceValidation",
                Success = false,
                FailureKind = kind,
                Summary = summary,
                ErrorMessage = error,
            };
        }
    }

    public sealed class CopilotWorkspaceValidationProcessRunner : ICopilotWorkspaceValidationRunner
    {
        private const int MaxStreamCharacters = 32_768;

        public async Task<CopilotWorkspaceValidationProcessResult> RunAsync(
            CopilotWorkspaceValidationCommand command,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);
            var startInfo = new ProcessStartInfo
            {
                FileName = command.ExecutablePath,
                WorkingDirectory = command.WorkingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            foreach (var argument in command.Arguments)
                startInfo.ArgumentList.Add(argument);
            startInfo.Environment["DOTNET_NOLOGO"] = "1";
            startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";

            using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            var stopwatch = Stopwatch.StartNew();
            if (!process.Start())
                throw new InvalidOperationException("The dotnet validation process did not start.");
            using var processJob = CopilotWindowsProcessJob.TryAssign(process);

            using var outputReadSource = new CancellationTokenSource();
            var stdoutTask = CopilotProcessExecutionSupport.ReadBoundedAsync(
                process.StandardOutput, MaxStreamCharacters, 8_192, "\n...<validation output truncated>...\n", outputReadSource.Token);
            var stderrTask = CopilotProcessExecutionSupport.ReadBoundedAsync(
                process.StandardError, MaxStreamCharacters, 8_192, "\n...<validation output truncated>...\n", outputReadSource.Token);
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
                stdoutTask, stderrTask, outputReadSource, process.StandardOutput, process.StandardError);
            stopwatch.Stop();
            if (cancelledByCaller)
                throw new OperationCanceledException(cancellationToken);
            return new CopilotWorkspaceValidationProcessResult(
                timedOut ? -1 : process.ExitCode,
                timedOut,
                standardOutput,
                standardError,
                stopwatch.Elapsed);
        }

    }
}
