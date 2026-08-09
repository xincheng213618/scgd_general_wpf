using ColorVision.Copilot;
using Microsoft.Extensions.AI;
using System.IO;
using System.Text.Json;

namespace ColorVision.UI.Tests;

public sealed class CopilotCodexSubagentHookTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void JsonAndInlineTomlLoadSubagentLifecycleEvents(bool inlineToml)
    {
        var codexHome = CreateTemporaryDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(codexHome, inlineToml ? "config.toml" : "hooks.json"),
                inlineToml
                    ? """
                      [[hooks.SubagentStart]]
                      matcher = "^explore$"

                      [[hooks.SubagentStart.hooks]]
                      type = "command"
                      commandWindows = "inspect-start"
                      additionalContextLimit = 125

                      [[hooks.SubagentStop]]
                      matcher = "^explore$"

                      [[hooks.SubagentStop.hooks]]
                      type = "command"
                      commandWindows = "inspect-stop"
                      additionalContextLimit = 250
                      """
                    : """
                      {
                        "hooks": {
                          "SubagentStart": [{
                            "matcher": "^explore$",
                            "hooks": [{
                              "type": "command",
                              "commandWindows": "inspect-start",
                              "additionalContextLimit": 125
                            }]
                          }],
                          "SubagentStop": [{
                            "matcher": "^explore$",
                            "hooks": [{
                              "type": "command",
                              "commandWindows": "inspect-stop",
                              "additionalContextLimit": 250
                            }]
                          }]
                        }
                      }
                      """);

            var options = CopilotProjectInstructionDiscoveryConfig.Load(codexHome);

            Assert.Collection(
                options.ConfiguredCommandHooks,
                hook =>
                {
                    Assert.Equal(CopilotCodexConfiguredHookEvent.SubagentStart, hook.Event);
                    Assert.Equal("^explore$", hook.ToolNamePattern);
                    Assert.Equal(125, hook.AdditionalContextLimitTokens);
                },
                hook =>
                {
                    Assert.Equal(CopilotCodexConfiguredHookEvent.SubagentStop, hook.Event);
                    Assert.Equal("^explore$", hook.ToolNamePattern);
                });
            Assert.Contains(options.ConfiguredHookIssues, issue =>
                issue.Message.Contains("SubagentStop", StringComparison.Ordinal)
                && issue.Message.Contains("ignores additionalContextLimit", StringComparison.Ordinal));
            Assert.DoesNotContain(options.ConfiguredHookIssues, issue =>
                issue.Message.Contains("SubagentStart", StringComparison.Ordinal)
                && issue.Message.Contains("ignores additionalContextLimit", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(codexHome, recursive: true);
        }
    }

    [Fact]
    public async Task SubagentStartInputMatchesCodexAndContinueFalseStillAddsContext()
    {
        var workspace = CreateTemporaryDirectory();
        try
        {
            var definition = CreateDefinition(
                workspace,
                CopilotCodexConfiguredHookEvent.SubagentStart,
                "start",
                0);
            var runner = new RecordingRunner(_ => SuccessfulResult(
                """{"continue":false,"stopReason":"ignored","systemMessage":"notice","hookSpecificOutput":{"hookEventName":"SubagentStart","additionalContext":"read conventions first"}}"""));
            var request = CreateRequest(workspace, definition);

            var output = await new CopilotCodexCommandHook(definition, runner)
                .OnSubagentStartAsync(
                    request,
                    request.CodexSubagentHookContext!,
                    CancellationToken.None);

            Assert.NotNull(output);
            Assert.False(output.HasFailure);
            Assert.Equal("notice", output.SystemMessage);
            Assert.Equal("read conventions first", output.AdditionalContext);
            using var input = JsonDocument.Parse(Assert.Single(runner.Calls).StandardInput);
            Assert.Equal("subagent-session", input.RootElement.GetProperty("session_id").GetString());
            Assert.Equal("run-1", input.RootElement.GetProperty("turn_id").GetString());
            Assert.Equal(JsonValueKind.Null, input.RootElement.GetProperty("transcript_path").ValueKind);
            Assert.Equal(workspace, input.RootElement.GetProperty("cwd").GetString(), ignoreCase: true);
            Assert.Equal("SubagentStart", input.RootElement.GetProperty("hook_event_name").GetString());
            Assert.Equal("test-model", input.RootElement.GetProperty("model").GetString());
            Assert.Equal("default", input.RootElement.GetProperty("permission_mode").GetString());
            Assert.Equal("run-1", input.RootElement.GetProperty("agent_id").GetString());
            Assert.Equal("explore", input.RootElement.GetProperty("agent_type").GetString());
            Assert.Equal(9, input.RootElement.EnumerateObject().Count());
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task SubagentStartMatcherSelectsAgentTypeAndAggregatesInDefinitionOrder()
    {
        var workspace = CreateTemporaryDirectory();
        try
        {
            var definitions = new[]
            {
                CreateDefinition(workspace, CopilotCodexConfiguredHookEvent.SubagentStart, "first", 0, "^explore$"),
                CreateDefinition(workspace, CopilotCodexConfiguredHookEvent.SubagentStart, "other", 1, "^scout$"),
                CreateDefinition(workspace, CopilotCodexConfiguredHookEvent.SubagentStart, "second", 2, "^explore$"),
            };
            var runner = new ConcurrentRunner(expectedCalls: 2);
            var runTask = new CopilotCodexSubagentStartHookExecutor(runner).RunAsync(
                CreateRequest(workspace, definitions),
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
    public async Task FailedAndAsyncSubagentStartHooksCannotBlockOrInjectContext()
    {
        var workspace = CreateTemporaryDirectory();
        try
        {
            var failed = CreateDefinition(
                workspace,
                CopilotCodexConfiguredHookEvent.SubagentStart,
                "failed",
                0);
            var asynchronous = CreateDefinition(
                workspace,
                CopilotCodexConfiguredHookEvent.SubagentStart,
                "async",
                1) with
            {
                ExecutionMode = CopilotToolExecutionHookMode.Async,
            };
            var runner = new RecordingRunner(definition =>
                definition.ExecutionMode == CopilotToolExecutionHookMode.Sync
                ? new CopilotCodexCommandHookProcessResult(2, false, string.Empty, "must not block")
                : SuccessfulResult("must not inject"));
            var scheduler = new RecordingScheduler();
            var diagnostics = new List<string>();

            var outcome = await new CopilotCodexSubagentStartHookExecutor(runner, scheduler).RunAsync(
                CreateRequest(workspace, failed, asynchronous),
                diagnostics.Add,
                CancellationToken.None);

            Assert.Empty(outcome.AdditionalContexts);
            Assert.Contains(runner.Calls, call => call.Result.ExitCode == 2);
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
    public async Task SubagentStopUsesAgentMatcherAndNeverRunsRootStop()
    {
        var workspace = CreateTemporaryDirectory();
        try
        {
            var definitions = new[]
            {
                CreateDefinition(workspace, CopilotCodexConfiguredHookEvent.Stop, "root-stop", 0),
                CreateDefinition(workspace, CopilotCodexConfiguredHookEvent.SubagentStop, "wrong-agent", 1, "^scout$"),
                CreateDefinition(workspace, CopilotCodexConfiguredHookEvent.SubagentStop, "child-stop", 2, "^explore$"),
            };
            var runner = new RecordingRunner(_ => SuccessfulResult(
                """{"decision":"block","reason":"one more pass"}"""));

            var outcome = await new CopilotCodexStopHookExecutor(runner).RunAsync(
                CreateRequest(workspace, definitions),
                stopHookActive: false,
                lastAssistantMessage: "child answer",
                onDiagnostic: null,
                CancellationToken.None);

            Assert.True(outcome.ShouldContinue);
            var call = Assert.Single(runner.Calls);
            Assert.Equal("child-stop", call.Definition.Command);
            using var input = JsonDocument.Parse(call.StandardInput);
            Assert.Equal("SubagentStop", input.RootElement.GetProperty("hook_event_name").GetString());
            Assert.Equal("run-1", input.RootElement.GetProperty("turn_id").GetString());
            Assert.Equal("run-1", input.RootElement.GetProperty("agent_id").GetString());
            Assert.Equal("explore", input.RootElement.GetProperty("agent_type").GetString());
            Assert.Equal(JsonValueKind.Null, input.RootElement.GetProperty("agent_transcript_path").ValueKind);
            Assert.False(input.RootElement.GetProperty("stop_hook_active").GetBoolean());
            Assert.Equal("child answer", input.RootElement.GetProperty("last_assistant_message").GetString());
            Assert.Equal(12, input.RootElement.EnumerateObject().Count());
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task SubagentStopContinueFalseWinsOverContinuation()
    {
        var workspace = CreateTemporaryDirectory();
        try
        {
            var definitions = new[]
            {
                CreateDefinition(workspace, CopilotCodexConfiguredHookEvent.SubagentStop, "continue", 0),
                CreateDefinition(workspace, CopilotCodexConfiguredHookEvent.SubagentStop, "stop", 1),
            };
            var runner = new RecordingRunner(definition => SuccessfulResult(
                definition.Command == "stop"
                    ? """{"continue":false,"stopReason":"finalize child"}"""
                    : """{"decision":"block","reason":"keep working"}"""));

            var outcome = await new CopilotCodexStopHookExecutor(runner).RunAsync(
                CreateRequest(workspace, definitions),
                stopHookActive: false,
                lastAssistantMessage: "answer",
                onDiagnostic: null,
                CancellationToken.None);

            Assert.True(outcome.ShouldStop);
            Assert.Equal("finalize child", outcome.StopReason);
            Assert.False(outcome.ShouldContinue);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public void ChildRequestCarriesLifecycleIdentityAndBoundedStartContext()
    {
        var workspace = CreateTemporaryDirectory();
        try
        {
            var parent = CreateRequest(workspace);
            parent = new CopilotAgentRequest
            {
                ConversationId = parent.ConversationId,
                TaskId = parent.TaskId,
                WorkspacePath = parent.WorkspacePath,
                UserText = "inspect files",
                TaskIntentText = "inspect files",
                Profile = parent.Profile,
                SearchRootPaths = [workspace],
                TrustedProjectRootPaths = [workspace],
                ConfiguredDeveloperInstructions = "base developer guidance",
                SessionStartAdditionalContexts = ["session-wide guidance"],
                CodexHooksEnabled = true,
                CodexCommandHooks = parent.CodexCommandHooks,
                CodexCustomSubagents =
                [
                    new CopilotCodexCustomSubagentDefinition
                    {
                        Name = "reviewer",
                    },
                ],
            };
            var runRequest = new CopilotSubagentRunRequest
            {
                RunId = "delegate-run-1",
                Task = "inspect files",
                Agent = "reviewer",
                RequestTokenBudget = 16_384,
            };

            var child = CopilotSubagentRunner.CreateChildRequest(
                parent,
                CopilotSubagentRoleCatalog.Default.GetRequired(CopilotSubagentRoleCatalog.ExploreRoleId),
                runRequest,
                ["read the test conventions first"]);

            Assert.NotNull(child.CodexSubagentHookContext);
            Assert.Equal("delegate-run-1", child.CodexSubagentHookContext.AgentId);
            Assert.Equal("delegate-run-1", child.CodexSubagentHookContext.TurnId);
            Assert.Equal("reviewer", child.CodexSubagentHookContext.AgentType);
            Assert.Contains("# SubagentStart hook context", child.ConfiguredDeveloperInstructions, StringComparison.Ordinal);
            Assert.Contains("read the test conventions first", child.ConfiguredDeveloperInstructions, StringComparison.Ordinal);
            Assert.Contains("base developer guidance", child.ConfiguredDeveloperInstructions, StringComparison.Ordinal);
            Assert.Equal(["session-wide guidance"], child.SessionStartAdditionalContexts);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task AgentRuntimeContinuesSubagentStopWithoutCallingRootStop()
    {
        var workspace = CreateTemporaryDirectory();
        try
        {
            var definitions = new[]
            {
                CreateDefinition(workspace, CopilotCodexConfiguredHookEvent.Stop, "root-stop", 0),
                CreateDefinition(workspace, CopilotCodexConfiguredHookEvent.SubagentStop, "child-stop", 1, "^explore$"),
            };
            var hookRunCount = 0;
            var hookRunner = new RecordingRunner(_ => SuccessfulResult(
                Interlocked.Increment(ref hookRunCount) == 1
                    ? """{"decision":"block","reason":"revise child once"}"""
                    : "{}"));
            var chatClient = new SequentialChatClient("first child answer", "revised child answer");
            var runtime = new CopilotMicrosoftAgentFrameworkRuntime(
                new CopilotToolRegistry(Array.Empty<ICopilotTool>()),
                new CopilotAgentContextBuilder(),
                new CopilotToolExecutor(),
                _ => chatClient,
                new EmptyExternalToolProvider(),
                new CopilotCapabilityCatalog(),
                new CopilotCodexStopHookExecutor(hookRunner));
            var request = CreateRequest(workspace, definitions);
            request.RuntimeExecutionScope = CopilotExecutionScope.ForAgentRun(request);
            var events = new List<CopilotAgentEvent>();

            var result = await runtime.RunAsync(request, events.Add, CancellationToken.None);

            Assert.Equal(CopilotAgentStopReason.Completed, result.StopReason);
            Assert.Equal(2, chatClient.CallCount);
            Assert.Equal(2, hookRunner.Calls.Count);
            Assert.All(hookRunner.Calls, call =>
                Assert.Equal(CopilotCodexConfiguredHookEvent.SubagentStop, call.Definition.Event));
            using var firstInput = JsonDocument.Parse(hookRunner.Calls[0].StandardInput);
            using var secondInput = JsonDocument.Parse(hookRunner.Calls[1].StandardInput);
            Assert.False(firstInput.RootElement.GetProperty("stop_hook_active").GetBoolean());
            Assert.True(secondInput.RootElement.GetProperty("stop_hook_active").GetBoolean());
            Assert.Contains(events, item => item.Type == CopilotAgentEventType.AnswerReset);
            Assert.Contains(events, item =>
                item.Type == CopilotAgentEventType.AnswerDelta
                && item.Text.Contains("revised child answer", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public void SubagentLifecycleHooksStayInSnapshotButNotPerToolBindings()
    {
        var workspace = CreateTemporaryDirectory();
        try
        {
            var definitions = new[]
            {
                CreateDefinition(workspace, CopilotCodexConfiguredHookEvent.SubagentStart, "start", 0),
                CreateDefinition(workspace, CopilotCodexConfiguredHookEvent.SubagentStop, "stop", 1),
            };

            Assert.Empty(CopilotCodexCommandHookFactory.Resolve(definitions, "ReadLocalFile"));
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
            ConversationId = "subagent-session",
            TaskId = "parent-turn",
            WorkspacePath = workspace,
            UserText = "test child task",
            TaskIntentText = "test child task",
            Profile = new CopilotProfileConfig
            {
                VendorType = CopilotVendorType.Custom,
                ProviderType = CopilotProviderType.OpenAICompatible,
                ApiKey = "test-key",
                BaseUrl = "https://example.test/v1",
                Model = "test-model",
                MaxTokens = 4_096,
            },
            Mode = CopilotAgentMode.Code,
            CodexHooksEnabled = true,
            CodexCommandHooks = definitions,
            CodexSubagentHookContext = new CopilotCodexSubagentHookContext(
                "run-1",
                "explore",
                "run-1"),
            CodexApprovalPolicy = CopilotCodexApprovalPolicy.CreateScalar(
                CopilotCodexApprovalPolicyMode.Untrusted),
            HarnessFeatures = CopilotAgentHarnessFeatures.None,
            RunBudgetOverride = new CopilotAgentRunBudgetOverride
            {
                RequestTokenBudget = 16_384,
                MaxToolCalls = 1,
                MaxAgentPasses = 2,
                TotalDuration = TimeSpan.FromSeconds(30),
            },
        };

    private static CopilotCodexCommandHookDefinition CreateDefinition(
        string workspace,
        CopilotCodexConfiguredHookEvent hookEvent,
        string command,
        int order,
        string matcher = "*") => new(
            $"codex-config:{hookEvent}:{order}",
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
            $"copilot-subagent-hook-{Guid.NewGuid():N}");
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
            var result = resultFactory(definition);
            Calls.Add(new CommandCall(definition, standardInput, result));
            return Task.FromResult(result);
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

    private sealed class EmptyExternalToolProvider : ICopilotExternalToolProvider
    {
        public Task<CopilotExternalToolLease> DiscoverAsync(
            CopilotAgentRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new CopilotExternalToolLease());
        }
    }

    private sealed class SequentialChatClient(params string[] responses) : IChatClient
    {
        private int _index;

        public int CallCount => _index;

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var response = GetNextResponse();
            return Task.FromResult(new ChatResponse(
                new ChatMessage(ChatRole.Assistant, response))
            {
                FinishReason = ChatFinishReason.Stop,
            });
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var response = GetNextResponse();
            await Task.CompletedTask;
            yield return new ChatResponseUpdate(ChatRole.Assistant, response)
            {
                FinishReason = ChatFinishReason.Stop,
            };
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }

        private string GetNextResponse()
        {
            var index = Interlocked.Increment(ref _index) - 1;
            Assert.InRange(index, 0, responses.Length - 1);
            return responses[index];
        }
    }

    private sealed record CommandCall(
        CopilotCodexCommandHookDefinition Definition,
        string StandardInput,
        CopilotCodexCommandHookProcessResult Result);
}
