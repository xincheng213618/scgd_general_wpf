using ColorVision.UI.Menus;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.SocketProtocol
{
    /// <summary>
    /// Socket管理窗口菜单项
    /// </summary>
    public class MenuProjectManager : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.Help;
        public override int Order => 9000;
        public override string Header => "Socket管理窗口";

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
            InitializeComponent();
            _socketManager = SocketManager.GetInstance();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            // 设置数据上下文
            this.DataContext = _socketManager;
        }

        /// <summary>
        /// ListView选择改变事件
        /// 当前未使用，保留用于未来扩展功能（如显示选中连接的详细信息）
        /// </summary>
        private void ListViewPlugins_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 预留：可以在此处实现选中TCP客户端时显示详细信息的逻辑
            // 例如：显示客户端IP、端口、连接时间、发送/接收字节数等统计信息
        }

        /// <summary>
        /// 窗口关闭时的清理逻辑
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            // 当前SocketManager是单例，不在此处释放
            // 如需清理，可在此添加特定逻辑
        }
    }
}
