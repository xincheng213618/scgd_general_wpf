using ColorVision.Copilot;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotSettledShellCancellationTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task CompletedShellCancellationDoesNotProveThatItsWriteWasUndone()
    {
        var workspace = Path.Combine(Path.GetTempPath(), "copilot-settled-shell-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);
        var marker = Path.Combine(workspace, "write-completed.txt");
        using var tool = new PartiallyCompletedShellTool(workspace, marker);
        var events = new ConcurrentQueue<CopilotAgentEvent>();
        var execution = new CopilotToolExecutor().ExecuteAsync(
            new CopilotToolInvocation
            {
                CallId = "settled-shell-write",
                Round = 1,
                Attempt = 1,
                MaxAttempts = 1,
                RuntimeName = "SettledShellCancellationTest",
                Tool = tool,
                FrameworkApprovalGranted = true,
                AgentRequest = new CopilotAgentRequest
                {
                    Mode = CopilotAgentMode.Code,
                    UserText = "Write the test marker in its isolated temporary directory.",
                    WorkspacePath = workspace,
                },
            }, events.Enqueue, CancellationToken.None);

        try
        {
            var deadline = Stopwatch.StartNew();
            while (!File.Exists(marker) && deadline.Elapsed < TestTimeout)
                await Task.Delay(25);
            Assert.True(File.Exists(marker), "The owned shell did not write its marker before cancellation.");

            // Only the runner's token is cancelled. The executor observes the settled
            // tool task's OCE, without racing its own WaitAsync cancellation token.
            tool.CancelProcess();
            var exception = await Assert.ThrowsAsync<CopilotToolExecutionCancellationException>(
                () => execution.WaitAsync(TestTimeout));

            Assert.NotNull(tool.RunnerTask);
            Assert.True(tool.RunnerTask.IsCanceled);
            Assert.Equal("written before cancellation", File.ReadAllText(marker));
            Assert.Equal(CopilotToolExecutionState.Interrupted, exception.Outcome.Execution.State);
            Assert.Equal(CopilotToolFailureKind.OutcomeUnknown, exception.Outcome.Result.FailureKind);
            Assert.Equal(CopilotToolFailureCode.OutcomeUnknown, exception.Outcome.Result.FailureCode);
            Assert.False(exception.Outcome.Execution.RetryEligible);
            var terminal = Assert.Single(events, item => item.Type == CopilotAgentEventType.ToolResult);
            Assert.Same(exception.Outcome.Result, terminal.ToolResult);
            Assert.Equal(CopilotToolFailureCode.OutcomeUnknown, terminal.ToolResult!.FailureCode);
        }
        finally
        {
            tool.CancelProcess();
            try { await execution.WaitAsync(TestTimeout); }
            catch (Exception) { }
            await tool.TerminateOwnedProcessIfNeededAsync();
            var fullPath = Path.GetFullPath(workspace);
            var tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!string.Equals(Path.GetDirectoryName(fullPath), tempRoot, StringComparison.OrdinalIgnoreCase)
                || !Path.GetFileName(fullPath).StartsWith("copilot-settled-shell-", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Refusing to delete an unexpected shell test directory.");
            }
            Directory.Delete(fullPath, recursive: true);
        }
    }

    private sealed class PartiallyCompletedShellTool : ICopilotFrameworkApprovedTool, IDisposable
    {
        private readonly CancellationTokenSource _processCancellation = new(TimeSpan.FromSeconds(20));
        private readonly TaskCompletionSource<(int Id, DateTime StartTimeUtc)> _processIdentity =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly CopilotShellProcessCommand _command;
        private readonly CopilotShellProcessRunner _runner;

        public PartiallyCompletedShellTool(string workspace, string marker)
        {
            var executable = CopilotShellCommandService.FindTrustedShellExecutable(CopilotShellKind.PowerShell);
            Assert.False(string.IsNullOrWhiteSpace(executable));
            var script = "[System.IO.File]::WriteAllText("
                + "'" + marker.Replace("'", "''", StringComparison.Ordinal) + "', 'written before cancellation')"
                + Environment.NewLine + "Start-Sleep -Seconds 30";
            _command = new CopilotShellProcessCommand(
                CopilotShellKind.PowerShell,
                executable!,
                CopilotShellCommandService.BuildArguments(CopilotShellKind.PowerShell, script),
                workspace,
                TimeSpan.FromSeconds(20));
            _runner = new CopilotShellProcessRunner(process =>
            {
                _processIdentity.TrySetResult((process.Id, process.StartTime.ToUniversalTime()));
                return CopilotWindowsProcessJob.TryAssign(process);
            });
        }

        public string Name => "PartiallyCompletedShell";
        public string Description => "Run a test-owned shell which writes a temporary marker before cancellation.";
        public CopilotToolCapabilityDescriptor Capability { get; } = CopilotToolCapabilityDescriptor.ProtectedWrite(
            CopilotToolIdempotency.NonIdempotent, TimeSpan.FromSeconds(30));
        public Task<CopilotShellProcessResult>? RunnerTask { get; private set; }
        public bool CanHandle(CopilotAgentRequest request) => true;
        public void CancelProcess() => _processCancellation.Cancel();

        public async Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request, CopilotAgentToolInput toolInput, CancellationToken cancellationToken)
        {
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _processCancellation.Token);
            RunnerTask = _runner.RunAsync(_command, cancellation.Token);
            await RunnerTask.ConfigureAwait(false);
            throw new InvalidOperationException("The test shell completed without the expected cancellation.");
        }

        public Task<CopilotToolResult> ExecuteApprovedAsync(
            CopilotAgentRequest request, CopilotAgentToolInput toolInput, CancellationToken cancellationToken) =>
            ExecuteAsync(request, toolInput, cancellationToken);

        public async Task TerminateOwnedProcessIfNeededAsync()
        {
            if (!_processIdentity.Task.IsCompletedSuccessfully)
                return;
            var identity = _processIdentity.Task.Result;
            try
            {
                using var process = Process.GetProcessById(identity.Id);
                if (process.StartTime.ToUniversalTime() != identity.StartTimeUtc || process.HasExited)
                    return;
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync().WaitAsync(TestTimeout);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception)
            {
                // The process may have exited between identity lookup and cleanup.
            }
        }

        public void Dispose() => _processCancellation.Dispose();
    }
}
