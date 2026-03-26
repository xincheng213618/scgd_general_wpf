using log4net;
using System.Diagnostics;
using System.IO.Ports;
using System.Text;

namespace ProjectARVRPro
{
    /// <summary>
    /// 串口通信助手 — 封装工控环境下的1发1收同步通信模式
    /// <para>特性：</para>
    /// <list type="bullet">
    /// <item>发送后同步等待响应，避免 DataReceived 事件的复杂性</item>
    /// <item>可配置超时（默认1000ms），超时后抛出 <see cref="TimeoutException"/></item>
    /// <item>线程安全（lock 保护），适用于工控环境</item>
    /// <item>可独立复用，不依赖 UI 框架</item>
    /// </list>
    /// </summary>
    public class SerialPortHelper : IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(SerialPortHelper));

        private SerialPort? _serialPort;
        private readonly object _lock = new();
        private bool _disposed;

        /// <summary>
        /// 收发超时时间（毫秒），默认1000ms，用户可配置
        /// </summary>
        public int TimeoutMs { get; set; } = 1000;

        /// <summary>
        /// 串口是否已打开
        /// </summary>
        public bool IsOpen => _serialPort?.IsOpen ?? false;

        /// <summary>
        /// 当前串口名称
        /// </summary>
        public string? PortName => _serialPort?.PortName;

        /// <summary>
        /// 发送命令时追加的行终止符，默认 "\r\n"
        /// </summary>
        public string NewLine { get; set; } = "\r\n";

        /// <summary>
        /// 接收轮询间隔（毫秒），默认2ms
        /// </summary>
        public int PollIntervalMs { get; set; } = 2;

        /// <summary>
        /// 收到数据后额外等待时间（毫秒），确保完整帧接收，默认20ms
        /// </summary>
        public int ReceiveSettleMs { get; set; } = 20;

        /// <summary>
        /// 打开串口
        /// </summary>
        public void Open(string portName, int baudRate = 115200, Parity parity = Parity.None, int dataBits = 8, StopBits stopBits = StopBits.One)
        {
            lock (_lock)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                Close();

                _serialPort = new SerialPort(portName, baudRate, parity, dataBits, stopBits)
                {
                    ReadTimeout = TimeoutMs,
                    WriteTimeout = TimeoutMs,
                    Encoding = Encoding.ASCII,
                };
                _serialPort.Open();
                log.Info($"串口已打开: {portName}, BaudRate={baudRate}");
            }
        }

        /// <summary>
        /// 关闭串口
        /// </summary>
        public void Close()
        {
            lock (_lock)
            {
                if (_serialPort != null)
                {
                    try
                    {
                        if (_serialPort.IsOpen)
                        {
                            _serialPort.DiscardInBuffer();
                            _serialPort.DiscardOutBuffer();
                            _serialPort.Close();
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Warn("关闭串口时出错", ex);
                    }
                    finally
                    {
                        _serialPort.Dispose();
                        _serialPort = null;
                    }
                    log.Info("串口已关闭");
                }
            }
        }

        /// <summary>
        /// 发送命令并同步等待响应（1发1收模式）
        /// </summary>
        /// <param name="command">发送的命令文本</param>
        /// <param name="timeoutMs">本次超时时间（毫秒），null 则使用 <see cref="TimeoutMs"/></param>
        /// <returns>设备返回的完整响应（已 Trim）</returns>
        /// <exception cref="InvalidOperationException">串口未打开</exception>
        /// <exception cref="TimeoutException">在超时时间内未收到响应</exception>
        public string SendAndReceive(string command, int? timeoutMs = null)
        {
            lock (_lock)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                if (_serialPort == null || !_serialPort.IsOpen)
                    throw new InvalidOperationException("串口未打开");

                int timeout = timeoutMs ?? TimeoutMs;

                // 清空接收缓冲区，确保不会读到旧数据
                _serialPort.DiscardInBuffer();

                // 发送命令
                _serialPort.WriteTimeout = timeout;
                _serialPort.Write(command + NewLine);
                log.Info($"[TX] {command}");

                // 轮询等待响应数据
                var sb = new StringBuilder();
                var sw = Stopwatch.StartNew();
                long lastDataTimestamp = -1;

                while (sw.ElapsedMilliseconds < timeout)
                {
                    if (_serialPort.BytesToRead > 0)
                    {
                        sb.Append(_serialPort.ReadExisting());
                        lastDataTimestamp = sw.ElapsedMilliseconds;
                        string current = sb.ToString();

                        // 收到至少一个完整行（含换行符）则认为响应完毕
                        if (current.Contains('\n') || current.Contains('\r'))
                        {
                            // 额外等待确保完整帧（设备可能分多次发送）
                            Thread.Sleep(ReceiveSettleMs);
                            if (_serialPort.BytesToRead > 0)
                                sb.Append(_serialPort.ReadExisting());

                            string response = sb.ToString().Trim();
                            log.Info($"[RX] {response}");
                            return response;
                        }
                    }
                    else if (lastDataTimestamp >= 0 && (sw.ElapsedMilliseconds - lastDataTimestamp) >= ReceiveSettleMs)
                    {
                        // 已收到数据但无换行符，且空闲时间超过 ReceiveSettleMs → 视为响应完毕
                        string response = sb.ToString().Trim();
                        if (!string.IsNullOrEmpty(response))
                        {
                            log.Info($"[RX] {response}");
                            return response;
                        }
                    }
                    Thread.Sleep(PollIntervalMs);
                }

                // 超时：检查是否有部分数据
                if (_serialPort.BytesToRead > 0)
                    sb.Append(_serialPort.ReadExisting());

                string partial = sb.Length > 0 ? sb.ToString().Trim() : "";
                string msg = string.IsNullOrEmpty(partial)
                    ? $"串口接收超时 ({timeout}ms), 指令: {command}"
                    : $"串口接收超时 ({timeout}ms), 指令: {command}, 部分数据: {partial}";
                log.Warn(msg);
                throw new TimeoutException(msg);
            }
        }

        /// <summary>
        /// 异步发送命令并等待响应（UI 友好版本，不阻塞 UI 线程）
        /// </summary>
        /// <param name="command">发送的命令文本</param>
        /// <param name="timeoutMs">本次超时时间（毫秒），null 则使用 <see cref="TimeoutMs"/></param>
        /// <returns>设备返回的完整响应（已 Trim）</returns>
        /// <exception cref="InvalidOperationException">串口未打开</exception>
        /// <exception cref="TimeoutException">在超时时间内未收到响应</exception>
        public Task<string> SendAndReceiveAsync(string command, int? timeoutMs = null)
        {
            return Task.Run(() => SendAndReceive(command, timeoutMs));
        }

        /// <summary>
        /// 仅发送命令，不等待响应
        /// </summary>
        public void Send(string command)
        {
            lock (_lock)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                if (_serialPort == null || !_serialPort.IsOpen)
                    throw new InvalidOperationException("串口未打开");

                _serialPort.WriteTimeout = TimeoutMs;
                _serialPort.Write(command + NewLine);
                log.Info($"[TX] {command}");
            }
        }

        /// <summary>
        /// 获取系统中可用的串口名称列表
        /// </summary>
        public static string[] GetPortNames() => SerialPort.GetPortNames();

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                Close();
            }
            GC.SuppressFinalize(this);
        }
    }
}
