using ColorVision.Themes;
using ColorVision.UI.LogImp;
using ColorVision.UI.Menus;
using System.Windows;

namespace WindowsServicePlugin.CVWinSMS
{
    public class MenuUpdateService : MenuItemBase
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
    public partial class UpdateServiceWindow : Window,IDisposable
    {
        public UpdateServiceWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
        }
        private LogOutput? logOutput;
        private void Window_Initialized(object sender, EventArgs e)
        {
            this.DataContext = UpdateService.Instance;
            logOutput = new LogOutput("%date{HH:mm:ss} [%thread] %-5level %message%newline");
            LogGrid.Children.Add(logOutput);
        }

        public void Dispose()
        {
            logOutput?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
