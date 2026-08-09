using ColorVision.Copilot;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace ColorVision.UI.Tests;

public sealed class CopilotCodexStopHookTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void JsonAndInlineTomlLoadStopAndReportIgnoredContextLimit(bool inlineToml)
    {
        var codexHome = CreateTemporaryDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(codexHome, inlineToml ? "config.toml" : "hooks.json"),
                inlineToml
                    ? """
                      [[hooks.Stop]]
                      matcher = "^ignored$"

                      [[hooks.Stop.hooks]]
                      type = "command"
                      commandWindows = "inspect-stop"
                      additionalContextLimit = 125
                      """
                    : """
                      {
                        "hooks": {
                          "Stop": [
                            {
                              "matcher": "^ignored$",
                              "hooks": [
                                {
                                  "type": "command",
                                  "commandWindows": "inspect-stop",
                                  "additionalContextLimit": 125
                                }
                              ]
                            }
                          ]
                        }
                      }
                      """);

            var options = CopilotProjectInstructionDiscoveryConfig.Load(codexHome);

            var hook = Assert.Single(options.ConfiguredCommandHooks);
            Assert.Equal(CopilotCodexConfiguredHookEvent.Stop, hook.Event);
            Assert.Equal("^ignored$", hook.ToolNamePattern);
            Assert.Contains(options.ConfiguredHookIssues, issue =>
                issue.Message.Contains("Stop", StringComparison.Ordinal)
                && issue.Message.Contains("ignores additionalContextLimit", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(codexHome, recursive: true);
        }
    }

    [Fact]
    public async Task MatcherIsIgnoredAndInputTracksRepeatedStopRuns()
    {
        var workspace = CreateTemporaryDirectory();
        try
        {
            var definition = CreateDefinition(workspace, "inspect", 0, matcher: "^never$");
            var runner = new RecordingRunner(_ => new CopilotCodexCommandHookProcessResult(
                0,
                false,
                """{"decision":"block","reason":"revise & verify"}""",
                string.Empty));
            var executor = new CopilotCodexStopHookExecutor(runner);
            var request = CreateRequest(workspace, definition);

            var first = await executor.RunAsync(
                request,
                stopHookActive: false,
                lastAssistantMessage: "first answer",
                onDiagnostic: null,
                CancellationToken.None);
            var repeated = await executor.RunAsync(
                request,
                stopHookActive: true,
                lastAssistantMessage: null,
                onDiagnostic: null,
                CancellationToken.None);

            Assert.True(first.ShouldContinue);
            Assert.False(first.ShouldStop);
            Assert.Contains("<hook_prompt", first.ContinuationPrompt, StringComparison.Ordinal);
            Assert.Contains("revise &amp; verify", first.ContinuationPrompt, StringComparison.Ordinal);
            Assert.True(repeated.ShouldContinue);
            Assert.Equal(2, runner.Calls.Count);
            using var firstInput = JsonDocument.Parse(runner.Calls[0].StandardInput);
            Assert.Equal("stop-session", firstInput.RootElement.GetProperty("session_id").GetString());
            Assert.Equal("stop-turn", firstInput.RootElement.GetProperty("turn_id").GetString());
            Assert.Equal("Stop", firstInput.RootElement.GetProperty("hook_event_name").GetString());
            Assert.False(firstInput.RootElement.GetProperty("stop_hook_active").GetBoolean());
            Assert.Equal("first answer", firstInput.RootElement.GetProperty("last_assistant_message").GetString());
            Assert.Equal("test-model", firstInput.RootElement.GetProperty("model").GetString());
            Assert.Equal("default", firstInput.RootElement.GetProperty("permission_mode").GetString());
            Assert.False(firstInput.RootElement.TryGetProperty("tool_name", out _));
            using var repeatedInput = JsonDocument.Parse(runner.Calls[1].StandardInput);
            Assert.True(repeatedInput.RootElement.GetProperty("stop_hook_active").GetBoolean());
            Assert.Equal(JsonValueKind.Null, repeatedInput.RootElement.GetProperty("last_assistant_message").ValueKind);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task SynchronousHandlersRunConcurrentlyAndAggregateInDefinitionOrder()
    {
        var workspace = CreateTemporaryDirectory();
        try
        {
            var definitions = new[]
            {
                CreateDefinition(workspace, "first", 0),
                CreateDefinition(workspace, "second", 1),
            };
            var runner = new ConcurrentRunner(expectedCalls: 2);
            var runTask = new CopilotCodexStopHookExecutor(runner).RunAsync(
                CreateRequest(workspace, definitions),
                stopHookActive: false,
                lastAssistantMessage: "answer",
                onDiagnostic: null,
                CancellationToken.None);

            await runner.AllStarted.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(runTask.IsCompleted);
            runner.Release();
            var outcome = await runTask;

            Assert.True(outcome.ShouldContinue);
            var firstIndex = outcome.ContinuationPrompt.IndexOf("first reason", StringComparison.Ordinal);
            var secondIndex = outcome.ContinuationPrompt.IndexOf("second reason", StringComparison.Ordinal);
            Assert.True(firstIndex >= 0);
            Assert.True(secondIndex > firstIndex);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task ContinueFalseWinsOverAllContinuationRequests()
    {
        var workspace = CreateTemporaryDirectory();
        try
        {
            var definitions = new[]
            {
                CreateDefinition(workspace, "continue", 0),
                CreateDefinition(workspace, "stop", 1),
            };
            var runner = new RecordingRunner(definition => new CopilotCodexCommandHookProcessResult(
                0,
                false,
                definition.Command == "stop"
                    ? """{"continue":false,"stopReason":"finalize now"}"""
                    : """{"decision":"block","reason":"keep working"}""",
                string.Empty));

            var outcome = await new CopilotCodexStopHookExecutor(runner).RunAsync(
                CreateRequest(workspace, definitions),
                stopHookActive: false,
                lastAssistantMessage: "answer",
                onDiagnostic: null,
                CancellationToken.None);

            Assert.True(outcome.ShouldStop);
            Assert.Equal("finalize now", outcome.StopReason);
            Assert.False(outcome.ShouldContinue);
            Assert.Empty(outcome.ContinuationPrompt);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task UnsupportedDecisionFailsOpenBeforeContinueFalseCanStop()
    {
        var workspace = CreateTemporaryDirectory();
        try
        {
            var runner = new RecordingRunner(_ => new CopilotCodexCommandHookProcessResult(
                0,
                false,
                """{"continue":false,"decision":"allow","stopReason":"must not stop"}""",
                string.Empty));
            var diagnostics = new List<string>();

            var outcome = await new CopilotCodexStopHookExecutor(runner).RunAsync(
                CreateRequest(workspace, CreateDefinition(workspace, "invalid-decision", 0)),
                stopHookActive: false,
                lastAssistantMessage: "answer",
                diagnostics.Add,
                CancellationToken.None);

            Assert.False(outcome.ShouldStop);
            Assert.False(outcome.ShouldContinue);
            Assert.Contains(diagnostics, item =>
                item.Contains("failed open", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Theory]
    [InlineData(0, "plain text is invalid", "")]
    [InlineData(7, "", "failed")]
    [InlineData(2, "", "")]
    public async Task InvalidOrFailedHandlersFailOpen(
        int exitCode,
        string standardOutput,
        string standardError)
    {
        var workspace = CreateTemporaryDirectory();
        try
        {
            var runner = new RecordingRunner(_ => new CopilotCodexCommandHookProcessResult(
                exitCode,
                false,
                standardOutput,
                standardError));
            var diagnostics = new List<string>();

            var outcome = await new CopilotCodexStopHookExecutor(runner).RunAsync(
                CreateRequest(workspace, CreateDefinition(workspace, "invalid", 0)),
                stopHookActive: false,
                lastAssistantMessage: "answer",
                diagnostics.Add,
                CancellationToken.None);

            Assert.False(outcome.ShouldStop);
            Assert.False(outcome.ShouldContinue);
            Assert.Contains(diagnostics, item =>
                item.Contains("failed open", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task ReasonWithoutDecisionIsIgnored()
    {
        var workspace = CreateTemporaryDirectory();
        try
        {
            var runner = new RecordingRunner(_ => new CopilotCodexCommandHookProcessResult(
                0,
                false,
                """{"reason":"unused feedback"}""",
                string.Empty));
            var diagnostics = new List<string>();

            var outcome = await new CopilotCodexStopHookExecutor(runner).RunAsync(
                CreateRequest(workspace, CreateDefinition(workspace, "reason-only", 0)),
                stopHookActive: false,
                lastAssistantMessage: "answer",
                diagnostics.Add,
                CancellationToken.None);

            Assert.False(outcome.ShouldStop);
            Assert.False(outcome.ShouldContinue);
            Assert.Contains(diagnostics, item =>
                item.Contains("hook completed", StringComparison.Ordinal));
            Assert.DoesNotContain(diagnostics, item =>
                item.Contains("failed open", StringComparison.Ordinal));
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
    public async Task AsyncOutputOnlyFailsWhenItLooksLikeInvalidJson(
        string standardOutput,
        bool expectedFailure)
    {
        var workspace = CreateTemporaryDirectory();
        try
        {
            var definition = CreateDefinition(workspace, "async-output", 0) with
            {
                ExecutionMode = CopilotToolExecutionHookMode.Async,
            };
            var runner = new RecordingRunner(_ => new CopilotCodexCommandHookProcessResult(
                0,
                false,
                standardOutput,
                string.Empty));

            var output = await new CopilotCodexCommandHook(definition, runner).OnStopAsync(
                CreateRequest(workspace, definition),
                stopHookActive: false,
                lastAssistantMessage: "answer",
                CancellationToken.None);

            Assert.NotNull(output);
            Assert.Equal(expectedFailure, output.HasFailure);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task AsyncHandlerIsScheduledWithoutApplyingControlOutput()
    {
        var workspace = CreateTemporaryDirectory();
        try
        {
            var definition = CreateDefinition(workspace, "async", 0) with
            {
                ExecutionMode = CopilotToolExecutionHookMode.Async,
            };
            var runner = new RecordingRunner(_ => new CopilotCodexCommandHookProcessResult(
                0,
                false,
                """{"decision":"block","reason":"must not control"}""",
                string.Empty));
            var scheduler = new RecordingScheduler();
            var diagnostics = new List<string>();

            var outcome = await new CopilotCodexStopHookExecutor(runner, scheduler).RunAsync(
                CreateRequest(workspace, definition),
                stopHookActive: true,
                lastAssistantMessage: "answer",
                diagnostics.Add,
                CancellationToken.None);

            Assert.False(outcome.ShouldContinue);
            Assert.Empty(runner.Calls);
            Assert.Contains(diagnostics, item =>
                item.Contains("async hook scheduled", StringComparison.Ordinal));
            await Assert.Single(scheduler.Callbacks)(CancellationToken.None);
            var call = Assert.Single(runner.Calls);
            using var input = JsonDocument.Parse(call.StandardInput);
            Assert.True(input.RootElement.GetProperty("stop_hook_active").GetBoolean());
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public void LifecycleHooksStayInSnapshotButNotPerToolBindings()
    {
        var workspace = CreateTemporaryDirectory();
        try
        {
            var stop = CreateDefinition(workspace, "stop", 0);
            var bindings = CopilotCodexCommandHookFactory.Resolve([stop], "ReadFile");
            var snapshots = CopilotCodexCommandHookFactory.CreateSnapshotEntries([stop]);

            Assert.Empty(bindings);
            Assert.Single(snapshots);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public void TurnProtocolAcceptsChatAnswerResetAfterRequestPreparation()
    {
        var state = CopilotTurnEventState.Create(CopilotAgentMode.Chat, "stop-turn");
        state = CopilotTurnEventReducer.Reduce(
            state,
            new CopilotTurnStartedEvent("stop-turn", CopilotAgentMode.Chat));
        state = CopilotTurnEventReducer.Reduce(
            state,
            new CopilotTurnRequestPreparedEvent(new CopilotPreparedTurnRequest("prompt", false)));

        state = CopilotTurnEventReducer.Reduce(state, new CopilotTurnChatAnswerResetEvent());

        Assert.True(state.Started);
        Assert.True(state.ChatRequestPrepared);
        Assert.Null(state.TerminalStatus);
    }

    [Fact]
    public async Task ChatRuntimeContinuesAfterStopAndPreservesTheCompletedAnswerInHistory()
    {
        var workspace = CreateTemporaryDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(workspace, "config.toml"),
                "[features]\nhooks = true");
            File.WriteAllText(
                Path.Combine(workspace, "hooks.json"),
                """
                {
                  "hooks": {
                    "Stop": [
                      {
                        "hooks": [
                          {
                            "type": "command",
                            "commandWindows": "inspect-stop"
                          }
                        ]
                      }
                    ]
                  }
                }
                """);
            using var handler = new SequentialChatHandler("first answer", "revised answer");
            using var httpClient = new HttpClient(handler);
            var chatService = new CopilotChatService(httpClient);
            var hookCallCount = 0;
            var runner = new RecordingRunner(_ =>
                new CopilotCodexCommandHookProcessResult(
                    0,
                    false,
                    Interlocked.Increment(ref hookCallCount) > 1
                        ? "{}"
                        : """{"decision":"block","reason":"revise once"}""",
                    string.Empty));
            var runtime = new CopilotTurnRuntime(
                chatService,
                new CopilotCodexStopHookExecutor(runner));
            var profile = new CopilotProfileConfig
            {
                VendorType = CopilotVendorType.Custom,
                ProviderType = CopilotProviderType.OpenAICompatible,
                ApiKey = "test-key",
                BaseUrl = "https://example.test/v1",
                Model = "test-model",
                MaxTokens = 4_096,
            };
            profile.UseSystemPromptOverride("Answer the test request.");
            var hostContext = new CopilotAgentHostContextSnapshot(
                activeDocumentPath: null,
                solutionDirectoryPath: workspace,
                attachments: null,
                liveContext: null,
                conversationHistory: null,
                additionalReadRootPaths: null,
                globalInstructionRootPath: workspace);
            var request = new CopilotTurnRequest(
                profile,
                CopilotAgentMode.Chat,
                "test prompt",
                existingRequestContent: string.Empty,
                chatAttachmentContextCaptured: false,
                refreshExternalContext: true,
                hostContext,
                CopilotConversationHistoryWindow.ResolveLimits(32_000, 4_096),
                sessionCheckpoint: null,
                recovery: null,
                runControl: null,
                new CopilotAgentDefaultsConfig(),
                externalMcpServers: null,
                conversationId: "stop-session",
                taskId: "stop-turn");
            var events = new List<CopilotTurnEvent>();

            await foreach (var turnEvent in runtime.RunAsync(request, CancellationToken.None))
                events.Add(turnEvent);

            Assert.Equal(2, handler.Payloads.Count);
            Assert.Equal(2, runner.Calls.Count);
            using var firstHookInput = JsonDocument.Parse(runner.Calls[0].StandardInput);
            using var repeatedHookInput = JsonDocument.Parse(runner.Calls[1].StandardInput);
            Assert.False(firstHookInput.RootElement.GetProperty("stop_hook_active").GetBoolean());
            Assert.True(repeatedHookInput.RootElement.GetProperty("stop_hook_active").GetBoolean());
            var firstDeltaIndex = events.FindIndex(item =>
                item is CopilotTurnChatDeltaEvent delta
                && delta.Delta.Content == "first answer");
            var resetIndex = events.FindIndex(item => item is CopilotTurnChatAnswerResetEvent);
            var revisedDeltaIndex = events.FindIndex(item =>
                item is CopilotTurnChatDeltaEvent delta
                && delta.Delta.Content == "revised answer");
            Assert.True(firstDeltaIndex >= 0);
            Assert.True(resetIndex > firstDeltaIndex);
            Assert.True(revisedDeltaIndex > resetIndex);
            using var secondPayload = JsonDocument.Parse(handler.Payloads[1]);
            var messages = secondPayload.RootElement.GetProperty("messages");
            var messageCount = messages.GetArrayLength();
            var previousAssistant = messages[messageCount - 2];
            var continuationUser = messages[messageCount - 1];
            Assert.Equal("assistant", previousAssistant.GetProperty("role").GetString());
            Assert.Equal("first answer", previousAssistant.GetProperty("content").GetString());
            Assert.Equal("user", continuationUser.GetProperty("role").GetString());
            Assert.Contains(
                "revise once",
                continuationUser.GetProperty("content").GetString(),
                StringComparison.Ordinal);
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
            ConversationId = "stop-session",
            TaskId = "stop-turn",
            WorkspacePath = workspace,
            Mode = CopilotAgentMode.Code,
            UserText = "test prompt",
            TaskIntentText = "test prompt",
            Profile = new CopilotProfileConfig
            {
                Model = "test-model",
            },
            CodexHooksEnabled = true,
            CodexCommandHooks = definitions,
            CodexApprovalPolicy = CopilotCodexApprovalPolicy.CreateScalar(
                CopilotCodexApprovalPolicyMode.Untrusted),
        };

    private static CopilotCodexCommandHookDefinition CreateDefinition(
        string workspace,
        string command,
        int order,
        string matcher = "*") => new(
            $"codex-config:stop:{order}",
            Path.Combine(workspace, "hooks.json"),
            CopilotProjectInstructionConfigSources.CodexHome,
            CopilotCodexConfiguredHookEvent.Stop,
            matcher,
            command,
            5,
            string.Empty,
            CopilotToolExecutionHookMode.Sync,
            order,
            new string(order % 2 == 0 ? 'a' : 'b', 64));

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"copilot-stop-hook-{Guid.NewGuid():N}");
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
            Calls.Add(new CommandCall(standardInput));
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
            return new CopilotCodexCommandHookProcessResult(
                0,
                false,
                JsonSerializer.Serialize(new
                {
                    decision = "block",
                    reason = definition.Command + " reason",
                }),
                string.Empty);
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

    private sealed class SequentialChatHandler(params string[] responses) : HttpMessageHandler
    {
        private int _index;

        public List<string> Payloads { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Payloads.Add(request.Content == null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken));
            var responseIndex = Interlocked.Increment(ref _index) - 1;
            Assert.InRange(responseIndex, 0, responses.Length - 1);
            var json = JsonSerializer.Serialize(new
            {
                choices = new[]
                {
                    new
                    {
                        message = new
                        {
                            role = "assistant",
                            content = responses[responseIndex],
                        },
                        finish_reason = "stop",
                    },
                },
            });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
        }
    }

    private sealed record CommandCall(string StandardInput);
}
