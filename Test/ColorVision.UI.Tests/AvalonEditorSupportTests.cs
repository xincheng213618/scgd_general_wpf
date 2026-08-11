using ColorVision.Solution.Editor.AvalonEditor;
using ColorVision.Solution.Editor;
using ColorVision.Solution.Terminal;
using ICSharpCode.AvalonEdit.Highlighting;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.UI.Tests;

public class AvalonEditorSupportTests
{
    [Theory]
    [InlineData("script.py")]
    [InlineData("SCRIPT.PYW")]
    public void PythonDocumentsExposeRunCapability(string filePath)
    {
        Assert.True(AvalonEditControll.IsPythonDocument(filePath));
    }

    [Theory]
    [InlineData("license.txt")]
    [InlineData("settings.json")]
    [InlineData("program.cs")]
    [InlineData(null)]
    public void NonPythonDocumentsDoNotExposeRunCapability(string? filePath)
    {
        Assert.False(AvalonEditControll.IsPythonDocument(filePath));
    }

    [Theory]
    [InlineData("license.txt", null)]
    [InlineData("script.py", "Python")]
    [InlineData("settings.json", "Json")]
    [InlineData("solution.cvproj", "Json")]
    [InlineData("project.csproj", "XML")]
    public void FileExtensionSelectsExpectedHighlighting(string filePath, string? expectedName)
    {
        Assert.Equal(expectedName, AvalonEditControll.GetHighlightingDefinition(filePath)?.Name);
    }

    [Theory]
    [InlineData("Comment", "EditorSyntaxCommentBrush")]
    [InlineData("String", "EditorSyntaxStringBrush")]
    [InlineData("NumberLiteral", "EditorSyntaxNumberBrush")]
    [InlineData("Keywords", "EditorSyntaxKeywordBrush")]
    [InlineData("MethodCall", "EditorSyntaxMethodBrush")]
    [InlineData("FieldName", "EditorSyntaxPropertyBrush")]
    [InlineData("Punctuation", "EditorForegroundBrush")]
    [InlineData("Visibility", "EditorSyntaxKeywordBrush")]
    [InlineData("GetSetAddRemove", "EditorSyntaxKeywordBrush")]
    [InlineData("JavaScriptIntrinsics", "EditorSyntaxMethodBrush")]
    [InlineData("Value", "EditorSyntaxPropertyBrush")]
    [InlineData("Position", "EditorSyntaxNumberBrush")]
    [InlineData("Header", "EditorSyntaxPreprocessorBrush")]
    [InlineData("FutureGrammarRole", null)]
    public void SyntaxRolesMapToThemeBrushes(string colorName, string? expectedResourceKey)
    {
        Assert.Equal(expectedResourceKey, ThemeAwareHighlightingColorizer.GetBrushResourceKey(colorName));
    }

    [Fact]
    public void EveryBuiltInNamedFixedForegroundHasThemeOwnedForeground()
    {
        foreach (IHighlightingDefinition definition in HighlightingManager.Instance.HighlightingDefinitions)
        {
            foreach (HighlightingColor color in definition.NamedHighlightingColors)
            {
                if (color.Foreground != null)
                {
                    Assert.False(string.IsNullOrWhiteSpace(
                        ThemeAwareHighlightingColorizer.GetForegroundBrushResourceKey(color)));
                }
            }
        }
    }

    [Fact]
    public void EveryBuiltInFixedSyntaxColorIsReplacedByThemeResources()
    {
        foreach (IHighlightingDefinition definition in HighlightingManager.Instance.HighlightingDefinitions)
        {
            var visitedRuleSets = new HashSet<HighlightingRuleSet>(ReferenceEqualityComparer.Instance);
            IEnumerable<HighlightingColor> colors = definition.NamedHighlightingColors
                .Concat(EnumerateRuleSetColors(definition.MainRuleSet, visitedRuleSets));

            foreach (HighlightingColor color in colors)
            {
                if (color.Foreground != null)
                {
                    Assert.False(string.IsNullOrWhiteSpace(
                        ThemeAwareHighlightingColorizer.GetForegroundBrushResourceKey(color)));
                }

                if (color.Background != null)
                {
                    Assert.Equal(
                        "EditorSyntaxBackgroundBrush",
                        ThemeAwareHighlightingColorizer.GetBackgroundBrushResourceKey(color));
                }
            }
        }
    }

