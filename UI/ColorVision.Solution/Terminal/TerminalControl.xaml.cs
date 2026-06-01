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
    public partial class TerminalControl : UserControl, IDisposable
    {
        private enum TerminalLifecycleState
        {
            Starting,
            Running,
            Exited,
            Killed,
            Disposed
        }

        private const short DefaultTerminalColumns = 120;
        private const short DefaultTerminalRows = 30;
        private const short MinTerminalColumns = 20;
        private const short MinTerminalRows = 5;
        private const short MaxTerminalColumns = 300;
        private const short MaxTerminalRows = 200;

        private static readonly ILog log = LogManager.GetLogger(typeof(TerminalControl));

        private ConPtyTerminal? _terminal;
        private TerminalScreenBuffer _screenBuffer = new(DefaultTerminalColumns, DefaultTerminalRows);
        private readonly object _outputLock = new();
        private readonly Queue<string> _pendingOutput = new();
        private int _flushQueued;
        private TerminalLifecycleState _terminalState = TerminalLifecycleState.Exited;
        private bool _disposed;
        private bool _initialShellStarted;
        private bool _initialShellStartQueued;
        private string _currentShell = "powershell";
        private short _terminalCols = DefaultTerminalColumns;
        private short _terminalRows = DefaultTerminalRows;
        private bool _resizeQueued;
        private int _terminalSessionId;
        private Action<string>? _outputReceivedHandler;
        private Action<int>? _processExitedHandler;

        // Input tracking for command history
        private readonly CommandHistory _commandHistory = new();
        private StringBuilder _currentInput = new();
        private int _inputCursorPos;

        internal bool IsDisposed => _disposed;

        private bool CanSendInput => _terminal != null && _terminalState == TerminalLifecycleState.Running;

        public TerminalControl()
        {
            InitializeComponent();

            TerminalDisplay.UrlClicked += OnUrlClicked;
            IsVisibleChanged += TerminalControl_IsVisibleChanged;
            LayoutUpdated += TerminalControl_LayoutUpdated;
            UpdateTerminalStatus();
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
            QueueInitialShellStart();
            QueueResizeToViewport();
        }

        private void TerminalControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
            {
                QueueInitialShellStart();
                QueueResizeToViewport();
            }
        }

        private void TerminalControl_LayoutUpdated(object? sender, EventArgs e)
        {
            if (_initialShellStarted || _disposed)
                return;

            QueueInitialShellStart();
        }

        private void QueueInitialShellStart()
        {
            if (_disposed || _initialShellStarted || _initialShellStartQueued)
                return;

            _initialShellStartQueued = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
            {
                _initialShellStartQueued = false;
                if (_disposed || _initialShellStarted)
                    return;

                if (!CanStartShellForVisibleControl())
                    return;

                MarkInitialShellStarted();
                StartShell();
            });
        }

        private bool CanStartShellForVisibleControl()
        {
            return IsLoaded && IsVisible;
        }

        private void MarkInitialShellStarted()
        {
            _initialShellStarted = true;
            LayoutUpdated -= TerminalControl_LayoutUpdated;
        }

        private void FocusTerminalInput(bool force = false)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
            {
                if (_disposed || !IsVisible)
                    return;

                if (TerminalRoot.ContextMenu?.IsOpen == true)
                    return;

                if (!force && !TerminalRoot.IsKeyboardFocusWithin)
                    return;

                TerminalScrollViewer.Focus();
                ImeProxy.Focus();
                Keyboard.Focus(ImeProxy);
            });
        }

        public void NotifyPanelActivated()
        {
            if (_disposed)
                return;

            QueueResizeToViewport();

            if (!CanSendInput)
            {
                if (CanStartShellForVisibleControl() &&
                    (_terminalState is TerminalLifecycleState.Exited or TerminalLifecycleState.Killed))
                {
                    MarkInitialShellStarted();
                    StartShell();
                }
                else
                {
                    QueueInitialShellStart();
                }
            }
        }

        public void StartShell(string? workingDirectory = null)
        {
            if (_disposed)
                return;

            StopShell(TerminalLifecycleState.Killed);
            ResetTrackedInput();

            var terminalSize = CalculateTerminalSize();
            _terminalCols = terminalSize.cols;
            _terminalRows = terminalSize.rows;
            _screenBuffer = new TerminalScreenBuffer(_terminalCols, _terminalRows);
            ClearOutput();

            workingDirectory ??= GetDefaultWorkingDirectory();

            string commandLine;
            if (_currentShell == "cmd")
                commandLine = "cmd.exe";
            else
                commandLine = "powershell.exe -NoLogo -NoExit";

            try
            {
                SetTerminalState(TerminalLifecycleState.Starting);
                _terminal = new ConPtyTerminal();
                int sessionId = ++_terminalSessionId;
                _outputReceivedHandler = text => OnConPtyOutput(sessionId, text);
                _processExitedHandler = exitCode => OnConPtyExited(sessionId, exitCode);
                _terminal.OutputReceived += _outputReceivedHandler;
                _terminal.ProcessExited += _processExitedHandler;
                _terminal.Start(commandLine, workingDirectory, _terminalCols, _terminalRows);
                SetTerminalState(TerminalLifecycleState.Running);

                log.Info($"Terminal: ConPTY started ({commandLine}) in {workingDirectory}");
            }
            catch (Exception ex)
            {
                ReleaseTerminal();
                SetTerminalState(TerminalLifecycleState.Exited);
                AppendOutput($"启动终端失败: {ex.Message}\r\n");
                log.Error("Terminal: failed to start ConPTY shell", ex);
            }
        }

        public void SendCommand(string command)
        {
            if (_disposed)
                return;

            if (!CanSendInput)
                StartShell();

            SendText(command + "\r\n", trackInput: false);
            ResetTrackedInput();
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
            if (SendControlSequence("\x03"))
                ResetTrackedInput();
        }

        private void OnConPtyOutput(int sessionId, string text)
        {
            if (sessionId != _terminalSessionId)
                return;

            // Queue raw VT100 output for processing by TerminalScreenBuffer
            lock (_outputLock)
            {
                _pendingOutput.Enqueue(text);
            }
            QueueOutputFlush(sessionId);
        }

        private void OnConPtyExited(int sessionId, int exitCode)
        {
            // Write exit message directly into the screen buffer on UI thread
            Dispatcher.BeginInvoke(() =>
            {
                if (_disposed || sessionId != _terminalSessionId || _terminalState == TerminalLifecycleState.Killed)
                    return;

                ReleaseTerminal();
                SetTerminalState(TerminalLifecycleState.Exited);

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

        private void QueueOutputFlush(int sessionId)
        {
            if (_disposed || Interlocked.Exchange(ref _flushQueued, 1) == 1)
                return;

            Dispatcher.BeginInvoke(DispatcherPriority.Normal, () =>
            {
                Interlocked.Exchange(ref _flushQueued, 0);

                if (_disposed || sessionId != _terminalSessionId)
                    return;

                FlushPendingOutput();
            });
        }

        private void FlushPendingOutput()
        {
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
            TerminalScrollViewer.InvalidateScrollInfo();

            // Scroll to cursor after layout completes (Loaded priority runs after Render/Layout)
            Dispatcher.BeginInvoke(DispatcherPriority.Render, () =>
            {
                if (_disposed)
                    return;

                ScrollToCursor();
                UpdateImeProxyPosition();
                TerminalDisplay.InvalidateVisual();
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
            Dispatcher.BeginInvoke(() =>
            {
                TerminalDisplay.UpdateContent(new List<TerminalLine>(), 0, 0);
                TerminalScrollViewer.ScrollToHome();
            });
        }

        private void KillShell()
        {
            StopShell(TerminalLifecycleState.Killed);
        }

        private void StopShell(TerminalLifecycleState finalState)
        {
            _terminalSessionId++;

            if (_terminal != null)
            {
                try
                {
                    _terminal.Kill();
                }
                catch (Exception ex)
                {
                    log.Warn($"Terminal: kill shell failed: {ex.Message}");
                }
                ReleaseTerminal();
            }

            if (_terminalState != TerminalLifecycleState.Disposed)
                SetTerminalState(finalState);

            ResetTrackedInput();
        }

        private void ReleaseTerminal()
        {
            if (_terminal == null)
                return;

            if (_outputReceivedHandler != null)
                _terminal.OutputReceived -= _outputReceivedHandler;
            if (_processExitedHandler != null)
                _terminal.ProcessExited -= _processExitedHandler;
            _outputReceivedHandler = null;
            _processExitedHandler = null;
            _terminal.Dispose();
            _terminal = null;
        }

        private string GetDefaultWorkingDirectory()
        {
            var dirInfo = SolutionManager.GetInstance()?.CurrentSolutionExplorer?.DirectoryInfo;
            if (dirInfo?.Exists == true)
                return dirInfo.FullName;
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        private void SetTerminalState(TerminalLifecycleState state)
        {
            _terminalState = state;
            UpdateCommandStates();
            UpdateTerminalStatus();
        }

        private void UpdateCommandStates()
        {
            if (MenuNewPowerShell == null)
                return;

            bool disposed = _terminalState == TerminalLifecycleState.Disposed;
            bool canStop = _terminalState is TerminalLifecycleState.Starting or TerminalLifecycleState.Running;

            MenuNewPowerShell.IsEnabled = !disposed;
            MenuNewCmd.IsEnabled = !disposed;
            MenuClear.IsEnabled = !disposed;
            MenuKill.IsEnabled = canStop;
        }

        private void UpdateTerminalStatus()
        {
            if (MenuStatus == null)
                return;

            string shell = _currentShell == "cmd" ? "CMD" : "PowerShell";
            MenuStatus.Header = $"{shell} / {_terminalState}";
        }

        private void TerminalScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            QueueInitialShellStart();
            QueueResizeToViewport();
        }

        private void QueueResizeToViewport()
        {
            if (_disposed || _resizeQueued)
                return;

            _resizeQueued = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
            {
                _resizeQueued = false;
                SynchronizeTerminalSize();
            });
        }

        private void SynchronizeTerminalSize()
        {
            if (_disposed)
                return;

            var terminalSize = CalculateTerminalSize();
            if (terminalSize.cols == _terminalCols && terminalSize.rows == _terminalRows)
                return;

            _terminalCols = terminalSize.cols;
            _terminalRows = terminalSize.rows;
            _screenBuffer.Resize(_terminalCols, _terminalRows);

            if (_terminal != null && _terminalState is TerminalLifecycleState.Starting or TerminalLifecycleState.Running)
            {
                try
                {
                    _terminal.Resize(_terminalCols, _terminalRows);
                }
                catch (Exception ex)
                {
                    log.Debug($"Terminal: resize failed: {ex.Message}");
                }
            }

            RenderBuffer();
        }

        private (short cols, short rows) CalculateTerminalSize()
        {
            double width = TerminalScrollViewer.ViewportWidth;
            if (double.IsNaN(width) || double.IsInfinity(width) || width <= 0)
                width = TerminalScrollViewer.ActualWidth;

            double height = TerminalScrollViewer.ViewportHeight;
            if (double.IsNaN(height) || double.IsInfinity(height) || height <= 0)
                height = TerminalScrollViewer.ActualHeight;

            double charWidth = Math.Max(1, TerminalDisplay.CharWidth);
            double lineHeight = Math.Max(1, TerminalDisplay.LineHeight);

            if (width <= 0 || height <= 0)
                return (_terminalCols, _terminalRows);

            int cols = Math.Clamp((int)Math.Floor(width / charWidth), MinTerminalColumns, MaxTerminalColumns);
            int rows = Math.Clamp((int)Math.Floor(height / lineHeight), MinTerminalRows, MaxTerminalRows);
            return ((short)cols, (short)rows);
        }

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

        #region UI Event Handlers

        /// <summary>
        /// Forward printable text input directly to ConPTY and track it.
        /// </summary>
        private void OutputTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (SendText(e.Text))
            {
                e.Handled = true;
            }
        }

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

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            SetTerminalState(TerminalLifecycleState.Disposed);
            Interlocked.Exchange(ref _flushQueued, 0);
            TerminalDisplay.UrlClicked -= OnUrlClicked;
            IsVisibleChanged -= TerminalControl_IsVisibleChanged;
            LayoutUpdated -= TerminalControl_LayoutUpdated;
            StopShell(TerminalLifecycleState.Disposed);
            TerminalService.GetInstance().ClearTerminalControl(this);
            GC.SuppressFinalize(this);
        }
    }
}
