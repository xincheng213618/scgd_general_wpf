using ColorVision.Copilot.Mcp;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotFlowRunAttemptsToolTests
{
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
    [InlineData("{\"run_record_id\":2147483648}")]
    [InlineData("{\"run_record_id\":\"1\"}")]
    [InlineData("{\"run_record_id\":1,\"max_attempts\":0}")]
    [InlineData("{\"run_record_id\":1,\"max_attempts\":101}")]
    [InlineData("{\"run_record_id\":1,\"node_id\":\"\"}")]
    [InlineData("{\"run_record_id\":1,\"node_id\":\" \\t\"}")]
    [InlineData("{\"run_record_id\":1,\"cursor\":\"\"}")]
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

    private static CopilotAgentRequest Request(CopilotAgentMode mode = CopilotAgentMode.Diagnose) => new()
    {
        Mode = mode, UserText = "Read the recorded attempts for this exact run.", ConversationId = "flow-run-attempts-test", TaskId = "flow-run-attempts-task",
    };

    private static CopilotMcpToolDispatcher Dispatcher(Func<int, string?, int, string?, CancellationToken, Task<CopilotCapabilityResult>> provider) =>
        new(new CopilotMcpToolEnvironment { WorkspaceSnapshotProvider = () => new CopilotMcpWorkspaceSnapshot(), FlowRunAttemptsInspector = provider });
}
