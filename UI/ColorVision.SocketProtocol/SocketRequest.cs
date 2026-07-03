#pragma warning disable CS8604
namespace ColorVision.SocketProtocol
{
    /// <summary>
    /// Socket请求消息
    /// </summary>
    public class SocketRequest : SocketMessageBase
    {
        public string Params { get; set; }
    }
}
