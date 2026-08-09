using ColorVision.Copilot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotWorkspaceValidationEnvironmentTests : IDisposable
{
    private readonly string _root = Path.GetFullPath(Path.Combine(
        Path.GetTempPath(),
        "ColorVisionCopilotWorkspaceValidationEnvironmentTests",
        Guid.NewGuid().ToString("N")));

    [Fact]
    public async Task ServicePassesTheFrozenRequestEnvironmentToValidation()
    {
        Directory.CreateDirectory(_root);
        var projectPath = Path.Combine(_root, "Validation.csproj");
        var dotnetPath = Path.Combine(_root, "dotnet.exe");
        File.WriteAllText(projectPath, "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        File.WriteAllText(dotnetPath, string.Empty);
        var runner = new RecordingRunner();
        var service = new CopilotWorkspaceValidationService(runner, () => dotnetPath);
        var request = new CopilotAgentRequest
        {
            ConversationId = "validation-thread",
            WritableLocalRootPaths = [_root],
            CodexShellEnvironmentPolicy = new CopilotCodexShellEnvironmentPolicy
            {
                Inherit = CopilotCodexShellEnvironmentInherit.None,
                Set = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["CV_VALIDATION_ENV_TEST"] = "frozen",
                    ["OPENAI_IDENTITY_TOKEN_FILE"] = "must-not-reach-child",
                },
            },
        };

        var result = await service.ExecuteAsync(
            request,
            new CopilotAgentToolInput
            {
                Path = projectPath,
                Arguments = new Dictionary<string, object?> { ["task"] = "build" },
            },
            CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        var command = Assert.Single(runner.Commands);
        Assert.NotNull(command.EnvironmentVariables);
        Assert.Equal("frozen", command.EnvironmentVariables["CV_VALIDATION_ENV_TEST"]);
        Assert.Equal("validation-thread", command.EnvironmentVariables["CODEX_THREAD_ID"]);
        Assert.DoesNotContain("OPENAI_IDENTITY_TOKEN_FILE", command.EnvironmentVariables.Keys);
    }

    [Fact]
    public async Task RunnerScrubsLaunchContextFromTheRealChildEnvironment()
    {
        Directory.CreateDirectory(_root);
        var systemRoot = Environment.GetEnvironmentVariable("SystemRoot")
            ?? Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var commandPrompt = Environment.GetEnvironmentVariable("ComSpec")
            ?? Path.Combine(systemRoot, "System32", "cmd.exe");
        var runner = new CopilotWorkspaceValidationProcessRunner();
        var result = await runner.RunAsync(
            new CopilotWorkspaceValidationCommand(
                commandPrompt,
                ["/d", "/c", "set"],
                _root,
                TimeSpan.FromSeconds(10))
            {
                EnvironmentVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["SystemRoot"] = systemRoot,
                    ["ComSpec"] = commandPrompt,
                    ["PATHEXT"] = ".COM;.EXE;.BAT;.CMD",
                    ["CV_VALIDATION_ENV_TEST"] = "visible",
                    ["OPENAI_IDENTITY_TOKEN_FILE"] = "identity-secret",
                    ["OpenAi_Federation_Rule_Id"] = "federation-secret",
                },
            },
            CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.False(result.TimedOut);
        Assert.Contains("CV_VALIDATION_ENV_TEST=visible", result.StandardOutput, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("OPENAI_IDENTITY_TOKEN_FILE", result.StandardOutput, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("OPENAI_FEDERATION_RULE_ID", result.StandardOutput, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("identity-secret", result.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("federation-secret", result.StandardOutput, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private sealed class RecordingRunner : ICopilotWorkspaceValidationRunner
    {
        public List<CopilotWorkspaceValidationCommand> Commands { get; } = [];

        public Task<CopilotWorkspaceValidationProcessResult> RunAsync(
            CopilotWorkspaceValidationCommand command,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Commands.Add(command);
            return Task.FromResult(new CopilotWorkspaceValidationProcessResult(
                0,
                false,
                string.Empty,
                string.Empty,
                TimeSpan.FromMilliseconds(1)));
        }
    }
}
