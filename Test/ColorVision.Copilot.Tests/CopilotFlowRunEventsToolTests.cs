using ColorVision.Copilot.Mcp;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotFlowRunEventsToolTests
{
    [Theory]
    [InlineData(1, null, null)]
    [InlineData(17, 1, null)]
    [InlineData(int.MaxValue, 100, "  opaque:cursor==  ")]
    public async Task BothSurfacesPreserveExactRunDefaultsCursorAndWholeJson(int runId, int? maximum, string? cursor)
    {
        var calls = new List<(int, int, string?)>();
        var eventRows = new[] { new { id = 30, sequence = 7 }, new { id = 31, sequence = 8 } }.Take(maximum ?? 40).ToArray();
        var payload = JsonSerializer.Serialize(new
        {
            run_record_id = runId, events = eventRows,
            has_more = true, next_cursor = "opaque earlier cursor", coverage = new { window = cursor == null ? "latest" : "earlier" },
        });
        var dispatcher = Dispatcher((actualRun, count, continuation, _) =>
        {
            calls.Add((actualRun, count, continuation));
            return Task.FromResult(new CopilotCapabilityResult
            {
                Success = true, Content = payload, Summary = "Must not prefix JSON.", PartialResultMessage = "Earlier event windows remain.",
            });
        });
        var arguments = new Dictionary<string, object?> { ["run_record_id"] = runId };
        if (maximum.HasValue) arguments["max_events"] = maximum.Value;
        if (cursor != null) arguments["cursor"] = cursor;
        var tool = new CopilotInspectFlowRunEventsTool(dispatcher);
        Assert.True(tool.InputSchema.TryBind(arguments, out var input, out var error), error);
        var agent = await tool.ExecuteAsync(Request(), input, CancellationToken.None);
        var mcp = await dispatcher.CallAsync("inspect_flow_run_events", CopilotFlowReadToolBase.ToJsonArguments(input), CancellationToken.None);
        Assert.True(agent.Success, agent.ErrorMessage);
        Assert.True(mcp.Success, mcp.Text);
        Assert.Equal(new[] { (runId, maximum ?? 40, cursor), (runId, maximum ?? 40, cursor) }, calls);
        Assert.Equal(payload, agent.Content);
        Assert.Equal(payload, mcp.Text);
        using var agentJson = JsonDocument.Parse(agent.Content);
        using var mcpJson = JsonDocument.Parse(mcp.Text);
        Assert.Equal(runId, agentJson.RootElement.GetProperty("run_record_id").GetInt32());
        Assert.Equal(eventRows.Select(item => item.sequence), agentJson.RootElement.GetProperty("events").EnumerateArray().Select(item => item.GetProperty("sequence").GetInt32()));
        Assert.True(agentJson.RootElement.GetProperty("has_more").GetBoolean());
        Assert.Equal("opaque earlier cursor", agentJson.RootElement.GetProperty("next_cursor").GetString());
        Assert.True(JsonElement.DeepEquals(agentJson.RootElement, mcpJson.RootElement));
        Assert.Null(agent.Approval);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"run_record_id\":0}")]
    [InlineData("{\"run_record_id\":2147483648}")]
    [InlineData("{\"run_record_id\":\"1\"}")]
    [InlineData("{\"run_record_id\":1,\"max_events\":0}")]
    [InlineData("{\"run_record_id\":1,\"max_events\":101}")]
    [InlineData("{\"run_record_id\":1,\"cursor\":\"\"}")]
    [InlineData("{\"run_record_id\":1,\"incident_id\":2}")]
    [MemberData(nameof(OversizedCursorArguments))]
    public async Task InvalidArgumentsDoNotReachEitherProviderRoute(string json)
    {
        var calls = 0;
        var dispatcher = Dispatcher((_, _, _, _) => { calls++; throw new InvalidOperationException("Invalid arguments reached run event inspection."); });
        var arguments = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
        var values = arguments.ToDictionary(item => item.Key, item => (object?)item.Value);
        var tool = new CopilotInspectFlowRunEventsTool(dispatcher);
        Assert.False(tool.InputSchema.TryBind(values, out _, out _));
        var agent = await tool.ExecuteAsync(Request(), new CopilotAgentToolInput { Arguments = values }, CancellationToken.None);
        var mcp = await dispatcher.CallAsync("inspect_flow_run_events", arguments, CancellationToken.None);
        Assert.False(agent.Success);
        Assert.False(mcp.Success);
        Assert.Equal(CopilotToolFailureKind.Validation, agent.FailureKind);
        Assert.Equal(agent.FailureKind, mcp.FailureKind);
        Assert.Equal(0, calls);
    }

    public static IEnumerable<object[]> OversizedCursorArguments =>
    [
        [JsonSerializer.Serialize(new { run_record_id = 1, cursor = new string('x', 4097) })],
    ];

    private static CopilotAgentRequest Request(CopilotAgentMode mode = CopilotAgentMode.Diagnose) => new()
    {
        Mode = mode, UserText = "Read the recorded events for this exact run.", ConversationId = "flow-run-events-test", TaskId = "flow-run-events-task",
    };

    private static CopilotMcpToolDispatcher Dispatcher(Func<int, int, string?, CancellationToken, Task<CopilotCapabilityResult>> provider) =>
        new(new CopilotMcpToolEnvironment { WorkspaceSnapshotProvider = () => new CopilotMcpWorkspaceSnapshot(), FlowRunEventsInspector = provider });
}
