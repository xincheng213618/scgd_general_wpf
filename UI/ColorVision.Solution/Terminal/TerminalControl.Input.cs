using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.Solution.Terminal
{
    public partial class TerminalControl
    {
        private bool SendTerminalInput(string text)
        {
            if (text.Length == 0 || !EnsureTerminalReadyForInput() || !_terminal!.Write(text))
                return false;

            TerminalDisplay.FollowOutput();
            return true;
        }

        private bool EnsureTerminalReadyForInput()
        {
            if (CanSendInput)
                return true;
            if (_disposed || _isProjectTerminal)
                return false;

            if (_terminalState is TerminalLifecycleState.Exited or TerminalLifecycleState.Killed)
            {
                if (!CanStartShellForVisibleControl())
                {
                    QueueInitialShellStart();
                    return false;
                }

                StartShell();
            }

            return CanSendInput;
        }

        private void PasteClipboardText()
        {
            string clipboardText;
            try
            {
                clipboardText = Clipboard.GetText();
            }
            catch (ExternalException ex)
            {
                log.Debug("Terminal: failed to read clipboard", ex);
                return;
            }

            bool bracketedPaste = CanSendInput && _screenBuffer.BracketedPaste;
            string text = FormatPasteText(clipboardText, bracketedPaste, out bool requiresConfirmation);
            if (text.Length == 0 || !EnsureTerminalReadyForInput())
                return;

            if (requiresConfirmation)
            {
                var terminal = _terminal;
                if (!ConfirmPasteWithNewlines(text) || !ReferenceEquals(_terminal, terminal) || !CanSendInput)
                    return;

                if (_screenBuffer.BracketedPaste)
                    text = FormatPasteText(clipboardText, bracketedPaste: true, out _);
            }

            SendTerminalInput(text);
        }

        private bool ConfirmPasteWithNewlines(string text)
        {
            const int maxPreviewLength = 500;
            const int maxPreviewLines = 6;
            int previewLength = Math.Min(text.Length, maxPreviewLength);
            int lines = 0;
            for (int i = 0; i < previewLength; i++)
            {
                if (text[i] == '\r' && ++lines == maxPreviewLines)
                    previewLength = i + 1;
            }
            if (char.IsHighSurrogate(text[previewLength - 1]))
                previewLength--;

            string preview = text[..previewLength].Replace("\r", " ↵\n", StringComparison.Ordinal);
            if (previewLength < text.Length)
                preview += "…";

            string message = DisplayText.Get("Terminal_PasteWithNewlines") + "\n\n" + preview;
            string title = Properties.Resources.Sol_Terminal_Title;
            var result = Window.GetWindow(this) is Window owner
                ? MessageBox.Show(owner, message, title, MessageBoxButton.OKCancel, MessageBoxImage.Warning, MessageBoxResult.Cancel)
                : MessageBox.Show(message, title, MessageBoxButton.OKCancel, MessageBoxImage.Warning, MessageBoxResult.Cancel);
            return result == MessageBoxResult.OK;
        }

        private void CopySelectedText()
        {
            string selectedText = TerminalDisplay.GetSelectedText();
            if (selectedText.Length == 0)
                return;

            try
            {
                Clipboard.SetText(selectedText);
            }
            catch (ExternalException ex)
            {
                log.Debug("Terminal: failed to write clipboard", ex);
            }
        }

        private static bool ClipboardHasText()
        {
            try
            {
                return Clipboard.ContainsText();
            }
            catch (ExternalException)
            {
                return false;
            }
        }

        internal static string FormatPasteText(string text, bool bracketedPaste, out bool requiresConfirmation)
        {
            requiresConfirmation = false;
            if (text.Length == 0)
                return string.Empty;

            var result = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if (ch == '\r' || ch == '\n')
                {
                    if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                        i++;
                    requiresConfirmation = !bracketedPaste;
                    result.Append('\r');
                }
                else if (ch == '\t' || !char.IsControl(ch))
                {
                    // Clipboard text should never act as an escape sequence or an interrupt.
                    result.Append(ch);
                }
            }

            if (result.Length == 0)
                return string.Empty;
            if (bracketedPaste)
                result.Insert(0, "\u001b[200~").Append("\u001b[201~");
            return result.ToString();
        }

        internal static string? EncodeTerminalKey(Key key, ModifierKeys modifiers, bool applicationCursorKeys, bool win32InputMode)
        {
            if ((modifiers & ModifierKeys.Windows) != 0 ||
                (modifiers == ModifierKeys.Alt && key is Key.F4 or Key.Space or Key.Tab))
            {
                return null;
            }

            bool shift = (modifiers & ModifierKeys.Shift) != 0;
            bool alt = (modifiers & ModifierKeys.Alt) != 0;
            bool control = (modifiers & ModifierKeys.Control) != 0;

            if (win32InputMode)
            {
                const int shiftPressed = 0x0010;
                const int leftAltPressed = 0x0002;
                const int leftCtrlPressed = 0x0008;
                int controlKeyState = (shift ? shiftPressed : 0) | (alt ? leftAltPressed : 0) | (control ? leftCtrlPressed : 0);
                switch (key)
                {
                    // A lone ESC byte is ambiguous with the start of a VT sequence.
                    case Key.Escape: return EncodeWin32Keystroke(27, 1, 27, controlKeyState);
                    case Key.Enter when modifiers != ModifierKeys.None: return EncodeWin32Keystroke(13, 28, control ? 10 : 13, controlKeyState);
                    case Key.Back when modifiers != ModifierKeys.None: return EncodeWin32Keystroke(8, 14, control ? 127 : 8, controlKeyState);
                    case Key.Space when control && !alt: return EncodeWin32Keystroke(32, 57, 0, controlKeyState);
                }
            }

            // xterm encodes Shift, Alt and Ctrl as bits in the 1-based CSI modifier parameter.
            int modifier = 1 + (shift ? 1 : 0) + (alt ? 2 : 0) + (control ? 4 : 0);

            char final = key switch
            {
                Key.Up => 'A',
                Key.Down => 'B',
                Key.Right => 'C',
                Key.Left => 'D',
                Key.End => 'F',
                Key.Home => 'H',
                Key.F1 => 'P',
                Key.F2 => 'Q',
                Key.F3 => 'R',
                Key.F4 => 'S',
                _ => '\0'
            };
            if (final != '\0')
            {
                if (modifier != 1)
                    return FormattableString.Invariant($"\u001b[1;{modifier}{final}");

                string prefix = applicationCursorKeys || key is >= Key.F1 and <= Key.F4 ? "\u001bO" : "\u001b[";
                return prefix + final;
            }

            int code = key switch
            {
                Key.Insert => 2,
                Key.Delete => 3,
                Key.PageUp => 5,
                Key.PageDown => 6,
                Key.F5 => 15,
                Key.F6 => 17,
                Key.F7 => 18,
                Key.F8 => 19,
                Key.F9 => 20,
                Key.F10 => 21,
                Key.F11 => 23,
                Key.F12 => 24,
                _ => 0
            };
            if (code != 0)
            {
                return modifier == 1
                    ? FormattableString.Invariant($"\u001b[{code}~")
                    : FormattableString.Invariant($"\u001b[{code};{modifier}~");
            }

            string? text = key switch
            {
                Key.Escape => "\u001b",
                Key.Enter => control ? "\n" : "\r",
                Key.Back => control ? "\b" : "\u007f",
                Key.Tab => shift ? "\u001b[Z" : "\t",
                Key.Space when control && !alt => "\0",
                >= Key.A and <= Key.Z when control && !alt => ((char)(key - Key.A + 1)).ToString(),
                _ => null
            };
            // Leave printable keys to WPF text composition, including AltGr characters.
            return alt && text != null ? "\u001b" + text : text;
        }

        private static string EncodeWin32Keystroke(int virtualKey, int scanCode, int unicode, int controlKeyState)
        {
            // ConPTY DECSET 9001 uses CSI Vk;Sc;Uc;Kd;Cs;Rc _ for each keyboard event.
            return FormattableString.Invariant(
                $"\u001b[{virtualKey};{scanCode};{unicode};1;{controlKeyState};1_\u001b[{virtualKey};{scanCode};{unicode};0;{controlKeyState};1_");
        }
    }
}
