#pragma warning disable CA1822
using ColorVision.Common.MVVM;
using log4net;
using Spectrum.TimedButtons;
using System.IO.Ports;
using System.Text;
using System.Windows.Input;

namespace Spectrum.Configs
{
    public class ShutterController : ViewModelBase, IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ShutterController));

        private SerialPort? _serialPort;
        private int activeOperationCount;
        private readonly SemaphoreSlim commandGate = new(1, 1);

        public bool IsBusy => Volatile.Read(ref activeOperationCount) > 0;

        // 绑定到界面的配置
        public ShutterConfig Config=> SpectrumConfig.Instance.ShutterConfig;

        // 连接状态
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

        // 命令
        public ICommand ConnectCommand { get; }
        public ICommand DisconnectCommand { get; }
        public ICommand OpenShutterCommand { get; }
        public ICommand CloseShutterCommand { get; }

        public ShutterController()
        {
            ConnectCommand = new TimedButtonCommand(
                async _ => await ConnectAsync(),
                _ => !IsConnected && !IsBusy,
                SpectrumTimedButtonHost.GetOwner,
                SpectrumTimedButtonHost.BuildOperationKey,
                "shutter-connect");

            DisconnectCommand = new TimedButtonCommand(
                async _ => await DisconnectAsync(),
                _ => IsConnected && !IsBusy && !SpectrometerManager.Instance.IsDeviceBusy,
                SpectrumTimedButtonHost.GetOwner,
                SpectrumTimedButtonHost.BuildOperationKey,
                "shutter-disconnect");

            OpenShutterCommand = new TimedButtonCommand(
                _ => SendCommand(Config.OpenCmd, "turn on"),
                _ => IsConnected && !IsBusy && !SpectrometerManager.Instance.IsDeviceBusy,
                SpectrumTimedButtonHost.GetOwner,
                SpectrumTimedButtonHost.BuildOperationKey,
                "shutter-open");

            CloseShutterCommand = new TimedButtonCommand(
                _ => SendCommand(Config.CloseCmd, "turn off"),
                _ => IsConnected && !IsBusy && !SpectrometerManager.Instance.IsDeviceBusy,
                SpectrumTimedButtonHost.GetOwner,
                SpectrumTimedButtonHost.BuildOperationKey,
                "shutter-close");
        }

        private bool ConnectCore()
        {
            try
            {
                if (_serialPort != null)
                    _serialPort.Dispose();

                _serialPort = new SerialPort(Config.SzComName, Config.BaudRate)
                {
                    ReadTimeout = 1000,
                    WriteTimeout = 1000
                };
                log.Info($"尝试连接到串口 {Config.SzComName}，波特率 {Config.BaudRate}");
                _serialPort.Open();
                IsConnected = true;
                LastErrorMessage = string.Empty;
                log.Info($"连接成功");
                return true;

            }
            catch (Exception ex)
            {
                LastErrorMessage = $"打开串口失败: {ex.Message}";
                log.Warn(LastErrorMessage, ex);
                IsConnected = false;
                SerialPort? serialPort = _serialPort;
                _serialPort = null;
                try
                {
                    serialPort?.Dispose();
                }
                catch (Exception disposeException)
                {
                    log.Warn("释放打开失败的快门串口失败", disposeException);
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
                LastErrorMessage = $"关闭串口失败: {ex.Message}";
                log.Warn(LastErrorMessage, ex);
                success = false;
            }
            finally
            {
                IsConnected = false;
                SerialPort? serialPort = _serialPort;
                _serialPort = null;
                try
                {
                    serialPort?.Dispose();
                }
                catch (Exception ex)
                {
                    log.Warn("释放快门串口失败", ex);
                    success = false;
                }
            }

            return success;
        }

        private Task<bool> ConnectAsync() => RunSerializedAsync(() => Task.Run(ConnectCore));

        private Task<bool> DisconnectAsync() => RunSerializedAsync(() => Task.Run(DisconnectCore));

        public async Task<bool> OpenShutter()
        {
            return await SendCommand(Config.OpenCmd, "turn on");
        }
        public async Task<bool> CloseShutter()
        {
            return await SendCommand(Config.CloseCmd, "turn off");
        }

        private async Task<bool> SendCommand(string cmd, string expectedResponse)
        {
            return await RunSerializedAsync(async () =>
            {
                if (_serialPort != null && _serialPort.IsOpen)
                {
                    try
                    {
                        // Drop delayed responses from the previous operation before sending a
                        // new command. Otherwise a stale acknowledgement can invert dark/light.
                        _serialPort.DiscardInBuffer();
                        _serialPort.Write(cmd);

                        string receiveBuffer = "";
                        string unexpectedResponse = expectedResponse.Equals("turn on", StringComparison.OrdinalIgnoreCase)
                            ? "turn off"
                            : "turn on";

                        // 3. 循环等待接收
                        // 根据 Configs.DelayTime 计算循环次数，例如 1000ms / 16ms ≈ 62次
                        int maxLoops = (Config.DelayTime > 0 ? Config.DelayTime : 1000) / 16;
                        if (maxLoops < 10) maxLoops = 60; // 保底循环次数

                        for (int i = 0; i < maxLoops; i++)
                        {
                            await Task.Delay(16); // 非阻塞延时，UI 不会卡顿

                            if (_serialPort == null || !_serialPort.IsOpen) break;

                            int bytesread = _serialPort.BytesToRead;
                            if (bytesread > 0)
                            {
                                byte[] buff = new byte[bytesread];
                                _serialPort.Read(buff, 0, bytesread);

                                // 将新读到的数据拼接到缓存中，防止数据包被从中间截断
                                string msg = Encoding.UTF8.GetString(buff);
                                receiveBuffer += msg;

                                if (receiveBuffer.Contains(unexpectedResponse, StringComparison.OrdinalIgnoreCase))
                                {
                                    LastErrorMessage = $"快门返回了相反状态：{unexpectedResponse}";
                                    log.Warn(LastErrorMessage);
                                    return false;
                                }

                                if (receiveBuffer.Contains(expectedResponse, StringComparison.OrdinalIgnoreCase))
                                {
                                    LastErrorMessage = string.Empty;
                                    return true;
                                }
                            }
                        }
                        LastErrorMessage = "快门未在规定时间内返回确认";
                        return false;
                    }
                    catch (Exception ex)
                    {
                        LastErrorMessage = $"快门通信失败: {ex.Message}";
                        log.Error(LastErrorMessage, ex);
                        // The operation already owns commandGate, so disconnect the port directly
                        // instead of trying to enter the same gate recursively.
                        DisconnectCore();
                        return false;
                    }
                }

                LastErrorMessage = "快门未连接";
                return false;
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
