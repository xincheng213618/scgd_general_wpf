using ColorVision.Copilot.Mcp;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotFlowRunAttemptsToolTests
{
    [Fact]
    public void BothSurfacesPublishTheSameReadOnlyContractWithoutReadingTheJournal()
    {
        var definition = CopilotSharedCapabilityCatalog.FlowRunAttempts;
        var dispatcher = Dispatcher((_, _, _, _, _) => throw new InvalidOperationException("Discovery must not read the journal."));
        var tool = Assert.IsType<CopilotInspectFlowRunAttemptsTool>(definition.CreateAgentTool(dispatcher));
        var descriptor = Assert.Single(dispatcher.ListTools(), item => item.Name == "inspect_flow_run_attempts");
        Assert.Equal("flow.run-attempts", definition.Id);
        Assert.Equal("InspectFlowRunAttempts", tool.Name);
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
        Assert.Equal(1, properties.GetProperty("node_id").GetProperty("minLength").GetInt32());
        Assert.Equal(128, properties.GetProperty("node_id").GetProperty("maxLength").GetInt32());
        Assert.Equal(1, properties.GetProperty("max_attempts").GetProperty("minimum").GetInt32());
        Assert.Equal(100, properties.GetProperty("max_attempts").GetProperty("maximum").GetInt32());
        Assert.Equal(40, properties.GetProperty("max_attempts").GetProperty("default").GetInt32());
        Assert.Equal(1, properties.GetProperty("cursor").GetProperty("minLength").GetInt32());
        Assert.Equal(4096, properties.GetProperty("cursor").GetProperty("maxLength").GetInt32());
        var registry = new CopilotToolRegistry([tool]);
        foreach (var mode in Enum.GetValues<CopilotAgentMode>().Where(mode => mode != CopilotAgentMode.Chat))
            Assert.Single(registry.FindTools(Request(mode)));
        Assert.Empty(registry.FindTools(Request(CopilotAgentMode.Chat)));
    }

    [Theory]
    [InlineData(1, null, null, null)]
    [InlineData(17, "  opaque-node/non-guid  ", 1, null)]
    [InlineData(int.MaxValue, "Node/A", 100, "  opaque:cursor==  ")]
    public async Task BothSurfacesPreserveExactRunDefaultsCursorAndWholeJson(int runId, string? nodeId, int? maximum, string? cursor)
    {
        var calls = new List<(int, string?, int, string?)>();
        var attemptRows = new[] { new { attempt_id = long.MaxValue - 1, attempt_no = 1 }, new { attempt_id = long.MaxValue, attempt_no = 1 } }.Take(maximum ?? 40).ToArray();
        var payload = JsonSerializer.Serialize(new
        {
            run_record_id = runId, attempts = attemptRows,
            has_more = true, next_cursor = "opaque earlier cursor", coverage = new { window = cursor == null ? "latest" : "earlier" },
        });
        var dispatcher = Dispatcher((actualRun, actualNode, count, continuation, _) =>
        {
            calls.Add((actualRun, actualNode, count, continuation));
            return Task.FromResult(new CopilotCapabilityResult
            {
                Success = true, Content = payload, Summary = "Must not prefix JSON.", PartialResultMessage = "Earlier attempt windows remain.",
            });
        });
        var arguments = new Dictionary<string, object?> { ["run_record_id"] = runId };
        if (nodeId != null) arguments["node_id"] = nodeId;
        if (maximum.HasValue) arguments["max_attempts"] = maximum.Value;
        if (cursor != null) arguments["cursor"] = cursor;
        var tool = new CopilotInspectFlowRunAttemptsTool(dispatcher);
        Assert.True(tool.InputSchema.TryBind(arguments, out var input, out var error), error);
        var agent = await tool.ExecuteAsync(Request(), input, CancellationToken.None);
        var mcp = await dispatcher.CallAsync("inspect_flow_run_attempts", CopilotFlowReadToolBase.ToJsonArguments(input), CancellationToken.None);
        Assert.True(agent.Success, agent.ErrorMessage);
        Assert.True(mcp.Success, mcp.Text);
        Assert.Equal(new[] { (runId, nodeId, maximum ?? 40, cursor), (runId, nodeId, maximum ?? 40, cursor) }, calls);
        Assert.Equal(payload, agent.Content);
        Assert.Equal(payload, mcp.Text);
        using var agentJson = JsonDocument.Parse(agent.Content);
        using var mcpJson = JsonDocument.Parse(mcp.Text);
        Assert.Equal(runId, agentJson.RootElement.GetProperty("run_record_id").GetInt32());
        Assert.Equal(attemptRows.Select(item => item.attempt_id), agentJson.RootElement.GetProperty("attempts").EnumerateArray().Select(item => item.GetProperty("attempt_id").GetInt64()));
        Assert.True(agentJson.RootElement.GetProperty("has_more").GetBoolean());
        Assert.Equal("opaque earlier cursor", agentJson.RootElement.GetProperty("next_cursor").GetString());
        Assert.True(JsonElement.DeepEquals(agentJson.RootElement, mcpJson.RootElement));
        Assert.Null(agent.Approval);
        Assert.Equal("Earlier attempt windows remain.", agent.PartialResultMessage);
        Assert.Equal(agent.PartialResultMessage, mcp.PartialResultMessage);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"run_record_id\":0}")]
    [InlineData("{\"run_record_id\":-1}")]
    [InlineData("{\"run_record_id\":2147483648}")]
    [InlineData("{\"run_record_id\":1.0}")]
    [InlineData("{\"run_record_id\":\"1\"}")]
    [InlineData("{\"run_record_id\":null}")]
    [InlineData("{\"run_record_id\":1,\"max_attempts\":0}")]
    [InlineData("{\"run_record_id\":1,\"max_attempts\":101}")]
    [InlineData("{\"run_record_id\":1,\"max_attempts\":40.0}")]
    [InlineData("{\"run_record_id\":1,\"node_id\":\"\"}")]
    [InlineData("{\"run_record_id\":1,\"node_id\":\" \\t\"}")]
    [InlineData("{\"run_record_id\":1,\"node_id\":null}")]
    [InlineData("{\"run_record_id\":1,\"node_id\":7}")]
    [InlineData("{\"run_record_id\":1,\"cursor\":\"\"}")]
    [InlineData("{\"run_record_id\":1,\"cursor\":42}")]
    [InlineData("{\"run_record_id\":1,\"cursor\":null}")]
    [InlineData("{\"run_record_id\":1,\"incident_id\":2}")]
    [MemberData(nameof(OversizedCursorArguments))]
    public async Task InvalidArgumentsDoNotReachEitherProviderRoute(string json)
    {
        var calls = 0;
        var dispatcher = Dispatcher((_, _, _, _, _) => { calls++; throw new InvalidOperationException("Invalid arguments reached run attempt inspection."); });
        var arguments = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
        var values = arguments.ToDictionary(item => item.Key, item => (object?)item.Value);
        var tool = new CopilotInspectFlowRunAttemptsTool(dispatcher);
        var agent = await tool.ExecuteAsync(Request(), new CopilotAgentToolInput { Arguments = values }, CancellationToken.None);
        var mcp = await dispatcher.CallAsync("inspect_flow_run_attempts", arguments, CancellationToken.None);
        Assert.False(agent.Success);
        Assert.False(mcp.Success);
        Assert.Equal(CopilotToolFailureKind.Validation, agent.FailureKind);
        Assert.Equal(agent.FailureKind, mcp.FailureKind);
        Assert.Equal(0, calls);
    }

    public static IEnumerable<object[]> OversizedCursorArguments =>
    [
        [JsonSerializer.Serialize(new { run_record_id = 1, cursor = new string('x', 4097) })],
        [JsonSerializer.Serialize(new { run_record_id = 1, node_id = new string('n', 129) })],
    ];

    [Theory]
    [InlineData(CopilotToolFailureKind.NotFound)]
    [InlineData(CopilotToolFailureKind.Validation)]
    [InlineData(CopilotToolFailureKind.Transient)]
    public async Task BothSurfacesPreserveInspectionFailureClassification(CopilotToolFailureKind kind)
    {
        var dispatcher = Dispatcher((_, _, _, _, _) => Task.FromResult(new CopilotCapabilityResult
        {
            Success = false, FailureKind = kind, ErrorMessage = "Exact run attempts are unavailable or the cursor is invalid.",
        }));
        var input = new CopilotAgentToolInput { Arguments = new Dictionary<string, object?> { ["run_record_id"] = 1 } };
        var agent = await new CopilotInspectFlowRunAttemptsTool(dispatcher).ExecuteAsync(Request(), input, CancellationToken.None);
        var mcp = await dispatcher.CallAsync("inspect_flow_run_attempts", CopilotFlowReadToolBase.ToJsonArguments(input), CancellationToken.None);
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
        var dispatcher = Dispatcher(async (_, _, _, _, token) =>
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
            ? new CopilotInspectFlowRunAttemptsTool(dispatcher).ExecuteAsync(Request(), input, cancellation.Token)
            : dispatcher.CallAsync("inspect_flow_run_attempts", CopilotFlowReadToolBase.ToJsonArguments(input), cancellation.Token);
        var providerToken = await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call);
        await stopped.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(providerToken.IsCancellationRequested);
    }

    private static CopilotAgentRequest Request(CopilotAgentMode mode = CopilotAgentMode.Diagnose) => new()
    {
        Mode = mode, UserText = "Read the recorded attempts for this exact run.", ConversationId = "flow-run-attempts-test", TaskId = "flow-run-attempts-task",
    };

    private static CopilotMcpToolDispatcher Dispatcher(Func<int, string?, int, string?, CancellationToken, Task<CopilotCapabilityResult>> provider) =>
        new(new CopilotMcpToolEnvironment { WorkspaceSnapshotProvider = () => new CopilotMcpWorkspaceSnapshot(), FlowRunAttemptsInspector = provider });
}
