#pragma warning disable CS8604
using Newtonsoft.Json;

namespace ColorVision.SocketProtocol
{
    /// <summary>
    /// Socket响应消息
    /// </summary>
    public class SocketResponse : SocketMessageBase
    {
        public int Code { get; set; }
        public string Msg { get; set; }
        public dynamic? Data { get; set; }

        /// <summary>Runs on the socket worker after a successful write, before reading the next request.</summary>
        [JsonIgnore]
        public Action? AfterSend { get; init; }
    }
}
