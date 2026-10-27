using ColorVision.Copilot.Mcp;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotFlowParameterRedactionTests
{
    [Theory]
    [InlineData("get_flow_summary")]
    [InlineData("preview_flow_action")]
    [InlineData("diagnose_flow_failure")]
    public async Task LegacyFlowTextSurfacesRedactByClrAndDisplayNamesWithoutDroppingDuplicateLabels(string toolName)
    {
        const string clrSecret = "ordinary-value-7319";
        const string displaySecret = "ordinary-value-4821";
        const string legacySecret = "ordinary-value-6173";
        var snapshot = Snapshot(
            new() { PropertyName = "ApiKey", Name = "Connection setting", Value = clrSecret },
            new() { PropertyName = "Mode", Name = "Setting", Value = "Production" },
            new() { PropertyName = "Route", Name = "Setting", Value = "Reference" },
            new() { Name = "LegacyMode", Value = "Retained" });
        var dispatcher = new CopilotMcpToolDispatcher(new CopilotMcpToolEnvironment
        {
            WorkspaceSnapshotProvider = () => new CopilotMcpWorkspaceSnapshot(),
            FlowSnapshotProvider = _ => Task.FromResult<CopilotFlowContextSnapshot?>(snapshot),
            RecentLogProvider = (_, _, _, _, _) => Task.FromResult(new CopilotCapabilityResult { Success = true, Content = "No matching log lines." }),
            LiveContextProvider = () => null,
        });
        var arguments = toolName switch
        {
            "preview_flow_action" => "{\"action\":\"explain_node\",\"node_id\":\"camera-1\"}",
            "diagnose_flow_failure" => "{\"node_id\":\"camera-1\"}",
            _ => "{}",
        };

        var result = await dispatcher.CallAsync(toolName,
            JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(arguments), CancellationToken.None);

        Assert.True(result.Success, result.Text);
        Assert.DoesNotContain(clrSecret, result.Text, StringComparison.Ordinal);
        Assert.Contains("Connection setting=<redacted>", result.Text, StringComparison.Ordinal);
        Assert.Contains("Setting=Production, Setting=Reference", result.Text, StringComparison.Ordinal);
        Assert.Contains("LegacyMode=Retained", result.Text, StringComparison.Ordinal);
        Assert.Equal(clrSecret, snapshot.Nodes[0].Parameters[0].Value);

        snapshot = Snapshot(
            new() { PropertyName = "Endpoint", Name = "ApiKey", Value = displaySecret },
            new() { Name = "Password", Value = legacySecret });
        var legacyResult = await dispatcher.CallAsync(toolName,
            JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(arguments), CancellationToken.None);
        Assert.True(legacyResult.Success, legacyResult.Text);
        Assert.DoesNotContain(displaySecret, legacyResult.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(legacySecret, legacyResult.Text, StringComparison.Ordinal);
        Assert.Contains("<redacted>", legacyResult.Text, StringComparison.Ordinal);
        Assert.Equal(displaySecret, snapshot.Nodes[0].Parameters[0].Value);
        Assert.Equal(legacySecret, snapshot.Nodes[0].Parameters[1].Value);
    }

    private static CopilotFlowContextSnapshot Snapshot(params CopilotContextProperty[] parameters) => new()
    {
        FlowName = "Offline inspection", Nodes =
        [
            new() { NodeId = "camera-1", Title = "Camera", IsSelected = true, Parameters = parameters },
        ],
    };
}
