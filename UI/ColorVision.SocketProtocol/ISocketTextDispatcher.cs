#pragma warning disable CS8604
using System.Net.Sockets;

namespace ColorVision.SocketProtocol
{
    /// <summary>
    /// 文本消息分发器接口
    /// </summary>
    public interface ISocketTextDispatcher
    {
        string? Handle(NetworkStream stream, string request);
    }
}
