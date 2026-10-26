using ColorVision.Copilot.Mcp;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotRecordedFlowTemplateToolTests
{
    [Fact]
    public void DiscoveryPublishesOneSharedReadOnlyContractWithoutOpeningTheJournal()
    {
        var definition = CopilotSharedCapabilityCatalog.RecordedFlowTemplate;
        var dispatcher = Dispatcher((_, _, _, _) => throw new InvalidOperationException("Discovery performed I/O."));
        var tool = Assert.IsType<CopilotInspectRecordedFlowTemplateTool>(definition.CreateAgentTool(dispatcher));
        var descriptor = Assert.Single(dispatcher.ListTools(), item => item.Name == "inspect_recorded_flow_template");
        Assert.Equal("flow.recorded-template", definition.Id);
        Assert.Equal("InspectRecordedFlowTemplate", tool.Name);
        Assert.Same(definition.AgentInputSchema, definition.McpInputSchema);
        Assert.True(JsonElement.DeepEquals(tool.InputSchema.JsonSchema, Assert.IsType<JsonElement>(descriptor.InputSchema)));
        Assert.Equal(CopilotSharedCapabilityExecutionRoute.ApplicationCapabilityRuntime, definition.ExecutionRoute);
        Assert.Equal(CopilotToolAccess.ReadOnly, tool.Capability.Access);
        Assert.Equal(CopilotToolIdempotency.Idempotent, tool.Capability.Idempotency);
        Assert.Equal(CopilotToolConcurrencyMode.SharedRead, tool.Capability.EffectiveConcurrencyMode);
        Assert.Equal(CopilotToolApprovalMode.Never, tool.Capability.ApprovalMode);
        Assert.Equal(CopilotToolAuditArgumentMode.NamesOnly, tool.Capability.AuditArgumentMode);
        Assert.Equal(TimeSpan.FromSeconds(15), tool.Capability.EffectiveExecutionTimeout);
        var properties = tool.InputSchema.JsonSchema.GetProperty("properties");
        Assert.Equal(int.MaxValue, properties.GetProperty("run_record_id").GetProperty("maximum").GetInt32());
        Assert.Equal(80, properties.GetProperty("max_nodes").GetProperty("default").GetInt32());
        Assert.Equal(200, properties.GetProperty("max_nodes").GetProperty("maximum").GetInt32());
        var registry = new CopilotToolRegistry([tool]);
        Assert.Single(registry.FindTools(Request()));
        Assert.Empty(registry.FindTools(Request(CopilotAgentMode.Chat)));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("8a29eb3d-46f4-4c95-b415-fd5b66d101dd", 1)]
    public async Task AgentAndMcpPreserveExactRunFocusDefaultsAndWholeJson(string? nodeId, int? maximum)
    {
        var calls = new List<(int, string?, int)>();
        const string payload = "{\"run\":{\"run_record_id\":2147483647},\"coverage\":{\"connections_complete\":false}}";
        var dispatcher = Dispatcher((runId, node, count, _) =>
        {
            calls.Add((runId, node, count));
            return Task.FromResult(new CopilotCapabilityResult { Success = true, Content = payload, Summary = "Not a JSON prefix." });
        });
        var arguments = new Dictionary<string, object?> { ["run_record_id"] = int.MaxValue };
        if (nodeId != null) arguments["node_id"] = nodeId;
        if (maximum.HasValue) arguments["max_nodes"] = maximum.Value;
        var tool = new CopilotInspectRecordedFlowTemplateTool(dispatcher);
        Assert.True(tool.InputSchema.TryBind(arguments, out var input, out var error), error);
        var agent = await tool.ExecuteAsync(Request(), input, CancellationToken.None);
        var mcp = await dispatcher.CallAsync("inspect_recorded_flow_template", CopilotFlowReadToolBase.ToJsonArguments(input), CancellationToken.None);
        Assert.True(agent.Success, agent.ErrorMessage);
        Assert.True(mcp.Success, mcp.Text);
        Assert.Equal(new[] { (int.MaxValue, nodeId, maximum ?? 80), (int.MaxValue, nodeId, maximum ?? 80) }, calls);
        Assert.Equal(payload, agent.Content);
        Assert.Equal(payload, mcp.Text);
        using var document = JsonDocument.Parse(agent.Content);
        Assert.False(document.RootElement.GetProperty("coverage").GetProperty("connections_complete").GetBoolean());
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"run_record_id\":0}")]
    [InlineData("{\"run_record_id\":2147483648}")]
    [InlineData("{\"run_record_id\":1.0}")]
    [InlineData("{\"run_record_id\":\"1\"}")]
    [InlineData("{\"run_record_id\":1,\"max_nodes\":201}")]
    [InlineData("{\"run_record_id\":1,\"max_nodes\":0}")]
    [InlineData("{\"run_record_id\":1,\"node_id\":null}")]
    [InlineData("{\"run_record_id\":1,\"node_id\":\"camera\"}")]
    [InlineData("{\"run_record_id\":1,\"node_id\":\"zzzzzzzz-zzzz-zzzz-zzzz-zzzzzzzzzzzz\"}")]
    [InlineData("{\"run_record_id\":1,\"snapshot_id\":99}")]
    public async Task BothSurfacesRejectInvalidInputBeforeTheProvider(string json)
    {
        var calls = 0;
        var dispatcher = Dispatcher((_, _, _, _) => { calls++; throw new InvalidOperationException("Invalid input reached the provider."); });
        var arguments = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
        var input = new CopilotAgentToolInput { Arguments = arguments.ToDictionary(pair => pair.Key, pair => (object?)pair.Value) };
        var agent = await new CopilotInspectRecordedFlowTemplateTool(dispatcher).ExecuteAsync(Request(), input, CancellationToken.None);
        var mcp = await dispatcher.CallAsync("inspect_recorded_flow_template", arguments, CancellationToken.None);
        Assert.False(agent.Success);
        Assert.False(mcp.Success);
        Assert.Equal(CopilotToolFailureKind.Validation, agent.FailureKind);
        Assert.Equal(agent.FailureKind, mcp.FailureKind);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task ClassifiedSourceFailureIsPreservedOnBothSurfaces()
    {
        var dispatcher = Dispatcher((_, _, _, _) => Task.FromResult(new CopilotCapabilityResult
        {
            Success = false, FailureKind = CopilotToolFailureKind.NotFound, ErrorMessage = "The exact snapshot reference is unavailable.",
        }));
        var input = new CopilotAgentToolInput { Arguments = new Dictionary<string, object?> { ["run_record_id"] = 17 } };
        var agent = await new CopilotInspectRecordedFlowTemplateTool(dispatcher).ExecuteAsync(Request(), input, CancellationToken.None);
        var mcp = await dispatcher.CallAsync("inspect_recorded_flow_template", CopilotFlowReadToolBase.ToJsonArguments(input), CancellationToken.None);
        Assert.False(agent.Success);
        Assert.False(mcp.Success);
        Assert.Equal(CopilotToolFailureKind.NotFound, agent.FailureKind);
        Assert.Equal(agent.FailureKind, mcp.FailureKind);
        Assert.Equal(mcp.Text, agent.Content);
    }

    private static CopilotAgentRequest Request(CopilotAgentMode mode = CopilotAgentMode.Diagnose) => new()
    {
        Mode = mode, UserText = "Inspect the recorded template for this incident's exact run.", ConversationId = "recorded-template-test", TaskId = "recorded-template-task",
    };
    private static CopilotMcpToolDispatcher Dispatcher(Func<int, string?, int, CancellationToken, Task<CopilotCapabilityResult>> provider) =>
        new(new CopilotMcpToolEnvironment { WorkspaceSnapshotProvider = () => new CopilotMcpWorkspaceSnapshot(), RecordedFlowTemplateInspector = provider });
}
