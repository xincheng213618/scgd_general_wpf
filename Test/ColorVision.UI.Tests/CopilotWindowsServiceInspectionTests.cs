#pragma warning disable CA1707
using ColorVision.Copilot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;

namespace ColorVision.UI.Tests;

public sealed class CopilotWindowsServiceInspectionTests
{
    [Fact]
    public async Task QueryStatusSortAndLimitAreAppliedToStructuredResults()
    {
        var provider = new RecordingProvider(
        [
            Snapshot("ColorVision.Camera", "ColorVision Camera", "running"),
            Snapshot("ColorVision", "ColorVision Core", "stopped"),
            Snapshot("MySQL80", "MySQL Server 8.0", "running"),
        ]);
        var input = new CopilotAgentToolInput
        {
            Arguments = new Dictionary<string, object?>
            {
                ["query"] = "ColorVision",
                ["status"] = "all",
                ["sortBy"] = "display_name",
                ["limit"] = 1,
            },
        };

        var result = await new CopilotWindowsServiceInspectionService(provider).ExecuteAsync(Request(), input, CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal("Returned 1 of 2 matching installed Windows service(s).", result.Summary);
        Assert.Equal("ColorVision", provider.Query);
        Assert.Equal("all", provider.Status);
        using var json = ReadResultJson(result.Content);
        Assert.Equal(2, json.RootElement.GetProperty("matched_service_count").GetInt32());
        Assert.Equal(1, json.RootElement.GetProperty("returned_service_count").GetInt32());
        Assert.True(json.RootElement.GetProperty("entries_truncated").GetBoolean());
        var service = Assert.Single(json.RootElement.GetProperty("services").EnumerateArray());
        Assert.Equal("ColorVision", service.GetProperty("service_name").GetString());
        Assert.Equal("stopped", service.GetProperty("status").GetString());
    }

    [Fact]
    public async Task PendingFilterAcceptsAllPendingStatesAndDefensivelyFiltersProviderData()
    {
        var provider = new RecordingProvider(
        [
            Snapshot("Starting", "Starting Service", "start_pending"),
            Snapshot("Stopping", "Stopping Service", "stop_pending"),
            Snapshot("Running", "Running Service", "running"),
        ]);
        var input = new CopilotAgentToolInput
        {
            Arguments = new Dictionary<string, object?> { ["status"] = "pending" },
        };

        var result = await new CopilotWindowsServiceInspectionService(provider).ExecuteAsync(Request(), input, CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        using var json = ReadResultJson(result.Content);
        var services = json.RootElement.GetProperty("services").EnumerateArray().ToArray();
        Assert.Equal(2, services.Length);
        Assert.All(services, service => Assert.EndsWith("_pending", service.GetProperty("status").GetString(), StringComparison.Ordinal));
    }

    [Fact]
    public async Task EmptyMatchIsSuccessfulCurrentEvidence()
    {
        var result = await new CopilotWindowsServiceInspectionService(new RecordingProvider([])).ExecuteAsync(
            Request(),
            new CopilotAgentToolInput { Arguments = new Dictionary<string, object?> { ["query"] = "missing-service" } },
            CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal("No installed Windows service matched query 'missing-service'.", result.Summary);
        using var json = ReadResultJson(result.Content);
        Assert.Equal(0, json.RootElement.GetProperty("matched_service_count").GetInt32());
        Assert.Empty(json.RootElement.GetProperty("services").EnumerateArray());
    }

    [Fact]
    public async Task InvalidDirectArgumentsAreRejectedBeforeProviderCapture()
    {
        var provider = new RecordingProvider([]);
        var service = new CopilotWindowsServiceInspectionService(provider);

        var invalidStatus = await ExecuteInvalid(service, "status", "installed");
        var invalidSort = await ExecuteInvalid(service, "sortBy", "cpu");
        var invalidLimit = await ExecuteInvalid(service, "limit", 51);
        var invalidQuery = await ExecuteInvalid(service, "query", new string('x', 257));

        Assert.All([invalidStatus, invalidSort, invalidLimit, invalidQuery], result =>
        {
            Assert.False(result.Success);
            Assert.Equal(CopilotToolFailureKind.Validation, result.FailureKind);
        });
        Assert.Equal(0, provider.CaptureCount);
    }

    [Fact]
    public void RegistryPublishesStrictReadOnlySchemaWithoutCommandText()
    {
        var registry = CopilotToolRegistry.CreateDefault();
        var tool = Assert.Single(registry.FindTools(Request()), candidate => candidate.Name == "InspectWindowsServices");

        Assert.Equal(CopilotToolAccess.ReadOnly, tool.Capability.Access);
        Assert.Equal(CopilotToolRiskLevel.Low, tool.Capability.RiskLevel);
        Assert.Equal(CopilotToolApprovalMode.Never, tool.Capability.ApprovalMode);
        Assert.True(tool.InputSchema.TryBind(new Dictionary<string, object?>(), out _, out var emptyError), emptyError);
        Assert.True(tool.InputSchema.TryBind(new Dictionary<string, object?>
        {
            ["query"] = "MySQL",
            ["status"] = "running",
            ["sortBy"] = "display_name",
            ["limit"] = 5,
        }, out _, out var validError), validError);
        Assert.False(tool.InputSchema.TryBind(new Dictionary<string, object?> { ["command"] = "Get-Service" }, out _, out var commandError));
        Assert.Contains("Unknown argument 'command'", commandError, StringComparison.Ordinal);
        Assert.False(tool.InputSchema.TryBind(new Dictionary<string, object?> { ["status"] = "installed" }, out _, out var statusError));
        Assert.Contains("must be one of", statusError, StringComparison.Ordinal);
        Assert.DoesNotContain(
            registry.FindTools(Request(CopilotAgentMode.Chat)),
            candidate => candidate.Name == "InspectWindowsServices");
    }

    [Fact]
    public void ProductionProviderReadsInstalledWindowsServicesWithoutShell()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var services = new CopilotWindowsServiceProvider().Capture(string.Empty, "all", CancellationToken.None);

        Assert.NotEmpty(services);
        Assert.All(services, service => Assert.False(string.IsNullOrWhiteSpace(service.ServiceName)));
        Assert.Contains(services, service => service.Status is "running" or "stopped" or "paused" or "start_pending" or "stop_pending" or "continue_pending" or "pause_pending" or "unknown");
    }

    [Fact]
    public async Task CancellationStopsBeforeProviderCapture()
    {
        var provider = new RecordingProvider([]);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new CopilotWindowsServiceInspectionService(provider).ExecuteAsync(
            Request(),
            CopilotAgentToolInput.Empty,
            cancellation.Token));

        Assert.Equal(0, provider.CaptureCount);
    }

    private static Task<CopilotToolResult> ExecuteInvalid(CopilotWindowsServiceInspectionService service, string name, object value)
    {
        return service.ExecuteAsync(
            Request(),
            new CopilotAgentToolInput { Arguments = new Dictionary<string, object?> { [name] = value } },
            CancellationToken.None);
    }

    private static CopilotWindowsServiceSnapshot Snapshot(string serviceName, string displayName, string status)
    {
        return new CopilotWindowsServiceSnapshot(
            serviceName,
            displayName,
            status,
            "Win32OwnProcess",
            true,
            false,
            true);
    }

    private static JsonDocument ReadResultJson(string content)
    {
        const string marker = "result_json: ";
        return JsonDocument.Parse(content[(content.IndexOf(marker, StringComparison.Ordinal) + marker.Length)..]);
    }

    private static CopilotAgentRequest Request(CopilotAgentMode mode = CopilotAgentMode.Auto)
    {
        return new CopilotAgentRequest
        {
            UserText = "检查当前 Windows 服务",
            Mode = mode,
            History = [new CopilotRequestMessage("assistant", "previous answer")],
        };
    }

    private sealed class RecordingProvider(IReadOnlyList<CopilotWindowsServiceSnapshot> snapshots) : ICopilotWindowsServiceProvider
    {
        public int CaptureCount { get; private set; }

        public string Query { get; private set; } = string.Empty;

        public string Status { get; private set; } = string.Empty;

        public IReadOnlyList<CopilotWindowsServiceSnapshot> Capture(
            string query,
            string status,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CaptureCount++;
            Query = query;
            Status = status;
            return snapshots;
        }
    }
}
