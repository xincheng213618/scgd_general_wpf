using ColorVision.Copilot;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotSuspendedProcessLauncherTests
{
    [Fact]
    public void BuildCommandLineEscapesWindowsArguments()
    {
        var commandLine = CopilotSuspendedProcessLauncher.BuildCommandLine(
            @"C:\Program Files\tool.exe",
            ["", "plain", "two words", "quote\"inside", "space trailing\\"]);

        Assert.Equal(
            """
            "C:\Program Files\tool.exe" "" plain "two words" "quote\"inside" "space trailing\\"
            """,
            commandLine);
    }

    [Fact]
    public void BuildCommandLineRejectsEmbeddedNullCharacters()
    {
        Assert.Throws<ArgumentException>(() =>
            CopilotSuspendedProcessLauncher.BuildCommandLine(
                @"C:\Windows\System32\cmd.exe",
                ["safe", "unsafe\0suffix"]));
    }
}
