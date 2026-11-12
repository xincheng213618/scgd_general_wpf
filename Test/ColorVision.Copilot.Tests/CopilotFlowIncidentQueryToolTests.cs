using ColorVision.Copilot.Mcp;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotFlowIncidentQueryToolTests
{
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
