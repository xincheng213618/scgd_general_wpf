using log4net;
using System.IO.Ports;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ProjectARVRPro
{
    /// <summary>
    /// 雷鸟切图调试窗口 — 专用于雷鸟设备的切图与亮度调节
    /// 切图指令: AT+UPWARDS / AT+DOWN（直接发送）
    /// 亮度写入: AT+W 30 XX (XX = 0x20~0x30)
    /// 亮度读取: AT+R 30 → 返回当前寄存器值并回显
    /// </summary>
    public partial class ThunderbirdSerialDebugWindow : Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ThunderbirdSerialDebugWindow));

        private SerialPort? _serialPort;
        private readonly StringBuilder _receiveBuffer = new();

        /// <summary>
        /// 当前亮度档位 (0~16, 对应 0x20~0x30)，-1 表示未知
        /// </summary>
        private int _currentBrightnessLevel = -1;

        /// <summary>
        /// 滑块变更是否由程序内部触发（避免循环触发）
        /// </summary>
        private bool _suppressSliderEvent;

        public ThunderbirdSerialDebugWindow()
        {
            InitializeComponent();
            InitializeBrightnessLevels();
            RefreshPortList();
        }

        /// <summary>
        /// 初始化亮度档位下拉列表 (0x20~0x30)
        /// </summary>
        private void InitializeBrightnessLevels()
        {
            for (int i = 0x20; i <= 0x30; i++)
            {
                int level = i - 0x20;
                BrightnessComboBox.Items.Add(new ComboBoxItem
                {
                    Content = $"档位 {level} (0x{i:X2})",
                    Tag = i
                });
            }
            BrightnessComboBox.SelectedIndex = 0;
        }

        // ─── Serial Port Connection ───────────────────────────

        private void RefreshPortList()
        {
            string? previousSelection = ComPortComboBox.SelectedItem as string;
            ComPortComboBox.Items.Clear();
            foreach (string portName in SerialPort.GetPortNames())
            {
                ComPortComboBox.Items.Add(portName);
            }

            if (previousSelection != null && ComPortComboBox.Items.Contains(previousSelection))
                ComPortComboBox.SelectedItem = previousSelection;
            else if (ComPortComboBox.Items.Count > 0)
                ComPortComboBox.SelectedIndex = 0;
        }

        private void RefreshPorts_Click(object sender, RoutedEventArgs e) => RefreshPortList();

        private void TogglePort_Click(object sender, RoutedEventArgs e)
        {
            if (_serialPort != null && _serialPort.IsOpen)
                ClosePort();
            else
                OpenPort();
        }

        private void OpenPort()
        {
            if (ComPortComboBox.SelectedItem == null)
            {
                UpdateStatus("请选择串口");
                return;
            }

            try
            {
                string portName = ComPortComboBox.SelectedItem.ToString()!;
                int baudRate = int.Parse(((ComboBoxItem)BaudRateComboBox.SelectedItem).Content.ToString()!);

                _serialPort = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
                {
                    ReadTimeout = 2000,
                    WriteTimeout = 2000,
                    Encoding = Encoding.ASCII
                };

                _serialPort.DataReceived += SerialPort_DataReceived;
                _serialPort.Open();

                TogglePortButton.Content = "断开";
                TogglePortButton.Background = new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36));
                ConnectionStatusText.Text = $"● 已连接 {portName}";
                ConnectionStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50));
                ComPortComboBox.IsEnabled = false;
                BaudRateComboBox.IsEnabled = false;
                SetControlButtonsEnabled(true);

                UpdateStatus($"已连接 {portName} (波特率:{baudRate})");
                log.Info($"雷鸟调试串口已打开: {portName}, BaudRate={baudRate}");

                // 连接后自动查询当前亮度状态
                QueryBrightnessInternal();
            }
            catch (Exception ex)
            {
                UpdateStatus($"连接失败: {ex.Message}");
                log.Error("打开串口失败", ex);
            }
        }

        private void ClosePort()
        {
            try
            {
                if (_serialPort != null)
                {
                    _serialPort.DataReceived -= SerialPort_DataReceived;
                    if (_serialPort.IsOpen)
                        _serialPort.Close();
                    _serialPort.Dispose();
                    _serialPort = null;
                }

                TogglePortButton.Content = "连接";
                TogglePortButton.Background = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50));
                ConnectionStatusText.Text = "● 未连接";
                ConnectionStatusText.Foreground = new SolidColorBrush(Colors.Gray);
                ComPortComboBox.IsEnabled = true;
                BaudRateComboBox.IsEnabled = true;
                SetControlButtonsEnabled(false);

                _currentBrightnessLevel = -1;
                CurrentBrightnessText.Text = "未知";

                UpdateStatus("已断开");
                log.Info("雷鸟调试串口已关闭");
            }
            catch (Exception ex)
            {
                UpdateStatus($"断开失败: {ex.Message}");
                log.Error("关闭串口失败", ex);
            }
        }

        /// <summary>
        /// 启用/禁用所有操作按钮
        /// </summary>
        private void SetControlButtonsEnabled(bool enabled)
        {
            SwitchUpButton.IsEnabled = enabled;
            SwitchDownButton.IsEnabled = enabled;
            BrightnessUpButton.IsEnabled = enabled;
            BrightnessDownButton.IsEnabled = enabled;
            BrightnessSlider.IsEnabled = enabled;
            BrightnessComboBox.IsEnabled = enabled;
            SetBrightnessButton.IsEnabled = enabled;
            QueryBrightnessButton.IsEnabled = enabled;
        }

        // ─── Serial Port Data Handling ────────────────────────

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                if (_serialPort == null || !_serialPort.IsOpen)
                    return;

                string data = _serialPort.ReadExisting();
                Dispatcher.Invoke(() =>
                {
                    _receiveBuffer.Append(data);
                    ReceiveTextBox.AppendText(data);
                    ReceiveTextBox.ScrollToEnd();

                    // 检查是否收到完整的行终止响应
                    string buffered = _receiveBuffer.ToString();
                    if (buffered.Contains('\r') || buffered.Contains('\n'))
                    {
                        ProcessResponse(buffered.Trim());
                        _receiveBuffer.Clear();
                    }
                });
            }
            catch (Exception ex)
            {
                log.Error("串口接收数据异常", ex);
            }
        }

        /// <summary>
        /// 处理串口应答：判断 OK/error，或解析亮度读取结果
        /// </summary>
        private void ProcessResponse(string response)
        {
            if (string.IsNullOrWhiteSpace(response))
                return;

            // 尝试解析亮度读取结果
            // 响应可能是纯十六进制数值，如 "20" ~ "30"
            string trimmed = response.Trim();

            // 去掉可能的 "OK" 前后文字，逐行检查
            string[] lines = trimmed.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                string l = line.Trim();
                if (l.Equals("OK", StringComparison.OrdinalIgnoreCase))
                {
                    UpdateStatus("操作成功 (OK)");
                    continue;
                }
                if (l.Contains("error", StringComparison.OrdinalIgnoreCase))
                {
                    UpdateStatus("操作失败 (error)");
                    continue;
                }

                // 尝试解析为十六进制亮度值
                if (TryParseHexBrightness(l, out int brightnessValue))
                {
                    int level = brightnessValue - 0x20;
                    if (level >= 0 && level <= 16)
                    {
                        _currentBrightnessLevel = level;
                        UpdateBrightnessDisplay(level, brightnessValue);
                        UpdateStatus($"当前亮度: 档位 {level} (0x{brightnessValue:X2})");
                    }
                }
            }
        }

        /// <summary>
        /// 尝试从响应行中解析十六进制亮度值
        /// </summary>
        private static bool TryParseHexBrightness(string line, out int value)
        {
            value = 0;
            string cleaned = line.Trim();
            // 移除可能的 "0x" 前缀
            if (cleaned.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                cleaned = cleaned[2..];

            if (int.TryParse(cleaned, System.Globalization.NumberStyles.HexNumber, null, out int parsed))
            {
                if (parsed >= 0x20 && parsed <= 0x30)
                {
                    value = parsed;
                    return true;
                }
            }
            // 也尝试十进制解析
            if (int.TryParse(cleaned, out int decParsed))
            {
                if (decParsed >= 0x20 && decParsed <= 0x30)
                {
                    value = decParsed;
                    return true;
                }
            }
            return false;
        }

        // ─── Command Sending ──────────────────────────────────

        /// <summary>
        /// 发送串口命令（公共方法，供外部流程调用）
        /// </summary>
        public bool SendCommand(string command)
        {
            if (_serialPort == null || !_serialPort.IsOpen)
            {
                UpdateStatus("串口未打开");
                return false;
            }
            if (string.IsNullOrWhiteSpace(command))
                return false;

            try
            {
                _serialPort.Write(command + "\r\n");
                AppendLog($"[TX] {command}");
                log.Info($"雷鸟串口发送: {command}");
                return true;
            }
            catch (Exception ex)
            {
                UpdateStatus($"发送失败: {ex.Message}");
                log.Error($"串口发送失败: {command}", ex);
                return false;
            }
        }

        private void AppendLog(string text)
        {
            ReceiveTextBox.AppendText(text + "\n");
            ReceiveTextBox.ScrollToEnd();
        }

        // ─── Image Switching (直接发送) ───────────────────────

        private void SwitchUp_Click(object sender, RoutedEventArgs e)
        {
            if (SendCommand("AT+UPWARDS"))
                UpdateStatus("已发送: 向上切图");
        }

        private void SwitchDown_Click(object sender, RoutedEventArgs e)
        {
            if (SendCommand("AT+DOWN"))
                UpdateStatus("已发送: 向下切图");
        }

        // ─── Brightness Controls ──────────────────────────────

        private void QueryBrightness_Click(object sender, RoutedEventArgs e)
        {
            QueryBrightnessInternal();
        }

        private void QueryBrightnessInternal()
        {
            if (SendCommand("AT+R 30"))
                UpdateStatus("正在查询亮度...");
        }

        /// <summary>
        /// 亮度 - 按钮：降低一档
        /// </summary>
        private void BrightnessDown_Click(object sender, RoutedEventArgs e)
        {
            int targetLevel = _currentBrightnessLevel <= 0 ? 0 : _currentBrightnessLevel - 1;
            SetBrightnessLevel(targetLevel);
        }

        /// <summary>
        /// 亮度 + 按钮：升高一档
        /// </summary>
        private void BrightnessUp_Click(object sender, RoutedEventArgs e)
        {
            int targetLevel = _currentBrightnessLevel >= 16 ? 16 : _currentBrightnessLevel + 1;
            SetBrightnessLevel(targetLevel);
        }

        /// <summary>
        /// 滑块拖动设置亮度
        /// </summary>
        private void BrightnessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_suppressSliderEvent)
                return;

            int level = (int)BrightnessSlider.Value;
            SliderValueText.Text = $"档位 {level} (0x{0x20 + level:X2})";

            // 只在用户松开鼠标时才发送（通过 IsMouseCaptured 判断拖拽结束）
            if (!BrightnessSlider.IsMouseCaptureWithin && _serialPort != null && _serialPort.IsOpen)
            {
                SetBrightnessLevel(level);
            }
        }

        /// <summary>
        /// 从下拉框指定档位设置亮度
        /// </summary>
        private void SetBrightness_Click(object sender, RoutedEventArgs e)
        {
            if (BrightnessComboBox.SelectedItem is ComboBoxItem item && item.Tag is int brightnessValue)
            {
                int level = brightnessValue - 0x20;
                SetBrightnessLevel(level);
            }
        }

        /// <summary>
        /// 设置亮度并发送 AT+W 30 XX 命令
        /// </summary>
        private void SetBrightnessLevel(int level)
        {
            if (level < 0) level = 0;
            if (level > 16) level = 16;

            int registerValue = 0x20 + level;
            string command = $"AT+W 30 {registerValue:X2}";

            if (SendCommand(command))
            {
                _currentBrightnessLevel = level;
                UpdateBrightnessDisplay(level, registerValue);
                UpdateStatus($"已设置亮度: 档位 {level} (0x{registerValue:X2})");
            }
        }

        /// <summary>
        /// 更新亮度显示（文本 + 滑块 + 下拉框同步）
        /// </summary>
        private void UpdateBrightnessDisplay(int level, int registerValue)
        {
            CurrentBrightnessText.Text = $"档位 {level} (0x{registerValue:X2})";

            _suppressSliderEvent = true;
            BrightnessSlider.Value = level;
            SliderValueText.Text = $"档位 {level} (0x{registerValue:X2})";
            _suppressSliderEvent = false;

            if (level >= 0 && level < BrightnessComboBox.Items.Count)
                BrightnessComboBox.SelectedIndex = level;
        }

        // ─── UI Helpers ───────────────────────────────────────

        private void ClearReceive_Click(object sender, RoutedEventArgs e)
        {
            ReceiveTextBox.Clear();
            _receiveBuffer.Clear();
        }

        private void UpdateStatus(string message)
        {
            if (Dispatcher.CheckAccess())
                StatusText.Text = message;
            else
                Dispatcher.Invoke(() => StatusText.Text = message);
        }

        protected override void OnClosed(EventArgs e)
        {
            ClosePort();
            base.OnClosed(e);
        }
    }
}
