using System.Net.Sockets;

namespace ColorVision.SocketProtocol
{
    public static class SocketConstants
    {
        public const string Menu = "Menu";
    }


    public interface ISocketJsonHandler
    {
        string EventName { get; }
        SocketResponse Handle(NetworkStream stream, SocketRequest request);
    }
}
