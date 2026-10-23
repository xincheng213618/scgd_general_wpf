using System.Diagnostics;
using System.IO;
using System.Text;

namespace ColorVision.Copilot.Tests;

public sealed partial class CopilotCodexTests
{
    [Fact]
    public async Task OwnedProcessLifetimeAllowsEofExitAndReusesOneStopOperation()
    {
        await using var owned = await OwnedLifetimeTestProcess.StartAsync(
            "[void][Console]::In.ReadToEnd(); [Console]::Out.WriteLine('EOF'); [Console]::Out.Flush(); exit 42");

        // Environment.Exit inside Windows PowerShell delays process teardown independently of EOF.
        // Use the shell's normal exit and acknowledge EOF separately from the exit-code assertion.
        var receivedEof = owned.Output.ReadLineAsync();
        var stopping = owned.Lifetime.StopAsync();
        Assert.Same(stopping, owned.Lifetime.StopAsync());
        Assert.True(await stopping.WaitAsync(TestTimeout));
        Assert.Equal("EOF", await receivedEof.WaitAsync(TestTimeout));
        Assert.Equal(42, owned.Process.ExitCode);
        Assert.True(owned.Lifetime.Stop());
        Assert.Equal(42, owned.Process.ExitCode);
    }

    [Fact]
    public async Task OwnedProcessLifetimeHandlesAnAlreadyExitedChild()
    {
        await using var owned = await OwnedLifetimeTestProcess.StartAsync("exit 0");
        await owned.Process.WaitForExitAsync().WaitAsync(TestTimeout);

        Assert.True(await owned.Lifetime.StopAsync().WaitAsync(TestTimeout));
        Assert.True(owned.Lifetime.Stop());
        Assert.Equal(0, owned.Process.ExitCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OwnedProcessLifetimeSkipsBufferedFlushWhenStdinPipeIsFull(bool synchronous)
    {
        await using var owned = await OwnedLifetimeTestProcess.StartAsync(OwnedLifetimeTestProcess.KeepAlive);
        owned.Input.AutoFlush = false;
        // Fill the native anonymous pipe, then leave text only in StreamWriter's buffer.
        // StreamWriter.Close would block trying to flush this text to a child that never reads it.
        await owned.Input.BaseStream.WriteAsync(new byte[4096]).AsTask().WaitAsync(TestTimeout);
        owned.Input.Write("buffered shutdown text");

        var stopping = synchronous
            ? Task.Run(() =>
            {
                var previousContext = SynchronizationContext.Current;
                SynchronizationContext.SetSynchronizationContext(new NonDispatchingShutdownContext());
                try { return owned.Lifetime.Stop(); }
                finally { SynchronizationContext.SetSynchronizationContext(previousContext); }
            })
            : owned.Lifetime.StopAsync();

        Assert.True(await stopping.WaitAsync(TestTimeout));
        Assert.True(owned.Process.HasExited);
    }

    [Fact]
    public async Task OwnedProcessLifetimeReleasesAnOutstandingNativePipeWrite()
    {
        await using var owned = await OwnedLifetimeTestProcess.StartAsync(OwnedLifetimeTestProcess.KeepAlive);
        // This is the actual redirected FileStream, not a cancellable fake or a detached Close task.
        // A child that never reads cannot consume this payload; termination must settle the writer.
        var pendingWrite = owned.Input.BaseStream.WriteAsync(new byte[16 * 1024 * 1024]).AsTask();
        Assert.False(pendingWrite.IsCompletedSuccessfully);

        Assert.True(await owned.Lifetime.StopAsync().WaitAsync(TestTimeout));
        var failure = await Record.ExceptionAsync(() => pendingWrite.WaitAsync(TestTimeout));
        Assert.True(failure is IOException or ObjectDisposedException, failure?.ToString());
        Assert.True(pendingWrite.IsCompleted);
        Assert.True(owned.Process.HasExited);
    }

    [Fact]
    public async Task OwnedProcessLifetimeStopsOnlyItsOwnProcessTree()
    {
        await using var unrelated = await OwnedLifetimeTestProcess.StartAsync(OwnedLifetimeTestProcess.KeepAlive);
        var executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe");
        var childCommand = Convert.ToBase64String(Encoding.Unicode.GetBytes(OwnedLifetimeTestProcess.KeepAlive));
        var script = $$"""
            $childInfo = New-Object System.Diagnostics.ProcessStartInfo
            $childInfo.FileName = '{{executable.Replace("'", "''")}}'
            $childInfo.UseShellExecute = $false
            $childInfo.CreateNoWindow = $true
            $childInfo.Arguments = '-NoProfile -NonInteractive -EncodedCommand {{childCommand}}'
            $child = [System.Diagnostics.Process]::Start($childInfo)
            [Console]::Out.WriteLine($child.Id)
            [Console]::Out.Flush()
            {{OwnedLifetimeTestProcess.KeepAlive}}
            """;
        await using var owned = await OwnedLifetimeTestProcess.StartAsync(script);
        var childId = int.Parse((await owned.Output.ReadLineAsync().WaitAsync(TestTimeout))!, System.Globalization.CultureInfo.InvariantCulture);
        using var child = Process.GetProcessById(childId);
        try
        {
            Assert.True(await owned.Lifetime.StopAsync().WaitAsync(TestTimeout));
            await child.WaitForExitAsync().WaitAsync(TestTimeout);
            Assert.True(child.HasExited);
            Assert.False(unrelated.Process.HasExited);
        }
        finally
        {
            // The only PID used here came from the synthetic parent created by this test.
            if (!child.HasExited) child.Kill(entireProcessTree: true);
            await child.WaitForExitAsync().WaitAsync(TestTimeout);
        }
    }

    [Fact]
    public async Task OwnedProcessLifetimeReportsUnconfirmedExitWhenProcessAccessIsLost()
    {
        await using var owned = await OwnedLifetimeTestProcess.StartAsync(OwnedLifetimeTestProcess.KeepAlive);
        using var cleanupProcess = Process.GetProcessById(owned.Process.Id);
        // Losing the Process association makes both WaitForExit and Kill fail. The helper must
        // report that it could not confirm exit, rather than waiting forever or searching by name.
        owned.Process.Dispose();
        try
        {
            Assert.False(await owned.Lifetime.StopAsync().WaitAsync(TestTimeout));
            Assert.False(cleanupProcess.HasExited);
        }
        finally
        {
            if (!cleanupProcess.HasExited) cleanupProcess.Kill(entireProcessTree: true);
            await cleanupProcess.WaitForExitAsync().WaitAsync(TestTimeout);
        }
    }

    [Theory]
    [InlineData("io", true)]
    [InlineData("invalid_operation", true)]
    [InlineData("win32", true)]
    [InlineData("known_tree", true)]
    [InlineData("nested_known_tree", true)]
    [InlineData("mixed_tree", false)]
    [InlineData("nested_mixed_tree", false)]
    [InlineData("empty_tree", false)]
    [InlineData("unexpected", false)]
    public void OwnedProcessLifetimeRecognizesOnlyExpectedProcessTreeFailures(string kind, bool expected)
    {
        var io = new IOException("Synthetic pipe failure.");
        var invalidOperation = new InvalidOperationException("Synthetic lost process association.");
        var win32 = new System.ComponentModel.Win32Exception(5, "Synthetic process access failure.");
        var unexpected = new FormatException("Synthetic unexpected failure.");
        Exception failure = kind switch
        {
            "io" => io,
            "invalid_operation" => invalidOperation,
            "win32" => win32,
            "known_tree" => new AggregateException(io, invalidOperation, win32),
            "nested_known_tree" => new AggregateException(io, new AggregateException(invalidOperation, win32)),
            "mixed_tree" => new AggregateException(io, unexpected),
            "nested_mixed_tree" => new AggregateException(io, new AggregateException(win32, unexpected)),
            "empty_tree" => new AggregateException(),
            _ => unexpected,
        };

        Assert.Equal(expected, CopilotCodexOwnedProcessLifetime.IsExpectedProcessFailure(failure));
    }

    private sealed class NonDispatchingShutdownContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback callback, object? state) =>
            throw new InvalidOperationException("Process shutdown must not require the caller's UI context.");
    }

