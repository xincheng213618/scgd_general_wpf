using log4net;
using System.Net.Sockets;
using System.Text;

namespace ProjectARVRPro.DeviceChannel
{
    /// <summary>
    /// Socket 通道 — 基于 TCP 长连接的通信通道
    /// </summary>
    public class SocketChannel : IDeviceChannel
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(SocketChannel));

        private TcpClient? _client;
        private NetworkStream? _stream;
        private readonly DeviceChannelConfig _config;

        public string Name => _config.Name;

        public bool IsConnected => _client?.Connected ?? false;

        public SocketChannel(DeviceChannelConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public async Task ConnectAsync()
        {
            if (_client?.Connected == true)
            {
                log.Info($"Socket通道 [{Name}] 已处于连接状态，跳过重复连接");
                return;
            }

            Cleanup();
            _client = new TcpClient();
            int timeout = _config.TimeoutMs > 0 ? _config.TimeoutMs : 5000;
            var connectTask = _client.ConnectAsync(_config.Host, _config.Port);
            if (await Task.WhenAny(connectTask, Task.Delay(timeout)) != connectTask)
            {
                Cleanup();
                throw new TimeoutException($"Socket通道 [{Name}] 连接超时 ({timeout}ms): {_config.Host}:{_config.Port}");
            }
            await connectTask; // propagate exceptions
            _stream = _client.GetStream();
            _stream.ReadTimeout = timeout;
            _stream.WriteTimeout = timeout;
            log.Info($"Socket通道 [{Name}] 已连接: {_config.Host}:{_config.Port}");
        }

        public Task DisconnectAsync()
        {
            Cleanup();
            log.Info($"Socket通道 [{Name}] 已断开");
            return Task.CompletedTask;
        }

        public async Task<DeviceCommandResult> ExecuteCommandAsync(string command, int? timeoutMs = null)
        {
            if (_stream == null || _client == null || !_client.Connected)
                return new DeviceCommandResult { Success = false, ErrorMessage = "Socket通道未连接" };

            try
            {
                byte[] data = Encoding.UTF8.GetBytes(command);
                await _stream.WriteAsync(data, 0, data.Length);
                log.Info($"Socket通道 [{Name}] 已发送: {command}");

                byte[] buffer = new byte[4096];
                int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length);
                string response = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                log.Info($"Socket通道 [{Name}] 应答: {response}");
                return new DeviceCommandResult { Success = true, Response = response };
            }
            catch (Exception ex)
            {
                log.Error($"Socket通道 [{Name}] 指令异常: {command}", ex);
                return new DeviceCommandResult { Success = false, ErrorMessage = ex.Message };
            }
        }

        private void Cleanup()
        {
            _stream?.Dispose();
            _stream = null;
            _client?.Dispose();
            _client = null;
        }

        public void Dispose()
        {
            Cleanup();
            GC.SuppressFinalize(this);
        }
    }
}
