using System.Globalization;
using System.Text;
using System.Text.Json;

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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ModelVisibleBackgroundOutputPagesPreserveContinuousEvidenceAndCommandState(bool completed)
    {
        const int tokenLimit = 512;
        var original = " \t" + string.Concat(Enumerable.Range(0, 600).Select(index => $"{index:D4}界😀|")) + "tail\t ";
        using var archive = CopilotTemporaryRedactedOutputArchive.TryCreate("BackgroundOutput", "stdout");
        Assert.NotNull(archive);
        archive!.Append(original);
        var process = new ControlledBackgroundShellProcess(
            wakeObservationOnCompletion: true, standardOutputArchive: archive);
        var registry = new CopilotBackgroundShellCommandRegistry(new ControlledBackgroundShellProcessLauncher(process));
        var request = CreateRequest();
        var readRequest = new CopilotAgentRequest
        {
            ConversationId = request.ConversationId,
            TaskId = request.TaskId,
            Profile = request.Profile,
            ToolOutputTokenLimitOverride = tokenLimit,
        };
        try
        {
            var started = await registry.StartAsync(request, CreateInput(), CancellationToken.None);
            Assert.True(started.Success, started.ErrorMessage);
            var backgroundId = started.Snapshot!.Id;
            if (completed)
                process.CompleteSuccessfully();
            var expected = original[..archive.ArchivedCharacters];
            Assert.True(expected.Length > tokenLimit * CopilotTokenEstimator.AsciiCharactersPerToken);
            var tool = new CopilotReadBackgroundShellCommandOutputTool(registry);
            var executor = new CopilotToolExecutor([]);
            var combined = new StringBuilder();
            var offset = 0;
            while (true)
            {
                var events = new List<CopilotAgentEvent>();
                var outcome = await executor.ExecuteAsync(new CopilotToolInvocation
                {
                    CallId = $"call:background-page:{offset}",
                    Round = 1,
                    Attempt = 1,
                    MaxAttempts = 1,
                    RuntimeName = "test",
                    Tool = tool,
                    AgentRequest = readRequest,
                    ToolInput = new CopilotAgentToolInput
                    {
                        Arguments = new Dictionary<string, object?>
                        {
                            ["backgroundId"] = backgroundId,
                            ["stream"] = "stdout",
                            ["offsetCharacters"] = offset,
                            ["maximumCharacters"] = CopilotBackgroundShellCommandRegistry.MaximumArchiveReadCharacters,
                        },
                    },
                }, events.Add, CancellationToken.None);
                var captured = CopilotToolResultContract.Capture(tool.Name, outcome.Result);
                Assert.True(captured.Success, captured.ErrorMessage);
                var evidence = Assert.Single(captured.BackgroundShellCommands);
                Assert.Equal(backgroundId, evidence.Id);
                Assert.Equal(completed ? CopilotBackgroundShellCommandState.Completed : CopilotBackgroundShellCommandState.Running, evidence.State);
                Assert.Equal(completed, evidence.IsTerminal);
                Assert.Equal(completed ? (int?)0 : null, evidence.ExitCode);
                var published = Assert.Single(events, item => item.Type == CopilotAgentEventType.ToolResult);
                Assert.Equal(evidence, Assert.Single(Assert.IsType<CopilotToolResult>(published.ToolResult).BackgroundShellCommands));
                Assert.Equal(outcome.FormattedModelResult, published.ModelToolResult);
                Assert.Equal(published.ModelToolResult, CopilotFrameworkToolResultFormatter.Format(outcome, tokenLimit));
                Assert.Null(outcome.ToolOutputArchive);
                CopilotAgentEventProtocol.Validate(published);
                Assert.True(CopilotTokenEstimator.EstimateTextWeight(published.ModelToolResult)
                    <= tokenLimit * CopilotTokenEstimator.AsciiCharactersPerToken);

                using var document = JsonDocument.Parse(published.ModelToolResult);
                var modelContent = document.RootElement.GetProperty("content").GetString()!.Replace("\r\n", "\n", StringComparison.Ordinal);
                const string contentMarker = "\ncontent:\n";
                var contentStart = modelContent.IndexOf(contentMarker, StringComparison.Ordinal);
                Assert.True(contentStart >= 0, "The model-visible page must retain its header and body boundary.");
                var header = modelContent[..contentStart].Split('\n')
                    .Select(line => line.Split(": ", 2, StringSplitOptions.None))
                    .Where(parts => parts.Length == 2)
                    .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.Ordinal);
                var body = modelContent[(contentStart + contentMarker.Length)..];
                var returned = int.Parse(header["returned_characters"], CultureInfo.InvariantCulture);
                var next = int.Parse(header["next_offset_characters"], CultureInfo.InvariantCulture);
                var endOfAvailableOutput = next == expected.Length;
                Assert.Equal(offset, int.Parse(header["offset_characters"], CultureInfo.InvariantCulture));
                Assert.Equal(expected.Length, int.Parse(header["archived_characters"], CultureInfo.InvariantCulture));
                Assert.Equal(body.Length, returned);
                Assert.Equal(offset + returned, next);
                Assert.InRange(next, offset + 1, expected.Length);
                Assert.Equal(expected[offset..next], body);
                Assert.False(char.IsLowSurrogate(body[0]));
                Assert.False(char.IsHighSurrogate(body[^1]));
                Assert.Equal(endOfAvailableOutput ? "true" : "false", header["end_of_available_output"]);
                Assert.Equal(completed ? "false" : "true", header["command_active"]);
                Assert.Equal(completed ? "completed" : "running", header["state"]);
                Assert.Equal("false", header["archive_truncated"]);
                Assert.Equal($"Read {returned} archived stdout character(s) from background command {backgroundId}; "
                    + (endOfAvailableOutput
                        ? completed ? "reached the archive end." : "reached the currently available end while the command remains active."
                        : "more archived output is available."), document.RootElement.GetProperty("summary").GetString());
                combined.Append(body);
                offset = next;
                if (endOfAvailableOutput)
                    break;
            }
            Assert.Equal(expected, combined.ToString());
        }
        finally
        {
            await registry.ShutdownAsync();
        }
    }

    [Fact]
    public async Task ModelBudgetTooSmallForBackgroundPagePreservesOriginalCompletionEvidence()
    {
        const int tokenLimit = 32;
        const string output = "completed stdout evidence";
        using var archive = CopilotTemporaryRedactedOutputArchive.TryCreate("BackgroundOutput", "stdout");
        Assert.NotNull(archive);
        archive!.Append(output);
        var process = new ControlledBackgroundShellProcess(
            wakeObservationOnCompletion: true, standardOutputArchive: archive);
        var registry = new CopilotBackgroundShellCommandRegistry(new ControlledBackgroundShellProcessLauncher(process));
        var request = CreateRequest();
        try
        {
            var started = await registry.StartAsync(request, CreateInput(), CancellationToken.None);
            Assert.True(started.Success, started.ErrorMessage);
            process.CompleteSuccessfully();
            var tool = new CopilotReadBackgroundShellCommandOutputTool(registry);
            var events = new List<CopilotAgentEvent>();
            var outcome = await new CopilotToolExecutor([]).ExecuteAsync(new CopilotToolInvocation
            {
                CallId = "call:background-tiny-page",
                Round = 1,
                Attempt = 1,
                MaxAttempts = 1,
                RuntimeName = "test",
                Tool = tool,
                AgentRequest = new CopilotAgentRequest
                {
                    ConversationId = request.ConversationId,
                    TaskId = request.TaskId,
                    Profile = request.Profile,
                    ToolOutputTokenLimitOverride = tokenLimit,
                },
                ToolInput = new CopilotAgentToolInput
                {
                    Arguments = new Dictionary<string, object?> { ["backgroundId"] = started.Snapshot!.Id },
                },
            }, events.Add, CancellationToken.None);
            Assert.True(outcome.Result.Success, outcome.Result.ErrorMessage);
            var nativeRead = Assert.IsType<CopilotBackgroundShellCommandOutputReadResult>(outcome.Result.BackgroundShellOutputArchiveRead);
            Assert.Equal(CopilotBackgroundShellCommandState.Completed, nativeRead.Snapshot!.State);
            Assert.False(nativeRead.Snapshot.IsActive);
            Assert.Equal(output, nativeRead.Page!.Content);
            Assert.Equal(output.Length, nativeRead.Page.ReturnedCharacters);
            Assert.Equal(output.Length, nativeRead.Page.NextOffsetCharacters);
            Assert.True(nativeRead.Page.EndOfAvailableOutput);
            var evidence = Assert.Single(outcome.Result.BackgroundShellCommands);
            Assert.True(evidence.IsTerminal);
            Assert.Equal(CopilotBackgroundShellCommandState.Completed, evidence.State);
            Assert.Equal(0, evidence.ExitCode);
            var published = Assert.Single(events, item => item.Type == CopilotAgentEventType.ToolResult);
            var publishedResult = Assert.IsType<CopilotToolResult>(published.ToolResult);
            Assert.Equal(nativeRead, publishedResult.BackgroundShellOutputArchiveRead);
            Assert.Equal(evidence, Assert.Single(publishedResult.BackgroundShellCommands));
            Assert.Equal(outcome.Result.Content, outcome.StepRecord.Observation.Content);
            Assert.Equal(outcome.Result.Summary, outcome.StepRecord.Observation.Summary);
            Assert.Contains(output, outcome.StepRecord.Observation.Content, StringComparison.Ordinal);
            Assert.Equal(published.ModelToolResult, outcome.StepRecord.ModelToolResult);
            Assert.Null(outcome.ToolOutputArchive);

            using var document = JsonDocument.Parse(published.ModelToolResult);
            Assert.DoesNotContain("next_offset_characters", published.ModelToolResult, StringComparison.Ordinal);
            Assert.DoesNotContain("reached the archive end", published.ModelToolResult, StringComparison.Ordinal);
            Assert.DoesNotContain("<empty>", published.ModelToolResult, StringComparison.Ordinal);
            if (document.RootElement.TryGetProperty("content", out var content))
                Assert.True(string.IsNullOrEmpty(content.GetString()));
            if (document.RootElement.TryGetProperty("summary", out var summary))
                Assert.True(string.IsNullOrEmpty(summary.GetString()));
            Assert.True(CopilotTokenEstimator.EstimateTextWeight(published.ModelToolResult)
                <= tokenLimit * CopilotTokenEstimator.AsciiCharactersPerToken);
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
        bool wakeObservationOnCompletion,
        CopilotTemporaryRedactedOutputArchive? standardOutputArchive = null) : ICopilotBackgroundShellProcess
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
            standardOutputArchive?.Complete();
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
                standardOutputArchive?.ObservedCharacters ?? 0,
                0,
                false,
                false,
                standardOutputArchive?.Available == true,
                false,
                standardOutputArchive?.ArchivedCharacters ?? 0,
                0,
                standardOutputArchive?.IsTruncated == true,
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
            stream == CopilotBackgroundShellOutputStream.StandardOutput && standardOutputArchive != null
                ? standardOutputArchive.Read(offsetCharacters, maximumCharacters, cancellationToken)
                : new(false, string.Empty, 0, 0, 0, 0, true, false, "Unavailable.");

        public CopilotRedactedOutputArchiveSearchResult SearchOutputArchive(
            CopilotBackgroundShellOutputStream stream,
            string literal,
            int offsetCharacters,
            CancellationToken cancellationToken) =>
            stream == CopilotBackgroundShellOutputStream.StandardOutput && standardOutputArchive != null
                ? standardOutputArchive.Search(literal, offsetCharacters, cancellationToken)
                : new(false, false, 0, 0, false, "Unavailable.");

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
            standardOutputArchive?.Complete();
            _completion.TrySetResult(CreateCompletion(
                CopilotBackgroundShellCommandState.Stopped,
                exitCode: null));
            return await _completion.Task;
        }

        public void Dispose()
        {
            standardOutputArchive?.Complete();
            _completion.TrySetResult(CreateCompletion(
                CopilotBackgroundShellCommandState.Stopped,
                exitCode: null));
        }

        private CopilotBackgroundShellProcessCompletion CreateCompletion(
            CopilotBackgroundShellCommandState state,
            int? exitCode) => new(
                state,
                exitCode,
                DateTimeOffset.UtcNow,
                string.Empty,
                string.Empty)
            {
                ObservationVersion = 1,
                ObservedStandardOutputCharacters = standardOutputArchive?.ObservedCharacters ?? 0,
                StandardOutputArchiveAvailable = standardOutputArchive?.Available == true,
                ArchivedStandardOutputCharacters = standardOutputArchive?.ArchivedCharacters ?? 0,
                StandardOutputArchiveTruncated = standardOutputArchive?.IsTruncated == true,
            };
    }
}
