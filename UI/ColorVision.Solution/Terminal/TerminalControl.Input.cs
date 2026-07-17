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
        #region Input Tracking & History

        private void ResetTrackedInput()
        {
            _currentInput.Clear();
            _inputCursorPos = 0;
            _commandHistory.ResetNavigation();
        }

        private bool SendText(string text, bool trackInput = true)
        {
            if (string.IsNullOrEmpty(text) || !EnsureTerminalReadyForInput())
                return false;

            _terminal!.Write(text);
            if (trackInput)
                TrackSentText(text);
            return true;
        }

        private bool SendControlSequence(string sequence)
        {
            if (string.IsNullOrEmpty(sequence) || !EnsureTerminalReadyForInput())
                return false;

            _terminal!.Write(sequence);
            return true;
        }

        private bool EnsureTerminalReadyForInput()
        {
            if (CanSendInput)
                return true;

            if (_disposed)
                return false;

            if (_terminalState is TerminalLifecycleState.Exited or TerminalLifecycleState.Killed)
            {
                if (!CanStartShellForVisibleControl())
                {
                    QueueInitialShellStart();
                    return false;
                }

                MarkInitialShellStarted();
                StartShell();
            }

            return CanSendInput;
        }

        private void TrackSentText(string text)
        {
            foreach (char ch in text)
            {
                if (ch == '\r' || ch == '\n')
                {
                    ResetTrackedInput();
                }
                else if (ch == '\b' || ch == '\x7f')
                {
                    RemoveTrackedInputBeforeCursor();
                }
                else if (ch == '\t')
                {
                    ResetTrackedInput();
                }
                else if (!char.IsControl(ch))
                {
                    _currentInput.Insert(_inputCursorPos, ch);
                    _inputCursorPos++;
                }
            }
        }

        private void RemoveTrackedInputBeforeCursor()
        {
            if (_inputCursorPos <= 0)
                return;

            _inputCursorPos--;
            _currentInput.Remove(_inputCursorPos, 1);
        }

        private bool PasteClipboardText()
        {
            if (!Clipboard.ContainsText())
                return false;

            return SendText(Clipboard.GetText());
        }

        private bool CopySelectedText()
        {
            var selectedText = TerminalDisplay.GetSelectedText();
            if (string.IsNullOrEmpty(selectedText))
                return false;

            Clipboard.SetText(selectedText);
            return true;
        }

        /// <summary>
        /// Detect shell context (python/node/shell) from the current prompt line
        /// and update the command history context accordingly.
        /// </summary>
        private void UpdateHistoryContext()
        {
            string promptLine = _screenBuffer.GetCurrentLineText();
            _commandHistory.DetectContext(promptLine);
        }

        /// <summary>
        /// Erase the current input on the ConPTY side and type a replacement.
        /// Moves cursor to end, backspaces all, then types newText.
        /// </summary>
        private void ReplaceCurrentInput(string newText)
        {
            if (!CanSendInput) return;

            var sb = new StringBuilder();
            // Move cursor to end of current input
            int movesRight = _currentInput.Length - _inputCursorPos;
            for (int i = 0; i < movesRight; i++)
                sb.Append("\x1b[C");
            // Backspace all characters
            for (int i = 0; i < _currentInput.Length; i++)
                sb.Append("\x7f");
            // Type the new text
            sb.Append(newText);
            SendControlSequence(sb.ToString());

            _currentInput.Clear();
            _currentInput.Append(newText);
            _inputCursorPos = newText.Length;
        }

        #endregion
    }
}
