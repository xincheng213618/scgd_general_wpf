using ColorVision.Copilot.Mcp;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotFlowGraphValidationToolTests
{
    private const string Revision = "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task BothSurfacesReturnWholeJsonAndAnInvalidGraphIsStillAToolSuccess(bool valid, bool executionAndReadOnly)
    {
        var revision = valid ? Revision : Revision.ToUpperInvariant();
        var calls = new List<string>();
        var invalidNode = Guid.Parse("12345678-1234-1234-1234-123456789abc");
        var dispatcher = Dispatcher((expected, _) =>
        {
            calls.Add(expected);
            return Task.FromResult<CopilotFlowValidationSnapshot?>(new(expected, true, valid,
                valid ? string.Empty : "No control path from the start node to an end node.", valid ? null : invalidNode,
                3, 2, executionAndReadOnly, executionAndReadOnly));
        });
        var tool = new CopilotValidateFlowGraphTool(dispatcher);
        Assert.True(tool.InputSchema.TryBind(new Dictionary<string, object?> { ["expected_revision"] = revision }, out var input, out var error), error);
        var agent = await tool.ExecuteAsync(Request(), input, CancellationToken.None);
        var mcp = await dispatcher.CallAsync("validate_flow_graph", CopilotFlowReadToolBase.ToJsonArguments(input), CancellationToken.None);
        Assert.True(agent.Success, agent.ErrorMessage);
        Assert.True(mcp.Success, mcp.Text);
        Assert.Equal(new[] { revision, revision }, calls);
        Assert.Equal(agent.Content, mcp.Text);
        using var agentJson = JsonDocument.Parse(agent.Content);
        using var mcpJson = JsonDocument.Parse(mcp.Text);
        var body = agentJson.RootElement;
        Assert.Equal("current_flow_editor", body.GetProperty("source").GetString());
        Assert.Equal("host_save_structure", body.GetProperty("validation_kind").GetString());
        Assert.Equal(revision.ToLowerInvariant(), body.GetProperty("revision").GetString());
        Assert.Equal(valid, body.GetProperty("valid").GetBoolean());
        Assert.Equal(3, body.GetProperty("node_count").GetInt32());
        Assert.Equal(2, body.GetProperty("connection_count").GetInt32());
        Assert.Equal(executionAndReadOnly, body.GetProperty("is_execution_active").GetBoolean());
        Assert.Equal(executionAndReadOnly, body.GetProperty("is_editor_read_only").GetBoolean());
        if (!valid)
        {
            Assert.Contains("No control path", body.GetProperty("diagnostic").GetString(), StringComparison.Ordinal);
            Assert.Equal(invalidNode, body.GetProperty("invalid_node_id").GetGuid());
        }
        Assert.True(JsonElement.DeepEquals(body, mcpJson.RootElement));
        Assert.Null(agent.Approval);
    }

    private static CopilotAgentRequest Request(CopilotAgentMode mode = CopilotAgentMode.Diagnose) => new()
    {
        Mode = mode, UserText = "Validate the current flow graph at this exact revision.", ConversationId = "flow-validation-test", TaskId = "flow-validation-task",
    };

    private static CopilotMcpToolDispatcher Dispatcher(Func<string, CancellationToken, Task<CopilotFlowValidationSnapshot?>> provider) =>
        new(new CopilotMcpToolEnvironment { WorkspaceSnapshotProvider = () => new CopilotMcpWorkspaceSnapshot(), FlowGraphValidator = provider });
}
