using ColorVision.Solution.Terminal;
using System.IO;
using System.Text;

namespace ColorVision.UI.Tests;

public class TerminalSessionTests
{
    private const string ReadPowerShellProjectData =
        "[pscustomobject]@{ProjectData = [IO.File]::ReadAllText([IO.Path]::Combine((Get-Location).ProviderPath, 'project [data].txt')).Trim()}";
    private const string ReadCmdProjectData = "type \"project [data].txt\"";

    [Fact]
    public async Task ExitedSessionDrainsOutputBeforeNotifying()
    {
        using var terminal = new ConPtyTerminal();
        var result = await RunToExitAsync(terminal, "cmd.exe /D /Q /C \"echo CV-CONPTY-FINAL& exit /B 37\"", Path.GetTempPath());

        Assert.Equal(37, result.ExitCode);
        Assert.Contains("CV-CONPTY-FINAL", result.Output);
        Assert.False(terminal.Write("echo after exit"));
    }

    [Theory]
    [InlineData("powershell", ReadPowerShellProjectData + "; cmd.exe /D /C 'exit /B 27'")]
    [InlineData("cmd", ReadCmdProjectData + " && exit /B 27")]
    public async Task ProjectCommandUsesLiteralWorkingDirectoryAndReturnsItsExitCode(string shell, string command)
    {
        using var directory = new ProjectDirectory("CV-PROJECT-DATA");
        using var terminal = new ConPtyTerminal();

        var result = await RunToExitAsync(terminal, TerminalProjectCommand.CreateCommandLine(shell, command), directory.FullName);

        Assert.Equal(27, result.ExitCode);
        Assert.Contains("CV-PROJECT-DATA", result.Output);
    }

    [Theory]
    [InlineData("powershell", ReadPowerShellProjectData, "; cmd.exe /D /C 'exit /B 31'", "Write-Output 'CV-RAN-PAST-FAILURE'", false)]
    [InlineData("cmd", ReadCmdProjectData, " && exit /B 31", "echo CV-RAN-PAST-FAILURE", false)]
    [InlineData("cmd", ReadCmdProjectData, " && exit /B 31", "echo CV-RAN-PAST-FAILURE", true)]
    public async Task ProjectBatchUsesEachWorkingDirectoryAndStopsAtFirstNativeFailure(
        string shell, string readCommand, string failureSuffix, string unexpectedCommand, bool longBatch)
    {
        const string firstTitle = "Step 'one' & [ready]";
        const string failingTitle = "Step 'two' & [failure]";
        using var first = new ProjectDirectory("CV-FIRST-PROJECT");
        using var second = new ProjectDirectory("CV-SECOND-PROJECT");
        List<TerminalCommandRequest> commands =
        [
            new(firstTitle, readCommand, first.FullName),
            new(failingTitle, readCommand + failureSuffix, second.FullName),
            new("Unexpected third step", unexpectedCommand, first.FullName),
        ];
        if (longBatch)
            commands.AddRange(Enumerable.Repeat(commands[^1], 128));
        using var terminal = new ConPtyTerminal();

        var result = await RunToExitAsync(terminal, TerminalProjectCommand.CreateBatchCommandLine(shell, commands), Path.GetTempPath());

        Assert.Equal(31, result.ExitCode);
        Assert.Contains(firstTitle, result.Output);
        Assert.Contains("CV-FIRST-PROJECT", result.Output);
        Assert.Contains(failingTitle, result.Output);
        Assert.Contains("CV-SECOND-PROJECT", result.Output);
        Assert.DoesNotContain("CV-RAN-PAST-FAILURE", result.Output);
    }

    private static async Task<(int ExitCode, string Output)> RunToExitAsync(
        ConPtyTerminal terminal, string commandLine, string workingDirectory)
    {
        var output = new StringBuilder();
        var exited = new TaskCompletionSource<(int ExitCode, string Output)>(TaskCreationOptions.RunContinuationsAsynchronously);
        terminal.OutputReceived += text => output.Append(text);
        terminal.ProcessExited += code => exited.TrySetResult((code, output.ToString()));

        await terminal.StartAsync(commandLine, workingDirectory).WaitAsync(TimeSpan.FromSeconds(30));
        return await exited.Task.WaitAsync(TimeSpan.FromSeconds(30));
    }

    private sealed class ProjectDirectory : IDisposable
    {
        private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("CV-项目 'data' %PATH% ! & [dir]-");
        private readonly string _inputFile;

        public string FullName => _directory.FullName;

        public ProjectDirectory(string fileContent)
        {
            _inputFile = Path.Combine(FullName, "project [data].txt");
            File.WriteAllText(_inputFile, fileContent + Environment.NewLine);
        }

        public void Dispose()
        {
            File.Delete(_inputFile);
            _directory.Delete();
        }
    }
}
