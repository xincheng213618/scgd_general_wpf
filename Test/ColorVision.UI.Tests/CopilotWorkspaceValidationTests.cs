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

public sealed class CopilotWorkspaceValidationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ColorVision-Copilot-Validation-" + Guid.NewGuid().ToString("N"));
    private readonly string _dotnetPath;

    public CopilotWorkspaceValidationTests()
    {
        Directory.CreateDirectory(_root);
        _dotnetPath = Path.Combine(_root, "trusted-dotnet.exe");
        File.WriteAllBytes(_dotnetPath, []);
    }

    [Fact]
    public async Task ApprovedBuildUsesOnlyFixedDotnetArguments()
    {
        var target = CreateFile("ColorVision.sln", string.Empty);
        var runner = new RecordingRunner(new CopilotWorkspaceValidationProcessResult(
            0, false, "Build succeeded.", string.Empty, TimeSpan.FromSeconds(2)));
        var service = CreateService(runner);

        var result = await service.ExecuteAsync(CreateRequest("请构建工作区"), CreateInput(
            "build", target, "Release", 42), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains("outcome: passed", result.Content, StringComparison.Ordinal);
        var command = Assert.Single(runner.Commands);
        Assert.Equal(Path.GetFullPath(_dotnetPath), command.ExecutablePath);
        Assert.Equal(_root, command.WorkingDirectory);
        Assert.Equal(TimeSpan.FromSeconds(42), command.Timeout);
        Assert.Equal(new[]
        {
            "build", target, "--configuration", "Release", "--no-restore", "--nologo", "--verbosity:minimal",
        }, command.Arguments);
        Assert.DoesNotContain(command.Arguments, argument => argument.Contains('&', StringComparison.Ordinal));
    }

    [Fact]
    public async Task NonzeroExitIsCompletedValidationEvidence()
    {
        var target = CreateFile("Project.csproj", "<Project />");
        var runner = new RecordingRunner(new CopilotWorkspaceValidationProcessResult(
            1, false, string.Empty, "error CS1002", TimeSpan.FromMilliseconds(25)));

        var result = await CreateService(runner).ExecuteAsync(
            CreateRequest("请运行测试"), CreateInput("test", target), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(CopilotToolFailureKind.None, result.FailureKind);
        Assert.Contains("exit code 1", result.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("outcome: failed", result.Content, StringComparison.Ordinal);
        Assert.Contains("error CS1002", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RejectsUnsupportedInjectedAndOutOfScopeInputsBeforeStartingProcess()
    {
        var target = CreateFile("Project.csproj", "<Project />");
        var textTarget = CreateFile("notes.txt", "notes");
        var outsideTarget = Path.Combine(Path.GetTempPath(), "ColorVision-Outside-" + Guid.NewGuid().ToString("N") + ".csproj");
        File.WriteAllText(outsideTarget, "<Project />");
        var runner = new RecordingRunner(new CopilotWorkspaceValidationProcessResult(0, false, string.Empty, string.Empty, TimeSpan.Zero));
        var service = CreateService(runner);
        try
        {
            var results = new[]
            {
                await service.ExecuteAsync(CreateRequest("验证"), CreateInput("build & whoami", target), CancellationToken.None),
                await service.ExecuteAsync(CreateRequest("验证"), CreateInput("build", target, "release"), CancellationToken.None),
                await service.ExecuteAsync(CreateRequest("验证"), CreateInput("test", target, timeoutSeconds: 601), CancellationToken.None),
                await service.ExecuteAsync(CreateRequest("验证"), CreateInput("build", textTarget), CancellationToken.None),
                await service.ExecuteAsync(CreateRequest("验证"), CreateInput("build", outsideTarget), CancellationToken.None),
            };

            Assert.All(results, result => Assert.False(result.Success));
            Assert.Empty(runner.Commands);
        }
        finally
        {
            File.Delete(outsideTarget);
        }
    }

    [Fact]
    public async Task ToolRequiresNativeApprovalAndSchemaRejectsInvalidValues()
    {
        var target = CreateFile("Project.csproj", "<Project />");
        var runner = new RecordingRunner(new CopilotWorkspaceValidationProcessResult(0, false, string.Empty, string.Empty, TimeSpan.Zero));
        var tool = new CopilotWorkspaceValidationTool(CreateService(runner));
        var request = CreateRequest("请构建项目");
        var input = CreateInput("build", target);

        var direct = await tool.ExecuteAsync(request, input, CancellationToken.None);

        Assert.False(direct.Success);
        Assert.Equal(CopilotToolFailureKind.Authorization, direct.FailureKind);
        Assert.Empty(runner.Commands);
        Assert.Contains(target, tool.CreateApprovalPresentation(input).Description, StringComparison.OrdinalIgnoreCase);
        Assert.False(tool.InputSchema.TryBind(new Dictionary<string, object?>
        {
            ["task"] = "build & whoami",
            ["path"] = target,
        }, out _, out var taskError));
        Assert.Contains("must be one of", taskError, StringComparison.OrdinalIgnoreCase);
        Assert.False(tool.InputSchema.TryBind(new Dictionary<string, object?>
        {
            ["task"] = "build",
            ["path"] = target,
            ["timeoutSeconds"] = 601,
        }, out _, out var timeoutError));
        Assert.Contains("at most 600", timeoutError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RealRunnerTerminatesBackgroundChildWhenValidationRootCompletes()
    {
        var result = await RunPowerShellAsync(BuildBackgroundChildCommand(), TimeSpan.FromSeconds(10), CancellationToken.None);
        var processId = ReadChildProcessId(result.StandardOutput);

        try
        {
            Assert.False(result.TimedOut);
            Assert.Equal(0, result.ExitCode);
            Assert.True(await WaitForProcessExitAsync(processId), $"Background validation child process {processId} was left running.");
        }
        finally
        {
            TryKillProcess(processId);
        }
    }

    [Fact]
    public async Task RealRunnerTimeoutTerminatesEntireValidationProcessJob()
    {
        var result = await RunPowerShellAsync(
            BuildBackgroundChildCommand("Start-Sleep -Seconds 30"),
            TimeSpan.FromSeconds(2),
            CancellationToken.None);
        var processId = ReadChildProcessId(result.StandardOutput);

        try
        {
            Assert.True(result.TimedOut);
            Assert.Equal(-1, result.ExitCode);
            Assert.True(await WaitForProcessExitAsync(processId), $"Timed-out validation child process {processId} was left running.");
        }
        finally
        {
            TryKillProcess(processId);
        }
    }

    [Fact]
    public async Task RealRunnerCancellationTerminatesEntireValidationProcessJob()
    {
        var processIdFile = Path.Combine(_root, "cancelled-validation-child.pid");
        var escapedProcessIdFile = processIdFile.Replace("'", "''", StringComparison.Ordinal);
        var command = BuildBackgroundChildCommand(
            $"Set-Content -LiteralPath '{escapedProcessIdFile}' -Value $child.Id -Encoding ASCII; Start-Sleep -Seconds 30");
        using var cancellationSource = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => RunPowerShellAsync(
            command,
            TimeSpan.FromSeconds(10),
            cancellationSource.Token));

        var processId = await ReadProcessIdFileAsync(processIdFile);
        try
        {
            Assert.True(await WaitForProcessExitAsync(processId), $"Cancelled validation child process {processId} was left running.");
        }
        finally
        {
            TryKillProcess(processId);
        }
    }

    [Fact]
    public void RegistryExposesValidationForExplicitValidationAndWorkspaceWritesOnly()
    {
        var registry = CopilotToolRegistry.CreateDefault();

        Assert.Contains(registry.FindTools(CreateRequest("请运行测试")), tool => tool.Name == "RunWorkspaceValidation");
        Assert.Contains(registry.FindTools(CreateRequest("请修改 Sample.cs")), tool => tool.Name == "RunWorkspaceValidation");
        Assert.Contains(registry.FindTools(CreateRequest("请创建文件 Sample.cs")), tool => tool.Name == "RunWorkspaceValidation");
        Assert.DoesNotContain(registry.FindTools(CreateRequest("解释一下怎么构建项目")), tool => tool.Name == "RunWorkspaceValidation");
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

    private CopilotWorkspaceValidationService CreateService(ICopilotWorkspaceValidationRunner runner)
    {
        return new CopilotWorkspaceValidationService(runner, () => _dotnetPath);
    }

    private CopilotAgentRequest CreateRequest(string userText)
    {
        return new CopilotAgentRequest
        {
            UserText = userText,
            Mode = CopilotAgentMode.Auto,
            WritableLocalRootPaths = [_root],
        };
    }

    private static CopilotAgentToolInput CreateInput(
        string task,
        string path,
        string configuration = "Debug",
        int timeoutSeconds = 300)
    {
        return new CopilotAgentToolInput
        {
            Path = path,
            Arguments = new Dictionary<string, object?>
            {
                ["task"] = task,
                ["path"] = path,
                ["configuration"] = configuration,
                ["timeoutSeconds"] = timeoutSeconds,
            },
        };
    }

    private string CreateFile(string relativePath, string content)
    {
        var path = Path.Combine(_root, relativePath);
        File.WriteAllText(path, content);
        return path;
    }

    private Task<CopilotWorkspaceValidationProcessResult> RunPowerShellAsync(
        string command,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var executable = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "System32",
            "WindowsPowerShell",
            "v1.0",
            "powershell.exe");
        return new CopilotWorkspaceValidationProcessRunner().RunAsync(
            new CopilotWorkspaceValidationCommand(
                executable,
                ["-NoLogo", "-NoProfile", "-NonInteractive", "-Command", command],
                _root,
                timeout),
            cancellationToken);
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
        throw new InvalidOperationException("The cancelled validation process did not publish its child process ID.");
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

    private sealed class RecordingRunner(CopilotWorkspaceValidationProcessResult result) : ICopilotWorkspaceValidationRunner
    {
        public List<CopilotWorkspaceValidationCommand> Commands { get; } = [];

        public Task<CopilotWorkspaceValidationProcessResult> RunAsync(
            CopilotWorkspaceValidationCommand command,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Commands.Add(command);
            return Task.FromResult(result);
        }
    }
}
