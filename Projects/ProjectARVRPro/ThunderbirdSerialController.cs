using log4net;
using System.IO.Ports;

namespace ProjectARVRPro
{
    /// <summary>
    /// 雷鸟设备串口通信控制器 - 独立的业务逻辑层
    /// 职责：管理串口连接、收发命令、状态管理
    /// </summary>
    public class ThunderbirdSerialController : IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ThunderbirdSerialController));
        private static readonly char[] ResponseSplitChars = new[] { '\r', '\n' };

        public event EventHandler? ConnectionStateChanged;

        private static ThunderbirdSerialController _instance;
        private static readonly object _locker = new();
        public static ThunderbirdSerialController GetInstance() { lock (_locker) { _instance ??= new ThunderbirdSerialController(); return _instance; } }

        private SerialPortHelper? _serialHelper;
        private int _currentBrightnessLevel = -1;
        private string? _currentPortName;
        private int _currentBaudRate = 9600;
        private int _currentTimeoutMs = 1000;

        /// <summary>
        /// 当前亮度档位 (0~16, 对应 0x20~0x30)，-1 表示未知
        /// </summary>
        public int CurrentBrightnessLevel => _currentBrightnessLevel;

        /// <summary>
        /// 串口是否已连接
        /// </summary>
        public bool IsConnected => _serialHelper?.IsOpen ?? false;

        /// <summary>
        /// 当前连接串口名
        /// </summary>
        public string? CurrentPortName => _currentPortName;

        /// <summary>
        /// 当前连接波特率
        /// </summary>
        public int CurrentBaudRate => _currentBaudRate;

        /// <summary>
        /// 当前连接超时(ms)
        /// </summary>
        public int CurrentTimeoutMs => _currentTimeoutMs;

        /// <summary>
        /// 打开串口连接
        /// </summary>
        public void Open(string portName, int baudRate = 9600, int timeoutMs = 1000)
        {
            if (IsConnected)
            {
                log.Warn($"串口已经打开: {_serialHelper?.PortName}");
                return;
            }

            try
            {
                _serialHelper = new SerialPortHelper { TimeoutMs = timeoutMs };
                _serialHelper.Open(portName, baudRate);
                _currentPortName = portName;
                _currentBaudRate = baudRate;
                _currentTimeoutMs = timeoutMs;
                log.Info($"雷鸟串口已打开: {portName}, BaudRate={baudRate}, Timeout={timeoutMs}ms");
                ConnectionStateChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                log.Error($"打开串口失败: {portName}", ex);
                _serialHelper = null;
                throw;
            }
        }

        /// <summary>
        /// 关闭串口连接
        /// </summary>
        public void Close()
        {
            bool wasConnected = IsConnected;
            try
            {
                _serialHelper?.Dispose();
                _serialHelper = null;
                _currentPortName = null;
                _currentBrightnessLevel = -1;
                log.Info("雷鸟串口已关闭");
                if (wasConnected)
                    ConnectionStateChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                log.Error("关闭串口失败", ex);
            }
        }

        /// <summary>
        /// 发送命令并等待响应（1发1收模式）
        /// </summary>
        public async Task<string?> SendCommandAsync(string command, int timeoutMs = 0)
        {
            if (_serialHelper == null || !_serialHelper.IsOpen)
            {
                log.Error("串口未打开，无法发送命令");
                return null;
            }

            int timeout = timeoutMs > 0 ? timeoutMs : _serialHelper.TimeoutMs;

            try
            {
                log.Debug($"[TX] {command}");
                string response = await _serialHelper.SendAndReceiveAsync(command, timeout);
                log.Debug($"[RX] {response}");
                return response;
            }
            catch (TimeoutException)
            {
                log.Warn($"串口超时: 发送 \"{command}\" 后 {timeout}ms 内未收到响应");
                return null;
            }
            catch (Exception ex)
            {
                log.Error($"串口发送失败: {command}", ex);
                return null;
            }
        }

        /// <summary>
        /// 向上切图 (AT+UPWARDS)
        /// </summary>
        public async Task<bool> SwitchUpAsync(int timeoutMs = 0)
        {
            string? response = await SendCommandAsync("AT+UPWARDS", timeoutMs);
            if (response != null)
            {
                ProcessResponse(response);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 向下切图 (AT+DOWN)
        /// </summary>
        public async Task<bool> SwitchDownAsync(int timeoutMs = 0)
        {
            string? response = await SendCommandAsync("AT+DOWN", timeoutMs);
            if (response != null)
            {
                ProcessResponse(response);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 查询当前亮度 (AT+R 30)
        /// </summary>
        public async Task<bool> QueryBrightnessAsync(int timeoutMs = 0)
        {
            string? response = await SendCommandAsync("AT+R 30", timeoutMs);
            if (response != null)
            {
                ProcessResponse(response);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 设置亮度档位 (AT+W 30 XX)
        /// </summary>
        public async Task<bool> SetBrightnessLevelAsync(int level, int timeoutMs = 0)
        {
            if (level < 0) level = 0;
            if (level > 16) level = 16;

            int registerValue = 0x20 + level;
            string command = $"AT+W 30 {registerValue:X2}";

            string? response = await SendCommandAsync(command, timeoutMs);
            if (response != null)
            {
                _currentBrightnessLevel = level;
                ProcessResponse(response);
                log.Info($"已设置亮度: 档位 {level} (0x{registerValue:X2})");
                return true;
            }
            return false;
        }

        /// <summary>
        /// 处理串口应答：判断 OK/error，或解析亮度读取结果
        /// </summary>
        private void ProcessResponse(string response)
        {
            if (string.IsNullOrWhiteSpace(response))
                return;

            string[] lines = response.Split(ResponseSplitChars, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                string l = line.Trim();
                if (l.Equals("OK", StringComparison.OrdinalIgnoreCase))
                {
                    log.Info("操作成功 (OK)");
                    continue;
                }
                if (l.Contains("error", StringComparison.OrdinalIgnoreCase))
                {
                    log.Warn("操作失败 (error)");
                    continue;
                }

                // 尝试解析为十六进制亮度值
                if (TryParseHexBrightness(l, out int brightnessValue))
                {
                    int level = brightnessValue - 0x20;
                    if (level >= 0 && level <= 16)
                    {
                        _currentBrightnessLevel = level;
                        log.Info($"当前亮度: 档位 {level} (0x{brightnessValue:X2})");
                    }
                }
            }
        }

        /// <summary>
        /// 尝试从响应行中解析十六进制亮度值
        /// </summary>
        private static bool TryParseHexBrightness(string line, out int value)
        {
            value = 0;
            string cleaned = line.Trim();
            if (cleaned.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                cleaned = cleaned[2..];

            if (int.TryParse(cleaned, System.Globalization.NumberStyles.HexNumber, null, out int parsed))
            {
                if (parsed >= 0x20 && parsed <= 0x30)
                {
                    value = parsed;
                    return true;
                }
            }
            if (int.TryParse(cleaned, out int decParsed))
            {
                if (decParsed >= 0x20 && decParsed <= 0x30)
                {
                    value = decParsed;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 一键快速切图（用于流程中）
        /// </summary>
        public async Task<bool> QuickSwitchDownAsync(int timeoutMs = 1000)
        {
            try
            {

                return await SwitchDownAsync(timeoutMs);
            }
            catch (Exception ex)
            {
                log.Error($"快速切图失败: ", ex);
                return false;
            }
        }

        public void Dispose()
        {
            Close();
            GC.SuppressFinalize(this);
        }
    }
}
