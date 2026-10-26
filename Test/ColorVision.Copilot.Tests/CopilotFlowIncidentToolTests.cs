using ColorVision.Copilot.Mcp;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotFlowIncidentToolTests
{
    [Fact]
    public void BothSurfacesPublishTheSameBoundedReadOnlyApplicationContract()
    {
        var definition = CopilotSharedCapabilityCatalog.FlowIncident;
        var dispatcher = CreateDispatcher((_, _, _) => throw new InvalidOperationException("Discovery must not inspect an incident."));
        var tool = Assert.IsType<CopilotInspectFlowIncidentTool>(definition.CreateAgentTool(dispatcher));
        var descriptor = Assert.Single(dispatcher.ListTools(), item => item.Name == "inspect_flow_incident");

        Assert.Equal("flow.incident", definition.Id);
        Assert.Equal("InspectFlowIncident", tool.Name);
        Assert.Contains(definition, CopilotSharedCapabilityCatalog.All);
        Assert.Equal(CopilotSharedCapabilityExecutionRoute.ApplicationCapabilityRuntime, definition.ExecutionRoute);
        Assert.Same(dispatcher, tool.ApplicationCapabilityInvoker);
        Assert.Same(definition.AgentInputSchema, tool.InputSchema);
        Assert.Same(definition.AgentInputSchema, definition.McpInputSchema);
        Assert.True(JsonElement.DeepEquals(tool.InputSchema.JsonSchema, Assert.IsType<JsonElement>(descriptor.InputSchema)));
        Assert.Equal(CopilotToolAccess.ReadOnly, tool.Capability.Access);
        Assert.Equal(CopilotToolIdempotency.Idempotent, tool.Capability.Idempotency);
        Assert.Equal(CopilotToolConcurrencyMode.SharedRead, tool.Capability.EffectiveConcurrencyMode);
        Assert.Equal(CopilotToolApprovalMode.Never, tool.Capability.ApprovalMode);
        Assert.Equal(CopilotToolAuditArgumentMode.NamesOnly, tool.Capability.AuditArgumentMode);
        Assert.Equal(TimeSpan.FromSeconds(15), tool.Capability.EffectiveExecutionTimeout);
        Assert.Equal(tool.Capability.EffectiveExecutionTimeout, descriptor.EffectiveExecutionTimeout);
        Assert.True(Assert.IsType<bool>(descriptor.Annotations["readOnlyHint"]));
        Assert.True(Assert.IsType<bool>(descriptor.Annotations["idempotentHint"]));
        Assert.False(Assert.IsType<bool>(descriptor.Annotations["destructiveHint"]));

        var properties = tool.InputSchema.JsonSchema.GetProperty("properties");
        Assert.Equal("integer", properties.GetProperty("incident_id").GetProperty("type").GetString());
        Assert.Equal(1, properties.GetProperty("incident_id").GetProperty("minimum").GetInt64());
        Assert.Equal(long.MaxValue, properties.GetProperty("incident_id").GetProperty("maximum").GetInt64());
        Assert.Equal(1, properties.GetProperty("max_events").GetProperty("minimum").GetInt32());
        Assert.Equal(100, properties.GetProperty("max_events").GetProperty("maximum").GetInt32());
        Assert.Equal(40, properties.GetProperty("max_events").GetProperty("default").GetInt32());

        var registry = new CopilotToolRegistry([tool]);
        foreach (var mode in new[] { CopilotAgentMode.Auto, CopilotAgentMode.Plan, CopilotAgentMode.Diagnose })
        {
            var request = Request(mode);
            Assert.Same(tool, Assert.Single(registry.FindTools(request)));
        }
        Assert.Empty(registry.FindTools(Request(CopilotAgentMode.Chat)));
    }

    [Theory]
    [InlineData(9007199254740993L, null)]
    [InlineData(long.MaxValue, 100)]
    public async Task BothSurfacesUseTheInjectedInspectorWithoutLosingLongIdsOrPartialEvidence(long incidentId, int? maxEvents)
    {
        var calls = new List<(long IncidentId, int MaxEvents)>();
        var providerResult = new CopilotCapabilityResult
        {
            Success = true,
            Summary = "Recorded incident inspection.",
            PartialResultMessage = "Only part of the event sequence is shown.",
            Content = JsonSerializer.Serialize(new
            {
                incident = new { incident_id = incidentId, run_record_id = 17 },
                coverage = new { returned_window_complete = false, partial_message = "Only part of the event sequence is shown." },
            }),
        };
        var dispatcher = CreateDispatcher((id, limit, _) =>
        {
            calls.Add((id, limit));
            return Task.FromResult(providerResult);
        });
        var tool = new CopilotInspectFlowIncidentTool(dispatcher);
        var arguments = new Dictionary<string, object?> { ["incident_id"] = incidentId };
        if (maxEvents.HasValue)
            arguments["max_events"] = maxEvents.Value;
        Assert.True(tool.InputSchema.TryBind(arguments, out var input, out var error), error);

        var agent = await tool.ExecuteAsync(Request(), input, CancellationToken.None);
        var mcp = await dispatcher.CallAsync("inspect_flow_incident", CopilotFlowReadToolBase.ToJsonArguments(input), CancellationToken.None);

        Assert.True(agent.Success, agent.ErrorMessage);
        Assert.True(mcp.Success, mcp.Text);
        Assert.Equal(new[] { (incidentId, maxEvents ?? 40), (incidentId, maxEvents ?? 40) }, calls);
        Assert.Equal(mcp.Text, agent.Content);
        Assert.Equal(providerResult.Content, agent.Content);
        Assert.Contains(providerResult.PartialResultMessage, agent.Content, StringComparison.Ordinal);
        Assert.DoesNotContain(providerResult.Summary, agent.Content, StringComparison.Ordinal);
        Assert.InRange(agent.Content.Length, 1, CopilotFlowIncidentInspectionService.MaximumContentCharacters);
        foreach (var content in new[] { agent.Content, mcp.Text })
        {
            using var document = JsonDocument.Parse(content);
            Assert.Equal(incidentId, document.RootElement.GetProperty("incident").GetProperty("incident_id").GetInt64());
            Assert.False(document.RootElement.GetProperty("coverage").GetProperty("returned_window_complete").GetBoolean());
        }
        Assert.Null(agent.Approval);
        Assert.False(mcp.RequiresApproval);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"incident_id\":\"17\"}")]
    [InlineData("{\"incident_id\":0}")]
    [InlineData("{\"incident_id\":-1}")]
    [InlineData("{\"incident_id\":1.0}")]
    [InlineData("{\"incident_id\":9223372036854775808}")]
    [InlineData("{\"incident_id\":17,\"max_events\":0}")]
    [InlineData("{\"incident_id\":17,\"max_events\":101}")]
    [InlineData("{\"incident_id\":17,\"max_events\":\"40\"}")]
    [InlineData("{\"incident_id\":17,\"max_events\":1.5}")]
    [InlineData("{\"incident_id\":17,\"query\":\"guess latest\"}")]
    public async Task BothSurfacesRejectInvalidArgumentsBeforeTheInspectorRuns(string json)
    {
        var calls = 0;
        var dispatcher = CreateDispatcher((_, _, _) =>
        {
            ++calls;
            throw new InvalidOperationException("Invalid input reached the inspector.");
        });
        var tool = new CopilotInspectFlowIncidentTool(dispatcher);
        var arguments = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
        var agentArguments = arguments.ToDictionary(pair => pair.Key, pair => (object?)pair.Value);
        Assert.False(tool.InputSchema.TryBind(agentArguments, out _, out _));

        var agent = await tool.ExecuteAsync(Request(), new CopilotAgentToolInput { Arguments = agentArguments }, CancellationToken.None);
        var mcp = await dispatcher.CallAsync("inspect_flow_incident", arguments, CancellationToken.None);

        Assert.False(agent.Success);
        Assert.Equal(CopilotToolFailureKind.Validation, agent.FailureKind);
        Assert.False(mcp.Success);
        Assert.Equal(CopilotToolFailureKind.Validation, mcp.FailureKind);
        Assert.Equal("invalid_arguments", mcp.ErrorCode);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task BothSurfacesPreserveTheInspectorFailureClassification()
    {
        var dispatcher = CreateDispatcher((_, _, _) => Task.FromResult(new CopilotCapabilityResult
        {
            Success = false,
            FailureKind = CopilotToolFailureKind.NotFound,
            ErrorMessage = "The exact recorded incident is absent.",
        }));
        var input = new CopilotAgentToolInput { Arguments = new Dictionary<string, object?> { ["incident_id"] = 17L } };

        var agent = await new CopilotInspectFlowIncidentTool(dispatcher).ExecuteAsync(Request(), input, CancellationToken.None);
        var mcp = await dispatcher.CallAsync("inspect_flow_incident", CopilotFlowReadToolBase.ToJsonArguments(input), CancellationToken.None);

        Assert.False(agent.Success);
        Assert.False(mcp.Success);
        Assert.Equal(CopilotToolFailureKind.NotFound, agent.FailureKind);
        Assert.Equal(agent.FailureKind, mcp.FailureKind);
        Assert.Equal(mcp.Text, agent.Content);
        Assert.Contains("exact recorded incident is absent", agent.Content, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CallerCancellationReachesTheInspectorAndPropagates(bool agentSurface)
    {
        var entered = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var dispatcher = CreateDispatcher(async (_, _, token) =>
        {
            entered.TrySetResult(token);
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return new CopilotCapabilityResult { Success = true };
            }
            finally
            {
                stopped.TrySetResult();
            }
        });
        var input = new CopilotAgentToolInput { Arguments = new Dictionary<string, object?> { ["incident_id"] = 17L } };
        using var cancellation = new CancellationTokenSource();
        Task call = agentSurface
            ? new CopilotInspectFlowIncidentTool(dispatcher).ExecuteAsync(Request(), input, cancellation.Token)
            : dispatcher.CallAsync("inspect_flow_incident", CopilotFlowReadToolBase.ToJsonArguments(input), cancellation.Token);
        var providerToken = await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call);
        await stopped.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(providerToken.IsCancellationRequested);
    }

    private static CopilotAgentRequest Request(CopilotAgentMode mode = CopilotAgentMode.Auto) => new()
    {
        Mode = mode,
        UserText = "Explain the current business context.",
        ConversationId = "flow-incident-test-conversation",
        TaskId = "flow-incident-test-task",
    };

    private static CopilotMcpToolDispatcher CreateDispatcher(Func<long, int, CancellationToken, Task<CopilotCapabilityResult>> inspector) =>
        new(new CopilotMcpToolEnvironment
        {
            WorkspaceSnapshotProvider = () => new CopilotMcpWorkspaceSnapshot(),
            FlowIncidentInspector = inspector,
        });
}
