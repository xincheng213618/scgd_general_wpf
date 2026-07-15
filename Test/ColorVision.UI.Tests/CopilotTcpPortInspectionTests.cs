using ColorVision.Copilot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.UI.Tests;

public sealed class CopilotTcpPortInspectionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ColorVision-Copilot-Port-" + Guid.NewGuid().ToString("N"));
    private readonly string _powershellPath;

    public CopilotTcpPortInspectionTests()
    {
        Directory.CreateDirectory(_root);
        _powershellPath = Path.Combine(_root, "powershell.exe");
        File.WriteAllBytes(_powershellPath, []);
    }

    [Fact]
    public async Task FixedDiagnosticReturnsOccupiedPortAndOwningProcessWithoutCommandInput()
    {
        var runner = new RecordingRunner(new CopilotShellProcessResult(
            0,
            false,
            "{\"port\":6666,\"occupied\":true,\"binding_count\":1,\"truncated\":false,\"bindings\":[{\"local_address\":\"0.0.0.0\",\"local_port\":6666,\"remote_address\":\"0.0.0.0\",\"remote_port\":0,\"state\":\"Listen\",\"process_id\":4321,\"process_name\":\"ColorVision\"}]}",
            string.Empty,
            TimeSpan.FromMilliseconds(25)));
        var service = CreateService(runner);

        var result = await service.ExecuteAsync(
            Request("我想要知道6666端口有没有被占用", CopilotShellKind.CommandPrompt),
            PortInput(6666),
            CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Contains("currently in use", result.Summary, StringComparison.Ordinal);
        Assert.Contains("occupied: true", result.Content, StringComparison.Ordinal);
        Assert.Contains("\"process_name\":\"ColorVision\"", result.Content, StringComparison.Ordinal);
        var command = Assert.Single(runner.Commands);
        Assert.Equal(CopilotShellKind.PowerShell, command.Shell);
        Assert.Equal(_powershellPath, command.ExecutablePath);
        Assert.Contains("Get-NetTCPConnection -LocalPort 6666", command.Arguments[^1], StringComparison.Ordinal);
        Assert.Contains("Get-Process -Id $_.OwningProcess", command.Arguments[^1], StringComparison.Ordinal);
        Assert.DoesNotContain("cmd.exe", command.Arguments[^1], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FixedDiagnosticReportsUnusedPort()
    {
        var runner = new RecordingRunner(new CopilotShellProcessResult(
            0,
            false,
            "{\"port\":60000,\"occupied\":false,\"binding_count\":0,\"truncated\":false,\"bindings\":[]}",
            string.Empty,
            TimeSpan.Zero));

        var result = await CreateService(runner).ExecuteAsync(
            Request("60000端口是否被占用"),
            PortInput(60000),
            CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Contains("not currently in use", result.Summary, StringComparison.Ordinal);
        Assert.Contains("occupied: false", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NonzeroDiagnosticExitIsReportedAsFailure()
    {
        var runner = new RecordingRunner(new CopilotShellProcessResult(
            1,
            false,
            string.Empty,
            "Get-NetTCPConnection is unavailable.",
            TimeSpan.Zero));

        var result = await CreateService(runner).ExecuteAsync(
            Request("检查端口 6666 是否占用"),
            PortInput(6666),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(CopilotToolFailureKind.Internal, result.FailureKind);
        Assert.Contains("exited with code 1", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    public async Task InvalidPortIsRejectedBeforeStartingPowerShell(int port)
    {
        var runner = new RecordingRunner(new CopilotShellProcessResult(0, false, string.Empty, string.Empty, TimeSpan.Zero));

        var result = await CreateService(runner).ExecuteAsync(
            Request("检查端口"),
            PortInput(port),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(CopilotToolFailureKind.Validation, result.FailureKind);
        Assert.Empty(runner.Commands);
    }

    [Fact]
    public void RegistryExposesStructuredPortInspectionWithoutUnrelatedShellExecution()
    {
        var registry = CopilotToolRegistry.CreateDefault();

        foreach (var prompt in new[]
        {
            "我想要知道6666端口有没有被占用",
            "检查6666端口和7777端口",
            "解释一下TCP端口",
        })
        {
            var tools = registry.FindTools(Request(prompt, hasHistory: true));
            Assert.Contains(tools, tool => tool.Name == "InspectTcpPort");
            Assert.DoesNotContain(tools, tool => tool.Name == "RunShellCommand");
        }

        var explicitShellTools = registry.FindTools(Request("用 CMD 检查 6666 端口", hasHistory: true));
        Assert.Contains(explicitShellTools, tool => tool.Name == "InspectTcpPort");
        Assert.Contains(explicitShellTools, tool => tool.Name == "RunShellCommand");
    }

    [Fact]
    public void RegistryPublishesDistinctPortAndShellSafetyContracts()
    {
        var registry = CopilotToolRegistry.CreateDefault();
        var inspectionTools = registry.FindTools(Request("我想要知道6666端口有没有被占用", hasHistory: true));
        var shellTools = registry.FindTools(Request("运行 PowerShell 命令检查网络", hasHistory: true));
        var inspection = Assert.Single(inspectionTools, tool => tool.Name == "InspectTcpPort");
        var shell = Assert.Single(shellTools, tool => tool.Name == "RunShellCommand");

        Assert.Equal(CopilotToolAccess.ReadOnly, inspection.Capability.Access);
        Assert.Equal(CopilotToolApprovalMode.Never, inspection.Capability.ApprovalMode);
        Assert.Equal(CopilotToolAccess.Write, shell.Capability.Access);
        Assert.Equal(CopilotToolApprovalMode.Always, shell.Capability.ApprovalMode);
        Assert.DoesNotContain(registry.FindTools(Request("检查6666端口", mode: CopilotAgentMode.Chat)), tool => tool.Name == "InspectTcpPort");
        Assert.DoesNotContain(registry.FindTools(Request("检查6666端口", mode: CopilotAgentMode.Chat)), tool => tool.Name == "RunShellCommand");
    }

    [Theory]
    [InlineData("现在呢")]
    [InlineData("再检查一遍")]
    [InlineData("check again")]
    public void RegistryRetainsOnlyPortInspectionForExplicitFollowUp(string followUp)
    {
        var request = new CopilotAgentRequest
        {
            UserText = followUp,
            Mode = CopilotAgentMode.Auto,
            SearchRootPaths = [_root],
            History =
            [
                new CopilotRequestMessage("user", "我想要知道6666端口有没有被占用"),
                new CopilotRequestMessage("assistant", "端口 6666 当前未被占用。"),
            ],
        };

        var tools = CopilotToolRegistry.CreateDefault().FindTools(request);

        Assert.Contains(tools, tool => tool.Name == "InspectTcpPort");
        Assert.DoesNotContain(tools, tool => tool.Name is "RunShellCommand" or "QueryDatabaseSql" or "ExecuteDatabaseSql" or "GetRecentLog");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private CopilotTcpPortInspectionService CreateService(ICopilotShellProcessRunner runner)
    {
        return new CopilotTcpPortInspectionService(new CopilotShellCommandService(runner, _ => _powershellPath));
    }

    private CopilotAgentRequest Request(
        string userText,
        CopilotShellKind preferredShell = CopilotShellKind.Auto,
        bool hasHistory = false,
        CopilotAgentMode mode = CopilotAgentMode.Auto)
    {
        return new CopilotAgentRequest
        {
            UserText = userText,
            Mode = mode,
            PreferredShell = preferredShell,
            SearchRootPaths = [_root],
            History = hasHistory
                ? [new CopilotRequestMessage("assistant", "previous answer")]
                : Array.Empty<CopilotRequestMessage>(),
        };
    }

    private static CopilotAgentToolInput PortInput(int port)
    {
        return new CopilotAgentToolInput
        {
            Arguments = new Dictionary<string, object?> { ["port"] = port },
        };
    }

    private sealed class RecordingRunner(CopilotShellProcessResult result) : ICopilotShellProcessRunner
    {
        public List<CopilotShellProcessCommand> Commands { get; } = [];

        public Task<CopilotShellProcessResult> RunAsync(CopilotShellProcessCommand command, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Commands.Add(command);
            return Task.FromResult(result);
        }
    }
}
