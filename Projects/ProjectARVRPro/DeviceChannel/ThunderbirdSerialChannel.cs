using log4net;

namespace ProjectARVRPro.DeviceChannel
{
    /// <summary>
    /// 雷鸟设备串口通道 — 基于 SerialPortHelper 的长连接通道
    /// <para>支持切图（AT+DOWN / AT+UPWARDS）和亮度控制等 AT 指令</para>
    /// </summary>
    public class ThunderbirdSerialChannel : IDeviceChannel
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ThunderbirdSerialChannel));

        private SerialPortHelper? _serialHelper;
        private readonly DeviceChannelConfig _config;

        public string Name => _config.Name;

        public bool IsConnected => _serialHelper?.IsOpen ?? false;

        public ThunderbirdSerialChannel(DeviceChannelConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public Task ConnectAsync()
        {
            if (_serialHelper?.IsOpen == true)
            {
                log.Info($"雷鸟通道 [{Name}] 已处于连接状态，跳过重复连接");
                return Task.CompletedTask;
            }

            _serialHelper?.Dispose();
            _serialHelper = new SerialPortHelper { TimeoutMs = _config.TimeoutMs };
            _serialHelper.Open(_config.SerialPortName, _config.BaudRate);
            log.Info($"雷鸟通道 [{Name}] 已连接: {_config.SerialPortName}, BaudRate={_config.BaudRate}");
            return Task.CompletedTask;
        }

        public Task DisconnectAsync()
        {
            _serialHelper?.Dispose();
            _serialHelper = null;
            log.Info($"雷鸟通道 [{Name}] 已断开");
            return Task.CompletedTask;
        }

        public async Task<DeviceCommandResult> ExecuteCommandAsync(string command, int? timeoutMs = null)
        {
            if (_serialHelper == null || !_serialHelper.IsOpen)
                return new DeviceCommandResult { Success = false, ErrorMessage = "雷鸟通道未连接" };

            try
            {
                string response = await _serialHelper.SendAndReceiveAsync(command, timeoutMs ?? _config.TimeoutMs);
                log.Info($"雷鸟通道 [{Name}] 指令完成: {command} → {response}");
                return new DeviceCommandResult { Success = true, Response = response };
            }
            catch (TimeoutException ex)
            {
                log.Warn($"雷鸟通道 [{Name}] 指令超时: {command}", ex);
                return new DeviceCommandResult { Success = false, ErrorMessage = ex.Message };
            }
            catch (Exception ex)
            {
                log.Error($"雷鸟通道 [{Name}] 指令异常: {command}", ex);
                return new DeviceCommandResult { Success = false, ErrorMessage = ex.Message };
            }
        }

        /// <summary>
        /// 向下切图
        /// </summary>
        public Task<DeviceCommandResult> SwitchDownAsync(int? timeoutMs = null)
        {
            return ExecuteCommandAsync("AT+DOWN", timeoutMs);
        }

        /// <summary>
        /// 向上切图
        /// </summary>
        public Task<DeviceCommandResult> SwitchUpAsync(int? timeoutMs = null)
        {
            return ExecuteCommandAsync("AT+UPWARDS", timeoutMs);
        }

        public void Dispose()
        {
            _serialHelper?.Dispose();
            _serialHelper = null;
            GC.SuppressFinalize(this);
        }
    }
}
