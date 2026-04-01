namespace ProjectARVRPro.DeviceChannel
{
    /// <summary>
    /// 设备通道执行结果
    /// </summary>
    public class DeviceCommandResult
    {
        public bool Success { get; set; }
        public string? Response { get; set; }
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// 设备通信通道抽象接口 — 统一封装串口、Socket 等不同通信方式
    /// <para>每个实现代表一种设备类型（如雷鸟串口、通用串口、Socket 等）</para>
    /// </summary>
    public interface IDeviceChannel : IDisposable
    {
        /// <summary>
        /// 通道名称（用于标识和日志）
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 当前是否已连接
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// 建立连接
        /// </summary>
        Task ConnectAsync();

        /// <summary>
        /// 断开连接
        /// </summary>
        Task DisconnectAsync();

        /// <summary>
        /// 发送命令并等待应答
        /// </summary>
        /// <param name="command">指令内容</param>
        /// <param name="timeoutMs">超时（毫秒），null 则使用默认值</param>
        Task<DeviceCommandResult> ExecuteCommandAsync(string command, int? timeoutMs = null);
    }
}
