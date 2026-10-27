using ColorVision.Copilot.Mcp;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Types;
using System.Globalization;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotDeviceServicesWorkflowTests
{
    [Fact]
    public async Task DeviceBeyondTheAutomaticSummaryCanBeDiscoveredAndFocusedWithoutNameCodeOrCurrentSelectionFallback()
    {
        var observations = Enumerable.Range(1, 61).Select(index => new CopilotDeviceServiceObservation(
            index.ToString("x32", CultureInfo.InvariantCulture), "device-service:42", 42, "shared-code", "shared-name",
            (int)ServiceTypes.Camera, (int)(index == 61 ? DeviceStatusType.OffLine : DeviceStatusType.Opened),
            CopilotDeviceServiceStatusReadState.Cached)).ToArray();
        var snapshot = new CopilotDeviceServicesSnapshot(new string('a', 32), new string('b', 64),
            new DateTimeOffset(2026, 10, 27, 3, 0, 0, TimeSpan.Zero), observations);
        var dispatcher = new CopilotMcpToolDispatcher(new CopilotMcpToolEnvironment
        {
            WorkspaceSnapshotProvider = () => new CopilotMcpWorkspaceSnapshot(),
            LiveContextProvider = () => new CopilotLiveContext { SourceId = "device-service:unrelated-current-selection", Title = "Current device" },
            DeviceServicesSnapshotProvider = _ => Task.FromResult<CopilotDeviceServicesSnapshot?>(snapshot),
        });
        var tool = new CopilotInspectDeviceServicesTool(dispatcher);
        var request = new CopilotAgentRequest
        {
            Mode = CopilotAgentMode.Diagnose, UserText = "Inspect loaded devices and the unavailable camera.",
            ConversationId = "device-discovery", TaskId = "device-discovery-task",
        };

        var first = await tool.ExecuteAsync(request, Input(new { max_devices = 30 }), CancellationToken.None);

        Assert.True(first.Success, first.ErrorMessage);
        Assert.NotEmpty(first.PartialResultMessage);
        using var firstPage = JsonDocument.Parse(first.Content);
        Assert.Equal(61, firstPage.RootElement.GetProperty("total_devices").GetInt32());
        Assert.Equal(30, firstPage.RootElement.GetProperty("devices").GetArrayLength());
        Assert.True(firstPage.RootElement.GetProperty("has_more").GetBoolean());
        var owner = firstPage.RootElement.GetProperty("source_instance_id").GetString();
        var cursor = firstPage.RootElement.GetProperty("next_cursor").GetString();
        var continuation = Arguments(new { max_devices = 100, cursor });
        var second = await dispatcher.CallAsync("inspect_device_services", continuation, CancellationToken.None);
        Assert.True(second.Success, second.Text);
        using var secondPage = JsonDocument.Parse(second.Text);
        Assert.Equal(31, secondPage.RootElement.GetProperty("devices").GetArrayLength());
        Assert.False(secondPage.RootElement.GetProperty("has_more").GetBoolean());
        var target = Assert.Single(secondPage.RootElement.GetProperty("devices").EnumerateArray(),
            device => device.GetProperty("status_code").GetInt32() == (int)DeviceStatusType.OffLine);
        var targetRef = target.GetProperty("device_ref").GetString();
        var focusArguments = new { device_ref = targetRef, expected_source_instance_id = owner };

        var focus = await tool.ExecuteAsync(request, Input(focusArguments), CancellationToken.None);

        Assert.True(focus.Success, focus.ErrorMessage);
        using var focusPage = JsonDocument.Parse(focus.Content);
        Assert.Equal(61, focusPage.RootElement.GetProperty("total_devices").GetInt32());
        Assert.Equal(1, focusPage.RootElement.GetProperty("matched_devices").GetInt32());
        var focusedDevice = Assert.Single(focusPage.RootElement.GetProperty("devices").EnumerateArray());
        Assert.Equal(targetRef, focusedDevice.GetProperty("device_ref").GetString());
        Assert.Equal("shared-code", focusedDevice.GetProperty("code").GetString());
        Assert.Equal("shared-name", focusedDevice.GetProperty("name").GetString());
        Assert.Equal("OffLine", focusedDevice.GetProperty("status").GetString());
        Assert.Equal("cached", focusedDevice.GetProperty("status_read_state").GetString());
        Assert.False(focusPage.RootElement.GetProperty("has_more").GetBoolean());

        var replacement = observations[^1] with { DeviceRef = new string('f', 32), StatusCode = (int)DeviceStatusType.Closed };
        snapshot = snapshot with { InventoryRevision = new string('c', 64), Devices = observations[..^1].Append(replacement).ToArray() };
        var staleFocus = await tool.ExecuteAsync(request, Input(focusArguments), CancellationToken.None);
        Assert.False(staleFocus.Success);
        Assert.Equal(CopilotToolFailureKind.NotFound, staleFocus.FailureKind);
        var stalePage = await dispatcher.CallAsync("inspect_device_services", continuation, CancellationToken.None);
        Assert.False(stalePage.Success);
        Assert.Equal(CopilotToolFailureKind.Validation, stalePage.FailureKind);

        var currentFocusArguments = Arguments(new { device_ref = replacement.DeviceRef, expected_source_instance_id = owner });
        var currentFocus = await dispatcher.CallAsync("inspect_device_services", currentFocusArguments, CancellationToken.None);
        Assert.True(currentFocus.Success, currentFocus.Text);
        using var currentPage = JsonDocument.Parse(currentFocus.Text);
        Assert.Equal("Closed", Assert.Single(currentPage.RootElement.GetProperty("devices").EnumerateArray())
            .GetProperty("status").GetString());
        snapshot = snapshot with { SourceInstanceId = new string('d', 32) };
        var changedOwner = await dispatcher.CallAsync("inspect_device_services", currentFocusArguments, CancellationToken.None);
        Assert.False(changedOwner.Success);
        Assert.Equal(CopilotToolFailureKind.Validation, changedOwner.FailureKind);
    }

    private static Dictionary<string, JsonElement> Arguments(object value)
        => JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(JsonSerializer.Serialize(value))!;

    private static CopilotAgentToolInput Input(object value)
        => new() { Arguments = Arguments(value).ToDictionary(pair => pair.Key, pair => (object?)pair.Value) };
}
