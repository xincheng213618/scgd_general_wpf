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
