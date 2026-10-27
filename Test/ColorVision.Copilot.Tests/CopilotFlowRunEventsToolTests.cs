using ColorVision.Copilot.Mcp;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotFlowRunEventsToolTests
{
    [Fact]
    public void BothSurfacesPublishTheSameReadOnlyContractWithoutReadingTheJournal()
    {
        var definition = CopilotSharedCapabilityCatalog.FlowRunEvents;
        var dispatcher = Dispatcher((_, _, _, _) => throw new InvalidOperationException("Discovery must not read the journal."));
        var tool = Assert.IsType<CopilotInspectFlowRunEventsTool>(definition.CreateAgentTool(dispatcher));
        var descriptor = Assert.Single(dispatcher.ListTools(), item => item.Name == "inspect_flow_run_events");
        Assert.Equal("flow.run-events", definition.Id);
        Assert.Equal("InspectFlowRunEvents", tool.Name);
        Assert.Same(definition.AgentInputSchema, definition.McpInputSchema);
        Assert.True(JsonElement.DeepEquals(tool.InputSchema.JsonSchema, Assert.IsType<JsonElement>(descriptor.InputSchema)));
        Assert.Equal(CopilotSharedCapabilityExecutionRoute.ApplicationCapabilityRuntime, definition.ExecutionRoute);
        Assert.Equal(CopilotToolAccess.ReadOnly, tool.Capability.Access);
        Assert.Equal(CopilotToolIdempotency.Idempotent, tool.Capability.Idempotency);
        Assert.Equal(CopilotToolConcurrencyMode.SharedRead, tool.Capability.EffectiveConcurrencyMode);
        Assert.Equal(CopilotToolApprovalMode.Never, tool.Capability.ApprovalMode);
        Assert.Equal(CopilotToolAuditArgumentMode.NamesOnly, tool.Capability.AuditArgumentMode);
        Assert.Equal(TimeSpan.FromSeconds(15), tool.Capability.EffectiveExecutionTimeout);
        var schema = tool.InputSchema.JsonSchema;
        Assert.Equal(new[] { "run_record_id" }, schema.GetProperty("required").EnumerateArray().Select(value => value.GetString()));
        var properties = schema.GetProperty("properties");
        Assert.Equal(1, properties.GetProperty("run_record_id").GetProperty("minimum").GetInt32());
        Assert.Equal(int.MaxValue, properties.GetProperty("run_record_id").GetProperty("maximum").GetInt32());
        Assert.Equal(1, properties.GetProperty("max_events").GetProperty("minimum").GetInt32());
        Assert.Equal(100, properties.GetProperty("max_events").GetProperty("maximum").GetInt32());
        Assert.Equal(40, properties.GetProperty("max_events").GetProperty("default").GetInt32());
        Assert.Equal(1, properties.GetProperty("cursor").GetProperty("minLength").GetInt32());
        Assert.Equal(4096, properties.GetProperty("cursor").GetProperty("maxLength").GetInt32());
        var registry = new CopilotToolRegistry([tool]);
        foreach (var mode in Enum.GetValues<CopilotAgentMode>().Where(mode => mode != CopilotAgentMode.Chat))
            Assert.Single(registry.FindTools(Request(mode)));
        Assert.Empty(registry.FindTools(Request(CopilotAgentMode.Chat)));
    }

    [Theory]
    [InlineData(1, null, null)]
    [InlineData(17, 1, null)]
    [InlineData(int.MaxValue, 100, "  opaque:cursor==  ")]
    public async Task BothSurfacesPreserveExactRunDefaultsCursorAndWholeJson(int runId, int? maximum, string? cursor)
    {
        var calls = new List<(int, int, string?)>();
        var eventRows = new[] { new { id = 30, sequence = 7 }, new { id = 31, sequence = 8 } }.Take(maximum ?? 40).ToArray();
        var payload = JsonSerializer.Serialize(new
        {
            run_record_id = runId, events = eventRows,
            has_more = true, next_cursor = "opaque earlier cursor", coverage = new { window = cursor == null ? "latest" : "earlier" },
        });
        var dispatcher = Dispatcher((actualRun, count, continuation, _) =>
        {
            calls.Add((actualRun, count, continuation));
            return Task.FromResult(new CopilotCapabilityResult
            {
                Success = true, Content = payload, Summary = "Must not prefix JSON.", PartialResultMessage = "Earlier event windows remain.",
            });
        });
        var arguments = new Dictionary<string, object?> { ["run_record_id"] = runId };
        if (maximum.HasValue) arguments["max_events"] = maximum.Value;
        if (cursor != null) arguments["cursor"] = cursor;
        var tool = new CopilotInspectFlowRunEventsTool(dispatcher);
        Assert.True(tool.InputSchema.TryBind(arguments, out var input, out var error), error);
        var agent = await tool.ExecuteAsync(Request(), input, CancellationToken.None);
        var mcp = await dispatcher.CallAsync("inspect_flow_run_events", CopilotFlowReadToolBase.ToJsonArguments(input), CancellationToken.None);
        Assert.True(agent.Success, agent.ErrorMessage);
        Assert.True(mcp.Success, mcp.Text);
        Assert.Equal(new[] { (runId, maximum ?? 40, cursor), (runId, maximum ?? 40, cursor) }, calls);
        Assert.Equal(payload, agent.Content);
        Assert.Equal(payload, mcp.Text);
        using var agentJson = JsonDocument.Parse(agent.Content);
        using var mcpJson = JsonDocument.Parse(mcp.Text);
        Assert.Equal(runId, agentJson.RootElement.GetProperty("run_record_id").GetInt32());
        Assert.Equal(eventRows.Select(item => item.sequence), agentJson.RootElement.GetProperty("events").EnumerateArray().Select(item => item.GetProperty("sequence").GetInt32()));
        Assert.True(agentJson.RootElement.GetProperty("has_more").GetBoolean());
        Assert.Equal("opaque earlier cursor", agentJson.RootElement.GetProperty("next_cursor").GetString());
        Assert.True(JsonElement.DeepEquals(agentJson.RootElement, mcpJson.RootElement));
        Assert.Null(agent.Approval);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"run_record_id\":0}")]
    [InlineData("{\"run_record_id\":-1}")]
    [InlineData("{\"run_record_id\":2147483648}")]
    [InlineData("{\"run_record_id\":1.0}")]
    [InlineData("{\"run_record_id\":\"1\"}")]
    [InlineData("{\"run_record_id\":null}")]
    [InlineData("{\"run_record_id\":1,\"max_events\":0}")]
    [InlineData("{\"run_record_id\":1,\"max_events\":101}")]
    [InlineData("{\"run_record_id\":1,\"max_events\":40.0}")]
    [InlineData("{\"run_record_id\":1,\"cursor\":\"\"}")]
    [InlineData("{\"run_record_id\":1,\"cursor\":42}")]
    [InlineData("{\"run_record_id\":1,\"cursor\":null}")]
    [InlineData("{\"run_record_id\":1,\"incident_id\":2}")]
    [MemberData(nameof(OversizedCursorArguments))]
    public async Task InvalidArgumentsDoNotReachEitherProviderRoute(string json)
    {
        var calls = 0;
        var dispatcher = Dispatcher((_, _, _, _) => { calls++; throw new InvalidOperationException("Invalid arguments reached run event inspection."); });
        var arguments = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
        var values = arguments.ToDictionary(item => item.Key, item => (object?)item.Value);
        var tool = new CopilotInspectFlowRunEventsTool(dispatcher);
        Assert.False(tool.InputSchema.TryBind(values, out _, out _));
        var agent = await tool.ExecuteAsync(Request(), new CopilotAgentToolInput { Arguments = values }, CancellationToken.None);
        var mcp = await dispatcher.CallAsync("inspect_flow_run_events", arguments, CancellationToken.None);
        Assert.False(agent.Success);
        Assert.False(mcp.Success);
        Assert.Equal(CopilotToolFailureKind.Validation, agent.FailureKind);
        Assert.Equal(agent.FailureKind, mcp.FailureKind);
        Assert.Equal(0, calls);
    }

    public static IEnumerable<object[]> OversizedCursorArguments =>
    [
        [JsonSerializer.Serialize(new { run_record_id = 1, cursor = new string('x', 4097) })],
    ];

    [Theory]
    [InlineData(CopilotToolFailureKind.NotFound)]
    [InlineData(CopilotToolFailureKind.Validation)]
    [InlineData(CopilotToolFailureKind.Transient)]
    public async Task BothSurfacesPreserveInspectionFailureClassification(CopilotToolFailureKind kind)
    {
        var dispatcher = Dispatcher((_, _, _, _) => Task.FromResult(new CopilotCapabilityResult
        {
            Success = false, FailureKind = kind, ErrorMessage = "Exact run events are unavailable or the cursor is invalid.",
        }));
        var input = new CopilotAgentToolInput { Arguments = new Dictionary<string, object?> { ["run_record_id"] = 1 } };
        var agent = await new CopilotInspectFlowRunEventsTool(dispatcher).ExecuteAsync(Request(), input, CancellationToken.None);
        var mcp = await dispatcher.CallAsync("inspect_flow_run_events", CopilotFlowReadToolBase.ToJsonArguments(input), CancellationToken.None);
        Assert.False(agent.Success);
        Assert.False(mcp.Success);
        Assert.Equal(kind, agent.FailureKind);
        Assert.Equal(kind, mcp.FailureKind);
        Assert.Equal(mcp.Text, agent.Content);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CallerCancellationReachesTheInspectorAndPropagates(bool agentSurface)
    {
        var entered = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var dispatcher = Dispatcher(async (_, _, _, token) =>
        {
            entered.TrySetResult(token);
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return new CopilotCapabilityResult { Success = true };
            }
            finally { stopped.TrySetResult(); }
        });
        var input = new CopilotAgentToolInput { Arguments = new Dictionary<string, object?> { ["run_record_id"] = 1 } };
        using var cancellation = new CancellationTokenSource();
        Task call = agentSurface
            ? new CopilotInspectFlowRunEventsTool(dispatcher).ExecuteAsync(Request(), input, cancellation.Token)
            : dispatcher.CallAsync("inspect_flow_run_events", CopilotFlowReadToolBase.ToJsonArguments(input), cancellation.Token);
        var providerToken = await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call);
        await stopped.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(providerToken.IsCancellationRequested);
    }

    private static CopilotAgentRequest Request(CopilotAgentMode mode = CopilotAgentMode.Diagnose) => new()
    {
        Mode = mode, UserText = "Read the recorded events for this exact run.", ConversationId = "flow-run-events-test", TaskId = "flow-run-events-task",
    };

    private static CopilotMcpToolDispatcher Dispatcher(Func<int, int, string?, CancellationToken, Task<CopilotCapabilityResult>> provider) =>
        new(new CopilotMcpToolEnvironment { WorkspaceSnapshotProvider = () => new CopilotMcpWorkspaceSnapshot(), FlowRunEventsInspector = provider });
}
