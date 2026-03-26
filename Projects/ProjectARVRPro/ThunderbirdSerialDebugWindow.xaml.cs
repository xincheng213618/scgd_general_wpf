using log4net;
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
    /// 通信模式: 1发1收同步模式（通过 SerialPortHelper 封装）
    /// </summary>
    public partial class ThunderbirdSerialDebugWindow : Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ThunderbirdSerialDebugWindow));

        private SerialPortHelper? _serialHelper;

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
            foreach (string portName in SerialPortHelper.GetPortNames())
            {
                ComPortComboBox.Items.Add(portName);
            }

            if (previousSelection != null && ComPortComboBox.Items.Contains(previousSelection))
                ComPortComboBox.SelectedItem = previousSelection;
            else if (ComPortComboBox.Items.Count > 0)
                ComPortComboBox.SelectedIndex = 0;
        }

        /// <summary>
        /// 从 UI 读取用户配置的超时时间
        /// </summary>
        private int GetConfiguredTimeout()
        {
            if (int.TryParse(TimeoutTextBox.Text, out int ms) && ms > 0)
                return ms;
            return 1000;
        }

        private void RefreshPorts_Click(object sender, RoutedEventArgs e) => RefreshPortList();

        private void TogglePort_Click(object sender, RoutedEventArgs e)
        {
            if (_serialHelper != null && _serialHelper.IsOpen)
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
                int timeout = GetConfiguredTimeout();

                _serialHelper = new SerialPortHelper { TimeoutMs = timeout };
                _serialHelper.Open(portName, baudRate);

                TogglePortButton.Content = "断开";
                TogglePortButton.Background = new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36));
                ConnectionStatusText.Text = $"● 已连接 {portName}";
                ConnectionStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50));
                ComPortComboBox.IsEnabled = false;
                BaudRateComboBox.IsEnabled = false;
                SetControlButtonsEnabled(true);

                UpdateStatus($"已连接 {portName} (波特率:{baudRate}, 超时:{timeout}ms)");
                log.Info($"雷鸟调试串口已打开: {portName}, BaudRate={baudRate}, Timeout={timeout}ms");

                // 连接后自动查询当前亮度状态
                _ = QueryBrightnessAsync();
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
                _serialHelper?.Dispose();
                _serialHelper = null;

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

        // ─── Command Sending (1发1收) ─────────────────────────

        /// <summary>
        /// 发送命令并等待响应（1发1收模式），自动处理超时提示
        /// </summary>
        /// <param name="command">AT 命令</param>
        /// <returns>响应内容，超时返回 null</returns>
        private async Task<string?> SendCommandAsync(string command)
        {
            if (_serialHelper == null || !_serialHelper.IsOpen)
            {
                UpdateStatus("串口未打开");
                return null;
            }

            // 同步用户配置的超时
            _serialHelper.TimeoutMs = GetConfiguredTimeout();

            AppendLog($"[TX] {command}");
            try
            {
                string response = await _serialHelper.SendAndReceiveAsync(command);
                AppendLog($"[RX] {response}");
                return response;
            }
            catch (TimeoutException ex)
            {
                AppendLog($"[超时] {ex.Message}");
                UpdateStatus($"⚠ 超时: 发送 \"{command}\" 后 {_serialHelper.TimeoutMs}ms 内未收到响应");
                log.Warn(ex.Message);
                return null;
            }
            catch (Exception ex)
            {
                AppendLog($"[错误] {ex.Message}");
                UpdateStatus($"发送失败: {ex.Message}");
                log.Error($"串口发送失败: {command}", ex);
                return null;
            }
        }

        /// <summary>
        /// 发送串口命令（公共方法，供外部流程调用）
        /// </summary>
        public async Task<string?> SendCommandWithResponseAsync(string command)
        {
            return await SendCommandAsync(command);
        }

        private void AppendLog(string text)
        {
            ReceiveTextBox.AppendText($"[{DateTime.Now:HH:mm:ss.fff}] {text}\n");
            ReceiveTextBox.ScrollToEnd();
        }

        // ─── Image Switching ──────────────────────────────────

        private async void SwitchUp_Click(object sender, RoutedEventArgs e)
        {
            SwitchUpButton.IsEnabled = false;
            try
            {
                string? response = await SendCommandAsync("AT+UPWARDS");
                if (response != null)
                {
                    ProcessResponse(response);
                    UpdateStatus("向上切图 完成");
                }
            }
            finally
            {
                if (_serialHelper?.IsOpen == true)
                    SwitchUpButton.IsEnabled = true;
            }
        }

        private async void SwitchDown_Click(object sender, RoutedEventArgs e)
        {
            SwitchDownButton.IsEnabled = false;
            try
            {
                string? response = await SendCommandAsync("AT+DOWN");
                if (response != null)
                {
                    ProcessResponse(response);
                    UpdateStatus("向下切图 完成");
                }
            }
            finally
            {
                if (_serialHelper?.IsOpen == true)
                    SwitchDownButton.IsEnabled = true;
            }
        }

        // ─── Brightness Controls ──────────────────────────────

        private async void QueryBrightness_Click(object sender, RoutedEventArgs e)
        {
            await QueryBrightnessAsync();
        }

        private async Task QueryBrightnessAsync()
        {
            UpdateStatus("正在查询亮度...");
            string? response = await SendCommandAsync("AT+R 30");
            if (response != null)
                ProcessResponse(response);
        }

        /// <summary>
        /// 亮度 - 按钮：降低一档
        /// </summary>
        private async void BrightnessDown_Click(object sender, RoutedEventArgs e)
        {
            int targetLevel = _currentBrightnessLevel <= 0 ? 0 : _currentBrightnessLevel - 1;
            await SetBrightnessLevelAsync(targetLevel);
        }

        /// <summary>
        /// 亮度 + 按钮：升高一档
        /// </summary>
        private async void BrightnessUp_Click(object sender, RoutedEventArgs e)
        {
            int targetLevel = _currentBrightnessLevel >= 16 ? 16 : _currentBrightnessLevel + 1;
            await SetBrightnessLevelAsync(targetLevel);
        }

        /// <summary>
        /// 滑块拖动更新显示（仅更新显示，不自动发送命令）
        /// </summary>
        private void BrightnessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_suppressSliderEvent)
                return;

            int level = (int)BrightnessSlider.Value;
            SliderValueText.Text = $"档位 {level} (0x{0x20 + level:X2})";

            if (level >= 0 && level < BrightnessComboBox.Items.Count)
                BrightnessComboBox.SelectedIndex = level;
        }

        /// <summary>
        /// 从下拉框/滑块指定档位设置亮度
        /// </summary>
        private async void SetBrightness_Click(object sender, RoutedEventArgs e)
        {
            int level = (int)BrightnessSlider.Value;
            await SetBrightnessLevelAsync(level);
        }

        /// <summary>
        /// 设置亮度并发送 AT+W 30 XX 命令
        /// </summary>
        private async Task SetBrightnessLevelAsync(int level)
        {
            if (level < 0) level = 0;
            if (level > 16) level = 16;

            int registerValue = 0x20 + level;
            string command = $"AT+W 30 {registerValue:X2}";

            string? response = await SendCommandAsync(command);
            if (response != null)
            {
                _currentBrightnessLevel = level;
                UpdateBrightnessDisplay(level, registerValue);
                ProcessResponse(response);
                UpdateStatus($"已设置亮度: 档位 {level} (0x{registerValue:X2})");
            }
        }

        /// <summary>
        /// 处理串口应答：判断 OK/error，或解析亮度读取结果
        /// </summary>
        private void ProcessResponse(string response)
        {
            if (string.IsNullOrWhiteSpace(response))
                return;

            string[] lines = response.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
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
            _serialHelper?.Dispose();
            _serialHelper = null;
            base.OnClosed(e);
        }
    }
}
