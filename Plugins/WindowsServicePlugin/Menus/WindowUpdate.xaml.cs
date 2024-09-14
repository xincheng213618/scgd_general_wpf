using ColorVision.Themes;
using System.Windows;

namespace WindowsServicePlugin
{
    /// <summary>
    /// WindowUpdate.xaml 的交互逻辑
    /// </summary>
    public partial class WindowUpdate : Window
    {
        InstallMQTT InstallMQTT;
        public WindowUpdate(InstallMQTT installMQTT)
        {
            InstallMQTT = installMQTT;
            InitializeComponent();
            this.ApplyCaption();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            DataContext = InstallMQTT;
        }
    }
}
