#pragma warning disable CA1711
using ColorVision.UI.Menus;
using System.Net.Sockets;
using System.Windows;

namespace ColorVision.SocketProtocol
{
    public static class SocketConstants
    {
        public const string Menu = "Menu";
    }


    public interface ISocketEventHandler
    {
        string EventName { get; }
        SocketResponse Handle(NetworkStream stream, SocketRequest request);
    }
}
