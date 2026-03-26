using log4net;
using System.IO.Ports;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace ProjectARVRPro
{
    /// <summary>
    /// 雷鸟切图调试窗口 - 串口通信工具
    /// 支持亮度配置（AT+W/AT+R）和切图指令（AT+UPWARDS/AT+DOWN）
    /// </summary>
    public partial class ThunderbirdSerialDebugWindow : Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ThunderbirdSerialDebugWindow));

        private SerialPort? _serialPort;
        private readonly StringBuilder _receiveBuffer = new();

        public ThunderbirdSerialDebugWindow()
        {
            InitializeComponent();
            InitializeBrightnessLevels();
            RefreshPortList();
        }

        /// <summary>
        /// 初始化亮度档位列表（0x20~0x30，共16档）
        /// bit0~4 为亮度值，bit5 置 1
        /// 0x20 亮度最小，0x30 亮度最大
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

        /// <summary>
        /// 刷新可用串口列表
        /// </summary>
        private void RefreshPortList()
        {
            string? previousSelection = ComPortComboBox.SelectedItem as string;
            ComPortComboBox.Items.Clear();
            foreach (string portName in SerialPort.GetPortNames())
            {
                ComPortComboBox.Items.Add(portName);
            }

            if (previousSelection != null && ComPortComboBox.Items.Contains(previousSelection))
            {
                ComPortComboBox.SelectedItem = previousSelection;
            }
            else if (ComPortComboBox.Items.Count > 0)
            {
                ComPortComboBox.SelectedIndex = 0;
            }
        }

        private void RefreshPorts_Click(object sender, RoutedEventArgs e)
        {
            RefreshPortList();
        }

        /// <summary>
        /// 打开/关闭串口
        /// </summary>
        private void TogglePort_Click(object sender, RoutedEventArgs e)
        {
            if (_serialPort != null && _serialPort.IsOpen)
            {
                ClosePort();
            }
            else
            {
                OpenPort();
            }
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
                int dataBits = int.Parse(((ComboBoxItem)DataBitsComboBox.SelectedItem).Content.ToString()!);

                StopBits stopBits = StopBitsComboBox.SelectedIndex switch
                {
                    0 => StopBits.One,
                    1 => StopBits.OnePointFive,
                    2 => StopBits.Two,
                    _ => StopBits.One
                };

                Parity parity = ParityComboBox.SelectedIndex switch
                {
                    0 => Parity.None,
                    1 => Parity.Odd,
                    2 => Parity.Even,
                    3 => Parity.Mark,
                    4 => Parity.Space,
                    _ => Parity.None
                };

                _serialPort = new SerialPort(portName, baudRate, parity, dataBits, stopBits)
                {
                    ReadTimeout = 5000,
                    WriteTimeout = 5000,
                    Encoding = Encoding.ASCII
                };

                _serialPort.DataReceived += SerialPort_DataReceived;
                _serialPort.Open();

                TogglePortButton.Content = "关闭串口";
                SetPortConfigEnabled(false);
                UpdateStatus($"已打开 {portName} (波特率:{baudRate})");
                log.Info($"雷鸟调试串口已打开: {portName}, BaudRate={baudRate}");
            }
            catch (Exception ex)
            {
                UpdateStatus($"打开串口失败: {ex.Message}");
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

                TogglePortButton.Content = "打开串口";
                SetPortConfigEnabled(true);
                UpdateStatus("串口已关闭");
                log.Info("雷鸟调试串口已关闭");
            }
            catch (Exception ex)
            {
                UpdateStatus($"关闭串口失败: {ex.Message}");
                log.Error("关闭串口失败", ex);
            }
        }

        private void SetPortConfigEnabled(bool enabled)
        {
            ComPortComboBox.IsEnabled = enabled;
            BaudRateComboBox.IsEnabled = enabled;
            StopBitsComboBox.IsEnabled = enabled;
            DataBitsComboBox.IsEnabled = enabled;
            ParityComboBox.IsEnabled = enabled;
        }

        /// <summary>
        /// 串口数据接收事件
        /// </summary>
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

                    // Check response for OK/error
                    if (data.Contains("OK", StringComparison.OrdinalIgnoreCase))
                    {
                        UpdateStatus("配置成功 (OK)");
                    }
                    else if (data.Contains("error", StringComparison.OrdinalIgnoreCase))
                    {
                        UpdateStatus("配置失败 (error)");
                    }
                });
            }
            catch (Exception ex)
            {
                log.Error("串口接收数据异常", ex);
            }
        }

        /// <summary>
        /// 发送数据
        /// </summary>
        private void Send_Click(object sender, RoutedEventArgs e)
        {
            SendCommand(SendTextBox.Text);
        }

        /// <summary>
        /// 发送串口命令
        /// </summary>
        /// <param name="command">要发送的命令文本</param>
        /// <returns>是否发送成功</returns>
        public bool SendCommand(string command)
        {
            if (_serialPort == null || !_serialPort.IsOpen)
            {
                UpdateStatus("串口未打开，请先打开串口");
                return false;
            }

            if (string.IsNullOrWhiteSpace(command))
            {
                UpdateStatus("发送内容为空");
                return false;
            }

            try
            {
                _serialPort.Write(command + "\r\n");
                UpdateStatus($"已发送: {command}");
                log.Info($"雷鸟调试串口发送: {command}");
                return true;
            }
            catch (Exception ex)
            {
                UpdateStatus($"发送失败: {ex.Message}");
                log.Error($"串口发送失败: {command}", ex);
                return false;
            }
        }

        /// <summary>
        /// 写亮度配置命令预设: AT+W 30 XX
        /// 其中 XX 为 PWM 寄存器值（0x20~0x30）
        /// bit0~4 为亮度值，bit5 置 1
        /// </summary>
        private void WriteBrightnessPreset_Click(object sender, RoutedEventArgs e)
        {
            if (BrightnessComboBox.SelectedItem is ComboBoxItem item && item.Tag is int brightnessValue)
            {
                SendTextBox.Text = $"AT+W 30 {brightnessValue:X2}";
            }
        }

        /// <summary>
        /// 读亮度命令预设: AT+R 30
        /// </summary>
        private void ReadBrightnessPreset_Click(object sender, RoutedEventArgs e)
        {
            SendTextBox.Text = "AT+R 30";
        }

        /// <summary>
        /// 向上切图命令预设: AT+UPWARDS
        /// </summary>
        private void SwitchUpPreset_Click(object sender, RoutedEventArgs e)
        {
            SendTextBox.Text = "AT+UPWARDS";
        }

        /// <summary>
        /// 向下切图命令预设: AT+DOWN
        /// </summary>
        private void SwitchDownPreset_Click(object sender, RoutedEventArgs e)
        {
            SendTextBox.Text = "AT+DOWN";
        }

        private void ClearReceive_Click(object sender, RoutedEventArgs e)
        {
            ReceiveTextBox.Clear();
            _receiveBuffer.Clear();
        }

        private void ClearSend_Click(object sender, RoutedEventArgs e)
        {
            SendTextBox.Clear();
        }

        private void UpdateStatus(string message)
        {
            if (Dispatcher.CheckAccess())
            {
                StatusText.Text = message;
            }
            else
            {
                Dispatcher.Invoke(() => StatusText.Text = message);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            ClosePort();
            base.OnClosed(e);
        }
    }
}
