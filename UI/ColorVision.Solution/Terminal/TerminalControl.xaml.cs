using log4net;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Solution.Terminal
{
    public partial class TerminalControl : UserControl
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(TerminalControl));

        private Process? _currentProcess;
        private readonly StringBuilder _outputBuffer = new();
        private readonly object _outputLock = new();
        private string? _workingDirectory;

        /// <summary>
        /// Maximum number of characters kept in the output buffer to prevent memory issues.
        /// </summary>
        private const int MaxOutputLength = 200_000;

        public TerminalControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Run a script file in the terminal.
        /// Determines the interpreter based on file extension.
        /// </summary>
        public void RunScript(string filePath)
        {
            if (!File.Exists(filePath))
            {
                AppendOutput($"文件不存在: {filePath}\r\n");
                return;
            }

            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            string fileName;
            string arguments;

            switch (ext)
            {
                case ".py":
                case ".pyw":
                    fileName = "python";
                    arguments = $"\"{filePath}\"";
                    break;
                case ".ps1":
                    fileName = "powershell";
                    arguments = $"-ExecutionPolicy Bypass -File \"{filePath}\"";
                    break;
                case ".bat":
                case ".cmd":
                    fileName = "cmd.exe";
                    arguments = $"/c \"{filePath}\"";
                    break;
                case ".sh":
                    fileName = "bash";
                    arguments = $"\"{filePath}\"";
                    break;
                case ".js":
                    fileName = "node";
                    arguments = $"\"{filePath}\"";
                    break;
                default:
                    AppendOutput($"不支持的脚本类型: {ext}\r\n");
                    return;
            }

            _workingDirectory = Path.GetDirectoryName(filePath);
            RunProcess(fileName, arguments, _workingDirectory);
        }

        /// <summary>
        /// Run a command with the given executable and arguments.
        /// </summary>
        public void RunProcess(string fileName, string arguments, string? workingDirectory = null)
        {
            // Stop any running process first
            StopCurrentProcess();

            workingDirectory ??= _workingDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            _workingDirectory = workingDirectory;

            Dispatcher.Invoke(() =>
            {
                TextWorkingDir.Text = workingDirectory;
                ButtonStop.IsEnabled = true;
                InputTextBox.IsEnabled = true;
            });

            AppendOutput($"> {fileName} {arguments}\r\n");

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    WorkingDirectory = workingDirectory,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                };

                _currentProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };

                _currentProcess.OutputDataReceived += (s, e) =>
                {
                    if (e.Data != null)
                        AppendOutput(e.Data + "\r\n");
                };

                _currentProcess.ErrorDataReceived += (s, e) =>
                {
                    if (e.Data != null)
                        AppendOutput(e.Data + "\r\n", isError: true);
                };

                _currentProcess.Exited += (s, e) =>
                {
                    int exitCode = 0;
                    try { exitCode = _currentProcess?.ExitCode ?? -1; } catch { }
                    AppendOutput($"\r\n进程已退出，退出代码: {exitCode}\r\n");
                    Dispatcher.Invoke(() =>
                    {
                        ButtonStop.IsEnabled = false;
                        InputTextBox.IsEnabled = false;
                    });
                };

                _currentProcess.Start();
                _currentProcess.BeginOutputReadLine();
                _currentProcess.BeginErrorReadLine();

                log.Info($"Terminal: started {fileName} {arguments}");
            }
            catch (Exception ex)
            {
                AppendOutput($"启动进程失败: {ex.Message}\r\n");
                log.Error($"Terminal: failed to start {fileName}", ex);
                Dispatcher.Invoke(() =>
                {
                    ButtonStop.IsEnabled = false;
                    InputTextBox.IsEnabled = false;
                });
            }
        }

        private void AppendOutput(string text, bool isError = false)
        {
            lock (_outputLock)
            {
                _outputBuffer.Append(text);

                // Trim if too long
                if (_outputBuffer.Length > MaxOutputLength)
                {
                    _outputBuffer.Remove(0, _outputBuffer.Length - MaxOutputLength);
                }
            }

            Dispatcher.BeginInvoke(() =>
            {
                string current;
                lock (_outputLock)
                {
                    current = _outputBuffer.ToString();
                }
                OutputTextBlock.Text = current;
                OutputScrollViewer.ScrollToEnd();
            });
        }

        private void StopCurrentProcess()
        {
            if (_currentProcess != null && !_currentProcess.HasExited)
            {
                try
                {
                    _currentProcess.Kill(entireProcessTree: true);
                    AppendOutput("进程已被终止\r\n");
                }
                catch (Exception ex)
                {
                    log.Warn($"Failed to kill process: {ex.Message}");
                }
            }
            _currentProcess = null;
        }

        private void ButtonClear_Click(object sender, RoutedEventArgs e)
        {
            lock (_outputLock)
            {
                _outputBuffer.Clear();
            }
            OutputTextBlock.Text = string.Empty;
        }

        private void ButtonStop_Click(object sender, RoutedEventArgs e)
        {
            StopCurrentProcess();
            ButtonStop.IsEnabled = false;
            InputTextBox.IsEnabled = false;
        }

        private void InputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && _currentProcess != null && !_currentProcess.HasExited)
            {
                var input = InputTextBox.Text;
                InputTextBox.Clear();

                try
                {
                    _currentProcess.StandardInput.WriteLine(input);
                    AppendOutput(input + "\r\n");
                }
                catch (Exception ex)
                {
                    AppendOutput($"输入失败: {ex.Message}\r\n");
                }

                e.Handled = true;
            }
        }
    }
}
