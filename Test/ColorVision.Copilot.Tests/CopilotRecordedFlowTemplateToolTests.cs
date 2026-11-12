using ColorVision.Copilot.Mcp;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotRecordedFlowTemplateToolTests
{
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

    private static CopilotAgentRequest Request(CopilotAgentMode mode = CopilotAgentMode.Diagnose) => new()
    {
        Mode = mode, UserText = "Inspect the recorded template for this incident's exact run.", ConversationId = "recorded-template-test", TaskId = "recorded-template-task",
    };
    private static CopilotMcpToolDispatcher Dispatcher(Func<int, string?, int, CancellationToken, Task<CopilotCapabilityResult>> provider) =>
        new(new CopilotMcpToolEnvironment { WorkspaceSnapshotProvider = () => new CopilotMcpWorkspaceSnapshot(), RecordedFlowTemplateInspector = provider });
}
