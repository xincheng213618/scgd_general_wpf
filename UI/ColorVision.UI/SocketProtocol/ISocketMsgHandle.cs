using System.Net.Sockets;

namespace ColorVision.UI.SocketProtocol
{
    public interface ISocketMsgHandle
    {
        public int Order { get; }
        bool Handle(NetworkStream stream, string message);
    }
}
