using ColorVision.Copilot.Mcp;
using ColorVision.Engine;
using System.Text.Json;
using static ColorVision.Copilot.Tests.CopilotLoadedBatchResultsServiceTests;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotLoadedBatchResultsToolTests
{
    [Fact]
    public void BothSurfacesPublishOneReadOnlyLoadedResultContract()
    {
        var definition = CopilotSharedCapabilityCatalog.LoadedBatchResults;
        var dispatcher = Dispatcher(_ => throw new InvalidOperationException("Discovery must not capture results."));
        var tool = Assert.IsType<CopilotInspectLoadedBatchResultsTool>(definition.CreateAgentTool(dispatcher));
        var descriptor = Assert.Single(dispatcher.ListTools(), row => row.Name == "inspect_loaded_batch_results");
        Assert.Equal("measurement.loaded-batch-results", definition.Id);
        Assert.Equal("InspectLoadedBatchResults", tool.Name);
        Assert.Same(dispatcher, tool.ApplicationCapabilityInvoker);
        Assert.Same(definition.AgentInputSchema, definition.McpInputSchema);
        Assert.True(JsonElement.DeepEquals(tool.InputSchema.JsonSchema, Assert.IsType<JsonElement>(descriptor.InputSchema)));
        Assert.Equal(CopilotSharedCapabilityExecutionRoute.ApplicationCapabilityRuntime, definition.ExecutionRoute);
        Assert.Equal(CopilotToolAccess.ReadOnly, tool.Capability.Access);
        Assert.Equal(CopilotToolIdempotency.Idempotent, tool.Capability.Idempotency);
        Assert.Equal(CopilotToolConcurrencyMode.SharedRead, tool.Capability.EffectiveConcurrencyMode);
        Assert.Equal(CopilotToolApprovalMode.Never, tool.Capability.ApprovalMode);
        Assert.Equal(CopilotToolAuditArgumentMode.NamesOnly, tool.Capability.AuditArgumentMode);
        Assert.Equal(TimeSpan.FromSeconds(15), tool.Capability.EffectiveExecutionTimeout);
        var properties = tool.InputSchema.JsonSchema.GetProperty("properties");
        foreach (var field in new[] { "source_ref", "expected_source_instance_id", "result_ref" })
        {
            Assert.Equal(32, properties.GetProperty(field).GetProperty("minLength").GetInt32());
            Assert.Equal(32, properties.GetProperty(field).GetProperty("maxLength").GetInt32());
            Assert.Equal("^[0-9a-fA-F]{32}$", properties.GetProperty(field).GetProperty("pattern").GetString());
        }
        Assert.Equal(1, properties.GetProperty("max_items").GetProperty("minimum").GetInt32());
        Assert.Equal(100, properties.GetProperty("max_items").GetProperty("maximum").GetInt32());
        Assert.Equal(40, properties.GetProperty("max_items").GetProperty("default").GetInt32());
        Assert.Equal(1, properties.GetProperty("cursor").GetProperty("minLength").GetInt32());
        Assert.Equal(4096, properties.GetProperty("cursor").GetProperty("maxLength").GetInt32());
        var registry = new CopilotToolRegistry([tool]);
        foreach (var mode in Enum.GetValues<CopilotAgentMode>().Where(mode => mode != CopilotAgentMode.Chat))
            Assert.Single(registry.FindTools(Request(mode)));
        Assert.Empty(registry.FindTools(Request(CopilotAgentMode.Chat)));
    }

    [Theory]
    [InlineData("directory")]
    [InlineData("results")]
    [InlineData("focus")]
    [InlineData("continuation")]
    public async Task NativeAndMcpReadDirectoryRowsAndExactFocusAsTheSameWholeJson(string mode)
    {
        var snapshot = Snapshot([Source(10, [Row(10), Row(11) with { Kind = "algorithm", ResultCode = null, DurationMs = -17 }]), Source(20, [])]);
        var dispatcher = Dispatcher(_ => Task.FromResult(snapshot));
        var arguments = new Dictionary<string, object?>();
        if (mode != "directory")
        {
            arguments["source_ref"] = Ref(10).ToUpperInvariant();
            arguments["expected_source_instance_id"] = Registry.ToUpperInvariant();
        }
        if (mode == "focus") arguments["result_ref"] = Ref(11).ToUpperInvariant();
        if (mode == "continuation")
        {
            var firstArguments = arguments.ToDictionary(pair => pair.Key, pair => JsonSerializer.SerializeToElement(pair.Value));
            firstArguments["max_items"] = JsonSerializer.SerializeToElement(1);
            var first = await dispatcher.CallAsync("inspect_loaded_batch_results", firstArguments, CancellationToken.None);
            Assert.True(first.Success, first.Text);
            using var firstJson = JsonDocument.Parse(first.Text);
            arguments["cursor"] = firstJson.RootElement.GetProperty("next_cursor").GetString();
            arguments["max_items"] = 100;
        }
        var tool = new CopilotInspectLoadedBatchResultsTool(dispatcher);
        Assert.True(tool.InputSchema.TryBind(arguments, out var input, out var error), error);
        var agent = await tool.ExecuteAsync(Request(), input, CancellationToken.None);
        var mcp = await dispatcher.CallAsync("inspect_loaded_batch_results", CopilotFlowReadToolBase.ToJsonArguments(input), CancellationToken.None);
        Assert.True(agent.Success, agent.ErrorMessage);
        Assert.True(mcp.Success, mcp.Text);
        Assert.Equal(agent.Content, mcp.Text);
        Assert.Equal(agent.PartialResultMessage, mcp.PartialResultMessage);
        using var document = JsonDocument.Parse(agent.Content);
        var root = document.RootElement;
        Assert.Equal(Registry, root.GetProperty("source_instance_id").GetString());
        Assert.Equal(mode == "continuation" ? "results" : mode, root.GetProperty("mode").GetString());
        Assert.Equal(mode == "directory" ? 2 : 0, root.GetProperty("sources").GetArrayLength());
        Assert.Equal(mode == "directory" ? 0 : mode == "results" ? 2 : 1, root.GetProperty("results").GetArrayLength());
        if (mode != "directory")
        {
            Assert.Equal(Ref(10), root.GetProperty("source_ref").GetString());
            Assert.NotEmpty(agent.PartialResultMessage);
        }
        Assert.Null(agent.Approval);
        Assert.False(mcp.RequiresApproval);
    }

    [Theory]
    [InlineData("{\"source_ref\":\"00000000000000000000000000000001\"}")]
    [InlineData("{\"result_ref\":\"00000000000000000000000000000001\"}")]
    [InlineData("{\"source_ref\":\"00000000000000000000000000000001\",\"expected_source_instance_id\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"result_ref\":\"00000000000000000000000000000001\",\"cursor\":\"opaque\"}")]
    [InlineData("{\"source_ref\":\"current\"}")]
    [InlineData("{\"source_ref\":null}")]
    [InlineData("{\"expected_source_instance_id\":null}")]
    [InlineData("{\"result_ref\":\" selected \"}")]
    [InlineData("{\"max_items\":0}")]
    [InlineData("{\"max_items\":101}")]
    [InlineData("{\"max_items\":1.0}")]
    [InlineData("{\"max_items\":\"40\"}")]
    [InlineData("{\"cursor\":\"\"}")]
    [InlineData("{\"cursor\":null}")]
    [InlineData("{\"cursor\":7}")]
    [InlineData("{\"cursor\":\"not-a-cursor\"}")]
    [InlineData("{\"batch_id\":17}")]
    [MemberData(nameof(OversizedCursor))]
    public async Task InvalidProtocolOrScopeArgumentsDoNotReachEitherSnapshotRoute(string json)
    {
        var calls = 0;
        var dispatcher = Dispatcher(_ => { calls++; throw new InvalidOperationException("Invalid input reached source capture."); });
        var arguments = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
        var input = new CopilotAgentToolInput { Arguments = arguments.ToDictionary(pair => pair.Key, pair => (object?)pair.Value) };
        var agent = await new CopilotInspectLoadedBatchResultsTool(dispatcher).ExecuteAsync(Request(), input, CancellationToken.None);
        var mcp = await dispatcher.CallAsync("inspect_loaded_batch_results", arguments, CancellationToken.None);
        Assert.False(agent.Success);
        Assert.False(mcp.Success);
        Assert.Equal(CopilotToolFailureKind.Validation, agent.FailureKind);
        Assert.Equal(agent.FailureKind, mcp.FailureKind);
        Assert.Equal(0, calls);
    }
    public static IEnumerable<object[]> OversizedCursor => [[JsonSerializer.Serialize(new { cursor = new string('c', 4097) })]];

    [Fact]
    public async Task UnavailableRegistryKeepsItsClassificationWithoutLeakingRawExceptionText()
    {
        var dispatcher = Dispatcher(_ => throw new CopilotLoadedBatchResultsSourceUnavailableException("PRIVATE_DETAIL"));
        var agent = await new CopilotInspectLoadedBatchResultsTool(dispatcher).ExecuteAsync(Request(), CopilotAgentToolInput.Empty, CancellationToken.None);
        var mcp = await dispatcher.CallAsync("inspect_loaded_batch_results", null, CancellationToken.None);
        Assert.False(agent.Success);
        Assert.False(mcp.Success);
        Assert.Equal(CopilotToolFailureKind.NotFound, agent.FailureKind);
        Assert.Equal(agent.FailureKind, mcp.FailureKind);
        Assert.DoesNotContain("PRIVATE_DETAIL", agent.Content, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CallerCancellationReachesTheSnapshotProviderAndPropagates(bool agentSurface)
    {
        var entered = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var dispatcher = Dispatcher(async token =>
        {
            entered.TrySetResult(token);
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return Snapshot([]);
        });
        using var cancellation = new CancellationTokenSource();
        Task call = agentSurface
            ? new CopilotInspectLoadedBatchResultsTool(dispatcher).ExecuteAsync(Request(), CopilotAgentToolInput.Empty, cancellation.Token)
            : dispatcher.CallAsync("inspect_loaded_batch_results", null, cancellation.Token);
        var providerToken = await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call);
        Assert.True(providerToken.IsCancellationRequested);
    }

    private static CopilotAgentRequest Request(CopilotAgentMode mode = CopilotAgentMode.Diagnose) => new()
    {
        Mode = mode, UserText = "Read registered loaded batch details.", ConversationId = "loaded-batches-test", TaskId = "loaded-batches-task",
    };
    private static CopilotMcpToolDispatcher Dispatcher(Func<CancellationToken, Task<CopilotLoadedBatchResultsSnapshot>> provider) => new(new CopilotMcpToolEnvironment
    {
        WorkspaceSnapshotProvider = () => new CopilotMcpWorkspaceSnapshot(), LoadedBatchResultsSnapshotProvider = provider,
    });
}
