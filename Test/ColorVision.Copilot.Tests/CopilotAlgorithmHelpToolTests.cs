using ColorVision.Copilot.Mcp;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotAlgorithmHelpToolTests
{
    [Theory]
    [InlineData("algorithms.fov-local", null, null)]
    [InlineData("algorithms.grid-distortion", null, 256)]
    [InlineData("ui.image-fusion", "  opaque:cursor==  ", 12000)]
    public async Task BothSurfacesPreserveExactTopicCursorDefaultsAndWholeJson(string topic, string? cursor, int? maximum)
    {
        var calls = new List<(string, string?, int)>();
        var payload = JsonSerializer.Serialize(new
        {
            topic_id = topic, content_format = "markdown_source", content = "# 算法原理\n参数说明：保留 Markdown 源文。", has_more = true, next_cursor = "opaque next cursor",
        });
        var dispatcher = Dispatcher((actualTopic, actualCursor, count, _) =>
        {
            calls.Add((actualTopic, actualCursor, count));
            return Task.FromResult(new CopilotCapabilityResult
            {
                Success = true, Content = payload, Summary = "Must not prefix JSON.", PartialResultMessage = "More source remains.",
            });
        });
        var arguments = new Dictionary<string, object?> { ["topic_id"] = topic };
        if (cursor != null) arguments["cursor"] = cursor;
        if (maximum.HasValue) arguments["max_characters"] = maximum.Value;
        var tool = new CopilotReadAlgorithmHelpTool(dispatcher);
        Assert.True(tool.InputSchema.TryBind(arguments, out var input, out var error), error);
        var agent = await tool.ExecuteAsync(Request(), input, CancellationToken.None);
        var mcp = await dispatcher.CallAsync("read_algorithm_help", CopilotFlowReadToolBase.ToJsonArguments(input), CancellationToken.None);
        Assert.True(agent.Success, agent.ErrorMessage);
        Assert.True(mcp.Success, mcp.Text);
        Assert.Equal(new[] { (topic, cursor, maximum ?? 6000), (topic, cursor, maximum ?? 6000) }, calls);
        Assert.Equal(payload, agent.Content);
        Assert.Equal(payload, mcp.Text);
        Assert.Equal("More source remains.", agent.PartialResultMessage);
        Assert.Equal(agent.PartialResultMessage, mcp.PartialResultMessage);
        using var agentJson = JsonDocument.Parse(agent.Content);
        using var mcpJson = JsonDocument.Parse(mcp.Text);
        Assert.Equal(topic, agentJson.RootElement.GetProperty("topic_id").GetString());
        Assert.Equal("markdown_source", agentJson.RootElement.GetProperty("content_format").GetString());
        Assert.Equal("# 算法原理\n参数说明：保留 Markdown 源文。", agentJson.RootElement.GetProperty("content").GetString());
        Assert.True(agentJson.RootElement.GetProperty("has_more").GetBoolean());
        Assert.Equal("opaque next cursor", agentJson.RootElement.GetProperty("next_cursor").GetString());
        Assert.True(JsonElement.DeepEquals(agentJson.RootElement, mcpJson.RootElement));
        Assert.Null(agent.Approval);
    }

    private static CopilotAgentRequest Request(CopilotAgentMode mode = CopilotAgentMode.Diagnose) => new()
    {
        Mode = mode, UserText = "Read the installed help for this exact algorithm topic.", ConversationId = "algorithm-help-test", TaskId = "algorithm-help-task",
    };

    private static CopilotMcpToolDispatcher Dispatcher(Func<string, string?, int, CancellationToken, Task<CopilotCapabilityResult>> provider) =>
        new(new CopilotMcpToolEnvironment { WorkspaceSnapshotProvider = () => new CopilotMcpWorkspaceSnapshot(), AlgorithmHelpReader = provider });
}
