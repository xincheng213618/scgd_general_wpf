using ColorVision.Copilot.Mcp;
using ColorVision.Engine.Services;
using System.Text.Json;
using static ColorVision.Copilot.Tests.CopilotDeviceServicesServiceTests;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotDeviceServicesToolTests
{
    [Fact]
    public void BothSurfacesShareTheReadOnlyLoadedRegistryContract()
    {
        var definition = CopilotSharedCapabilityCatalog.DeviceServices;
        var dispatcher = Dispatcher(_ => throw new InvalidOperationException("Discovery must not capture devices."));
        var tool = Assert.IsType<CopilotInspectDeviceServicesTool>(definition.CreateAgentTool(dispatcher));
        var descriptor = Assert.Single(dispatcher.ListTools(), item => item.Name == "inspect_device_services");
        Assert.Equal("device.services", definition.Id);
        Assert.Equal("InspectDeviceServices", tool.Name);
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
        foreach (var field in new[] { "device_ref", "expected_source_instance_id" })
        {
            Assert.Equal(32, properties.GetProperty(field).GetProperty("minLength").GetInt32());
            Assert.Equal(32, properties.GetProperty(field).GetProperty("maxLength").GetInt32());
            Assert.Equal("^[0-9a-fA-F]{32}$", properties.GetProperty(field).GetProperty("pattern").GetString());
        }
        Assert.Equal(1, properties.GetProperty("max_devices").GetProperty("minimum").GetInt32());
        Assert.Equal(100, properties.GetProperty("max_devices").GetProperty("maximum").GetInt32());
        Assert.Equal(40, properties.GetProperty("max_devices").GetProperty("default").GetInt32());
        Assert.Equal(1, properties.GetProperty("cursor").GetProperty("minLength").GetInt32());
        Assert.Equal(4096, properties.GetProperty("cursor").GetProperty("maxLength").GetInt32());
        var registry = new CopilotToolRegistry([tool]);
        foreach (var mode in Enum.GetValues<CopilotAgentMode>().Where(mode => mode != CopilotAgentMode.Chat))
            Assert.Single(registry.FindTools(Request(mode)));
        Assert.Empty(registry.FindTools(Request(CopilotAgentMode.Chat)));
    }

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

    [Theory]
    [InlineData("{\"device_ref\":\"00000000000000000000000000000001\"}")]
    [InlineData("{\"device_ref\":\"00000000000000000000000000000001\",\"expected_source_instance_id\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"cursor\":\"opaque\"}")]
    [InlineData("{\"device_ref\":\"camera-name\"}")]
    [InlineData("{\"device_ref\":null}")]
    [InlineData("{\"expected_source_instance_id\":\" aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"}")]
    [InlineData("{\"expected_source_instance_id\":null}")]
    [InlineData("{\"max_devices\":0}")]
    [InlineData("{\"max_devices\":101}")]
    [InlineData("{\"max_devices\":1.0}")]
    [InlineData("{\"max_devices\":\"40\"}")]
    [InlineData("{\"cursor\":\"\"}")]
    [InlineData("{\"cursor\":null}")]
    [InlineData("{\"cursor\":7}")]
    [InlineData("{\"cursor\":\"not-a-cursor\"}")]
    [InlineData("{\"code\":\"camera-1\"}")]
    [MemberData(nameof(OversizedCursor))]
    public async Task InvalidProtocolOrScopeArgumentsNeverReachTheSnapshotProvider(string json)
    {
        var calls = 0;
        var dispatcher = Dispatcher(_ => { calls++; throw new InvalidOperationException("Invalid arguments reached device capture."); });
        var arguments = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
        var input = new CopilotAgentToolInput { Arguments = arguments.ToDictionary(item => item.Key, item => (object?)item.Value) };
        var agent = await new CopilotInspectDeviceServicesTool(dispatcher).ExecuteAsync(Request(), input, CancellationToken.None);
        var mcp = await dispatcher.CallAsync("inspect_device_services", arguments, CancellationToken.None);
        Assert.False(agent.Success);
        Assert.False(mcp.Success);
        Assert.Equal(CopilotToolFailureKind.Validation, agent.FailureKind);
        Assert.Equal(agent.FailureKind, mcp.FailureKind);
        Assert.Equal(0, calls);
    }
    public static IEnumerable<object[]> OversizedCursor => [[JsonSerializer.Serialize(new { cursor = new string('c', 4097) })]];

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
            ? new CopilotInspectDeviceServicesTool(dispatcher).ExecuteAsync(Request(), CopilotAgentToolInput.Empty, cancellation.Token)
            : dispatcher.CallAsync("inspect_device_services", null, cancellation.Token);
        var providerToken = await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call);
        Assert.True(providerToken.IsCancellationRequested);
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
