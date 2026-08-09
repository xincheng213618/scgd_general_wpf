using ColorVision.Copilot;
using System.IO;
using System.Text.Json;

namespace ColorVision.UI.Tests;

public sealed class CopilotCodexCompactHookTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void JsonAndInlineTomlLoadCompactEventsAndReportIgnoredContextLimit(bool inlineToml)
    {
        var codexHome = CreateTemporaryDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(codexHome, inlineToml ? "config.toml" : "hooks.json"),
                inlineToml
                    ? """
                      [[hooks.PreCompact]]
                      matcher = "^auto$"

                      [[hooks.PreCompact.hooks]]
                      type = "command"
                      commandWindows = "inspect-pre-compact"
                      additionalContextLimit = 125

                      [[hooks.PostCompact]]
                      matcher = "^manual$"

                      [[hooks.PostCompact.hooks]]
                      type = "command"
                      commandWindows = "inspect-post-compact"
                      async = true
                      """
                    : """
                      {
                        "hooks": {
                          "PreCompact": [
                            {
                              "matcher": "^auto$",
                              "hooks": [
                                {
                                  "type": "command",
                                  "commandWindows": "inspect-pre-compact",
                                  "additionalContextLimit": 125
                                }
                              ]
                            }
                          ],
                          "PostCompact": [
                            {
                              "matcher": "^manual$",
                              "hooks": [
                                {
                                  "type": "command",
                                  "commandWindows": "inspect-post-compact",
                                  "async": true
                                }
                              ]
                            }
                          ]
                        }
                      }
                      """);

            var options = CopilotProjectInstructionDiscoveryConfig.Load(codexHome);

            Assert.Collection(
                options.ConfiguredCommandHooks,
                hook =>
                {
                    Assert.Equal(CopilotCodexConfiguredHookEvent.PreCompact, hook.Event);
                    Assert.Equal("^auto$", hook.ToolNamePattern);
                    Assert.Equal(CopilotToolExecutionHookMode.Sync, hook.ExecutionMode);
                },
                hook =>
                {
                    Assert.Equal(CopilotCodexConfiguredHookEvent.PostCompact, hook.Event);
                    Assert.Equal("^manual$", hook.ToolNamePattern);
                    Assert.Equal(CopilotToolExecutionHookMode.Async, hook.ExecutionMode);
                });
            Assert.Contains(options.ConfiguredHookIssues, issue =>
                issue.Message.Contains("PreCompact", StringComparison.Ordinal)
                && issue.Message.Contains("ignores additionalContextLimit", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(codexHome, recursive: true);
        }
    }

    [Theory]
    [InlineData((int)CopilotCodexConfiguredHookEvent.PreCompact, "auto")]
    [InlineData((int)CopilotCodexConfiguredHookEvent.PostCompact, "manual")]
    public async Task CommandInputMatchesCodexCompactLifecycleSchema(
        int hookEventValue,
        string trigger)
    {
        var hookEvent = (CopilotCodexConfiguredHookEvent)hookEventValue;
        var workspace = CreateTemporaryDirectory();
        try
        {
            var runner = new RecordingRunner(_ => SuccessfulResult("{}"));
            var definition = CreateDefinition(workspace, hookEvent, "inspect", 0);
            var request = CreateRequest(workspace, definition);

            var output = await new CopilotCodexCommandHook(definition, runner).OnCompactAsync(
                request,
                hookEvent,
                trigger,
                CancellationToken.None);

            Assert.NotNull(output);
            using var input = JsonDocument.Parse(Assert.Single(runner.Calls).StandardInput);
            Assert.Equal("compact-session", input.RootElement.GetProperty("session_id").GetString());
            Assert.Equal("compact-turn", input.RootElement.GetProperty("turn_id").GetString());
            Assert.Equal(JsonValueKind.Null, input.RootElement.GetProperty("transcript_path").ValueKind);
            Assert.Equal(workspace, input.RootElement.GetProperty("cwd").GetString(), ignoreCase: true);
            Assert.Equal(hookEvent.ToString(), input.RootElement.GetProperty("hook_event_name").GetString());
            Assert.Equal("test-model", input.RootElement.GetProperty("model").GetString());
            Assert.Equal(trigger, input.RootElement.GetProperty("trigger").GetString());
            Assert.False(input.RootElement.TryGetProperty("permission_mode", out _));
            Assert.Equal(7, input.RootElement.EnumerateObject().Count());
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task TriggerMatcherSelectsOnlyTheCurrentCompactionSource()
    {
        var workspace = CreateTemporaryDirectory();
        try
        {
            var definitions = new[]
            {
                CreateDefinition(workspace, CopilotCodexConfiguredHookEvent.PreCompact, "auto", 0, "^auto$"),
                CreateDefinition(workspace, CopilotCodexConfiguredHookEvent.PreCompact, "manual", 1, "^manual$"),
            };
            var runner = new RecordingRunner(definition => SuccessfulResult(
                $$"""{"continue":false,"stopReason":"{{definition.Command}} stopped"}"""));

            var outcome = await new CopilotCodexCompactHookExecutor(runner).RunAsync(
                CreateRequest(workspace, definitions),
                CopilotCodexConfiguredHookEvent.PreCompact,
                CopilotCodexCompactHookTrigger.Auto,
                onDiagnostic: null,
                CancellationToken.None);

            Assert.True(outcome.ShouldStop);
            Assert.Equal("auto stopped", outcome.StopReason);
            Assert.Equal("auto", Assert.Single(runner.Calls).Definition.Command);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task SynchronousHandlersRunConcurrentlyAndAggregateInConfigurationOrder()
    {
        var workspace = CreateTemporaryDirectory();
        try
        {
            var definitions = new[]
            {
                CreateDefinition(workspace, CopilotCodexConfiguredHookEvent.PreCompact, "first", 0),
                CreateDefinition(workspace, CopilotCodexConfiguredHookEvent.PreCompact, "second", 1),
            };
            var runner = new ConcurrentRunner(expectedCalls: 2);
            var runTask = new CopilotCodexCompactHookExecutor(runner).RunAsync(
                CreateRequest(workspace, definitions),
                CopilotCodexConfiguredHookEvent.PreCompact,
                CopilotCodexCompactHookTrigger.Auto,
                onDiagnostic: null,
                CancellationToken.None);

            await runner.AllStarted.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(runTask.IsCompleted);
            runner.Release();
            var outcome = await runTask;

            Assert.True(outcome.ShouldStop);
            Assert.Equal("first reason", outcome.StopReason);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task PreCompactStopPreventsCompactionAndPostCompactExecution()
    {
        var workspace = CreateTemporaryDirectory();
        try
        {
            var definitions = new[]
            {
                CreateDefinition(workspace, CopilotCodexConfiguredHookEvent.PreCompact, "pre", 0),
                CreateDefinition(workspace, CopilotCodexConfiguredHookEvent.PostCompact, "post", 1),
            };
            var runner = new RecordingRunner(definition => SuccessfulResult(
                definition.Event == CopilotCodexConfiguredHookEvent.PreCompact
                    ? """{"continue":false,"stopReason":"policy denied compaction"}"""
                    : "{}"));
            var compactCalls = 0;
            var lifecycle = new CopilotCodexCompactionHookLifecycle(
                new CopilotCodexCompactHookExecutor(runner));

            var outcome = await lifecycle.RunAsync(
                CreateRequest(workspace, definitions),
                CopilotCodexCompactHookTrigger.Manual,
                _ =>
                {
                    compactCalls++;
                    return Task.FromResult(true);
                },
                onDiagnostic: null,
                CancellationToken.None);

            Assert.False(outcome.CompactionApplied);
            Assert.True(outcome.PreCompact.ShouldStop);
            Assert.False(outcome.PostCompact.ShouldStop);
            Assert.Equal(0, compactCalls);
            Assert.Equal(CopilotCodexConfiguredHookEvent.PreCompact, Assert.Single(runner.Calls).Definition.Event);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task PostCompactStopOccursAfterCompactionAndStopsAutomaticContinuation()
    {
        var workspace = CreateTemporaryDirectory();
        try
        {
            var definitions = new[]
            {
                CreateDefinition(workspace, CopilotCodexConfiguredHookEvent.PreCompact, "pre", 0, "^auto$"),
                CreateDefinition(workspace, CopilotCodexConfiguredHookEvent.PostCompact, "post", 1, "^auto$"),
            };
            var compactApplied = false;
            var runner = new RecordingRunner(definition =>
            {
                if (definition.Event == CopilotCodexConfiguredHookEvent.PostCompact)
                    Assert.True(compactApplied);
                return SuccessfulResult(definition.Event == CopilotCodexConfiguredHookEvent.PostCompact
                    ? """{"continue":false,"stopReason":"pause before original prompt"}"""
                    : "{}");
            });
            var lifecycle = new CopilotCodexCompactionHookLifecycle(
                new CopilotCodexCompactHookExecutor(runner));

            var outcome = await lifecycle.RunAsync(
                CreateRequest(workspace, definitions),
                CopilotCodexCompactHookTrigger.Auto,
                _ =>
                {
                    compactApplied = true;
                    return Task.FromResult(true);
                },
                onDiagnostic: null,
                CancellationToken.None);

            Assert.True(outcome.CompactionApplied);
            Assert.False(outcome.PreCompact.ShouldStop);
            Assert.True(outcome.PostCompact.ShouldStop);
            Assert.Equal("pause before original prompt", outcome.PostCompact.StopReason);
            Assert.Equal(
                [CopilotCodexConfiguredHookEvent.PreCompact, CopilotCodexConfiguredHookEvent.PostCompact],
                runner.Calls.Select(call => call.Definition.Event));
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Theory]
    [InlineData("plain output", false)]
    [InlineData("{malformed", true)]
    [InlineData("[]", true)]
    [InlineData("{\"decision\":\"block\",\"reason\":\"unsupported\"}", true)]
    public async Task PlainOutputIsIgnoredButJsonLikeInvalidOutputFailsOpen(
        string standardOutput,
        bool expectedFailure)
    {
        var workspace = CreateTemporaryDirectory();
        try
        {
            var definition = CreateDefinition(
                workspace,
                CopilotCodexConfiguredHookEvent.PostCompact,
                "inspect",
                0);
            var runner = new RecordingRunner(_ => SuccessfulResult(standardOutput));
            var diagnostics = new List<string>();

            var outcome = await new CopilotCodexCompactHookExecutor(runner).RunAsync(
                CreateRequest(workspace, definition),
                CopilotCodexConfiguredHookEvent.PostCompact,
                CopilotCodexCompactHookTrigger.Manual,
                diagnostics.Add,
                CancellationToken.None);

            Assert.False(outcome.ShouldStop);
            Assert.Equal(
                expectedFailure,
                diagnostics.Any(item => item.Contains("failed open", StringComparison.Ordinal)));
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(7)]
    public async Task NonzeroExitFailsOpen(int exitCode)
    {
        var workspace = CreateTemporaryDirectory();
        try
        {
            var definition = CreateDefinition(
                workspace,
                CopilotCodexConfiguredHookEvent.PreCompact,
                "failed",
                0);
            var runner = new RecordingRunner(_ => new CopilotCodexCommandHookProcessResult(
                exitCode,
                false,
                string.Empty,
                "failed"));
            var diagnostics = new List<string>();

            var outcome = await new CopilotCodexCompactHookExecutor(runner).RunAsync(
                CreateRequest(workspace, definition),
                CopilotCodexConfiguredHookEvent.PreCompact,
                CopilotCodexCompactHookTrigger.Manual,
                diagnostics.Add,
                CancellationToken.None);

            Assert.False(outcome.ShouldStop);
            Assert.Contains(diagnostics, item => item.Contains("failed open", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task AsyncHookIsScheduledWithoutApplyingControlOutput()
    {
        var workspace = CreateTemporaryDirectory();
        try
        {
            var definition = CreateDefinition(
                workspace,
                CopilotCodexConfiguredHookEvent.PostCompact,
                "async",
                0) with
            {
                ExecutionMode = CopilotToolExecutionHookMode.Async,
            };
            var runner = new RecordingRunner(_ => SuccessfulResult(
                """{"continue":false,"stopReason":"must not control"}"""));
            var scheduler = new RecordingScheduler();
            var diagnostics = new List<string>();

            var outcome = await new CopilotCodexCompactHookExecutor(runner, scheduler).RunAsync(
                CreateRequest(workspace, definition),
                CopilotCodexConfiguredHookEvent.PostCompact,
                CopilotCodexCompactHookTrigger.Manual,
                diagnostics.Add,
                CancellationToken.None);

            Assert.False(outcome.ShouldStop);
            Assert.Empty(runner.Calls);
            Assert.Contains(diagnostics, item => item.Contains("async hook scheduled", StringComparison.Ordinal));
            await Assert.Single(scheduler.Callbacks)(CancellationToken.None);
            Assert.Single(runner.Calls);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public void CompactHooksStayInCheckpointSnapshotButNotPerToolBindings()
    {
        var workspace = CreateTemporaryDirectory();
        try
        {
            var definitions = new[]
            {
                CreateDefinition(workspace, CopilotCodexConfiguredHookEvent.PreCompact, "pre", 0),
                CreateDefinition(workspace, CopilotCodexConfiguredHookEvent.PostCompact, "post", 1),
            };

            Assert.Empty(CopilotCodexCommandHookFactory.Resolve(definitions, "ReadFile"));
            Assert.Equal(2, CopilotCodexCommandHookFactory.CreateSnapshotEntries(definitions).Count);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    private static CopilotAgentRequest CreateRequest(
        string workspace,
        params CopilotCodexCommandHookDefinition[] definitions) => new()
        {
            ConversationId = "compact-session",
            TaskId = "compact-turn",
            WorkspacePath = workspace,
            Mode = CopilotAgentMode.Chat,
            Profile = new CopilotProfileConfig
            {
                Model = "test-model",
            },
            CodexHooksEnabled = true,
            CodexCommandHooks = definitions,
        };

    private static CopilotCodexCommandHookDefinition CreateDefinition(
        string workspace,
        CopilotCodexConfiguredHookEvent hookEvent,
        string command,
        int order,
        string matcher = "*") => new(
            $"codex-config:compact:{order}",
            Path.Combine(workspace, "hooks.json"),
            CopilotProjectInstructionConfigSources.CodexHome,
            hookEvent,
            matcher,
            command,
            5,
            string.Empty,
            CopilotToolExecutionHookMode.Sync,
            order,
            new string(order % 2 == 0 ? 'a' : 'b', 64));

    private static CopilotCodexCommandHookProcessResult SuccessfulResult(string output) =>
        new(0, false, output, string.Empty);

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"copilot-compact-hook-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class RecordingRunner(
        Func<CopilotCodexCommandHookDefinition, CopilotCodexCommandHookProcessResult> resultFactory)
        : ICopilotCodexCommandHookRunner
    {
        public List<CommandCall> Calls { get; } = [];

        public Task<CopilotCodexCommandHookProcessResult> RunAsync(
            CopilotCodexCommandHookDefinition definition,
            CopilotAgentRequest request,
            string standardInput,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls.Add(new CommandCall(definition, standardInput));
            return Task.FromResult(resultFactory(definition));
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
            return SuccessfulResult(JsonSerializer.Serialize(new
            {
                @continue = false,
                stopReason = definition.Command + " reason",
            }));
        }
    }

    private sealed class RecordingScheduler : ICopilotCodexLifecycleHookBackgroundScheduler
    {
        public List<Func<CancellationToken, Task<CopilotCodexAsyncHookOutput?>>> Callbacks { get; } = [];

        public bool TrySchedule(
            string conversationId,
            string sourceId,
            string eventName,
            string turnId,
            TimeSpan timeout,
            Func<CancellationToken, Task<CopilotCodexAsyncHookOutput?>> callback)
        {
            Callbacks.Add(callback);
            return true;
        }
    }

    private sealed record CommandCall(
        CopilotCodexCommandHookDefinition Definition,
        string StandardInput);
}
