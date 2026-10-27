using ColorVision.Copilot.Mcp;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotFlowGraphPaginationToolTests
{
    private const string Revision = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string FirstNode = "00000001-0000-0000-0000-000000000000";
    private const string SecondNode = "00000002-0000-0000-0000-000000000000";
    private const string ThirdNode = "00000003-0000-0000-0000-000000000000";

    [Fact]
    public void BothSurfacesPublishTheSameTwoStreamSchemaWithoutReadingTheEditor()
    {
        var definition = CopilotSharedCapabilityCatalog.FlowGraph;
        var dispatcher = Dispatcher(_ => throw new InvalidOperationException("Discovery must not inspect the editor."));
        var tool = new CopilotInspectFlowGraphTool(dispatcher);
        var descriptor = Assert.Single(dispatcher.ListTools(), item => item.Name == "get_flow_graph");
        Assert.Same(definition.AgentInputSchema, definition.McpInputSchema);
        Assert.True(JsonElement.DeepEquals(tool.InputSchema.JsonSchema, Assert.IsType<JsonElement>(descriptor.InputSchema)));
        Assert.Equal(CopilotSharedCapabilityExecutionRoute.ApplicationCapabilityRuntime, definition.ExecutionRoute);
        Assert.Equal(CopilotToolAccess.ReadOnly, tool.Capability.Access);
        Assert.Equal(CopilotToolConcurrencyMode.SharedRead, tool.Capability.EffectiveConcurrencyMode);
        var properties = tool.InputSchema.JsonSchema.GetProperty("properties");
        Assert.Equal(1, properties.GetProperty("node_id").GetProperty("minLength").GetInt32());
        Assert.Equal(256, properties.GetProperty("node_id").GetProperty("maxLength").GetInt32());
        Assert.Equal(new[] { "nodes", "edges", "ports", "properties" }, properties.GetProperty("section").GetProperty("enum").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal("nodes", properties.GetProperty("section").GetProperty("default").GetString());
        foreach (var limit in new[] { "max_nodes", "max_edges", "max_items" })
        {
            Assert.Equal(1, properties.GetProperty(limit).GetProperty("minimum").GetInt32());
            Assert.Equal(200, properties.GetProperty(limit).GetProperty("maximum").GetInt32());
            Assert.Equal(80, properties.GetProperty(limit).GetProperty("default").GetInt32());
        }
        Assert.Equal(64, properties.GetProperty("expected_revision").GetProperty("minLength").GetInt32());
        Assert.Equal(64, properties.GetProperty("expected_revision").GetProperty("maxLength").GetInt32());
        Assert.Equal("^[0-9a-fA-F]{64}$", properties.GetProperty("expected_revision").GetProperty("pattern").GetString());
        Assert.Equal(32, properties.GetProperty("expected_source_instance_id").GetProperty("minLength").GetInt32());
        Assert.Equal(32, properties.GetProperty("expected_source_instance_id").GetProperty("maxLength").GetInt32());
        Assert.Equal("^[0-9a-fA-F]{32}$", properties.GetProperty("expected_source_instance_id").GetProperty("pattern").GetString());
        Assert.Equal(1, properties.GetProperty("cursor").GetProperty("minLength").GetInt32());
        Assert.Equal(4096, properties.GetProperty("cursor").GetProperty("maxLength").GetInt32());
    }

    [Fact]
    public async Task BothSurfacesUseTheSameDefaultArgumentsAndReturnWholeJson()
    {
        var snapshot = Snapshot();
        Func<CancellationToken, Task<CopilotFlowContextSnapshot?>> provider = _ => Task.FromResult<CopilotFlowContextSnapshot?>(snapshot);
        var expected = await new CopilotFlowGraphInspectionService(provider).InspectAsync(null, false, 80, "nodes", 80, null, null, CancellationToken.None);
        var dispatcher = Dispatcher(provider);
        var agent = await new CopilotInspectFlowGraphTool(dispatcher).ExecuteAsync(Request(), CopilotAgentToolInput.Empty, CancellationToken.None);
        var mcp = await dispatcher.CallAsync("get_flow_graph", null, CancellationToken.None);
        Assert.True(expected.Success, expected.ErrorMessage);
        Assert.True(agent.Success, agent.ErrorMessage);
        Assert.True(mcp.Success, mcp.Text);
        Assert.Equal(expected.Content, agent.Content);
        Assert.Equal(expected.Content, mcp.Text);
        using var agentJson = JsonDocument.Parse(agent.Content);
        using var mcpJson = JsonDocument.Parse(mcp.Text);
        Assert.Equal("colorvision.flow-graph.v1", agentJson.RootElement.GetProperty("format").GetString());
        Assert.Equal("nodes", agentJson.RootElement.GetProperty("section").GetString());
        Assert.True(JsonElement.DeepEquals(agentJson.RootElement, mcpJson.RootElement));
    }

    [Theory]
    [InlineData("nodes", null)]
    [InlineData("edges", null)]
    [InlineData("edges", ThirdNode)]
    [InlineData("ports", null)]
    [InlineData("properties", null)]
    public async Task BothSurfacesContinueTheChosenStreamAndPreserveWholeJson(string section, string? nodeId)
    {
        var snapshot = Snapshot();
        Func<CancellationToken, Task<CopilotFlowContextSnapshot?>> provider = _ => Task.FromResult<CopilotFlowContextSnapshot?>(snapshot);
        var includeProperties = section != "ports";
        var first = await new CopilotFlowGraphInspectionService(provider).InspectAsync(nodeId, includeProperties, 1, section, 1, Revision, null, CancellationToken.None, new string('a', 32), 1);
        Assert.True(first.Success, first.ErrorMessage);
        using var firstJson = JsonDocument.Parse(first.Content);
        Assert.True(firstJson.RootElement.GetProperty("has_more").GetBoolean());
        var cursor = firstJson.RootElement.GetProperty("next_cursor").GetString()!;
        var dispatcher = Dispatcher(provider);
        var arguments = new Dictionary<string, object?>
        {
            ["section"] = section, ["include_properties"] = includeProperties, ["max_nodes"] = 200, ["max_edges"] = 200, ["max_items"] = 200,
            ["expected_revision"] = Revision, ["cursor"] = cursor,
        };
        if (nodeId != null) arguments["node_id"] = nodeId;
        var input = new CopilotAgentToolInput { Arguments = arguments };
        var agent = await new CopilotInspectFlowGraphTool(dispatcher).ExecuteAsync(Request(), input, CancellationToken.None);
        var mcp = await dispatcher.CallAsync("get_flow_graph", CopilotFlowReadToolBase.ToJsonArguments(input), CancellationToken.None);
        Assert.True(agent.Success, agent.ErrorMessage);
        Assert.True(mcp.Success, mcp.Text);
        Assert.Equal(agent.Content, mcp.Text);
        using var agentJson = JsonDocument.Parse(agent.Content);
        using var mcpJson = JsonDocument.Parse(mcp.Text);
        var body = agentJson.RootElement;
        Assert.Equal(section, body.GetProperty("section").GetString());
        Assert.False(body.GetProperty("has_more").GetBoolean());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("next_cursor").ValueKind);
        if (section == "edges")
        {
            Assert.Empty(body.GetProperty("nodes").EnumerateArray());
            Assert.Single(body.GetProperty("edges").EnumerateArray());
        }
        else if (section == "nodes") Assert.Equal(2, body.GetProperty("nodes").GetArrayLength());
        else
        {
            Assert.Empty(body.GetProperty("nodes").EnumerateArray());
            Assert.Empty(body.GetProperty("edges").EnumerateArray());
            Assert.Equal(section == "ports" ? 5 : 2, body.GetProperty(section).GetArrayLength());
            if (section == "ports")
            {
                Assert.Equal(6, body.GetProperty("totalPortCount").GetInt32());
                Assert.Equal(5, body.GetProperty("returnedPortCount").GetInt32());
                Assert.Equal(JsonValueKind.Null, body.GetProperty("totalPropertyCount").ValueKind);
                Assert.All(body.GetProperty("ports").EnumerateArray(), port =>
                {
                    Assert.False(string.IsNullOrEmpty(port.GetProperty("nodeInstanceId").GetString()));
                    Assert.Contains(port.GetProperty("direction").GetString(), new[] { "input", "output" });
                    Assert.Contains(port.GetProperty("portId").GetString(), new[] { "in:0", "out:0" });
                    Assert.Equal("System.Object", port.GetProperty("dataType").GetString());
                });
            }
            else
            {
                Assert.Equal(3, body.GetProperty("totalPropertyCount").GetInt32());
                Assert.Equal(2, body.GetProperty("returnedPropertyCount").GetInt32());
                Assert.All(body.GetProperty("properties").EnumerateArray(), property =>
                {
                    Assert.Equal(0, property.GetProperty("parameterIndex").GetInt32());
                    Assert.Equal("Threshold", property.GetProperty("name").GetString());
                    Assert.Equal("10", property.GetProperty("value").GetString());
                    Assert.False(property.GetProperty("textTruncated").GetBoolean());
                });
            }
        }
        Assert.True(JsonElement.DeepEquals(body, mcpJson.RootElement));

        var paddedArguments = input.Arguments.ToDictionary(item => item.Key, item => item.Value);
        paddedArguments["cursor"] = " " + cursor + " ";
        var paddedInput = new CopilotAgentToolInput { Arguments = paddedArguments };
        var paddedAgent = await new CopilotInspectFlowGraphTool(dispatcher).ExecuteAsync(Request(), paddedInput, CancellationToken.None);
        var paddedMcp = await dispatcher.CallAsync("get_flow_graph", CopilotFlowReadToolBase.ToJsonArguments(paddedInput), CancellationToken.None);
        Assert.Equal(CopilotToolFailureKind.Validation, paddedAgent.FailureKind);
        Assert.Equal(CopilotToolFailureKind.Validation, paddedMcp.FailureKind);
    }

    [Theory]
    [InlineData("{\"section\":\"all\"}")]
    [InlineData("{\"section\":\"Nodes\"}")]
    [InlineData("{\"max_nodes\":0}")]
    [InlineData("{\"max_nodes\":201}")]
    [InlineData("{\"max_edges\":0}")]
    [InlineData("{\"max_edges\":201}")]
    [InlineData("{\"max_edges\":80.0}")]
    [InlineData("{\"max_items\":0}")]
    [InlineData("{\"max_items\":201}")]
    [InlineData("{\"max_items\":80.0}")]
    [InlineData("{\"max_items\":\"80\"}")]
    [InlineData("{\"section\":\"properties\"}")]
    [InlineData("{\"section\":\"properties\",\"include_properties\":false}")]
    [InlineData("{\"cursor\":\"\"}")]
    [InlineData("{\"cursor\":null}")]
    [InlineData("{\"cursor\":42}")]
    [InlineData("{\"expected_revision\":\"\"}")]
    [InlineData("{\"expected_revision\":null}")]
    [InlineData("{\"node_id\":\"\"}")]
    [InlineData("{\"expected_source_instance_id\":\"short\"}")]
    [InlineData("{\"expected_source_instance_id\":\"zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz\"}")]
    [MemberData(nameof(OversizedOrMalformedArguments))]
    public async Task InvalidArgumentsCannotReadTheSnapshotProvider(string json)
    {
        var calls = 0;
        var dispatcher = Dispatcher(_ => { calls++; throw new InvalidOperationException("Invalid input reached the editor."); });
        var args = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
        var input = new CopilotAgentToolInput { Arguments = args.ToDictionary(item => item.Key, item => (object?)item.Value) };
        var agent = await new CopilotInspectFlowGraphTool(dispatcher).ExecuteAsync(Request(), input, CancellationToken.None);
        var mcp = await dispatcher.CallAsync("get_flow_graph", args, CancellationToken.None);
        Assert.Equal(CopilotToolFailureKind.Validation, agent.FailureKind);
        Assert.Equal(agent.FailureKind, mcp.FailureKind);
        Assert.Equal(0, calls);
    }

    public static IEnumerable<object[]> OversizedOrMalformedArguments =>
    [
        [JsonSerializer.Serialize(new { cursor = new string('x', 4097) })],
        [JsonSerializer.Serialize(new { expected_revision = new string('a', 63) })],
        [JsonSerializer.Serialize(new { expected_revision = Revision + "\n" })],
        [JsonSerializer.Serialize(new { expected_revision = new string('z', 64) })],
        [JsonSerializer.Serialize(new { node_id = new string('x', 257) })],
    ];

    [Theory]
    [InlineData(false, CopilotToolFailureKind.NotFound)]
    [InlineData(true, CopilotToolFailureKind.Validation)]
    public async Task BothSurfacesPreserveMissingEditorAndStaleRevisionFailures(bool stale, CopilotToolFailureKind kind)
    {
        var dispatcher = Dispatcher(_ => Task.FromResult<CopilotFlowContextSnapshot?>(stale ? Snapshot() : null));
        var input = new CopilotAgentToolInput { Arguments = new Dictionary<string, object?> { ["expected_revision"] = new string('b', 64) } };
        var agent = await new CopilotInspectFlowGraphTool(dispatcher).ExecuteAsync(Request(), input, CancellationToken.None);
        var mcp = await dispatcher.CallAsync("get_flow_graph", CopilotFlowReadToolBase.ToJsonArguments(input), CancellationToken.None);
        Assert.False(agent.Success);
        Assert.False(mcp.Success);
        Assert.Equal(kind, agent.FailureKind);
        Assert.Equal(kind, mcp.FailureKind);
        Assert.Equal(agent.Content, mcp.Text);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CallerCancellationReachesTheSnapshotProvider(bool agentSurface)
    {
        var entered = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var dispatcher = Dispatcher(async token =>
        {
            entered.TrySetResult(token);
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return null;
        });
        using var cancellation = new CancellationTokenSource();
        Task pending = agentSurface
            ? new CopilotInspectFlowGraphTool(dispatcher).ExecuteAsync(Request(), CopilotAgentToolInput.Empty, cancellation.Token)
            : dispatcher.CallAsync("get_flow_graph", null, cancellation.Token);
        var token = await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.True(token.IsCancellationRequested);
    }

    [Fact]
    public async Task BothSurfacesRejectSwitchingHostsBetweenNodeAndEdgeStreamsEvenWhenTheRevisionMatches()
    {
        var snapshot = Snapshot();
        var dispatcher = Dispatcher(_ => Task.FromResult<CopilotFlowContextSnapshot?>(snapshot));
        var first = await dispatcher.CallAsync("get_flow_graph", null, CancellationToken.None);
        Assert.True(first.Success, first.Text);
        using var firstJson = JsonDocument.Parse(first.Text);
        var source = firstJson.RootElement.GetProperty("sourceInstanceId").GetString();
        snapshot = Snapshot(new string('b', 32));
        var input = new CopilotAgentToolInput { Arguments = new Dictionary<string, object?>
        {
            ["section"] = "edges", ["expected_revision"] = Revision, ["expected_source_instance_id"] = source,
        } };
        var agent = await new CopilotInspectFlowGraphTool(dispatcher).ExecuteAsync(Request(), input, CancellationToken.None);
        var mcp = await dispatcher.CallAsync("get_flow_graph", CopilotFlowReadToolBase.ToJsonArguments(input), CancellationToken.None);
        Assert.False(agent.Success);
        Assert.False(mcp.Success);
        Assert.Equal(CopilotToolFailureKind.Validation, agent.FailureKind);
        Assert.Equal(CopilotToolFailureKind.Validation, mcp.FailureKind);
        Assert.Equal(agent.Content, mcp.Text);
    }

    private static CopilotFlowContextSnapshot Snapshot(string? sourceInstanceId = null) => new()
    {
        SourceId = "flow-graph-tool-test", SourceInstanceId = sourceInstanceId ?? new string('a', 32), Revision = Revision, FlowName = "Test graph", TemplateId = "17", Status = "Ready",
        Nodes = new[] { ThirdNode, FirstNode, SecondNode }.Select(id => new CopilotFlowNodeContextSnapshot
        {
            InstanceId = id, NodeId = id, Title = id, TypeKey = "Test|Node", Parameters = [new() { Name = "Threshold", Value = "10" }],
            InputPorts = [new() { PortId = "in:0", Name = "IN", DataType = "System.Object" }],
            OutputPorts = [new() { PortId = "out:0", Name = "OUT", DataType = "System.Object" }],
        }).ToArray(),
        Edges =
        [
            new() { SourceNodeId = SecondNode, SourcePortId = "out:0", TargetNodeId = ThirdNode, TargetPortId = "in:0" },
            new() { SourceNodeId = FirstNode, SourcePortId = "out:0", TargetNodeId = ThirdNode, TargetPortId = "in:0" },
        ],
    };

    private static CopilotAgentRequest Request() => new() { Mode = CopilotAgentMode.Diagnose, UserText = "Inspect this flow graph.", ConversationId = "graph-pagination-test", TaskId = "graph-pagination-task" };
    private static CopilotMcpToolDispatcher Dispatcher(Func<CancellationToken, Task<CopilotFlowContextSnapshot?>> provider) =>
        new(new CopilotMcpToolEnvironment { WorkspaceSnapshotProvider = () => new CopilotMcpWorkspaceSnapshot(), FlowSnapshotProvider = provider });
}
