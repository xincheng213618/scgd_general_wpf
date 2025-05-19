using ColorVision.UI.Menus;
using System.Windows;

namespace WindowsServicePlugin.CVWinSMS
{
    public class UpdateService : MenuItemBase
    {
        public override string OwnerGuid => "ServiceLog";
        public override string Header => "更新服务";
        public override int Order => 3;


        public override void Execute()
        {
            UpdateServiceWindow updateServiceWindow = new UpdateServiceWindow();
            updateServiceWindow.Show();
        }

    }


    /// <summary>
    /// UpdateServiceWindow.xaml 的交互逻辑
    /// </summary>
    public partial class UpdateServiceWindow : Window
    {
        public UpdateServiceWindow()
        {
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            this.DataContext = UpdateService1.Instance;
        }
    }
}
