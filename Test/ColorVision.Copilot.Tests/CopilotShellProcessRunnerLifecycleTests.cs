using ColorVision.Copilot;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotShellProcessRunnerLifecycleTests
{
    [Fact]
    public async Task MissingWindowsJobStopsShellAndFailsClosed()
    {
        var workspace = CreateTemporaryDirectory();
        var executionMarker = Path.Combine(workspace, "must-not-run.txt");
        ProcessIdentity? processIdentity = null;
        try
        {
            var executable = CopilotShellCommandService.FindTrustedShellExecutable(
                CopilotShellKind.PowerShell);
            Assert.False(string.IsNullOrWhiteSpace(executable));
            var runner = new CopilotShellProcessRunner(process =>
            {
                processIdentity = ProcessIdentity.Capture(process);
                return null;
            });

            var exception = await Assert.ThrowsAsync<CopilotProcessTreeContainmentException>(() =>
                runner.RunAsync(
                    new CopilotShellProcessCommand(
                        CopilotShellKind.PowerShell,
                        executable!,
                        CopilotShellCommandService.BuildArguments(
                            CopilotShellKind.PowerShell,
                            $"[System.IO.File]::WriteAllText({QuotePowerShellLiteral(executionMarker)}, 'ran'); Start-Sleep -Seconds 30"),
                        workspace,
                        TimeSpan.FromSeconds(35)),
                    CancellationToken.None));

            Assert.Contains("Windows Job Object", exception.Message, StringComparison.Ordinal);
            Assert.NotNull(processIdentity);
            Assert.True(
                await WaitForProcessExitAsync(processIdentity.Value, TimeSpan.FromSeconds(2)),
                $"Uncontained shell process {processIdentity.Value.Id} survived failed Job Object assignment.");
            Assert.False(
                File.Exists(executionMarker),
                "The shell command ran before failed Job Object assignment was rejected.");
        }
        finally
        {
            if (processIdentity.HasValue)
                await KillLeakedProcessAsync(processIdentity.Value);
            await DeleteTemporaryDirectoryAsync(workspace);
        }
    }

    [Fact]
    public async Task ShellCommandStartsOnlyAfterWindowsJobAssignment()
    {
        var workspace = CreateTemporaryDirectory();
        var executionMarker = Path.Combine(workspace, "assigned-before-run.txt");
        using var gate = new BlockingJobAssigner(succeed: true);
        try
        {
            var executable = Environment.GetEnvironmentVariable("ComSpec")
                ?? Path.Combine(Environment.SystemDirectory, "cmd.exe");
            var commandText = "echo executed>assigned-before-run.txt"
                + " & echo stdout-token"
                + " & echo stderr-token 1>&2"
                + " & exit /b 23";
            var runner = new CopilotShellProcessRunner(gate.Assign);
            var runTask = Task.Run(() => runner.RunAsync(
                new CopilotShellProcessCommand(
                    CopilotShellKind.CommandPrompt,
                    executable,
                    CopilotShellCommandService.BuildArguments(
                        CopilotShellKind.CommandPrompt,
                        commandText),
                    workspace,
                    TimeSpan.FromSeconds(10)),
                CancellationToken.None));

            _ = await gate.Entered.WaitAsync(TimeSpan.FromSeconds(5));
            var executedBeforeAssignment = await WaitForFileAsync(
                executionMarker,
                TimeSpan.FromSeconds(1));
            gate.Release();
            var result = await runTask.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.False(
                executedBeforeAssignment,
                "The shell command ran while its primary thread should still have been suspended.");
            Assert.True(File.Exists(executionMarker));
            Assert.Equal(23, result.ExitCode);
            Assert.False(result.TimedOut);
            Assert.True(result.ProcessTreeContained);
            Assert.Contains("stdout-token", result.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("stderr-token", result.StandardError, StringComparison.Ordinal);
        }
        finally
        {
            gate.Release();
            await DeleteTemporaryDirectoryAsync(workspace);
        }
    }

    [Fact]
    public async Task CancellationDuringWindowsJobAssignmentNeverResumesShell()
    {
        var workspace = CreateTemporaryDirectory();
        var executionMarker = Path.Combine(workspace, "cancelled-before-run.txt");
        using var gate = new BlockingJobAssigner(succeed: true);
        using var cancellationSource = new CancellationTokenSource();
        ProcessIdentity? processIdentity = null;
        try
        {
            var executable = Environment.GetEnvironmentVariable("ComSpec")
                ?? Path.Combine(Environment.SystemDirectory, "cmd.exe");
            var runner = new CopilotShellProcessRunner(gate.Assign);
            var runTask = Task.Run(() => runner.RunAsync(
                new CopilotShellProcessCommand(
                    CopilotShellKind.CommandPrompt,
                    executable,
                    CopilotShellCommandService.BuildArguments(
                        CopilotShellKind.CommandPrompt,
                        "echo executed>cancelled-before-run.txt"),
                    workspace,
                    TimeSpan.FromSeconds(10)),
                cancellationSource.Token));

            processIdentity = await gate.Entered.WaitAsync(TimeSpan.FromSeconds(5));
            cancellationSource.Cancel();
            gate.Release();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runTask);

            Assert.False(
                File.Exists(executionMarker),
                "The cancelled shell command resumed after Job Object assignment.");
            Assert.True(
                await WaitForProcessExitAsync(processIdentity.Value, TimeSpan.FromSeconds(2)),
                $"Cancelled suspended shell process {processIdentity.Value.Id} survived cleanup.");
        }
        finally
        {
            gate.Release();
            if (processIdentity.HasValue)
                await KillLeakedProcessAsync(processIdentity.Value);
            await DeleteTemporaryDirectoryAsync(workspace);
        }
    }

    [Fact]
    public async Task RedirectedStreamsDrainWhenStderrFillsBeforeStdout()
    {
        var workspace = CreateTemporaryDirectory();
        try
        {
            var executable = CopilotShellCommandService.FindTrustedShellExecutable(
                CopilotShellKind.PowerShell);
            Assert.False(string.IsNullOrWhiteSpace(executable));
            var result = await new CopilotShellProcessRunner().RunAsync(
                new CopilotShellProcessCommand(
                    CopilotShellKind.PowerShell,
                    executable!,
                    CopilotShellCommandService.BuildArguments(
                        CopilotShellKind.PowerShell,
                        "[Console]::Error.Write(('e' * 131072)); [Console]::Out.Write('stdout-after-stderr'); exit 0"),
                    workspace,
                    TimeSpan.FromSeconds(10)),
                CancellationToken.None);

            Assert.False(result.TimedOut);
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("stdout-after-stderr", result.StandardOutput, StringComparison.Ordinal);
            Assert.True(result.ObservedStandardErrorCharacters >= 131_072);
            Assert.True(result.StandardErrorTruncated);
        }
        finally
        {
            await DeleteTemporaryDirectoryAsync(workspace);
        }
    }

    [Fact]
    public async Task CompletedShellProcessTerminatesDetachedDescendant()
    {
        var workspace = CreateTemporaryDirectory();
        var markerPath = Path.Combine(workspace, "must-not-survive.txt");
        try
        {
            var result = await RunDetachedMarkerCommandAsync(
                workspace,
                markerPath,
                keepRootAlive: false,
                timeout: TimeSpan.FromSeconds(5));

            Assert.False(result.TimedOut);
            Assert.Equal(0, result.ExitCode);
            Assert.True(result.ProcessTreeContained);
            Assert.True(File.Exists(GetLaunchMarkerPath(markerPath)));
            await Task.Delay(TimeSpan.FromSeconds(2));
            Assert.False(
                File.Exists(markerPath),
                "A detached descendant survived an ordinary approved shell command.");
        }
        finally
        {
            await DeleteTemporaryDirectoryAsync(workspace);
        }
    }

    [Fact]
    public async Task TimedOutShellProcessTerminatesDetachedDescendant()
    {
        var workspace = CreateTemporaryDirectory();
        var markerPath = Path.Combine(workspace, "must-not-exist.txt");
        var releaseMarkerPath = markerPath + ".release";
        try
        {
            var result = await RunDetachedMarkerCommandAsync(
                workspace,
                markerPath,
                keepRootAlive: true,
                timeout: TimeSpan.FromSeconds(5),
                releaseMarkerPath: releaseMarkerPath);

            Assert.True(result.TimedOut);
            Assert.True(result.ProcessTreeContained);
            Assert.True(File.Exists(GetLaunchMarkerPath(markerPath)));
            File.WriteAllText(releaseMarkerPath, "release");
            Assert.False(
                await WaitForFileAsync(markerPath, TimeSpan.FromSeconds(2)),
                "A detached descendant survived after the shell process timed out.");
        }
        finally
        {
            await DeleteTemporaryDirectoryAsync(workspace);
        }
    }

    [Fact]
    public async Task CancelledShellProcessTerminatesDetachedDescendant()
    {
        var workspace = CreateTemporaryDirectory();
        var markerPath = Path.Combine(workspace, "cancelled-must-not-exist.txt");
        using var cancellationSource = new CancellationTokenSource();
        var launchObserved = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                RunDetachedMarkerCommandAsync(
                    workspace,
                    markerPath,
                    keepRootAlive: true,
                    timeout: TimeSpan.FromSeconds(5),
                    standardOutputReceived: chunk =>
                    {
                        if (!chunk.Contains("child-launched", StringComparison.Ordinal))
                            return;
                        launchObserved.TrySetResult();
                        cancellationSource.Cancel();
                    },
                    cancellationToken: cancellationSource.Token));

            await launchObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(File.Exists(GetLaunchMarkerPath(markerPath)));
            await Task.Delay(TimeSpan.FromSeconds(2));
            Assert.False(
                File.Exists(markerPath),
                "A detached descendant survived after the shell process was cancelled.");
        }
        finally
        {
            await DeleteTemporaryDirectoryAsync(workspace);
        }
    }

    private static Task<CopilotShellProcessResult> RunDetachedMarkerCommandAsync(
        string workspace,
        string markerPath,
        bool keepRootAlive,
        TimeSpan timeout,
        Action<string>? standardOutputReceived = null,
        string? releaseMarkerPath = null,
        CancellationToken cancellationToken = default)
    {
        var executable = CopilotShellCommandService.FindTrustedShellExecutable(
            CopilotShellKind.PowerShell);
        Assert.False(string.IsNullOrWhiteSpace(executable));

        var childScript = string.IsNullOrWhiteSpace(releaseMarkerPath)
            ? "Start-Sleep -Milliseconds 1000; "
                + $"[System.IO.File]::WriteAllText({QuotePowerShellLiteral(markerPath)}, 'done')"
            : $"while (-not [System.IO.File]::Exists({QuotePowerShellLiteral(releaseMarkerPath)})) "
                + "{ Start-Sleep -Milliseconds 25 }; "
                + $"[System.IO.File]::WriteAllText({QuotePowerShellLiteral(markerPath)}, 'done')";
        var encodedChildScript = Convert.ToBase64String(Encoding.Unicode.GetBytes(childScript));
        var command = "Start-Process -WindowStyle Hidden "
            + $"-FilePath {QuotePowerShellLiteral(executable!)} "
            + "-ArgumentList '-NoLogo','-NoProfile','-NonInteractive','-EncodedCommand',"
            + QuotePowerShellLiteral(encodedChildScript)
            + "; [System.IO.File]::WriteAllText("
            + QuotePowerShellLiteral(GetLaunchMarkerPath(markerPath))
            + ", 'launched'); Write-Output 'child-launched'";
        if (keepRootAlive)
            command += "; Start-Sleep -Seconds 30";

        return new CopilotShellProcessRunner().RunAsync(
            new CopilotShellProcessCommand(
                CopilotShellKind.PowerShell,
                executable!,
                CopilotShellCommandService.BuildArguments(CopilotShellKind.PowerShell, command),
                workspace,
                timeout)
            {
                StandardOutputReceived = standardOutputReceived,
            },
            cancellationToken);
    }

    private static async Task<bool> WaitForFileAsync(string path, TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            if (File.Exists(path))
                return true;
            await Task.Delay(25);
        }
        return File.Exists(path);
    }

    private static async Task<bool> WaitForProcessExitAsync(
        ProcessIdentity identity,
        TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            try
            {
                using var process = Process.GetProcessById(identity.Id);
                if (process.HasExited || !identity.Matches(process))
                    return true;
            }
            catch (ArgumentException)
            {
                return true;
            }

            await Task.Delay(25);
        }
        return false;
    }

    private static async Task KillLeakedProcessAsync(ProcessIdentity identity)
    {
        try
        {
            using var process = Process.GetProcessById(identity.Id);
            if (process.HasExited || !identity.Matches(process))
                return;
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException
            or System.ComponentModel.Win32Exception or TimeoutException)
        {
        }
    }

    private readonly record struct ProcessIdentity(int Id, DateTime StartTimeUtc)
    {
        public static ProcessIdentity Capture(Process process) =>
            new(process.Id, process.StartTime.ToUniversalTime());

        public bool Matches(Process process) =>
            process.Id == Id && process.StartTime.ToUniversalTime() == StartTimeUtc;
    }

    private sealed class BlockingJobAssigner(bool succeed) : IDisposable
    {
        private readonly TaskCompletionSource<ProcessIdentity> _entered =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly ManualResetEventSlim _release = new();

        public Task<ProcessIdentity> Entered => _entered.Task;

        public CopilotWindowsProcessJob? Assign(Process process)
        {
            _entered.TrySetResult(ProcessIdentity.Capture(process));
            if (!_release.Wait(TimeSpan.FromSeconds(10)))
                throw new TimeoutException("The test did not release Job Object assignment.");
            return succeed ? CopilotWindowsProcessJob.TryAssign(process) : null;
        }

        public void Release() => _release.Set();

        public void Dispose() => _release.Dispose();
    }

    private static string QuotePowerShellLiteral(string value) =>
        "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";

    private static string GetLaunchMarkerPath(string markerPath) => markerPath + ".launched";

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"copilot-shell-lifecycle-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static async Task DeleteTemporaryDirectoryAsync(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var expectedParent = Path.GetFullPath(Path.GetTempPath())
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!string.Equals(
                Path.GetDirectoryName(fullPath)?.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar),
                expectedParent,
                StringComparison.OrdinalIgnoreCase)
            || !Path.GetFileName(fullPath).StartsWith(
                "copilot-shell-lifecycle-",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Refusing to delete an unexpected lifecycle test directory.");
        }

        var stopwatch = Stopwatch.StartNew();
        while (true)
        {
            try
            {
                Directory.Delete(fullPath, recursive: true);
                return;
            }
            catch (IOException) when (stopwatch.Elapsed < TimeSpan.FromSeconds(5))
            {
                await Task.Delay(25);
            }
            catch (UnauthorizedAccessException) when (stopwatch.Elapsed < TimeSpan.FromSeconds(5))
            {
                await Task.Delay(25);
            }
        }
    }
}
