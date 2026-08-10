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
        private readonly Queue<(int SessionId, string Text)> _pendingOutput = new();
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

        public void StartShell(string? workingDirectory = null, string? startupCommand = null)
        {
            if (_disposed)
                return;

            // StartShell may be called by RunScript before the newly-created dock panel reaches
            // Loaded. Mark this as the initial shell so the queued Loaded callback cannot restart
            // the shell and clear the first script's output.
            MarkInitialShellStarted();
            StopShell(TerminalLifecycleState.Killed);
            ResetTrackedInput();

            var terminalSize = CalculateTerminalSize();
            _terminalCols = terminalSize.cols;
            _terminalRows = terminalSize.rows;
            _screenBuffer = new TerminalScreenBuffer(_terminalCols, _terminalRows);
            int sessionId = ++_terminalSessionId;
            ClearOutput(sessionId);

            workingDirectory ??= GetDefaultWorkingDirectory();

            string commandLine;
            if (_currentShell == "cmd")
                commandLine = startupCommand == null ? "cmd.exe" : $"cmd.exe /K {startupCommand}";
            else
                commandLine = startupCommand == null
                    ? "powershell.exe -NoLogo -NoExit"
                    : $"powershell.exe -NoLogo -NoExit -EncodedCommand {Convert.ToBase64String(Encoding.Unicode.GetBytes(startupCommand))}";

            try
            {
                SetTerminalState(TerminalLifecycleState.Starting);
                _terminal = new ConPtyTerminal();
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

        public void SendCommand(string command, string workingDirectory)
        {
            if (string.IsNullOrWhiteSpace(workingDirectory))
            {
                SendCommand(command);
                return;
            }

            string changeDirectoryCommand = _currentShell == "cmd"
                ? $"cd /d \"{workingDirectory.Replace("\"", "\"\"")}\" && {command}"
                : $"Set-Location -LiteralPath '{workingDirectory.Replace("'", "''")}'; {command}";
            SendCommand(changeDirectoryCommand);
        }

        public void SendCommandBatch(IReadOnlyList<TerminalCommandRequest> commands)
        {
            if (commands.Count == 0)
                return;

            SendCommand(BuildBatchCommand(commands, _currentShell));
        }

        internal static string BuildBatchCommand(IReadOnlyList<TerminalCommandRequest> commands, string shell)
        {
            if (string.Equals(shell, "cmd", StringComparison.OrdinalIgnoreCase))
            {
                return string.Join(" && ", commands.Select(command =>
                    $"cd /d \"{command.WorkingDirectory.Replace("\"", "\"\"")}\"" +
                    $" && echo ==== {EscapeCmdEcho(command.DisplayName)} ====" +
                    $" && call {command.Command}"));
            }

            var commandBuilder = new StringBuilder("& { $ErrorActionPreference = 'Stop'; ");
            foreach (TerminalCommandRequest command in commands)
            {
                string displayName = command.DisplayName.Replace("'", "''");
                string workingDirectory = command.WorkingDirectory.Replace("'", "''");
                commandBuilder.Append($"Write-Host '==== {displayName} ===='; ");
                commandBuilder.Append($"Set-Location -LiteralPath '{workingDirectory}'; ");
                commandBuilder.Append($"& {{ {command.Command} }}; ");
                commandBuilder.Append($"if (-not $?) {{ throw '命令失败: {displayName}' }}; ");
            }
            commandBuilder.Append("}");
            return commandBuilder.ToString();
        }

        private static string EscapeCmdEcho(string value)
        {
            return value
                .Replace("^", "^^")
                .Replace("&", "^&")
                .Replace("|", "^|")
                .Replace("<", "^<")
                .Replace(">", "^>");
        }

        public void RunScript(string filePath)
        {
            if (!File.Exists(filePath))
            {
                AppendOutput($"文件不存在: {filePath}\r\n");
                return;
            }

            string fullPath = Path.GetFullPath(filePath);
            string dir = Path.GetDirectoryName(fullPath) ?? "";
            string command = BuildScriptStartupCommand(fullPath, _currentShell);

            // Script execution starts a fresh interactive shell with the script as its startup
            // command. This is reliable even before a newly-created ConPTY prompt is ready and
            // leaves the shell open for inspection and follow-up commands.
            StartShell(dir, command);
        }

        internal static string BuildScriptStartupCommand(string filePath, string shell)
        {
            string powerShellCommand = BuildPowerShellScriptCommand(filePath);
            if (!string.Equals(shell, "cmd", StringComparison.OrdinalIgnoreCase))
                return powerShellCommand;

            string encodedPowerShellCommand = Convert.ToBase64String(Encoding.Unicode.GetBytes(powerShellCommand));
            return $"powershell.exe -NoLogo -NoProfile -EncodedCommand {encodedPowerShellCommand}";
        }

        private static string BuildPowerShellScriptCommand(string filePath)
        {
            string encodedPath = Convert.ToBase64String(Encoding.Unicode.GetBytes(filePath));
            string loadPath = "$scriptPath = [System.Text.Encoding]::Unicode.GetString(" +
                $"[System.Convert]::FromBase64String('{encodedPath}')); ";
            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            string invocation = extension switch
            {
                ".py" or ".pyw" => "& python -- $scriptPath",
                ".ps1" => "& powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File $scriptPath",
                ".sh" => "& bash -- $scriptPath",
                ".js" => "& node -- $scriptPath",
                _ => "& $scriptPath",
            };

            string invokeAndCaptureResult;
            if (extension is ".bat" or ".cmd")
            {
                // cmd.exe reparses its /C payload, including percent expansion inside quoted
                // paths. Expand one fixed environment-variable reference instead: cmd performs
                // only one expansion pass, so metacharacters in the variable value stay literal.
                invokeAndCaptureResult =
                    "$scriptPathVariableExisted = Test-Path -LiteralPath Env:COLORVISION_SCRIPT_PATH; " +
                    "$previousScriptPathVariable = $env:COLORVISION_SCRIPT_PATH; " +
                    "try { " +
                    "$env:COLORVISION_SCRIPT_PATH = $scriptPath; " +
                    "$batchProcess = Start-Process -FilePath $env:ComSpec " +
                    "-ArgumentList '/D /V:OFF /S /C \"\"%COLORVISION_SCRIPT_PATH%\"\"' " +
                    "-NoNewWindow -Wait -PassThru -ErrorAction Stop; " +
                    "$scriptExitCode = $batchProcess.ExitCode; $scriptSucceeded = $scriptExitCode -eq 0; " +
                    "} catch { " +
                    "Write-Error $_; $scriptSucceeded = $false; $scriptExitCode = 1; " +
                    "} finally { " +
                    "if ($scriptPathVariableExisted) { $env:COLORVISION_SCRIPT_PATH = $previousScriptPathVariable } " +
                    "else { Remove-Item -LiteralPath Env:COLORVISION_SCRIPT_PATH -ErrorAction SilentlyContinue } " +
                    "}";
            }
            else
            {
                invokeAndCaptureResult = invocation
                    + "; $scriptSucceeded = $?; $scriptExitCode = $LASTEXITCODE";
            }

            return loadPath
                + "Write-Host ('▶ ' + $scriptPath); "
                + "$global:LASTEXITCODE = $null; "
                + invokeAndCaptureResult
                + "; "
                + "if ($null -eq $scriptExitCode) { $scriptExitCode = if ($scriptSucceeded) { 0 } else { 1 } }; "
                + "if (-not $scriptSucceeded -and $scriptExitCode -eq 0) { $scriptExitCode = 1 }; "
                + "Write-Host (\"`n[进程已结束，退出代码: {0}]\" -f $scriptExitCode)";
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
                _pendingOutput.Enqueue((sessionId, text));
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

                if (_disposed)
                    return;

                if (sessionId != _terminalSessionId)
                {
                    QueueOutputFlush(_terminalSessionId);
                    return;
                }

                FlushPendingOutput(sessionId);
            });
        }

        private void FlushPendingOutput(int sessionId)
        {
            string rawText;
            lock (_outputLock)
            {
                if (_pendingOutput.Count == 0) return;
                var sb = new StringBuilder();
                while (_pendingOutput.Count > 0)
                {
                    var pendingOutput = _pendingOutput.Dequeue();
                    if (pendingOutput.SessionId == sessionId)
                        sb.Append(pendingOutput.Text);
                }
                rawText = sb.ToString();
            }

            if (rawText.Length == 0)
                return;

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
            ClearOutput(_terminalSessionId);
        }

        private void ClearOutput(int sessionId)
        {
            lock (_outputLock)
            {
                _pendingOutput.Clear();
            }
            _screenBuffer.Clear();
            Dispatcher.BeginInvoke(() =>
            {
                if (_disposed || sessionId != _terminalSessionId)
                    return;

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
