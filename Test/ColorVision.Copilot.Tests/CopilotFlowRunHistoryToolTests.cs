using ColorVision.Copilot.Mcp;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotFlowRunHistoryToolTests
{
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
