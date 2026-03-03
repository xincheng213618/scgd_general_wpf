using ColorVision.Common.MVVM;
using log4net;
using Microsoft.VisualBasic.Logging;
using System.IO.Ports;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace Spectrum.Configs
{
    public class ShutterController : ViewModelBase, IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ShutterController));

        private SerialPort? _serialPort;

        // 绑定到界面的配置
        public ShutterConfig Config { get; set; }

        // 连接状态
        private bool _isConnected;
        public bool IsConnected
        {
            get => _isConnected;
            set { _isConnected = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusText)); }
        }

        public string StatusText => IsConnected ? "已连接 (Connected)" : "未连接 (Disconnected)";

        // 命令
        public ICommand ConnectCommand { get; }
        public ICommand DisconnectCommand { get; }
        public ICommand OpenShutterCommand { get; }
        public ICommand CloseShutterCommand { get; }

        public ShutterController()
        {
            Config = SpectrumConfig.Instance.ShutterConfig;
            ConnectCommand = new RelayCommand(_ => Connect(), _ => !IsConnected);
            DisconnectCommand = new RelayCommand(_ => Disconnect(), _ => IsConnected);
            OpenShutterCommand = new RelayCommand(_ => SendCommand(Config.OpenCmd), _ => IsConnected);
            CloseShutterCommand = new RelayCommand(_ => SendCommand(Config.CloseCmd), _ => IsConnected);
        }

        private void Connect()
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
                log.Info($"连接成功");

            }
            catch (Exception ex)
            {
                log.Info($"打开串口失败: {ex.Message}");
                MessageBox.Show($"打开串口失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                IsConnected = false;
            }
        }

        private void Disconnect()
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
                log.Info($"关闭串口失败: {{ex.Message}}");

                MessageBox.Show($"关闭串口失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsConnected = false;
                _serialPort?.Dispose();
                _serialPort = null;
            }
        }

        public async Task<bool> OpenShutter()
        {
            return await SendCommand(Config.OpenCmd);
        }
        public async Task<bool> CloseShutter()
        {
            return await SendCommand(Config.CloseCmd);
        }


        private async Task<bool> SendCommand(string cmd)
        {
            if (_serialPort != null && _serialPort.IsOpen)
            {
                try
                {
                    // 发送指令
                    _serialPort.Write(cmd);

                    string receiveBuffer = "";

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

                            // 忽略大小写检查返回值
                            if (receiveBuffer.Contains("turn on", StringComparison.OrdinalIgnoreCase))
                            {
                                return true;
                            }
                            else if (receiveBuffer.Contains("turn off", StringComparison.OrdinalIgnoreCase))
                            {
                                return true;
                            }
                        }
                    }
                    return false;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"发送或读取指令失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    Disconnect(); // 异常通常是线被拔了，直接断开
                    return false;
                }
                finally
                {
                }
            }

            return false;
        }

        public void Dispose()
        {
            _serialPort?.Close();
            _serialPort?.Dispose();
            _serialPort = null;
        }
    }
}