    private sealed class OwnedLifetimeTestProcess : IAsyncDisposable
    {
        internal const string KeepAlive = "while ($true) { [System.Threading.Thread]::Sleep(1000) }";
        internal Process Process { get; }
        internal StreamWriter Input { get; }
        internal StreamReader Output { get; }
        private StreamReader Error { get; }
        internal CopilotCodexOwnedProcessLifetime Lifetime { get; }

        private OwnedLifetimeTestProcess(Process process)
        {
            Process = process;
            Input = process.StandardInput;
            Output = process.StandardOutput;
            Error = process.StandardError;
            Lifetime = new CopilotCodexOwnedProcessLifetime(process, Input);
        }

        internal static async Task<OwnedLifetimeTestProcess> StartAsync(string script)
        {
            var process = CreateOwnedPowerShellProcess("[Console]::Out.WriteLine('READY'); [Console]::Out.Flush(); " + script);
            Assert.True(process.Start());
            var owned = new OwnedLifetimeTestProcess(process);
            try
            {
                Assert.Equal("READY", await owned.Output.ReadLineAsync().WaitAsync(TestTimeout));
                return owned;
            }
            catch
            {
                await owned.DisposeAsync();
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                try
                {
                    if (!Process.HasExited) Process.Kill(entireProcessTree: true);
                    await Process.WaitForExitAsync().WaitAsync(TestTimeout);
                }
                catch (InvalidOperationException)
                {
                    // The lost-association test retains a separate handle for its own cleanup.
                }
            }
            finally
            {
                try { Input.Dispose(); }
                catch (Exception exception) when (exception is IOException or InvalidOperationException) { }
                Output.Dispose();
                Error.Dispose();
                Process.Dispose();
            }
        }
    }
}
