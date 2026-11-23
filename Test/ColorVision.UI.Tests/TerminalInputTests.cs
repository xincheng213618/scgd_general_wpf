using System.Windows.Input;
using ColorVision.Solution.Terminal;
using Xunit;

namespace ColorVision.UI.Tests;

public class TerminalInputTests
{
    [Theory]
    [InlineData(Key.Up, ModifierKeys.None, false, "\u001b[A")]
    [InlineData(Key.Down, ModifierKeys.None, true, "\u001bOB")]
    [InlineData(Key.Left, ModifierKeys.Control, true, "\u001b[1;5D")]
    [InlineData(Key.F5, ModifierKeys.Shift, false, "\u001b[15;2~")]
    [InlineData(Key.Tab, ModifierKeys.Shift, false, "\u001b[Z")]
    [InlineData(Key.C, ModifierKeys.Control, false, "\u0003")]
    [InlineData(Key.L, ModifierKeys.Control, false, "\f")]
    [InlineData(Key.Enter, ModifierKeys.None, false, "\r")]
    [InlineData(Key.F4, ModifierKeys.Alt, false, null)]
    [InlineData(Key.E, ModifierKeys.Control | ModifierKeys.Alt, false, null)]
    public void KeyEncodingPreservesShellEditingAndInputModes(
        Key key, ModifierKeys modifiers, bool applicationCursorKeys, string? expected)
    {
        Assert.Equal(expected, TerminalControl.EncodeTerminalKey(key, modifiers, applicationCursorKeys, win32InputMode: false));
    }

    [Theory]
    [InlineData(Key.Escape, ModifierKeys.None, "\u001b", "\u001b[27;1;27;1;0;1_\u001b[27;1;27;0;0;1_")]
    [InlineData(Key.Enter, ModifierKeys.Shift, "\r", "\u001b[13;28;13;1;16;1_\u001b[13;28;13;0;16;1_")]
    [InlineData(Key.Back, ModifierKeys.Control, "\b", "\u001b[8;14;127;1;8;1_\u001b[8;14;127;0;8;1_")]
    [InlineData(Key.Space, ModifierKeys.Control, "\0", "\u001b[32;57;0;1;8;1_\u001b[32;57;0;0;8;1_")]
    public void Win32InputModePreservesKeysThatVtCannotExpressUnambiguously(
        Key key, ModifierKeys modifiers, string vt, string win32)
    {
        Assert.Equal(vt, TerminalControl.EncodeTerminalKey(key, modifiers, applicationCursorKeys: false, win32InputMode: false));
        Assert.Equal(win32, TerminalControl.EncodeTerminalKey(key, modifiers, applicationCursorKeys: false, win32InputMode: true));
    }

    [Theory]
    [InlineData(false, "第一行\rsecond\tline\rthird\r😀")]
    [InlineData(true, "\u001b[200~第一行\rsecond\tline\rthird\r😀\u001b[201~")]
    public void PasteNormalizesLineEndingsAndDropsControlKeys(bool bracketedPaste, string expected)
    {
        string input = "第一行\r\nsecond\tline\nthird\r😀\u001b\u0003\u007f";
        Assert.Equal(expected, TerminalControl.FormatPasteText(input, bracketedPaste, out bool requiresConfirmation));
        Assert.Equal(!bracketedPaste, requiresConfirmation);
    }

    [Theory]
    [InlineData("")]
    [InlineData("\u001b\u0003")]
    public void EmptyPasteDoesNotSendMarkers(string input)
    {
        Assert.Empty(TerminalControl.FormatPasteText(input, true, out bool requiresConfirmation));
        Assert.False(requiresConfirmation);
    }

    [Theory]
    [InlineData("single line", false)]
    [InlineData("single line\n", true)]
    public void LegacyPasteConfirmsEvenASingleTrailingNewline(string input, bool expectedConfirmation)
    {
        TerminalControl.FormatPasteText(input, false, out bool requiresConfirmation);
        Assert.Equal(expectedConfirmation, requiresConfirmation);
    }
}
