using ColorVision.Copilot.Mcp;
using ColorVision.Engine.Services;
using System.Text.Json;
using static ColorVision.Copilot.Tests.CopilotDeviceServicesServiceTests;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotDeviceServicesToolTests
{
    [Theory]
    [InlineData("list")]
    [InlineData("focus")]
    [InlineData("cursor")]
    public async Task AgentAndMcpReadTheSameDirectoryFocusAndContinuationAsWholeJson(string mode)
    {
        var snapshot = Snapshot([Row(10), Row(1)]);
        var dispatcher = Dispatcher(_ => Task.FromResult<CopilotDeviceServicesSnapshot?>(snapshot));
        var arguments = new Dictionary<string, object?>();
        if (mode == "focus")
        {
            arguments["device_ref"] = Ref(10).ToUpperInvariant();
            arguments["expected_source_instance_id"] = snapshot.SourceInstanceId.ToUpperInvariant();
            arguments["max_devices"] = 100;
        }
        if (mode == "cursor")
        {
            var first = await dispatcher.CallAsync("inspect_device_services", new Dictionary<string, JsonElement> { ["max_devices"] = JsonSerializer.SerializeToElement(1) }, CancellationToken.None);
            Assert.True(first.Success, first.Text);
            using var firstJson = JsonDocument.Parse(first.Text);
            arguments["cursor"] = firstJson.RootElement.GetProperty("next_cursor").GetString();
            arguments["max_devices"] = 100;
        }
        var tool = new CopilotInspectDeviceServicesTool(dispatcher);
        Assert.True(tool.InputSchema.TryBind(arguments, out var input, out var error), error);
        var agent = await tool.ExecuteAsync(Request(), input, CancellationToken.None);
        var mcp = await dispatcher.CallAsync("inspect_device_services", CopilotFlowReadToolBase.ToJsonArguments(input), CancellationToken.None);
        Assert.True(agent.Success, agent.ErrorMessage);
        Assert.True(mcp.Success, mcp.Text);
        Assert.Equal(agent.Content, mcp.Text);
        using var document = JsonDocument.Parse(agent.Content);
        var root = document.RootElement;
        Assert.Equal(snapshot.SourceInstanceId, root.GetProperty("source_instance_id").GetString());
        Assert.Equal(2, root.GetProperty("total_devices").GetInt32());
        Assert.Equal(mode == "list" ? 2 : 1, root.GetProperty("devices").GetArrayLength());
        Assert.Equal(mode == "focus" ? 1 : 2, root.GetProperty("matched_devices").GetInt32());
        Assert.False(root.GetProperty("has_more").GetBoolean());
        Assert.Equal(mode == "cursor", !string.IsNullOrEmpty(agent.PartialResultMessage));
        Assert.Equal(agent.PartialResultMessage, mcp.PartialResultMessage);
        Assert.Null(agent.Approval);
    }

    [Fact]
    public async Task UnsupportedMetadataIsExplicitPartialEvidenceOnBothSurfaces()
    {
        var dispatcher = Dispatcher(_ => Task.FromResult<CopilotDeviceServicesSnapshot?>(Snapshot([Row(1) with
        {
            StatusCode = null, StatusReadState = CopilotDeviceServiceStatusReadState.Unsupported,
        }])));
        var agent = await new CopilotInspectDeviceServicesTool(dispatcher).ExecuteAsync(Request(), CopilotAgentToolInput.Empty, CancellationToken.None);
        var mcp = await dispatcher.CallAsync("inspect_device_services", null, CancellationToken.None);
        Assert.True(agent.Success);
        Assert.True(mcp.Success);
        Assert.NotEmpty(agent.PartialResultMessage);
        Assert.Equal(agent.PartialResultMessage, mcp.PartialResultMessage);
        using var document = JsonDocument.Parse(agent.Content);
        var row = Assert.Single(document.RootElement.GetProperty("devices").EnumerateArray());
        Assert.Equal("unsupported", row.GetProperty("status_read_state").GetString());
        Assert.Equal("Unknown", row.GetProperty("status").GetString());
    }

    [Fact]
    public async Task OwnerChangesKeepTheirValidationClassificationAcrossBothSurfaces()
    {
        var dispatcher = Dispatcher(_ => throw new CopilotDeviceServicesSourceChangedException("PRIVATE_DETAIL"));
        var agent = await new CopilotInspectDeviceServicesTool(dispatcher).ExecuteAsync(Request(), CopilotAgentToolInput.Empty, CancellationToken.None);
        var mcp = await dispatcher.CallAsync("inspect_device_services", null, CancellationToken.None);
        Assert.False(agent.Success);
        Assert.False(mcp.Success);
        Assert.Equal(CopilotToolFailureKind.Validation, agent.FailureKind);
        Assert.Equal(agent.FailureKind, mcp.FailureKind);
        Assert.DoesNotContain("PRIVATE_DETAIL", agent.Content, StringComparison.Ordinal);
    }

    private static CopilotAgentRequest Request(CopilotAgentMode mode = CopilotAgentMode.Diagnose) => new()
    {
        Mode = mode, UserText = "Inspect the loaded device registry.", ConversationId = "devices-test", TaskId = "devices-task",
    };
    private static CopilotMcpToolDispatcher Dispatcher(Func<CancellationToken, Task<CopilotDeviceServicesSnapshot?>> provider) => new(new CopilotMcpToolEnvironment
    {
        WorkspaceSnapshotProvider = () => new CopilotMcpWorkspaceSnapshot(), DeviceServicesSnapshotProvider = provider,
    });
}
