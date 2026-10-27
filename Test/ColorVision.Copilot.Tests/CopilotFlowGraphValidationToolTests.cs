using ColorVision.Copilot.Mcp;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotFlowGraphValidationToolTests
{
    private const string Revision = "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";

    [Fact]
    public void BothSurfacesPublishOneReadOnlyRevisionContractWithoutInspectingTheEditor()
    {
        var definition = CopilotSharedCapabilityCatalog.FlowGraphValidation;
        var dispatcher = Dispatcher((_, _) => throw new InvalidOperationException("Discovery must not inspect the editor."));
        var tool = Assert.IsType<CopilotValidateFlowGraphTool>(definition.CreateAgentTool(dispatcher));
        var descriptor = Assert.Single(dispatcher.ListTools(), item => item.Name == "validate_flow_graph");
        Assert.Equal("flow.validate", definition.Id);
        Assert.Equal("ValidateFlowGraph", tool.Name);
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
        Assert.Equal(new[] { "expected_revision" }, schema.GetProperty("required").EnumerateArray().Select(value => value.GetString()));
        var revision = Assert.Single(schema.GetProperty("properties").EnumerateObject());
        Assert.Equal("expected_revision", revision.Name);
        Assert.Equal(64, revision.Value.GetProperty("minLength").GetInt32());
        Assert.Equal(64, revision.Value.GetProperty("maxLength").GetInt32());
        Assert.Equal("^[0-9a-fA-F]{64}$", revision.Value.GetProperty("pattern").GetString());
        var registry = new CopilotToolRegistry([tool]);
        foreach (var mode in Enum.GetValues<CopilotAgentMode>().Where(mode => mode != CopilotAgentMode.Chat))
            Assert.Single(registry.FindTools(Request(mode)));
        Assert.Empty(registry.FindTools(Request(CopilotAgentMode.Chat)));
    }

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

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"expected_revision\":\"\"}")]
    [InlineData("{\"expected_revision\":64}")]
    [InlineData("{\"expected_revision\":null}")]
    [MemberData(nameof(MalformedRevisionArguments))]
    public async Task InvalidArgumentsDoNotReachTheEditorProvider(string json)
    {
        var calls = 0;
        var dispatcher = Dispatcher((_, _) => { calls++; throw new InvalidOperationException("Invalid arguments reached the editor."); });
        var arguments = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
        var values = arguments.ToDictionary(item => item.Key, item => (object?)item.Value);
        var tool = new CopilotValidateFlowGraphTool(dispatcher);
        Assert.False(tool.InputSchema.TryBind(values, out _, out _));
        var agent = await tool.ExecuteAsync(Request(), new CopilotAgentToolInput { Arguments = values }, CancellationToken.None);
        var mcp = await dispatcher.CallAsync("validate_flow_graph", arguments, CancellationToken.None);
        Assert.False(agent.Success);
        Assert.False(mcp.Success);
        Assert.Equal(CopilotToolFailureKind.Validation, agent.FailureKind);
        Assert.Equal(agent.FailureKind, mcp.FailureKind);
        Assert.Equal(0, calls);
    }

    public static IEnumerable<object[]> MalformedRevisionArguments =>
    [
        [JsonSerializer.Serialize(new { expected_revision = new string('a', 63) })],
        [JsonSerializer.Serialize(new { expected_revision = new string('a', 65) })],
        [JsonSerializer.Serialize(new { expected_revision = new string('g', 64) })],
        [JsonSerializer.Serialize(new { expected_revision = " " + Revision })],
        [JsonSerializer.Serialize(new { expected_revision = Revision + "\n" })],
        [JsonSerializer.Serialize(new { expected_revision = Revision, node_id = "not-a-whole-graph-check" })],
    ];

    [Theory]
    [InlineData("missing-editor", CopilotToolFailureKind.NotFound)]
    [InlineData("revision-mismatch", CopilotToolFailureKind.Validation)]
    public async Task BothSurfacesPreserveUnavailableOrStaleCheckFailures(string failure, CopilotToolFailureKind kind)
    {
        var dispatcher = Dispatcher((_, _) => Task.FromResult<CopilotFlowValidationSnapshot?>(failure == "missing-editor" ? null
            : new(new string('b', 64), false, null, "PRIVATE_STALE_DIAGNOSTIC", null, 3, 2, false, false)));
        var input = Input();
        var agent = await new CopilotValidateFlowGraphTool(dispatcher).ExecuteAsync(Request(), input, CancellationToken.None);
        var mcp = await dispatcher.CallAsync("validate_flow_graph", CopilotFlowReadToolBase.ToJsonArguments(input), CancellationToken.None);
        Assert.False(agent.Success);
        Assert.False(mcp.Success);
        Assert.Equal(kind, agent.FailureKind);
        Assert.Equal(kind, mcp.FailureKind);
        Assert.Equal(mcp.Text, agent.Content);
        Assert.DoesNotContain("PRIVATE_STALE_DIAGNOSTIC", agent.Content, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CallerCancellationReachesTheValidatorAndPropagates(bool agentSurface)
    {
        var entered = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var dispatcher = Dispatcher(async (_, token) =>
        {
            entered.TrySetResult(token);
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return null;
            }
            finally { stopped.TrySetResult(); }
        });
        using var cancellation = new CancellationTokenSource();
        Task call = agentSurface
            ? new CopilotValidateFlowGraphTool(dispatcher).ExecuteAsync(Request(), Input(), cancellation.Token)
            : dispatcher.CallAsync("validate_flow_graph", CopilotFlowReadToolBase.ToJsonArguments(Input()), cancellation.Token);
        var providerToken = await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call);
        await stopped.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(providerToken.IsCancellationRequested);
    }

    private static CopilotAgentToolInput Input() => new() { Arguments = new Dictionary<string, object?> { ["expected_revision"] = Revision } };

    private static CopilotAgentRequest Request(CopilotAgentMode mode = CopilotAgentMode.Diagnose) => new()
    {
        Mode = mode, UserText = "Validate the current flow graph at this exact revision.", ConversationId = "flow-validation-test", TaskId = "flow-validation-task",
    };

    private static CopilotMcpToolDispatcher Dispatcher(Func<string, CancellationToken, Task<CopilotFlowValidationSnapshot?>> provider) =>
        new(new CopilotMcpToolEnvironment { WorkspaceSnapshotProvider = () => new CopilotMcpWorkspaceSnapshot(), FlowGraphValidator = provider });
}
