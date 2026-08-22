#pragma warning disable CA1822
using ColorVision.Common.MVVM;
using log4net;
using Spectrum.TimedButtons;
using System.IO.Ports;
using System.Text;
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
        private int activeOperationCount;
        private readonly SemaphoreSlim commandGate = new(1, 1);

        public bool IsBusy => Volatile.Read(ref activeOperationCount) > 0;

        public FilterWheelConfig Config => SpectrumConfig.Instance.FilterWheelConfig;

        private bool _isConnected;
        public bool IsConnected
        {
            get => _isConnected;
            set { _isConnected = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusText)); }
        }

        public string StatusText => IsConnected ? "已连接 (Connected)" : "未连接 (Disconnected)";

        public string LastErrorMessage
        {
            get => _lastErrorMessage;
            private set { _lastErrorMessage = value; OnPropertyChanged(); }
        }
        private string _lastErrorMessage = string.Empty;

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
            ConnectCommand = new TimedButtonCommand(
                async _ => await ConnectAsync(),
                _ => !IsConnected && !IsBusy,
                SpectrumTimedButtonHost.GetOwner,
                SpectrumTimedButtonHost.BuildOperationKey,
                "filter-wheel-connect");

            DisconnectCommand = new TimedButtonCommand(
                async _ => await DisconnectAsync(),
                _ => IsConnected && !IsBusy && !SpectrometerManager.Instance.IsDeviceBusy,
                SpectrumTimedButtonHost.GetOwner,
                SpectrumTimedButtonHost.BuildOperationKey,
                "filter-wheel-disconnect");

            QueryPositionCommand = new TimedButtonCommand(
                async _ => await QueryPositionAsync() >= 0,
                _ => IsConnected && !IsBusy && !SpectrometerManager.Instance.IsDeviceBusy,
                SpectrumTimedButtonHost.GetOwner,
                SpectrumTimedButtonHost.BuildOperationKey,
                "filter-wheel-query");

            SetPositionCommand = new TimedButtonCommand(
                async p =>
                {
                    if (p is int pos)
                    {
                        return await SetPositionAsync(pos);
                    }

                    if (p is string s && int.TryParse(s, out int parsed))
                    {
                        return await SetPositionAsync(parsed);
                    }

                    return false;
                },
                _ => IsConnected && !IsBusy && !SpectrometerManager.Instance.IsDeviceBusy,
                SpectrumTimedButtonHost.GetOwner,
                SpectrumTimedButtonHost.BuildOperationKey,
                "filter-wheel-set-position");
        }

        private bool ConnectCore()
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
                LastErrorMessage = string.Empty;
                log.Info("FilterWheel: 连接成功");
                return true;
            }
            catch (Exception ex)
            {
                LastErrorMessage = $"打开滤色轮串口失败: {ex.Message}";
                log.Error(LastErrorMessage, ex);
                IsConnected = false;
                SerialPort? serialPort = _serialPort;
                _serialPort = null;
                try
                {
                    serialPort?.Dispose();
                }
                catch (Exception disposeException)
                {
                    log.Warn("FilterWheel: 释放打开失败的串口失败", disposeException);
                }
                return false;
            }
        }

        private bool DisconnectCore()
        {
            bool success = true;
            try
            {
                if (_serialPort != null && _serialPort.IsOpen)
                {
                    _serialPort.Close();
                }
            }
            catch (Exception ex)
            {
                LastErrorMessage = $"关闭滤色轮串口失败: {ex.Message}";
                log.Error(LastErrorMessage, ex);
                success = false;
            }
            finally
            {
                IsConnected = false;
                CurrentPosition = -1;
                SerialPort? serialPort = _serialPort;
                _serialPort = null;
                try
                {
                    serialPort?.Dispose();
                }
                catch (Exception ex)
                {
                    log.Warn("FilterWheel: 释放串口失败", ex);
                    success = false;
                }
            }

            return success;
        }

        public bool Connect()
        {
            bool connected = RunSerialized(ConnectCore);
            if (connected)
                _ = QueryPositionAsync();
            return connected;
        }

        public bool Disconnect() => RunSerialized(DisconnectCore);

        private async Task<bool> ConnectAsync()
        {
            bool connected = await RunSerializedAsync(() => Task.Run(ConnectCore));
            if (connected)
                await QueryPositionAsync();
            return connected;
        }

        private Task<bool> DisconnectAsync() => RunSerializedAsync(() => Task.Run(DisconnectCore));

        /// <summary>
        /// Sends "NOW" to query the current filter wheel position.
        /// </summary>
        public async Task<int> QueryPositionAsync()
        {
            string? response = await SendCommandAsync("NOW");
            if (response != null && int.TryParse(response.Trim(), out int pos) && pos >= 0 && pos <= 4)
            {
                bool positionChanged = CurrentPosition != pos;
                CurrentPosition = pos;
                LastErrorMessage = string.Empty;
                log.Info($"FilterWheel: 当前位置 = {pos} ({CurrentPositionName})");
                if (positionChanged)
                    PositionChanged?.Invoke(pos);
                return pos;
            }
            LastErrorMessage = "滤色轮位置查询失败";
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
                bool positionChanged = CurrentPosition != confirmedPos;
                CurrentPosition = confirmedPos;
                LastErrorMessage = string.Empty;
                log.Info($"FilterWheel: 设置位置成功 = {confirmedPos} ({CurrentPositionName})");
                if (positionChanged)
                    PositionChanged?.Invoke(confirmedPos);
                return true;
            }
            LastErrorMessage = $"滤色轮位置 {position} 切换失败";
            log.Warn($"FilterWheel: 设置位置 {position} 失败，响应: '{response}'");
            return false;
        }

        private const int PollingIntervalMs = 16;
        private const int CommandTimeoutMs = 10000;

        private async Task<string?> SendCommandAsync(string cmd)
        {
            return await RunSerializedAsync(async () =>
            {
                if (_serialPort == null || !_serialPort.IsOpen)
                    return null;

                try
                {
                    // Clear input buffer
                    _serialPort.DiscardInBuffer();
                    _serialPort.Write(cmd);

                    string receiveBuffer = "";
                    int maxLoops = CommandTimeoutMs / PollingIntervalMs;

                    for (int i = 0; i < maxLoops; i++)
                    {
                        await Task.Delay(PollingIntervalMs);

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
                    LastErrorMessage = $"滤色轮通信失败: {ex.Message}";
                    log.Error(LastErrorMessage, ex);
                    // The operation already owns commandGate. Tear down the failed port
                    // directly instead of re-entering the same gate.
                    DisconnectCore();
                    return null;
                }
            });
        }

        private async Task<T> RunSerializedAsync<T>(Func<Task<T>> operation)
        {
            Interlocked.Increment(ref activeOperationCount);
            try
            {
                await commandGate.WaitAsync();
                try
                {
                    return await operation();
                }
                finally
                {
                    commandGate.Release();
                }
            }
            finally
            {
                Interlocked.Decrement(ref activeOperationCount);
            }
        }

        private T RunSerialized<T>(Func<T> operation)
        {
            Interlocked.Increment(ref activeOperationCount);
            try
            {
                commandGate.Wait();
                try
                {
                    return operation();
                }
                finally
                {
                    commandGate.Release();
                }
            }
            finally
            {
                Interlocked.Decrement(ref activeOperationCount);
            }
        }

        public void Dispose()
        {
            RunSerialized(DisconnectCore);
            GC.SuppressFinalize(this);
        }
    }
}
