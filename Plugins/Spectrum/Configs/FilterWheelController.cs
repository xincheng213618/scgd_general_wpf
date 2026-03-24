using ColorVision.Common.MVVM;
using log4net;
using System.IO.Ports;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace Spectrum.Configs
{
    /// <summary>
    /// Controller for the filter wheel via serial port.
    /// Protocol:
    ///   - Send "0"-"4" to set position, returns "0"-"4" confirming the position.
    ///   - Send "NOW" to query current position, returns "0"-"4".
    /// Baud rate: 9600.
    /// </summary>
    public class FilterWheelController : ViewModelBase, IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(FilterWheelController));

        private SerialPort? _serialPort;

        public FilterWheelConfig Config => SpectrumConfig.Instance.FilterWheelConfig;

        private bool _isConnected;
        public bool IsConnected
        {
            get => _isConnected;
            set { _isConnected = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusText)); }
        }

        public string StatusText => IsConnected ? "已连接 (Connected)" : "未连接 (Disconnected)";

        /// <summary>
        /// Current filter wheel position (0-4), or -1 if unknown.
        /// </summary>
        private int _currentPosition = -1;
        public int CurrentPosition
        {
            get => _currentPosition;
            set { _currentPosition = value; OnPropertyChanged(); OnPropertyChanged(nameof(CurrentPositionName)); }
        }

        /// <summary>
        /// Display name of the current position (e.g., "ND0", "ND10", etc.)
        /// </summary>
        public string CurrentPositionName
        {
            get
            {
                if (CurrentPosition < 0) return "未知";
                return Config.GetHoleName(CurrentPosition) ?? CurrentPosition.ToString();
            }
        }

        public ICommand ConnectCommand { get; }
        public ICommand DisconnectCommand { get; }
        public ICommand QueryPositionCommand { get; }
        public ICommand SetPositionCommand { get; }

        /// <summary>
        /// Event raised when the filter wheel position changes successfully.
        /// The int parameter is the new position (0-4).
        /// </summary>
        public event Action<int>? PositionChanged;

        public FilterWheelController()
        {
            ConnectCommand = new RelayCommand(_ => Connect(), _ => !IsConnected);
            DisconnectCommand = new RelayCommand(_ => Disconnect(), _ => IsConnected);
            QueryPositionCommand = new RelayCommand(_ => _ = QueryPositionAsync(), _ => IsConnected);
            SetPositionCommand = new RelayCommand(p =>
            {
                if (p is int pos)
                    _ = SetPositionAsync(pos);
                else if (p is string s && int.TryParse(s, out int parsed))
                    _ = SetPositionAsync(parsed);
            }, _ => IsConnected);
        }

        public void Connect()
        {
            try
            {
                _serialPort?.Dispose();

                _serialPort = new SerialPort(Config.SzComName, Config.BaudRate)
                {
                    ReadTimeout = 2000,
                    WriteTimeout = 1000
                };
                log.Info($"FilterWheel: 尝试连接到串口 {Config.SzComName}，波特率 {Config.BaudRate}");
                _serialPort.Open();
                IsConnected = true;
                log.Info("FilterWheel: 连接成功");

                // Query current position after connect
                _ = QueryPositionAsync();
            }
            catch (Exception ex)
            {
                log.Error($"FilterWheel: 打开串口失败: {ex.Message}");
                MessageBox.Show($"打开滤色轮串口失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                IsConnected = false;
            }
        }

        public void Disconnect()
        {
            try
            {
                if (_serialPort != null && _serialPort.IsOpen)
                {
                    _serialPort.Close();
                }
            }
            catch (Exception ex)
            {
                log.Error($"FilterWheel: 关闭串口失败: {ex.Message}");
                MessageBox.Show($"关闭滤色轮串口失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsConnected = false;
                CurrentPosition = -1;
                _serialPort?.Dispose();
                _serialPort = null;
            }
        }

        /// <summary>
        /// Sends "NOW" to query the current filter wheel position.
        /// </summary>
        public async Task<int> QueryPositionAsync()
        {
            string? response = await SendCommandAsync("NOW");
            if (response != null && int.TryParse(response.Trim(), out int pos) && pos >= 0 && pos <= 4)
            {
                CurrentPosition = pos;
                log.Info($"FilterWheel: 当前位置 = {pos} ({CurrentPositionName})");
                return pos;
            }
            log.Warn($"FilterWheel: 查询位置失败，响应: '{response}'");
            return -1;
        }

        /// <summary>
        /// Sends a position command (0-4) to set the filter wheel.
        /// </summary>
        public async Task<bool> SetPositionAsync(int position)
        {
            if (position < 0 || position > 4)
            {
                log.Warn($"FilterWheel: 无效位置 {position}，必须为 0-4");
                return false;
            }

            string? response = await SendCommandAsync(position.ToString());
            if (response != null && int.TryParse(response.Trim(), out int confirmedPos) && confirmedPos == position)
            {
                CurrentPosition = confirmedPos;
                log.Info($"FilterWheel: 设置位置成功 = {confirmedPos} ({CurrentPositionName})");
                PositionChanged?.Invoke(confirmedPos);
                return true;
            }
            log.Warn($"FilterWheel: 设置位置 {position} 失败，响应: '{response}'");
            return false;
        }

        private async Task<string?> SendCommandAsync(string cmd)
        {
            if (_serialPort == null || !_serialPort.IsOpen)
                return null;

            try
            {
                // Clear input buffer
                _serialPort.DiscardInBuffer();
                _serialPort.Write(cmd);

                string receiveBuffer = "";
                int maxLoops = 125; // ~2 seconds (125 * 16ms)

                for (int i = 0; i < maxLoops; i++)
                {
                    await Task.Delay(16);

                    if (_serialPort == null || !_serialPort.IsOpen) break;

                    int bytesRead = _serialPort.BytesToRead;
                    if (bytesRead > 0)
                    {
                        byte[] buff = new byte[bytesRead];
                        _serialPort.Read(buff, 0, bytesRead);
                        string msg = Encoding.UTF8.GetString(buff);
                        receiveBuffer += msg;

                        // Check if we have a valid response (a single digit 0-4 or similar)
                        string trimmed = receiveBuffer.Trim();
                        if (trimmed.Length > 0 && int.TryParse(trimmed, out _))
                        {
                            return trimmed;
                        }
                    }
                }

                // Return whatever we received
                return receiveBuffer.Trim().Length > 0 ? receiveBuffer.Trim() : null;
            }
            catch (Exception ex)
            {
                log.Error($"FilterWheel: 发送或读取指令失败: {ex.Message}");
                MessageBox.Show($"滤色轮通信失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Disconnect();
                return null;
            }
        }

        public void Dispose()
        {
            _serialPort?.Close();
            _serialPort?.Dispose();
            _serialPort = null;
        }
    }
}
