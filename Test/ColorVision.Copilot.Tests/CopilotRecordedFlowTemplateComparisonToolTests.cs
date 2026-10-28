using ColorVision.Copilot.Mcp;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotRecordedFlowTemplateComparisonToolTests
{
    [Theory]
    [InlineData(1, int.MaxValue, null, null)]
    [InlineData(17, 17, 1, null)]
    [InlineData(17, 29, 200, "  opaque:cursor==  ")]
    public async Task BothSurfacesPreserveExplicitIdsDefaultsAndWholeJson(int baseline, int current, int? maximum, string? cursor)
    {
        var calls = new List<(int, int, int, string?)>();
        const string payload = "{\"same_recorded_bytes\":false,\"counts\":{\"detailed_change_rows\":25},\"has_more\":true,\"next_cursor\":\"next opaque cursor\",\"coverage\":{\"details_truncated\":true}}";
        var dispatcher = Dispatcher((left, right, count, continuation, _) =>
        {
            calls.Add((left, right, count, continuation));
            return Task.FromResult(new CopilotCapabilityResult { Success = true, Content = payload, Summary = "Not part of the JSON." });
        });
        var args = new Dictionary<string, object?> { ["baseline_run_record_id"] = baseline, ["current_run_record_id"] = current };
        if (maximum.HasValue) args["max_changes"] = maximum.Value;
        if (cursor != null) args["cursor"] = cursor;
        var tool = new CopilotCompareRecordedFlowTemplatesTool(dispatcher);
        Assert.True(tool.InputSchema.TryBind(args, out var input, out var error), error);
        var agent = await tool.ExecuteAsync(Request(), input, CancellationToken.None);
        var mcp = await dispatcher.CallAsync("compare_recorded_flow_templates", CopilotFlowReadToolBase.ToJsonArguments(input), CancellationToken.None);
        Assert.True(agent.Success, agent.ErrorMessage);
        Assert.True(mcp.Success, mcp.Text);
        Assert.Equal(new[] { (baseline, current, maximum ?? 50, cursor), (baseline, current, maximum ?? 50, cursor) }, calls);
        Assert.Equal(payload, agent.Content);
        Assert.Equal(payload, mcp.Text);
        using var document = JsonDocument.Parse(agent.Content);
        using var mcpDocument = JsonDocument.Parse(mcp.Text);
        Assert.True(document.RootElement.GetProperty("coverage").GetProperty("details_truncated").GetBoolean());
        Assert.True(document.RootElement.GetProperty("has_more").GetBoolean());
        Assert.Equal("next opaque cursor", document.RootElement.GetProperty("next_cursor").GetString());
        Assert.True(JsonElement.DeepEquals(document.RootElement, mcpDocument.RootElement));
        Assert.Null(agent.Approval);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"baseline_run_record_id\":1}")]
    [InlineData("{\"current_run_record_id\":2}")]
    [InlineData("{\"baseline_run_record_id\":0,\"current_run_record_id\":2}")]
    [InlineData("{\"baseline_run_record_id\":1,\"current_run_record_id\":2147483648}")]
    [InlineData("{\"baseline_run_record_id\":\"1\",\"current_run_record_id\":2}")]
    [InlineData("{\"baseline_run_record_id\":1,\"current_run_record_id\":2,\"max_changes\":0}")]
    [InlineData("{\"baseline_run_record_id\":1,\"current_run_record_id\":2,\"max_changes\":201}")]
    [InlineData("{\"baseline_run_record_id\":1,\"current_run_record_id\":2,\"snapshot_id\":3}")]
    [InlineData("{\"baseline_run_record_id\":1,\"current_run_record_id\":2,\"cursor\":\"\"}")]
    [MemberData(nameof(OversizedCursorArguments))]
    public async Task InvalidPairArgumentsDoNotReachTheProvider(string json)
    {
        var calls = 0;
        var dispatcher = Dispatcher((_, _, _, _, _) => { calls++; throw new InvalidOperationException("Invalid arguments reached comparison."); });
        var args = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
        var input = new CopilotAgentToolInput { Arguments = args.ToDictionary(item => item.Key, item => (object?)item.Value) };
        var agent = await new CopilotCompareRecordedFlowTemplatesTool(dispatcher).ExecuteAsync(Request(), input, CancellationToken.None);
        var mcp = await dispatcher.CallAsync("compare_recorded_flow_templates", args, CancellationToken.None);
        Assert.False(agent.Success);
        Assert.False(mcp.Success);
        Assert.Equal(CopilotToolFailureKind.Validation, agent.FailureKind);
        Assert.Equal(agent.FailureKind, mcp.FailureKind);
        Assert.Equal(0, calls);
    }

    public static IEnumerable<object[]> OversizedCursorArguments =>
    [
        [JsonSerializer.Serialize(new { baseline_run_record_id = 1, current_run_record_id = 2, cursor = new string('x', 4097) })],
    ];

    private static CopilotAgentRequest Request(CopilotAgentMode mode = CopilotAgentMode.Diagnose) => new()
    {
        Mode = mode, UserText = "Compare the retained templates for these exact runs.", ConversationId = "template-comparison-test", TaskId = "comparison-task",
    };
    private static CopilotMcpToolDispatcher Dispatcher(Func<int, int, int, string?, CancellationToken, Task<CopilotCapabilityResult>> provider) =>
        new(new CopilotMcpToolEnvironment { WorkspaceSnapshotProvider = () => new CopilotMcpWorkspaceSnapshot(), RecordedFlowTemplateComparer = provider });
}
