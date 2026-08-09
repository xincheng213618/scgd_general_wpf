using ColorVision.Copilot;
using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;

namespace ColorVision.UI.Tests;

public sealed class CopilotCodexSessionEndHookTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void JsonAndInlineTomlNormalizeSessionEndHandlers(bool inlineToml)
    {
        var codexHome = CreateTemporaryDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(codexHome, inlineToml ? "config.toml" : "hooks.json"),
                inlineToml
                    ? """
                      [[hooks.SessionEnd]]
                      matcher = "^other$"

                      [[hooks.SessionEnd.hooks]]
                      type = "command"
                      commandWindows = "save-session"
                      additionalContextLimit = 250

                      [[hooks.SessionEnd.hooks]]
                      type = "command"
                      commandWindows = "cleanup-session"
                      timeout = 99
                      async = true
                      """
                    : """
                      {
                        "hooks": {
                          "SessionEnd": [{
                            "matcher": "^other$",
                            "hooks": [{
                              "type": "command",
                              "commandWindows": "save-session",
                              "additionalContextLimit": 250
                            }, {
                              "type": "command",
                              "commandWindows": "cleanup-session",
                              "timeout": 99,
                              "async": true
                            }]
                          }]
                        }
                      }
                      """);

            var options = CopilotProjectInstructionDiscoveryConfig.Load(codexHome);

            Assert.Equal(2, options.ConfiguredCommandHooks.Count);
            Assert.All(options.ConfiguredCommandHooks, hook =>
            {
                Assert.Equal(CopilotCodexConfiguredHookEvent.SessionEnd, hook.Event);
                Assert.Equal("^other$", hook.ToolNamePattern);
                Assert.Equal(CopilotToolExecutionHookMode.Sync, hook.ExecutionMode);
                Assert.True(hook.IsStructurallyValid());
            });
            Assert.Equal(1, options.ConfiguredCommandHooks[0].TimeoutSeconds);
            Assert.Equal(3, options.ConfiguredCommandHooks[1].TimeoutSeconds);
            Assert.Contains(options.ConfiguredHookIssues, issue =>
                issue.Message.Contains("additionalContextLimit", StringComparison.Ordinal)
                && issue.Message.Contains("SessionEnd", StringComparison.Ordinal));
            Assert.Contains(options.ConfiguredHookIssues, issue =>
                issue.Message.Contains("clamped", StringComparison.Ordinal));
            Assert.Contains(options.ConfiguredHookIssues, issue =>
                issue.Message.Contains("synchronously", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(codexHome, recursive: true);
        }
    }

    [Fact]
    public async Task InputMatchesCodexAndSuccessfulOutputIsAdvisory()
    {
        var workspace = CreateTemporaryDirectory();
        try
        {
            var runner = new RecordingRunner(_ => new CopilotCodexCommandHookProcessResult(
                0,
                false,
                """{"continue":false,"stopReason":"ignored","systemMessage":"ignored"}""",
                "ignored stderr"));
            var outcome = await new CopilotCodexSessionEndHookExecutor(runner).RunAsync(
                CreateRequest(workspace, CreateDefinition(workspace, "end", 0, "^other$")),
                onDiagnostic: null,
                CancellationToken.None);

            Assert.False(outcome.WasAlreadyEnded);
            Assert.Equal(1, outcome.MatchedHookCount);
            Assert.Equal(0, outcome.FailedHookCount);
            using var input = JsonDocument.Parse(Assert.Single(runner.Calls).StandardInput);
            Assert.Equal("session-1", input.RootElement.GetProperty("session_id").GetString());
            Assert.Equal(JsonValueKind.Null, input.RootElement.GetProperty("transcript_path").ValueKind);
            Assert.Equal(workspace, input.RootElement.GetProperty("cwd").GetString(), ignoreCase: true);
            Assert.Equal("SessionEnd", input.RootElement.GetProperty("hook_event_name").GetString());
            Assert.Equal("other", input.RootElement.GetProperty("reason").GetString());
            Assert.False(input.RootElement.TryGetProperty("turn_id", out _));
            Assert.False(input.RootElement.TryGetProperty("model", out _));
            Assert.False(input.RootElement.TryGetProperty("permission_mode", out _));
            Assert.Equal(5, input.RootElement.EnumerateObject().Count());
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task MatcherRunsHooksConcurrentlyAndReportsFailuresWithoutBlockingEnd()
    {
        var workspace = CreateTemporaryDirectory();
        try
        {
            var runner = new ConcurrentRunner(expectedCalls: 2);
            var diagnostics = new List<string>();
            var runTask = new CopilotCodexSessionEndHookExecutor(runner).RunAsync(
                CreateRequest(
                    workspace,
                    CreateDefinition(workspace, "completed", 0, "^other$"),
                    CreateDefinition(workspace, "wrong", 1, "^clear$"),
                    CreateDefinition(workspace, "failed", 2, "^other$")),
                diagnostics.Add,
                CancellationToken.None);

            await runner.AllStarted.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(runTask.IsCompleted);
            runner.Release();
            var outcome = await runTask;

            Assert.Equal(2, outcome.MatchedHookCount);
            Assert.Equal(1, outcome.FailedHookCount);
            Assert.Contains(diagnostics, item =>
                item.Contains("completed", StringComparison.Ordinal)
                && item.Contains("SessionEnd", StringComparison.Ordinal));
            Assert.Contains(diagnostics, item =>
                item.Contains("closing failed", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task LifecycleCoalescesConcurrentEndAndReopensExplicitly()
    {
        var workspace = CreateTemporaryDirectory();
        try
        {
            var runner = new CountingRunner();
            var lifecycle = new CopilotCodexSessionEndHookLifecycle(
                new CopilotCodexSessionEndHookExecutor(runner));
            var request = CreateRequest(
                workspace,
                CreateDefinition(workspace, "end", 0));

            var first = lifecycle.EndAsync(request, null, CancellationToken.None);
            var concurrent = lifecycle.EndAsync(request, null, CancellationToken.None);
            await runner.Started.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(1, runner.CallCount);
            runner.Release();

            var outcomes = await Task.WhenAll(first, concurrent);
            Assert.All(outcomes, outcome => Assert.False(outcome.WasAlreadyEnded));
            Assert.True((await lifecycle.EndAsync(request, null, CancellationToken.None)).WasAlreadyEnded);
            Assert.Equal(1, runner.CallCount);

            lifecycle.Reopen(request.ConversationId);
            runner.ResetRelease();
            var reopened = lifecycle.EndAsync(request, null, CancellationToken.None);
            await runner.Started.WaitAsync(TimeSpan.FromSeconds(5));
            runner.Release();
            Assert.False((await reopened).WasAlreadyEnded);
            Assert.Equal(2, runner.CallCount);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task RuntimeEndClearsStartStateAndResumeReopensSession()
    {
        var workspace = CreateTemporaryDirectory();
        try
        {
            var runner = new RecordingRunner(call =>
                call.Definition.Event == CopilotCodexConfiguredHookEvent.SessionStart
                    ? new CopilotCodexCommandHookProcessResult(0, false, "restored context", string.Empty)
                    : new CopilotCodexCommandHookProcessResult(0, false, "ignored output", string.Empty));
            var startLifecycle = new CopilotCodexSessionStartHookLifecycle(
                new CopilotCodexSessionStartHookExecutor(runner));
            var endLifecycle = new CopilotCodexSessionEndHookLifecycle(
                new CopilotCodexSessionEndHookExecutor(runner));
            var runtime = new CopilotTurnRuntime(
                new CopilotChatService(),
                startLifecycle,
                endLifecycle);
            var request = CreateRequest(
                workspace,
                CreateDefinition(
                    workspace,
                    "start",
                    0,
                    "^resume$",
                    CopilotCodexConfiguredHookEvent.SessionStart),
                CreateDefinition(workspace, "end", 1));

            runtime.QueueSessionStart(
                request.ConversationId,
                CopilotCodexSessionStartSource.Resume);
            var firstStart = await runtime.RunSessionStartHooksAsync(
                request,
                hasPersistedHistory: false,
                null,
                CancellationToken.None);
            Assert.Equal(["restored context"], firstStart.AdditionalContexts);

            var firstEnd = await runtime.RunSessionEndHooksAsync(
                request,
                null,
                CancellationToken.None);
            Assert.False(firstEnd.WasAlreadyEnded);
            Assert.True((await runtime.RunSessionEndHooksAsync(
                request,
                null,
                CancellationToken.None)).WasAlreadyEnded);

            runtime.QueueSessionStart(
                request.ConversationId,
                CopilotCodexSessionStartSource.Resume);
            var resumed = await runtime.RunSessionStartHooksAsync(
                request,
                hasPersistedHistory: false,
                null,
                CancellationToken.None);
            Assert.Equal(["restored context"], resumed.AdditionalContexts);
            Assert.False((await runtime.RunSessionEndHooksAsync(
                request,
                null,
                CancellationToken.None)).WasAlreadyEnded);

            Assert.Equal(
                ["resume", "other", "resume", "other"],
                runner.Calls.Select(ReadLifecycleValue).ToArray());
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public void SessionEndDefinitionsMustStaySynchronousAndBounded()
    {
        var workspace = CreateTemporaryDirectory();
        try
        {
            Assert.False((CreateDefinition(workspace, "slow", 0) with
            {
                TimeoutSeconds = 4,
            }).IsStructurallyValid());
            Assert.False((CreateDefinition(workspace, "async", 0) with
            {
                ExecutionMode = CopilotToolExecutionHookMode.Async,
            }).IsStructurallyValid());
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    private static string ReadLifecycleValue(CommandCall call)
    {
        using var input = JsonDocument.Parse(call.StandardInput);
        return input.RootElement.TryGetProperty("source", out var source)
            ? source.GetString() ?? string.Empty
            : input.RootElement.GetProperty("reason").GetString() ?? string.Empty;
    }

    private static CopilotAgentRequest CreateRequest(
        string workspace,
        params CopilotCodexCommandHookDefinition[] definitions) => new()
        {
            ConversationId = "session-1",
            TaskId = "turn-1",
            WorkspacePath = workspace,
            UserText = "test prompt",
            TaskIntentText = "test prompt",
            Profile = new CopilotProfileConfig
            {
                Model = "test-model",
            },
            Mode = CopilotAgentMode.Code,
            CodexHooksEnabled = true,
            CodexCommandHooks = definitions,
        };

    private static CopilotCodexCommandHookDefinition CreateDefinition(
        string workspace,
        string command,
        int order,
        string matcher = "*",
        CopilotCodexConfiguredHookEvent hookEvent = CopilotCodexConfiguredHookEvent.SessionEnd) => new(
            $"codex-config:session-end:{order}",
            Path.Combine(workspace, "hooks.json"),
            CopilotProjectInstructionConfigSources.CodexHome,
            hookEvent,
            matcher,
            command,
            2,
            string.Empty,
            CopilotToolExecutionHookMode.Sync,
            order,
            new string(order % 2 == 0 ? 'c' : 'd', 64));

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"copilot-session-end-hook-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class RecordingRunner(
        Func<CommandCall, CopilotCodexCommandHookProcessResult> resultFactory)
        : ICopilotCodexCommandHookRunner
    {
        private readonly ConcurrentQueue<CommandCall> _calls = new();

        public IReadOnlyList<CommandCall> Calls => _calls.ToArray();

        public Task<CopilotCodexCommandHookProcessResult> RunAsync(
            CopilotCodexCommandHookDefinition definition,
            CopilotAgentRequest request,
            string standardInput,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var call = new CommandCall(definition, standardInput);
            _calls.Enqueue(call);
            return Task.FromResult(resultFactory(call));
        }
    }

    private sealed class ConcurrentRunner(int expectedCalls) : ICopilotCodexCommandHookRunner
    {
        private readonly TaskCompletionSource<bool> _allStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _started;

        public Task AllStarted => _allStarted.Task;

        public void Release() => _release.TrySetResult(true);

        public async Task<CopilotCodexCommandHookProcessResult> RunAsync(
            CopilotCodexCommandHookDefinition definition,
            CopilotAgentRequest request,
            string standardInput,
            CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _started) == expectedCalls)
                _allStarted.TrySetResult(true);
            await _release.Task.WaitAsync(cancellationToken);
            return string.Equals(definition.Command, "failed", StringComparison.Ordinal)
                ? new CopilotCodexCommandHookProcessResult(7, false, string.Empty, "closing failed")
                : new CopilotCodexCommandHookProcessResult(0, false, "ignored", string.Empty);
        }
    }

    private sealed class CountingRunner : ICopilotCodexCommandHookRunner
    {
        private TaskCompletionSource<bool> _started = NewCompletion();
        private TaskCompletionSource<bool> _release = NewCompletion();
        private int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);

        public Task Started => _started.Task;

        public void Release() => _release.TrySetResult(true);

        public void ResetRelease()
        {
            _started = NewCompletion();
            _release = NewCompletion();
        }

        public async Task<CopilotCodexCommandHookProcessResult> RunAsync(
            CopilotCodexCommandHookDefinition definition,
            CopilotAgentRequest request,
            string standardInput,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _callCount);
            _started.TrySetResult(true);
            await _release.Task.WaitAsync(cancellationToken);
            return new CopilotCodexCommandHookProcessResult(0, false, string.Empty, string.Empty);
        }

        private static TaskCompletionSource<bool> NewCompletion() =>
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed record CommandCall(
        CopilotCodexCommandHookDefinition Definition,
        string StandardInput);
}
