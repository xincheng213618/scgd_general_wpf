using ColorVision.Copilot;
using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;

namespace ColorVision.UI.Tests;

public sealed class CopilotDelegatedDirectAnswerChatClientTests
{
    [Fact]
    public async Task AgentRuntimeSkipsBothParentProviderCallsForCompletedExclusiveExplore()
    {
        const string answer =
            "- C:\\workspace\\Coordinator.cs:24-26 — verified the bounded coordinator budget.\n"
            + "- C:\\workspace\\Explore.cs:434-475 — verified `CombineBudgets`.\n"
            + "complete: yes — every requested file has grounded evidence.";
        using var provider = new DelegateCallingChatClient("provider fallback");
        var registry = new CopilotToolRegistry(
        [
            new CopilotDelegateExploreTool(new StubSubagentRunner(new CopilotSubagentResult
            {
                Answer = answer,
                StopReason = CopilotAgentStopReason.Completed,
                ToolNames = ["ReadLocalFile"],
                Budget = new CopilotAgentBudgetSnapshot
                {
                    ProviderCalls = 1,
                    ToolCalls = 1,
                    RequestTokenBudget = 16_384,
                    ConsumedTokens = 2_048,
                },
                UsedPreselectedEvidence = true,
                HasSuccessfulEvidence = true,
            })),
        ]);
        var runtime = new CopilotMicrosoftAgentFrameworkRuntime(
            registry,
            new CopilotAgentContextBuilder(),
            new CopilotToolExecutor(),
            _ => provider,
            EmptyExternalToolProvider.Instance,
            new CopilotCapabilityCatalog());
        var events = new List<CopilotAgentEvent>();

        var result = await runtime.RunAsync(
            ExclusiveExploreRequest(),
            events.Add,
            CancellationToken.None);

        Assert.Equal(CopilotAgentStopReason.Completed, result.StopReason);
        Assert.Equal(0, provider.CallCount);
        Assert.Equal(1, result.Budget.ProviderCalls);
        Assert.True(result.Budget.UsedDelegatedDirectAnswer);
        Assert.Single(result.StepRecords);
        Assert.Equal(answer, string.Concat(events
            .Where(agentEvent => agentEvent.Type == CopilotAgentEventType.AnswerDelta)
            .Select(agentEvent => agentEvent.Text)));
        Assert.Contains(events, agentEvent =>
            agentEvent.Type == CopilotAgentEventType.RuntimeDiagnostic
            && agentEvent.Text.Contains(
                "without a second parent provider call",
                StringComparison.Ordinal));
        Assert.Contains(events, agentEvent =>
            agentEvent.Type == CopilotAgentEventType.RuntimeDiagnostic
            && agentEvent.Text.Contains(
                "without a parent provider planning call",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task AgentRuntimeUsesOneParentFallbackWhenExploreAnswerCannotReturnDirectly()
    {
        using var provider = new RecordingChatClient("parent fallback");
        var registry = new CopilotToolRegistry(
        [
            new CopilotDelegateExploreTool(new StubSubagentRunner(new CopilotSubagentResult
            {
                Answer = "The delegated inspection completed, but its answer is not safe to return directly.",
                StopReason = CopilotAgentStopReason.Completed,
                ToolNames = ["ReadLocalFile"],
                Budget = new CopilotAgentBudgetSnapshot
                {
                    ProviderCalls = 1,
                    ToolCalls = 1,
                    RequestTokenBudget = 16_384,
                    ConsumedTokens = 2_048,
                },
                UsedPreselectedEvidence = true,
                HasSuccessfulEvidence = true,
            })),
        ]);
        var runtime = new CopilotMicrosoftAgentFrameworkRuntime(
            registry,
            new CopilotAgentContextBuilder(),
            new CopilotToolExecutor(),
            _ => provider,
            EmptyExternalToolProvider.Instance,
            new CopilotCapabilityCatalog());
        var events = new List<CopilotAgentEvent>();

        var result = await runtime.RunAsync(
            ExclusiveExploreRequest(),
            events.Add,
            CancellationToken.None);

        Assert.Equal(CopilotAgentStopReason.Completed, result.StopReason);
        Assert.Equal(1, provider.CallCount);
        Assert.Equal(2, result.Budget.ProviderCalls);
        Assert.False(result.Budget.UsedDelegatedDirectAnswer);
        Assert.Single(result.StepRecords);
        Assert.Equal("parent fallback", string.Concat(events
            .Where(agentEvent => agentEvent.Type == CopilotAgentEventType.AnswerDelta)
            .Select(agentEvent => agentEvent.Text)));
        Assert.Contains(events, agentEvent =>
            agentEvent.Type == CopilotAgentEventType.RuntimeDiagnostic
            && agentEvent.Text.Contains(
                "without a parent provider planning call",
                StringComparison.Ordinal));
        Assert.DoesNotContain(events, agentEvent =>
            agentEvent.Type == CopilotAgentEventType.RuntimeDiagnostic
            && agentEvent.Text.Contains(
                "without a second parent provider call",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task CompletedExclusiveExploreResultStreamsDirectlyWhenDisplayFormatChanges()
    {
        const string callId = "call-completed-explore";
        const string answer =
            "- C:\\workspace\\Coordinator.cs:24-26 — verified the bounded coordinator budget.\n"
            + "- C:\\workspace\\Explore.cs:434-475 — verified `CombineBudgets`.\n"
            + "complete: yes — every requested file has grounded evidence.";
        using var provider = new RecordingChatClient("provider fallback");
        var directAnswerCount = 0;
        using var client = new CopilotDelegatedDirectAnswerChatClient(
            provider,
            ExclusiveExploreRequest(),
            () =>
            [
                CompletedExploreStep(
                    callId,
                    answer,
                    displayContent: """{"format":"changed","answer_field":"not parsed"}"""),
            ],
            taskLedgerEnabled: false,
            () => directAnswerCount++);

        var updates = new List<ChatResponseUpdate>();
        await foreach (var update in client.GetStreamingResponseAsync(
            [FunctionResultMessage(callId)],
            cancellationToken: CancellationToken.None))
        {
            updates.Add(update);
        }

        Assert.Equal(0, provider.CallCount);
        Assert.Equal(1, directAnswerCount);
        Assert.Equal(answer, string.Concat(updates.Select(update => update.Text)));
        Assert.Equal(ChatFinishReason.Stop, Assert.Single(updates).FinishReason);
    }

    [Theory]
    [InlineData(true, true, false, "complete: yes — done.")]
    [InlineData(false, false, false, "complete: yes — done.")]
    [InlineData(false, true, false, "complete: no — missing evidence.")]
    [InlineData(false, true, true, "complete: yes — done.")]
    public async Task UnsafeOrIncompleteResultFallsBackToProvider(
        bool taskLedgerEnabled,
        bool successfulEvidence,
        bool wasTruncated,
        string completionLine)
    {
        const string callId = "call-fallback-explore";
        var answer =
            "- C:\\workspace\\Coordinator.cs:24-26 — verified bounded evidence.\n"
            + completionLine;
        using var provider = new RecordingChatClient("provider fallback");
        var directAnswerCount = 0;
        using var client = new CopilotDelegatedDirectAnswerChatClient(
            provider,
            ExclusiveExploreRequest(),
            () => [CompletedExploreStep(callId, answer, successfulEvidence, wasTruncated: wasTruncated)],
            taskLedgerEnabled,
            () => directAnswerCount++);

        var response = await client.GetResponseAsync(
            [FunctionResultMessage(callId)],
            cancellationToken: CancellationToken.None);

        Assert.Equal(1, provider.CallCount);
        Assert.Equal(0, directAnswerCount);
        Assert.Equal("provider fallback", response.Text);
    }

    [Fact]
    public async Task LegacyFormattedContentWithoutStructuredAnswerFallsBackToProvider()
    {
        const string callId = "call-legacy-content";
        const string answer =
            "- C:\\workspace\\Coordinator.cs:24-26 — verified bounded evidence.\n"
            + "complete: yes — done.";
        using var provider = new RecordingChatClient("provider fallback");
        var directAnswerCount = 0;
        using var client = new CopilotDelegatedDirectAnswerChatClient(
            provider,
            ExclusiveExploreRequest(),
            () => [CompletedExploreStep(callId, answer, includeStructuredAnswer: false)],
            taskLedgerEnabled: false,
            () => directAnswerCount++);

        var response = await client.GetResponseAsync(
            [FunctionResultMessage(callId)],
            cancellationToken: CancellationToken.None);

        Assert.Equal(1, provider.CallCount);
        Assert.Equal(0, directAnswerCount);
        Assert.Equal("provider fallback", response.Text);
    }

    [Fact]
    public async Task MultipleCurrentToolStepsFallBackToParentProvider()
    {
        const string callId = "call-multiple-steps";
        const string answer =
            "- C:\\workspace\\Coordinator.cs:24-26 — verified bounded evidence.\n"
            + "complete: yes — done.";
        using var provider = new RecordingChatClient("provider fallback");
        using var client = new CopilotDelegatedDirectAnswerChatClient(
            provider,
            ExclusiveExploreRequest(),
            () =>
            [
                CompletedExploreStep(callId, answer),
                CompletedExploreStep("call-second-explore", answer),
            ],
            taskLedgerEnabled: false,
            () => { });

        var response = await client.GetResponseAsync(
            [FunctionResultMessage(callId)],
            cancellationToken: CancellationToken.None);

        Assert.Equal(1, provider.CallCount);
        Assert.Equal("provider fallback", response.Text);
    }

    private static CopilotAgentRequest ExclusiveExploreRequest() => new()
    {
        UserText =
            "请只使用 DelegateExplore 子 Agent 检查 C:\\workspace 下的 Coordinator.cs 和 Explore.cs，"
            + "返回准确文件路径与行号证据，不要由父 Agent 直接读取文件。",
        Profile = new CopilotProfileConfig
        {
            ProviderType = CopilotProviderType.OpenAICompatible,
            BaseUrl = "https://example.com/v1",
            ApiKey = "test",
            Model = "test-model",
        },
        SearchRootPaths = [@"C:\workspace"],
        RequiredSuccessfulToolNames = ["DelegateExplore"],
        RequiresDelegatedWorkspaceEvidence = true,
        Mode = CopilotAgentMode.Code,
    };

    private static Microsoft.Extensions.AI.ChatMessage FunctionResultMessage(string callId) =>
        new(ChatRole.Tool, [new FunctionResultContent(callId, "{}")]);

    private static CopilotAgentStepRecord CompletedExploreStep(
        string callId,
        string answer,
        bool successfulEvidence = true,
        string? displayContent = null,
        bool includeStructuredAnswer = true,
        bool wasTruncated = false) => new()
    {
        ToolCall = new CopilotToolCall { ToolName = "DelegateExplore" },
        Observation = new CopilotToolObservation
        {
            Success = true,
            Content = displayContent ??
                "[Explore subagent result]\n"
                + "role: explore\n"
                + "run_id: explore-test\n"
                + "stop_reason: Completed\n"
                + "request_token_budget: 16384\n"
                + "queue_ms: 0\n"
                + "budget_finalization: false\n"
                + "preselected_evidence: true\n"
                + $"successful_tool_evidence: {successfulEvidence.ToString().ToLowerInvariant()}\n"
                + "output_truncated: false\n"
                + "tools_used: ReadLocalFile\n"
                + "answer:\n"
                + answer,
            DelegatedAnswer = includeStructuredAnswer
                ? new CopilotDelegatedAnswer
                {
                    Text = answer,
                    StopReason = CopilotAgentStopReason.Completed,
                    HasSuccessfulEvidence = successfulEvidence,
                    WasTruncated = wasTruncated,
                }
                : null,
            DelegatedRunUsage = new CopilotDelegatedRunUsage
            {
                RoleId = "explore",
                RunId = "explore-test",
                StopReason = CopilotAgentStopReason.Completed,
            },
        },
        Execution = new CopilotToolExecutionInfo { CallId = callId },
    };

    private sealed class RecordingChatClient(string responseText) : IChatClient
    {
        public int CallCount { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(new ChatResponse(
                new Microsoft.Extensions.AI.ChatMessage(ChatRole.Assistant, responseText))
            {
                FinishReason = ChatFinishReason.Stop,
            });
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            CallCount++;
            yield return new ChatResponseUpdate(ChatRole.Assistant, responseText)
            {
                FinishReason = ChatFinishReason.Stop,
            };
            await Task.CompletedTask;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) =>
            serviceType.IsInstanceOfType(this) ? this : null;

        public void Dispose()
        {
        }
    }

    private sealed class DelegateCallingChatClient(string fallbackText) : IChatClient
    {
        public int CallCount { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            if (CallCount == 1)
            {
                return Task.FromResult(new ChatResponse(
                    new Microsoft.Extensions.AI.ChatMessage(
                        ChatRole.Assistant,
                        [CreateDelegateCall()]))
                {
                    FinishReason = ChatFinishReason.ToolCalls,
                });
            }

            return Task.FromResult(new ChatResponse(
                new Microsoft.Extensions.AI.ChatMessage(ChatRole.Assistant, fallbackText))
            {
                FinishReason = ChatFinishReason.Stop,
            });
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            CallCount++;
            if (CallCount == 1)
            {
                yield return new ChatResponseUpdate(ChatRole.Assistant, [CreateDelegateCall()])
                {
                    FinishReason = ChatFinishReason.ToolCalls,
                };
                yield break;
            }

            yield return new ChatResponseUpdate(ChatRole.Assistant, fallbackText)
            {
                FinishReason = ChatFinishReason.Stop,
            };
            await Task.CompletedTask;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) =>
            serviceType.IsInstanceOfType(this) ? this : null;

        public void Dispose()
        {
        }

        private static FunctionCallContent CreateDelegateCall() => new(
            "call-runtime-explore",
            "colorvision_delegate_explore",
            new Dictionary<string, object?>
            {
                ["task"] = "Inspect Coordinator.cs and Explore.cs and return grounded file-line evidence.",
            });
    }

    private sealed class StubSubagentRunner(CopilotSubagentResult result) : ICopilotSubagentRunner
    {
        public Task<CopilotSubagentResult> RunAsync(
            CopilotAgentRequest parentRequest,
            CopilotSubagentRoleDescriptor role,
            CopilotSubagentRunRequest runRequest,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(result);
        }
    }

    private sealed class EmptyExternalToolProvider : ICopilotExternalToolProvider
    {
        public static EmptyExternalToolProvider Instance { get; } = new();

        public Task<CopilotExternalToolLease> DiscoverAsync(
            CopilotAgentRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new CopilotExternalToolLease());
    }
}
