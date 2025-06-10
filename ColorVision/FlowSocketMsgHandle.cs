#pragma warning disable CA1711
using ColorVision.UI.Menus;
using ColorVision.SocketProtocol;
using System.Linq;
using System.Net.Sockets;
using System.Windows;

namespace ColorVision.UI
{
    public class FlowSocketMsgHandle : ISocketEventHandler
    {
        public string EventName => SocketConstants.Menu;
        public SocketResponse Handle(NetworkStream stream, SocketRequest request)
        {
            
            if (MenuManager.GetInstance().MenuItems.FirstOrDefault(a => a.Header == request.Params) is IMenuItem  menuItem)
            {
                if (menuItem.Command != null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        menuItem.Command.Execute(null);
                    });
                    return new SocketResponse { Code = 0, Msg = $"Menu Execute {request.Params}", EventName = EventName };
                }
                else
                {
                    return new SocketResponse { Code = -2, Msg = $"Menu Cant Execute ", EventName = EventName };
                }
            }
            else
            {
                return new SocketResponse { Code = -1, Msg = $"Cant Find Menu {request.Params}", EventName = EventName };
            }
        }
    }
}
