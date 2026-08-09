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
    public async Task CompletedHookProcessCanPreserveDetachedDescendant()
    {
        var workspace = CreateTemporaryDirectory();
        var markerPath = Path.Combine(workspace, "preserved.txt");
        try
        {
            var result = await RunDetachedMarkerCommandAsync(
                workspace,
                markerPath,
                keepRootAlive: false,
                preserveDescendants: true,
                timeout: TimeSpan.FromSeconds(5));

            Assert.False(result.TimedOut);
            Assert.Equal(0, result.ExitCode);
            Assert.True(result.ProcessTreeContained);
            Assert.True(
                await WaitForFileAsync(markerPath, TimeSpan.FromSeconds(5)),
                "The completed hook's detached descendant was terminated with the root process.");
        }
        finally
        {
            await DeleteTemporaryDirectoryAsync(workspace);
        }
    }

    [Fact]
    public async Task CompletedApprovedShellProcessStillTerminatesDetachedDescendant()
    {
        var workspace = CreateTemporaryDirectory();
        var markerPath = Path.Combine(workspace, "must-not-survive.txt");
        try
        {
            var result = await RunDetachedMarkerCommandAsync(
                workspace,
                markerPath,
                keepRootAlive: false,
                preserveDescendants: false,
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
    public async Task TimedOutHookProcessStillTerminatesDetachedDescendant()
    {
        var workspace = CreateTemporaryDirectory();
        var markerPath = Path.Combine(workspace, "must-not-exist.txt");
        try
        {
            var result = await RunDetachedMarkerCommandAsync(
                workspace,
                markerPath,
                keepRootAlive: true,
                preserveDescendants: true,
                timeout: TimeSpan.FromMilliseconds(500));

            Assert.True(result.TimedOut);
            Assert.True(result.ProcessTreeContained);
            Assert.True(File.Exists(GetLaunchMarkerPath(markerPath)));
            await Task.Delay(TimeSpan.FromSeconds(2));
            Assert.False(
                File.Exists(markerPath),
                "A detached descendant survived after the hook process timed out.");
        }
        finally
        {
            await DeleteTemporaryDirectoryAsync(workspace);
        }
    }

    [Fact]
    public async Task CancelledHookProcessStillTerminatesDetachedDescendant()
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
                    preserveDescendants: true,
                    timeout: TimeSpan.FromSeconds(5),
                    cancellationSource.Token,
                    chunk =>
                    {
                        if (!chunk.Contains("child-launched", StringComparison.Ordinal))
                            return;
                        launchObserved.TrySetResult();
                        cancellationSource.Cancel();
                    }));

            await launchObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(File.Exists(GetLaunchMarkerPath(markerPath)));
            await Task.Delay(TimeSpan.FromSeconds(2));
            Assert.False(
                File.Exists(markerPath),
                "A detached descendant survived after the hook process was cancelled.");
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
        bool preserveDescendants,
        TimeSpan timeout,
        CancellationToken cancellationToken = default,
        Action<string>? standardOutputReceived = null)
    {
        var executable = CopilotShellCommandService.FindTrustedShellExecutable(
            CopilotShellKind.PowerShell);
        Assert.False(string.IsNullOrWhiteSpace(executable));

        var childScript = "Start-Sleep -Milliseconds 1000; "
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
                PreserveDescendantsOnCompletion = preserveDescendants,
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
