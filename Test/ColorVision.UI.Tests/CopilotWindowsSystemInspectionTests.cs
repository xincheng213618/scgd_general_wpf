using ColorVision.Copilot;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.UI.Tests;

public sealed class CopilotWindowsSystemInspectionTests
{
    [Fact]
    public void DefaultProviderReadsCurrentWindowsRuntimeWithoutShell()
    {
        var snapshot = new CopilotWindowsSystemInfoProvider().Read();

        Assert.False(string.IsNullOrWhiteSpace(snapshot.ProductName));
        Assert.False(string.IsNullOrWhiteSpace(snapshot.OsVersion));
        Assert.True(snapshot.BuildNumber > 0);
        Assert.False(string.IsNullOrWhiteSpace(snapshot.OsArchitecture));
        Assert.StartsWith(".NET", snapshot.FrameworkDescription, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FixedDiagnosticReturnsStructuredWindowsVersionWithoutCommandInput()
    {
        var provider = new RecordingProvider(CreateSnapshot());
        var service = new CopilotWindowsSystemInspectionService(provider);

        var result = await service.ExecuteAsync(Request("检查当前 Windows 版本"), CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(1, provider.ReadCount);
        Assert.Contains("Windows 11 Pro 24H2", result.Summary, StringComparison.Ordinal);
        Assert.Contains("build 26100.3775", result.Summary, StringComparison.Ordinal);
        Assert.Contains("product_name: Windows 11 Pro", result.Content, StringComparison.Ordinal);
        Assert.Contains("\"os_architecture\":\"X64\"", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("powershell", result.Content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("command", result.Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RegistryExposesWindowsSystemInspectionOnlyForRelevantIntentOrFollowUp()
    {
        var registry = CopilotToolRegistry.CreateDefault();

        Assert.Contains(
            registry.FindTools(Request("检查当前 Windows 版本")),
            tool => tool.Name == "InspectWindowsSystem");
        foreach (var prompt in new[] { "数据库里现在有多少数据", "解释一下色彩空间" })
        {
            Assert.DoesNotContain(
                registry.FindTools(Request(prompt)),
                tool => tool.Name == "InspectWindowsSystem");
        }

        var followUp = new CopilotAgentRequest
        {
            UserText = "继续",
            Mode = CopilotAgentMode.Auto,
            History =
            [
                new CopilotRequestMessage("user", "检查当前 Windows 版本"),
                new CopilotRequestMessage("assistant", "Windows 版本信息如下。"),
            ],
        };
        Assert.Contains(registry.FindTools(followUp), tool => tool.Name == "InspectWindowsSystem");

        Assert.DoesNotContain(
            registry.FindTools(Request("检查当前 Windows 版本", CopilotAgentMode.Chat)),
            tool => tool.Name == "InspectWindowsSystem");
    }

    [Fact]
    public void ToolPublishesStrictEmptyReadOnlySchema()
    {
        var tool = new CopilotInspectWindowsSystemTool(new CopilotWindowsSystemInspectionService(new RecordingProvider(CreateSnapshot())));

        Assert.Equal(CopilotToolAccess.ReadOnly, tool.Capability.Access);
        Assert.Equal(CopilotToolApprovalMode.Never, tool.Capability.ApprovalMode);
        Assert.True(tool.InputSchema.TryBind(new Dictionary<string, object?>(), out _, out var emptyError), emptyError);
        Assert.False(tool.InputSchema.TryBind(new Dictionary<string, object?> { ["command"] = "systeminfo" }, out _, out var error));
        Assert.Contains("Unknown argument 'command'", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CancellationStopsBeforeReadingSystemInformation()
    {
        var provider = new RecordingProvider(CreateSnapshot());
        var service = new CopilotWindowsSystemInspectionService(provider);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ExecuteAsync(Request("检查系统"), cancellation.Token));

        Assert.Equal(0, provider.ReadCount);
    }

    private static CopilotAgentRequest Request(string userText, CopilotAgentMode mode = CopilotAgentMode.Auto)
    {
        return new CopilotAgentRequest
        {
            UserText = userText,
            Mode = mode,
            History = [new CopilotRequestMessage("assistant", "previous answer")],
        };
    }

    private static CopilotWindowsSystemInfoSnapshot CreateSnapshot()
    {
        return new CopilotWindowsSystemInfoSnapshot(
            "Windows 11 Pro",
            "24H2",
            "Professional",
            "Client",
            "10.0.26100.0",
            26100,
            3775,
            "X64",
            "X64",
            ".NET 10.0.0");
    }

    private sealed class RecordingProvider(CopilotWindowsSystemInfoSnapshot snapshot) : ICopilotWindowsSystemInfoProvider
    {
        public int ReadCount { get; private set; }

        public CopilotWindowsSystemInfoSnapshot Read()
        {
            ReadCount++;
            return snapshot;
        }
    }
}
