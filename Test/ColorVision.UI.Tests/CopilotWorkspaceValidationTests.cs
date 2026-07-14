using ColorVision.Copilot;
using System;
using System.Collections.Generic;
using System.IO;
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
