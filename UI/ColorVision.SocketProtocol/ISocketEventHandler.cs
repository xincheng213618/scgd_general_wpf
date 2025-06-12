#pragma warning disable CA1711
using ColorVision.UI.Menus;
using System.Net.Sockets;
using System.Windows;

namespace ColorVision.SocketProtocol
{
    public interface ISocketEventHandler
    {
        string EventName { get; }
        SocketResponse Handle(NetworkStream stream, SocketRequest request);
    }
}
