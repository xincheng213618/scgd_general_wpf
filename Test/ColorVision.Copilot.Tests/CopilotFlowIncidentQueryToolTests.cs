using ColorVision.Copilot.Mcp;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotFlowIncidentQueryToolTests
{
    [Fact]
    public void BothSurfacesPublishTheSameReadOnlyPagingContract()
    {
        var definition = CopilotSharedCapabilityCatalog.FlowIncidents;
        var dispatcher = CreateDispatcher((_, _, _, _) => throw new InvalidOperationException("Discovery must not query incidents."));
        var tool = Assert.IsType<CopilotListFlowIncidentsTool>(definition.CreateAgentTool(dispatcher));
        var descriptor = Assert.Single(dispatcher.ListTools(), item => item.Name == "list_flow_incidents");

        Assert.Equal("flow.incidents", definition.Id);
        Assert.Equal("ListFlowIncidents", tool.Name);
        Assert.Contains(definition, CopilotSharedCapabilityCatalog.All);
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
        Assert.Equal(tool.Capability.EffectiveExecutionTimeout, descriptor.EffectiveExecutionTimeout);
        Assert.True(Assert.IsType<bool>(descriptor.Annotations["readOnlyHint"]));
        Assert.True(Assert.IsType<bool>(descriptor.Annotations["idempotentHint"]));
        Assert.False(Assert.IsType<bool>(descriptor.Annotations["destructiveHint"]));

        var properties = tool.InputSchema.JsonSchema.GetProperty("properties");
        Assert.Equal(new[] { "active", "open", "acknowledged", "resolved", "all" },
            properties.GetProperty("state").GetProperty("enum").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal("active", properties.GetProperty("state").GetProperty("default").GetString());
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
    [InlineData("active", 100, "  opaque:+/= cursor\t ")]
    [InlineData("open", 1, null)]
    [InlineData("acknowledged", 20, null)]
    [InlineData("resolved", 20, null)]
    [InlineData("all", 20, null)]
    public async Task BothSurfacesPreserveTheFiltersCursorAndWholeJsonBody(string? state, int? maxResults, string? cursor)
    {
        const string nextCursor = "next:+/opaque=";
        var calls = new List<(string State, int Maximum, string? Cursor)>();
        var providerResult = new CopilotCapabilityResult
        {
            Success = true,
            Summary = "Recorded flow incident page.",
            Content = JsonSerializer.Serialize(new
            {
                incidents = new[] { new { incident_id = long.MaxValue, run_record_id = 17 } },
                next_cursor = nextCursor,
                coverage = new { history_snapshot = false },
            }),
        };
        var dispatcher = CreateDispatcher((filter, maximum, continuation, _) =>
        {
            calls.Add((filter, maximum, continuation));
            return Task.FromResult(providerResult);
        });
        var tool = new CopilotListFlowIncidentsTool(dispatcher);
        var arguments = new Dictionary<string, object?>();
        if (state != null)
            arguments["state"] = state;
        if (maxResults.HasValue)
            arguments["max_results"] = maxResults.Value;
        if (cursor != null)
            arguments["cursor"] = cursor;
        Assert.True(tool.InputSchema.TryBind(arguments, out var input, out var error), error);

        var agent = await tool.ExecuteAsync(Request(), input, CancellationToken.None);
        var mcp = await dispatcher.CallAsync("list_flow_incidents", CopilotFlowReadToolBase.ToJsonArguments(input), CancellationToken.None);

        Assert.True(agent.Success, agent.ErrorMessage);
        Assert.True(mcp.Success, mcp.Text);
        Assert.Equal(new[] { (state ?? "active", maxResults ?? 20, cursor), (state ?? "active", maxResults ?? 20, cursor) }, calls);
        Assert.Equal(providerResult.Content, agent.Content);
        Assert.Equal(agent.Content, mcp.Text);
        Assert.DoesNotContain(providerResult.Summary, agent.Content, StringComparison.Ordinal);
        foreach (var content in new[] { agent.Content, mcp.Text })
        {
            using var document = JsonDocument.Parse(content);
            Assert.Equal(long.MaxValue, Assert.Single(document.RootElement.GetProperty("incidents").EnumerateArray()).GetProperty("incident_id").GetInt64());
            Assert.Equal(nextCursor, document.RootElement.GetProperty("next_cursor").GetString());
            Assert.False(document.RootElement.GetProperty("coverage").GetProperty("history_snapshot").GetBoolean());
        }
        Assert.Null(agent.Approval);
        Assert.False(mcp.RequiresApproval);
    }

    [Theory]
    [InlineData("{\"state\":\"Active\"}")]
    [InlineData("{\"state\":\" active \"}")]
    [InlineData("{\"state\":\"unknown\"}")]
    [InlineData("{\"state\":null}")]
    [InlineData("{\"max_results\":0}")]
    [InlineData("{\"max_results\":101}")]
    [InlineData("{\"max_results\":\"20\"}")]
    [InlineData("{\"max_results\":1.0}")]
    [InlineData("{\"cursor\":17}")]
    [InlineData("{\"cursor\":\"\"}")]
    [InlineData("{\"cursor\":null}")]
    [InlineData("{\"query\":\"latest incident\"}")]
    public Task BothSurfacesRejectInvalidArgumentsBeforeTheQueryProviderRuns(string json) =>
        AssertInvalidArgumentsRejectedAsync(json);

    [Fact]
    public Task BothSurfacesRejectAnOversizedCursorBeforeTheQueryProviderRuns() =>
        AssertInvalidArgumentsRejectedAsync(JsonSerializer.Serialize(new { cursor = new string('c', 4097) }));

    [Fact]
    public async Task BothSurfacesPreserveTheQueryProviderFailureClassification()
    {
        var dispatcher = CreateDispatcher((_, _, _, _) => Task.FromResult(new CopilotCapabilityResult
        {
            Success = false,
            FailureKind = CopilotToolFailureKind.Validation,
            ErrorMessage = "The cursor does not belong to these filters.",
        }));
        var input = new CopilotAgentToolInput { Arguments = new Dictionary<string, object?> { ["cursor"] = "other-filter-cursor" } };

        var agent = await new CopilotListFlowIncidentsTool(dispatcher).ExecuteAsync(Request(), input, CancellationToken.None);
        var mcp = await dispatcher.CallAsync("list_flow_incidents", CopilotFlowReadToolBase.ToJsonArguments(input), CancellationToken.None);

        Assert.False(agent.Success);
        Assert.False(mcp.Success);
        Assert.Equal(CopilotToolFailureKind.Validation, agent.FailureKind);
        Assert.Equal(agent.FailureKind, mcp.FailureKind);
        Assert.Equal(mcp.Text, agent.Content);
        Assert.Contains("cursor does not belong", agent.Content, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CallerCancellationReachesTheQueryProviderAndPropagates(bool agentSurface)
    {
        var entered = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var dispatcher = CreateDispatcher(async (_, _, _, token) =>
        {
            entered.TrySetResult(token);
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return new CopilotCapabilityResult { Success = true };
            }
            finally
            {
                stopped.TrySetResult();
            }
        });
        using var cancellation = new CancellationTokenSource();
        Task call = agentSurface
            ? new CopilotListFlowIncidentsTool(dispatcher).ExecuteAsync(Request(), CopilotAgentToolInput.Empty, cancellation.Token)
            : dispatcher.CallAsync("list_flow_incidents", null, cancellation.Token);
        var providerToken = await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call);
        await stopped.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(providerToken.IsCancellationRequested);
    }

    private static async Task AssertInvalidArgumentsRejectedAsync(string json)
    {
        var calls = 0;
        var dispatcher = CreateDispatcher((_, _, _, _) =>
        {
            ++calls;
            throw new InvalidOperationException("Invalid input reached the query provider.");
        });
        var tool = new CopilotListFlowIncidentsTool(dispatcher);
        var arguments = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
        var agentArguments = arguments.ToDictionary(pair => pair.Key, pair => (object?)pair.Value);
        Assert.False(tool.InputSchema.TryBind(agentArguments, out _, out _));

        var agent = await tool.ExecuteAsync(Request(), new CopilotAgentToolInput { Arguments = agentArguments }, CancellationToken.None);
        var mcp = await dispatcher.CallAsync("list_flow_incidents", arguments, CancellationToken.None);

        Assert.False(agent.Success);
        Assert.Equal(CopilotToolFailureKind.Validation, agent.FailureKind);
        Assert.False(mcp.Success);
        Assert.Equal(CopilotToolFailureKind.Validation, mcp.FailureKind);
        Assert.Equal("invalid_arguments", mcp.ErrorCode);
        Assert.Equal(0, calls);
    }

    private static CopilotAgentRequest Request(CopilotAgentMode mode = CopilotAgentMode.Auto) => new()
    {
        Mode = mode,
        UserText = "Explain the current business context.",
        ConversationId = "flow-incident-query-test-conversation",
        TaskId = "flow-incident-query-test-task",
    };

    private static CopilotMcpToolDispatcher CreateDispatcher(Func<string, int, string?, CancellationToken, Task<CopilotCapabilityResult>> provider) =>
        new(new CopilotMcpToolEnvironment
        {
            WorkspaceSnapshotProvider = () => new CopilotMcpWorkspaceSnapshot(),
            FlowIncidentQueryProvider = provider,
        });
}
