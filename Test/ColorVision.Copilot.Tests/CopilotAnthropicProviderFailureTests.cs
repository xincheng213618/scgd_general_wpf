using Anthropic;
using Anthropic.Core;
using Anthropic.Exceptions;
using Anthropic.Models;
using ColorVision.Copilot;
using Microsoft.Extensions.AI;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotAnthropicProviderFailureTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OfficialAdapterThrowsSseExceptionBeforePublishingIncompleteUsage(bool includeText)
    {
        using var handler = new SseHandler(_ => ErrorStream("overloaded_error", includeText));
        using var httpClient = new HttpClient(handler);
        using var provider = CreateProvider(httpClient);
        var updates = new List<ChatResponseUpdate>();

        var error = await Assert.ThrowsAsync<AnthropicSseException>(async () =>
        {
            await foreach (var update in provider.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "Read the result.")]))
                updates.Add(update);
        });

        Assert.Equal<ErrorType?>(ErrorType.OverloadedError, error.ErrorType);
        Assert.Null(error.InnerException);
        Assert.Equal(includeText ? "Partial answer." : string.Empty, string.Concat(updates.Select(update => update.Text)));
        // The installed adapter accumulates wire usage privately and publishes it only when its stream completes.
        Assert.Empty(updates.SelectMany(update => update.Contents).OfType<UsageContent>());
        Assert.Equal(1, handler.CallCount);
    }

    [Theory]
    [InlineData("overloaded_error", true, ErrorType.OverloadedError)]
    [InlineData("rate_limit_error", true, ErrorType.RateLimitError)]
    [InlineData("api_error", true, ErrorType.ApiError)]
    [InlineData("timeout_error", true, ErrorType.TimeoutError)]
    [InlineData("authentication_error", false, ErrorType.AuthenticationError)]
    [InlineData("invalid_request_error", false, ErrorType.InvalidRequestError)]
    [InlineData("unknown_error", false, null)]
    public async Task BeforeContentRetriesOnlyTransientSseErrors(string errorType, bool shouldRetry, ErrorType? expectedSdkErrorType)
    {
        using var handler = new SseHandler(call => call == 1
            ? ErrorStream(errorType, includeText: false)
            : CompletedStream(toolCall: false));
        using var httpClient = new HttpClient(handler);
        using var budget = new CopilotTokenBudgetChatClient(CreateProvider(httpClient), CreateBudget());
        var retries = new List<CopilotProviderRetryInfo>();
        using var retry = new CopilotProviderRetryChatClient(
            budget, retries.Add, maximumAttempts: 2, delayAsync: (_, _) => Task.CompletedTask);
        var answer = new StringBuilder();
        async Task ReadAsync()
        {
            await foreach (var update in retry.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "Read the result.")]))
                answer.Append(update.Text);
        }

        if (shouldRetry)
        {
            await ReadAsync();
            Assert.Equal("Completed answer.", answer.ToString());
            Assert.Equal(2, handler.CallCount);
            var observedRetry = Assert.Single(retries);
            Assert.Equal(errorType, observedRetry.FailureKind);
            Assert.Null(observedRetry.StatusCode);
            Assert.Equal(110, budget.Snapshot.ReportedTotalTokens);
            Assert.True(budget.Snapshot.ConsumedTokens > 110);
            Assert.Equal(2, budget.Snapshot.ProviderCalls);
        }
        else
        {
            var error = await Assert.ThrowsAsync<AnthropicSseException>(ReadAsync);
            Assert.Equal(expectedSdkErrorType, error.ErrorType);
            Assert.Empty(answer.ToString());
            Assert.Empty(retries);
            Assert.Equal(1, handler.CallCount);
            Assert.Equal(1, budget.Snapshot.ProviderCalls);
            Assert.Equal(0, budget.Snapshot.ReportedTotalTokens);
        }
        Assert.True(budget.Snapshot.UsedEstimatedUsage);
    }

    [Fact]
    public async Task TransientSseErrorAfterTextDoesNotReplayTheProviderCall()
    {
        using var handler = new SseHandler(_ => ErrorStream("overloaded_error", includeText: true));
        using var httpClient = new HttpClient(handler);
        using var budget = new CopilotTokenBudgetChatClient(CreateProvider(httpClient), CreateBudget());
        var retries = new List<CopilotProviderRetryInfo>();
        using var retry = new CopilotProviderRetryChatClient(
            budget, retries.Add, maximumAttempts: 2, delayAsync: (_, _) => Task.CompletedTask);
        var answer = new StringBuilder();

        await Assert.ThrowsAsync<AnthropicSseException>(async () =>
        {
            await foreach (var update in retry.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "Read the result.")]))
                answer.Append(update.Text);
        });

        Assert.Equal("Partial answer.", answer.ToString());
        Assert.Empty(retries);
        Assert.Equal(1, handler.CallCount);
        Assert.Equal(1, budget.Snapshot.ProviderCalls);
        Assert.Equal(0, budget.Snapshot.ReportedTotalTokens);
        Assert.True(budget.Snapshot.ConsumedTokens > 0);
        Assert.True(budget.Snapshot.UsedEstimatedUsage);
    }

    [Theory]
    [InlineData(false, "overloaded_error")]
    [InlineData(true, "overloaded_error")]
    [InlineData(true, "authentication_error")]
    public async Task SseFailureAfterAgentProgressPreservesFactsAndCheckpoint(bool completeToolFirst, string errorType)
    {
        var directory = Directory.CreateTempSubdirectory("CopilotAnthropicProviderFailureTests-");
        try
        {
            using var handler = new SseHandler(call => completeToolFirst && call == 1
                ? CompletedStream(toolCall: true)
                : ErrorStream(errorType, includeText: true));
            using var httpClient = new HttpClient(handler);
            var tool = new ValidationProbeTool();
            var catalog = new CopilotCapabilityCatalog();
            catalog.PublishSource(CopilotCapabilitySourceKind.BuiltIn, "anthropic-failure-tests", "Anthropic failure tests", [tool]);
            var runtime = new CopilotMicrosoftAgentFrameworkRuntime(
                new CopilotToolRegistry([tool]),
                new CopilotAgentContextBuilder(),
                new CopilotToolExecutor(),
                _ => CreateProvider(httpClient),
                new EmptyExternalToolProvider(),
                catalog,
                new CopilotAgentSkillUsageStore(directory.FullName));
            var request = new CopilotAgentRequest
            {
                ConversationId = "anthropic-failure-conversation",
                TaskId = "anthropic-failure-task",
                WorkspacePath = directory.FullName,
                UserText = "Run the bounded workspace validation probe and summarize its result.",
                TaskIntentText = "Run the bounded workspace validation probe and summarize its result.",
                Profile = new CopilotProfileConfig
                {
                    ProviderType = CopilotProviderType.AnthropicCompatible,
                    VendorType = CopilotVendorType.Custom,
                    ApiKey = "test-key",
                    BaseUrl = "https://example.test",
                    Model = "test-model",
                    MaxTokens = 4_096,
                },
                Mode = CopilotAgentMode.Code,
                HarnessFeatures = CopilotAgentHarnessFeatures.None,
                CodexApprovalPolicy = CopilotCodexApprovalPolicy.CreateScalar(CopilotCodexApprovalPolicyMode.Untrusted),
                RunBudgetOverride = new CopilotAgentRunBudgetOverride
                {
                    RequestTokenBudget = 32_768,
                    MaxToolCalls = 2,
                    MaxAgentPasses = 1,
                    TotalDuration = TimeSpan.FromSeconds(30),
                },
            };
            var events = new List<CopilotAgentEvent>();

            var result = await runtime.RunAsync(request, events.Add, CancellationToken.None);

            Assert.Equal(CopilotAgentStopReason.ProviderFailure, result.StopReason);
            Assert.Equal(completeToolFirst ? 2 : 1, handler.CallCount);
            Assert.Equal(completeToolFirst ? 2 : 1, result.Budget.ProviderCalls);
            Assert.Equal(completeToolFirst ? 110 : 0, result.Usage.EffectiveTotalTokens);
            Assert.Equal(result.Usage.EffectiveTotalTokens, result.Budget.ReportedTotalTokens);
            Assert.True(result.Budget.ConsumedTokens > result.Budget.ReportedTotalTokens);
            Assert.True(result.Budget.UsedEstimatedUsage);
            Assert.Equal(completeToolFirst ? 1 : 0, tool.CallCount);
            Assert.Equal(tool.CallCount, result.Budget.ToolCalls);
            Assert.Equal(tool.CallCount, result.StepRecords.Count);
            Assert.All(result.StepRecords, step => Assert.Equal(CopilotToolExecutionState.Completed, step.Execution.State));
            Assert.Contains(events, item => item.Type == CopilotAgentEventType.AnswerDelta && item.Text == "Partial answer.");
            Assert.DoesNotContain(events, item => item.ProviderRetry != null);
            Assert.NotNull(result.SessionCheckpoint);
            Assert.Contains(result.SessionCheckpoint.TaskEventJournal.Events, item =>
                item.Type == CopilotAgentTaskEventType.RunStopped && item.State == CopilotAgentStopReason.ProviderFailure.ToString());
            if (completeToolFirst)
                Assert.Contains(result.SessionCheckpoint.TaskEventJournal.Events, item => item.Type == CopilotAgentTaskEventType.ToolCompleted);

            var turn = CopilotTurnEventReducer.Reduce(CopilotTurnEventState.Create(request.Mode), new CopilotTurnStartedEvent(request.Mode));
            var sink = new CopilotTurnEventSink(turnEvent => turn = CopilotTurnEventReducer.Reduce(turn, turnEvent));
            foreach (var agentEvent in events)
            {
                sink.OnAgentEvent(agentEvent);
                if (agentEvent.Type == CopilotAgentEventType.BudgetUpdated)
                    sink.OnTokenUsageUpdated(CopilotTurnRuntime.GetReportedTokenUsage(agentEvent.Budget!));
            }
            sink.OnPlanUpdated(CopilotTurnPlanSnapshot.FromTaskLedger(result.TaskLedger));
            sink.OnTokenUsageUpdated(result.Usage);
            var completed = CopilotTurnResult.FromAgent(request.Mode, result.Usage, result);
            turn = CopilotTurnEventReducer.Reduce(turn, new CopilotTurnCompletedEvent(completed));
            Assert.Same(completed, CopilotTurnEventReducer.RequireCompletion(turn));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private static IChatClient CreateProvider(HttpClient httpClient) => new AnthropicClient(new ClientOptions
    {
        ApiKey = "test-key",
        BaseUrl = "https://example.test",
        HttpClient = httpClient,
        MaxRetries = 0,
    }).AsIChatClient("test-model", 4_096);

    private static CopilotAgentTokenBudget CreateBudget() => new()
    {
        ContextWindowTokens = CopilotAgentTokenBudget.MinimumContextWindowTokens,
        MaxOutputTokens = 1_024,
        RequestTokenBudget = 32_768,
    };

    private static string Event(string type, object payload) => "event: " + type + "\ndata: " + JsonSerializer.Serialize(payload) + "\n\n";

    private static string MessageStart() => Event("message_start", new
    {
        type = "message_start",
        message = new
        {
            id = "msg_test", type = "message", role = "assistant", model = "test-model", content = Array.Empty<object>(),
            stop_reason = (string?)null, stop_sequence = (string?)null, usage = new { input_tokens = 100, output_tokens = 0 },
        },
    });

    private static string TextBlock(string text) => Event("content_block_start", new
    {
        type = "content_block_start", index = 0, content_block = new { type = "text", text = string.Empty },
    }) + Event("content_block_delta", new
    {
        type = "content_block_delta", index = 0, delta = new { type = "text_delta", text },
    });

    private static string ErrorStream(string errorType, bool includeText) => MessageStart()
        + (includeText ? TextBlock("Partial answer.") : string.Empty)
        + Event("error", new { type = "error", error = new { type = errorType, message = "Controlled provider error." } });

    private static string CompletedStream(bool toolCall)
    {
        var content = toolCall
            ? Event("content_block_start", new
            {
                type = "content_block_start", index = 0,
                content_block = new { type = "tool_use", id = "validation-call", name = "colorvision_run_workspace_validation", input = new { } },
            }) + Event("content_block_delta", new
            {
                type = "content_block_delta", index = 0, delta = new { type = "input_json_delta", partial_json = "{}" },
            })
            : TextBlock("Completed answer.");
        return MessageStart() + content
            + Event("content_block_stop", new { type = "content_block_stop", index = 0 })
            + Event("message_delta", new
            {
                type = "message_delta", delta = new { stop_reason = toolCall ? "tool_use" : "end_turn", stop_sequence = (string?)null },
                usage = new { input_tokens = 100, output_tokens = 10 },
            })
            + Event("message_stop", new { type = "message_stop" });
    }

    private sealed class SseHandler(Func<int, string> response) : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response(++CallCount), Encoding.UTF8, "text/event-stream"),
            });
        }
    }

    private sealed class ValidationProbeTool : ICopilotAgentDrivenTool
    {
        public string Name => "RunWorkspaceValidation";
        public string Description => "Returns deterministic validation evidence.";
        public int CallCount { get; private set; }
        public bool CanHandle(CopilotAgentRequest request) => true;
        public bool IsAvailable(CopilotAgentRequest request) => true;

        public Task<CopilotToolResult> ExecuteAsync(CopilotAgentRequest request, CopilotAgentToolInput toolInput, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return Task.FromResult(new CopilotToolResult
            {
                ToolName = Name, Success = true, Summary = "Validation probe completed.", ProcessOperation = "test", ProcessExitCode = 0,
            });
        }
    }

    private sealed class EmptyExternalToolProvider : ICopilotExternalToolProvider
    {
        public Task<CopilotExternalToolLease> DiscoverAsync(CopilotAgentRequest request, CancellationToken cancellationToken)
            => Task.FromResult(new CopilotExternalToolLease());
    }
}
