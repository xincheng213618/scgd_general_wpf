using System.IO;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotBackgroundShellMaintenanceGuardTests
{
    [Fact]
    public async Task MaintenanceGuardReadsOnlyReservationsAndCompletionWithoutConsumingOutput()
    {
        var process = new FakeProcess();
        var launcher = new DeferredLauncher();
        var registry = new CopilotBackgroundShellCommandRegistry(launcher);
        Assert.False(registry.HasActiveCommands);
        Task<CopilotBackgroundShellCommandStartResult> starting = registry.StartAsync(new CopilotAgentRequest
        {
            ConversationId = "maintenance-test", TaskId = "task", Profile = CopilotProfileConfig.CreateDefault(),
            PreferredShell = CopilotShellKind.CommandPrompt,
        }, new CopilotAgentToolInput
        {
            Arguments = new Dictionary<string, object?> { ["command"] = "echo fake", ["shell"] = "cmd" },
        }, CancellationToken.None);
        try
        {
            await launcher.Called.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(registry.HasActiveCommands); // Reservation exists before a fake process is returned.
            Assert.Equal(0, process.OutputReadCount);
            launcher.Result.SetResult(process);
            var started = await starting.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(started.Success, started.ErrorMessage);
            int reads = process.OutputReadCount;
            Assert.True(registry.HasActiveCommands);
            Assert.True(registry.HasActiveCommands);
            Assert.Equal(reads, process.OutputReadCount);
            process.Complete();
            Assert.False(registry.HasActiveCommands);
            Assert.Equal(reads, process.OutputReadCount);
        }
        finally
        {
            launcher.Result.TrySetResult(process);
            await starting.WaitAsync(TimeSpan.FromSeconds(5));
            await registry.ShutdownAsync();
        }
    }

    [Fact]
    public async Task ShutdownDuringStartDisposesTheLateProcessEvenWhenStoppingFails()
    {
        var process = new FakeProcess(stopFails: true);
        var launcher = new DeferredLauncher();
        var registry = new CopilotBackgroundShellCommandRegistry(launcher);
        var starting = registry.StartAsync(new()
        {
            ConversationId = "shutdown-start-handoff", TaskId = "task", Profile = CopilotProfileConfig.CreateDefault(),
            PreferredShell = CopilotShellKind.CommandPrompt,
        }, new()
        {
            Arguments = new Dictionary<string, object?> { ["command"] = "echo fake", ["shell"] = "cmd" },
        }, CancellationToken.None);
        try
        {
            await launcher.Called.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await registry.ShutdownAsync();
            launcher.Result.SetResult(process);

            var started = await starting.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.False(started.Success);
            Assert.Equal(CopilotToolFailureKind.Internal, started.FailureKind);
            Assert.Null(started.Snapshot);
            Assert.Empty(registry.GetSnapshots("shutdown-start-handoff"));
            Assert.False(registry.HasActiveCommands);
            Assert.Equal(1, process.StopCount);
            Assert.Equal(1, process.DisposeCount);
            Assert.True(process.Completion.IsCompletedSuccessfully);
        }
        finally
        {
            launcher.Result.TrySetResult(process);
            await starting.WaitAsync(TimeSpan.FromSeconds(5));
            if (!process.Completion.IsCompletedSuccessfully)
                process.Dispose();
            await registry.ShutdownAsync();
        }
    }

    private sealed class DeferredLauncher : ICopilotBackgroundShellProcessLauncher
    {
        public TaskCompletionSource Called { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<ICopilotBackgroundShellProcess> Result { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<ICopilotBackgroundShellProcess> StartAsync(CopilotShellProcessCommand command, CancellationToken cancellationToken)
        {
            Called.TrySetResult();
            return Result.Task;
        }
    }

    private sealed class FakeProcess(bool stopFails = false) : ICopilotBackgroundShellProcess
    {
        private readonly TaskCompletionSource<CopilotBackgroundShellProcessCompletion> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int ProcessId => 123;
        public bool ProcessTreeContained => true;
        public int OutputReadCount { get; private set; }
        public int StopCount { get; private set; }
        public int DisposeCount { get; private set; }
        public Task<CopilotBackgroundShellProcessCompletion> Completion => _completion.Task;
        public void Complete() => _completion.TrySetResult(new(CopilotBackgroundShellCommandState.Completed, 0, DateTimeOffset.UtcNow, string.Empty, string.Empty));
        public CopilotBackgroundShellProcessOutput GetOutputSnapshot()
        {
            OutputReadCount++;
            return new(string.Empty, string.Empty, 0, 0, false, false, false, false, 0, 0, false, false);
        }
        public CopilotRedactedOutputArchivePage ReadOutputArchive(CopilotBackgroundShellOutputStream stream, int offsetCharacters, int maximumCharacters, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Maintenance must not read archived output.");
        public CopilotRedactedOutputArchiveSearchResult SearchOutputArchive(CopilotBackgroundShellOutputStream stream, string literal, int offsetCharacters, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Maintenance must not search archived output.");
        public Task WaitForObservationChangeAsync(long observationVersion, TimeSpan timeout, CancellationToken cancellationToken)
            => Completion.WaitAsync(cancellationToken);
        public Task<CopilotBackgroundShellProcessCompletion> StopAsync(CancellationToken cancellationToken)
        {
            StopCount++;
            if (stopFails)
                throw new IOException("The controlled process stop failed before releasing ownership.");
            Complete();
            return Completion;
        }
        public void Dispose()
        {
            DisposeCount++;
            Complete();
        }
    }
}
