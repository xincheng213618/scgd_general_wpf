using ColorVision.Copilot.Mcp;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotFlowRunQueryToolTests
{
    [Fact]
    public void BothSurfacesShareTheReadOnlyDiscoveryContract()
    {
        var definition = CopilotSharedCapabilityCatalog.FlowRuns;
        var dispatcher = Dispatcher((_, _, _, _) => throw new InvalidOperationException("Discovery must not query storage."));
        var tool = Assert.IsType<CopilotListFlowRunsTool>(definition.CreateAgentTool(dispatcher));
        var descriptor = Assert.Single(dispatcher.ListTools(), item => item.Name == "list_flow_runs");
        Assert.Equal("flow.runs", definition.Id);
        Assert.Equal("ListFlowRuns", tool.Name);
        Assert.Equal(CopilotSharedCapabilityExecutionRoute.ApplicationCapabilityRuntime, definition.ExecutionRoute);
        Assert.Same(dispatcher, tool.ApplicationCapabilityInvoker);
        Assert.Same(definition.AgentInputSchema, tool.InputSchema);
        Assert.Same(definition.AgentInputSchema, definition.McpInputSchema);
        Assert.True(JsonElement.DeepEquals(tool.InputSchema.JsonSchema, Assert.IsType<JsonElement>(descriptor.InputSchema)));
        Assert.Equal(CopilotToolAccess.ReadOnly, tool.Capability.Access);
        Assert.Equal(CopilotToolIdempotency.Idempotent, tool.Capability.Idempotency);
        Assert.Equal(CopilotToolConcurrencyMode.SharedRead, tool.Capability.EffectiveConcurrencyMode);
        Assert.Equal(CopilotToolApprovalMode.Never, tool.Capability.ApprovalMode);
        Assert.Equal(CopilotToolAuditArgumentMode.NamesOnly, tool.Capability.AuditArgumentMode);
        Assert.Equal(TimeSpan.FromSeconds(15), tool.Capability.EffectiveExecutionTimeout);
        Assert.True(Assert.IsType<bool>(descriptor.Annotations["readOnlyHint"]));
        Assert.True(Assert.IsType<bool>(descriptor.Annotations["idempotentHint"]));
        Assert.False(Assert.IsType<bool>(descriptor.Annotations["destructiveHint"]));
        var properties = tool.InputSchema.JsonSchema.GetProperty("properties");
        Assert.Equal(1, properties.GetProperty("flow_key").GetProperty("minLength").GetInt32());
        Assert.Equal(256, properties.GetProperty("flow_key").GetProperty("maxLength").GetInt32());
        Assert.Equal(1, properties.GetProperty("max_results").GetProperty("minimum").GetInt32());
        Assert.Equal(100, properties.GetProperty("max_results").GetProperty("maximum").GetInt32());
        Assert.Equal(20, properties.GetProperty("max_results").GetProperty("default").GetInt32());
        Assert.Equal(1, properties.GetProperty("cursor").GetProperty("minLength").GetInt32());
        Assert.Equal(4096, properties.GetProperty("cursor").GetProperty("maxLength").GetInt32());
        var registry = new CopilotToolRegistry([tool]);
        foreach (var mode in Enum.GetValues<CopilotAgentMode>().Where(mode => mode != CopilotAgentMode.Chat))
            Assert.Same(tool, Assert.Single(registry.FindTools(Request(mode))));
        Assert.Empty(registry.FindTools(Request(CopilotAgentMode.Chat)));
    }

    [Theory]
    [InlineData(null, null, null)]
    [InlineData(" \u2003Flow/Case-Sensitive\t ", 100, "  opaque:+/= cursor\t ")]
    public async Task BothSurfacesForwardRawInputsAndPublishTheWholeJsonBody(string? flowKey, int? maximum, string? cursor)
    {
        var calls = new List<(string? Key, int Maximum, string? Cursor)>();
        var body = JsonSerializer.Serialize(new
        {
            runs = new[] { new { run_record_id = int.MaxValue, flow_key = "Flow/Case-Sensitive" } },
            has_more = true, next_cursor = "opaque-next", coverage = new { query_scope = "live retained records" },
        });
        var dispatcher = Dispatcher((key, count, continuation, _) =>
        {
            calls.Add((key, count, continuation));
            return Task.FromResult(new CopilotCapabilityResult { Success = true, Summary = "Do not prefix JSON", Content = body });
        });
        var arguments = new Dictionary<string, object?>();
        if (flowKey != null) arguments["flow_key"] = flowKey;
        if (maximum.HasValue) arguments["max_results"] = maximum.Value;
        if (cursor != null) arguments["cursor"] = cursor;
        var tool = new CopilotListFlowRunsTool(dispatcher);
        Assert.True(tool.InputSchema.TryBind(arguments, out var input, out var error), error);
        var agent = await tool.ExecuteAsync(Request(), input, CancellationToken.None);
        var mcp = await dispatcher.CallAsync("list_flow_runs", CopilotFlowReadToolBase.ToJsonArguments(input), CancellationToken.None);
        Assert.True(agent.Success, agent.ErrorMessage);
        Assert.True(mcp.Success, mcp.Text);
        Assert.Equal(new[] { (flowKey, maximum ?? 20, cursor), (flowKey, maximum ?? 20, cursor) }, calls);
        Assert.Equal(body, agent.Content);
        Assert.Equal(body, mcp.Text);
        using var parsed = JsonDocument.Parse(agent.Content);
        Assert.Equal(int.MaxValue, parsed.RootElement.GetProperty("runs")[0].GetProperty("run_record_id").GetInt32());
        Assert.True(parsed.RootElement.GetProperty("has_more").GetBoolean());
        Assert.Equal("opaque-next", parsed.RootElement.GetProperty("next_cursor").GetString());
        Assert.Null(agent.Approval);
        Assert.False(mcp.RequiresApproval);
    }

    [Theory]
    [InlineData("{\"flow_key\":\"\"}")]
    [InlineData("{\"flow_key\":\" \\t\"}")]
    [InlineData("{\"flow_key\":null}")]
    [InlineData("{\"flow_key\":7}")]
    [InlineData("{\"max_results\":0}")]
    [InlineData("{\"max_results\":101}")]
    [InlineData("{\"max_results\":1.0}")]
    [InlineData("{\"max_results\":\"20\"}")]
    [InlineData("{\"cursor\":\"\"}")]
    [InlineData("{\"cursor\":null}")]
    [InlineData("{\"cursor\":42}")]
    [InlineData("{\"template_id\":7}")]
    public Task InvalidInputsNeverReachEitherProvider(string json) => InvalidAsync(json);

    [Fact]
    public async Task OversizedFilterAndCursorAreRejectedBeforeTheProvider()
    {
        await InvalidAsync(JsonSerializer.Serialize(new { flow_key = new string('f', 257) }));
        await InvalidAsync(JsonSerializer.Serialize(new { cursor = new string('c', 4097) }));
    }

    [Theory]
    [InlineData(CopilotToolFailureKind.Validation)]
    [InlineData(CopilotToolFailureKind.Transient)]
    public async Task BothSurfacesKeepProviderFailureClassification(CopilotToolFailureKind kind)
    {
        var dispatcher = Dispatcher((_, _, _, _) => Task.FromResult(new CopilotCapabilityResult
        {
            Success = false, FailureKind = kind, ErrorMessage = "The run query failed at its declared boundary.",
        }));
        var agent = await new CopilotListFlowRunsTool(dispatcher).ExecuteAsync(Request(), CopilotAgentToolInput.Empty, CancellationToken.None);
        var mcp = await dispatcher.CallAsync("list_flow_runs", null, CancellationToken.None);
        Assert.False(agent.Success);
        Assert.False(mcp.Success);
        Assert.Equal(kind, agent.FailureKind);
        Assert.Equal(kind, mcp.FailureKind);
        Assert.Equal(agent.Content, mcp.Text);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationReachesTheProviderAndPropagates(bool agentSurface)
    {
        var entered = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var dispatcher = Dispatcher(async (_, _, _, token) =>
        {
            entered.TrySetResult(token);
            try { await Task.Delay(Timeout.InfiniteTimeSpan, token); return new CopilotCapabilityResult { Success = true }; }
            finally { stopped.TrySetResult(); }
        });
        using var cancellation = new CancellationTokenSource();
        Task call = agentSurface
            ? new CopilotListFlowRunsTool(dispatcher).ExecuteAsync(Request(), CopilotAgentToolInput.Empty, cancellation.Token)
            : dispatcher.CallAsync("list_flow_runs", null, cancellation.Token);
        var providerToken = await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call);
        await stopped.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(providerToken.IsCancellationRequested);
    }

    private static async Task InvalidAsync(string json)
    {
        var calls = 0;
        var dispatcher = Dispatcher((_, _, _, _) => { calls++; throw new InvalidOperationException("Invalid input reached provider."); });
        var arguments = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
        var agentArguments = arguments.ToDictionary(pair => pair.Key, pair => (object?)pair.Value);
        var agent = await new CopilotListFlowRunsTool(dispatcher).ExecuteAsync(Request(), new CopilotAgentToolInput { Arguments = agentArguments }, CancellationToken.None);
        var mcp = await dispatcher.CallAsync("list_flow_runs", arguments, CancellationToken.None);
        Assert.False(agent.Success);
        Assert.False(mcp.Success);
        Assert.Equal(CopilotToolFailureKind.Validation, agent.FailureKind);
        Assert.Equal(CopilotToolFailureKind.Validation, mcp.FailureKind);
        Assert.Equal(0, calls);
    }

    private static CopilotAgentRequest Request(CopilotAgentMode mode = CopilotAgentMode.Auto) => new()
    {
        Mode = mode, UserText = "Inspect retained flow runs.", ConversationId = "flow-run-query", TaskId = "flow-run-query-task",
    };
    private static CopilotMcpToolDispatcher Dispatcher(Func<string?, int, string?, CancellationToken, Task<CopilotCapabilityResult>> query) => new(new CopilotMcpToolEnvironment
    {
        WorkspaceSnapshotProvider = () => new CopilotMcpWorkspaceSnapshot(), FlowRunQueryProvider = query,
    });
}
