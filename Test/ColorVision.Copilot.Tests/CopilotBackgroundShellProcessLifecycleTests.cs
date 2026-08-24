using ColorVision.Copilot;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotBackgroundShellProcessLifecycleTests
{
    [Fact]
    public async Task LauncherRejectsProcessWhenWindowsJobIsUnavailable()
    {
        var workspace = CreateTemporaryDirectory();
        var executionMarker = Path.Combine(workspace, "must-not-run.txt");
        ProcessIdentity? processIdentity = null;
        var launcher = new CopilotBackgroundShellProcessLauncher(process =>
        {
            processIdentity = ProcessIdentity.Capture(process);
            return null;
        });
        var executable = Environment.GetEnvironmentVariable("ComSpec")
            ?? Path.Combine(Environment.SystemDirectory, "cmd.exe");
        try
        {
            var exception = await Assert.ThrowsAsync<CopilotProcessTreeContainmentException>(() =>
                launcher.StartAsync(
                    new CopilotShellProcessCommand(
                        CopilotShellKind.CommandPrompt,
                        executable,
                        CopilotShellCommandService.BuildArguments(
                            CopilotShellKind.CommandPrompt,
                            "echo executed>must-not-run.txt & ping -n 120 127.0.0.1 > NUL"),
                        workspace,
                        TimeSpan.FromMinutes(1)),
                    CancellationToken.None));

            Assert.Contains("Windows Job Object", exception.Message, StringComparison.Ordinal);
            Assert.NotNull(processIdentity);
            Assert.True(
                await WaitForProcessExitAsync(processIdentity.Value, TimeSpan.FromSeconds(2)),
                $"Uncontained background shell process {processIdentity.Value.Id} survived failed Job Object assignment.");
            Assert.False(
                File.Exists(executionMarker),
                "The background command ran before failed Job Object assignment was rejected.");
        }
        finally
        {
            if (processIdentity.HasValue)
                await KillLeakedProcessAsync(processIdentity.Value);
            await DeleteTemporaryDirectoryAsync(workspace);
        }
    }

    [Fact]
    public async Task BackgroundCommandStartsOnlyAfterWindowsJobAssignment()
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
            var launcher = new CopilotBackgroundShellProcessLauncher(gate.Assign);
            var startTask = Task.Run(() => launcher.StartAsync(
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
            using var process = await startTask.WaitAsync(TimeSpan.FromSeconds(10));
            var completion = await process.Completion.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.False(
                executedBeforeAssignment,
                "The background command ran while its primary thread should still have been suspended.");
            Assert.True(File.Exists(executionMarker));
            Assert.True(process.ProcessTreeContained);
            Assert.Equal(CopilotBackgroundShellCommandState.Failed, completion.State);
            Assert.Equal(23, completion.ExitCode);
            Assert.Contains("stdout-token", completion.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("stderr-token", completion.StandardError, StringComparison.Ordinal);
        }
        finally
        {
            gate.Release();
            await DeleteTemporaryDirectoryAsync(workspace);
        }
    }

    [Fact]
    public async Task CompletedCommandCancelsUnusedLifetimeTimer()
    {
        var workspace = CreateTemporaryDirectory();
        CancellationToken lifetimeToken = default;
        try
        {
            var executable = Environment.GetEnvironmentVariable("ComSpec")
                ?? Path.Combine(Environment.SystemDirectory, "cmd.exe");
            var launcher = new CopilotBackgroundShellProcessLauncher(
                CopilotWindowsProcessJob.TryAssign,
                static process => process.WaitForExitAsync(),
                (delay, cancellationToken) =>
                {
                    lifetimeToken = cancellationToken;
                    return Task.Delay(delay, cancellationToken);
                });
            using var process = await launcher.StartAsync(
                new CopilotShellProcessCommand(
                    CopilotShellKind.CommandPrompt,
                    executable,
                    CopilotShellCommandService.BuildArguments(
                        CopilotShellKind.CommandPrompt,
                        "echo completed"),
                    workspace,
                    TimeSpan.FromHours(12)),
                CancellationToken.None);

            var result = await process.Completion.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Equal(CopilotBackgroundShellCommandState.Completed, result.State);
            Assert.True(lifetimeToken.CanBeCanceled);
            Assert.True(
                lifetimeToken.IsCancellationRequested,
                "A completed background command retained its unused maximum-lifetime timer.");
        }
        finally
        {
            await DeleteTemporaryDirectoryAsync(workspace);
        }
    }

    [Fact]
    public async Task MonitorWaitFailureTerminatesProcessBeforePublishingCompletion()
    {
        var workspace = CreateTemporaryDirectory();
        ProcessIdentity? processIdentity = null;
        try
        {
            var executable = Environment.GetEnvironmentVariable("ComSpec")
                ?? Path.Combine(Environment.SystemDirectory, "cmd.exe");
            var launcher = new CopilotBackgroundShellProcessLauncher(
                process =>
                {
                    processIdentity = ProcessIdentity.Capture(process);
                    return CopilotWindowsProcessJob.TryAssign(process);
                },
                static _ => Task.FromException(
                    new InvalidOperationException("synthetic monitor wait failure")),
                static (delay, cancellationToken) => Task.Delay(delay, cancellationToken));
            using var process = await launcher.StartAsync(
                new CopilotShellProcessCommand(
                    CopilotShellKind.CommandPrompt,
                    executable,
                    CopilotShellCommandService.BuildArguments(
                        CopilotShellKind.CommandPrompt,
                        "ping -n 120 127.0.0.1 > NUL"),
                    workspace,
                    TimeSpan.FromMinutes(1)),
                CancellationToken.None);

            var completion = await process.Completion.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Equal(CopilotBackgroundShellCommandState.Failed, completion.State);
            Assert.Contains(
                "synthetic monitor wait failure",
                completion.StandardError,
                StringComparison.Ordinal);
            Assert.NotNull(processIdentity);
            Assert.True(
                await WaitForProcessExitAsync(processIdentity.Value, TimeSpan.FromSeconds(2)),
                $"Background shell process {processIdentity.Value.Id} was still alive when failed completion was published.");
        }
        finally
        {
            if (processIdentity.HasValue)
                await KillLeakedProcessAsync(processIdentity.Value);
            await DeleteTemporaryDirectoryAsync(workspace);
        }
    }

    [Fact]
    public async Task RootExitTerminatesDescendantHoldingRedirectedPipes()
    {
        var workspace = CreateTemporaryDirectory();
        var readyPath = Path.Combine(workspace, "child-ready.txt");
        var leakedMarkerPath = Path.Combine(workspace, "child-survived.txt");
        ProcessIdentity? childIdentity = null;
        try
        {
            var executable = CopilotShellCommandService.FindTrustedShellExecutable(
                CopilotShellKind.PowerShell);
            Assert.False(string.IsNullOrWhiteSpace(executable));
            var childScript =
                "Write-Output 'child-stdout'; [Console]::Error.WriteLine('child-stderr'); "
                + "$identity = \"$PID|$([System.Diagnostics.Process]::GetCurrentProcess().StartTime.ToUniversalTime().Ticks)\"; "
                + $"[System.IO.File]::WriteAllText({QuotePowerShellLiteral(readyPath)}, $identity); "
                + "Start-Sleep -Milliseconds 1200; "
                + $"[System.IO.File]::WriteAllText({QuotePowerShellLiteral(leakedMarkerPath)}, 'survived')";
            var encodedChildScript = Convert.ToBase64String(
                Encoding.Unicode.GetBytes(childScript));
            var command = "Start-Process -NoNewWindow "
                + $"-FilePath {QuotePowerShellLiteral(executable!)} "
                + "-ArgumentList '-NoLogo','-NoProfile','-NonInteractive','-EncodedCommand',"
                + QuotePowerShellLiteral(encodedChildScript)
                + $"; $deadline = [DateTime]::UtcNow.AddSeconds(5); while (-not [System.IO.File]::Exists({QuotePowerShellLiteral(readyPath)}) -and [DateTime]::UtcNow -lt $deadline) "
                + "{ Start-Sleep -Milliseconds 25 }; "
                + $"if (-not [System.IO.File]::Exists({QuotePowerShellLiteral(readyPath)})) {{ throw 'child did not start' }}; "
                + "Write-Output 'root-stdout'; [Console]::Error.WriteLine('root-stderr')";
            var launcher = new CopilotBackgroundShellProcessLauncher();
            var stopwatch = Stopwatch.StartNew();
            using var process = await launcher.StartAsync(
                new CopilotShellProcessCommand(
                    CopilotShellKind.PowerShell,
                    executable!,
                    CopilotShellCommandService.BuildArguments(
                        CopilotShellKind.PowerShell,
                        command),
                    workspace,
                    TimeSpan.FromSeconds(10)),
                CancellationToken.None);

            var completion = await process.Completion.WaitAsync(TimeSpan.FromSeconds(10));
            stopwatch.Stop();
            childIdentity = ParseProcessIdentity(File.ReadAllText(readyPath));

            Assert.Equal(CopilotBackgroundShellCommandState.Completed, completion.State);
            Assert.Equal(0, completion.ExitCode);
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5));
            Assert.Contains("root-stdout", completion.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("root-stderr", completion.StandardError, StringComparison.Ordinal);
            Assert.Contains("child-stdout", completion.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("child-stderr", completion.StandardError, StringComparison.Ordinal);
            Assert.True(
                await WaitForProcessExitAsync(childIdentity.Value, TimeSpan.FromSeconds(2)),
                $"Background descendant {childIdentity.Value.Id} survived root completion.");
            await Task.Delay(1500);
            Assert.False(
                File.Exists(leakedMarkerPath),
                "A background descendant wrote output after terminal completion was published.");
        }
        finally
        {
            if (childIdentity.HasValue)
                await KillLeakedProcessAsync(childIdentity.Value);
            await DeleteTemporaryDirectoryAsync(workspace);
        }
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
            or Win32Exception or TimeoutException)
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

    private static ProcessIdentity ParseProcessIdentity(string value)
    {
        var parts = value.Split('|');
        Assert.Equal(2, parts.Length);
        return new ProcessIdentity(
            int.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture),
            new DateTime(
                long.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture),
                DateTimeKind.Utc));
    }

    private static string QuotePowerShellLiteral(string value) =>
        "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";

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

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"copilot-background-lifecycle-{Guid.NewGuid():N}");
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
                "copilot-background-lifecycle-",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Refusing to delete an unexpected background lifecycle test directory.");
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
            catch (UnauthorizedAccessException) when (
                stopwatch.Elapsed < TimeSpan.FromSeconds(5))
            {
                await Task.Delay(25);
            }
        }
    }
}
