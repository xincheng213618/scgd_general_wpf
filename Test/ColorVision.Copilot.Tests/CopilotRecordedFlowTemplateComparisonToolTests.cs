using ColorVision.Copilot.Mcp;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotRecordedFlowTemplateComparisonToolTests
{
    [Fact]
    public void BothSurfacesPublishTheSameReadOnlyPairContractWithoutReadingStorage()
    {
        var definition = CopilotSharedCapabilityCatalog.RecordedFlowTemplateComparison;
        var dispatcher = Dispatcher((_, _, _, _, _) => throw new InvalidOperationException("Discovery must not read storage."));
        var tool = Assert.IsType<CopilotCompareRecordedFlowTemplatesTool>(definition.CreateAgentTool(dispatcher));
        var descriptor = Assert.Single(dispatcher.ListTools(), item => item.Name == "compare_recorded_flow_templates");
        Assert.Equal("flow.recorded-template-compare", definition.Id);
        Assert.Equal("CompareRecordedFlowTemplates", tool.Name);
        Assert.Same(definition.AgentInputSchema, definition.McpInputSchema);
        Assert.True(JsonElement.DeepEquals(tool.InputSchema.JsonSchema, Assert.IsType<JsonElement>(descriptor.InputSchema)));
        Assert.Equal(CopilotSharedCapabilityExecutionRoute.ApplicationCapabilityRuntime, definition.ExecutionRoute);
        Assert.Equal(CopilotToolAccess.ReadOnly, tool.Capability.Access);
        Assert.Equal(CopilotToolIdempotency.Idempotent, tool.Capability.Idempotency);
        Assert.Equal(CopilotToolConcurrencyMode.SharedRead, tool.Capability.EffectiveConcurrencyMode);
        Assert.Equal(CopilotToolApprovalMode.Never, tool.Capability.ApprovalMode);
        Assert.Equal(CopilotToolAuditArgumentMode.NamesOnly, tool.Capability.AuditArgumentMode);
        Assert.Equal(TimeSpan.FromSeconds(15), tool.Capability.EffectiveExecutionTimeout);
        var schema = tool.InputSchema.JsonSchema;
        Assert.Equal(new[] { "baseline_run_record_id", "current_run_record_id" }, schema.GetProperty("required").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal(int.MaxValue, schema.GetProperty("properties").GetProperty("current_run_record_id").GetProperty("maximum").GetInt32());
        Assert.Equal(50, schema.GetProperty("properties").GetProperty("max_changes").GetProperty("default").GetInt32());
        Assert.Equal(1, schema.GetProperty("properties").GetProperty("cursor").GetProperty("minLength").GetInt32());
        Assert.Equal(4096, schema.GetProperty("properties").GetProperty("cursor").GetProperty("maxLength").GetInt32());
        var registry = new CopilotToolRegistry([tool]);
        Assert.Single(registry.FindTools(Request()));
        Assert.Empty(registry.FindTools(Request(CopilotAgentMode.Chat)));
    }

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
    [InlineData("{\"baseline_run_record_id\":1.0,\"current_run_record_id\":2}")]
    [InlineData("{\"baseline_run_record_id\":\"1\",\"current_run_record_id\":2}")]
    [InlineData("{\"baseline_run_record_id\":1,\"current_run_record_id\":2,\"max_changes\":0}")]
    [InlineData("{\"baseline_run_record_id\":1,\"current_run_record_id\":2,\"max_changes\":201}")]
    [InlineData("{\"baseline_run_record_id\":1,\"current_run_record_id\":2,\"max_changes\":1.5}")]
    [InlineData("{\"baseline_run_record_id\":1,\"current_run_record_id\":2,\"snapshot_id\":3}")]
    [InlineData("{\"baseline_run_record_id\":1,\"current_run_record_id\":2,\"cursor\":\"\"}")]
    [InlineData("{\"baseline_run_record_id\":1,\"current_run_record_id\":2,\"cursor\":42}")]
    [InlineData("{\"baseline_run_record_id\":1,\"current_run_record_id\":2,\"cursor\":null}")]
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

    [Fact]
    public async Task BothSurfacesPreserveUnavailableSnapshotClassification()
    {
        var dispatcher = Dispatcher((_, _, _, _, _) => Task.FromResult(new CopilotCapabilityResult
        {
            Success = false, FailureKind = CopilotToolFailureKind.NotFound, ErrorMessage = "The exact referenced snapshot is unavailable.",
        }));
        var input = new CopilotAgentToolInput { Arguments = new Dictionary<string, object?> { ["baseline_run_record_id"] = 1, ["current_run_record_id"] = 2 } };
        var agent = await new CopilotCompareRecordedFlowTemplatesTool(dispatcher).ExecuteAsync(Request(), input, CancellationToken.None);
        var mcp = await dispatcher.CallAsync("compare_recorded_flow_templates", CopilotFlowReadToolBase.ToJsonArguments(input), CancellationToken.None);
        Assert.False(agent.Success);
        Assert.Equal(CopilotToolFailureKind.NotFound, agent.FailureKind);
        Assert.Equal(agent.FailureKind, mcp.FailureKind);
        Assert.Equal(mcp.Text, agent.Content);
    }

    [Fact]
    public async Task CallerCancellationReachesThePairedProvider()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var dispatcher = Dispatcher(async (_, _, _, _, token) =>
        {
            entered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return new CopilotCapabilityResult { Success = true };
        });
        var args = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>("{\"baseline_run_record_id\":1,\"current_run_record_id\":2}")!;
        using var cancellation = new CancellationTokenSource();
        var call = dispatcher.CallAsync("compare_recorded_flow_templates", args, cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call);
    }

    private static CopilotAgentRequest Request(CopilotAgentMode mode = CopilotAgentMode.Diagnose) => new()
    {
        Mode = mode, UserText = "Compare the retained templates for these exact runs.", ConversationId = "template-comparison-test", TaskId = "comparison-task",
    };
    private static CopilotMcpToolDispatcher Dispatcher(Func<int, int, int, string?, CancellationToken, Task<CopilotCapabilityResult>> provider) =>
        new(new CopilotMcpToolEnvironment { WorkspaceSnapshotProvider = () => new CopilotMcpWorkspaceSnapshot(), RecordedFlowTemplateComparer = provider });
}
