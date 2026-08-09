using ColorVision.Copilot;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotBackgroundShellProcessLifecycleTests
{
    [Fact]
    public async Task DisposeWithoutWindowsJobTerminatesProcessAndPreservesCompletionWaiter()
    {
        var process = StartLongRunningProcess();
        var processId = process.Id;
        var owner = new CopilotBackgroundShellProcess(
            process,
            processJob: null,
            maximumLifetime: TimeSpan.FromMinutes(1));
        var completion = owner.Completion;

        try
        {
            owner.Dispose();

            var result = await completion.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Equal(CopilotBackgroundShellCommandState.Stopped, result.State);
            Assert.True(
                await WaitForProcessExitAsync(processId, TimeSpan.FromSeconds(2)),
                $"Background shell process {processId} survived disposal without a Windows Job.");
        }
        finally
        {
            owner.Dispose();
            await KillLeakedProcessAsync(processId);
        }
    }

    private static Process StartLongRunningProcess()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = Environment.GetEnvironmentVariable("ComSpec")
                ?? Path.Combine(Environment.SystemDirectory, "cmd.exe"),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add("/d");
        startInfo.ArgumentList.Add("/s");
        startInfo.ArgumentList.Add("/c");
        startInfo.ArgumentList.Add("ping -n 120 127.0.0.1 > NUL");

        var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true,
        };
        if (!process.Start())
        {
            process.Dispose();
            throw new InvalidOperationException("The lifecycle test process did not start.");
        }
        process.StandardInput.Close();
        return process;
    }

    private static async Task<bool> WaitForProcessExitAsync(int processId, TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            try
            {
                using var process = Process.GetProcessById(processId);
                if (process.HasExited)
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

    private static async Task KillLeakedProcessAsync(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            if (process.HasExited)
                return;
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception or TimeoutException)
        {
        }
    }
}
