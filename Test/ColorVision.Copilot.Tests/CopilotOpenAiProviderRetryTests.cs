using ColorVision.Copilot;
using Microsoft.Extensions.AI;
using System.ClientModel;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotOpenAiProviderRetryTests
{
    [Theory]
    [InlineData(true, "gpt-6-astra", true)]
    [InlineData(true, "gpt-5.5", false)]
    [InlineData(false, "gpt-6-astra", false)]
    public async Task RuntimePublishesCacheComparisonWithoutChangingToolEvidenceOrBudget(bool responsesApi, string model, bool expectedDiagnostics)
    {
        await using var server = new LoopbackProvider(call =>
        {
            var response = CompletedResponse(responsesApi, toolCall: call == 1);
            return response with
            {
                Body = response.Body.Replace("gpt-5.5", model, StringComparison.Ordinal).Replace("\"usage\":",
                    "\"prompt_cache_diagnostics\":{\"type\":\"cache_miss\",\"reason\":\"input_changed\",\"cache_missed_tokens\":9000},\"usage\":", StringComparison.Ordinal),
            };
        });
        using var fixture = new RunFixture(server, responsesApi, requestTool: true);
        fixture.Profile.Model = model;

        var result = await fixture.RunAsync();

        Assert.Equal(CopilotAgentStopReason.Completed, result.StopReason);
        Assert.Equal(2, server.CallCount);
        Assert.Equal(2, result.Budget.ProviderCalls);
        Assert.Equal(0, result.Budget.ProviderRetryCount);
        Assert.Equal(220, result.Usage.EffectiveTotalTokens);
        Assert.Equal(220, result.Budget.ReportedTotalTokens);
        Assert.Equal(1, fixture.Tool.CallCount);
        Assert.Equal(CopilotToolExecutionState.Completed, Assert.Single(result.StepRecords).Execution.State);
        Assert.Equal(expectedDiagnostics ? 1 : 0, fixture.Events.Count(item => item.Type == CopilotAgentEventType.RuntimeDiagnostic
            && item.Text.Contains("提示缓存比较：cache_miss", StringComparison.Ordinal)));
        using var payload = JsonDocument.Parse(server.Payloads[1]);
        if (expectedDiagnostics)
            Assert.Equal("resp_test", payload.RootElement.GetProperty("prompt_cache_options").GetProperty("comparison_response_id").GetString());
        else
            Assert.False(payload.RootElement.TryGetProperty("prompt_cache_options", out _));
        fixture.AssertTurnLifecycle(result);
    }

    [Theory]
    [InlineData(false, "insufficient_quota")]
    [InlineData(true, "insufficient_quota")]
    [InlineData(false, "credit_balance_exhausted")]
    [InlineData(true, "credit_balance_exhausted")]
    [InlineData(false, "organization_spend_limit_exceeded")]
    [InlineData(true, "organization_spend_limit_exceeded")]
    [InlineData(false, "project_spend_limit_exceeded")]
    [InlineData(true, "project_spend_limit_exceeded")]
    [InlineData(false, "organization_usage_limit_exceeded")]
    [InlineData(true, "organization_usage_limit_exceeded")]
    public async Task RuntimeDoesNotRetryBillingFailuresOrReplayCompletedTools(bool responsesApi, string code)
    {
        await using var server = new LoopbackProvider(call => call == 1
            ? CompletedResponse(responsesApi, toolCall: true)
            : StructuredErrorResponse(429, code, "rate_limit_error"));
        using var fixture = new RunFixture(server, responsesApi, requestTool: true);

        var result = await fixture.RunAsync();

        Assert.Equal(CopilotAgentStopReason.ProviderFailure, result.StopReason);
        Assert.Equal(2, server.CallCount);
        Assert.Equal(2, result.Budget.ProviderCalls);
        Assert.Equal(0, result.Budget.ProviderRetryCount);
        Assert.DoesNotContain(fixture.Events, item => item.ProviderRetry != null);
        Assert.Equal(1, fixture.Tool.CallCount);
        Assert.Equal(CopilotToolExecutionState.Completed, Assert.Single(result.StepRecords).Execution.State);
        Assert.NotNull(result.SessionCheckpoint);
        Assert.Contains(result.SessionCheckpoint.TaskEventJournal.Events, item => item.Type == CopilotAgentTaskEventType.ToolCompleted);
        fixture.AssertTurnLifecycle(result);
        server.AssertRoute(responsesApi);
    }

    [Theory]
    [InlineData(false, 429, "slow_down", "rate_limit_error")]
    [InlineData(true, 429, "slow_down", "rate_limit_error")]
    [InlineData(false, 503, "server_is_overloaded", "service_unavailable_error")]
    [InlineData(true, 503, "server_is_overloaded", "service_unavailable_error")]
    public async Task SdkRetryHonorsServerDelayAndReportsSpecificError(bool responsesApi, int status, string code, string type)
    {
        await using var server = new LoopbackProvider(call => call == 1
            ? StructuredErrorResponse(status, code, type) with { Headers = "Retry-After: 7\r\n" }
            : CompletedResponse(responsesApi, toolCall: false));
        using var httpClient = server.CreateClient(responsesApi);
        var delays = new List<TimeSpan>();
        var retries = new List<CopilotProviderRetryInfo>();
        using var provider = new CopilotProviderRetryChatClient(
            CopilotOpenAiAgentChatClientFactory.Create(CreateProfile(responsesApi), httpClient), retries.Add,
            delayFactory: _ => TimeSpan.Zero, delayAsync: (delay, _) => { delays.Add(delay); return Task.CompletedTask; });

        var text = new StringBuilder();
        await foreach (var update in provider.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "Read the result.")], cancellationToken: server.Token))
            text.Append(update.Text);

        Assert.Equal("Completed answer.", text.ToString());
        Assert.Equal(2, server.CallCount);
        Assert.Equal(TimeSpan.FromSeconds(7), Assert.Single(delays));
        var retry = Assert.Single(retries);
        Assert.Equal($"HTTP {status} ({code})", retry.FailureKind);
        Assert.Equal(status, retry.StatusCode);
        server.AssertRoute(responsesApi);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task SdkLongRetryAfterStopsInsteadOfRetryingEarly(bool responsesApi, bool httpDate)
    {
        var header = httpDate ? DateTimeOffset.UtcNow.AddMinutes(10).ToString("R", CultureInfo.InvariantCulture) : "600";
        await using var server = new LoopbackProvider(_ => StructuredErrorResponse(429, "slow_down", "rate_limit_error") with
        {
            Headers = $"Retry-After: {header}\r\nx-request-id: req-long-delay\r\n",
        });
        using var httpClient = server.CreateClient(responsesApi);
        var retries = new List<CopilotProviderRetryInfo>();
        var waits = 0;
        using var provider = new CopilotProviderRetryChatClient(
            CopilotOpenAiAgentChatClientFactory.Create(CreateProfile(responsesApi), httpClient), retries.Add,
            delayAsync: (_, _) => { waits++; return Task.CompletedTask; });

        var error = await Assert.ThrowsAnyAsync<ClientResultException>(async () =>
        {
            await foreach (var _ in provider.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "Read the result.")], cancellationToken: server.Token)) { }
        });

        Assert.Equal(429, error.Status);
        Assert.Equal("req-long-delay", CopilotProviderRequestId.Find(error));
        Assert.True(CopilotProviderRetryChatClient.ResolveRetryDelay(error, TimeSpan.Zero) > TimeSpan.FromMinutes(2));
        Assert.Equal(1, server.CallCount);
        Assert.Equal(0, waits);
        Assert.Empty(retries);
    }

    private static ProviderResponse StructuredErrorResponse(int status, string code, string type)
        => new(status, "application/json", JsonSerializer.Serialize(new { error = new { code, type, message = "Controlled failure." } }));

    [Theory]
    [InlineData(false, 401)]
    [InlineData(false, 429)]
    [InlineData(false, 503)]
    [InlineData(true, 401)]
    [InlineData(true, 429)]
    [InlineData(true, 503)]
    public async Task ProductionFactoryLeavesHttpRetriesToHost(bool responsesApi, int statusCode)
    {
        await using var server = new LoopbackProvider(_ => ErrorResponse(statusCode));
        using var httpClient = server.CreateClient(responsesApi);
        using var provider = CopilotOpenAiAgentChatClientFactory.Create(CreateProfile(responsesApi), httpClient);

        var error = await Assert.ThrowsAnyAsync<ClientResultException>(() => provider.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "Summarize the supplied result.")], cancellationToken: server.Token));

        Assert.Equal(statusCode, error.Status);
        Assert.Equal(1, server.CallCount);
        server.AssertRoute(responsesApi);
    }

    [Theory]
    [InlineData(false, 429)]
    [InlineData(false, 503)]
    [InlineData(true, 429)]
    [InlineData(true, 503)]
    public async Task RuntimeRetryAccountsForEveryHttpAttempt(bool responsesApi, int statusCode)
    {
        await using var server = new LoopbackProvider(call => call == 1
            ? ErrorResponse(statusCode)
            : CompletedResponse(responsesApi, toolCall: false));
        using var fixture = new RunFixture(server, responsesApi, requestTool: false);

        var result = await fixture.RunAsync();

        Assert.Equal(CopilotAgentStopReason.Completed, result.StopReason);
        Assert.Equal(2, server.CallCount);
        Assert.Equal(server.CallCount, result.Budget.ProviderCalls);
        Assert.Equal(1, result.Budget.ProviderRetryCount);
        Assert.Equal(statusCode == 429 ? 1 : 0, result.Budget.ProviderRateLimitRetryCount);
        var retry = Assert.Single(fixture.Events, item => item.ProviderRetry != null).ProviderRetry!;
        Assert.Equal(statusCode, retry.StatusCode);
        Assert.Equal("HTTP " + statusCode, retry.FailureKind);
        Assert.Equal(110, result.Usage.EffectiveTotalTokens);
        Assert.Equal(110, result.Budget.ReportedTotalTokens);
        Assert.True(result.Budget.ConsumedTokens > result.Budget.ReportedTotalTokens);
        Assert.True(result.Budget.UsedEstimatedUsage);
        Assert.Empty(result.StepRecords);
        Assert.Equal(0, fixture.Tool.CallCount);
        server.AssertRoute(responsesApi);
        fixture.AssertTurnLifecycle(result);
    }

    [Theory]
    [InlineData(false, 401, 1)]
    [InlineData(false, 422, 1)]
    [InlineData(false, 429, 3)]
    [InlineData(false, 503, 3)]
    [InlineData(true, 401, 1)]
    [InlineData(true, 422, 1)]
    [InlineData(true, 429, 3)]
    [InlineData(true, 503, 3)]
    public async Task HttpFailureAfterToolPreservesFactsWithoutMultiplyingRetries(bool responsesApi, int statusCode, int failedAttempts)
    {
        await using var server = new LoopbackProvider(call => call == 1
            ? CompletedResponse(responsesApi, toolCall: true)
            : ErrorResponse(statusCode));
        using var fixture = new RunFixture(server, responsesApi, requestTool: true);

        var result = await fixture.RunAsync();

        Assert.Equal(CopilotAgentStopReason.ProviderFailure, result.StopReason);
        var blocker = Assert.Single(result.Blockers, item => item.Kind == CopilotAgentBlockerKind.ProviderOutput);
        Assert.Equal(statusCode is 429 or 503 ? "provider_unavailable" : "provider_request_rejected", blocker.Code);
        Assert.Contains($"HTTP {statusCode}", blocker.Summary);
        Assert.DoesNotContain("连接中断", blocker.Summary);
        Assert.Contains(fixture.Events, item => item.Type == CopilotAgentEventType.RuntimeDiagnostic
            && item.Text.Contains($"HTTP {statusCode}", StringComparison.Ordinal));
        Assert.DoesNotContain(fixture.Events, item => item.Text.Contains("starting one bounded no-tools finalization", StringComparison.Ordinal));
        Assert.True(blocker.IsStructurallyValid());
        Assert.False(blocker.RetryEligible);
        Assert.True(blocker.RequiresUserInput);
        Assert.Equal(1 + failedAttempts, server.CallCount);
        Assert.Equal(server.CallCount, result.Budget.ProviderCalls);
        Assert.Equal(failedAttempts - 1, result.Budget.ProviderRetryCount);
        Assert.Equal(statusCode == 429 ? failedAttempts - 1 : 0, result.Budget.ProviderRateLimitRetryCount);
        Assert.Equal(failedAttempts - 1, fixture.Events.Count(item => item.ProviderRetry != null));
        Assert.Equal(110, result.Usage.EffectiveTotalTokens);
        Assert.Equal(110, result.Budget.ReportedTotalTokens);
        Assert.True(result.Budget.ConsumedTokens > 110);
        Assert.True(result.Budget.UsedEstimatedUsage);
        Assert.Equal(1, fixture.Tool.CallCount);
        Assert.Equal(1, result.Budget.ToolCalls);
        Assert.Equal(CopilotToolExecutionState.Completed, Assert.Single(result.StepRecords).Execution.State);
        Assert.NotNull(result.SessionCheckpoint);
        Assert.Contains(result.SessionCheckpoint.TaskEventJournal.Events, item => item.Type == CopilotAgentTaskEventType.ToolCompleted);
        Assert.Contains(result.SessionCheckpoint.TaskEventJournal.Events, item =>
            item.Type == CopilotAgentTaskEventType.RunStopped && item.State == CopilotAgentStopReason.ProviderFailure.ToString());
        server.AssertRoute(responsesApi);
        fixture.AssertTurnLifecycle(result);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QuotaFailureAfterToolKeepsActionableCauseWithoutRetrying(bool responsesApi)
    {
        var failure = ErrorResponse(429) with
        {
            Body = JsonSerializer.Serialize(new
            {
                error = new { code = "insufficient_quota", type = "rate_limit_error", message = "Untrusted error body echoing test-key." },
            }),
        };
        await using var server = new LoopbackProvider(call => call == 1 ? CompletedResponse(responsesApi, toolCall: true) : failure);
        using var fixture = new RunFixture(server, responsesApi, requestTool: true);

        var result = await fixture.RunAsync();

        Assert.Equal(CopilotAgentStopReason.ProviderFailure, result.StopReason);
        Assert.Equal(2, server.CallCount);
        Assert.Equal(2, result.Budget.ProviderCalls);
        Assert.Equal(0, result.Budget.ProviderRetryCount);
        Assert.Equal(1, fixture.Tool.CallCount);
        var blocker = Assert.Single(result.Blockers, item => item.Kind == CopilotAgentBlockerKind.ProviderOutput);
        Assert.Equal("provider_request_rejected", blocker.Code);
        Assert.Contains("HTTP 429 / insufficient_quota", blocker.Summary);
        Assert.Contains("账户限制", blocker.Summary);
        Assert.False(blocker.RetryEligible);
        Assert.True(blocker.RequiresUserInput);
        Assert.DoesNotContain(fixture.Events, item => item.Text.Contains("test-key", StringComparison.Ordinal));
        Assert.NotNull(result.SessionCheckpoint);
        Assert.Contains(result.SessionCheckpoint.TaskEventJournal.Events, item =>
            item.Type == CopilotAgentTaskEventType.BlockerDetected && item.State == blocker.Code);
    }

    [Theory]
    [InlineData(false, 401, 1)]
    [InlineData(false, 503, 3)]
    [InlineData(true, 401, 1)]
    [InlineData(true, 503, 3)]
    [InlineData(false, 413, 1)]
    [InlineData(true, 413, 1)]
    public async Task HttpFailureDuringNoToolsFinalizationPreservesCauseAndCompletedTool(bool responsesApi, int statusCode, int failedAttempts)
    {
        var empty = CompletedResponse(responsesApi, toolCall: false);
        empty = empty with { Body = empty.Body.Replace("Completed answer.", "", StringComparison.Ordinal) };
        await using var server = new LoopbackProvider(call => call switch
        {
            1 => CompletedResponse(responsesApi, toolCall: true),
            2 => empty,
            _ => ErrorResponse(statusCode),
        });
        using var fixture = new RunFixture(server, responsesApi, requestTool: true);

        var result = await fixture.RunAsync();

        Assert.Equal(CopilotAgentStopReason.ProviderFailure, result.StopReason);
        Assert.Equal(2 + failedAttempts, server.CallCount);
        Assert.Equal(server.CallCount, result.Budget.ProviderCalls);
        Assert.Equal(failedAttempts - 1, result.Budget.ProviderRetryCount);
        // This finalization prompt has no older conversation groups to compact; 413 must not resend the same input.
        Assert.Equal(0, result.Budget.ContextRecoveryCount);
        Assert.Equal(220, result.Usage.EffectiveTotalTokens);
        Assert.Equal(1, fixture.Tool.CallCount);
        Assert.Equal(CopilotToolExecutionState.Completed, Assert.Single(result.StepRecords).Execution.State);
        var blocker = Assert.Single(result.Blockers, item => item.Kind == CopilotAgentBlockerKind.ProviderOutput);
        Assert.Equal(statusCode switch { 413 => "provider_context_window", 503 => "provider_unavailable", _ => "provider_request_rejected" }, blocker.Code);
        Assert.Contains(statusCode == 413 ? "上下文" : $"HTTP {statusCode}", blocker.Summary);
        Assert.Contains(fixture.Events, item => item.Type == CopilotAgentEventType.AnswerDelta && item.Text.Contains(blocker.Summary, StringComparison.Ordinal));
        foreach (var payload in server.Payloads.Skip(2))
        {
            using var document = JsonDocument.Parse(payload);
            Assert.False(document.RootElement.TryGetProperty("tools", out var tools) && tools.GetArrayLength() > 0);
        }
        Assert.NotNull(result.SessionCheckpoint);
        Assert.Contains(result.SessionCheckpoint.TaskEventJournal.Events, item =>
            item.Type == CopilotAgentTaskEventType.ToolCompleted);
        Assert.Contains(result.SessionCheckpoint.TaskEventJournal.Events, item =>
            item.Type == CopilotAgentTaskEventType.BlockerDetected && item.State == blocker.Code);
        server.AssertRoute(responsesApi);
        fixture.AssertTurnLifecycle(result);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SdkHttpErrorRetainsRetryAfterRequestIdAndAuthenticationClassification(bool responsesApi)
    {
        await using var server = new LoopbackProvider(_ => ErrorResponse(401) with
        {
            Headers = "Retry-After: 7\r\nx-request-id: req-openai-controlled\r\n",
        });
        using var httpClient = server.CreateClient(responsesApi);
        using var provider = CopilotOpenAiAgentChatClientFactory.Create(CreateProfile(responsesApi), httpClient);

        var error = await Assert.ThrowsAnyAsync<ClientResultException>(() => provider.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "Summarize the result.")], cancellationToken: server.Token));

        Assert.Equal(401, error.Status);
        Assert.Equal(1, server.CallCount);
        Assert.False(CopilotProviderRetryChatClient.TryClassifyTransientFailure(error, server.Token, out _, out var statusCode));
        Assert.Equal(401, statusCode);
        Assert.False(CopilotContextWindowFailureClassifier.TryClassify(error, out _));
        Assert.Equal(TimeSpan.FromSeconds(7), CopilotProviderRetryChatClient.ResolveRetryDelay(error, TimeSpan.FromMilliseconds(250)));
        var rawResponse = error.GetRawResponse();
        Assert.NotNull(rawResponse);
        Assert.True(rawResponse.Headers.TryGetValue("x-request-id", out var requestId));
        Assert.Equal("req-openai-controlled", requestId);
        Assert.Contains("Controlled HTTP failure", error.Message, StringComparison.Ordinal);
        server.AssertRoute(responsesApi);
    }

    private sealed class RunFixture : IDisposable
    {
        private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("CopilotOpenAiProviderRetryTests-");
        private readonly LoopbackProvider _server;
        private readonly HttpClient _httpClient;
        private readonly CopilotMicrosoftAgentFrameworkRuntime _runtime;
        private readonly CopilotAgentRequest _request;

        public RunFixture(LoopbackProvider server, bool responsesApi, bool requestTool)
        {
            _server = server;
            _httpClient = server.CreateClient(responsesApi);
            var catalog = new CopilotCapabilityCatalog();
            catalog.PublishSource(CopilotCapabilitySourceKind.BuiltIn, "openai-retry-tests", "OpenAI retry tests", [Tool]);
            _runtime = new CopilotMicrosoftAgentFrameworkRuntime(
                new CopilotToolRegistry([Tool]),
                new CopilotAgentContextBuilder(),
                new CopilotToolExecutor(),
                profile => CopilotOpenAiAgentChatClientFactory.Create(profile, _httpClient),
                new EmptyExternalToolProvider(),
                catalog,
                new CopilotAgentSkillUsageStore(_directory.FullName));
            var prompt = requestTool
                ? "Run the bounded workspace validation probe and summarize its result."
                : "Summarize the supplied result.";
            _request = new CopilotAgentRequest
            {
                Profile = CreateProfile(responsesApi),
                ConversationId = "openai-retry-conversation",
                TaskId = "openai-retry-task",
                WorkspacePath = _directory.FullName,
                UserText = prompt,
                TaskIntentText = prompt,
                Mode = requestTool ? CopilotAgentMode.Code : CopilotAgentMode.Auto,
                HarnessFeatures = CopilotAgentHarnessFeatures.None,
                CodexApprovalPolicy = CopilotCodexApprovalPolicy.CreateScalar(CopilotCodexApprovalPolicyMode.Untrusted),
                RunBudgetOverride = new CopilotAgentRunBudgetOverride
                {
                    RequestTokenBudget = 32_768,
                    MaxToolCalls = 2,
                    MaxAgentPasses = 1,
                    TotalDuration = TimeSpan.FromSeconds(60),
                },
            };
        }

        public ValidationProbeTool Tool { get; } = new();
        public CopilotProfileConfig Profile => _request.Profile;
        public List<CopilotAgentEvent> Events { get; } = [];
        public Task<CopilotAgentRunResult> RunAsync() => _runtime.RunAsync(_request, Events.Add, _server.Token);

        public void AssertTurnLifecycle(CopilotAgentRunResult result)
        {
            var turn = CopilotTurnEventReducer.Reduce(CopilotTurnEventState.Create(_request.Mode), new CopilotTurnStartedEvent(_request.Mode));
            var sink = new CopilotTurnEventSink(turnEvent => turn = CopilotTurnEventReducer.Reduce(turn, turnEvent));
            foreach (var agentEvent in Events)
            {
                sink.OnAgentEvent(agentEvent);
                if (agentEvent.Type == CopilotAgentEventType.BudgetUpdated)
                    sink.OnTokenUsageUpdated(CopilotTurnRuntime.GetReportedTokenUsage(agentEvent.Budget!));
            }
            sink.OnPlanUpdated(CopilotTurnPlanSnapshot.FromTaskLedger(result.TaskLedger));
            sink.OnTokenUsageUpdated(result.Usage);
            var completed = CopilotTurnResult.FromAgent(_request.Mode, result.Usage, result);
            turn = CopilotTurnEventReducer.Reduce(turn, new CopilotTurnCompletedEvent(completed));
            Assert.Same(completed, CopilotTurnEventReducer.RequireCompletion(turn));
        }

        public void Dispose()
        {
            _httpClient.Dispose();
            _directory.Delete(recursive: true);
        }
    }

    private static CopilotProfileConfig CreateProfile(bool responsesApi) => new()
    {
        ProviderType = CopilotProviderType.OpenAICompatible,
        VendorType = responsesApi ? CopilotVendorType.OpenAI : CopilotVendorType.Custom,
        ApiKey = "test-key",
        BaseUrl = responsesApi ? "https://api.openai.com/v1" : "https://example.test/v1",
        Model = responsesApi ? "gpt-5.5" : "test-model",
        MaxTokens = 4_096,
    };

    private static ProviderResponse ErrorResponse(int statusCode) => new(statusCode, "application/json", JsonSerializer.Serialize(new
    {
        error = new
        {
            type = statusCode switch { 401 => "authentication_error", 429 => "rate_limit_error", _ => "server_error" },
            message = "Controlled HTTP failure.",
            code = statusCode.ToString(CultureInfo.InvariantCulture),
        },
    }));

    private static string Event(object payload) => "data: " + JsonSerializer.Serialize(payload) + "\n\n";

    private static ProviderResponse CompletedResponse(bool responsesApi, bool toolCall)
    {
        if (!responsesApi)
        {
            object delta = toolCall
                ? new { role = "assistant", tool_calls = new[] { new { index = 0, id = "validation-call", type = "function", function = new { name = "colorvision_run_workspace_validation", arguments = "{}" } } } }
                : new { role = "assistant", content = "Completed answer." };
            var content = Event(new
            {
                id = "chatcmpl_test", @object = "chat.completion.chunk", created = 1234567890, model = "test-model",
                choices = new[] { new { index = 0, delta, finish_reason = (string?)null } },
            });
            var finish = Event(new
            {
                id = "chatcmpl_test", @object = "chat.completion.chunk", created = 1234567890, model = "test-model",
                choices = new[] { new { index = 0, delta = new { }, finish_reason = toolCall ? "tool_calls" : "stop" } },
            });
            var usage = Event(new
            {
                id = "chatcmpl_test", @object = "chat.completion.chunk", created = 1234567890, model = "test-model",
                choices = Array.Empty<object>(), usage = new { prompt_tokens = 100, completion_tokens = 10, total_tokens = 110 },
            });
            return new ProviderResponse(200, "text/event-stream", content + finish + usage + "data: [DONE]\n\n");
        }

        var start = Event(new
        {
            type = "response.created", sequence_number = 0,
            response = new { id = "resp_test", @object = "response", created_at = 1234567890, model = "gpt-5.5", status = "in_progress", output = Array.Empty<object>() },
        });
        object completedItem;
        string updates;
        if (toolCall)
        {
            var item = new { type = "function_call", id = "fc_test", call_id = "validation-call", name = "colorvision_run_workspace_validation", arguments = "", status = "in_progress" };
            completedItem = new { type = "function_call", id = "fc_test", call_id = "validation-call", name = "colorvision_run_workspace_validation", arguments = "{}", status = "completed" };
            updates = Event(new { type = "response.output_item.added", sequence_number = 1, output_index = 0, item })
                + Event(new { type = "response.function_call_arguments.delta", sequence_number = 2, item_id = "fc_test", output_index = 0, delta = "{}" })
                + Event(new { type = "response.function_call_arguments.done", sequence_number = 3, item_id = "fc_test", output_index = 0, name = "colorvision_run_workspace_validation", arguments = "{}" })
                + Event(new { type = "response.output_item.done", sequence_number = 4, output_index = 0, item = completedItem });
        }
        else
        {
            completedItem = new { type = "message", id = "msg_test", role = "assistant", status = "completed", content = new[] { new { type = "output_text", text = "Completed answer.", annotations = Array.Empty<object>() } } };
            updates = Event(new { type = "response.output_text.delta", sequence_number = 1, item_id = "msg_test", output_index = 0, content_index = 0, delta = "Completed answer." });
        }
        var completed = Event(new
        {
            type = "response.completed", sequence_number = 5,
            response = new
            {
                id = "resp_test", @object = "response", created_at = 1234567890, model = "gpt-5.5", status = "completed", output = new[] { completedItem },
                usage = new { input_tokens = 100, output_tokens = 10, total_tokens = 110, input_tokens_details = new { cached_tokens = 0 }, output_tokens_details = new { reasoning_tokens = 0 } },
            },
        });
        return new ProviderResponse(200, "text/event-stream", start + updates + completed + "data: [DONE]\n\n");
    }

    private sealed record ProviderResponse(int StatusCode, string ContentType, string Body, string Headers = "");

    private sealed class LoopbackProvider : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _lifetime = new(TimeSpan.FromSeconds(60));
        private readonly Func<int, ProviderResponse> _response;
        private readonly Task _serveTask;
        private readonly List<string> _requestLines = [];
        private int _callCount;

        public LoopbackProvider(Func<int, ProviderResponse> response)
        {
            _response = response;
            _listener.Start();
            BaseUri = new Uri("http://127.0.0.1:" + ((IPEndPoint)_listener.LocalEndpoint).Port);
            _serveTask = ServeAsync();
        }

        public Uri BaseUri { get; }
        public CancellationToken Token => _lifetime.Token;
        public int CallCount => Volatile.Read(ref _callCount);
        public List<string> Payloads { get; } = [];
        public HttpClient CreateClient(bool responsesApi) => new(new LoopbackRedirectHandler(BaseUri, responsesApi));

        public void AssertRoute(bool responsesApi)
        {
            Assert.NotEmpty(_requestLines);
            Assert.All(_requestLines, line => Assert.Equal(responsesApi ? "POST /v1/responses HTTP/1.1" : "POST /v1/chat/completions HTTP/1.1", line));
        }

        private async Task ServeAsync()
        {
            try
            {
                while (true)
                {
                    using var client = await _listener.AcceptTcpClientAsync(Token);
                    await using var stream = client.GetStream();
                    using var reader = new StreamReader(stream, Encoding.Latin1, false, 1024, leaveOpen: true);
                    _requestLines.Add(await reader.ReadLineAsync(Token) ?? throw new EndOfStreamException());
                    var contentLength = 0;
                    while (true)
                    {
                        var line = await reader.ReadLineAsync(Token) ?? throw new EndOfStreamException();
                        if (line.Length == 0)
                            break;
                        if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                            contentLength = int.Parse(line[15..].Trim(), CultureInfo.InvariantCulture);
                    }
                    if (contentLength is < 0 or > 1024 * 1024)
                        throw new InvalidDataException("Unexpected loopback request length.");
                    var remaining = contentLength;
                    var requestBody = new StringBuilder();
                    var buffer = new char[4096];
                    while (remaining > 0)
                    {
                        var count = await reader.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, remaining)), Token);
                        if (count == 0)
                            throw new EndOfStreamException();
                        requestBody.Append(buffer, 0, count);
                        remaining -= count;
                    }
                    Payloads.Add(requestBody.ToString());
                    var response = _response(Interlocked.Increment(ref _callCount));
                    var body = Encoding.UTF8.GetBytes(response.Body);
                    var headers = Encoding.ASCII.GetBytes(
                        "HTTP/1.1 " + response.StatusCode + " Controlled\r\n"
                        + "Content-Type: " + response.ContentType + "\r\n"
                        + "Content-Length: " + body.Length + "\r\n"
                        + response.Headers + "Connection: close\r\n\r\n");
                    await stream.WriteAsync(headers, Token);
                    await stream.WriteAsync(body, Token);
                    await stream.FlushAsync(Token);
                }
            }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
            catch (SocketException) when (_lifetime.IsCancellationRequested) { }
        }

        public async ValueTask DisposeAsync()
        {
            _lifetime.Cancel();
            _listener.Stop();
            try { await _serveTask; }
            finally { _lifetime.Dispose(); }
        }
    }

    private sealed class LoopbackRedirectHandler : DelegatingHandler
    {
        private readonly Uri _loopback;
        private readonly string _expectedHost;

        public LoopbackRedirectHandler(Uri loopback, bool responsesApi)
            : base(new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false, UseCookies = false })
        {
            _loopback = loopback;
            _expectedHost = responsesApi ? "api.openai.com" : "example.test";
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri ?? throw new InvalidOperationException("Missing request URI.");
            if (uri.Host != _expectedHost && uri.GetLeftPart(UriPartial.Authority) != _loopback.GetLeftPart(UriPartial.Authority))
                throw new InvalidOperationException("Unexpected provider destination.");
            // Keep production's official-host routing while sending every byte only to the controlled local server.
            request.RequestUri = new Uri(_loopback, uri.PathAndQuery);
            return base.SendAsync(request, cancellationToken);
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
