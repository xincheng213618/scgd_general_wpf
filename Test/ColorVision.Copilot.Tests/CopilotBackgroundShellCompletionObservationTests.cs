using ColorVision.Copilot;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotBackgroundShellCompletionObservationTests
{
    [Fact]
    public async Task CancelledTerminalObservationKeepsCompletionAvailableForAgentDelivery()
    {
        var process = new ControlledBackgroundShellProcess(
            wakeObservationOnCompletion: false);
        var registry = new CopilotBackgroundShellCommandRegistry(
            new ControlledBackgroundShellProcessLauncher(process));
        var completionPublished =
            new TaskCompletionSource<CopilotBackgroundShellCommandCompletedEventArgs>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        registry.CommandCompleted += (_, eventArgs) =>
            completionPublished.TrySetResult(eventArgs);

        try
        {
            var started = await registry.StartAsync(
                CreateRequest(),
                CreateInput(),
                CancellationToken.None);
            Assert.True(started.Success, started.ErrorMessage);
            using var cancellation = new CancellationTokenSource();
            var observation = registry.WaitForObservationAsync(
                "conversation-1",
                started.Snapshot!.Id,
                outputContains: null,
                timeoutSeconds: 10,
                onSnapshot: null,
                cancellation.Token);
            await process.ObservationWaitStarted.WaitAsync(
                TimeSpan.FromSeconds(5));

            process.CompleteSuccessfully();
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                async () => await observation);
            var published = await completionPublished.Task.WaitAsync(
                TimeSpan.FromSeconds(5));
            Assert.True(published.TerminalObservationWasPendingAtCompletion);
            Assert.False(published.TerminalResultWasReturned);
        }
        finally
        {
            await registry.ShutdownAsync();
        }
    }

    [Fact]
    public async Task ReturnedTerminalObservationSuppressesDuplicateAgentDelivery()
    {
        var process = new ControlledBackgroundShellProcess(
            wakeObservationOnCompletion: true);
        var registry = new CopilotBackgroundShellCommandRegistry(
            new ControlledBackgroundShellProcessLauncher(process));
        var completionPublished =
            new TaskCompletionSource<CopilotBackgroundShellCommandCompletedEventArgs>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        registry.CommandCompleted += (_, eventArgs) =>
            completionPublished.TrySetResult(eventArgs);

        try
        {
            var started = await registry.StartAsync(
                CreateRequest(),
                CreateInput(),
                CancellationToken.None);
            Assert.True(started.Success, started.ErrorMessage);
            var observation = registry.WaitForObservationAsync(
                "conversation-1",
                started.Snapshot!.Id,
                outputContains: null,
                timeoutSeconds: 10,
                onSnapshot: null,
                CancellationToken.None);
            await process.ObservationWaitStarted.WaitAsync(
                TimeSpan.FromSeconds(5));

            process.CompleteSuccessfully();

            var observed = await observation.WaitAsync(TimeSpan.FromSeconds(5));
            var published = await completionPublished.Task.WaitAsync(
                TimeSpan.FromSeconds(5));
            Assert.Equal(
                CopilotBackgroundShellCommandObservation.Terminal,
                observed.Observation);
            Assert.True(published.TerminalObservationWasPendingAtCompletion);
            Assert.True(published.TerminalResultWasReturned);
        }
        finally
        {
            await registry.ShutdownAsync();
        }
    }

    private static CopilotAgentRequest CreateRequest() => new()
    {
        ConversationId = "conversation-1",
        TaskId = "task-1",
        Profile = CopilotProfileConfig.CreateDefault(),
        PreferredShell = CopilotShellKind.CommandPrompt,
    };

    private static CopilotAgentToolInput CreateInput() => new()
    {
        Arguments = new Dictionary<string, object?>
        {
            ["command"] = "echo test",
            ["shell"] = "cmd",
        },
    };

    private sealed class ControlledBackgroundShellProcessLauncher(
        ControlledBackgroundShellProcess process) :
        ICopilotBackgroundShellProcessLauncher
    {
        public Task<ICopilotBackgroundShellProcess> StartAsync(
            CopilotShellProcessCommand command,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<ICopilotBackgroundShellProcess>(process);
        }
    }

    private sealed class ControlledBackgroundShellProcess(
        bool wakeObservationOnCompletion) : ICopilotBackgroundShellProcess
    {
        private readonly TaskCompletionSource<CopilotBackgroundShellProcessCompletion>
            _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _observationWaitStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int ProcessId => 123;

        public bool ProcessTreeContained => true;

        public Task<CopilotBackgroundShellProcessCompletion> Completion =>
            _completion.Task;

        public Task ObservationWaitStarted => _observationWaitStarted.Task;

        public void CompleteSuccessfully()
        {
            _completion.TrySetResult(CreateCompletion(
                CopilotBackgroundShellCommandState.Completed,
                exitCode: 0));
        }

        public CopilotBackgroundShellProcessOutput GetOutputSnapshot()
        {
            var observationVersion = _completion.Task.IsCompletedSuccessfully
                ? 1
                : 0;
            return new CopilotBackgroundShellProcessOutput(
                string.Empty,
                string.Empty,
                0,
                0,
                false,
                false,
                false,
                false,
                0,
                0,
                false,
                false)
            {
                ObservationVersion = observationVersion,
            };
        }

        public CopilotRedactedOutputArchivePage ReadOutputArchive(
            CopilotBackgroundShellOutputStream stream,
            int offsetCharacters,
            int maximumCharacters,
            CancellationToken cancellationToken) =>
            new(false, string.Empty, 0, 0, 0, 0, true, false, "Unavailable.");

        public CopilotRedactedOutputArchiveSearchResult SearchOutputArchive(
            CopilotBackgroundShellOutputStream stream,
            string literal,
            int offsetCharacters,
            CancellationToken cancellationToken) =>
            new(false, false, 0, 0, false, "Unavailable.");

        public async Task WaitForObservationChangeAsync(
            long observationVersion,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            _observationWaitStarted.TrySetResult();
            if (wakeObservationOnCompletion)
            {
                await _completion.Task.WaitAsync(cancellationToken);
                return;
            }

            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }

        public async Task<CopilotBackgroundShellProcessCompletion> StopAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _completion.TrySetResult(CreateCompletion(
                CopilotBackgroundShellCommandState.Stopped,
                exitCode: null));
            return await _completion.Task;
        }

        public void Dispose()
        {
            _completion.TrySetResult(CreateCompletion(
                CopilotBackgroundShellCommandState.Stopped,
                exitCode: null));
        }

        private static CopilotBackgroundShellProcessCompletion CreateCompletion(
            CopilotBackgroundShellCommandState state,
            int? exitCode) => new(
                state,
                exitCode,
                DateTimeOffset.UtcNow,
                string.Empty,
                string.Empty)
            {
                ObservationVersion = 1,
            };
    }
}
