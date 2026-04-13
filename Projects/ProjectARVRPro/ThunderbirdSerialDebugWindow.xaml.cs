using log4net;
using ColorVision.UI;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ProjectARVRPro
{
    /// <summary>
    /// 雷鸟切图调试窗口 — UI 界面层，负责用户交互
    /// 所有通信逻辑由 ThunderbirdSerialController 负责
    /// </summary>
    public partial class ThunderbirdSerialDebugWindow : Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ThunderbirdSerialDebugWindow));

        private ThunderbirdSerialController _controller = ThunderbirdSerialController.GetInstance();
        private ProjectARVRProConfig _projectConfig = ProjectARVRProConfig.Instance;

        /// <summary>
        /// 滑块变更是否由程序内部触发（避免循环触发）
        /// </summary>
        private bool _suppressSliderEvent;

        public ThunderbirdSerialDebugWindow()
        {
            InitializeComponent();
            InitializeBrightnessLevels();
            RefreshPortList();
            SyncUiFromController();
        }

        private void SaveThunderbirdConfig()
        {
            if (ComPortComboBox.SelectedItem != null)
                _projectConfig.ThunderbirdPortName = ComPortComboBox.SelectedItem.ToString() ?? string.Empty;

            if (BaudRateComboBox.SelectedItem is ComboBoxItem baudItem && int.TryParse(baudItem.Content?.ToString(), out int baudRate))
                _projectConfig.ThunderbirdBaudRate = baudRate;

            if (int.TryParse(TimeoutTextBox.Text, out int timeoutMs) && timeoutMs > 0)
                _projectConfig.ThunderbirdTimeoutMs = timeoutMs;

            _projectConfig.ThunderbirdAutoConnect = AutoConnectCheckBox.IsChecked == true;
            ConfigService.Instance.SaveConfigs();
        }

        private void SyncUiFromController()
        {
            if (!_controller.IsConnected)
            {
                TogglePortButton.Content = "连接";
                TogglePortButton.Background = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50));
                ConnectionStatusText.Text = "● 未连接";
                ConnectionStatusText.Foreground = new SolidColorBrush(Colors.Gray);
                SetControlButtonsEnabled(false);
                return;
            }

            string portName = _controller.CurrentPortName ?? "未知串口";
            int baudRate = _controller.CurrentBaudRate;
            int timeout = _controller.CurrentTimeoutMs;

            if (!string.IsNullOrWhiteSpace(_controller.CurrentPortName) && ComPortComboBox.Items.Contains(_controller.CurrentPortName))
                ComPortComboBox.SelectedItem = _controller.CurrentPortName;

            foreach (var item in BaudRateComboBox.Items)
            {
                if (item is ComboBoxItem comboItem &&
                    int.TryParse(comboItem.Content?.ToString(), out int itemBaudRate) &&
                    itemBaudRate == baudRate)
                {
                    BaudRateComboBox.SelectedItem = comboItem;
                    break;
                }
            }

            TimeoutTextBox.Text = timeout.ToString();

            TogglePortButton.Content = "断开";
            TogglePortButton.Background = new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36));
            ConnectionStatusText.Text = $"● 已连接 {portName}";
            ConnectionStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50));
            ComPortComboBox.IsEnabled = false;
            BaudRateComboBox.IsEnabled = false;
            SetControlButtonsEnabled(true);
            UpdateStatus($"已连接 {portName} (波特率:{baudRate}, 超时:{timeout}ms)");

            _ = QueryBrightnessAsync().ContinueWith(t =>
            {
                if (t.IsFaulted && t.Exception != null)
                    log.Error("窗口初始化查询亮度失败", t.Exception);
            }, TaskScheduler.Default);
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
            else if (!string.IsNullOrWhiteSpace(_projectConfig.ThunderbirdPortName) && ComPortComboBox.Items.Contains(_projectConfig.ThunderbirdPortName))
                ComPortComboBox.SelectedItem = _projectConfig.ThunderbirdPortName;
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
            if (_controller.IsConnected)
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

                _projectConfig.ThunderbirdPortName = portName;
                _projectConfig.ThunderbirdBaudRate = baudRate;
                _projectConfig.ThunderbirdTimeoutMs = timeout;
                _projectConfig.ThunderbirdAutoConnect = AutoConnectCheckBox.IsChecked == true;
                ConfigService.Instance.SaveConfigs();

                _controller.Open(portName, baudRate, timeout);

                TogglePortButton.Content = "断开";
                TogglePortButton.Background = new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36));
                ConnectionStatusText.Text = $"● 已连接 {portName}";
                ConnectionStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50));
                ComPortComboBox.IsEnabled = false;
                BaudRateComboBox.IsEnabled = false;
                SetControlButtonsEnabled(true);

                UpdateStatus($"已连接 {portName} (波特率:{baudRate}, 超时:{timeout}ms)");

                // 连接后自动查询当前亮度状态
                _ = QueryBrightnessAsync().ContinueWith(t =>
                {
                    if (t.IsFaulted && t.Exception != null)
                        log.Error("自动查询亮度失败", t.Exception);
                }, TaskScheduler.Default);

                SyncUiFromController();
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
                _controller.Close();

                TogglePortButton.Content = "连接";
                TogglePortButton.Background = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50));
                ConnectionStatusText.Text = "● 未连接";
                ConnectionStatusText.Foreground = new SolidColorBrush(Colors.Gray);
                ComPortComboBox.IsEnabled = true;
                BaudRateComboBox.IsEnabled = true;
                SetControlButtonsEnabled(false);

                CurrentBrightnessText.Text = "未知";

                UpdateStatus("已断开");
                SaveThunderbirdConfig();
            }
            catch (Exception ex)
            {
                UpdateStatus($"断开失败: {ex.Message}");
                log.Error("关闭串口失败", ex);
            }
        }

        private void AutoConnectCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            SaveThunderbirdConfig();
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

        // ─── Command Sending ─────────────────────────

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
                AppendLog("[TX] AT+UPWARDS");
                bool success = await _controller.SwitchUpAsync(GetConfiguredTimeout());
                if (success)
                {
                    AppendLog("[RX] OK");
                    UpdateStatus("向上切图 完成");
                }
                else
                {
                    UpdateStatus("向上切图 失败");
                }
            }
            finally
            {
                if (_controller.IsConnected)
                    SwitchUpButton.IsEnabled = true;
            }
        }

        private async void SwitchDown_Click(object sender, RoutedEventArgs e)
        {
            SwitchDownButton.IsEnabled = false;
            try
            {
                AppendLog("[TX] AT+DOWN");
                bool success = await _controller.SwitchDownAsync(GetConfiguredTimeout());
                if (success)
                {
                    AppendLog("[RX] OK");
                    UpdateStatus("向下切图 完成");
                }
                else
                {
                    UpdateStatus("向下切图 失败");
                }
            }
            finally
            {
                if (_controller.IsConnected)
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
            AppendLog("[TX] AT+R 30");
            bool success = await _controller.QueryBrightnessAsync(GetConfiguredTimeout());
            if (success)
            {
                AppendLog($"[RX] 0x{_controller.CurrentBrightnessLevel + 0x20:X2}");
                UpdateStatus($"当前亮度: 档位 {_controller.CurrentBrightnessLevel} (0x{_controller.CurrentBrightnessLevel + 0x20:X2})");
                UpdateBrightnessDisplay(_controller.CurrentBrightnessLevel, _controller.CurrentBrightnessLevel + 0x20);
            }
        }

        /// <summary>
        /// 亮度 - 按钮：降低一档
        /// </summary>
        private async void BrightnessDown_Click(object sender, RoutedEventArgs e)
        {
            int targetLevel = _controller.CurrentBrightnessLevel <= 0 ? 0 : _controller.CurrentBrightnessLevel - 1;
            await SetBrightnessLevelAsync(targetLevel);
        }

        /// <summary>
        /// 亮度 + 按钮：升高一档
        /// </summary>
        private async void BrightnessUp_Click(object sender, RoutedEventArgs e)
        {
            int targetLevel = _controller.CurrentBrightnessLevel >= 16 ? 16 : _controller.CurrentBrightnessLevel + 1;
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
            AppendLog($"[TX] AT+W 30 {registerValue:X2}");

            bool success = await _controller.SetBrightnessLevelAsync(level, GetConfiguredTimeout());
            if (success)
            {
                AppendLog("[RX] OK");
                UpdateBrightnessDisplay(level, registerValue);
                UpdateStatus($"已设置亮度: 档位 {level} (0x{registerValue:X2})");
            }
            else
            {
                UpdateStatus("设置亮度失败");
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
        }

        private void UpdateStatus(string message)
        {
            if (Dispatcher.CheckAccess())
                StatusText.Text = message;
            else
                Dispatcher.Invoke(() => StatusText.Text = message);
        }
    }
}
