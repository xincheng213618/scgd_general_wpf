#pragma warning disable CS8604
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
    }
}
