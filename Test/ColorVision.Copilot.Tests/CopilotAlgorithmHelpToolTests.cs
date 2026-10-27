using ColorVision.Copilot.Mcp;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotAlgorithmHelpToolTests
{
    [Fact]
    public void BothSurfacesPublishOneReadOnlyContractWithoutReadingHelpOrOpeningUi()
    {
        var definition = CopilotSharedCapabilityCatalog.AlgorithmHelp;
        var dispatcher = Dispatcher((_, _, _, _) => throw new InvalidOperationException("Discovery must not read help."));
        var tool = Assert.IsType<CopilotReadAlgorithmHelpTool>(definition.CreateAgentTool(dispatcher));
        var descriptor = Assert.Single(dispatcher.ListTools(), item => item.Name == "read_algorithm_help");
        Assert.Equal("docs.algorithm-help", definition.Id);
        Assert.Equal("ReadAlgorithmHelp", tool.Name);
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
        Assert.Equal(new[] { "topic_id" }, schema.GetProperty("required").EnumerateArray().Select(value => value.GetString()));
        var properties = schema.GetProperty("properties");
        Assert.Equal(1, properties.GetProperty("topic_id").GetProperty("minLength").GetInt32());
        Assert.Equal(128, properties.GetProperty("topic_id").GetProperty("maxLength").GetInt32());
        Assert.Equal("^[a-z0-9]+(?:[.-][a-z0-9]+)*$", properties.GetProperty("topic_id").GetProperty("pattern").GetString());
        Assert.Equal(1, properties.GetProperty("cursor").GetProperty("minLength").GetInt32());
        Assert.Equal(4096, properties.GetProperty("cursor").GetProperty("maxLength").GetInt32());
        Assert.Equal(256, properties.GetProperty("max_characters").GetProperty("minimum").GetInt32());
        Assert.Equal(12000, properties.GetProperty("max_characters").GetProperty("maximum").GetInt32());
        Assert.Equal(6000, properties.GetProperty("max_characters").GetProperty("default").GetInt32());
        var registry = new CopilotToolRegistry([tool]);
        foreach (var mode in Enum.GetValues<CopilotAgentMode>().Where(mode => mode != CopilotAgentMode.Chat))
            Assert.Single(registry.FindTools(Request(mode)));
        Assert.Empty(registry.FindTools(Request(CopilotAgentMode.Chat)));
    }

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

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"topic_id\":\"\"}")]
    [InlineData("{\"topic_id\":42}")]
    [InlineData("{\"topic_id\":null}")]
    [InlineData("{\"topic_id\":\"Algorithms.fov-local\"}")]
    [InlineData("{\"topic_id\":\"algorithms/fov-local\"}")]
    [InlineData("{\"topic_id\":\" algorithms.fov-local \"}")]
    [InlineData("{\"topic_id\":\"algorithms..fov-local\"}")]
    [InlineData("{\"topic_id\":\"algorithms.fov-local\",\"max_characters\":255}")]
    [InlineData("{\"topic_id\":\"algorithms.fov-local\",\"max_characters\":12001}")]
    [InlineData("{\"topic_id\":\"algorithms.fov-local\",\"max_characters\":6000.0}")]
    [InlineData("{\"topic_id\":\"algorithms.fov-local\",\"max_characters\":\"6000\"}")]
    [InlineData("{\"topic_id\":\"algorithms.fov-local\",\"cursor\":\"\"}")]
    [InlineData("{\"topic_id\":\"algorithms.fov-local\",\"cursor\":42}")]
    [InlineData("{\"topic_id\":\"algorithms.fov-local\",\"cursor\":null}")]
    [InlineData("{\"topic_id\":\"algorithms.fov-local\",\"path\":\"arbitrary.md\"}")]
    [MemberData(nameof(OversizedArguments))]
    public async Task InvalidArgumentsAreRejectedBeforeEitherSurfaceReadsHelp(string json)
    {
        var calls = 0;
        var dispatcher = Dispatcher((_, _, _, _) => { calls++; throw new InvalidOperationException("Invalid arguments reached help reader."); });
        var arguments = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
        var values = arguments.ToDictionary(item => item.Key, item => (object?)item.Value);
        var tool = new CopilotReadAlgorithmHelpTool(dispatcher);
        Assert.False(tool.InputSchema.TryBind(values, out _, out _));
        var agent = await tool.ExecuteAsync(Request(), new CopilotAgentToolInput { Arguments = values }, CancellationToken.None);
        var mcp = await dispatcher.CallAsync("read_algorithm_help", arguments, CancellationToken.None);
        Assert.False(agent.Success);
        Assert.False(mcp.Success);
        Assert.Equal(CopilotToolFailureKind.Validation, agent.FailureKind);
        Assert.Equal(agent.FailureKind, mcp.FailureKind);
        Assert.Equal(0, calls);
    }

    public static IEnumerable<object[]> OversizedArguments =>
    [
        [JsonSerializer.Serialize(new { topic_id = new string('x', 129) })],
        [JsonSerializer.Serialize(new { topic_id = "algorithms.fov-local", cursor = new string('x', 4097) })],
    ];

    [Fact]
    public async Task TrailingLineFeedInTopicIsRejectedBeforeEitherSurfaceReadsHelp()
    {
        var calls = 0;
        var dispatcher = Dispatcher((_, _, _, _) => { calls++; throw new InvalidOperationException("Invalid topic reached help reader."); });
        var input = new CopilotAgentToolInput
        {
            Arguments = new Dictionary<string, object?> { ["topic_id"] = "algorithms.fov-local\n" },
        };
        var agent = await new CopilotReadAlgorithmHelpTool(dispatcher).ExecuteAsync(Request(), input, CancellationToken.None);
        var mcp = await dispatcher.CallAsync("read_algorithm_help", CopilotFlowReadToolBase.ToJsonArguments(input), CancellationToken.None);
        Assert.False(agent.Success);
        Assert.False(mcp.Success);
        Assert.Equal(CopilotToolFailureKind.Validation, agent.FailureKind);
        Assert.Equal(agent.FailureKind, mcp.FailureKind);
        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData(CopilotToolFailureKind.NotFound)]
    [InlineData(CopilotToolFailureKind.Validation)]
    [InlineData(CopilotToolFailureKind.Transient)]
    public async Task BothSurfacesPreserveReaderFailureClassification(CopilotToolFailureKind kind)
    {
        var dispatcher = Dispatcher((_, _, _, _) => Task.FromResult(new CopilotCapabilityResult
        {
            Success = false, FailureKind = kind, ErrorMessage = "Help source is unavailable or continuation is invalid.",
        }));
        var input = new CopilotAgentToolInput { Arguments = new Dictionary<string, object?> { ["topic_id"] = "algorithms.fov-local" } };
        var agent = await new CopilotReadAlgorithmHelpTool(dispatcher).ExecuteAsync(Request(), input, CancellationToken.None);
        var mcp = await dispatcher.CallAsync("read_algorithm_help", CopilotFlowReadToolBase.ToJsonArguments(input), CancellationToken.None);
        Assert.False(agent.Success);
        Assert.False(mcp.Success);
        Assert.Equal(kind, agent.FailureKind);
        Assert.Equal(kind, mcp.FailureKind);
        Assert.Equal(mcp.Text, agent.Content);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CallerCancellationReachesTheReaderAndPropagates(bool agentSurface)
    {
        var entered = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var dispatcher = Dispatcher(async (_, _, _, token) =>
        {
            entered.TrySetResult(token);
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return new CopilotCapabilityResult { Success = true };
            }
            finally { stopped.TrySetResult(); }
        });
        var input = new CopilotAgentToolInput { Arguments = new Dictionary<string, object?> { ["topic_id"] = "algorithms.fov-local" } };
        using var cancellation = new CancellationTokenSource();
        Task call = agentSurface
            ? new CopilotReadAlgorithmHelpTool(dispatcher).ExecuteAsync(Request(), input, cancellation.Token)
            : dispatcher.CallAsync("read_algorithm_help", CopilotFlowReadToolBase.ToJsonArguments(input), cancellation.Token);
        var providerToken = await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call);
        await stopped.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(providerToken.IsCancellationRequested);
    }

    private static CopilotAgentRequest Request(CopilotAgentMode mode = CopilotAgentMode.Diagnose) => new()
    {
        Mode = mode, UserText = "Read the installed help for this exact algorithm topic.", ConversationId = "algorithm-help-test", TaskId = "algorithm-help-task",
    };

    private static CopilotMcpToolDispatcher Dispatcher(Func<string, string?, int, CancellationToken, Task<CopilotCapabilityResult>> provider) =>
        new(new CopilotMcpToolEnvironment { WorkspaceSnapshotProvider = () => new CopilotMcpWorkspaceSnapshot(), AlgorithmHelpReader = provider });
}
