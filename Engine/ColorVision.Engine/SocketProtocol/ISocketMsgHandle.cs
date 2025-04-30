using System.Net.Sockets;

namespace ColorVision.Engine
{
    public interface ISocketMsgHandle
    {
        public int Order { get; }
        bool Handle(NetworkStream stream, string message);
    }
}
