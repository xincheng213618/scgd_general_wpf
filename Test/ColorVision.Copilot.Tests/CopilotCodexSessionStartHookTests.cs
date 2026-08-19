using ColorVision.Copilot;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotCodexSessionStartHookTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void JsonAndInlineTomlLoadSessionStart(bool inlineToml)
    {
        var codexHome = CreateTemporaryDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(codexHome, inlineToml ? "config.toml" : "hooks.json"),
                inlineToml
                    ? """
                      [[hooks.SessionStart]]
                      matcher = "^(resume|compact)$"

                      [[hooks.SessionStart.hooks]]
                      type = "command"
                      commandWindows = "load-session-context"
                      additionalContextLimit = 375
                      """
                    : """
                      {
                        "hooks": {
                          "SessionStart": [{
                            "matcher": "^(resume|compact)$",
                            "hooks": [{
                              "type": "command",
                              "commandWindows": "load-session-context",
                              "additionalContextLimit": 375
                            }]
                          }]
                        }
                      }
                      """);

            var options = CopilotProjectInstructionDiscoveryConfig.Load(codexHome);

            var hook = Assert.Single(options.ConfiguredCommandHooks);
            Assert.Equal(CopilotCodexConfiguredHookEvent.SessionStart, hook.Event);
            Assert.Equal("^(resume|compact)$", hook.ToolNamePattern);
            Assert.Equal(375, hook.AdditionalContextLimitTokens);
            Assert.DoesNotContain(options.ConfiguredHookIssues, issue =>
                issue.Message.Contains("SessionStart", StringComparison.Ordinal)
                && issue.Message.Contains("ignores additionalContextLimit", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(codexHome, recursive: true);
        }
    }

    [Fact]
    public async Task InputMatchesCodexAndContinueFalsePreservesContext()
    {
        var workspace = CreateTemporaryDirectory();
        try
        {
            var definition = CreateDefinition(workspace, "start", 0, "^resume$");
            var runner = new RecordingRunner(_ => SuccessfulResult(
                """{"continue":false,"stopReason":"pause","systemMessage":"notice","hookSpecificOutput":{"hookEventName":"SessionStart","additionalContext":"reload conventions"}}"""));
            var request = CreateRequest(workspace, definition);

            var outcome = await new CopilotCodexSessionStartHookExecutor(runner).RunAsync(
                request,
                CopilotCodexSessionStartSource.Resume,
                onDiagnostic: null,
                CancellationToken.None);

            Assert.True(outcome.ShouldStop);
            Assert.Equal("pause", outcome.StopReason);
            Assert.Equal(["reload conventions"], outcome.AdditionalContexts);
            using var input = JsonDocument.Parse(Assert.Single(runner.Calls).StandardInput);
            Assert.Equal("session-1", input.RootElement.GetProperty("session_id").GetString());
            Assert.Equal(JsonValueKind.Null, input.RootElement.GetProperty("transcript_path").ValueKind);
            Assert.Equal(workspace, input.RootElement.GetProperty("cwd").GetString(), ignoreCase: true);
            Assert.Equal("SessionStart", input.RootElement.GetProperty("hook_event_name").GetString());
            Assert.Equal("test-model", input.RootElement.GetProperty("model").GetString());
            Assert.Equal("default", input.RootElement.GetProperty("permission_mode").GetString());
            Assert.Equal("resume", input.RootElement.GetProperty("source").GetString());
            Assert.False(input.RootElement.TryGetProperty("turn_id", out _));
            Assert.Equal(7, input.RootElement.EnumerateObject().Count());
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task MatcherRunsSyncHooksConcurrentlyAndAggregatesInDefinitionOrder()
    {
        var workspace = CreateTemporaryDirectory();
        try
        {
            var definitions = new[]
            {
                CreateDefinition(workspace, "first", 0, "^compact$"),
                CreateDefinition(workspace, "wrong", 1, "^startup$"),
                CreateDefinition(workspace, "second", 2, "^compact$"),
            };
            var runner = new ConcurrentRunner(expectedCalls: 2);
            var runTask = new CopilotCodexSessionStartHookExecutor(runner).RunAsync(
                CreateRequest(workspace, definitions),
                CopilotCodexSessionStartSource.Compact,
                onDiagnostic: null,
                CancellationToken.None);

            await runner.AllStarted.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(runTask.IsCompleted);
            runner.Release();
            var outcome = await runTask;

            Assert.Equal(["first context", "second context"], outcome.AdditionalContexts);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task FailedAndAsyncHooksCannotStopOrInjectContext()
    {
        var workspace = CreateTemporaryDirectory();
        try
        {
            var failed = CreateDefinition(workspace, "failed", 0);
            var asynchronous = CreateDefinition(workspace, "async", 1) with
            {
                ExecutionMode = CopilotToolExecutionHookMode.Async,
            };
            var runner = new RecordingRunner(call =>
                call.Definition.ExecutionMode == CopilotToolExecutionHookMode.Sync
                    ? new CopilotCodexCommandHookProcessResult(2, false, string.Empty, "failure")
                    : SuccessfulResult(
                        """{"continue":false,"hookSpecificOutput":{"hookEventName":"SessionStart","additionalContext":"must not inject"}}"""));
            var scheduler = new RecordingScheduler();
            var diagnostics = new List<string>();

            var outcome = await new CopilotCodexSessionStartHookExecutor(runner, scheduler).RunAsync(
                CreateRequest(workspace, failed, asynchronous),
                CopilotCodexSessionStartSource.Startup,
                diagnostics.Add,
                CancellationToken.None);

            Assert.False(outcome.ShouldStop);
            Assert.Empty(outcome.AdditionalContexts);
            Assert.Contains(diagnostics, item => item.Contains("failed open", StringComparison.Ordinal));
            Assert.Contains(diagnostics, item => item.Contains("async hook scheduled", StringComparison.Ordinal));
            await Assert.Single(scheduler.Callbacks)(CancellationToken.None);
            Assert.Equal(2, runner.Calls.Count);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task LifecycleRunsInitialSourceOnceAndPersistsContextsUntilClear()
    {
        var workspace = CreateTemporaryDirectory();
        try
        {
            var definition = CreateDefinition(workspace, "source-context", 0);
            var runner = new RecordingRunner(call =>
            {
                using var input = JsonDocument.Parse(call.StandardInput);
                var source = input.RootElement.GetProperty("source").GetString();
                return SuccessfulResult(source + " context");
            });
            var lifecycle = new CopilotCodexSessionStartHookLifecycle(
                new CopilotCodexSessionStartHookExecutor(runner));
            var request = CreateRequest(workspace, definition);

            var startup = await lifecycle.RunBeforeTurnAsync(
                request,
                hasPersistedHistory: false,
                onDiagnostic: null,
                CancellationToken.None);
            var repeated = await lifecycle.RunBeforeTurnAsync(
                request,
                hasPersistedHistory: true,
                onDiagnostic: null,
                CancellationToken.None);
            lifecycle.Queue(request.ConversationId, CopilotCodexSessionStartSource.Compact);
            var compact = await lifecycle.RunBeforeTurnAsync(
                request,
                hasPersistedHistory: true,
                onDiagnostic: null,
                CancellationToken.None);
            lifecycle.Queue(request.ConversationId, CopilotCodexSessionStartSource.Clear);
            var clear = await lifecycle.RunBeforeTurnAsync(
                request,
                hasPersistedHistory: false,
                onDiagnostic: null,
                CancellationToken.None);

            Assert.Equal(["startup context"], startup.AdditionalContexts);
            Assert.Equal(startup.AdditionalContexts, repeated.AdditionalContexts);
            Assert.Equal(["startup context", "compact context"], compact.AdditionalContexts);
            Assert.Equal(["clear context"], clear.AdditionalContexts);
            Assert.Equal(["startup", "compact", "clear"], ReadSources(runner.Calls));
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task StoppedSourceIsConsumedAndItsContextReachesLaterTurns()
    {
        var workspace = CreateTemporaryDirectory();
        try
        {
            var definition = CreateDefinition(workspace, "stop", 0);
            var runner = new RecordingRunner(_ => SuccessfulResult(
                """{"continue":false,"stopReason":"pause","hookSpecificOutput":{"hookEventName":"SessionStart","additionalContext":"retained"}}"""));
            var lifecycle = new CopilotCodexSessionStartHookLifecycle(
                new CopilotCodexSessionStartHookExecutor(runner));
            var request = CreateRequest(workspace, definition);

            var stopped = await lifecycle.RunBeforeTurnAsync(
                request,
                hasPersistedHistory: false,
                onDiagnostic: null,
                CancellationToken.None);
            var later = await lifecycle.RunBeforeTurnAsync(
                request,
                hasPersistedHistory: false,
                onDiagnostic: null,
                CancellationToken.None);

            Assert.True(stopped.ShouldStop);
            Assert.False(later.ShouldStop);
            Assert.Equal(["retained"], later.AdditionalContexts);
            Assert.Single(runner.Calls);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task QueuedInitialSourceOverridesHistoryHeuristic()
    {
        var workspace = CreateTemporaryDirectory();
        try
        {
            var definition = CreateDefinition(workspace, "source", 0);
            var runner = new RecordingRunner(_ => SuccessfulResult("{}"));
            var lifecycle = new CopilotCodexSessionStartHookLifecycle(
                new CopilotCodexSessionStartHookExecutor(runner));
            var resumed = CreateRequestForConversation(workspace, "restored-session", definition);
            var forked = CreateRequestForConversation(workspace, "forked-session", definition);
            lifecycle.Queue(resumed.ConversationId, CopilotCodexSessionStartSource.Resume);
            lifecycle.Queue(forked.ConversationId, CopilotCodexSessionStartSource.Startup);

            await lifecycle.RunBeforeTurnAsync(
                resumed,
                hasPersistedHistory: false,
                onDiagnostic: null,
                CancellationToken.None);
            await lifecycle.RunBeforeTurnAsync(
                forked,
                hasPersistedHistory: true,
                onDiagnostic: null,
                CancellationToken.None);

            Assert.Equal(["resume", "startup"], ReadSources(runner.Calls));
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public void DeveloperContextAndBlockedErrorStayBoundedAndDistinct()
    {
        var context = CopilotCodexSessionStartHookExecutor.BuildDeveloperContext(
            [new string('x', CopilotProjectInstructionDiscoveryConfig.MaximumDeveloperInstructionCharacters * 2)]);
        var error = CopilotTurnError.FromException(
            new CopilotSessionStartHookBlockedException(new string('y', 4_096)));

        Assert.Contains("# SessionStart hook context", context, StringComparison.Ordinal);
        Assert.Contains("aggregate context truncated", context, StringComparison.Ordinal);
        Assert.True(CopilotTokenEstimator.EstimateTextWeight(context)
            <= CopilotProjectInstructionDiscoveryConfig.MaximumDeveloperInstructionCharacters);
        Assert.Equal("session_start_hook_stopped", error.Code);
        Assert.True(error.IsStructurallyValid());
    }

    [Fact]
    public async Task ChatRuntimeStopsBeforeProviderRequest()
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
                    "SessionStart": [{
                      "matcher": "^startup$",
                      "hooks": [{
                        "type": "command",
                        "commandWindows": "inspect-session"
                      }]
                    }]
                  }
                }
                """);
            using var handler = new CountingHandler();
            using var httpClient = new HttpClient(handler);
            var runner = new RecordingRunner(_ => SuccessfulResult(
                """{"continue":false,"stopReason":"initialize later"}"""));
            var runtime = new CopilotTurnRuntime(
                new CopilotChatService(httpClient),
                new CopilotCodexSessionStartHookLifecycle(
                    new CopilotCodexSessionStartHookExecutor(runner)));
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
                conversationId: "session-runtime",
                taskId: "turn-runtime");
            var events = new List<CopilotTurnEvent>();

            var exception = await Assert.ThrowsAsync<CopilotSessionStartHookBlockedException>(
                async () =>
                {
                    await foreach (var turnEvent in runtime.RunAsync(request, CancellationToken.None))
                        events.Add(turnEvent);
                });

            Assert.Equal("initialize later", exception.Message);
            Assert.Equal(0, handler.CallCount);
            Assert.DoesNotContain(events, item => item is CopilotTurnRequestPreparedEvent);
            var error = Assert.Single(events.OfType<CopilotTurnErrorEvent>()).Error;
            Assert.Equal("session_start_hook_stopped", error.Code);
            Assert.Equal("initialize later", error.Message);
            Assert.Single(runner.Calls);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public void SessionStartStaysInSnapshotButNotPerToolBindings()
    {
        var workspace = CreateTemporaryDirectory();
        try
        {
            var definition = CreateDefinition(workspace, "start", 0);

            Assert.Empty(CopilotCodexCommandHookFactory.Resolve([definition], "ReadLocalFile"));
            var snapshot = Assert.Single(
                CopilotCodexCommandHookFactory.CreateSnapshotEntries([definition]));
            Assert.Equal(definition.SourceId, snapshot.SourceId);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task AgentRuntimeCommitsStartStateBeforeRunningSessionStartHook()
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
                    "SessionStart": [{
                      "matcher": "^startup$",
                      "hooks": [{
                        "type": "command",
                        "commandWindows": "inspect-session"
                      }]
                    }]
                  }
                }
                """);
            using var handler = new CountingHandler();
            using var httpClient = new HttpClient(handler);
            var runner = new RecordingRunner(_ => SuccessfulResult(
                """{"continue":false,"stopReason":"initialize later"}"""));
            var runtime = new CopilotTurnRuntime(
                new CopilotChatService(httpClient),
                new CopilotCodexSessionStartHookLifecycle(
                    new CopilotCodexSessionStartHookExecutor(runner)));
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
            var request = new CopilotTurnRequest(
                profile,
                CopilotAgentMode.Auto,
                "test prompt",
                existingRequestContent: string.Empty,
                chatAttachmentContextCaptured: false,
                refreshExternalContext: true,
                new CopilotAgentHostContextSnapshot(
                    activeDocumentPath: null,
                    solutionDirectoryPath: workspace,
                    attachments: null,
                    liveContext: null,
                    conversationHistory: null,
                    additionalReadRootPaths: null,
                    globalInstructionRootPath: workspace),
                CopilotConversationHistoryWindow.ResolveLimits(32_000, 4_096),
                sessionCheckpoint: null,
                recovery: null,
                runControl: null,
                new CopilotAgentDefaultsConfig(),
                externalMcpServers: null,
                conversationId: "session-runtime-agent",
                taskId: "turn-runtime-agent");
            var events = new List<CopilotTurnEvent>();
            CopilotTurnStatePersistenceBarrierEvent? barrier = null;

            await using var enumerator = runtime
                .RunAsync(request, CancellationToken.None)
                .GetAsyncEnumerator();
            while (await enumerator.MoveNextAsync())
            {
                events.Add(enumerator.Current);
                if (enumerator.Current is CopilotTurnStatePersistenceBarrierEvent candidate)
                {
                    barrier = candidate;
                    break;
                }
            }

            Assert.NotNull(barrier);
            Assert.Empty(runner.Calls);
            Assert.Equal(0, handler.CallCount);
            barrier.TryCommit();

            var exception = await Assert.ThrowsAsync<CopilotSessionStartHookBlockedException>(
                async () =>
                {
                    while (await enumerator.MoveNextAsync())
                        events.Add(enumerator.Current);
                });

            Assert.Equal("initialize later", exception.Message);
            Assert.Single(runner.Calls);
            Assert.Equal(0, handler.CallCount);
            Assert.Contains(events, item => item is CopilotTurnErrorEvent);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task ChatRuntimeInjectsSessionContextIntoProviderRequest()
    {
        var workspace = CreateTemporaryDirectory();
        try
        {
            File.WriteAllText(Path.Combine(workspace, "config.toml"), "[features]\nhooks = true");
            File.WriteAllText(
                Path.Combine(workspace, "hooks.json"),
                """
                {"hooks":{"SessionStart":[{"hooks":[{"type":"command","commandWindows":"load-context"}]}]}}
                """);
            using var handler = new RecordingChatHandler("answer");
            using var httpClient = new HttpClient(handler);
            var runner = new RecordingRunner(_ => SuccessfulResult("read workspace conventions"));
            var runtime = new CopilotTurnRuntime(
                new CopilotChatService(httpClient),
                new CopilotCodexSessionStartHookLifecycle(
                    new CopilotCodexSessionStartHookExecutor(runner)));
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
            var request = new CopilotTurnRequest(
                profile,
                CopilotAgentMode.Chat,
                "test prompt",
                existingRequestContent: string.Empty,
                chatAttachmentContextCaptured: false,
                refreshExternalContext: true,
                new CopilotAgentHostContextSnapshot(
                    activeDocumentPath: null,
                    solutionDirectoryPath: workspace,
                    attachments: null,
                    liveContext: null,
                    conversationHistory: null,
                    additionalReadRootPaths: null,
                    globalInstructionRootPath: workspace),
                CopilotConversationHistoryWindow.ResolveLimits(32_000, 4_096),
                sessionCheckpoint: null,
                recovery: null,
                runControl: null,
                new CopilotAgentDefaultsConfig(),
                externalMcpServers: null,
                conversationId: "session-context",
                taskId: "turn-context");

            await foreach (var _ in runtime.RunAsync(request, CancellationToken.None))
            {
            }

            var payload = Assert.Single(handler.Payloads);
            Assert.Contains("# SessionStart hook context", payload, StringComparison.Ordinal);
            Assert.Contains("read workspace conventions", payload, StringComparison.Ordinal);
            Assert.Single(runner.Calls);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    private static string[] ReadSources(IReadOnlyList<CommandCall> calls) => calls
        .Select(call =>
        {
            using var input = JsonDocument.Parse(call.StandardInput);
            return input.RootElement.GetProperty("source").GetString() ?? string.Empty;
        })
        .ToArray();

    private static CopilotAgentRequest CreateRequest(
        string workspace,
        params CopilotCodexCommandHookDefinition[] definitions) =>
        CreateRequestForConversation(workspace, "session-1", definitions);

    private static CopilotAgentRequest CreateRequestForConversation(
        string workspace,
        string conversationId,
        params CopilotCodexCommandHookDefinition[] definitions) => new()
        {
            ConversationId = conversationId,
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
            CodexApprovalPolicy = CopilotCodexApprovalPolicy.CreateScalar(
                CopilotCodexApprovalPolicyMode.Untrusted),
        };

    private static CopilotCodexCommandHookDefinition CreateDefinition(
        string workspace,
        string command,
        int order,
        string matcher = "*") => new(
            $"codex-config:session-start:{order}",
            Path.Combine(workspace, "hooks.json"),
            CopilotProjectInstructionConfigSources.CodexHome,
            CopilotCodexConfiguredHookEvent.SessionStart,
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
            $"copilot-session-start-hook-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class RecordingRunner(
        Func<CommandCall, CopilotCodexCommandHookProcessResult> resultFactory)
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
            var pending = new CommandCall(definition, standardInput);
            Calls.Add(pending);
            return Task.FromResult(resultFactory(pending));
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
            return SuccessfulResult(definition.Command + " context");
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
            Assert.Equal("SessionStart", eventName);
            Callbacks.Add(callback);
            return true;
        }
    }

    private sealed class CountingHandler : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            throw new InvalidOperationException("The provider must not be called.");
        }
    }

    private sealed class RecordingChatHandler(params string[] responses) : HttpMessageHandler
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

    private sealed record CommandCall(
        CopilotCodexCommandHookDefinition Definition,
        string StandardInput);
}
