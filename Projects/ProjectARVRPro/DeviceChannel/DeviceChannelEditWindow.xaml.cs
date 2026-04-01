using log4net;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO.Ports;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace ProjectARVRPro.DeviceChannel
{
    /// <summary>
    /// 通道类型 → 串口参数区域可见性
    /// </summary>
    public class ChannelTypeToSerialVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DeviceChannelType type)
                return type is DeviceChannelType.ThunderbirdSerial or DeviceChannelType.GenericSerial
                    ? Visibility.Visible : Visibility.Collapsed;
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    /// <summary>
    /// 通道类型 → Socket 参数区域可见性
    /// </summary>
    public class ChannelTypeToSocketVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DeviceChannelType type)
                return type == DeviceChannelType.Socket ? Visibility.Visible : Visibility.Collapsed;
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    /// <summary>
    /// 设备通道配置编辑窗口
    /// </summary>
    public partial class DeviceChannelEditWindow : Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(DeviceChannelEditWindow));

        private readonly DeviceChannelManager _manager;
        private ObservableCollection<DeviceChannelConfig> _configs;

        public DeviceChannelEditWindow(DeviceChannelManager manager)
        {
            _manager = manager;
            // 工作在原始集合上，保存时由 Manager 持久化
            _configs = manager.ChannelConfigs;
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            ChannelListBox.ItemsSource = _configs;
            ChannelTypeComboBox.ItemsSource = Enum.GetValues<DeviceChannelType>();
            RefreshPorts();

            if (_configs.Count > 0)
                ChannelListBox.SelectedIndex = 0;
        }

        // ─── 通道列表操作 ───────────────────────────────

        private void AddChannel_Click(object sender, RoutedEventArgs e)
        {
            var config = new DeviceChannelConfig
            {
                Name = $"通道{_configs.Count + 1}",
                ChannelType = DeviceChannelType.ThunderbirdSerial,
                IsEnabled = true,
                BaudRate = 115200,
                TimeoutMs = 1000
            };
            _configs.Add(config);
            ChannelListBox.SelectedItem = config;
        }

        private void RemoveChannel_Click(object sender, RoutedEventArgs e)
        {
            if (ChannelListBox.SelectedItem is DeviceChannelConfig config)
            {
                var result = MessageBox.Show($"确定删除通道 \"{config.Name}\"？", "确认删除",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Yes)
                    _configs.Remove(config);
            }
        }

        private void ChannelListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 切换选中项时刷新串口列表
            if (ChannelListBox.SelectedItem != null)
                RefreshPorts();

            // 控制占位文字
            PlaceholderText.Visibility = ChannelListBox.SelectedItem == null ? Visibility.Visible : Visibility.Collapsed;
        }

        // ─── 串口操作 ──────────────────────────────────

        private void RefreshPorts_Click(object sender, RoutedEventArgs e) => RefreshPorts();

        private void RefreshPorts()
        {
            try
            {
                var ports = SerialPort.GetPortNames();
                SerialPortComboBox.ItemsSource = ports;
            }
            catch (Exception ex)
            {
                log.Warn("刷新串口列表失败", ex);
            }
        }

        // ─── 连接测试 ──────────────────────────────────

        private async void TestConnect_Click(object sender, RoutedEventArgs e)
        {
            if (ChannelListBox.SelectedItem is not DeviceChannelConfig config)
                return;

            TestResultText.Text = "正在测试...";
            TestResultText.Foreground = System.Windows.Media.Brushes.Gray;

            try
            {
                var channel = DeviceChannelManager.CreateChannelFromConfig(config);
                await channel.ConnectAsync();
                await channel.DisconnectAsync();
                channel.Dispose();

                TestResultText.Text = "✔ 连接成功";
                TestResultText.Foreground = System.Windows.Media.Brushes.Green;
            }
            catch (Exception ex)
            {
                TestResultText.Text = $"✘ 连接失败: {ex.Message}";
                TestResultText.Foreground = System.Windows.Media.Brushes.Red;
                log.Warn($"通道测试连接失败: {config.Name}", ex);
            }
        }

        // ─── 保存 / 关闭 ───────────────────────────────

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            _manager.Save();
            DialogResult = true;
            Close();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
