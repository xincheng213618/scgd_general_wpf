using ColorVision.Copilot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.UI.Tests;

public sealed class CopilotShellCommandTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ColorVision-Copilot-Shell-" + Guid.NewGuid().ToString("N"));
    private readonly string _powershellPath;
    private readonly string _cmdPath;

    public CopilotShellCommandTests()
    {
        Directory.CreateDirectory(_root);
        _powershellPath = CreateFile("powershell.exe");
        _cmdPath = CreateFile("cmd.exe");
    }

    [Fact]
    public async Task AutoShellUsesConfiguredPowerShellForPortInspection()
    {
        var runner = new RecordingRunner(new CopilotShellProcessResult(
            0, false, "LocalPort OwningProcess\r\n6666 1234", string.Empty, TimeSpan.FromMilliseconds(30)));
        var service = CreateService(runner);
        var request = Request("我想要知道6666端口有没有被占用", preferredShell: CopilotShellKind.Auto);

        var result = await service.ExecuteAsync(request, Input(
            "Get-NetTCPConnection -LocalPort 6666 -ErrorAction SilentlyContinue | Select-Object LocalPort,OwningProcess",
            "auto",
            _root,
            45), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains("6666", result.Content, StringComparison.Ordinal);
        var process = Assert.Single(runner.Commands);
        Assert.Equal(CopilotShellKind.PowerShell, process.Shell);
        Assert.Equal(_powershellPath, process.ExecutablePath);
        Assert.Equal(new[]
        {
            "-NoLogo", "-NoProfile", "-NonInteractive", "-Command",
            "[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false); $OutputEncoding = [Console]::OutputEncoding; Get-NetTCPConnection -LocalPort 6666 -ErrorAction SilentlyContinue | Select-Object LocalPort,OwningProcess",
        }, process.Arguments);
        Assert.Equal(_root, process.WorkingDirectory);
        Assert.Equal(TimeSpan.FromSeconds(45), process.Timeout);
    }

    [Fact]
    public async Task AutoShellUsesConfiguredCmdAndPreservesCommandAsOneArgument()
    {
        var runner = new RecordingRunner(new CopilotShellProcessResult(1, false, string.Empty, "not found", TimeSpan.Zero));
        var service = CreateService(runner);
        var request = Request("使用 cmd 检查6666端口", preferredShell: CopilotShellKind.CommandPrompt);

        var result = await service.ExecuteAsync(request, Input("netstat -ano | findstr :6666"), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains("exit code 1", result.Summary, StringComparison.OrdinalIgnoreCase);
        var process = Assert.Single(runner.Commands);
        Assert.Equal(CopilotShellKind.CommandPrompt, process.Shell);
        Assert.Equal(_cmdPath, process.ExecutablePath);
        Assert.Equal(new[] { "/d", "/s", "/c", "netstat -ano | findstr :6666" }, process.Arguments);
    }

    [Fact]
    public async Task RejectsInvalidInputsBeforeStartingProcess()
    {
        var runner = new RecordingRunner(new CopilotShellProcessResult(0, false, string.Empty, string.Empty, TimeSpan.Zero));
        var service = CreateService(runner);
        var missingDirectory = Path.Combine(_root, "missing");
        var results = new[]
        {
            await service.ExecuteAsync(Request("执行命令"), Input(string.Empty), CancellationToken.None),
            await service.ExecuteAsync(Request("执行命令"), Input("whoami", "bash"), CancellationToken.None),
            await service.ExecuteAsync(Request("执行命令"), Input("whoami", timeoutSeconds: 601), CancellationToken.None),
            await service.ExecuteAsync(Request("执行命令"), Input("whoami", workingDirectory: missingDirectory), CancellationToken.None),
        };

        Assert.All(results, result => Assert.False(result.Success));
        Assert.Empty(runner.Commands);
    }

    [Fact]
    public async Task ToolAlwaysRequiresNativeApprovalAndShowsExactCommand()
    {
        var runner = new RecordingRunner(new CopilotShellProcessResult(0, false, string.Empty, string.Empty, TimeSpan.Zero));
        var tool = new CopilotShellCommandTool(CreateService(runner));
        var input = Input("netstat -ano | findstr :6666", "cmd", _root);

        var direct = await tool.ExecuteAsync(Request("检查6666端口"), input, CancellationToken.None);

        Assert.False(direct.Success);
        Assert.Equal(CopilotToolFailureKind.Authorization, direct.FailureKind);
        Assert.Empty(runner.Commands);
        var approval = tool.CreateApprovalPresentation(input);
        Assert.Contains("CMD", approval.Title, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("netstat -ano | findstr :6666", approval.Description, StringComparison.Ordinal);
        Assert.Contains(_root, approval.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ToolPublishesStrictStructuredCommandSchema()
    {
        var schema = new CopilotShellCommandTool().InputSchema.JsonSchema;

        Assert.Equal("object", schema.GetProperty("type").GetString());
        Assert.True(schema.GetProperty("properties").TryGetProperty("command", out var command));
        Assert.Equal("string", command.GetProperty("type").GetString());
        Assert.Contains(schema.GetProperty("required").EnumerateArray(), item => item.GetString() == "command");
        Assert.False(schema.GetProperty("additionalProperties").GetBoolean());
        Assert.True(new CopilotShellCommandTool().InputSchema.TryBind(
            new Dictionary<string, object?> { ["command"] = "Get-ComputerInfo", ["shell"] = "powershell" },
            out _,
            out _));
    }

    [Fact]
    public async Task RealRunnerExecutesPowerShellAndCmdWithoutPython()
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var runner = new CopilotShellProcessRunner();
        var powershell = await runner.RunAsync(new CopilotShellProcessCommand(
            CopilotShellKind.PowerShell,
            Path.Combine(windows, "System32", "WindowsPowerShell", "v1.0", "powershell.exe"),
            ["-NoLogo", "-NoProfile", "-NonInteractive", "-Command", "[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false); Write-Output 'CV_POWERSHELL_OK_中文'"],
            _root,
            TimeSpan.FromSeconds(10)), CancellationToken.None);
        var cmd = await runner.RunAsync(new CopilotShellProcessCommand(
            CopilotShellKind.CommandPrompt,
            Path.Combine(windows, "System32", "cmd.exe"),
            ["/d", "/s", "/c", "echo CV_CMD_OK_中文"],
            _root,
            TimeSpan.FromSeconds(10)), CancellationToken.None);

        Assert.False(powershell.TimedOut);
        Assert.Equal(0, powershell.ExitCode);
        Assert.Contains("CV_POWERSHELL_OK_中文", powershell.StandardOutput, StringComparison.Ordinal);
        Assert.False(cmd.TimedOut);
        Assert.Equal(0, cmd.ExitCode);
        Assert.Contains("CV_CMD_OK_中文", cmd.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void RegistryKeepsShellAvailableForAgentSelectionOutsideChatMode()
    {
        var registry = CopilotToolRegistry.CreateDefault();

        foreach (var prompt in new[] { "我想要知道6666端口有没有被占用", "解释一下畸变校正", "检查当前系统的版本", "继续解释" })
            Assert.Contains(registry.FindTools(Request(prompt, hasHistory: true)), tool => tool.Name == "RunShellCommand");
        Assert.DoesNotContain(registry.FindTools(Request("检查端口", mode: CopilotAgentMode.Chat)), tool => tool.Name == "RunShellCommand");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private CopilotShellCommandService CreateService(ICopilotShellProcessRunner runner)
    {
        return new CopilotShellCommandService(runner, shell => shell == CopilotShellKind.CommandPrompt ? _cmdPath : _powershellPath);
    }

    private CopilotAgentRequest Request(
        string userText,
        CopilotAgentMode mode = CopilotAgentMode.Auto,
        CopilotShellKind preferredShell = CopilotShellKind.Auto,
        bool hasHistory = false)
    {
        return new CopilotAgentRequest
        {
            UserText = userText,
            Profile = new CopilotProfileConfig(),
            Mode = mode,
            PreferredShell = preferredShell,
            History = hasHistory ? [new CopilotRequestMessage("user", "previous request")] : [],
            SearchRootPaths = [_root],
        };
    }

    private static CopilotAgentToolInput Input(
        string command,
        string shell = "auto",
        string? workingDirectory = null,
        int timeoutSeconds = 60)
    {
        var arguments = new Dictionary<string, object?>
        {
            ["command"] = command,
            ["shell"] = shell,
            ["timeoutSeconds"] = timeoutSeconds,
        };
        if (workingDirectory != null)
            arguments["workingDirectory"] = workingDirectory;
        return new CopilotAgentToolInput { Arguments = arguments };
    }

    private string CreateFile(string name)
    {
        var path = Path.Combine(_root, name);
        File.WriteAllBytes(path, []);
        return path;
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
