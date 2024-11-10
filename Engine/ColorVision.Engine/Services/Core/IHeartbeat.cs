using System;

namespace ColorVision.Engine.Services.Core
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
