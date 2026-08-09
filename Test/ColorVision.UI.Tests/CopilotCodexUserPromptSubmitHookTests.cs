using ColorVision.Copilot;
using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;

namespace ColorVision.UI.Tests;

public sealed class CopilotCodexUserPromptSubmitHookTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void JsonAndInlineTomlLoadUserPromptSubmitWithoutMatcher(bool inlineToml)
    {
        var codexHome = CreateTemporaryDirectory();
        try
        {
            if (inlineToml)
            {
                File.WriteAllText(
                    Path.Combine(codexHome, "config.toml"),
                    """
                    [[hooks.UserPromptSubmit]]

                    [[hooks.UserPromptSubmit.hooks]]
                    type = "command"
                    commandWindows = "inspect-prompt"
                    additionalContextLimit = 125
                    """);
            }
            else
            {
                File.WriteAllText(
                    Path.Combine(codexHome, "hooks.json"),
                    """
                    {
                      "hooks": {
                        "UserPromptSubmit": [
                          {
                            "hooks": [
                              {
                                "type": "command",
                                "commandWindows": "inspect-prompt",
                                "additionalContextLimit": 125
                              }
                            ]
                          }
                        ]
                      }
                    }
                    """);
            }

            var options = CopilotProjectInstructionDiscoveryConfig.Load(codexHome);

            var hook = Assert.Single(options.ConfiguredCommandHooks);
            Assert.Equal(CopilotCodexConfiguredHookEvent.UserPromptSubmit, hook.Event);
            Assert.Equal("*", hook.ToolNamePattern);
            Assert.Equal(125, hook.AdditionalContextLimitTokens);
            Assert.Empty(options.ConfiguredHookIssues);
        }
        finally
        {
            Directory.Delete(codexHome, recursive: true);
        }
    }

    [Fact]
    public void AsyncUserPromptSubmitLoadsWithoutCompatibilityDiagnostic()
    {
        var codexHome = CreateTemporaryDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(codexHome, "hooks.json"),
                """
                {
                  "hooks": {
                    "UserPromptSubmit": [
                      {
                        "hooks": [
                          {
                            "type": "command",
                            "commandWindows": "observe-prompt",
                            "async": true
                          }
                        ]
                      }
                    ]
                  }
                }
                """);

            var options = CopilotProjectInstructionDiscoveryConfig.Load(codexHome);

            var hook = Assert.Single(options.ConfiguredCommandHooks);
            Assert.Equal(CopilotToolExecutionHookMode.Async, hook.ExecutionMode);
            Assert.Empty(options.ConfiguredHookIssues);
        }
        finally
        {
            Directory.Delete(codexHome, recursive: true);
        }
    }

    [Fact]
    public async Task MatcherIsIgnoredAndStructuredBlockPreservesRedactedWarning()
    {
        const string secret = "user-prompt-hook-secret";
        var workspace = CreateTemporaryDirectory();
        try
        {
            var definition = CreateDefinition(
                workspace,
                command: "prompt-policy",
                order: 0,
                matcher: "^NeverMatchesPrompt$");
            var runner = new RecordingRunner(_ => new CopilotCodexCommandHookProcessResult(
                0,
                false,
                JsonSerializer.Serialize(new
                {
                    systemMessage = "prompt warning; api_key=" + secret,
                    decision = "block",
                    reason = "prompt policy denied",
                    hookSpecificOutput = new
                    {
                        hookEventName = "UserPromptSubmit",
                        additionalContext = "not submitted",
                    },
                }),
                string.Empty));
            var diagnostics = new List<string>();

            var outcome = await new CopilotCodexUserPromptSubmitHookExecutor(runner).RunAsync(
                CreateRequest(workspace, definition),
                "  inspect this exact prompt  ",
                diagnostics.Add,
                CancellationToken.None);

            Assert.True(outcome.ShouldStop);
            Assert.Equal("prompt policy denied", outcome.StopReason);
            Assert.Equal(["not submitted"], outcome.AdditionalContexts);
            Assert.Contains(diagnostics, item =>
                item.Contains("api_key=<redacted>", StringComparison.Ordinal));
            Assert.DoesNotContain(diagnostics, item =>
                item.Contains(secret, StringComparison.Ordinal));
            Assert.Contains(diagnostics, item =>
                item.Contains("hook blocked", StringComparison.Ordinal));
            var call = Assert.Single(runner.Calls);
            using var input = JsonDocument.Parse(call.StandardInput);
            Assert.Equal("prompt-session", input.RootElement.GetProperty("session_id").GetString());
            Assert.Equal("prompt-turn", input.RootElement.GetProperty("turn_id").GetString());
            Assert.Equal("UserPromptSubmit", input.RootElement.GetProperty("hook_event_name").GetString());
            Assert.Equal("  inspect this exact prompt  ", input.RootElement.GetProperty("prompt").GetString());
            Assert.Equal("test-model", input.RootElement.GetProperty("model").GetString());
            Assert.Equal("default", input.RootElement.GetProperty("permission_mode").GetString());
            Assert.False(input.RootElement.TryGetProperty("tool_name", out _));
            Assert.False(input.RootElement.TryGetProperty("tool_input", out _));
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task SynchronousHandlersStartConcurrentlyAndAggregateInDefinitionOrder()
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
            var runTask = new CopilotCodexUserPromptSubmitHookExecutor(runner).RunAsync(
                CreateRequest(workspace, definitions),
                "run both policies",
                onDiagnostic: null,
                CancellationToken.None);

            await runner.AllStarted.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(runTask.IsCompleted);
            runner.Release();
            var outcome = await runTask;

            Assert.False(outcome.ShouldStop);
            Assert.Equal(["first context", "second context"], outcome.AdditionalContexts);
            Assert.Equal(2, runner.Calls.Count);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task UnknownStructuredOutputFailsClosedAfterReportingWarning()
    {
        var workspace = CreateTemporaryDirectory();
        try
        {
            var runner = new RecordingRunner(_ => new CopilotCodexCommandHookProcessResult(
                0,
                false,
                """{"systemMessage":"retain this warning","unexpectedField":true}""",
                string.Empty));
            var diagnostics = new List<string>();

            var outcome = await new CopilotCodexUserPromptSubmitHookExecutor(runner).RunAsync(
                CreateRequest(workspace, CreateDefinition(workspace, "invalid", 0)),
                "do not leak this prompt",
                diagnostics.Add,
                CancellationToken.None);

            Assert.True(outcome.ShouldStop);
            Assert.Contains("invalid universal output", outcome.StopReason, StringComparison.Ordinal);
            Assert.Contains(diagnostics, item =>
                item.Contains("retain this warning", StringComparison.Ordinal));
            Assert.Contains(diagnostics, item =>
                item.Contains("hook failed", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task InvalidDecisionTypeFailsClosed()
    {
        var workspace = CreateTemporaryDirectory();
        try
        {
            var runner = new RecordingRunner(_ => new CopilotCodexCommandHookProcessResult(
                0,
                false,
                """{"decision":true}""",
                string.Empty));

            var outcome = await new CopilotCodexUserPromptSubmitHookExecutor(runner).RunAsync(
                CreateRequest(workspace, CreateDefinition(workspace, "invalid-decision", 0)),
                "do not bypass the prompt policy",
                onDiagnostic: null,
                CancellationToken.None);

            Assert.True(outcome.ShouldStop);
            Assert.Contains("invalid universal output", outcome.StopReason, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Theory]
    [InlineData(2, "", "blocked by stderr policy")]
    [InlineData(0, "{\"continue\":false,\"stopReason\":\"blocked by common output\"}", "")]
    public async Task ExitTwoAndContinueFalseBlockThePrompt(
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

            var outcome = await new CopilotCodexUserPromptSubmitHookExecutor(runner).RunAsync(
                CreateRequest(workspace, CreateDefinition(workspace, "blocking", 0)),
                "blocked prompt",
                onDiagnostic: null,
                CancellationToken.None);

            Assert.True(outcome.ShouldStop);
            Assert.Contains("blocked by", outcome.StopReason, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task PlainTextContextIsRedactedAndBounded()
    {
        const string secret = "plain-context-secret";
        var workspace = CreateTemporaryDirectory();
        try
        {
            var definition = CreateDefinition(
                workspace,
                command: "plain",
                order: 0,
                additionalContextLimitTokens: 5);
            var runner = new RecordingRunner(_ => new CopilotCodexCommandHookProcessResult(
                0,
                false,
                "api_key=" + secret + "; abcdefghijklmnopqrstuvwxyz",
                string.Empty));

            var outcome = await new CopilotCodexUserPromptSubmitHookExecutor(runner).RunAsync(
                CreateRequest(workspace, definition),
                "plain context",
                onDiagnostic: null,
                CancellationToken.None);

            var context = Assert.Single(outcome.AdditionalContexts);
            Assert.DoesNotContain(secret, context, StringComparison.Ordinal);
            Assert.True(CopilotTokenEstimator.EstimateTextWeight(context)
                <= 5 * CopilotTokenEstimator.AsciiCharactersPerToken);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task AsyncHandlerSchedulesWithoutBlockingSubmittedPrompt()
    {
        var workspace = CreateTemporaryDirectory();
        try
        {
            var runner = new RecordingRunner(_ => new CopilotCodexCommandHookProcessResult(
                0,
                false,
                "async context",
                string.Empty));
            var scheduler = new RecordingScheduler();
            var diagnostics = new List<string>();
            var definition = CreateDefinition(workspace, "async", 0) with
            {
                ExecutionMode = CopilotToolExecutionHookMode.Async,
            };

            var outcome = await new CopilotCodexUserPromptSubmitHookExecutor(
                runner,
                scheduler).RunAsync(
                CreateRequest(workspace, definition),
                "async prompt",
                diagnostics.Add,
                CancellationToken.None);

            Assert.False(outcome.ShouldStop);
            Assert.Empty(runner.Calls);
            Assert.Contains(diagnostics, item =>
                item.Contains("async hook scheduled", StringComparison.Ordinal));
            var output = await Assert.Single(scheduler.Callbacks)(CancellationToken.None);
            Assert.Single(runner.Calls);
            Assert.Equal("async context", Assert.IsType<CopilotCodexAsyncHookOutput>(output).AdditionalContext);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public void HookContextUsesDedicatedAgentAndChatInstructionSurfaces()
    {
        var request = new CopilotAgentRequest
        {
            Mode = CopilotAgentMode.Code,
            UserText = "answer the prompt",
            UserPromptSubmitAdditionalContexts = ["check the reproduction first"],
        };

        var harness = CopilotMicrosoftAgentFrameworkRuntime.BuildHarnessInstructions(
            request,
            Array.Empty<ICopilotTool>(),
            CopilotAgentEnvironmentContext.Capture(request),
            taskLedgerEnabled: false,
            agentModeEnabled: false);
        var chatContext = CopilotCodexUserPromptSubmitHookExecutor.BuildDeveloperContext(
            ["check the reproduction first"]);

        Assert.Contains("# UserPromptSubmit hook context", harness, StringComparison.Ordinal);
        Assert.Contains("check the reproduction first", harness, StringComparison.Ordinal);
        Assert.Contains("# UserPromptSubmit hook context", chatContext, StringComparison.Ordinal);
        Assert.Contains("check the reproduction first", chatContext, StringComparison.Ordinal);
        Assert.Contains("never grants a tool", chatContext, StringComparison.Ordinal);
    }

    [Fact]
    public void AggregateHookContextIsBoundedBeforeItReachesEitherModelSurface()
    {
        var context = CopilotCodexUserPromptSubmitHookExecutor.BuildDeveloperContext(
            [new string('a', 40_000), new string('b', 40_000)]);

        Assert.Contains("aggregate context truncated", context, StringComparison.Ordinal);
        Assert.True(CopilotTokenEstimator.EstimateTextWeight(context)
            <= CopilotProjectInstructionDiscoveryConfig.MaximumDeveloperInstructionCharacters);
    }

    [Fact]
    public void TurnProtocolAcceptsPromptHookDiagnosticBeforeRequestPreparation()
    {
        var state = CopilotTurnEventState.Create(CopilotAgentMode.Chat, "prompt-hook-turn");
        state = CopilotTurnEventReducer.Reduce(
            state,
            new CopilotTurnStartedEvent("prompt-hook-turn", CopilotAgentMode.Chat));

        state = CopilotTurnEventReducer.Reduce(
            state,
            new CopilotTurnRuntimeDiagnosticEvent("UserPromptSubmit hook started"));

        Assert.True(state.Started);
        Assert.Null(state.TerminalStatus);
    }

    [Fact]
    public void BlockedPromptProducesBoundedStructurallyValidTurnError()
    {
        var error = CopilotTurnError.FromException(
            new CopilotUserPromptSubmitHookBlockedException(new string('x', 4_096)));

        Assert.Equal("user_prompt_hook_blocked", error.Code);
        Assert.True(error.IsStructurallyValid());
        Assert.True(error.Message.Length <= CopilotTurnError.MaximumMessageLength);
    }

    private static CopilotAgentRequest CreateRequest(
        string workspace,
        params CopilotCodexCommandHookDefinition[] definitions) => new()
        {
            ConversationId = "prompt-session",
            TaskId = "prompt-turn",
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
        string matcher = "*",
        int additionalContextLimitTokens = CopilotToolExecutionOutcome.DefaultAdditionalContextLimitTokens) =>
        new(
            $"codex-config:user-prompt:{order}",
            Path.Combine(workspace, "hooks.json"),
            CopilotProjectInstructionConfigSources.CodexHome,
            CopilotCodexConfiguredHookEvent.UserPromptSubmit,
            matcher,
            command,
            5,
            string.Empty,
            CopilotToolExecutionHookMode.Sync,
            order,
            new string(order % 2 == 0 ? 'a' : 'b', 64),
            additionalContextLimitTokens);

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"copilot-user-prompt-hook-{Guid.NewGuid():N}");
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

        public ConcurrentQueue<string> Calls { get; } = new();

        public Task AllStarted => _allStarted.Task;

        public void Release() => _release.TrySetResult(true);

        public async Task<CopilotCodexCommandHookProcessResult> RunAsync(
            CopilotCodexCommandHookDefinition definition,
            CopilotAgentRequest request,
            string standardInput,
            CancellationToken cancellationToken)
        {
            Calls.Enqueue(definition.Command);
            if (Interlocked.Increment(ref _started) == expectedCalls)
                _allStarted.TrySetResult(true);
            await _release.Task.WaitAsync(cancellationToken);
            return new CopilotCodexCommandHookProcessResult(
                0,
                false,
                definition.Command + " context",
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
            Assert.Equal("prompt-session", conversationId);
            Assert.Equal("UserPromptSubmit", eventName);
            Callbacks.Add(callback);
            return true;
        }
    }

    private sealed record CommandCall(
        CopilotCodexCommandHookDefinition Definition,
        string StandardInput);
}
