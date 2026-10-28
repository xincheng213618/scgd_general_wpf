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
