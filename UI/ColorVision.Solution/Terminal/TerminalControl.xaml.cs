using log4net;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace ColorVision.Solution.Terminal
{
    public partial class TerminalControl : UserControl
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(TerminalControl));

        private ConPtyTerminal? _terminal;
        private TerminalScreenBuffer _screenBuffer = new();
        private string _lastRendered = "";
        private readonly object _outputLock = new();
        private readonly Queue<string> _pendingOutput = new();
        private DispatcherTimer? _flushTimer;
        private bool _isShellRunning;
        private string _currentShell = "powershell";

        // Input tracking for command history
        private readonly CommandHistory _commandHistory = new();
        private StringBuilder _currentInput = new();
        private int _inputCursorPos;

        public TerminalControl()
        {
            InitializeComponent();
            _flushTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
            _flushTimer.Tick += FlushOutput;

            TerminalDisplay.UrlClicked += OnUrlClicked;
        }

        private void OnUrlClicked(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                log.Warn($"Terminal: failed to open URL '{url}': {ex.Message}");
            }
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_isShellRunning)
                StartShell();
            ImeProxy.Focus();
        }

        public void StartShell(string? workingDirectory = null)
        {
            KillShell();
            ClearOutput();
            _screenBuffer = new TerminalScreenBuffer();
            _lastRendered = "";

            workingDirectory ??= GetDefaultWorkingDirectory();

            string commandLine;
            if (_currentShell == "cmd")
                commandLine = "cmd.exe";
            else
                commandLine = "powershell.exe -NoLogo -NoExit";

            try
            {
                _terminal = new ConPtyTerminal();
                _terminal.OutputReceived += OnConPtyOutput;
                _terminal.ProcessExited += OnConPtyExited;
                _terminal.Start(commandLine, workingDirectory);
                _isShellRunning = true;

                log.Info($"Terminal: ConPTY started ({commandLine}) in {workingDirectory}");
            }
            catch (Exception ex)
            {
                AppendOutput($"启动终端失败: {ex.Message}\r\n");
                log.Error("Terminal: failed to start ConPTY shell", ex);
            }
        }

        public void SendCommand(string command)
        {
            if (!_isShellRunning || _terminal == null)
            {
                StartShell();
                Task.Delay(500).ContinueWith(_ =>
                    Dispatcher.BeginInvoke(() => _terminal?.Write(command + "\r\n")));
                return;
            }
            _terminal.Write(command + "\r\n");
        }

        public void RunScript(string filePath)
        {
            if (!File.Exists(filePath))
            {
                AppendOutput($"文件不存在: {filePath}\r\n");
                return;
            }

            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            var dir = Path.GetDirectoryName(filePath) ?? "";
            string command;

            if (_currentShell == "cmd")
            {
                command = ext switch
                {
                    ".py" or ".pyw" => $"cd /d \"{dir}\" && python \"{filePath}\"",
                    ".bat" or ".cmd" => $"cd /d \"{dir}\" && \"{filePath}\"",
                    ".sh" => $"cd /d \"{dir}\" && bash \"{filePath}\"",
                    ".js" => $"cd /d \"{dir}\" && node \"{filePath}\"",
                    ".ps1" => $"powershell -ExecutionPolicy Bypass -File \"{filePath}\"",
                    _ => $"\"{filePath}\""
                };
            }
            else
            {
                command = ext switch
                {
                    ".py" or ".pyw" => $"cd \"{dir}\"; python \"{filePath}\"",
                    ".ps1" => $"cd \"{dir}\"; & \"{filePath}\"",
                    ".bat" or ".cmd" => $"cd \"{dir}\"; cmd /c \"{filePath}\"",
                    ".sh" => $"cd \"{dir}\"; bash \"{filePath}\"",
                    ".js" => $"cd \"{dir}\"; node \"{filePath}\"",
                    _ => $"& \"{filePath}\""
                };
            }

            SendCommand(command);
        }

        public void SendCtrlC()
        {
            _terminal?.Write("\x03");
        }

        private void OnConPtyOutput(string text)
        {
            // Queue raw VT100 output for processing by TerminalScreenBuffer
            lock (_outputLock)
            {
                _pendingOutput.Enqueue(text);
            }
            Dispatcher.BeginInvoke(() =>
            {
                if (_flushTimer != null && !_flushTimer.IsEnabled)
                    _flushTimer.Start();
            });
        }

        private void OnConPtyExited(int exitCode)
        {
            _isShellRunning = false;
            // Write exit message directly into the screen buffer on UI thread
            Dispatcher.BeginInvoke(() =>
            {
                _screenBuffer.Write($"\r\n[终端已退出, 退出代码: {exitCode}]\r\n");
                RenderBuffer();
            });
        }

        private void AppendOutput(string text)
        {
            // For locally-generated messages (not from ConPTY)
            Dispatcher.BeginInvoke(() =>
            {
                _screenBuffer.Write(text);
                RenderBuffer();
            });
        }

        private void FlushOutput(object? sender, EventArgs e)
        {
            _flushTimer?.Stop();

            string rawText;
            lock (_outputLock)
            {
                if (_pendingOutput.Count == 0) return;
                var sb = new StringBuilder();
                while (_pendingOutput.Count > 0)
                    sb.Append(_pendingOutput.Dequeue());
                rawText = sb.ToString();
            }

            _screenBuffer.Write(rawText);
            RenderBuffer();
        }

        private void RenderBuffer()
        {
            var (lines, cursorLine, cursorCol) = _screenBuffer.RenderLines();
            TerminalDisplay.UpdateContent(lines, cursorLine, cursorCol);

            // Scroll to cursor after layout completes (Loaded priority runs after Render/Layout)
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () =>
            {
                ScrollToCursor();
                UpdateImeProxyPosition();
            });
        }

        private void ScrollToCursor()
        {
            double viewportHeight = TerminalScrollViewer.ViewportHeight;
            if (viewportHeight <= 0) return;

            double cursorY = TerminalDisplay.CursorY;
            double currentOffset = TerminalScrollViewer.VerticalOffset;

            if (cursorY < currentOffset)
                TerminalScrollViewer.ScrollToVerticalOffset(cursorY);
            else if (cursorY + TerminalDisplay.LineHeight > currentOffset + viewportHeight)
                TerminalScrollViewer.ScrollToVerticalOffset(cursorY + TerminalDisplay.LineHeight - viewportHeight);
        }

        private void ClearOutput()
        {
            lock (_outputLock)
            {
                _pendingOutput.Clear();
            }
            _screenBuffer.Clear();
            _lastRendered = "";
            Dispatcher.BeginInvoke(() =>
            {
                TerminalDisplay.UpdateContent(new List<TerminalLine>(), 0, 0);
                TerminalScrollViewer.ScrollToHome();
            });
        }

        private void KillShell()
        {
            if (_terminal != null)
            {
                _isShellRunning = false;
                try
                {
                    _terminal.Kill();
                }
                catch (Exception ex)
                {
                    log.Warn($"Terminal: kill shell failed: {ex.Message}");
                }
                _terminal.OutputReceived -= OnConPtyOutput;
                _terminal.ProcessExited -= OnConPtyExited;
                _terminal.Dispose();
                _terminal = null;
            }
        }

        private string GetDefaultWorkingDirectory()
        {
            var dirInfo = SolutionManager.GetInstance()?.CurrentSolutionExplorer?.DirectoryInfo;
            if (dirInfo?.Exists == true)
                return dirInfo.FullName;
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        #region Input Tracking & History

        private void ResetInputTracking()
        {
            _currentInput.Clear();
            _inputCursorPos = 0;
            _commandHistory.ResetNavigation();
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
            if (_terminal == null) return;

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
            _terminal.Write(sb.ToString());

            _currentInput.Clear();
            _currentInput.Append(newText);
            _inputCursorPos = newText.Length;
        }

        #endregion

        #region UI Event Handlers

        /// <summary>
        /// Forward printable text input directly to ConPTY and track it.
        /// </summary>
        private void OutputTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (_terminal != null && _isShellRunning && !string.IsNullOrEmpty(e.Text))
            {
                _terminal.Write(e.Text);
                _currentInput.Insert(_inputCursorPos, e.Text);
                _inputCursorPos += e.Text.Length;
                e.Handled = true;
            }
        }

        /// <summary>
        /// Forward printable text input from IME proxy to ConPTY.
        /// The hidden TextBox handles IME composition natively; we intercept committed text.
        /// </summary>
        private void ImeProxy_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (_terminal != null && _isShellRunning && !string.IsNullOrEmpty(e.Text))
            {
                _terminal.Write(e.Text);
                _currentInput.Insert(_inputCursorPos, e.Text);
                _inputCursorPos += e.Text.Length;
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
            if (_terminal != null && _isShellRunning && !string.IsNullOrEmpty(e.Text))
            {
                _terminal.Write(e.Text);
                _currentInput.Insert(_inputCursorPos, e.Text);
                _inputCursorPos += e.Text.Length;
                e.Handled = true;
            }
        }

        /// <summary>
        /// Handle special keys: Enter, Backspace, Tab, arrows, Ctrl+C, Ctrl+L, history nav, etc.
        /// </summary>
        private void OutputTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (_terminal == null || !_isShellRunning) return;

            // Let IME framework handle composition keys (Chinese/Japanese input)
            if (e.Key == Key.ImeProcessed) return;

            // --- Ctrl combos ---
            if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
            {
                var selectedText = TerminalDisplay.GetSelectedText();
                if (string.IsNullOrEmpty(selectedText))
                {
                    _terminal.Write("\x03");
                    ResetInputTracking();
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
                if (Clipboard.ContainsText())
                {
                    var text = Clipboard.GetText();
                    _terminal.Write(text);
                    _currentInput.Insert(_inputCursorPos, text);
                    _inputCursorPos += text.Length;
                    e.Handled = true;
                }
                return;
            }

            if (e.Key == Key.L && Keyboard.Modifiers == ModifierKeys.Control)
            {
                ClearOutput();
                _terminal.Write("\x0C");
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
                _terminal.Write("\r");
                ResetInputTracking();
                e.Handled = true;
                return;
            }

            // --- Space ---
            if (e.Key == Key.Space)
            {
                _terminal.Write(" ");
                _currentInput.Insert(_inputCursorPos, ' ');
                _inputCursorPos++;
                e.Handled = true;
                return;
            }

            // --- Backspace ---
            if (e.Key == Key.Back)
            {
                _terminal.Write("\x7f");
                if (_inputCursorPos > 0)
                {
                    _inputCursorPos--;
                    _currentInput.Remove(_inputCursorPos, 1);
                }
                e.Handled = true;
                return;
            }

            // --- Delete ---
            if (e.Key == Key.Delete)
            {
                _terminal.Write("\x1b[3~");
                if (_inputCursorPos < _currentInput.Length)
                    _currentInput.Remove(_inputCursorPos, 1);
                e.Handled = true;
                return;
            }

            // --- Cursor movement (track position) ---
            if (e.Key == Key.Left && Keyboard.Modifiers == ModifierKeys.None)
            {
                _terminal.Write("\x1b[D");
                if (_inputCursorPos > 0) _inputCursorPos--;
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Right && Keyboard.Modifiers == ModifierKeys.None)
            {
                _terminal.Write("\x1b[C");
                if (_inputCursorPos < _currentInput.Length) _inputCursorPos++;
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Home && Keyboard.Modifiers == ModifierKeys.None)
            {
                _terminal.Write("\x1b[H");
                _inputCursorPos = 0;
                e.Handled = true;
                return;
            }
            if (e.Key == Key.End && Keyboard.Modifiers == ModifierKeys.None)
            {
                _terminal.Write("\x1b[F");
                _inputCursorPos = _currentInput.Length;
                e.Handled = true;
                return;
            }

            // --- Other special keys: Tab, Escape, function keys, etc. ---
            if (e.Key == Key.Tab)
            {
                _terminal.Write("\t");
                // Tab completion changes input unpredictably; reset tracking
                ResetInputTracking();
                e.Handled = true;
                return;
            }

            // Ctrl + letter (except C, V, L already handled above)
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key >= Key.A && e.Key <= Key.Z)
            {
                char c = (char)(e.Key - Key.A + 1);
                _terminal.Write(c.ToString());
                if (e.Key == Key.U) // Ctrl+U clears line
                    ResetInputTracking();
                e.Handled = true;
                return;
            }

            // Remaining keys via VT sequence mapping
            string? seq = KeyToVTSequence(e.Key);
            if (seq != null)
            {
                _terminal.Write(seq);
                e.Handled = true;
            }
        }

        private void TerminalScrollViewer_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (ImeProxy.IsFocused) return;

            if (_terminal == null || !_isShellRunning) return;

            if (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (Clipboard.ContainsText())
                {
                    var text = Clipboard.GetText();
                    _terminal.Write(text);
                    _currentInput.Insert(_inputCursorPos, text);
                    _inputCursorPos += text.Length;
                    e.Handled = true;
                }
                return;
            }

            // Redirect to ImeProxy for consistent handling
            ImeProxy.Focus();
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
            ImeProxy.Focus();
        }

        private void TerminalScrollViewer_GotFocus(object sender, RoutedEventArgs e)
        {
            if (!ImeProxy.IsFocused)
                ImeProxy.Focus();
        }

        private void ButtonNewShell_Click(object sender, RoutedEventArgs e)
        {
            StartShell();
            ImeProxy.Focus();
        }

        private void ButtonClear_Click(object sender, RoutedEventArgs e)
        {
            ClearOutput();
        }

        private void ButtonKill_Click(object sender, RoutedEventArgs e)
        {
            KillShell();
            AppendOutput("[终端已终止]\r\n");
        }

        private void ShellSelector_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (ShellSelector?.SelectedIndex == 1)
                _currentShell = "cmd";
            else
                _currentShell = "powershell";
        }

        #endregion
    }
}
