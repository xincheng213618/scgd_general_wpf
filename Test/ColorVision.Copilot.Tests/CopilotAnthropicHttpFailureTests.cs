using Anthropic.Exceptions;
using ColorVision.Copilot;
using Microsoft.Extensions.AI;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotAnthropicHttpFailureTests
{
    [Theory]
    [InlineData(401)]
    [InlineData(429)]
    [InlineData(503)]
    public async Task ProductionFactoryLeavesHttpRetriesToHostAndPreservesSdkStatus(int statusCode)
    {
        await using var server = new LoopbackProvider(_ => ErrorResponse(statusCode));
        using var provider = CopilotMicrosoftAgentFrameworkRuntime.CreateChatClient(CreateProfile(server));

        var error = await Assert.ThrowsAnyAsync<AnthropicApiException>(() => provider.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "Read the supplied result.")], cancellationToken: server.Token));

        Assert.Equal((HttpStatusCode)statusCode, error.StatusCode);
        // AnthropicApiException hides this property with a throwing typed getter when no inner exception exists.
        Assert.Null(((Exception)error).InnerException);
        Assert.Equal(1, server.CallCount);
    }

    [Theory]
    [InlineData(429)]
    [InlineData(503)]
    public async Task TransientHttpFailureIsRetriedByHostWithEveryAttemptInBudget(int statusCode)
    {
        await using var server = new LoopbackProvider(call => call == 1 ? ErrorResponse(statusCode) : CompletedResponse(toolCall: false));
        using var fixture = new RunFixture(server, requestTool: false);

        var result = await fixture.RunAsync();

        Assert.Equal(CopilotAgentStopReason.Completed, result.StopReason);
        Assert.Equal(2, server.CallCount);
        Assert.Equal(2, result.Budget.ProviderCalls);
        Assert.Equal(1, result.Budget.ProviderRetryCount);
        Assert.Equal(statusCode == 429 ? 1 : 0, result.Budget.ProviderRateLimitRetryCount);
        var retry = Assert.Single(fixture.Events, item => item.ProviderRetry != null).ProviderRetry!;
        Assert.Equal(statusCode, retry.StatusCode);
        Assert.Equal($"HTTP {statusCode} ({(statusCode == 429 ? "rate_limit_error" : "api_error")})", retry.FailureKind);
        Assert.Equal(110, result.Usage.EffectiveTotalTokens);
        Assert.Equal(110, result.Budget.ReportedTotalTokens);
        Assert.True(result.Budget.ConsumedTokens > result.Budget.ReportedTotalTokens);
        Assert.True(result.Budget.UsedEstimatedUsage);
        Assert.Empty(result.StepRecords);
        Assert.Equal(0, fixture.Tool.CallCount);
        fixture.AssertTurnLifecycle(result);
    }

    [Theory]
    [InlineData(401, 1)]
    [InlineData(422, 1)]
    [InlineData(429, 3)]
    [InlineData(503, 3)]
    public async Task HttpFailureAfterCompletedToolPreservesFactsWithoutMultiplyingRetries(int statusCode, int failedAttempts)
    {
        await using var server = new LoopbackProvider(call => call == 1 ? CompletedResponse(toolCall: true) : ErrorResponse(statusCode));
        using var fixture = new RunFixture(server, requestTool: true);

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
        fixture.AssertTurnLifecycle(result);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QuotaFailureAfterToolKeepsActionableCauseWithoutRetrying(bool typeOnly)
    {
        var failure = ErrorResponse(429) with
        {
            Body = JsonSerializer.Serialize(new
            {
                error = new
                {
                    code = typeOnly ? "" : "insufficient_quota",
                    type = typeOnly ? "insufficient_quota" : "rate_limit_error",
                    message = "Untrusted error body echoing test-key.",
                },
            }),
        };
        await using var server = new LoopbackProvider(call => call == 1 ? CompletedResponse(toolCall: true) : failure);
        using var fixture = new RunFixture(server, requestTool: true);

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
    [InlineData(401, 1, false)]
    [InlineData(503, 3, false)]
    [InlineData(413, 1, false)]
    [InlineData(401, 1, true)]
    [InlineData(503, 3, true)]
    public async Task HttpFailureDuringNoToolsFinalizationPreservesCauseAndCompletedTool(int statusCode, int failedAttempts, bool partialAnswer)
    {
        var incomplete = CompletedResponse(toolCall: false);
        incomplete = incomplete with
        {
            Body = incomplete.Body.Replace("Completed answer.", partialAnswer ? "Partial answer." : "", StringComparison.Ordinal)
                .Replace("\"end_turn\"", partialAnswer ? "\"max_tokens\"" : "\"end_turn\"", StringComparison.Ordinal),
        };
        await using var server = new LoopbackProvider(call => call switch
        {
            1 => CompletedResponse(toolCall: true),
            2 => incomplete,
            _ => ErrorResponse(statusCode),
        });
        using var fixture = new RunFixture(server, requestTool: true);

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
        var answer = string.Concat(fixture.Events.Where(item => item.Type == CopilotAgentEventType.AnswerDelta).Select(item => item.Text));
        if (partialAnswer)
            Assert.Equal("Partial answer.", answer);
        else
            Assert.Contains(blocker.Summary, answer);
        Assert.DoesNotContain(fixture.Events, item => item.Type == CopilotAgentEventType.AnswerReset);
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
        fixture.AssertTurnLifecycle(result);
    }

    private sealed class RunFixture : IDisposable
    {
        private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("CopilotAnthropicHttpFailureTests-");
        private readonly LoopbackProvider _server;
        private readonly CopilotMicrosoftAgentFrameworkRuntime _runtime;
        private readonly CopilotAgentRequest _request;

        public RunFixture(LoopbackProvider server, bool requestTool)
        {
            _server = server;
            var catalog = new CopilotCapabilityCatalog();
            catalog.PublishSource(CopilotCapabilitySourceKind.BuiltIn, "anthropic-http-tests", "Anthropic HTTP tests", [Tool]);
            _runtime = new CopilotMicrosoftAgentFrameworkRuntime(
                new CopilotToolRegistry([Tool]),
                new CopilotAgentContextBuilder(),
                new CopilotToolExecutor(),
                CopilotMicrosoftAgentFrameworkRuntime.CreateChatClient,
                new EmptyExternalToolProvider(),
                catalog,
                new CopilotAgentSkillUsageStore(_directory.FullName));
            var prompt = requestTool
                ? "Run the bounded workspace validation probe and summarize its result."
                : "Summarize the supplied result.";
            _request = new CopilotAgentRequest
            {
                Profile = CreateProfile(server),
                ConversationId = "anthropic-http-conversation",
                TaskId = "anthropic-http-task",
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
                    TotalDuration = TimeSpan.FromSeconds(30),
                },
            };
        }

        public ValidationProbeTool Tool { get; } = new();
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

        public void Dispose() => _directory.Delete(recursive: true);
    }

    private static CopilotProfileConfig CreateProfile(LoopbackProvider server) => new()
    {
        ProviderType = CopilotProviderType.AnthropicCompatible,
        VendorType = CopilotVendorType.Custom,
        ApiKey = "test-key",
        BaseUrl = server.BaseUrl,
        Model = "test-model",
        MaxTokens = 4_096,
    };

    private static ProviderResponse ErrorResponse(int statusCode) => new(statusCode, "application/json", JsonSerializer.Serialize(new
    {
        type = "error",
        error = new
        {
            type = statusCode switch { 401 => "authentication_error", 429 => "rate_limit_error", _ => "api_error" },
            message = "Controlled HTTP failure.",
        },
    }));

    private static string Event(string type, object payload) => "event: " + type + "\ndata: " + JsonSerializer.Serialize(payload) + "\n\n";

    private static ProviderResponse CompletedResponse(bool toolCall)
    {
        var start = Event("message_start", new
        {
            type = "message_start",
            message = new
            {
                id = "msg_test", type = "message", role = "assistant", model = "test-model", content = Array.Empty<object>(),
                stop_reason = (string?)null, stop_sequence = (string?)null, usage = new { input_tokens = 100, output_tokens = 0 },
            },
        });
        var content = toolCall
            ? Event("content_block_start", new
            {
                type = "content_block_start", index = 0,
                content_block = new { type = "tool_use", id = "validation-call", name = "colorvision_run_workspace_validation", input = new { } },
            }) + Event("content_block_delta", new
            {
                type = "content_block_delta", index = 0, delta = new { type = "input_json_delta", partial_json = "{}" },
            })
            : Event("content_block_start", new
            {
                type = "content_block_start", index = 0, content_block = new { type = "text", text = string.Empty },
            }) + Event("content_block_delta", new
            {
                type = "content_block_delta", index = 0, delta = new { type = "text_delta", text = "Completed answer." },
            });
        return new ProviderResponse(200, "text/event-stream", start + content
            + Event("content_block_stop", new { type = "content_block_stop", index = 0 })
            + Event("message_delta", new
            {
                type = "message_delta", delta = new { stop_reason = toolCall ? "tool_use" : "end_turn", stop_sequence = (string?)null },
                usage = new { input_tokens = 100, output_tokens = 10 },
            })
            + Event("message_stop", new { type = "message_stop" }));
    }

    private sealed record ProviderResponse(int StatusCode, string ContentType, string Body);

    private sealed class LoopbackProvider : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _lifetime = new(TimeSpan.FromSeconds(20));
        private readonly Func<int, ProviderResponse> _response;
        private readonly Task _serveTask;
        private int _callCount;

        public LoopbackProvider(Func<int, ProviderResponse> response)
        {
            _response = response;
            _listener.Start();
            BaseUrl = "http://127.0.0.1:" + ((IPEndPoint)_listener.LocalEndpoint).Port;
            _serveTask = ServeAsync();
        }

        public string BaseUrl { get; }
        public CancellationToken Token => _lifetime.Token;
        public int CallCount => Volatile.Read(ref _callCount);
        public List<string> Payloads { get; } = [];

        private async Task ServeAsync()
        {
            try
            {
                while (true)
                {
                    using var client = await _listener.AcceptTcpClientAsync(Token);
                    await using var stream = client.GetStream();
                    using var reader = new StreamReader(stream, Encoding.Latin1, false, 1024, leaveOpen: true);
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
                    var buffer = new char[4096];
                    var requestBody = new StringBuilder();
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
                        // Keep old SDK retry probes fast without replacing production's request pipeline.
                        + "Retry-After-Ms: 1\r\nConnection: close\r\n\r\n");
                    await stream.WriteAsync(headers, Token);
                    await stream.WriteAsync(body, Token);
                    await stream.FlushAsync(Token);
                }
            }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
            {
            }
            catch (SocketException) when (_lifetime.IsCancellationRequested)
            {
            }
        }

        public async ValueTask DisposeAsync()
        {
            _lifetime.Cancel();
            _listener.Stop();
            try { await _serveTask; }
            finally { _lifetime.Dispose(); }
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
