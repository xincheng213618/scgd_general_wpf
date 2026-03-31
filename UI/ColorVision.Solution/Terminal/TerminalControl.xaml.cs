using log4net;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Solution.Terminal
{
    public partial class TerminalControl : UserControl
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(TerminalControl));

        private Process? _shellProcess;
        private StreamWriter? _shellInput;
        private readonly StringBuilder _outputBuffer = new();
        private readonly object _outputLock = new();
        private readonly List<string> _commandHistory = new();
        private int _historyIndex = -1;
        private bool _isShellRunning;
        private string _currentShell = "powershell";

        private const int MaxOutputLength = 500_000;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GenerateConsoleCtrlEvent(uint dwCtrlEvent, uint dwProcessGroupId);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(uint dwProcessId);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeConsole();
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleCtrlHandler(IntPtr handlerRoutine, bool add);
        private const uint CTRL_C_EVENT = 0;

        public TerminalControl()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_isShellRunning)
                StartShell();
            InputTextBox.Focus();
        }

        public void StartShell(string? workingDirectory = null)
        {
            KillShell();
            ClearOutput();

            workingDirectory ??= GetDefaultWorkingDirectory();

            string shellExe;
            string shellArgs;

            if (_currentShell == "cmd")
            {
                shellExe = "cmd.exe";
                shellArgs = "";
            }
            else
            {
                shellExe = "powershell.exe";
                shellArgs = "-NoLogo -NoExit";
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = shellExe,
                    Arguments = shellArgs,
                    WorkingDirectory = workingDirectory,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                };

                _shellProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };
                _shellProcess.OutputDataReceived += OnOutputDataReceived;
                _shellProcess.ErrorDataReceived += OnErrorDataReceived;
                _shellProcess.Exited += OnProcessExited;

                _shellProcess.Start();
                _shellProcess.BeginOutputReadLine();
                _shellProcess.BeginErrorReadLine();

                _shellInput = _shellProcess.StandardInput;
                _shellInput.AutoFlush = true;
                _isShellRunning = true;

                // Force shell to use UTF-8 output encoding
                if (_currentShell == "cmd")
                    _shellInput.WriteLine("chcp 65001 >nul");
                else
                    _shellInput.WriteLine("[Console]::OutputEncoding = [System.Text.Encoding]::UTF8; $OutputEncoding = [System.Text.Encoding]::UTF8");

                UpdatePrompt(workingDirectory);

                log.Info($"Terminal: shell started ({shellExe}) in {workingDirectory}");
            }
            catch (Exception ex)
            {
                AppendOutput($"启动终端失败: {ex.Message}\r\n");
                log.Error("Terminal: failed to start shell", ex);
            }
        }

        public void SendCommand(string command)
        {
            if (!_isShellRunning || _shellInput == null)
            {
                StartShell();
                Task.Delay(500).ContinueWith(_ =>
                    Dispatcher.BeginInvoke(() => WriteToShell(command)));
                return;
            }
            WriteToShell(command);
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
            if (_shellProcess == null || _shellProcess.HasExited) return;

            uint pid = (uint)_shellProcess.Id;
            Task.Run(() =>
            {
                try
                {
                    // Detach from any previous console first
                    FreeConsole();
                    if (AttachConsole(pid))
                    {
                        SetConsoleCtrlHandler(IntPtr.Zero, true);
                        GenerateConsoleCtrlEvent(CTRL_C_EVENT, 0);
                        Thread.Sleep(200);
                        FreeConsole();
                        SetConsoleCtrlHandler(IntPtr.Zero, false);
                    }
                    else
                    {
                        _shellInput?.Write('\x03');
                        _shellInput?.Flush();
                    }
                }
                catch (Exception ex)
                {
                    log.Warn($"Terminal: Ctrl+C failed: {ex.Message}");
                    try { _shellInput?.Write('\x03'); _shellInput?.Flush(); } catch { }
                }
            });
        }

        private void WriteToShell(string command)
        {
            if (_shellInput == null) return;
            try
            {
                _shellInput.WriteLine(command);
            }
            catch (Exception ex)
            {
                AppendOutput($"发送命令失败: {ex.Message}\r\n");
            }
        }

        private void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
                AppendOutput(e.Data + Environment.NewLine);
        }

        private void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
                AppendOutput(e.Data + Environment.NewLine);
        }

        private void OnProcessExited(object? sender, EventArgs e)
        {
            _isShellRunning = false;
            int exitCode = 0;
            try { exitCode = _shellProcess?.ExitCode ?? -1; } catch { }
            AppendOutput($"\r\n[终端已退出, 退出代码: {exitCode}]\r\n");
        }

        private void AppendOutput(string text)
        {
            lock (_outputLock)
            {
                _outputBuffer.Append(text);
                if (_outputBuffer.Length > MaxOutputLength)
                    _outputBuffer.Remove(0, _outputBuffer.Length - MaxOutputLength);
            }

            Dispatcher.BeginInvoke(() =>
            {
                string content;
                lock (_outputLock)
                {
                    content = _outputBuffer.ToString();
                }
                OutputTextBox.Text = content;
                OutputTextBox.ScrollToEnd();
            });
        }

        private void ClearOutput()
        {
            lock (_outputLock)
            {
                _outputBuffer.Clear();
            }
            Dispatcher.BeginInvoke(() => OutputTextBox.Clear());
        }

        private void KillShell()
        {
            if (_shellProcess != null)
            {
                _isShellRunning = false;
                try
                {
                    if (!_shellProcess.HasExited)
                        _shellProcess.Kill(entireProcessTree: true);
                }
                catch (Exception ex)
                {
                    log.Warn($"Terminal: kill shell failed: {ex.Message}");
                }

                _shellProcess.OutputDataReceived -= OnOutputDataReceived;
                _shellProcess.ErrorDataReceived -= OnErrorDataReceived;
                _shellProcess.Exited -= OnProcessExited;
                _shellProcess.Dispose();
                _shellProcess = null;
                _shellInput = null;
            }
        }

        private void UpdatePrompt(string path)
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (_currentShell == "cmd")
                    PromptLabel.Text = $"{path}>";
                else
                    PromptLabel.Text = $"PS {path}>";
            });
        }

        private string GetDefaultWorkingDirectory()
        {
            var dirInfo = SolutionManager.GetInstance()?.CurrentSolutionExplorer?.DirectoryInfo;
            if (dirInfo?.Exists == true)
                return dirInfo.FullName;
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        #region UI Event Handlers

        private void InputTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var command = InputTextBox.Text;
                InputTextBox.Clear();

                if (!string.IsNullOrWhiteSpace(command))
                {
                    _commandHistory.Add(command);
                    _historyIndex = _commandHistory.Count;
                }

                SendCommand(command);
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                if (_commandHistory.Count > 0 && _historyIndex > 0)
                {
                    _historyIndex--;
                    InputTextBox.Text = _commandHistory[_historyIndex];
                    InputTextBox.CaretIndex = InputTextBox.Text.Length;
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                if (_historyIndex < _commandHistory.Count - 1)
                {
                    _historyIndex++;
                    InputTextBox.Text = _commandHistory[_historyIndex];
                    InputTextBox.CaretIndex = InputTextBox.Text.Length;
                }
                else
                {
                    _historyIndex = _commandHistory.Count;
                    InputTextBox.Clear();
                }
                e.Handled = true;
            }
            else if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
            {
                SendCtrlC();
                e.Handled = true;
            }
            else if (e.Key == Key.L && Keyboard.Modifiers == ModifierKeys.Control)
            {
                ClearOutput();
                e.Handled = true;
            }
        }

        private void OutputTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
            {
                // Allow Ctrl+C to copy selected text from output, or send SIGINT if nothing selected
                if (string.IsNullOrEmpty(OutputTextBox.SelectedText))
                {
                    SendCtrlC();
                    e.Handled = true;
                }
                return;
            }

            // Redirect typing to input box
            if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control) &&
                !Keyboard.Modifiers.HasFlag(ModifierKeys.Alt) &&
                e.Key != Key.Tab &&
                e.Key >= Key.A && e.Key <= Key.OemClear)
            {
                InputTextBox.Focus();
            }
        }

        private void ButtonNewShell_Click(object sender, RoutedEventArgs e)
        {
            StartShell();
            InputTextBox.Focus();
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
