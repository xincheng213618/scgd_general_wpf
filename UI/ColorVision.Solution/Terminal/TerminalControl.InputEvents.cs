#pragma warning disable CA1822,CA1834
using log4net;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace ColorVision.Solution.Terminal
{
    public partial class TerminalControl
    {
        #region UI Event Handlers

        /// <summary>
        /// Forward printable text input from IME proxy to ConPTY.
        /// The hidden TextBox handles IME composition natively; we intercept committed text.
        /// </summary>
        private void ImeProxy_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (SendText(e.Text))
            {
                e.Handled = true;
                // Clear residual composition text from the proxy TextBox
                Dispatcher.BeginInvoke(DispatcherPriority.Input, () => ImeProxy.Clear());
            }
        }

        /// <summary>
        /// Position the invisible IME proxy TextBox at the terminal cursor location
        /// so the OS IME candidate window appears at the correct position.
        /// </summary>
        private void UpdateImeProxyPosition()
        {
            double cursorX = TerminalDisplay.CursorCol * TerminalDisplay.CharWidth;
            double cursorY = TerminalDisplay.CursorLine * TerminalDisplay.LineHeight
                             - TerminalScrollViewer.VerticalOffset;
            Canvas.SetLeft(ImeProxy, cursorX);
            Canvas.SetTop(ImeProxy, Math.Max(0, cursorY));
        }

        private void TerminalScrollViewer_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            QueueInitialShellStart();
            if (SendText(e.Text))
            {
                e.Handled = true;
            }
        }

        /// <summary>
        /// Handle special keys: Enter, Backspace, Tab, arrows, Ctrl+C, Ctrl+L, history nav, etc.
        /// </summary>
        private void OutputTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!EnsureTerminalReadyForInput()) return;

            // Let IME framework handle composition keys (Chinese/Japanese input)
            if (e.Key == Key.ImeProcessed) return;

            // --- Ctrl combos ---
            if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
            {
                var selectedText = TerminalDisplay.GetSelectedText();
                if (string.IsNullOrEmpty(selectedText))
                {
                    SendControlSequence("\x03");
                    ResetTrackedInput();
                    e.Handled = true;
                }
                else
                {
                    Clipboard.SetText(selectedText);
                    e.Handled = true;
                }
                return;
            }

            if (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (PasteClipboardText())
                    e.Handled = true;
                return;
            }

            if (e.Key == Key.L && Keyboard.Modifiers == ModifierKeys.Control)
            {
                ClearOutput();
                SendControlSequence("\x0C");
                e.Handled = true;
                return;
            }

            // --- History navigation: Up / Down ---
            if (e.Key == Key.Up && Keyboard.Modifiers == ModifierKeys.None)
            {
                UpdateHistoryContext();
                var entry = _commandHistory.NavigateUp(_currentInput.ToString());
                if (entry != null)
                    ReplaceCurrentInput(entry);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Down && Keyboard.Modifiers == ModifierKeys.None)
            {
                UpdateHistoryContext();
                var entry = _commandHistory.NavigateDown();
                if (entry != null)
                    ReplaceCurrentInput(entry);
                e.Handled = true;
                return;
            }

            // --- Enter: execute and save to history ---
            if (e.Key == Key.Enter)
            {
                var cmd = _currentInput.ToString();
                UpdateHistoryContext();
                _commandHistory.Add(cmd);
                SendControlSequence("\r");
                ResetTrackedInput();
                e.Handled = true;
                return;
            }

            // --- Space ---
            if (e.Key == Key.Space)
            {
                SendText(" ");
                e.Handled = true;
                return;
            }

            // --- Backspace ---
            if (e.Key == Key.Back)
            {
                SendControlSequence("\x7f");
                RemoveTrackedInputBeforeCursor();
                e.Handled = true;
                return;
            }

            // --- Delete ---
            if (e.Key == Key.Delete)
            {
                SendControlSequence("\x1b[3~");
                if (_inputCursorPos < _currentInput.Length)
                    _currentInput.Remove(_inputCursorPos, 1);
                e.Handled = true;
                return;
            }

            // --- Cursor movement (track position) ---
            if (e.Key == Key.Left && Keyboard.Modifiers == ModifierKeys.None)
            {
                SendControlSequence("\x1b[D");
                if (_inputCursorPos > 0) _inputCursorPos--;
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Right && Keyboard.Modifiers == ModifierKeys.None)
            {
                SendControlSequence("\x1b[C");
                if (_inputCursorPos < _currentInput.Length) _inputCursorPos++;
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Home && Keyboard.Modifiers == ModifierKeys.None)
            {
                SendControlSequence("\x1b[H");
                _inputCursorPos = 0;
                e.Handled = true;
                return;
            }
            if (e.Key == Key.End && Keyboard.Modifiers == ModifierKeys.None)
            {
                SendControlSequence("\x1b[F");
                _inputCursorPos = _currentInput.Length;
                e.Handled = true;
                return;
            }

            // --- Other special keys: Tab, Escape, function keys, etc. ---
            if (e.Key == Key.Tab)
            {
                SendControlSequence("\t");
                // Tab completion changes input unpredictably; reset tracking
                ResetTrackedInput();
                e.Handled = true;
                return;
            }

            // Ctrl + letter (except C, V, L already handled above)
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key >= Key.A && e.Key <= Key.Z)
            {
                char c = (char)(e.Key - Key.A + 1);
                SendControlSequence(c.ToString());
                if (e.Key == Key.U) // Ctrl+U clears line
                    ResetTrackedInput();
                e.Handled = true;
                return;
            }

            // Remaining keys via VT sequence mapping
            string? seq = KeyToVTSequence(e.Key);
            if (seq != null)
            {
                SendControlSequence(seq);
                e.Handled = true;
            }
        }

        private void TerminalScrollViewer_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (ImeProxy.IsFocused) return;

            if (!CanSendInput)
            {
                QueueInitialShellStart();
                FocusTerminalInput();
                return;
            }

            if (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (PasteClipboardText())
                    e.Handled = true;
                return;
            }

            // Redirect to ImeProxy for consistent handling
            FocusTerminalInput();
        }

        /// <summary>
        /// Map remaining special keys to VT100 sequences (non-input keys only).
        /// Keys handled explicitly in PreviewKeyDown are not included here.
        /// </summary>
        private static string? KeyToVTSequence(Key key)
        {
            return key switch
            {
                Key.Escape => "\x1b",
                Key.Insert => "\x1b[2~",
                Key.PageUp => "\x1b[5~",
                Key.PageDown => "\x1b[6~",
                Key.F1 => "\x1bOP",
                Key.F2 => "\x1bOQ",
                Key.F3 => "\x1bOR",
                Key.F4 => "\x1bOS",
                Key.F5 => "\x1b[15~",
                Key.F6 => "\x1b[17~",
                Key.F7 => "\x1b[18~",
                Key.F8 => "\x1b[19~",
                Key.F9 => "\x1b[20~",
                Key.F10 => "\x1b[21~",
                Key.F11 => "\x1b[23~",
                Key.F12 => "\x1b[24~",
                _ => null
            };
        }

        private void TerminalScrollViewer_MouseDown(object sender, MouseButtonEventArgs e)
        {
            QueueInitialShellStart();

            if (e.ChangedButton == MouseButton.Right)
                return;

            FocusTerminalInput(force: true);
        }

        private void TerminalScrollViewer_GotFocus(object sender, RoutedEventArgs e)
        {
            QueueInitialShellStart();
            if (!ImeProxy.IsFocused)
                FocusTerminalInput();
        }

        private void StartShellFromMenu(string shell)
        {
            _currentShell = shell;
            MarkInitialShellStarted();
            UpdateTerminalStatus();
            StartShell();
            FocusTerminalInput(force: true);
        }

        private void MenuNewPowerShell_Click(object sender, RoutedEventArgs e)
        {
            StartShellFromMenu("powershell");
            e.Handled = true;
        }

        private void MenuNewCmd_Click(object sender, RoutedEventArgs e)
        {
            StartShellFromMenu("cmd");
            e.Handled = true;
        }

        private void MenuKill_Click(object sender, RoutedEventArgs e)
        {
            KillShell();
            AppendOutput("[终端已终止]\r\n");
            FocusTerminalInput(force: true);
            e.Handled = true;
        }

        private void TerminalContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            QueueInitialShellStart();
            bool hasSelection = !string.IsNullOrEmpty(TerminalDisplay.GetSelectedText());
            MenuCopy.Visibility = hasSelection ? Visibility.Visible : Visibility.Collapsed;
            MenuPaste.IsEnabled = Clipboard.ContainsText() && CanSendInput;
            UpdateCommandStates();
            UpdateTerminalStatus();
        }

        private void MenuCopy_Click(object sender, RoutedEventArgs e)
        {
            CopySelectedText();
            FocusTerminalInput(force: true);
            e.Handled = true;
        }

        private void MenuPaste_Click(object sender, RoutedEventArgs e)
        {
            PasteClipboardText();
            FocusTerminalInput(force: true);
            e.Handled = true;
        }

        private void MenuClear_Click(object sender, RoutedEventArgs e)
        {
            ClearOutput();
            FocusTerminalInput(force: true);
            e.Handled = true;
        }

        #endregion
    }
}
