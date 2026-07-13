#pragma warning disable CS8604
using Newtonsoft.Json;

namespace ColorVision.SocketProtocol
{
    /// <summary>
    /// Socket消息基类
    /// </summary>
    public class SocketMessageBase
    {
        public string Version { get; set; }
        public string MsgID { get; set; }
        public string EventName { get; set; }
        public string SerialNumber { get; set; }

        public override string ToString() => JsonConvert.SerializeObject(this);
    }
}