    [Fact]
    public void TextEditorRegistrationIncludesPythonWindowScripts()
    {
        EditorForExtensionAttribute attribute = Assert.Single(
            typeof(ColorVision.Solution.TextEditor).GetCustomAttributes<EditorForExtensionAttribute>());

        Assert.Contains(".pyw", attribute.Extensions, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void UndoAndRedoButtonsTargetAvalonEditTextArea()
    {
        WpfTestHost.Invoke(() =>
        {
            using var control = new AvalonEditControll();
            var editor = Assert.IsType<ICSharpCode.AvalonEdit.TextEditor>(control.FindName("textEditor"));
            var undoButton = Assert.IsType<Button>(control.FindName("UndoButton"));
            var redoButton = Assert.IsType<Button>(control.FindName("RedoButton"));

            editor.AppendText("change");
            Assert.Same(editor.TextArea, undoButton.CommandTarget);
            Assert.Same(editor.TextArea, redoButton.CommandTarget);
            Assert.True(ApplicationCommands.Undo.CanExecute(null, undoButton.CommandTarget));

            ApplicationCommands.Undo.Execute(null, undoButton.CommandTarget);
            Assert.True(ApplicationCommands.Redo.CanExecute(null, redoButton.CommandTarget));
        });
    }

    [Theory]
    [InlineData("line1\r\nline2", "CRLF")]
    [InlineData("line1\nline2", "LF")]
    [InlineData("line1\rline2", "CR")]
    [InlineData("single line", "—")]
    public void LineEndingLabelReflectsDocumentContent(string text, string expected)
    {
        Assert.Equal(expected, AvalonEditControll.GetLineEndingLabel(text));
    }

    [Fact]
    public void ScriptStartupCommandDoesNotEmbedPowerShellOrCmdMetacharacters()
    {
        const string filePath = @"C:\qa\$(Write-Output INJECTED)\%TEMP%\it's `safe`.py";
        string encodedPath = Convert.ToBase64String(Encoding.Unicode.GetBytes(filePath));

        string powerShellCommand = TerminalControl.BuildScriptStartupCommand(filePath, "powershell");
        Assert.DoesNotContain(filePath, powerShellCommand, StringComparison.Ordinal);
        Assert.DoesNotContain("$(Write-Output INJECTED)", powerShellCommand, StringComparison.Ordinal);
        Assert.DoesNotContain("%TEMP%", powerShellCommand, StringComparison.Ordinal);
        Assert.Contains(encodedPath, powerShellCommand, StringComparison.Ordinal);
        Assert.Contains("& python -- $scriptPath", powerShellCommand, StringComparison.Ordinal);
        Assert.Contains("$global:LASTEXITCODE = $null", powerShellCommand, StringComparison.Ordinal);
        Assert.Contains("-not $scriptSucceeded -and $scriptExitCode -eq 0", powerShellCommand, StringComparison.Ordinal);
        Assert.Contains("[进程已结束，退出代码:", powerShellCommand, StringComparison.Ordinal);

        string cmdCommand = TerminalControl.BuildScriptStartupCommand(filePath, "cmd");
        Assert.DoesNotContain(filePath, cmdCommand, StringComparison.Ordinal);
        Assert.DoesNotContain("$(Write-Output INJECTED)", cmdCommand, StringComparison.Ordinal);
        Assert.DoesNotContain("%TEMP%", cmdCommand, StringComparison.Ordinal);
        string nestedPayload = cmdCommand.Split(' ', StringSplitOptions.RemoveEmptyEntries)[^1];
        string decodedPowerShellCommand = Encoding.Unicode.GetString(Convert.FromBase64String(nestedPayload));
        Assert.Equal(powerShellCommand, decodedPowerShellCommand);
    }

    [Fact]
    public void CmdScriptPathMetacharactersRemainLiteralDuringExecution()
    {
        string directoryPath = Path.Combine(
            Path.GetTempPath(),
            $"ColorVision batch-%TEMP%-^-$()-'`-{Guid.NewGuid():N}");
        string scriptPath = Path.Combine(directoryPath, "literal.cmd");
        Directory.CreateDirectory(directoryPath);
        File.WriteAllText(scriptPath, "@echo BAT_LITERAL_OK\r\n");

        try
        {
            string command = TerminalControl.BuildScriptStartupCommand(scriptPath, "powershell");
            string output = RunWindowsPowerShell(command + "; Write-Output ('COLORVISION_TEST_EXIT_CODE=' + $scriptExitCode)");

            Assert.Contains("BAT_LITERAL_OK", output, StringComparison.Ordinal);
            Assert.Contains("COLORVISION_TEST_EXIT_CODE=0", output, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    [Fact]
    public void ConPtyTerminalStartsInsideKillOnCloseJob()
    {
        using var ready = new ManualResetEventSlim();
        using var terminal = new ConPtyTerminal();
        terminal.OutputReceived += output =>
        {
            if (output.Contains("JOB_READY", StringComparison.Ordinal))
                ready.Set();
        };

        terminal.Start(
            "cmd.exe /D /K echo JOB_READY",
            Path.GetTempPath(),
            cols: 80,
            rows: 20);

        Assert.True(ready.Wait(TimeSpan.FromSeconds(10)), "ConPTY did not start inside the job object.");
        terminal.Kill();
    }

    [Theory]
    [InlineData(65001)]
    [InlineData(1200)]
    public void EditorPreservesDetectedEncodingWhenSaving(int codePage)
    {
        Encoding encoding = codePage == 65001 ? new UTF8Encoding(true) : Encoding.Unicode;
        string filePath = Path.Combine(Path.GetTempPath(), $"ColorVision-Avalon-{Guid.NewGuid():N}.txt");
        byte[] content = encoding.GetBytes("第一行\r\nsecond line");
        File.WriteAllBytes(filePath, [.. encoding.GetPreamble(), .. content]);

        try
        {
            WpfTestHost.Invoke(() =>
            {
                using var control = new AvalonEditControll(filePath);
                var editor = Assert.IsType<ICSharpCode.AvalonEdit.TextEditor>(control.FindName("textEditor"));
                editor.AppendText("\r\n保存后仍然可读");
                Assert.True(control.Save());
            });

            byte[] savedBytes = File.ReadAllBytes(filePath);
            Assert.True(savedBytes.AsSpan().StartsWith(encoding.GetPreamble()));
            string savedText = encoding.GetString(savedBytes.AsSpan(encoding.GetPreamble().Length));
            Assert.Contains("保存后仍然可读", savedText, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    private static IEnumerable<HighlightingColor> EnumerateRuleSetColors(
        HighlightingRuleSet ruleSet,
        HashSet<HighlightingRuleSet> visitedRuleSets)
    {
        if (!visitedRuleSets.Add(ruleSet))
            yield break;

        foreach (HighlightingRule rule in ruleSet.Rules)
        {
            if (rule.Color != null)
                yield return rule.Color;
        }

        foreach (HighlightingSpan span in ruleSet.Spans)
        {
            if (span.StartColor != null)
                yield return span.StartColor;
            if (span.SpanColor != null)
                yield return span.SpanColor;
            if (span.EndColor != null)
                yield return span.EndColor;
            if (span.RuleSet != null)
            {
                foreach (HighlightingColor color in EnumerateRuleSetColors(span.RuleSet, visitedRuleSets))
                    yield return color;
            }
        }
    }

    private static string RunWindowsPowerShell(string command)
    {
        string encodedCommand = Convert.ToBase64String(Encoding.Unicode.GetBytes(command));
        var startInfo = new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add("-NoLogo");
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-EncodedCommand");
        startInfo.ArgumentList.Add(encodedCommand);

        using Process process = Process.Start(startInfo)!;
        string standardOutput = process.StandardOutput.ReadToEnd();
        string standardError = process.StandardError.ReadToEnd();
        Assert.True(process.WaitForExit(10_000), "PowerShell command did not exit in time.");
        return standardOutput + standardError;
    }
}
