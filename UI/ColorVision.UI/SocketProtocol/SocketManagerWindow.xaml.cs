using ColorVision.UI.Menus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ColorVision.UI.SocketProtocol
{
    public class MenuProjectManager : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.Help;
        public override int Order => 9000;
        public override string Header => "SocketManagerWindow";

        public override void Execute()
        {
            new SocketManagerWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show();
        }
    }

    /// <summary>
    /// SocketManagerWindow.xaml 的交互逻辑
    /// </summary>
    public partial class SocketManagerWindow : Window
    {
        public SocketManagerWindow()
        {
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            this.DataContext = SocketManager.GetInstance();
        }

        private void ListViewPlugins_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }
    }
}
