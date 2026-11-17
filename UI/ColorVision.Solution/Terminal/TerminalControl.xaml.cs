#pragma warning disable CA1822,CA1834
using ColorVision.Common.MVVM;
using ColorVision.UI.Docking;
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
    public partial class TerminalControl : UserControl, IDisposable, IDockPanelTitleActionProvider
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
        private const short MaxTerminalColumns = 1000;
        private const short MaxTerminalRows = 500;
        private const int MaxQueuedOutputChars = 256 * 1024;
        private const int OutputBatchChars = 16 * 1024;

        private enum UpdateKind { Text, Exit, Resize, Clear }

        private readonly record struct PendingUpdate(UpdateKind Kind, string? Text = null, int ExitCode = 0);

        private static readonly ILog log = LogManager.GetLogger(typeof(TerminalControl));

        private ConPtyTerminal? _terminal;
        private TerminalScreenBuffer _screenBuffer = new(DefaultTerminalColumns, DefaultTerminalRows);
        private readonly object _outputLock = new();
        private readonly Queue<PendingUpdate> _pendingOutput = new();
        private readonly bool _isProjectTerminal;
        private int _queuedOutputChars;
        private bool _flushQueued;
        private TerminalLifecycleState _terminalState = TerminalLifecycleState.Exited;
        private bool _disposed;
        private bool _initialShellStarted;
        private bool _initialShellStartQueued;
        private string _currentShell = "powershell";
        private short _terminalCols = DefaultTerminalColumns;
        private short _terminalRows = DefaultTerminalRows;
        private bool _resizeQueued;
        private bool _resizeAfterOutput;
        private int _terminalSessionId;
        private Action<string>? _outputReceivedHandler;
        private Action<int>? _processExitedHandler;
        private readonly Application? _application;

        internal bool IsDisposed => _disposed;
        internal string CurrentShell => _currentShell;
        public IReadOnlyList<DockPanelTitleAction> TitleActions { get; }

        private bool CanSendInput => _terminal != null &&
            _terminalState is TerminalLifecycleState.Starting or TerminalLifecycleState.Running;

        public TerminalControl() : this(isProjectTerminal: false)
        {
        }

        internal TerminalControl(bool isProjectTerminal)
        {
            _isProjectTerminal = isProjectTerminal;
            TitleActions =
            [
                new(new RelayCommand(_ => TerminalService.GetInstance().NewTerminal()), "\uE710", Properties.Resources.Terminal_NewTerminal),
                new(new RelayCommand<UIElement>(anchor =>
                {
                    if (anchor != null)
                        OpenActionsMenu(anchor);
                }, anchor => anchor != null && !_disposed), "\uE713", Properties.Resources.Sol_Miv_Settings) { PassButtonAsParameter = true }
            ];
            InitializeComponent();

            _application = Application.Current;
            if (_application != null)
                _application.Exit += Application_Exit;

            if (_isProjectTerminal)
            {
                MenuNewPowerShell.Visibility = Visibility.Collapsed;
                MenuNewCmd.Visibility = Visibility.Collapsed;
            }

            TerminalDisplay.UrlClicked += OnUrlClicked;
            TerminalDisplay.FontMetricsChanged += QueueResizeToViewport;
            TerminalDisplay.InputRequested += OnTerminalInputRequested;
            IsVisibleChanged += TerminalControl_IsVisibleChanged;
            TerminalScrollViewer.ScrollChanged += TerminalScrollViewer_ScrollChanged;
            TerminalRoot.IsKeyboardFocusWithinChanged += (_, _) =>
                TerminalDisplay.SetInputFocus(TerminalRoot.IsKeyboardFocusWithin);
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
                RenderBuffer();
            }
        }

        private void QueueInitialShellStart()
        {
            if (_disposed || _isProjectTerminal || _initialShellStarted || _initialShellStartQueued)
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

        private void MarkInitialShellStarted() => _initialShellStarted = true;

        private void FocusTerminalInput(bool force = false)
        {
            if (ImeProxy.IsKeyboardFocused)
                return;

            Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
            {
                if (_disposed || !IsVisible)
                    return;

                if (TerminalRoot.ContextMenu?.IsOpen == true)
                    return;

                if (!force && !TerminalRoot.IsKeyboardFocusWithin)
                    return;

                Keyboard.Focus(ImeProxy);
            });
        }

        private void OnTerminalInputRequested() => FocusTerminalInput(force: true);

        public void NotifyPanelActivated()
        {
            if (_disposed)
                return;

            QueueResizeToViewport();
            QueueInitialShellStart();
        }

        public void StartShell(string? workingDirectory = null, string? startupCommand = null)
        {
            if (_isProjectTerminal)
                return;

            string commandLine;
            if (_currentShell == "cmd")
                commandLine = startupCommand == null ? "cmd.exe" : $"cmd.exe /K {startupCommand}";
            else
                commandLine = startupCommand == null
                    ? "powershell.exe -NoLogo -NoExit"
                    : $"powershell.exe -NoLogo -NoExit -EncodedCommand {Convert.ToBase64String(Encoding.Unicode.GetBytes(startupCommand))}";

            _ = StartSessionAsync(commandLine, workingDirectory);
        }

        internal bool TryStartProjectCommand(string shell, string commandLine, string? workingDirectory)
        {
            if (!_isProjectTerminal || _disposed || CanSendInput)
                return false;

            _currentShell = shell;
            _ = StartSessionAsync(commandLine, workingDirectory);
            return true;
        }

        private async Task StartSessionAsync(string commandLine, string? workingDirectory)
        {
            if (_disposed)
                return;

            // StartShell may be called by RunScript before the newly-created dock panel reaches
            // Loaded. Mark this as the initial shell so the queued Loaded callback cannot restart
            // the shell and clear the first script's output.
            MarkInitialShellStarted();
            StopShell(TerminalLifecycleState.Killed);

            var terminalSize = CalculateTerminalSize();
            _terminalCols = terminalSize.cols;
            _terminalRows = terminalSize.rows;
            _screenBuffer = new TerminalScreenBuffer(_terminalCols, _terminalRows);
            int sessionId = _terminalSessionId;
            TerminalDisplay.ResetViewport();
            RenderBuffer();

            var terminal = new ConPtyTerminal();
            _terminal = terminal;
            _screenBuffer.ResponseReady += response => terminal.Write(response);
            _outputReceivedHandler = text => OnConPtyOutput(sessionId, text);
            _processExitedHandler = exitCode => OnConPtyExited(sessionId, exitCode);
            terminal.OutputReceived += _outputReceivedHandler;
            terminal.ProcessExited += _processExitedHandler;
            SetTerminalState(TerminalLifecycleState.Starting);
            try
            {
                workingDirectory ??= GetDefaultWorkingDirectory();
                await terminal.StartAsync(commandLine, workingDirectory, _terminalCols, _terminalRows);
                if (ReferenceEquals(_terminal, terminal))
                {
                    SetTerminalState(TerminalLifecycleState.Running);
                    log.Info($"Terminal: ConPTY started ({_currentShell}) in {workingDirectory}");
                }
            }
            catch (Exception ex)
            {
                if (!ReferenceEquals(_terminal, terminal))
                    return;

                ReleaseTerminal();
                SetTerminalState(TerminalLifecycleState.Exited);
                AppendOutput($"启动终端失败: {ex.Message}\r\n");
                log.Error("Terminal: failed to start ConPTY shell", ex);
            }
        }

        public void RunScript(string filePath)
        {
            if (!File.Exists(filePath))
            {
                MarkInitialShellStarted();
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
            if (CanSendInput)
                SendTerminalInput("\x03");
        }

        private void OnConPtyOutput(int sessionId, string text)
        {
            lock (_outputLock)
            {
                while (sessionId == _terminalSessionId && _queuedOutputChars >= MaxQueuedOutputChars)
                    Monitor.Wait(_outputLock);

                if (sessionId != _terminalSessionId)
                    return;

                _pendingOutput.Enqueue(new PendingUpdate(UpdateKind.Text, text));
                _queuedOutputChars += text.Length;
                QueueOutputFlush();
            }
        }

        private void OnConPtyExited(int sessionId, int exitCode)
        {
            lock (_outputLock)
            {
                if (sessionId != _terminalSessionId)
                    return;

                _pendingOutput.Enqueue(new PendingUpdate(UpdateKind.Exit, ExitCode: exitCode));
                QueueOutputFlush();
            }
        }

        private void AppendOutput(string text)
        {
            lock (_outputLock)
            {
                _pendingOutput.Enqueue(new PendingUpdate(UpdateKind.Text, text));
                _queuedOutputChars += text.Length;
                QueueOutputFlush();
            }
        }

        // Called under _outputLock. One bounded batch per dispatcher operation lets input and
        // layout run while a process continues to print.
        private void QueueOutputFlush()
        {
            if (_flushQueued)
                return;

            _flushQueued = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Background, FlushPendingOutput);
        }

        private void FlushPendingOutput()
        {
            bool changed = false;
            int processedChars = 0;
            while (!_disposed && processedChars < OutputBatchChars)
            {
                PendingUpdate pending;
                lock (_outputLock)
                {
                    if (!_pendingOutput.TryDequeue(out pending))
                        break;
                    _queuedOutputChars -= pending.Text?.Length ?? 0;
                    Monitor.Pulse(_outputLock);
                }

                switch (pending.Kind)
                {
                    case UpdateKind.Resize:
                        _resizeAfterOutput = false;
                        changed |= ApplyViewportSize();
                        break;
                    case UpdateKind.Clear:
                        changed |= ApplyClearOutput();
                        break;
                    case UpdateKind.Text:
                        _screenBuffer.Write(pending.Text!);
                        processedChars += pending.Text!.Length;
                        changed = true;
                        break;
                    case UpdateKind.Exit:
                        ReleaseTerminal();
                        SetTerminalState(TerminalLifecycleState.Exited);
                        _screenBuffer.Write($"\r\n[终端已退出, 退出代码: {pending.ExitCode}]\r\n");
                        changed = true;
                        break;
                }
            }

            lock (_outputLock)
            {
                _flushQueued = false;
                if (_pendingOutput.Count != 0)
                    QueueOutputFlush();
            }

            if (changed && IsVisible)
                RenderBuffer();
        }

        private void RenderBuffer()
        {
            var (lines, cursorLine, cursorCol) = _screenBuffer.RenderLines();
            TerminalDisplay.UpdateContent(lines, cursorLine, cursorCol,
                _screenBuffer.CursorVisible && CanSendInput, _screenBuffer.IsAlternateScreen,
                _screenBuffer.ScrollbackDiscarded, _screenBuffer.ScrollbackCount, _screenBuffer.Cols);
            UpdateImeProxyPosition();
        }

        private void ClearOutput()
        {
            if (!QueueAfterPendingOutput(UpdateKind.Clear) && ApplyClearOutput())
                RenderBuffer();
        }

        private bool ApplyClearOutput()
        {
            if (CanSendInput && _screenBuffer.IsAlternateScreen)
                return false;

            if (CanSendInput)
                _screenBuffer.ClearDisplayedOutput();
            else
                _screenBuffer.Clear();
            TerminalDisplay.ResetViewport();
            return true;
        }

        private void KillShell()
        {
            StopShell(TerminalLifecycleState.Killed);
        }

        private void StopShell(TerminalLifecycleState finalState)
        {
            lock (_outputLock)
            {
                _terminalSessionId++;
                _pendingOutput.Clear();
                _queuedOutputChars = 0;
                _resizeAfterOutput = false;
                Monitor.PulseAll(_outputLock);
            }
            ReleaseTerminal();

            if (_terminalState != TerminalLifecycleState.Disposed)
                SetTerminalState(finalState);
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

        internal static string GetDefaultWorkingDirectory()
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
            MenuClear.IsEnabled = !disposed && (!CanSendInput || !_screenBuffer.IsAlternateScreen);
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

        private void TerminalScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.ViewportWidthChange != 0 || e.ViewportHeightChange != 0)
                QueueResizeToViewport();
            UpdateImeProxyPosition();
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
            if (_disposed || !IsVisible || _resizeAfterOutput)
                return;

            // Bytes already received were produced at the previous terminal width.
            // Leave the resize behind those chunks.
            _resizeAfterOutput = QueueAfterPendingOutput(UpdateKind.Resize);
            if (!_resizeAfterOutput && ApplyViewportSize())
                RenderBuffer();
        }

        private bool QueueAfterPendingOutput(UpdateKind kind)
        {
            lock (_outputLock)
            {
                if (_pendingOutput.Count == 0)
                    return false;
                _pendingOutput.Enqueue(new PendingUpdate(kind));
                QueueOutputFlush();
                return true;
            }
        }

        private bool ApplyViewportSize()
        {
            if (_disposed || !IsVisible)
                return false;

            var terminalSize = CalculateTerminalSize();
            if (terminalSize.cols == _terminalCols && terminalSize.rows == _terminalRows)
                return false;

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

            return true;
        }

        private (short cols, short rows) CalculateTerminalSize()
        {
            double width = TerminalDisplay.ViewportWidth;
            if (double.IsNaN(width) || double.IsInfinity(width) || width <= 0)
                width = TerminalScrollViewer.ActualWidth;

            double height = TerminalDisplay.ViewportHeight;
            if (double.IsNaN(height) || double.IsInfinity(height) || height <= 0)
                height = TerminalScrollViewer.ActualHeight;

            double charWidth = Math.Max(1, TerminalDisplay.CharWidth);
            double lineHeight = Math.Max(1, TerminalDisplay.LineHeight);

            if (width <= 0 || height <= 0)
                return (_terminalCols, _terminalRows);

            int cols = Math.Clamp((int)Math.Floor(width / charWidth), 1, MaxTerminalColumns);
            int rows = Math.Clamp((int)Math.Floor(height / lineHeight), 1, MaxTerminalRows);
            return ((short)cols, (short)rows);
        }


        private void Application_Exit(object sender, ExitEventArgs e) => Dispose();

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            if (_application != null)
                _application.Exit -= Application_Exit;
            SetTerminalState(TerminalLifecycleState.Disposed);
            TerminalDisplay.UrlClicked -= OnUrlClicked;
            TerminalDisplay.FontMetricsChanged -= QueueResizeToViewport;
            TerminalDisplay.InputRequested -= OnTerminalInputRequested;
            IsVisibleChanged -= TerminalControl_IsVisibleChanged;
            TerminalScrollViewer.ScrollChanged -= TerminalScrollViewer_ScrollChanged;
            StopShell(TerminalLifecycleState.Disposed);
            TerminalService.GetInstance().ClearTerminalControl(this);
            GC.SuppressFinalize(this);
        }
    }
}
