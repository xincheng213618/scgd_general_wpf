using ColorVision.Themes;
using ColorVision.UI.Menus;
using Newtonsoft.Json;
using System.Net.Sockets;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Clipboard = ColorVision.Common.NativeMethods.Clipboard;

namespace ColorVision.SocketProtocol
{
    /// <summary>
    /// Socket管理窗口菜单项
    /// </summary>
    public class MenuProjectManager : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.Help;
        public override int Order => 9000;
        public override string Header => Properties.Resources.SocketManagementWindow;

        public override void Execute()
        {
            new SocketManagerWindow() 
            { 
                Owner = Application.Current.GetActiveWindow(), 
                WindowStartupLocation = WindowStartupLocation.CenterOwner 
            }.Show();
        }
    }

    /// <summary>
    /// SocketManagerWindow.xaml 的交互逻辑
    /// 用于管理和监控Socket连接及消息传输
    /// </summary>
    public partial class SocketManagerWindow : Window
    {
        private readonly SocketManager _socketManager;

        public SocketManagerWindow()
        {
            _socketManager = SocketManager.GetInstance();
            InitializeComponent();
            this.ApplyCaption();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            this.DataContext = _socketManager;
            // 加载最近的消息记录
            _socketManager.MessageManager.LoadAll();
        }

        /// <summary>
        /// TCP客户端列表选择改变事件
        /// </summary>
        private void ClientsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 显示选中客户端的详细信息
        }

        /// <summary>
        /// 消息列表选择改变事件
        /// </summary>
        private void MessagesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MessagesListView.SelectedItem is SocketMessage message)
            {
                DetailPanel.DataContext = message;
            }
        }

        /// <summary>
        /// 复制消息内容(右键菜单)
        /// </summary>
        private void CopyMessage_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is SocketMessage message)
            {
                Clipboard.SetText(message.Content ?? string.Empty);
            }
        }

        /// <summary>
        /// 重发消息(右键菜单)
        /// </summary>
        private void ResendMessage_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is SocketMessage message)
            {
                ResendMessageToClient(message);
            }
        }

        /// <summary>
        /// 删除消息(右键菜单)
        /// </summary>
        private void DeleteMessage_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is SocketMessage message)
            {
                _socketManager.MessageManager.DeleteMessage(message);
            }
        }

        /// <summary>
        /// 复制消息内容(底部按钮)
        /// </summary>
        private void CopyContent_Click(object sender, RoutedEventArgs e)
        {
            if (DetailPanel.DataContext is SocketMessage message)
            {
                Clipboard.SetText(message.Content ?? string.Empty);
            }
        }

        /// <summary>
        /// 复制格式化后的消息内容(底部按钮)
        /// </summary>
        private void CopyFormattedContent_Click(object sender, RoutedEventArgs e)
        {
            if (DetailPanel.DataContext is SocketMessage message && !string.IsNullOrEmpty(message.Content))
            {
                try
                {
                    // 尝试格式化JSON
                    var obj = JsonConvert.DeserializeObject(message.Content);
                    string formatted = JsonConvert.SerializeObject(obj, Formatting.Indented);
                    Clipboard.SetText(formatted);
                }
                catch
                {
                    // 如果不是JSON，直接复制原内容
                    Clipboard.SetText(message.Content);
                }
            }
        }

        /// <summary>
        /// 重发消息(底部按钮)
        /// </summary>
        private void ResendContent_Click(object sender, RoutedEventArgs e)
        {
            if (DetailPanel.DataContext is SocketMessage message)
            {
                ResendMessageToClient(message);
            }
        }

        /// <summary>
        /// 删除消息(底部按钮)
        /// </summary>
        private void DeleteContent_Click(object sender, RoutedEventArgs e)
        {
            if (DetailPanel.DataContext is SocketMessage message)
            {
                _socketManager.MessageManager.DeleteMessage(message);
                DetailPanel.DataContext = null;
            }
        }

        /// <summary>
        /// 重发消息到客户端
        /// </summary>
        private void ResendMessageToClient(SocketMessage message)
        {
            if (string.IsNullOrEmpty(message.Content)) return;

            // 查找匹配的客户端连接
            TcpClient? targetClient = null;
            foreach (var client in _socketManager.TcpClients)
            {
                if (client.Client.RemoteEndPoint?.ToString() == message.ClientEndPoint)
                {
                    targetClient = client;
                    break;
                }
            }

            if (targetClient != null && targetClient.Connected)
            {
                try
                {
                    var stream = targetClient.GetStream();
                    byte[] data = Encoding.UTF8.GetBytes(message.Content);
                    stream.Write(data, 0, data.Length);
                    
                    // 记录重发消息
                    var resendMsg = new SocketMessage
                    {
                        ClientEndPoint = message.ClientEndPoint,
                        Direction = SocketMessageDirection.Sent,
                        Content = message.Content,
                        MessageTime = DateTime.Now,
                        EventName = message.EventName,
                        MsgID = message.MsgID
                    };
                    _socketManager.MessageManager.AddMessage(resendMsg);
                    
                    MessageBox.Show(Properties.Resources.ResendSuccess, "ColorVision", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(string.Format(Properties.Resources.ResendFailed, ex.Message), "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show(Properties.Resources.ClientNotConnected, "ColorVision", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// 窗口关闭时的清理逻辑
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
        }
    }
}
