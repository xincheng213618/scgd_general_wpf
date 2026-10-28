using ColorVision.Copilot.Mcp;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotFlowIncidentToolTests
{
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
