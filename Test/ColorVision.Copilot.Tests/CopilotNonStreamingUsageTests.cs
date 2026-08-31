using ColorVision.Copilot;
using Microsoft.Extensions.AI;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotNonStreamingUsageTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OfficialAdapterResponseUsageExhaustsBudgetBeforeAnotherProviderCall(bool responsesApi)
    {
        using var handler = new CompletionHandler(responsesApi);
        using var httpClient = new HttpClient(handler);
        using var provider = CopilotOpenAiAgentChatClientFactory.Create(CreateProfile(responsesApi), httpClient);
        using var client = new CopilotTokenBudgetChatClient(provider, CreateBudget(4_096));

        var response = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "Summarize the result.")]);

        Assert.Equal(5_000, response.Usage?.TotalTokenCount);
        Assert.DoesNotContain(response.Messages.SelectMany(message => message.Contents), content => content is UsageContent);
        Assert.Equal(5_000, client.Snapshot.ReportedTotalTokens);
        Assert.Equal(5_000, client.Snapshot.ConsumedTokens);
        Assert.Equal(2_000, client.Snapshot.ReportedCachedInputTokens);
        Assert.False(client.Snapshot.UsedEstimatedUsage);
        Assert.True(client.Snapshot.BudgetExhausted);
        await Assert.ThrowsAsync<CopilotAgentTokenBudgetExceededException>(() =>
            client.GetResponseAsync([new ChatMessage(ChatRole.User, "Continue.")]));
        Assert.Single(handler.Payloads);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task ResponseAndMessageUsageCountOncePerCallAndAccumulateAcrossCalls(bool responseUsage, bool messageUsage)
    {
        using var client = new CopilotTokenBudgetChatClient(
            new UsageLocationChatClient(responseUsage, messageUsage), CreateBudget(8_192));

        await client.GetResponseAsync([new ChatMessage(ChatRole.User, "First summary.")]);
        Assert.Equal(1_250, client.Snapshot.ReportedTotalTokens);
        await client.GetResponseAsync([new ChatMessage(ChatRole.User, "Second summary.")]);

        Assert.Equal(2, client.Snapshot.ProviderCalls);
        Assert.Equal(2_000, client.Snapshot.ReportedInputTokens);
        Assert.Equal(500, client.Snapshot.ReportedOutputTokens);
        Assert.Equal(2_500, client.Snapshot.ReportedTotalTokens);
        Assert.Equal(2_500, client.Snapshot.ConsumedTokens);
        Assert.Equal(1_000, client.Snapshot.ReportedCachedInputTokens);
        Assert.False(client.Snapshot.UsedEstimatedUsage);
    }

    [Theory]
    [InlineData(false, "stop")]
    [InlineData(false, "length")]
    [InlineData(false, "content_filter")]
    [InlineData(true, "stop")]
    [InlineData(true, "length")]
    [InlineData(true, "content_filter")]
    public async Task FinalAnswerPathsPreserveOfficialResponseUsageAndIncompleteCheckpoint(bool finalAnswerOnly, string finishReason)
    {
        var directory = Directory.CreateTempSubdirectory("CopilotNonStreamingUsageTests-");
        try
        {
            using var handler = new CompletionHandler(responsesApi: false, finishReason);
            using var httpClient = new HttpClient(handler);
            var profile = CreateProfile(responsesApi: false);
            using var provider = new EmptyAnswerThenOfficialClient(
                CopilotOpenAiAgentChatClientFactory.Create(profile, httpClient));
            var catalog = new CopilotCapabilityCatalog();
            catalog.PublishSource(CopilotCapabilitySourceKind.BuiltIn, "usage-tests", "Usage tests", [new CatalogTool()]);
            var externalProvider = new EmptyExternalToolProvider();
            var runtime = new CopilotMicrosoftAgentFrameworkRuntime(
                new CopilotToolRegistry(Array.Empty<ICopilotTool>()),
                new CopilotAgentContextBuilder(),
                new CopilotToolExecutor(),
                _ => provider,
                externalProvider,
                catalog,
                new CopilotAgentSkillUsageStore(directory.FullName));
            CopilotAgentSessionCheckpoint? checkpoint = null;
            if (finalAnswerOnly)
            {
                var journal = new CopilotAgentTaskEventJournalBuilder();
                journal.RecordRunStarted();
                journal.RecordStop(CopilotAgentStopReason.IncompleteOutput);
                checkpoint = CopilotAgentSessionCheckpoint.Create(profile, "{}", catalog.GetSnapshot(), taskEventJournal: journal.Snapshot());
                Assert.NotNull(checkpoint);
            }
            var request = new CopilotAgentRequest
            {
                Profile = profile,
                ConversationId = "non-streaming-usage-conversation",
                TaskId = "non-streaming-usage-task",
                WorkspacePath = directory.FullName,
                UserText = "Summarize the supplied result.",
                TaskIntentText = "Summarize the supplied result.",
                Mode = CopilotAgentMode.Auto,
                HarnessFeatures = CopilotAgentHarnessFeatures.None,
                SessionCheckpoint = checkpoint,
                Recovery = finalAnswerOnly ? new CopilotAgentRecoveryRequest
                {
                    Mode = CopilotAgentRecoveryMode.Finalize,
                    PreviousStopReason = CopilotAgentStopReason.IncompleteOutput,
                } : null,
                RunBudgetOverride = new CopilotAgentRunBudgetOverride
                {
                    RequestTokenBudget = 32_768,
                    MaxToolCalls = 1,
                    MaxAgentPasses = 1,
                    TotalDuration = TimeSpan.FromSeconds(30),
                },
            };
            var events = new List<CopilotAgentEvent>();

            var result = await runtime.RunAsync(request, events.Add, CancellationToken.None);

            var expectedUsage = finalAnswerOnly ? 5_000 : 5_110;
            Assert.Equal(expectedUsage, result.Usage.EffectiveTotalTokens);
            Assert.Equal(expectedUsage, result.Budget.ReportedTotalTokens);
            Assert.Equal(expectedUsage, result.Budget.ConsumedTokens);
            Assert.Equal(2_000, result.Usage.CachedInputTokens);
            Assert.False(result.Budget.UsedEstimatedUsage);
            Assert.Equal(finalAnswerOnly ? 1 : 2, result.Budget.ProviderCalls);
            Assert.Equal(finalAnswerOnly ? 0 : 1, provider.StreamingCalls);
            Assert.Equal(finalAnswerOnly ? 0 : 1, externalProvider.DiscoveryCalls);
            Assert.Empty(result.StepRecords);
            var payload = Assert.Single(handler.Payloads);
            using var requestJson = JsonDocument.Parse(payload);
            Assert.False(requestJson.RootElement.TryGetProperty("tools", out var tools) && tools.GetArrayLength() > 0);
            Assert.Contains(events, item => item.Type == CopilotAgentEventType.AnswerDelta
                && item.Text.Contains("Provider final answer", StringComparison.Ordinal));
            Assert.Equal(finishReason == "stop" ? CopilotAgentStopReason.Completed : CopilotAgentStopReason.IncompleteOutput, result.StopReason);
            if (finishReason != "stop")
            {
                Assert.NotNull(result.SessionCheckpoint);
                Assert.Contains(result.SessionCheckpoint.TaskEventJournal.Events, item =>
                    item.Type == CopilotAgentTaskEventType.RunStopped
                    && item.State == CopilotAgentStopReason.IncompleteOutput.ToString());
            }
            else if (finalAnswerOnly)
            {
                Assert.Null(result.SessionCheckpoint);
            }

            var turn = CopilotTurnEventReducer.Reduce(CopilotTurnEventState.Create(request.Mode), new CopilotTurnStartedEvent(request.Mode));
            var sink = new CopilotTurnEventSink(turnEvent => turn = CopilotTurnEventReducer.Reduce(turn, turnEvent));
            foreach (var agentEvent in events)
            {
                sink.OnAgentEvent(agentEvent);
                if (agentEvent.Type == CopilotAgentEventType.BudgetUpdated && agentEvent.Budget!.ReportedTotalTokens > 0)
                {
                    var budget = agentEvent.Budget;
                    sink.OnTokenUsageUpdated(new CopilotTokenUsage(
                        budget.ReportedInputTokens, budget.ReportedOutputTokens, budget.ReportedTotalTokens, budget.ReportedCachedInputTokens));
                }
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

    private static CopilotAgentTokenBudget CreateBudget(int requestBudget) => new()
    {
        ContextWindowTokens = CopilotAgentTokenBudget.MinimumContextWindowTokens,
        MaxOutputTokens = 1_024,
        RequestTokenBudget = requestBudget,
    };

    private static CopilotProfileConfig CreateProfile(bool responsesApi) => new()
    {
        ProviderType = CopilotProviderType.OpenAICompatible,
        VendorType = responsesApi ? CopilotVendorType.OpenAI : CopilotVendorType.Custom,
        ApiKey = "test-key",
        BaseUrl = responsesApi ? "https://api.openai.com/v1" : "https://example.test/v1",
        Model = responsesApi ? "gpt-5.5" : "test-model",
        MaxTokens = 4_096,
    };

    private sealed class CompletionHandler(bool responsesApi, string finishReason = "stop") : HttpMessageHandler
    {
        public List<string> Payloads { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Payloads.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            var json = responsesApi
                ? """
                    {"id":"resp_test","object":"response","created_at":1234567890,"model":"gpt-5.5","status":"completed","output":[{"type":"message","id":"msg_test","role":"assistant","status":"completed","content":[{"type":"output_text","text":"Provider final answer.","annotations":[]}]}],"usage":{"input_tokens":4000,"output_tokens":1000,"total_tokens":5000,"input_tokens_details":{"cached_tokens":2000},"output_tokens_details":{"reasoning_tokens":0}}}
                    """
                : """
                    {"id":"chatcmpl_test","object":"chat.completion","created":1234567890,"model":"test-model","choices":[{"index":0,"message":{"role":"assistant","content":"Provider final answer."},"finish_reason":"__FINISH_REASON__"}],"usage":{"prompt_tokens":4000,"completion_tokens":1000,"total_tokens":5000,"prompt_tokens_details":{"cached_tokens":2000},"completion_tokens_details":{"reasoning_tokens":0}}}
                    """.Replace("__FINISH_REASON__", finishReason, StringComparison.Ordinal);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
        }
    }

    private sealed class EmptyAnswerThenOfficialClient(IChatClient innerClient) : DelegatingChatClient(innerClient)
    {
        public int StreamingCalls { get; private set; }

        public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StreamingCalls++;
            await Task.CompletedTask;
            yield return new ChatResponseUpdate(ChatRole.Assistant,
            [
                new UsageContent(new UsageDetails { InputTokenCount = 100, OutputTokenCount = 10, TotalTokenCount = 110 }),
            ]) { FinishReason = ChatFinishReason.Stop };
        }
    }

    private sealed class UsageLocationChatClient(bool responseUsage, bool messageUsage) : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var usage = new UsageDetails
            {
                InputTokenCount = 1_000,
                OutputTokenCount = 250,
                TotalTokenCount = 1_250,
                CachedInputTokenCount = 500,
            };
            var message = new ChatMessage(ChatRole.Assistant, "Summary.");
            if (messageUsage)
                message.Contents.Add(new UsageContent(usage));
            return Task.FromResult(new ChatResponse(message) { Usage = responseUsage ? usage : null, FinishReason = ChatFinishReason.Stop });
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    private sealed class EmptyExternalToolProvider : ICopilotExternalToolProvider
    {
        public int DiscoveryCalls { get; private set; }
        public Task<CopilotExternalToolLease> DiscoverAsync(CopilotAgentRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DiscoveryCalls++;
            return Task.FromResult(new CopilotExternalToolLease());
        }
    }

    private sealed class CatalogTool : ICopilotTool
    {
        public string Name => "UsageEvidence";
        public string Description => "Represents persisted evidence without exposing an executable tool.";
        public bool CanHandle(CopilotAgentRequest request) => false;
        public Task<CopilotToolResult> ExecuteAsync(CopilotAgentRequest request, CopilotAgentToolInput input, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Final answer recovery must not execute tools.");
    }
}
