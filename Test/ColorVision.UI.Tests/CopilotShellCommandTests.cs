using ColorVision.Copilot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
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
            0, false, "LocalPort OwningProcess\r\n6666 1234", string.Empty, TimeSpan.FromMilliseconds(30))
        {
            ProcessTreeContained = true,
        });
        var service = CreateService(runner);
        var request = Request("我想要知道6666端口有没有被占用", preferredShell: CopilotShellKind.Auto);

        var result = await service.ExecuteAsync(request, Input(
            "Get-NetTCPConnection -LocalPort 6666 -ErrorAction SilentlyContinue | Select-Object LocalPort,OwningProcess",
            "auto",
            _root,
            45), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains("6666", result.Content, StringComparison.Ordinal);
        Assert.Contains("process_tree: windows_job_object", result.Content, StringComparison.Ordinal);
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
    public async Task RealRunnerAppliesExplicitEnvironmentOverrides()
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var inheritedName = "COLORVISION_COPILOT_INHERITED_" + Guid.NewGuid().ToString("N");
        var explicitName = "COLORVISION_COPILOT_EXPLICIT_" + Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable(inheritedName, "parent-value");
        try
        {
            var commandText = $"Write-Output ('removed=' + [string]::IsNullOrEmpty($env:{inheritedName})); Write-Output ('set=' + $env:{explicitName})";
            var result = await new CopilotShellProcessRunner().RunAsync(new CopilotShellProcessCommand(
                CopilotShellKind.PowerShell,
                Path.Combine(windows, "System32", "WindowsPowerShell", "v1.0", "powershell.exe"),
                ["-NoLogo", "-NoProfile", "-NonInteractive", "-Command", commandText],
                _root,
                TimeSpan.FromSeconds(10))
            {
                EnvironmentOverrides = new Dictionary<string, string?>
                {
                    [inheritedName] = null,
                    [explicitName] = "isolated-value",
                },
            }, CancellationToken.None);

            Assert.False(result.TimedOut);
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("removed=True", result.StandardOutput, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("set=isolated-value", result.StandardOutput, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable(inheritedName, null);
        }
    }

    [Fact]
    public async Task RealRunnerTerminatesBackgroundChildWhenRootShellCompletes()
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var runner = new CopilotShellProcessRunner();
        var result = await runner.RunAsync(new CopilotShellProcessCommand(
            CopilotShellKind.PowerShell,
            Path.Combine(windows, "System32", "WindowsPowerShell", "v1.0", "powershell.exe"),
            ["-NoLogo", "-NoProfile", "-NonInteractive", "-Command", BuildBackgroundChildCommand()],
            _root,
            TimeSpan.FromSeconds(10)), CancellationToken.None);
        var processId = ReadChildProcessId(result.StandardOutput);

        try
        {
            Assert.True(result.ProcessTreeContained);
            Assert.False(result.TimedOut);
            Assert.True(await WaitForProcessExitAsync(processId), $"Background child process {processId} was left running.");
        }
        finally
        {
            TryKillProcess(processId);
        }
    }

    [Fact]
    public async Task RealRunnerTimeoutTerminatesEntireProcessJob()
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var runner = new CopilotShellProcessRunner();
        var result = await runner.RunAsync(new CopilotShellProcessCommand(
            CopilotShellKind.PowerShell,
            Path.Combine(windows, "System32", "WindowsPowerShell", "v1.0", "powershell.exe"),
            ["-NoLogo", "-NoProfile", "-NonInteractive", "-Command", BuildBackgroundChildCommand("Start-Sleep -Seconds 30")],
            _root,
            TimeSpan.FromSeconds(2)), CancellationToken.None);
        var processId = ReadChildProcessId(result.StandardOutput);

        try
        {
            Assert.True(result.ProcessTreeContained);
            Assert.True(result.TimedOut);
            Assert.True(await WaitForProcessExitAsync(processId), $"Timed-out child process {processId} was left running.");
        }
        finally
        {
            TryKillProcess(processId);
        }
    }

    [Fact]
    public async Task RealRunnerCancellationTerminatesEntireProcessJob()
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var processIdFile = Path.Combine(_root, "cancelled-child.pid");
        var escapedProcessIdFile = processIdFile.Replace("'", "''", StringComparison.Ordinal);
        var command = BuildBackgroundChildCommand($"Set-Content -LiteralPath '{escapedProcessIdFile}' -Value $child.Id -Encoding ASCII; Start-Sleep -Seconds 30");
        var runner = new CopilotShellProcessRunner();
        using var cancellationSource = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runner.RunAsync(new CopilotShellProcessCommand(
            CopilotShellKind.PowerShell,
            Path.Combine(windows, "System32", "WindowsPowerShell", "v1.0", "powershell.exe"),
            ["-NoLogo", "-NoProfile", "-NonInteractive", "-Command", command],
            _root,
            TimeSpan.FromSeconds(10)), cancellationSource.Token));

        var processId = await ReadProcessIdFileAsync(processIdFile);
        try
        {
            Assert.True(await WaitForProcessExitAsync(processId), $"Cancelled child process {processId} was left running.");
        }
        finally
        {
            TryKillProcess(processId);
        }
    }

    [Fact]
    public void RegistryExposesShellOnlyForExplicitShellIntentOutsideChatMode()
    {
        var registry = CopilotToolRegistry.CreateDefault();

        foreach (var prompt in new[] { "运行 PowerShell 命令", "用 CMD 执行命令", "在终端运行脚本" })
            Assert.Contains(registry.FindTools(Request(prompt, hasHistory: true)), tool => tool.Name == "RunShellCommand");
        foreach (var prompt in new[] { "我想要知道6666端口有没有被占用", "解释一下畸变校正", "检查当前系统的版本", "继续解释" })
            Assert.DoesNotContain(registry.FindTools(Request(prompt, hasHistory: true)), tool => tool.Name == "RunShellCommand");
        Assert.DoesNotContain(registry.FindTools(Request("PowerShell 是什么", hasHistory: true)), tool => tool.Name == "RunShellCommand");
        Assert.DoesNotContain(registry.FindTools(Request("检查端口", mode: CopilotAgentMode.Chat)), tool => tool.Name == "RunShellCommand");
    }

    public void Dispose()
    {
        for (var attempt = 0; attempt < 100 && Directory.Exists(_root); attempt++)
        {
            try
            {
                Directory.Delete(_root, recursive: true);
                return;
            }
            catch (Exception ex) when (attempt < 99 && ex is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(50);
            }
        }

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

    private static string BuildBackgroundChildCommand(string? continuation = null)
    {
        return "Start-Sleep -Milliseconds 200; "
            + "$child = Start-Process -FilePath $env:ComSpec -ArgumentList '/d /s /c ping 127.0.0.1 -n 31 > nul' -WindowStyle Hidden -PassThru; "
            + "Write-Output ('CV_CHILD_PID=' + $child.Id); "
            + (continuation ?? string.Empty);
    }

    private static int ReadChildProcessId(string output)
    {
        const string prefix = "CV_CHILD_PID=";
        var line = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .First(item => item.StartsWith(prefix, StringComparison.Ordinal));
        return int.Parse(line[prefix.Length..], NumberStyles.None, CultureInfo.InvariantCulture);
    }

    private static async Task<int> ReadProcessIdFileAsync(string path)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (File.Exists(path))
            {
                var text = await File.ReadAllTextAsync(path);
                if (int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var processId))
                    return processId;
            }
            await Task.Delay(50);
        }
        throw new InvalidOperationException("The cancelled shell did not publish its child process ID.");
    }

    private static async Task<bool> WaitForProcessExitAsync(int processId)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            try
            {
                using var process = Process.GetProcessById(processId);
                if (process.HasExited)
                    return true;
            }
            catch (ArgumentException)
            {
                return true;
            }
            await Task.Delay(50);
        }
        return false;
    }

    private static void TryKillProcess(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
        }
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
