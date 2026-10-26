using ColorVision.Copilot.Mcp;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotFlowRunHistoryToolTests
{
    [Fact]
    public void BothSurfacesPublishTheSameReadOnlyHistoryContract()
    {
        var definition = CopilotSharedCapabilityCatalog.FlowRunHistory;
        var dispatcher = CreateDispatcher((_, _, _, _) => throw new InvalidOperationException("Discovery must not read run history."));
        var tool = Assert.IsType<CopilotInspectFlowRunHistoryTool>(definition.CreateAgentTool(dispatcher));
        var descriptor = Assert.Single(dispatcher.ListTools(), item => item.Name == "inspect_flow_run_history");

        Assert.Equal("flow.run-history", definition.Id);
        Assert.Equal("InspectFlowRunHistory", tool.Name);
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
        Assert.Equal(1, properties.GetProperty("run_record_id").GetProperty("minimum").GetInt32());
        Assert.Equal(int.MaxValue, properties.GetProperty("run_record_id").GetProperty("maximum").GetInt32());
        Assert.Equal(1, properties.GetProperty("max_runs").GetProperty("minimum").GetInt32());
        Assert.Equal(50, properties.GetProperty("max_runs").GetProperty("maximum").GetInt32());
        Assert.Equal(10, properties.GetProperty("max_runs").GetProperty("default").GetInt32());
        Assert.Equal(1, properties.GetProperty("node_id").GetProperty("minLength").GetInt32());
        Assert.Equal(128, properties.GetProperty("node_id").GetProperty("maxLength").GetInt32());
        Assert.False(properties.GetProperty("node_id").TryGetProperty("pattern", out _));

        var registry = new CopilotToolRegistry([tool]);
        foreach (var mode in Enum.GetValues<CopilotAgentMode>().Where(mode => mode != CopilotAgentMode.Chat))
            Assert.Same(tool, Assert.Single(registry.FindTools(Request(mode))));
        Assert.Empty(registry.FindTools(Request(CopilotAgentMode.Chat)));
    }

    [Theory]
    [InlineData(1, null, null)]
    [InlineData(int.MaxValue, "  vendor:node/id  ", 50)]
    public async Task BothSurfacesPreserveExactIdsDefaultsAndWholeJsonBody(int runId, string? nodeId, int? maxRuns)
    {
        var calls = new List<(int RunId, string? NodeId, int Maximum)>();
        var providerResult = new CopilotCapabilityResult
        {
            Success = true,
            Summary = "Recorded flow run history inspection.",
            Content = JsonSerializer.Serialize(new
            {
                target = new { run_record_id = runId },
                node_id = nodeId,
                previous_runs = Array.Empty<object>(),
                coverage = new { full_history = false, actual_canvas_equivalence = "unknown" },
            }),
        };
        var dispatcher = CreateDispatcher((id, exactNodeId, maximum, _) =>
        {
            calls.Add((id, exactNodeId, maximum));
            return Task.FromResult(providerResult);
        });
        var tool = new CopilotInspectFlowRunHistoryTool(dispatcher);
        var arguments = new Dictionary<string, object?> { ["run_record_id"] = runId };
        if (nodeId != null)
            arguments["node_id"] = nodeId;
        if (maxRuns.HasValue)
            arguments["max_runs"] = maxRuns.Value;
        Assert.True(tool.InputSchema.TryBind(arguments, out var input, out var error), error);

        var agent = await tool.ExecuteAsync(Request(), input, CancellationToken.None);
        var mcp = await dispatcher.CallAsync("inspect_flow_run_history", CopilotFlowReadToolBase.ToJsonArguments(input), CancellationToken.None);

        Assert.True(agent.Success, agent.ErrorMessage);
        Assert.True(mcp.Success, mcp.Text);
        Assert.Equal(new[] { (runId, nodeId, maxRuns ?? 10), (runId, nodeId, maxRuns ?? 10) }, calls);
        Assert.Equal(providerResult.Content, agent.Content);
        Assert.Equal(agent.Content, mcp.Text);
        Assert.DoesNotContain(providerResult.Summary, agent.Content, StringComparison.Ordinal);
        foreach (var content in new[] { agent.Content, mcp.Text })
        {
            using var document = JsonDocument.Parse(content);
            Assert.Equal(runId, document.RootElement.GetProperty("target").GetProperty("run_record_id").GetInt32());
            Assert.Equal(nodeId, document.RootElement.GetProperty("node_id").GetString());
            Assert.False(document.RootElement.GetProperty("coverage").GetProperty("full_history").GetBoolean());
        }
        Assert.Null(agent.Approval);
        Assert.False(mcp.RequiresApproval);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"run_record_id\":0}")]
    [InlineData("{\"run_record_id\":2147483648}")]
    [InlineData("{\"run_record_id\":\"17\"}")]
    [InlineData("{\"run_record_id\":1.0}")]
    [InlineData("{\"run_record_id\":17,\"max_runs\":0}")]
    [InlineData("{\"run_record_id\":17,\"max_runs\":51}")]
    [InlineData("{\"run_record_id\":17,\"max_runs\":\"10\"}")]
    [InlineData("{\"run_record_id\":17,\"max_runs\":1.0}")]
    [InlineData("{\"run_record_id\":17,\"node_id\":\"\"}")]
    [InlineData("{\"run_record_id\":17,\"node_id\":null}")]
    [InlineData("{\"run_record_id\":17,\"node_id\":17}")]
    [InlineData("{\"run_record_id\":17,\"cursor\":\"invented\"}")]
    public Task BothSurfacesRejectInvalidArgumentsBeforeTheInspectorRuns(string json) =>
        AssertInvalidArgumentsRejectedAsync(json);

    [Fact]
    public Task BothSurfacesRejectAnOversizedExactNodeIdBeforeTheInspectorRuns() =>
        AssertInvalidArgumentsRejectedAsync(JsonSerializer.Serialize(new { run_record_id = 17, node_id = new string('n', 129) }));

    [Fact]
    public async Task BothSurfacesPreserveTheInspectorFailureClassification()
    {
        var dispatcher = CreateDispatcher((_, _, _, _) => Task.FromResult(new CopilotCapabilityResult
        {
            Success = false,
            FailureKind = CopilotToolFailureKind.NotFound,
            ErrorMessage = "The exact recorded run is absent.",
        }));
        var input = new CopilotAgentToolInput { Arguments = new Dictionary<string, object?> { ["run_record_id"] = 17 } };

        var agent = await new CopilotInspectFlowRunHistoryTool(dispatcher).ExecuteAsync(Request(), input, CancellationToken.None);
        var mcp = await dispatcher.CallAsync("inspect_flow_run_history", CopilotFlowReadToolBase.ToJsonArguments(input), CancellationToken.None);

        Assert.False(agent.Success);
        Assert.False(mcp.Success);
        Assert.Equal(CopilotToolFailureKind.NotFound, agent.FailureKind);
        Assert.Equal(agent.FailureKind, mcp.FailureKind);
        Assert.Equal(mcp.Text, agent.Content);
        Assert.Contains("exact recorded run is absent", agent.Content, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CallerCancellationReachesTheInspectorAndPropagates(bool agentSurface)
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
        var input = new CopilotAgentToolInput { Arguments = new Dictionary<string, object?> { ["run_record_id"] = 17 } };
        using var cancellation = new CancellationTokenSource();
        Task call = agentSurface
            ? new CopilotInspectFlowRunHistoryTool(dispatcher).ExecuteAsync(Request(), input, cancellation.Token)
            : dispatcher.CallAsync("inspect_flow_run_history", CopilotFlowReadToolBase.ToJsonArguments(input), cancellation.Token);
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
            throw new InvalidOperationException("Invalid input reached the history inspector.");
        });
        var tool = new CopilotInspectFlowRunHistoryTool(dispatcher);
        var arguments = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
        var agentArguments = arguments.ToDictionary(pair => pair.Key, pair => (object?)pair.Value);
        Assert.False(tool.InputSchema.TryBind(agentArguments, out _, out _));

        var agent = await tool.ExecuteAsync(Request(), new CopilotAgentToolInput { Arguments = agentArguments }, CancellationToken.None);
        var mcp = await dispatcher.CallAsync("inspect_flow_run_history", arguments, CancellationToken.None);

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
        ConversationId = "flow-run-history-test-conversation",
        TaskId = "flow-run-history-test-task",
    };

    private static CopilotMcpToolDispatcher CreateDispatcher(Func<int, string?, int, CancellationToken, Task<CopilotCapabilityResult>> inspector) =>
        new(new CopilotMcpToolEnvironment
        {
            WorkspaceSnapshotProvider = () => new CopilotMcpWorkspaceSnapshot(),
            FlowRunHistoryInspector = inspector,
        });
}
