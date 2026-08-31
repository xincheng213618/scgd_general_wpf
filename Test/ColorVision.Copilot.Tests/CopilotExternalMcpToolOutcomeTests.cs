using ColorVision.Copilot;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotExternalMcpToolOutcomeTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);

    [Theory]
    [InlineData("send")]
    [InlineData("body")]
    [InlineData("http")]
    [InlineData("malformed")]
    [InlineData("truncated")]
    [InlineData("invalid-result")]
    [InlineData("null-content")]
    public async Task LostWriteResponseRetainsUnknownOutcomeAfterTheServerPerformedItsWork(string failure)
    {
        using var handler = new McpHandler(failure switch
        {
            "body" => ResponseMode.BodyReadFailure,
            "http" => ResponseMode.HttpFailure,
            "malformed" => ResponseMode.MalformedJson,
            "truncated" => ResponseMode.TruncatedJson,
            "invalid-result" => ResponseMode.InvalidResult,
            "null-content" => ResponseMode.NullContent,
            _ => ResponseMode.SendFailure,
        });
        await using var lease = await DiscoverAsync(handler);
        var tool = Assert.Single(lease.Tools);
        var events = new ConcurrentQueue<CopilotAgentEvent>();

        var outcome = await new CopilotToolExecutor().ExecuteAsync(
            CreateInvocation(tool), events.Enqueue, CancellationToken.None).WaitAsync(TestTimeout);

        Assert.Equal(1, handler.ToolCallCount);
        Assert.Equal(1, handler.CompletedServerWorkCount);
        Assert.False(outcome.Result.Success);
        Assert.Equal(CopilotToolFailureKind.OutcomeUnknown, outcome.Result.FailureKind);
        Assert.Equal(CopilotToolFailureCode.OutcomeUnknown, outcome.Result.FailureCode);
        Assert.False(outcome.Execution.RetryEligible);
        var terminal = Assert.Single(events, item => item.Type == CopilotAgentEventType.ToolResult);
        Assert.Equal(CopilotToolFailureCode.OutcomeUnknown, terminal.ToolResult?.FailureCode);
        using var modelResult = JsonDocument.Parse(CopilotFrameworkToolResultFormatter.Format(outcome));
        Assert.Equal("outcome_unknown", modelResult.RootElement.GetProperty("failure_kind").GetString());
        Assert.Equal(CopilotToolFailureCode.OutcomeUnknown, modelResult.RootElement.GetProperty("failure_code").GetString());
        Assert.False(modelResult.RootElement.GetProperty("retry_allowed").GetBoolean());
        AssertCheckpointRequiresReplan(events, tool);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExplicitToolResponseRemainsAnAuthoritativeResult(bool isError)
    {
        using var handler = new McpHandler(isError ? ResponseMode.ToolError : ResponseMode.Success);
        await using var lease = await DiscoverAsync(handler);

        var outcome = await new CopilotToolExecutor().ExecuteAsync(
            CreateInvocation(Assert.Single(lease.Tools)), _ => { }, CancellationToken.None).WaitAsync(TestTimeout);

        Assert.Equal(1, handler.ToolCallCount);
        Assert.Equal(!isError, outcome.Result.Success);
        Assert.Equal(isError ? CopilotToolFailureKind.Unspecified : CopilotToolFailureKind.None, outcome.Result.FailureKind);
        Assert.NotEqual(CopilotToolFailureCode.OutcomeUnknown, outcome.Result.FailureCode);
        Assert.Equal(isError ? "Operation rejected." : string.Empty, outcome.Result.ErrorMessage);
        Assert.Equal(isError ? string.Empty : "Operation completed.", outcome.Result.Content);
        Assert.False(outcome.Execution.RetryEligible);
    }

    [Fact]
    public async Task LostReadOnlyResponseRemainsEligibleForTheExistingBoundedRetry()
    {
        using var handler = new McpHandler(ResponseMode.SendFailure);
        await using var lease = await DiscoverAsync(handler, CopilotMcpClientAccessPolicy.ReadOnly);

        var outcome = await new CopilotToolExecutor().ExecuteAsync(
            CreateInvocation(Assert.Single(lease.Tools), approvalGranted: false), _ => { }, CancellationToken.None).WaitAsync(TestTimeout);

        Assert.Equal(1, handler.ToolCallCount);
        Assert.Equal(CopilotToolFailureKind.Transient, outcome.Result.FailureKind);
        Assert.NotEqual(CopilotToolFailureCode.OutcomeUnknown, outcome.Result.FailureCode);
        Assert.True(outcome.Execution.RetryEligible);
    }

    [Fact]
    public async Task ExplicitJsonRpcRejectionDoesNotBecomeAnUnknownOutcome()
    {
        using var handler = new McpHandler(ResponseMode.ProtocolError);
        await using var lease = await DiscoverAsync(handler);

        var outcome = await new CopilotToolExecutor().ExecuteAsync(
            CreateInvocation(Assert.Single(lease.Tools)), _ => { }, CancellationToken.None).WaitAsync(TestTimeout);

        Assert.Equal(1, handler.ToolCallCount);
        Assert.Equal(0, handler.CompletedServerWorkCount);
        Assert.False(outcome.Result.Success);
        Assert.NotEqual(CopilotToolFailureKind.OutcomeUnknown, outcome.Result.FailureKind);
        Assert.NotEqual(CopilotToolFailureCode.OutcomeUnknown, outcome.Result.FailureCode);
        Assert.False(outcome.Execution.RetryEligible);
    }

    [Fact]
    public async Task WriteWithoutApprovalDoesNotCrossTheRemoteCallBoundary()
    {
        using var handler = new McpHandler(ResponseMode.SendFailure);
        await using var lease = await DiscoverAsync(handler);

        var outcome = await new CopilotToolExecutor().ExecuteAsync(
            CreateInvocation(Assert.Single(lease.Tools), approvalGranted: false), _ => { }, CancellationToken.None).WaitAsync(TestTimeout);

        Assert.False(outcome.Result.Success);
        Assert.NotEqual(CopilotToolFailureKind.OutcomeUnknown, outcome.Result.FailureKind);
        Assert.Equal(0, handler.ToolCallCount);
        Assert.Equal(0, handler.CompletedServerWorkCount);
    }

    [Fact]
    public async Task InvalidArgumentsDoNotCrossTheRemoteCallBoundary()
    {
        using var handler = new McpHandler(ResponseMode.SendFailure);
        await using var lease = await DiscoverAsync(handler);

        var outcome = await new CopilotToolExecutor().ExecuteAsync(
            CreateInvocation(Assert.Single(lease.Tools), input: CopilotAgentToolInput.Empty), _ => { }, CancellationToken.None).WaitAsync(TestTimeout);

        Assert.Equal(CopilotToolFailureKind.Validation, outcome.Result.FailureKind);
        Assert.NotEqual(CopilotToolFailureCode.OutcomeUnknown, outcome.Result.FailureCode);
        Assert.Equal(0, handler.ToolCallCount);
        Assert.Equal(0, handler.CompletedServerWorkCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvalidNestedArgumentsDoNotCrossTheRemoteCallBoundary(bool unknownProperty)
    {
        using var handler = new McpHandler(ResponseMode.SendFailure);
        await using var lease = await DiscoverAsync(handler);
        var input = new CopilotAgentToolInput
        {
            Arguments = new Dictionary<string, object?>
            {
                ["operationId"] = "test-operation",
                ["options"] = unknownProperty
                    ? new Dictionary<string, object?> { ["unexpected"] = true }
                    : new Dictionary<string, object?> { ["mode"] = "unsafe" },
            },
        };

        var outcome = await new CopilotToolExecutor().ExecuteAsync(
            CreateInvocation(Assert.Single(lease.Tools), input: input), _ => { }, CancellationToken.None).WaitAsync(TestTimeout);

        Assert.Equal(CopilotToolFailureKind.Validation, outcome.Result.FailureKind);
        Assert.Equal(0, handler.ToolCallCount);
        Assert.Equal(0, handler.CompletedServerWorkCount);
    }

    [Fact]
    public async Task CancellingAnAwaitedWriteResponseDoesNotProveTheRemoteWorkWasCancelled()
    {
        using var handler = new McpHandler(ResponseMode.AwaitCancellation);
        await using var lease = await DiscoverAsync(handler);
        using var cancellation = new CancellationTokenSource();
        var tool = Assert.IsAssignableFrom<ICopilotFrameworkApprovedTool>(Assert.Single(lease.Tools));
        var invocation = CreateInvocation(tool);
        var call = tool.ExecuteApprovedAsync(invocation.AgentRequest, invocation.ToolInput, cancellation.Token);
        try
        {
            await handler.CallStarted.Task.WaitAsync(TestTimeout);
            cancellation.Cancel();
            var result = await call.WaitAsync(TestTimeout);

            Assert.Equal(1, handler.CompletedServerWorkCount);
            Assert.False(result.Success);
            Assert.Equal(CopilotToolFailureKind.OutcomeUnknown, result.FailureKind);
            Assert.Equal(CopilotToolFailureCode.OutcomeUnknown, result.FailureCode);
        }
        finally
        {
            cancellation.Cancel();
        }
    }

    [Fact]
    public async Task CancellationBeforeAnApprovedCallDoesNotDispatchRemoteWork()
    {
        using var handler = new McpHandler(ResponseMode.SendFailure);
        await using var lease = await DiscoverAsync(handler);
        var tool = Assert.IsAssignableFrom<ICopilotFrameworkApprovedTool>(Assert.Single(lease.Tools));
        var invocation = CreateInvocation(tool);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => tool.ExecuteApprovedAsync(
            invocation.AgentRequest, invocation.ToolInput, new CancellationToken(canceled: true)));

        Assert.Equal(0, handler.ToolCallCount);
        Assert.Equal(0, handler.CompletedServerWorkCount);
    }

    private static void AssertCheckpointRequiresReplan(IEnumerable<CopilotAgentEvent> events, ICopilotTool tool)
    {
        var journal = new CopilotAgentTaskEventJournalBuilder();
        journal.RecordRunStarted();
        foreach (var agentEvent in events)
            journal.Observe(agentEvent);
        journal.RecordStop(CopilotAgentStopReason.Interrupted);
        var profile = new CopilotProfileConfig
        {
            VendorType = CopilotVendorType.Custom,
            ProviderType = CopilotProviderType.OpenAICompatible,
            ApiKey = "mcp-outcome-test-key",
            BaseUrl = "https://example.test/v1",
            Model = "test-model",
        };
        var catalog = new CopilotCapabilityCatalog();
        catalog.PublishSource(CopilotCapabilitySourceKind.ExternalMcp, "mcp-outcome-tests", "MCP outcome tests", [tool]);
        var capabilities = catalog.GetSnapshot();
        var checkpoint = Assert.IsType<CopilotAgentSessionCheckpoint>(CopilotAgentSessionCheckpoint.Create(
            profile, "{}", capabilities, taskEventJournal: journal.Snapshot()));

        var compatibility = checkpoint.EvaluateFor(profile, capabilities);

        Assert.Equal(CopilotAgentCheckpointCompatibilityKind.UncertainToolOutcome, compatibility.Kind);
        Assert.False(compatibility.CanResume);
        Assert.True(compatibility.RequiresReplan);
    }

    private static async Task<CopilotExternalToolLease> DiscoverAsync(
        McpHandler handler, CopilotMcpClientAccessPolicy accessPolicy = CopilotMcpClientAccessPolicy.RequireApproval)
    {
        var name = "outcome-" + Guid.NewGuid().ToString("N");
        var provider = new CopilotMcpToolProvider(new CopilotMcpToolDiscoveryCache(), new CopilotCapabilityCatalog(), handler.CreateClient);
        var lease = await provider.DiscoverAsync(new CopilotAgentRequest
        {
            ExternalMcpServers = [new CopilotMcpClientServerConfig
            {
                Name = name,
                Endpoint = "https://mcp.example.test/" + name,
                AccessPolicy = accessPolicy,
            }],
        }, CancellationToken.None).WaitAsync(TestTimeout);
        Assert.Single(lease.Tools);
        return lease;
    }

    private static CopilotToolInvocation CreateInvocation(
        ICopilotTool tool, bool approvalGranted = true, CopilotAgentToolInput? input = null) => new()
    {
        CallId = "mcp-outcome-call",
        Round = 1,
        Attempt = 1,
        MaxAttempts = 2,
        RuntimeName = "test",
        Tool = tool,
        FrameworkApprovalGranted = approvalGranted,
        AgentRequest = new CopilotAgentRequest { Mode = CopilotAgentMode.Auto, UserText = "Run the requested external operation." },
        ToolInput = input ?? new CopilotAgentToolInput { Arguments = new Dictionary<string, object?> { ["operationId"] = "test-operation" } },
    };

    private enum ResponseMode { Success, ToolError, ProtocolError, SendFailure, BodyReadFailure, HttpFailure, MalformedJson, TruncatedJson, InvalidResult, NullContent, AwaitCancellation }

    private sealed class McpHandler(ResponseMode responseMode) : HttpMessageHandler
    {
        private int _toolCallCount;
        private int _completedServerWorkCount;
        public int ToolCallCount => Volatile.Read(ref _toolCallCount);
        public int CompletedServerWorkCount => Volatile.Read(ref _completedServerWorkCount);
        public TaskCompletionSource CallStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public HttpClient CreateClient() => new(this, disposeHandler: false) { Timeout = Timeout.InfiniteTimeSpan };

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method != HttpMethod.Post)
                return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed);
            Assert.Null(request.Headers.Authorization);
            using var payload = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
            var root = payload.RootElement;
            var method = root.GetProperty("method").GetString();
            if (method is "notifications/initialized" or "notifications/cancelled")
                return new HttpResponseMessage(HttpStatusCode.Accepted);
            var id = root.GetProperty("id").Clone();
            if (method == "server/discover")
                return JsonResponse(new { jsonrpc = "2.0", id, error = new { code = -32601, message = "Method not found" } });
            if (method == "initialize")
            {
                return JsonResponse(new
                {
                    jsonrpc = "2.0", id,
                    result = new
                    {
                        protocolVersion = root.GetProperty("params").GetProperty("protocolVersion").GetString(),
                        capabilities = new { tools = new { } },
                        serverInfo = new { name = "fake-mcp", version = "1.0" },
                    },
                });
            }
            if (method == "tools/list")
            {
                return JsonResponse(new
                {
                    jsonrpc = "2.0", id,
                    result = new
                    {
                        tools = new[] { new
                        {
                            name = "run_operation",
                            description = "Run an operation in the fake MCP test handler.",
                            inputSchema = new
                            {
                                type = "object",
                                properties = new
                                {
                                    operationId = new { type = "string" },
                                    options = new
                                    {
                                        type = "object",
                                        properties = new { mode = new { type = "string", @enum = new[] { "safe" } } },
                                        additionalProperties = false,
                                    },
                                },
                                required = new[] { "operationId" },
                                additionalProperties = false,
                            },
                        } },
                    },
                });
            }
            Assert.Equal("tools/call", method);
            Assert.Equal("test-operation", root.GetProperty("params").GetProperty("arguments").GetProperty("operationId").GetString());
            Interlocked.Increment(ref _toolCallCount);
            if (responseMode == ResponseMode.ProtocolError)
                return JsonResponse(new { jsonrpc = "2.0", id, error = new { code = -32602, message = "The fake server rejected the arguments." } });
            Interlocked.Increment(ref _completedServerWorkCount);
            CallStarted.TrySetResult();
            if (responseMode == ResponseMode.AwaitCancellation)
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            if (responseMode == ResponseMode.SendFailure)
                throw new IOException("The response connection was lost after the fake server completed its work.");
            if (responseMode == ResponseMode.HttpFailure)
                return new HttpResponseMessage(HttpStatusCode.GatewayTimeout) { Content = new StringContent("The gateway lost the completed operation response.") };
            if (responseMode is ResponseMode.MalformedJson or ResponseMode.TruncatedJson or ResponseMode.InvalidResult or ResponseMode.NullContent)
            {
                var body = "{\"jsonrpc\":\"2.0\",\"id\":" + id.GetRawText()
                    + (responseMode switch
                    {
                        ResponseMode.MalformedJson => ",\"result\":broken}",
                        ResponseMode.TruncatedJson => ",\"result\":{\"content\":[",
                        ResponseMode.NullContent => ",\"result\":{\"content\":null}}",
                        _ => ",\"result\":{\"content\":[{\"type\":\"text\",\"text\":42}]}}",
                    });
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            }
            if (responseMode == ResponseMode.BodyReadFailure)
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new FailingResponseStream()) };
                response.Content.Headers.ContentType = new("application/json");
                return response;
            }
            var isError = responseMode == ResponseMode.ToolError;
            return JsonResponse(new
            {
                jsonrpc = "2.0", id,
                result = new { isError, content = new[] { new { type = "text", text = isError ? "Operation rejected." : "Operation completed." } } },
            });
        }

        private static HttpResponseMessage JsonResponse(object payload) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
        };
    }

    private sealed class FailingResponseStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => throw new IOException("The fake response body was lost.");
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => ValueTask.FromException<int>(new IOException("The fake response body was lost."));
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
