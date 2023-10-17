using System;

namespace ColorVision.MQTT
{
    /// <summary>
    /// 心跳接口
    /// </summary>
    public interface IHeartbeat
    {
        public DateTime LastAliveTime { get; set; }

        public bool IsAlive { get; set; }
    }
}
