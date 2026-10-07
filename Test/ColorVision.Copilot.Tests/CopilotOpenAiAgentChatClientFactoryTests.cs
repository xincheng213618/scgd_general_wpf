#pragma warning disable MAAI001
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotOpenAiAgentChatClientFactoryTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ArgumentFragmentsAreProgressWhileMetadataOnlyStreamsRemainBounded(bool argumentProgress)
    {
        var arguments = JsonSerializer.Serialize(new { path = "fixture/" + new string('a', 96) + ".txt" });
        var item = new { type = "function_call", id = "fc_fragmented", call_id = "call_fragmented", name = "read_file", arguments, status = "completed" };
        var response = new { id = "resp_fragmented", @object = "response", created_at = 1234567890, model = "deepseek-flash", status = "completed", output = new[] { item } };
        var stream = new StringBuilder();
        var sequence = 0;
        var started = new { response.id, response.@object, response.created_at, response.model, status = "in_progress", output = Array.Empty<object>() };
        stream.Append("data: ").Append(JsonSerializer.Serialize(new { type = "response.created", sequence_number = sequence++, response = started })).Append("\n\n");
        if (argumentProgress)
            stream.Append("data: ").Append(JsonSerializer.Serialize(new { type = "response.output_item.added", sequence_number = sequence++, output_index = 0,
                item = new { item.type, item.id, item.call_id, item.name, arguments = "", status = "in_progress" } })).Append("\n\n");
        foreach (var character in arguments)
        {
            object update = argumentProgress
                ? new { type = "response.function_call_arguments.delta", sequence_number = sequence++, item_id = item.id, output_index = 0, delta = character.ToString() }
                : new { type = "response.in_progress", sequence_number = sequence++, response = started };
            stream.Append("data: ").Append(JsonSerializer.Serialize(update)).Append("\n\n");
        }
        stream.Append("data: ").Append(JsonSerializer.Serialize(new { type = "response.output_item.done", sequence_number = sequence++, output_index = 0, item })).Append("\n\n");
        stream.Append("data: ").Append(JsonSerializer.Serialize(new { type = "response.completed", sequence_number = sequence, response })).Append("\n\n");
        using var handler = new CapturingHandler(stream.ToString());
        using var httpClient = new HttpClient(handler);
        using var transport = CopilotOpenAiAgentChatClientFactory.Create(CreateProfile(CopilotVendorType.DeepSeek, "https://api.deepseek.com/responses", "deepseek-flash"), httpClient);
        using var recovery = new CopilotProviderConnectionRecoveryChatClient(transport);
        using var client = new CopilotProviderRetryChatClient(recovery);
        var run = client.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "Read the selected file.")], CreateToolOptions()).ToChatResponseAsync();
        if (argumentProgress)
        {
            var result = await run;
            var call = Assert.Single(result.Messages.SelectMany(message => message.Contents).OfType<FunctionCallContent>());
            Assert.Equal("call_fragmented", call.CallId);
            Assert.Equal("fixture/" + new string('a', 96) + ".txt", call.Arguments!["path"]?.ToString());
        }
        else Assert.Contains("metadata-only", (await Assert.ThrowsAsync<InvalidOperationException>(() => run)).Message);
        Assert.Single(handler.Payloads);
    }

    private const string PlainReasoningResponseJson =
        """
        {"id":"resp_plain","object":"response","created_at":1234567890,"model":"deepseek-flash","status":"completed","output":[{"type":"reasoning","id":"rs_plain","summary":[],"content":[{"type":"reasoning_text","text":"Inspect the selected file before answering."}]},{"type":"function_call","id":"fc_plain","call_id":"call_plain","name":"read_file","arguments":"{\"path\":\"evidence.txt\"}","status":"completed"}],"usage":{"input_tokens":12,"output_tokens":8,"total_tokens":20,"input_tokens_details":{"cached_tokens":0},"output_tokens_details":{"reasoning_tokens":4}}}
        """;

    private static string PlainReasoningResponseStream =>
        """
        data: {"type":"response.created","sequence_number":0,"response":{"id":"resp_plain","object":"response","created_at":1234567890,"model":"deepseek-flash","status":"in_progress","output":[]}}

        data: {"type":"response.output_item.added","sequence_number":1,"output_index":0,"item":{"type":"reasoning","id":"rs_plain","summary":[],"content":[]}}

        data: {"type":"response.reasoning_text.delta","sequence_number":2,"item_id":"rs_plain","output_index":0,"content_index":0,"delta":"Inspect the selected file "}

        data: {"type":"response.reasoning_text.delta","sequence_number":3,"item_id":"rs_plain","output_index":0,"content_index":0,"delta":"before answering."}

        data: {"type":"response.output_item.done","sequence_number":4,"output_index":0,"item":{"type":"reasoning","id":"rs_plain","summary":[],"content":[{"type":"reasoning_text","text":"Inspect the selected file before answering."}]}}

        data: {"type":"response.output_item.done","sequence_number":5,"output_index":1,"item":{"type":"function_call","id":"fc_plain","call_id":"call_plain","name":"read_file","arguments":"{\"path\":\"evidence.txt\"}","status":"completed"}}

        """ + "\n\ndata: {\"type\":\"response.completed\",\"sequence_number\":6,\"response\":" + PlainReasoningResponseJson + "}\n\n";

    [Theory]
    [InlineData(true, false, "keep")]
    [InlineData(true, true, "keep")]
    [InlineData(false, false, "keep")]
    [InlineData(false, true, "keep")]
    [InlineData(true, true, "compact")]
    [InlineData(false, true, "compact")]
    [InlineData(true, true, "remove")]
    [InlineData(false, true, "remove")]
    public async Task PlainReasoningKeepsItsContentAcrossToolCallsAndSavedHistory(bool streamFirst, bool serialize, string historyChange)
    {
        using var handler = new CapturingHandler(new[]
        {
            (HttpStatusCode.OK, streamFirst ? PlainReasoningResponseStream : PlainReasoningResponseJson, streamFirst ? "text/event-stream" : "application/json"),
            (HttpStatusCode.OK, TextResponseStream, "text/event-stream"),
        });
        using var httpClient = new HttpClient(handler);
        using var client = CopilotOpenAiAgentChatClientFactory.Create(
            CreateProfile(CopilotVendorType.DeepSeek, "https://api.deepseek.com/responses", "deepseek-flash"), httpClient);
        var messages = new List<ChatMessage> { new(ChatRole.User, "Inspect the file.") };
        var first = streamFirst ? await client.GetStreamingResponseAsync(messages, CreateToolOptions()).ToChatResponseAsync()
            : await client.GetResponseAsync(messages, CreateToolOptions());
        var reasoning = Assert.Single(first.Messages.SelectMany(message => message.Contents).OfType<TextReasoningContent>());
        Assert.Equal("Inspect the selected file before answering.", reasoning.Text);
        messages.AddRange(serialize ? JsonSerializer.Deserialize<List<ChatMessage>>(JsonSerializer.Serialize(first.Messages, AIJsonUtilities.DefaultOptions), AIJsonUtilities.DefaultOptions)! : first.Messages);
        foreach (var message in messages)
        {
            foreach (var saved in message.Contents.OfType<TextReasoningContent>().ToArray())
            {
                if (historyChange == "compact") saved.Text = "Compacted reasoning.";
                else if (historyChange == "remove") message.Contents.Remove(saved);
            }
        }
        messages.Add(new(ChatRole.Tool, [new FunctionResultContent("call_plain", "Observed evidence.")]));
        var beforeReplay = JsonSerializer.Serialize(messages, AIJsonUtilities.DefaultOptions);
        var response = await client.GetStreamingResponseAsync(messages, CreateToolOptions()).ToChatResponseAsync();
        Assert.Equal("Responses adapter OK.", response.Text);
        Assert.Equal(beforeReplay, JsonSerializer.Serialize(messages, AIJsonUtilities.DefaultOptions));
        using var replay = JsonDocument.Parse(handler.LastPayload);
        var input = replay.RootElement.GetProperty("input").EnumerateArray().ToArray();
        Assert.Contains(input, item => item.GetProperty("type").GetString() == "function_call_output" && item.GetProperty("call_id").GetString() == "call_plain");
        if (historyChange == "remove")
        {
            Assert.DoesNotContain(input, item => item.GetProperty("type").GetString() == "reasoning");
            return;
        }
        var item = Assert.Single(input, item => item.GetProperty("type").GetString() == "reasoning");
        Assert.Equal("rs_plain", item.GetProperty("id").GetString());
        var content = Assert.Single(item.GetProperty("content").EnumerateArray());
        Assert.Equal("reasoning_text", content.GetProperty("type").GetString());
        Assert.Equal(historyChange == "compact" ? "Compacted reasoning." : reasoning.Text, content.GetProperty("text").GetString());
        Assert.Empty(item.GetProperty("summary").EnumerateArray());
        Assert.False(item.TryGetProperty("encrypted_content", out _));
        Assert.Equal("function_call", input[Array.IndexOf(input, item) + 1].GetProperty("type").GetString());
    }

    [Fact]
    public async Task ExistingSummaryIsNotReclassifiedFromTheEndpointOrItsText()
    {
        using var handler = new CapturingHandler(TextResponseStream);
        using var httpClient = new HttpClient(handler);
        using var client = CopilotOpenAiAgentChatClientFactory.Create(
            CreateProfile(CopilotVendorType.DeepSeek, "https://api.deepseek.com/responses", "deepseek-flash"), httpClient);
        await client.GetStreamingResponseAsync([
            new ChatMessage(ChatRole.Assistant, [new TextReasoningContent("Existing summary.")]),
            new ChatMessage(ChatRole.User, "Continue."),
        ]).ToChatResponseAsync();
        using var replay = JsonDocument.Parse(handler.LastPayload);
        var item = Assert.Single(replay.RootElement.GetProperty("input").EnumerateArray(), item => item.GetProperty("type").GetString() == "reasoning");
        Assert.False(item.TryGetProperty("content", out _));
        Assert.Equal("Existing summary.", Assert.Single(item.GetProperty("summary").EnumerateArray()).GetProperty("text").GetString());
    }

    [Fact]
    public async Task HarnessCheckpointRetainsPlainReasoningWithoutReexecutingCompletedTools()
    {
        using var handler = new CapturingHandler(PlainReasoningResponseStream, TextResponseStream, TextResponseStream);
        using var httpClient = new HttpClient(handler);
        using var client = CopilotOpenAiAgentChatClientFactory.Create(
            CreateProfile(CopilotVendorType.DeepSeek, "https://api.deepseek.com/responses", "deepseek-flash"), httpClient);
        var reads = 0;
        AIAgent CreateHarness() => client.AsHarnessAgent(new HarnessAgentOptions
        {
            Name = "PlainReasoningCheckpoint", DisableCompaction = true, DisableFileMemory = true,
            DisableWebSearch = true, DisableTodoProvider = true, DisableAgentModeProvider = true,
            DisableAgentSkillsProvider = true, DisableToolAutoApproval = true, DisableOpenTelemetry = true,
            MaximumIterationsPerRequest = 3,
            ChatOptions = new() { Tools = [AIFunctionFactory.Create((string path) => { reads++; return "Observed evidence."; }, "read_file")] },
        });
        var original = CreateHarness();
        var session = await original.CreateSessionAsync();
        await foreach (var _ in original.RunStreamingAsync([new ChatMessage(ChatRole.User, "Read the selected file.")], session)) { }
        Assert.Equal(1, reads);
        var checkpoint = await original.SerializeSessionAsync(session);
        var restored = CreateHarness();
        var restoredSession = await restored.DeserializeSessionAsync(checkpoint);
        await foreach (var _ in restored.RunStreamingAsync([new ChatMessage(ChatRole.User, "Continue using the observed evidence.")], restoredSession)) { }
        Assert.Equal(1, reads);
        Assert.Equal(3, handler.Payloads.Count);
        using var payload = JsonDocument.Parse(handler.LastPayload);
        var input = payload.RootElement.GetProperty("input").EnumerateArray().ToArray();
        var reasoning = Assert.Single(input, item => item.GetProperty("type").GetString() == "reasoning");
        Assert.Equal("rs_plain", reasoning.GetProperty("id").GetString());
        Assert.Equal("Inspect the selected file before answering.", Assert.Single(reasoning.GetProperty("content").EnumerateArray()).GetProperty("text").GetString());
        Assert.Single(input, item => item.GetProperty("type").GetString() == "function_call" && item.GetProperty("call_id").GetString() == "call_plain");
        Assert.Single(input, item => item.GetProperty("type").GetString() == "function_call_output" && item.GetProperty("call_id").GetString() == "call_plain");
    }

    [Theory]
    [InlineData(true, "server_error")]
    [InlineData(false, "server_error")]
    [InlineData(true, "insufficient_quota")]
    [InlineData(false, "insufficient_quota")]
    public async Task FailedResponsesCannotDispatchToolsAndSurfaceProviderFailure(bool streaming, string errorCode)
    {
        const string functionName = "failed_tool";
        var profile = CreateProfile(CopilotVendorType.OpenAI, "https://api.openai.com/v1/responses", "gpt-5.5");
        var failedResponse = CreateFailedFunctionCallResponse(functionName, errorCode, "Synthetic terminal provider failure. Fake credential: " + profile.ApiKey);
        using var handler = new CapturingHandler(HttpStatusCode.OK,
            streaming ? CreateTerminalResponseStream(failedResponse) : failedResponse,
            streaming ? "text/event-stream" : "application/json");
        using var httpClient = new HttpClient(handler);
        using var transport = CopilotOpenAiAgentChatClientFactory.Create(profile, httpClient);
        using var client = new FunctionInvokingChatClient(new CopilotIncompleteToolCallGuardChatClient(transport));
        var executions = 0;
        var options = new ChatOptions { Tools = [AIFunctionFactory.Create(() => { executions++; return "executed"; }, functionName)] };
        ChatMessage[] messages = [new(ChatRole.User, "Observe the supplied data.")];
        var failure = await Assert.ThrowsAsync<CopilotProviderPayloadException>(async () => _ = streaming
            ? await client.GetStreamingResponseAsync(messages, options).ToChatResponseAsync()
            : await client.GetResponseAsync(messages, options));
        Assert.Equal(0, executions);
        Assert.Single(handler.Payloads);
        Assert.Equal(errorCode, failure.ErrorCode);
        Assert.Equal(errorCode == "server_error", failure.IsTransient);
        Assert.DoesNotContain(profile.ApiKey, failure.Message, StringComparison.Ordinal);
        Assert.Empty(failure.RequestId);
        Assert.Equal(12, failure.ReportedUsage.InputTokens);
        Assert.Equal(8, failure.ReportedUsage.OutputTokens);
        Assert.Equal(20, failure.ReportedUsage.EffectiveTotalTokens);
        Assert.Equal(3, failure.ReportedUsage.CachedInputTokens);
    }

    [Fact]
    public async Task RuntimeRetriesFailedResponseBeforeOutputAndAccountsForBothAttempts()
    {
        using var handler = new CapturingHandler(
            CreateTerminalResponseStream(CreateFailedFunctionCallResponse(null, "server_error")), TextResponseStream);
        var tool = new IncompleteCheckpointProbeTool();
        var events = new List<CopilotAgentEvent>();
        var result = await RunCapturedResponseRuntimeAsync(handler, tool, events);
        Assert.Equal(CopilotAgentStopReason.Completed, result.StopReason);
        Assert.Equal(0, tool.ExecutionCount);
        Assert.Equal(2, handler.Payloads.Count);
        Assert.Equal(2, result.Budget.ProviderCalls);
        Assert.Equal(1, result.Budget.ProviderRetryCount);
        Assert.Equal(35, result.Usage.EffectiveTotalTokens);
        Assert.Equal(result.Usage.EffectiveTotalTokens, result.Budget.ReportedTotalTokens);
        Assert.Single(events, item => item.ProviderRetry != null);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NonStreamingFailedAttemptsRetainTheirUsageAcrossRetry(bool quotaFailure)
    {
        using var handler = new CapturingHandler(new[]
        {
            (HttpStatusCode.OK, CreateFailedFunctionCallResponse(null, "server_error"), "application/json"),
            (HttpStatusCode.OK, quotaFailure ? CreateFailedFunctionCallResponse(null, "insufficient_quota") : BilledTextResponseJson, "application/json"),
        });
        using var httpClient = new HttpClient(handler);
        using var transport = CopilotOpenAiAgentChatClientFactory.Create(
            CreateProfile(CopilotVendorType.OpenAI, "https://api.openai.com/v1/responses", "gpt-5.5"), httpClient);
        using var budget = new CopilotTokenBudgetChatClient(transport, new CopilotAgentTokenBudget
        {
            ContextWindowTokens = CopilotAgentTokenBudget.MinimumContextWindowTokens,
            RequestTokenBudget = 4_096, MaxOutputTokens = 128,
        });
        using var retry = new CopilotProviderRetryChatClient(budget, budget.RecordProviderRetry,
            delayFactory: _ => TimeSpan.Zero, delayAsync: (_, _) => Task.CompletedTask);
        using var client = new FunctionInvokingChatClient(new CopilotIncompleteToolCallGuardChatClient(retry));
        var executions = 0;
        var options = new ChatOptions { Tools = [AIFunctionFactory.Create(() => { executions++; return "executed"; }, "billing_tool")] };
        ChatMessage[] messages = [new(ChatRole.User, "Summarize the supplied notes.")];
        if (quotaFailure)
        {
            var failure = await Assert.ThrowsAsync<CopilotProviderPayloadException>(() => client.GetResponseAsync(messages, options));
            Assert.Equal("insufficient_quota", failure.ErrorCode);
            var usage = CopilotProviderRetryChatClient.ExtractFailureUsage(failure);
            Assert.Equal(40, usage.EffectiveTotalTokens);
            Assert.Equal(6, usage.CachedInputTokens);
        }
        else
        {
            var response = await client.GetResponseAsync(messages, options);
            Assert.Equal("The reported response succeeded.", response.Text);
            Assert.Equal("resp_retry_success", response.ResponseId);
            Assert.Equal(35, response.Usage?.TotalTokenCount);
            Assert.Equal(22, response.Usage?.InputTokenCount);
            Assert.Equal(13, response.Usage?.OutputTokenCount);
            Assert.Equal(5, response.Usage?.CachedInputTokenCount);
            Assert.Equal(1, response.Usage?.ReasoningTokenCount);
        }
        Assert.Equal(0, executions);
        Assert.Equal(2, handler.Payloads.Count);
        Assert.Equal(2, budget.Snapshot.ProviderCalls);
        Assert.Equal(1, budget.Snapshot.ProviderRetryCount);
        Assert.Equal(quotaFailure ? 40 : 35, budget.Snapshot.ReportedTotalTokens);
        Assert.Equal(quotaFailure ? 6 : 5, budget.Snapshot.ReportedCachedInputTokens);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(true, false)]
    public async Task ContextFailureBeforeContentRecoversOnceWithOnlyReportedBilling(bool streaming, bool hasReportedUsage)
    {
        var failedResponse = CreateFailedFunctionCallResponse(null, "context_length_exceeded", "Synthetic maximum context length exceeded.");
        if (!hasReportedUsage)
        {
            var unbilledResponse = System.Text.Json.Nodes.JsonNode.Parse(failedResponse)!;
            unbilledResponse["usage"] = null;
            failedResponse = unbilledResponse.ToJsonString();
        }
        using var handler = new CapturingHandler(new[]
        {
            (HttpStatusCode.OK, streaming ? CreateTerminalResponseStream(failedResponse) : failedResponse,
                streaming ? "text/event-stream" : "application/json"),
            (HttpStatusCode.OK, streaming ? TextResponseStream : BilledTextResponseJson,
                streaming ? "text/event-stream" : "application/json"),
        });
        using var httpClient = new HttpClient(handler);
        using var transport = CopilotOpenAiAgentChatClientFactory.Create(
            CreateProfile(CopilotVendorType.OpenAI, "https://api.openai.com/v1/responses", "gpt-5.5"), httpClient);
        using var budget = new CopilotTokenBudgetChatClient(
            new CopilotProviderInactivityChatClient(new CopilotCancellationGuardChatClient(transport)),
            new CopilotAgentTokenBudget { ContextWindowTokens = 65_536, RequestTokenBudget = 131_072, MaxOutputTokens = 128 });
        using var retry = new CopilotProviderRetryChatClient(budget, budget.RecordProviderRetry,
            delayAsync: (_, _) => throw new InvalidOperationException("A context rejection must use compaction instead of transient retry."));
        var recoveries = new List<CopilotContextWindowRecoveryInfo>();
        using var recovery = new CopilotContextWindowRecoveryChatClient(retry, budget.Snapshot.InputBudgetTokens, info =>
        {
            budget.RecordContextRecovery(info);
            recoveries.Add(info);
        });
        using var client = new FunctionInvokingChatClient(new CopilotIncompleteToolCallGuardChatClient(recovery));
        var executions = 0;
        var options = new ChatOptions { Tools = [AIFunctionFactory.Create(() => { executions++; return "executed"; }, "context_billing_tool")] };
        var messages = Enumerable.Range(0, 11).Select(index => new ChatMessage(index % 2 == 0 ? ChatRole.User : ChatRole.Assistant,
            $"context-{index} " + new string((char)('a' + index), 2_000))).ToArray();
        var response = streaming
            ? await client.GetStreamingResponseAsync(messages, options).ToChatResponseAsync()
            : await client.GetResponseAsync(messages, options);

        Assert.Equal(0, executions);
        if (streaming)
            Assert.Equal(ChatFinishReason.Stop, response.FinishReason);
        else
        {
#pragma warning disable OPENAI001
            Assert.Equal(OpenAI.Responses.ResponseStatus.Completed,
                Assert.IsType<OpenAI.Responses.ResponseResult>(response.RawRepresentation).Status);
#pragma warning restore OPENAI001
        }
        Assert.False(string.IsNullOrWhiteSpace(response.Text));
        Assert.Equal(2, handler.Payloads.Count);
        Assert.Single(recoveries);
        using var firstPayload = JsonDocument.Parse(handler.Payloads[0]);
        using var recoveredPayload = JsonDocument.Parse(handler.Payloads[1]);
        Assert.True(recoveredPayload.RootElement.GetProperty("input").GetArrayLength()
            < firstPayload.RootElement.GetProperty("input").GetArrayLength());
        var expectedTotalTokens = hasReportedUsage ? 35 : 15;
        var expectedCachedInputTokens = hasReportedUsage ? (streaming ? 3 : 5) : 0;
        Assert.Equal(expectedTotalTokens, response.Usage?.TotalTokenCount);
        Assert.Equal(hasReportedUsage ? 22 : 10, response.Usage?.InputTokenCount);
        Assert.Equal(hasReportedUsage ? 13 : 5, response.Usage?.OutputTokenCount);
        Assert.Equal(expectedCachedInputTokens, response.Usage?.CachedInputTokenCount);
        Assert.Equal(expectedTotalTokens, budget.Snapshot.ReportedTotalTokens);
        Assert.Equal(expectedTotalTokens, budget.Snapshot.ConsumedTokens);
        Assert.False(budget.Snapshot.UsedEstimatedUsage);
        Assert.Equal(expectedCachedInputTokens, budget.Snapshot.ReportedCachedInputTokens);
        Assert.Equal(2, budget.Snapshot.ProviderCalls);
        Assert.Equal(0, budget.Snapshot.ProviderRetryCount);
    }

    [Fact]
    public async Task RuntimeDoesNotRetryQuotaFailureAndRetainsItsReportedUsage()
    {
        using var handler = new CapturingHandler(
            CreateTerminalResponseStream(CreateFailedFunctionCallResponse(null, "insufficient_quota")));
        var tool = new IncompleteCheckpointProbeTool();
        var events = new List<CopilotAgentEvent>();
        var failure = await Assert.ThrowsAsync<CopilotProviderPayloadException>(() => RunCapturedResponseRuntimeAsync(handler, tool, events));
        Assert.Equal("insufficient_quota", failure.ErrorCode);
        Assert.Equal(20, failure.ReportedUsage.EffectiveTotalTokens);
        Assert.Equal(0, tool.ExecutionCount);
        Assert.Single(handler.Payloads);
        Assert.DoesNotContain(events, item => item.ProviderRetry != null);
        Assert.Equal(20, events.Last(item => item.Type == CopilotAgentEventType.BudgetUpdated).Budget!.ReportedTotalTokens);
    }

    [Fact]
    public async Task RuntimeFailureAfterToolProgressDoesNotDispatchAnotherToolOrReplayItsBilling()
    {
        var tool = new IncompleteCheckpointProbeTool();
        var functionName = CopilotMicrosoftAgentFrameworkRuntime.HarnessToolBridge.ToFunctionName(tool.Name);
        var completed = System.Text.Json.Nodes.JsonNode.Parse(CreateIncompleteFunctionCallResponse(functionName, null))!;
        completed["id"] = "resp_completed_checkpoint";
        completed["status"] = "completed";
        completed["incomplete_details"] = null;
        completed["output"]![0]!["call_id"] = "call_completed_checkpoint";
        using var handler = new CapturingHandler(CreateTerminalResponseStream(completed.ToJsonString()),
            CreateTerminalResponseStream(CreateFailedFunctionCallResponse(functionName, "server_error")));
        var events = new List<CopilotAgentEvent>();
        var result = await RunCapturedResponseRuntimeAsync(handler, tool, events);
        Assert.Equal(CopilotAgentStopReason.ProviderFailure, result.StopReason);
        Assert.Equal(1, tool.ExecutionCount);
        Assert.Equal(CopilotToolExecutionState.Completed, Assert.Single(result.StepRecords).Execution.State);
        Assert.Equal(2, handler.Payloads.Count);
        Assert.Equal(2, result.Budget.ProviderCalls);
        Assert.Equal(0, result.Budget.ProviderRetryCount);
        Assert.Equal(40, result.Usage.EffectiveTotalTokens);
        Assert.Equal(result.Usage.EffectiveTotalTokens, result.Budget.ReportedTotalTokens);
        Assert.DoesNotContain(events, item => item.ProviderRetry != null);
    }

    [Theory]
    [InlineData(true, "max_output_tokens", "length")]
    [InlineData(false, "max_output_tokens", "length")]
    [InlineData(true, "content_filter", "content_filter")]
    [InlineData(false, "content_filter", "content_filter")]
    [InlineData(true, "unknown_reason", "incomplete")]
    [InlineData(false, "unknown_reason", "incomplete")]
    [InlineData(true, null, "incomplete")]
    [InlineData(false, null, "incomplete")]
    public async Task IncompleteResponsesRetainFinishReasonAndUsageWithoutExecutingTools(bool streaming, string? reason, string expectedFinishReason)
    {
        const string functionName = "incomplete_tool";
        using var handler = new CapturingHandler(HttpStatusCode.OK,
            streaming ? CreateIncompleteFunctionCallStream(functionName, reason) : CreateIncompleteFunctionCallResponse(functionName, reason),
            streaming ? "text/event-stream" : "application/json");
        using var httpClient = new HttpClient(handler);
        using var transport = CopilotOpenAiAgentChatClientFactory.Create(
            CreateProfile(CopilotVendorType.OpenAI, "https://api.openai.com/v1/responses", "gpt-5.5"), httpClient);
        using var client = new FunctionInvokingChatClient(new CopilotIncompleteToolCallGuardChatClient(transport));
        var executions = 0;
        var options = new ChatOptions
        {
            Tools = [AIFunctionFactory.Create(() => { executions++; return "executed"; }, functionName)],
        };
        ChatMessage[] messages = [new(ChatRole.User, "Observe the supplied data.")];
        var response = streaming
            ? await client.GetStreamingResponseAsync(messages, options).ToChatResponseAsync()
            : await client.GetResponseAsync(messages, options);
        Assert.Equal(0, executions);
        Assert.Single(handler.Payloads);
        Assert.Equal(new ChatFinishReason(expectedFinishReason), response.FinishReason);
        Assert.Equal(12, response.Usage?.InputTokenCount);
        Assert.Equal(8, response.Usage?.OutputTokenCount);
        Assert.Equal(20, response.Usage?.TotalTokenCount);
        Assert.Equal(3, response.Usage?.CachedInputTokenCount);
        Assert.Equal(2, response.Usage?.ReasoningTokenCount);
        Assert.True(Assert.Single(response.Messages.SelectMany(message => message.Contents).OfType<FunctionCallContent>()).InformationalOnly);
    }

    [Theory]
    [InlineData(true, "max_output_tokens")]
    [InlineData(true, "content_filter")]
    [InlineData(false, "length")]
    [InlineData(false, "content_filter")]
    public async Task RuntimeCheckpointDoesNotReplayAnUnpairedIncompleteToolCall(bool responsesApi, string incompleteReason)
    {
        var directory = Directory.CreateTempSubdirectory("CopilotIncompleteCheckpointTests-");
        var resolved = Path.GetFullPath(directory.FullName);
        Assert.Equal(Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())), Path.GetDirectoryName(resolved), ignoreCase: true);
        Assert.StartsWith("CopilotIncompleteCheckpointTests-", Path.GetFileName(resolved), StringComparison.Ordinal);
        try
        {
            var tool = new IncompleteCheckpointProbeTool();
            var functionName = CopilotMicrosoftAgentFrameworkRuntime.HarnessToolBridge.ToFunctionName(tool.Name);
            var firstStream = responsesApi ? CreateIncompleteFunctionCallStream(functionName, incompleteReason)
                : CreateCheckpointChatStream(functionName, incompleteReason);
            var responses = new List<(HttpStatusCode, string, string)>
            {
                (HttpStatusCode.OK, firstStream, "text/event-stream"),
            };
            if (incompleteReason is "max_output_tokens" or "length")
            {
                responses.Add((HttpStatusCode.OK,
                    responsesApi
                        ? """{"id":"resp_repair","object":"response","created_at":1234567890,"model":"gpt-5.5","status":"completed","output":[{"type":"message","id":"msg_repair","role":"assistant","status":"completed","content":[{"type":"output_text","text":"The observation was incomplete.","annotations":[]}]}]}"""
                        : """{"id":"chat_repair","object":"chat.completion","created":1234567890,"model":"test-model","choices":[{"index":0,"message":{"role":"assistant","content":"The observation was incomplete."},"finish_reason":"stop"}]}""",
                    "application/json"));
            }
            responses.Add((HttpStatusCode.OK, responsesApi ? TextResponseStream : CreateCheckpointChatStream(null, "stop"), "text/event-stream"));
            using var handler = new CapturingHandler(responses);
            using var httpClient = new HttpClient(handler);
            var profile = responsesApi ? CreateProfile(CopilotVendorType.OpenAI, "https://api.openai.com/v1/responses", "gpt-5.5")
                : CreateProfile(CopilotVendorType.Custom, "https://example.test/v1", "test-model");
            var catalog = new CopilotCapabilityCatalog();
            catalog.PublishSource(CopilotCapabilitySourceKind.BuiltIn, "incomplete-checkpoint-tests", "Incomplete checkpoint tests", [tool]);
            var usageStore = new CopilotAgentSkillUsageStore(directory.FullName);
            CopilotMicrosoftAgentFrameworkRuntime CreateRuntime() => new(
                new CopilotToolRegistry([tool]), new CopilotAgentContextBuilder(), new CopilotToolExecutor(),
                selected => CopilotOpenAiAgentChatClientFactory.Create(selected, httpClient),
                new NoCheckpointExternalTools(), catalog, usageStore);
            CopilotAgentRequest CreateRequest(CopilotAgentSessionCheckpoint? checkpoint) => new()
            {
                Profile = profile,
                ConversationId = "incomplete-checkpoint-conversation",
                TaskId = checkpoint == null ? "incomplete-checkpoint-first" : "incomplete-checkpoint-follow-up",
                WorkspacePath = directory.FullName,
                UserText = checkpoint == null ? "Summarize the supplied notes." : "Continue the same summary.",
                TaskIntentText = "Summarize the supplied notes.",
                Mode = CopilotAgentMode.Auto,
                HarnessFeatures = CopilotAgentHarnessFeatures.None,
                SessionCheckpoint = checkpoint,
                RunBudgetOverride = new CopilotAgentRunBudgetOverride
                {
                    RequestTokenBudget = 32_768, MaxToolCalls = 2, MaxAgentPasses = 1,
                    TotalDuration = TimeSpan.FromSeconds(10),
                },
            };
            var firstEvents = new List<CopilotAgentEvent>();
            var first = await CreateRuntime().RunAsync(CreateRequest(null), firstEvents.Add, CancellationToken.None);
            Assert.Equal(0, tool.ExecutionCount);
            Assert.Contains(firstEvents, item => item.Text.Contains("retained as informational evidence and were not dispatched", StringComparison.Ordinal));
            var checkpoint = Assert.IsType<CopilotAgentSessionCheckpoint>(first.SessionCheckpoint);
            Assert.Contains("call_incomplete_checkpoint", checkpoint.SerializedSessionJson, StringComparison.Ordinal);
            var restored = Assert.IsType<CopilotAgentSessionCheckpoint>(Newtonsoft.Json.JsonConvert.DeserializeObject<CopilotAgentSessionCheckpoint>(
                Newtonsoft.Json.JsonConvert.SerializeObject(checkpoint)));
            var firstRequestCount = handler.Payloads.Count;
            var resumed = await CreateRuntime().RunAsync(CreateRequest(restored), _ => { }, CancellationToken.None);
            Assert.Equal(0, tool.ExecutionCount);
            Assert.Contains(resumed.TaskEventJournal.Events, item => item.Type == CopilotAgentTaskEventType.SessionResumed);
            Assert.Equal(firstRequestCount + 1, handler.Payloads.Count);
            using var payload = JsonDocument.Parse(handler.Payloads[firstRequestCount]);
            var input = payload.RootElement.GetProperty(responsesApi ? "input" : "messages").EnumerateArray().ToArray();
            var calls = responsesApi
                ? input.Where(item => item.GetProperty("type").GetString() == "function_call")
                    .Select(item => item.GetProperty("call_id").GetString()).OrderBy(value => value).ToArray()
                : input.Where(item => item.TryGetProperty("tool_calls", out _))
                    .SelectMany(item => item.GetProperty("tool_calls").EnumerateArray())
                    .Select(item => item.GetProperty("id").GetString()).OrderBy(value => value).ToArray();
            var outputs = responsesApi
                ? input.Where(item => item.GetProperty("type").GetString() == "function_call_output")
                    .Select(item => item.GetProperty("call_id").GetString()).OrderBy(value => value).ToArray()
                : input.Where(item => item.GetProperty("role").GetString() == "tool")
                    .Select(item => item.GetProperty("tool_call_id").GetString()).OrderBy(value => value).ToArray();
            Assert.Equal(calls, outputs);
        }
        finally
        {
            Directory.Delete(resolved, recursive: true);
        }
    }

    private static string CreateCheckpointChatStream(string? functionName, string finishReason)
    {
        object delta = functionName == null ? new { role = "assistant", content = "Responses adapter OK." }
            : new { role = "assistant", tool_calls = new[] { new { index = 0, id = "call_incomplete_checkpoint", type = "function",
                function = new { name = functionName, arguments = "{}" } } } };
        return "data: " + JsonSerializer.Serialize(new
        {
            id = "chat_incomplete_checkpoint", @object = "chat.completion.chunk", created = 1234567890,
            model = "test-model", choices = new[] { new { index = 0, delta, finish_reason = (string?)null } },
        }) + "\n\n" + "data: " + JsonSerializer.Serialize(new
        {
            id = "chat_incomplete_checkpoint", @object = "chat.completion.chunk", created = 1234567890,
            model = "test-model", choices = new[] { new { index = 0, delta = new { }, finish_reason = finishReason } },
        }) + "\n\ndata: [DONE]\n\n";
    }

    private static string CreateIncompleteFunctionCallResponse(string functionName, string? incompleteReason)
    {
        var item = new { type = "function_call", id = "fc_incomplete_checkpoint", call_id = "call_incomplete_checkpoint", name = functionName, arguments = "{}", status = "completed" };
        return JsonSerializer.Serialize(new
        {
            id = "resp_incomplete_checkpoint", @object = "response", created_at = 1234567890,
            model = "gpt-5.5", status = "incomplete", output = new[] { item },
            incomplete_details = incompleteReason == null ? new Dictionary<string, object?>()
                : new Dictionary<string, object?> { ["reason"] = incompleteReason },
            usage = new { input_tokens = 12, output_tokens = 8, total_tokens = 20,
                input_tokens_details = new { cached_tokens = 3 }, output_tokens_details = new { reasoning_tokens = 2 } },
        });
    }

    private static string CreateFailedFunctionCallResponse(string? functionName, string errorCode, string? errorMessage = null)
    {
        var response = System.Text.Json.Nodes.JsonNode.Parse(CreateIncompleteFunctionCallResponse(functionName ?? "unused", null))!;
        response["id"] = "resp_failed_checkpoint";
        response["status"] = "failed";
        response["incomplete_details"] = null;
        response["error"] = new System.Text.Json.Nodes.JsonObject { ["code"] = errorCode, ["message"] = errorMessage ?? "Synthetic terminal provider failure." };
        if (functionName == null) response["output"] = new System.Text.Json.Nodes.JsonArray();
        else response["output"]![0]!["call_id"] = "call_failed_checkpoint";
        return response.ToJsonString();
    }

    private static string CreateIncompleteFunctionCallStream(string functionName, string? incompleteReason)
        => CreateTerminalResponseStream(CreateIncompleteFunctionCallResponse(functionName, incompleteReason));

    private static string CreateTerminalResponseStream(string responseJson)
    {
        var response = JsonSerializer.Deserialize<JsonElement>(responseJson);
        var stream = new StringBuilder();
        var sequence = 0;
        var started = new { id = response.GetProperty("id").GetString(), @object = "response", created_at = 1234567890,
            model = "gpt-5.5", status = "in_progress", output = Array.Empty<object>() };
        stream.Append("data: ").Append(JsonSerializer.Serialize(new { type = "response.created", sequence_number = sequence++, response = started })).Append("\n\n");
        foreach (var item in response.GetProperty("output").EnumerateArray())
        {
            var id = item.GetProperty("id").GetString();
            var name = item.GetProperty("name").GetString();
            var arguments = item.GetProperty("arguments").GetString();
            stream.Append("data: ").Append(JsonSerializer.Serialize(new { type = "response.output_item.added", sequence_number = sequence++, output_index = 0,
                item = new { type = "function_call", id, call_id = item.GetProperty("call_id").GetString(), name, arguments = "", status = "in_progress" } })).Append("\n\n");
            stream.Append("data: ").Append(JsonSerializer.Serialize(new { type = "response.function_call_arguments.done", sequence_number = sequence++, item_id = id, output_index = 0, name, arguments })).Append("\n\n");
            stream.Append("data: ").Append(JsonSerializer.Serialize(new { type = "response.output_item.done", sequence_number = sequence++, output_index = 0, item })).Append("\n\n");
        }
        stream.Append("data: ").Append(JsonSerializer.Serialize(new { type = "response." + response.GetProperty("status").GetString(), sequence_number = sequence, response })).Append("\n\n");
        return stream.ToString();
    }

    private static async Task<CopilotAgentRunResult> RunCapturedResponseRuntimeAsync(CapturingHandler handler, IncompleteCheckpointProbeTool tool, List<CopilotAgentEvent> events)
    {
        var directory = Directory.CreateTempSubdirectory("CopilotFailedResponseRuntimeTests-");
        var resolved = Path.GetFullPath(directory.FullName);
        Assert.Equal(Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())), Path.GetDirectoryName(resolved), ignoreCase: true);
        Assert.StartsWith("CopilotFailedResponseRuntimeTests-", Path.GetFileName(resolved), StringComparison.Ordinal);
        try
        {
            using var httpClient = new HttpClient(handler, disposeHandler: false);
            var catalog = new CopilotCapabilityCatalog();
            catalog.PublishSource(CopilotCapabilitySourceKind.BuiltIn, "failed-response-tests", "Failed response tests", [tool]);
            var runtime = new CopilotMicrosoftAgentFrameworkRuntime(new CopilotToolRegistry([tool]),
                new CopilotAgentContextBuilder(), new CopilotToolExecutor(),
                profile => CopilotOpenAiAgentChatClientFactory.Create(profile, httpClient),
                new NoCheckpointExternalTools(), catalog, new CopilotAgentSkillUsageStore(directory.FullName));
            return await runtime.RunAsync(new CopilotAgentRequest
            {
                Profile = CreateProfile(CopilotVendorType.OpenAI, "https://api.openai.com/v1/responses", "gpt-5.5"),
                ConversationId = "failed-response-conversation", TaskId = "failed-response-task",
                WorkspacePath = directory.FullName, UserText = "Summarize the supplied notes.", TaskIntentText = "Summarize the supplied notes.",
                Mode = CopilotAgentMode.Auto, HarnessFeatures = CopilotAgentHarnessFeatures.None,
                RunBudgetOverride = new CopilotAgentRunBudgetOverride
                {
                    RequestTokenBudget = 32_768, MaxToolCalls = 2, MaxAgentPasses = 1, TotalDuration = TimeSpan.FromSeconds(10),
                },
            }, events.Add, CancellationToken.None);
        }
        finally
        {
            Directory.Delete(resolved, recursive: true);
        }
    }

    private sealed class IncompleteCheckpointProbeTool : ICopilotAgentDrivenTool
    {
        public string Name => "IncompleteCheckpointProbe";
        public string Description => "Returns a deterministic observation.";
        public int ExecutionCount { get; private set; }
        public bool CanHandle(CopilotAgentRequest request) => true;
        public bool IsAvailable(CopilotAgentRequest request) => true;
        public Task<CopilotToolResult> ExecuteAsync(CopilotAgentRequest request, CopilotAgentToolInput input, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            ExecutionCount++;
            return Task.FromResult(new CopilotToolResult { ToolName = Name, Success = true, Summary = "Observed." });
        }
    }

    private sealed class NoCheckpointExternalTools : ICopilotExternalToolProvider
    {
        public Task<CopilotExternalToolLease> DiscoverAsync(CopilotAgentRequest request, CancellationToken token)
            => Task.FromResult(new CopilotExternalToolLease());
    }

    private const string BilledTextResponseJson = """{"id":"resp_retry_success","object":"response","created_at":1234567890,"model":"gpt-5.5","status":"completed","output":[{"type":"message","id":"msg_retry_success","role":"assistant","status":"completed","content":[{"type":"output_text","text":"The reported response succeeded.","annotations":[]}]}],"usage":{"input_tokens":10,"output_tokens":5,"total_tokens":15,"input_tokens_details":{"cached_tokens":2},"output_tokens_details":{"reasoning_tokens":1}}}""";

    private const string TextResponseStream =
        """
        data: {"type":"response.created","sequence_number":0,"response":{"id":"resp_test","object":"response","created_at":1234567890,"model":"gpt-5.5","status":"in_progress","output":[]}}

        data: {"type":"response.output_text.delta","sequence_number":1,"item_id":"msg_test","output_index":0,"content_index":0,"delta":"Responses adapter "}

        data: {"type":"response.output_text.delta","sequence_number":2,"item_id":"msg_test","output_index":0,"content_index":0,"delta":"OK."}

        data: {"type":"response.completed","sequence_number":3,"response":{"id":"resp_test","object":"response","created_at":1234567890,"model":"gpt-5.5","status":"completed","output":[{"type":"message","id":"msg_test","role":"assistant","status":"completed","content":[{"type":"output_text","text":"Responses adapter OK.","annotations":[]}]}],"usage":{"input_tokens":10,"output_tokens":5,"total_tokens":15,"input_tokens_details":{"cached_tokens":0},"output_tokens_details":{"reasoning_tokens":0}}}}

        data: [DONE]

        """;

    private const string FunctionCallResponseStream =
        """
        data: {"type":"response.created","sequence_number":0,"response":{"id":"resp_tool","object":"response","created_at":1234567890,"model":"gpt-5.5","status":"in_progress","output":[]}}

        data: {"type":"response.output_item.added","sequence_number":1,"output_index":0,"item":{"type":"function_call","id":"fc_test","call_id":"call_test","name":"read_file","arguments":"","status":"in_progress"}}

        data: {"type":"response.function_call_arguments.delta","sequence_number":2,"item_id":"fc_test","output_index":0,"delta":"{\"path\":\"C:\\\\workspace\\\\evidence.txt\"}"}

        data: {"type":"response.function_call_arguments.done","sequence_number":3,"item_id":"fc_test","output_index":0,"name":"read_file","arguments":"{\"path\":\"C:\\\\workspace\\\\evidence.txt\"}"}

        data: {"type":"response.output_item.done","sequence_number":4,"output_index":0,"item":{"type":"function_call","id":"fc_test","call_id":"call_test","name":"read_file","arguments":"{\"path\":\"C:\\\\workspace\\\\evidence.txt\"}","status":"completed"}}

        data: {"type":"response.completed","sequence_number":5,"response":{"id":"resp_tool","object":"response","created_at":1234567890,"model":"gpt-5.5","status":"completed","output":[{"type":"function_call","id":"fc_test","call_id":"call_test","name":"read_file","arguments":"{\"path\":\"C:\\\\workspace\\\\evidence.txt\"}","status":"completed"}],"usage":{"input_tokens":12,"output_tokens":8,"total_tokens":20,"input_tokens_details":{"cached_tokens":0},"output_tokens_details":{"reasoning_tokens":0}}}}

        data: [DONE]

        """;

    private const string ReasoningResponseStream =
        """
        data: {"type":"response.created","sequence_number":0,"response":{"id":"resp_reasoning","object":"response","created_at":1234567890,"model":"gpt-5.5","status":"in_progress","output":[]}}

        data: {"type":"response.output_item.added","sequence_number":1,"output_index":0,"item":{"type":"reasoning","id":"rs_test","summary":[],"encrypted_content":"encrypted-reasoning-test"}}

        data: {"type":"response.output_item.done","sequence_number":2,"output_index":0,"item":{"type":"reasoning","id":"rs_test","summary":[],"encrypted_content":"encrypted-reasoning-test"}}

        data: {"type":"response.output_item.added","sequence_number":3,"output_index":1,"item":{"type":"message","id":"msg_reasoning","role":"assistant","status":"in_progress","phase":"final_answer","content":[]}}

        data: {"type":"response.output_text.delta","sequence_number":4,"item_id":"msg_reasoning","output_index":1,"content_index":0,"delta":"First answer."}

        data: {"type":"response.output_item.done","sequence_number":5,"output_index":1,"item":{"type":"message","id":"msg_reasoning","role":"assistant","status":"completed","phase":"final_answer","content":[{"type":"output_text","text":"First answer.","annotations":[]}]}}

        data: {"type":"response.completed","sequence_number":6,"response":{"id":"resp_reasoning","object":"response","created_at":1234567890,"model":"gpt-5.5","status":"completed","output":[{"type":"reasoning","id":"rs_test","summary":[],"encrypted_content":"encrypted-reasoning-test"},{"type":"message","id":"msg_reasoning","role":"assistant","status":"completed","phase":"final_answer","content":[{"type":"output_text","text":"First answer.","annotations":[]}]}],"usage":{"input_tokens":10,"output_tokens":5,"total_tokens":15,"input_tokens_details":{"cached_tokens":0},"output_tokens_details":{"reasoning_tokens":2}}}}

        data: [DONE]

        """;

    [Theory]
    [InlineData("https://api.deepseek.com/responses", CopilotReasoningMode.Max, "max")]
    [InlineData("https://example.test/gateway/responses/", CopilotReasoningMode.High, "high")]
    [InlineData("https://example.test/v1/responses", CopilotReasoningMode.Disabled, "none")]
    public async Task ExplicitResponsesEndpointSupportsStatelessToolRoundTrip(string endpoint, CopilotReasoningMode mode, string effort)
    {
        using var handler = new CapturingHandler(FunctionCallResponseStream.Replace("data: [DONE]", string.Empty), TextResponseStream.Replace("data: [DONE]", string.Empty));
        using var httpClient = new HttpClient(handler);
        var profile = CreateProfile(CopilotVendorType.DeepSeek, endpoint, "deepseek-flash");
        profile.ReasoningMode = mode;
        using var client = CopilotOpenAiAgentChatClientFactory.Create(profile, httpClient);
        var request = new CopilotAgentRequest
        {
            Profile = profile, CodexReasoningEffort = CopilotCodexReasoningEffort.Minimal,
            CodexFastModeEnabled = true, CodexServiceTier = "fast",
        };
        var options = CopilotMicrosoftAgentFrameworkRuntime.BuildChatOptions(request, CreateToolOptions().Tools!);
        var messages = new List<ChatMessage> { new(ChatRole.User, "Read the evidence and answer.") };
        var first = await client.GetStreamingResponseAsync(messages, options).ToChatResponseAsync();
        var call = Assert.Single(first.Messages.SelectMany(message => message.Contents).OfType<FunctionCallContent>());
        messages.AddRange(JsonSerializer.Deserialize<List<ChatMessage>>(JsonSerializer.Serialize(first.Messages, AIJsonUtilities.DefaultOptions), AIJsonUtilities.DefaultOptions)!);
        messages.Add(new(ChatRole.Tool, [new FunctionResultContent(call.CallId, "observed file content")]));
        var answer = await client.GetStreamingResponseAsync(messages, options).ToChatResponseAsync();
        Assert.Equal("Responses adapter OK.", answer.Text);
        Assert.Equal(new Uri(endpoint.TrimEnd('/')), handler.LastRequestUri);
        Assert.Equal(2, handler.Payloads.Count);
        foreach (var payloadText in handler.Payloads)
        {
            using var payload = JsonDocument.Parse(payloadText);
            var root = payload.RootElement;
            Assert.False(root.GetProperty("store").GetBoolean());
            Assert.Equal(effort, root.GetProperty("reasoning").GetProperty("effort").GetString());
            foreach (var key in new[] { "previous_response_id", "safety_identifier", "service_tier", "include" })
                Assert.False(root.TryGetProperty(key, out _), key);
        }
        using var replay = JsonDocument.Parse(handler.LastPayload);
        var input = replay.RootElement.GetProperty("input").EnumerateArray().ToArray();
        Assert.Contains(input, item => item.GetProperty("type").GetString() == "function_call" && item.GetProperty("call_id").GetString() == call.CallId);
        Assert.Contains(input, item => item.GetProperty("type").GetString() == "function_call_output" && item.GetProperty("call_id").GetString() == call.CallId);
        Assert.DoesNotContain(input, item => item.GetProperty("type").GetString() == "item_reference");
    }

    [Fact]
    public async Task OfficialOpenAiAgentUsesStatelessResponsesStreamingContract()
    {
        using var handler = new CapturingHandler(
            HttpStatusCode.OK,
            TextResponseStream,
            "text/event-stream");
        using var httpClient = new HttpClient(handler);
        using var client = CopilotOpenAiAgentChatClientFactory.Create(
            CreateProfile(
                CopilotVendorType.OpenAI,
                "https://api.openai.com/v1/responses",
                "gpt-5.5"),
            httpClient);
        var options = CreateToolOptions();

        var response = await client.GetStreamingResponseAsync(
                [new ChatMessage(ChatRole.User, "Inspect the file.")],
                options)
            .ToChatResponseAsync();

        Assert.Equal(
            new Uri("https://api.openai.com/v1/responses"),
            handler.LastRequestUri);
        Assert.Equal("Responses adapter OK.", response.Text);
        Assert.Equal(ChatFinishReason.Stop, response.FinishReason);
        Assert.Equal(10, response.Usage?.InputTokenCount);
        Assert.Equal(5, response.Usage?.OutputTokenCount);
        Assert.Equal(15, response.Usage?.TotalTokenCount);

        using var payload = JsonDocument.Parse(handler.LastPayload);
        var root = payload.RootElement;
        Assert.Equal("gpt-5.5", root.GetProperty("model").GetString());
        Assert.Equal(512, root.GetProperty("max_output_tokens").GetInt32());
        Assert.Equal(
            "Use the supplied tool when evidence is required.",
            root.GetProperty("instructions").GetString());
        Assert.False(root.GetProperty("store").GetBoolean());
        Assert.Contains(
            root.GetProperty("include").EnumerateArray(),
            item => item.GetString() == "reasoning.encrypted_content");
        Assert.Contains(
            root.GetProperty("tools").EnumerateArray(),
            tool => tool.GetProperty("name").GetString() == "read_file");
    }

    [Fact]
    public async Task OfficialOpenAiAgentAddsAStableHashedSafetyIdentifier()
    {
        using var handler = new CapturingHandler(
            HttpStatusCode.OK,
            TextResponseStream,
            "text/event-stream");
        using var httpClient = new HttpClient(handler);
        var profile = CreateProfile(
            CopilotVendorType.OpenAI,
            "https://api.openai.com/v1",
            "gpt-5.6");
        using var client = CopilotOpenAiAgentChatClientFactory.Create(profile, httpClient);
        var request = new CopilotAgentRequest { Profile = profile };

        await client.GetStreamingResponseAsync(
                [new ChatMessage(ChatRole.User, "Use the official Responses contract.")],
                CopilotMicrosoftAgentFrameworkRuntime.BuildChatOptions(request, []))
            .ToChatResponseAsync();

        using var payload = JsonDocument.Parse(handler.LastPayload);
        var safetyIdentifier = payload.RootElement
            .GetProperty("safety_identifier")
            .GetString();
        Assert.NotNull(safetyIdentifier);
        Assert.Matches("^[0-9a-f]{64}$", safetyIdentifier);
        Assert.Equal(CopilotOpenAiSafetyIdentifier.GetCurrent(), safetyIdentifier);
    }

    [Fact]
    public void SafetyIdentifierHashesAStableNormalizedAccountKey()
    {
        var first = CopilotOpenAiSafetyIdentifier.Create(@"DOMAIN\Example.User");
        var second = CopilotOpenAiSafetyIdentifier.Create(@"domain\example.user");

        Assert.Equal(first, second);
        Assert.Matches("^[0-9a-f]{64}$", first);
        Assert.DoesNotContain("example", first, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(first, CopilotOpenAiSafetyIdentifier.Create(@"DOMAIN\Other.User"));
        Assert.Equal(string.Empty, CopilotOpenAiSafetyIdentifier.Create(" "));
    }

    [Fact]
    public async Task OfficialOpenAiAgentMapsResponsesFunctionCallsForHarnessExecution()
    {
        using var handler = new CapturingHandler(
            HttpStatusCode.OK,
            FunctionCallResponseStream,
            "text/event-stream");
        using var httpClient = new HttpClient(handler);
        using var client = CopilotOpenAiAgentChatClientFactory.Create(
            CreateProfile(
                CopilotVendorType.OpenAI,
                "https://api.openai.com/v1",
                "gpt-5.5"),
            httpClient);

        var response = await client.GetStreamingResponseAsync(
                [new ChatMessage(ChatRole.User, "Inspect the evidence.")],
                CreateToolOptions())
            .ToChatResponseAsync();
        var functionCall = Assert.Single(
            response.Messages
                .SelectMany(message => message.Contents)
                .OfType<FunctionCallContent>());

        Assert.Equal(ChatFinishReason.ToolCalls, response.FinishReason);
        Assert.Equal("call_test", functionCall.CallId);
        Assert.Equal("read_file", functionCall.Name);
        Assert.Equal(
            @"C:\workspace\evidence.txt",
            functionCall.Arguments?["path"]?.ToString());
    }

    [Fact]
    public async Task OfficialOpenAiAgentReplaysEveryStatelessOutputItemAcrossSerializedTurns()
    {
        using var handler = new CapturingHandler(
            ReasoningResponseStream,
            TextResponseStream,
            TextResponseStream);
        using var httpClient = new HttpClient(handler);
        using var client = CopilotOpenAiAgentChatClientFactory.Create(
            CreateProfile(
                CopilotVendorType.OpenAI,
                "https://api.openai.com/v1",
                "gpt-5.5"),
            httpClient);
        var firstUserMessage = new ChatMessage(ChatRole.User, "Remember the reasoning state.");

        var firstResponse = await client.GetStreamingResponseAsync(
                [firstUserMessage])
            .ToChatResponseAsync();
        var serializedMessages = JsonSerializer.Serialize(
            firstResponse.Messages,
            AIJsonUtilities.DefaultOptions);
        var restoredMessages = JsonSerializer.Deserialize<List<ChatMessage>>(
            serializedMessages,
            AIJsonUtilities.DefaultOptions);
        var secondTurnMessages = new List<ChatMessage> { firstUserMessage };
        secondTurnMessages.AddRange(Assert.IsType<List<ChatMessage>>(restoredMessages));
        secondTurnMessages.Add(new ChatMessage(ChatRole.User, "Continue from the prior state."));
        await client.GetStreamingResponseAsync(secondTurnMessages)
            .ToChatResponseAsync();

        Assert.Equal(2, handler.Payloads.Count);
        AssertStatelessResponseItemsReplayed(handler.Payloads[1]);

        secondTurnMessages.Add(new ChatMessage(ChatRole.User, "Verify the preserved state again."));
        await client.GetStreamingResponseAsync(secondTurnMessages)
            .ToChatResponseAsync();

        Assert.Equal(3, handler.Payloads.Count);
        AssertStatelessResponseItemsReplayed(handler.Payloads[2]);
    }

    [Fact]
    public async Task OfficialOpenAiAgentDegradesStaleMessageReplayMetadataToPortableContent()
    {
        using var handler = new CapturingHandler(
            ReasoningResponseStream,
            TextResponseStream);
        using var httpClient = new HttpClient(handler);
        using var client = CopilotOpenAiAgentChatClientFactory.Create(
            CreateProfile(
                CopilotVendorType.OpenAI,
                "https://api.openai.com/v1",
                "gpt-5.5"),
            httpClient);
        var firstUserMessage = new ChatMessage(ChatRole.User, "Remember the reasoning state.");

        var firstResponse = await client.GetStreamingResponseAsync(
                [firstUserMessage])
            .ToChatResponseAsync();
        var serializedMessages = JsonSerializer.Serialize(
            firstResponse.Messages,
            AIJsonUtilities.DefaultOptions);
        var restoredMessages = Assert.IsType<List<ChatMessage>>(
            JsonSerializer.Deserialize<List<ChatMessage>>(
                serializedMessages,
                AIJsonUtilities.DefaultOptions));
        var restoredAssistant = Assert.Single(restoredMessages);
        restoredAssistant.Contents = [new TextContent("Compacted answer.")];
        var secondTurnMessages = new List<ChatMessage> { firstUserMessage, restoredAssistant };
        secondTurnMessages.Add(new ChatMessage(ChatRole.User, "Continue from the compacted state."));

        await client.GetStreamingResponseAsync(secondTurnMessages)
            .ToChatResponseAsync();

        using var document = JsonDocument.Parse(handler.Payloads[1]);
        var assistantItem = Assert.Single(
            document.RootElement.GetProperty("input").EnumerateArray(),
            item => item.GetProperty("type").GetString() == "message"
                && item.GetProperty("role").GetString() == "assistant");
        var outputText = Assert.Single(assistantItem.GetProperty("content").EnumerateArray());
        Assert.Equal("Compacted answer.", outputText.GetProperty("text").GetString());
    }

    private static void AssertStatelessResponseItemsReplayed(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        var inputItems = document.RootElement.GetProperty("input").EnumerateArray().ToArray();
        var reasoningItem = Assert.Single(inputItems.Where(
            item => item.GetProperty("type").GetString() == "reasoning"));
        Assert.True(
            reasoningItem.TryGetProperty("id", out var reasoningId),
            payload);
        Assert.Equal("rs_test", reasoningId.GetString());
        Assert.Equal(
            "encrypted-reasoning-test",
            reasoningItem.GetProperty("encrypted_content").GetString());
        var assistantItem = Assert.Single(inputItems.Where(
            item => item.GetProperty("type").GetString() == "message"
                && item.GetProperty("role").GetString() == "assistant"));
        Assert.True(assistantItem.TryGetProperty("id", out var assistantId), payload);
        Assert.Equal("msg_reasoning", assistantId.GetString());
        Assert.True(assistantItem.TryGetProperty("phase", out var assistantPhase));
        Assert.Equal("final_answer", assistantPhase.GetString());
        var outputText = Assert.Single(assistantItem.GetProperty("content").EnumerateArray());
        Assert.Equal("output_text", outputText.GetProperty("type").GetString());
        Assert.Equal("First answer.", outputText.GetProperty("text").GetString());
        Assert.False(document.RootElement.GetProperty("store").GetBoolean());
    }

    [Theory]
    [InlineData("fast", "high", "priority", "high")]
    [InlineData("flex", "medium", "flex", "medium")]
    [InlineData("scale", "low", "scale", "low")]
    public async Task OfficialOpenAiAgentPreservesCodexResponsePreferencesOnTheWire(
        string configuredServiceTier,
        string configuredVerbosity,
        string expectedServiceTier,
        string expectedVerbosity)
    {
        Assert.True(CopilotCodexModelVerbositySelection.TryParse(
            configuredVerbosity,
            out var verbosity));
        using var handler = new CapturingHandler(
            HttpStatusCode.OK,
            TextResponseStream,
            "text/event-stream");
        using var httpClient = new HttpClient(handler);
        var profile = CreateProfile(
            CopilotVendorType.OpenAI,
            "https://api.openai.com/v1",
            "gpt-6-astra");
        using var client = CopilotOpenAiAgentChatClientFactory.Create(profile, httpClient);
        var request = new CopilotAgentRequest
        {
            Profile = profile,
            CodexReasoningEffort = CopilotCodexReasoningEffort.Minimal,
            CodexReasoningSummary = CopilotCodexReasoningSummary.Concise,
            CodexServiceTier = configuredServiceTier,
            CodexModelVerbosity = verbosity,
        };

        await client.GetStreamingResponseAsync(
                [new ChatMessage(ChatRole.User, "Use the configured response preferences.")],
                CopilotMicrosoftAgentFrameworkRuntime.BuildChatOptions(request, []))
            .ToChatResponseAsync();

        using var payload = JsonDocument.Parse(handler.LastPayload);
        var root = payload.RootElement;
        Assert.False(root.GetProperty("store").GetBoolean());
        Assert.Equal(
            expectedServiceTier,
            root.GetProperty("service_tier").GetString());
        Assert.Equal(
            expectedVerbosity,
            root.GetProperty("text").GetProperty("verbosity").GetString());
        Assert.Equal("low", root.GetProperty("reasoning").GetProperty("effort").GetString());
        Assert.Equal("concise", root.GetProperty("reasoning").GetProperty("summary").GetString());
    }

    [Fact]
    public async Task OfficialOpenAiAgentDropsServiceTierWhenFastModeIsDisabled()
    {
        using var handler = new CapturingHandler(
            HttpStatusCode.OK,
            TextResponseStream,
            "text/event-stream");
        using var httpClient = new HttpClient(handler);
        var profile = CreateProfile(
            CopilotVendorType.OpenAI,
            "https://api.openai.com/v1",
            "gpt-5.5");
        using var client = CopilotOpenAiAgentChatClientFactory.Create(profile, httpClient);
        var request = new CopilotAgentRequest
        {
            Profile = profile,
            CodexFastModeEnabled = false,
            CodexServiceTier = "fast",
        };

        await client.GetStreamingResponseAsync(
                [new ChatMessage(ChatRole.User, "Honor the fast mode gate.")],
                CopilotMicrosoftAgentFrameworkRuntime.BuildChatOptions(request, []))
            .ToChatResponseAsync();

        using var payload = JsonDocument.Parse(handler.LastPayload);
        Assert.False(payload.RootElement.TryGetProperty("service_tier", out _));
    }

    [Theory]
    [InlineData(null, "minimal", "concise", "low", "concise")]
    [InlineData(null, "none", null, "low", null)]
    [InlineData(null, "xhigh", "detailed", "xhigh", "detailed")]
    [InlineData(null, "max", "concise", "max", "concise")]
    [InlineData(null, "ultra", "auto", "max", "auto")]
    [InlineData(null, "high", "none", "high", null)]
    [InlineData(null, null, "auto", null, "auto")]
    [InlineData(null, null, "none", null, null)]
    [InlineData(false, "minimal", "concise", "low", null)]
    [InlineData(true, "minimal", null, "low", "auto")]
    [InlineData(true, "high", "none", "high", null)]
    public async Task OfficialOpenAiAgentHonorsCodexReasoningOptionsOnTheResponsesWire(
        bool? supportsReasoningSummaries,
        string? configuredEffort,
        string? configuredSummary,
        string? expectedEffort,
        string? expectedSummary)
    {
        var effort = CopilotCodexReasoningEffort.Unspecified;
        var summary = CopilotCodexReasoningSummary.Unspecified;
        if (configuredEffort != null)
            Assert.True(CopilotCodexReasoningEffortSelection.TryParsePlanMode(configuredEffort, out effort));
        if (configuredSummary != null)
            Assert.True(CopilotCodexReasoningSummarySelection.TryParse(configuredSummary, out summary));
        using var handler = new CapturingHandler(
            HttpStatusCode.OK,
            TextResponseStream,
            "text/event-stream");
        using var httpClient = new HttpClient(handler);
        var profile = CreateProfile(
            CopilotVendorType.OpenAI,
            "https://api.openai.com/v1",
            "gpt-6-astra");
        using var client = CopilotOpenAiAgentChatClientFactory.Create(profile, httpClient);
        var request = new CopilotAgentRequest
        {
            Profile = profile,
            CodexReasoningEffort = effort,
            CodexReasoningSummary = summary,
            CodexModelSupportsReasoningSummaries = supportsReasoningSummaries,
        };

        await client.GetStreamingResponseAsync(
                [new ChatMessage(ChatRole.User, "Use the configured reasoning contract.")],
                CopilotMicrosoftAgentFrameworkRuntime.BuildChatOptions(request, []))
            .ToChatResponseAsync();

        using var payload = JsonDocument.Parse(handler.LastPayload);
        if (expectedEffort == null && expectedSummary == null)
        {
            Assert.False(payload.RootElement.TryGetProperty("reasoning", out _));
            return;
        }

        var reasoning = payload.RootElement.GetProperty("reasoning");
        if (expectedEffort == null)
            Assert.False(reasoning.TryGetProperty("effort", out _));
        else
            Assert.Equal(expectedEffort, reasoning.GetProperty("effort").GetString());
        if (expectedSummary == null)
            Assert.False(reasoning.TryGetProperty("summary", out _));
        else
            Assert.Equal(expectedSummary, reasoning.GetProperty("summary").GetString());
    }

    [Fact]
    public async Task AstraProfileMaxEffortIsPreservedOnTheResponsesWire()
    {
        using var handler = new CapturingHandler(
            HttpStatusCode.OK,
            TextResponseStream,
            "text/event-stream");
        using var httpClient = new HttpClient(handler);
        var profile = CreateProfile(
            CopilotVendorType.OpenAI,
            "https://api.openai.com/v1",
            "gpt-6-astra");
        profile.ReasoningMode = CopilotReasoningMode.Max;
        using var client = CopilotOpenAiAgentChatClientFactory.Create(profile, httpClient);
        var request = new CopilotAgentRequest { Profile = profile };

        await client.GetStreamingResponseAsync(
                [new ChatMessage(ChatRole.User, "Use maximum Astra reasoning.")],
                CopilotMicrosoftAgentFrameworkRuntime.BuildChatOptions(request, []))
            .ToChatResponseAsync();

        using var payload = JsonDocument.Parse(handler.LastPayload);
        Assert.Equal(
            "max",
            payload.RootElement.GetProperty("reasoning").GetProperty("effort").GetString());
    }

    [Fact]
    public async Task ThirdPartyCompatibleAgentKeepsChatCompletionsTransport()
    {
        using var handler = new CapturingHandler(
            HttpStatusCode.BadRequest,
            """{"error":{"message":"capture complete","type":"invalid_request_error"}}""",
            "application/json");
        using var httpClient = new HttpClient(handler);
        var profile = CreateProfile(
            CopilotVendorType.Custom,
            "https://example.test/v1",
            "gpt-5.5-compatible");

        Assert.True(profile.EnsureValid());
        Assert.Equal(CopilotVendorType.OpenAI, profile.VendorType);
        using var client = CopilotOpenAiAgentChatClientFactory.Create(
            profile,
            httpClient);
        var request = new CopilotAgentRequest
        {
            Profile = profile,
            CodexReasoningEffort = CopilotCodexReasoningEffort.Minimal,
            CodexReasoningSummary = CopilotCodexReasoningSummary.Concise,
            CodexModelSupportsReasoningSummaries = false,
        };

        await Assert.ThrowsAnyAsync<Exception>(
            () => client.GetResponseAsync(
                [new ChatMessage(ChatRole.User, "Keep proxy compatibility.")],
                CopilotMicrosoftAgentFrameworkRuntime.BuildChatOptions(request, [])));

        Assert.Equal(
            new Uri("https://example.test/v1/chat/completions"),
            handler.LastRequestUri);
        using var payload = JsonDocument.Parse(handler.LastPayload);
        Assert.False(payload.RootElement.TryGetProperty("store", out _));
        Assert.False(payload.RootElement.TryGetProperty("safety_identifier", out _));
    }

    private static ChatOptions CreateToolOptions()
    {
        return new ChatOptions
        {
            Instructions = "Use the supplied tool when evidence is required.",
            MaxOutputTokens = 512,
            Tools =
            [
                AIFunctionFactory.Create(
                    (string path) => path,
                    "read_file",
                    "Reads a local file."),
            ],
        };
    }

    private static CopilotProfileConfig CreateProfile(
        CopilotVendorType vendorType,
        string baseUrl,
        string model)
    {
        return new CopilotProfileConfig
        {
            VendorType = vendorType,
            ProviderType = CopilotProviderType.OpenAICompatible,
            ApiKey = "test-key",
            BaseUrl = baseUrl,
            Model = model,
            MaxTokens = 4_096,
        };
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode StatusCode, string Response, string MediaType)> _responses;

        public CapturingHandler(
            HttpStatusCode statusCode,
            string response,
            string mediaType)
            : this([(statusCode, response, mediaType)])
        {
        }

        public CapturingHandler(params string[] responseStreams)
            : this(responseStreams.Select(response => (
                HttpStatusCode.OK,
                response,
                "text/event-stream")))
        {
        }

        public CapturingHandler(
            IEnumerable<(HttpStatusCode StatusCode, string Response, string MediaType)> responses)
        {
            _responses = new Queue<(HttpStatusCode, string, string)>(responses);
        }

        public Uri? LastRequestUri { get; private set; }

        public string LastPayload { get; private set; } = string.Empty;

        public List<string> Payloads { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            LastPayload = request.Content == null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            Payloads.Add(LastPayload);
            var response = _responses.Dequeue();
            return new HttpResponseMessage(response.StatusCode)
            {
                Content = new StringContent(
                    response.Response,
                    Encoding.UTF8,
                    response.MediaType),
            };
        }
    }
}
