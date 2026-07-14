#pragma warning disable CA1707
using ColorVision.Copilot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.UI.Tests;

public sealed class CopilotWindowsProcessInspectionTests
{
    [Fact]
    public async Task FixedProviderResultIsFilteredSortedAndBoundedStructurally()
    {
        var provider = new RecordingProvider(
        [
            Snapshot(30, "worker", cpu: 40, memory: 3_000),
            Snapshot(10, "ColorVision", cpu: 5, memory: 9_000),
            Snapshot(20, "helper", cpu: 20, memory: 6_000),
        ]);
        var service = new CopilotWindowsProcessInspectionService(provider);
        var input = new CopilotAgentToolInput
        {
            Arguments = new Dictionary<string, object?> { ["sortBy"] = "memory", ["limit"] = 2 },
        };

        var result = await service.ExecuteAsync(Request(), input, CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal("Returned 2 running Windows process(es) sorted by working-set memory.", result.Summary);
        using var json = ReadResultJson(result.Content);
        Assert.Equal("memory", json.RootElement.GetProperty("sort_by").GetString());
        Assert.Equal(3, json.RootElement.GetProperty("matched_process_count").GetInt32());
        Assert.Equal(2, json.RootElement.GetProperty("returned_process_count").GetInt32());
        Assert.True(json.RootElement.GetProperty("entries_truncated").GetBoolean());
        var processes = json.RootElement.GetProperty("processes");
        Assert.Equal(10, processes[0].GetProperty("process_id").GetInt32());
        Assert.Equal(20, processes[1].GetProperty("process_id").GetInt32());
        Assert.Equal(1, provider.CaptureCount);
        Assert.Null(provider.ProcessId);
        Assert.Equal(string.Empty, provider.ProcessName);
    }

    [Fact]
    public async Task ExactPidAndExeNameArePassedAndDefensivelyFiltered()
    {
        var provider = new RecordingProvider(
        [
            Snapshot(42, "ColorVision", cpu: 1, memory: 100),
            Snapshot(43, "ColorVision", cpu: 2, memory: 200),
            Snapshot(42, "other", cpu: 3, memory: 300),
        ]);
        var input = new CopilotAgentToolInput
        {
            Arguments = new Dictionary<string, object?> { ["processId"] = 42, ["name"] = "ColorVision.exe" },
        };

        var result = await new CopilotWindowsProcessInspectionService(provider).ExecuteAsync(Request(), input, CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(42, provider.ProcessId);
        Assert.Equal("ColorVision.exe", provider.ProcessName);
        using var json = ReadResultJson(result.Content);
        Assert.Equal(1, json.RootElement.GetProperty("matched_process_count").GetInt32());
        Assert.Equal(42, json.RootElement.GetProperty("processes")[0].GetProperty("process_id").GetInt32());
        Assert.Equal("ColorVision", json.RootElement.GetProperty("processes")[0].GetProperty("process_name").GetString());
    }

    [Fact]
    public async Task EmptyMatchIsSuccessfulEvidenceThatProcessIsNotRunning()
    {
        var provider = new RecordingProvider([]);
        var result = await new CopilotWindowsProcessInspectionService(provider).ExecuteAsync(
            Request(),
            new CopilotAgentToolInput { Arguments = new Dictionary<string, object?> { ["name"] = "missing.exe" } },
            CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal("No running Windows process matched name missing.exe.", result.Summary);
        using var json = ReadResultJson(result.Content);
        Assert.Equal(0, json.RootElement.GetProperty("matched_process_count").GetInt32());
        Assert.Empty(json.RootElement.GetProperty("processes").EnumerateArray());
    }

    [Fact]
    public async Task ServiceRejectsInvalidDirectArgumentsBeforeSampling()
    {
        var provider = new RecordingProvider([]);
        var service = new CopilotWindowsProcessInspectionService(provider);

        var invalidPid = await service.ExecuteAsync(
            Request(),
            new CopilotAgentToolInput { Arguments = new Dictionary<string, object?> { ["processId"] = 0 } },
            CancellationToken.None);
        var invalidSort = await service.ExecuteAsync(
            Request(),
            new CopilotAgentToolInput { Arguments = new Dictionary<string, object?> { ["sortBy"] = "lifetime_cpu" } },
            CancellationToken.None);
        var invalidLimit = await service.ExecuteAsync(
            Request(),
            new CopilotAgentToolInput { Arguments = new Dictionary<string, object?> { ["limit"] = 26 } },
            CancellationToken.None);
        var invalidName = await service.ExecuteAsync(
            Request(),
            new CopilotAgentToolInput { Arguments = new Dictionary<string, object?> { ["name"] = ".exe" } },
            CancellationToken.None);

        Assert.All([invalidPid, invalidSort, invalidLimit, invalidName], result =>
        {
            Assert.False(result.Success);
            Assert.Equal(CopilotToolFailureKind.Validation, result.FailureKind);
        });
        Assert.Equal(0, provider.CaptureCount);
    }

    [Fact]
    public async Task RegistryPublishesStrictReadOnlyProcessSchema()
    {
        var registry = CopilotToolRegistry.CreateDefault();
        var tool = Assert.Single(registry.FindTools(Request()), candidate => candidate.Name == "InspectWindowsProcesses");

        Assert.Equal(CopilotToolAccess.ReadOnly, tool.Capability.Access);
        Assert.Equal(CopilotToolRiskLevel.Low, tool.Capability.RiskLevel);
        Assert.Equal(CopilotToolApprovalMode.Never, tool.Capability.ApprovalMode);
        Assert.True(tool.InputSchema.TryBind(new Dictionary<string, object?>(), out _, out var emptyError), emptyError);
        Assert.True(tool.InputSchema.TryBind(new Dictionary<string, object?>
        {
            ["processId"] = 42,
            ["name"] = "ColorVision.exe",
            ["sortBy"] = "cpu",
            ["limit"] = 5,
        }, out _, out var validError), validError);
        Assert.False(tool.InputSchema.TryBind(new Dictionary<string, object?> { ["command"] = "Get-Process" }, out _, out var commandError));
        Assert.Contains("Unknown argument 'command'", commandError, StringComparison.Ordinal);
        Assert.False(tool.InputSchema.TryBind(new Dictionary<string, object?> { ["sortBy"] = "all" }, out _, out var sortError));
        Assert.Contains("must be one of", sortError, StringComparison.Ordinal);
        Assert.DoesNotContain(
            registry.FindTools(Request(CopilotAgentMode.Chat)),
            candidate => candidate.Name == "InspectWindowsProcesses");

        var result = await tool.ExecuteAsync(Request(), CopilotAgentToolInput.Empty, CancellationToken.None);
        Assert.True(result.Success, result.ErrorMessage);
    }

    [Fact]
    public async Task ProductionProviderCapturesTheCurrentTestProcessWithoutShell()
    {
        if (!OperatingSystem.IsWindows())
            return;

        using var current = Process.GetCurrentProcess();
        var processId = current.Id;
        var result = await new CopilotWindowsProcessInspectionService().ExecuteAsync(
            Request(),
            new CopilotAgentToolInput { Arguments = new Dictionary<string, object?> { ["processId"] = processId } },
            CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        using var json = ReadResultJson(result.Content);
        Assert.Equal(1, json.RootElement.GetProperty("matched_process_count").GetInt32());
        var process = Assert.Single(json.RootElement.GetProperty("processes").EnumerateArray());
        Assert.Equal(processId, process.GetProperty("process_id").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(process.GetProperty("process_name").GetString()));
        Assert.Equal(CopilotWindowsProcessProvider.SampleWindowMilliseconds, json.RootElement.GetProperty("sample_window_ms").GetInt32());
    }

    private static CopilotWindowsProcessSnapshot Snapshot(int processId, string name, double cpu, long memory)
    {
        return new CopilotWindowsProcessSnapshot(
            processId,
            name,
            cpu,
            memory,
            memory / 2,
            3,
            1,
            "2026-07-15T00:00:00.0000000Z",
            $"C:\\Apps\\{name}.exe");
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
            UserText = "检查当前进程",
            Mode = mode,
            History = [new CopilotRequestMessage("assistant", "previous answer")],
        };
    }

    private sealed class RecordingProvider(IReadOnlyList<CopilotWindowsProcessSnapshot> snapshots) : ICopilotWindowsProcessProvider
    {
        public int CaptureCount { get; private set; }

        public int? ProcessId { get; private set; }

        public string ProcessName { get; private set; } = string.Empty;

        public Task<IReadOnlyList<CopilotWindowsProcessSnapshot>> CaptureAsync(
            int? processId,
            string processName,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CaptureCount++;
            ProcessId = processId;
            ProcessName = processName;
            return Task.FromResult(snapshots);
        }
    }
}
