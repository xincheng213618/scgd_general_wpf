using ColorVision.Copilot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotShellCommandLineLengthTests : IDisposable
{
    private readonly string _root = Path.GetFullPath(Path.Combine(
        Path.GetTempPath(),
        nameof(CopilotShellCommandLineLengthTests),
        Guid.NewGuid().ToString("N")));

    [Theory]
    [InlineData(CopilotShellKind.PowerShell, "powershell")]
    [InlineData(CopilotShellKind.CommandPrompt, "cmd")]
    public async Task ForegroundServiceRejectsRawMaximumWhoseWindowsEncodingIsTooLong(
        CopilotShellKind shell,
        string shellInput)
    {
        Directory.CreateDirectory(_root);
        var executablePath = Path.Combine(_root, shellInput + ".exe");
        File.WriteAllText(executablePath, string.Empty);
        var command = new string('"', CopilotShellCommandService.MaximumCommandCharacters);
        var arguments = CopilotShellCommandService.BuildArguments(shell, command);
        Assert.False(CopilotSuspendedProcessLauncher.TryBuildCommandLine(
            executablePath,
            arguments,
            out var encodedCommandLine));
        Assert.True(
            encodedCommandLine.Length > CopilotSuspendedProcessLauncher.MaximumCommandLineCharacters);
        var runner = new RecordingRunner();
        using var archiveRegistry = new CopilotShellCommandOutputArchiveRegistry();
        var service = new CopilotShellCommandService(
            runner,
            _ => executablePath,
            archiveRegistry);

        var result = await service.ExecuteAsync(
            CreateRequest(shell),
            CreateInput(command, shellInput),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(CopilotToolFailureKind.Validation, result.FailureKind);
        Assert.Equal(
            CopilotShellCommandService.CommandLineTooLongFailureCode,
            result.FailureCode);
        Assert.Contains("Windows argument encoding", result.Summary, StringComparison.Ordinal);
        Assert.Equal(0, runner.InvocationCount);
    }

    [Fact]
    public async Task ForegroundServiceAcceptsRawMaximumWhenWindowsEncodingFits()
    {
        Directory.CreateDirectory(_root);
        var executablePath = Path.Combine(_root, "powershell.exe");
        File.WriteAllText(executablePath, string.Empty);
        var command = new string('x', CopilotShellCommandService.MaximumCommandCharacters);
        var arguments = CopilotShellCommandService.BuildArguments(
            CopilotShellKind.PowerShell,
            command);
        Assert.True(CopilotSuspendedProcessLauncher.TryBuildCommandLine(
            executablePath,
            arguments,
            out _));
        var runner = new RecordingRunner();
        using var archiveRegistry = new CopilotShellCommandOutputArchiveRegistry();
        var service = new CopilotShellCommandService(
            runner,
            _ => executablePath,
            archiveRegistry);

        var result = await service.ExecuteAsync(
            CreateRequest(CopilotShellKind.PowerShell),
            CreateInput(command, "powershell"),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(1, runner.InvocationCount);
    }

    [Fact]
    public async Task LauncherReportsEncodedLengthFailureWithoutArgumentException()
    {
        Directory.CreateDirectory(_root);
        var executablePath = Path.Combine(_root, "powershell.exe");
        var command = new string('"', CopilotShellCommandService.MaximumCommandCharacters);
        var jobAssignmentAttempted = false;

        var exception = await Assert.ThrowsAsync<CopilotProcessCommandLineTooLongException>(() =>
            CopilotSuspendedProcessLauncher.LaunchAsync(
                executablePath,
                CopilotShellCommandService.BuildArguments(CopilotShellKind.PowerShell, command),
                _root,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                Encoding.UTF8,
                _ =>
                {
                    jobAssignmentAttempted = true;
                    return null;
                },
                CancellationToken.None));

        Assert.IsNotType<ArgumentException>(exception);
        Assert.False(jobAssignmentAttempted);
    }

    [Fact]
    public async Task BackgroundRegistryRejectsEncodedLengthBeforeLauncherReservation()
    {
        Directory.CreateDirectory(_root);
        var command = new string('"', CopilotShellCommandService.MaximumCommandCharacters);
        var launcher = new RecordingBackgroundLauncher();
        var registry = new CopilotBackgroundShellCommandRegistry(launcher);

        var result = await registry.StartAsync(
            CreateRequest(CopilotShellKind.PowerShell),
            CreateInput(command, "powershell"),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(CopilotToolFailureKind.Validation, result.FailureKind);
        Assert.Contains("encoded command line", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal(0, launcher.InvocationCount);
    }

    [Fact]
    public async Task CommandPromptLimitIsValidatedBeforeForegroundOrBackgroundLaunch()
    {
        Directory.CreateDirectory(_root);
        var executablePath = Path.Combine(_root, "cmd.exe");
        File.WriteAllText(executablePath, string.Empty);
        var command = new string('x', 8_200);
        var arguments = CopilotShellCommandService.BuildArguments(
            CopilotShellKind.CommandPrompt,
            command);
        Assert.True(CopilotSuspendedProcessLauncher.TryBuildCommandLine(
            executablePath,
            arguments,
            out var encodedCommandLine));
        Assert.True(
            encodedCommandLine.Length > CopilotShellCommandService.MaximumCommandPromptCommandLineCharacters);
        Assert.False(CopilotShellCommandService.TryBuildSupportedCommandLine(
            CopilotShellKind.CommandPrompt,
            executablePath,
            arguments,
            out _));
        var foregroundRunner = new RecordingRunner();
        using var archiveRegistry = new CopilotShellCommandOutputArchiveRegistry();
        var foregroundService = new CopilotShellCommandService(
            foregroundRunner,
            _ => executablePath,
            archiveRegistry);
        var backgroundLauncher = new RecordingBackgroundLauncher();
        var backgroundRegistry = new CopilotBackgroundShellCommandRegistry(
            backgroundLauncher);

        var foregroundResult = await foregroundService.ExecuteAsync(
            CreateRequest(CopilotShellKind.CommandPrompt),
            CreateInput(command, "cmd"),
            CancellationToken.None);
        var backgroundResult = await backgroundRegistry.StartAsync(
            CreateRequest(CopilotShellKind.CommandPrompt),
            CreateInput(command, "cmd"),
            CancellationToken.None);

        Assert.False(foregroundResult.Success);
        Assert.Equal(CopilotToolFailureKind.Validation, foregroundResult.FailureKind);
        Assert.Equal(
            CopilotShellCommandService.CommandLineTooLongFailureCode,
            foregroundResult.FailureCode);
        Assert.Equal(0, foregroundRunner.InvocationCount);
        Assert.False(backgroundResult.Success);
        Assert.Equal(CopilotToolFailureKind.Validation, backgroundResult.FailureKind);
        Assert.Contains("8191", backgroundResult.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal(0, backgroundLauncher.InvocationCount);
    }

    public void Dispose()
    {
        if (!Directory.Exists(_root))
            return;

        var fullPath = Path.GetFullPath(_root);
        var expectedParent = Path.GetFullPath(Path.Combine(
            Path.GetTempPath(),
            nameof(CopilotShellCommandLineLengthTests)))
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!string.Equals(
                Path.GetDirectoryName(fullPath)?.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar),
                expectedParent,
                StringComparison.OrdinalIgnoreCase)
            || !Guid.TryParseExact(Path.GetFileName(fullPath), "N", out _))
        {
            throw new InvalidOperationException(
                "Refusing to delete an unexpected command-line test directory.");
        }
        Directory.Delete(fullPath, recursive: true);
    }

    private CopilotAgentRequest CreateRequest(CopilotShellKind shell) => new()
    {
        ConversationId = "command-line-length",
        WritableLocalRootPaths = [_root],
        SearchRootPaths = [_root],
        PreferredShell = shell,
    };

    private CopilotAgentToolInput CreateInput(string command, string shell) => new()
    {
        Arguments = new Dictionary<string, object?>
        {
            ["command"] = command,
            ["workingDirectory"] = _root,
            ["shell"] = shell,
            ["timeoutSeconds"] = 10,
        },
    };

    private sealed class RecordingRunner : ICopilotShellProcessRunner
    {
        public int InvocationCount { get; private set; }

        public Task<CopilotShellProcessResult> RunAsync(
            CopilotShellProcessCommand command,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            InvocationCount++;
            return Task.FromResult(new CopilotShellProcessResult(
                0,
                false,
                string.Empty,
                string.Empty,
                TimeSpan.Zero)
            {
                ProcessTreeContained = true,
            });
        }
    }

    private sealed class RecordingBackgroundLauncher : ICopilotBackgroundShellProcessLauncher
    {
        public int InvocationCount { get; private set; }

        public Task<ICopilotBackgroundShellProcess> StartAsync(
            CopilotShellProcessCommand command,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            InvocationCount++;
            throw new InvalidOperationException("The rejected command must not reach the background launcher.");
        }
    }
}
