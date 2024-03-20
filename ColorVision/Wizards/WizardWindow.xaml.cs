using ColorVision.MQTT;
using ColorVision.MySql;
using ColorVision.RC;
using ColorVision.Settings;
using System.Diagnostics;
using System.Windows;

namespace ColorVision.Wizards
{
    /// <summary>
    /// WizardWindow.xaml 的交互逻辑
    /// </summary>
    public partial class WizardWindow : Window
    {
        public WizardWindow()
        {
            InitializeComponent();
        }
        private void Window_Initialized(object sender, System.EventArgs e)
        {
            this.DataContext = ConfigHandler.GetInstance().SoftwareConfig;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            ConfigHandler.GetInstance().SoftwareConfig.SoftwareSetting.WizardCompletionKey = true;
            ConfigHandler.GetInstance().SaveConfig();
            //这里使用件的启动路径，启动主程序
            Process.Start(Application.ResourceAssembly.Location.Replace(".dll", ".exe"), "-r");
            Application.Current.Shutdown();
        }

        private void MysqlButton_Click(object sender, RoutedEventArgs e)
        {
            new MySqlConnect() { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }

        private void MQTTButton_Click(object sender, RoutedEventArgs e)
        {
            new MQTTConnect() { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }

        private void RCButton_Click(object sender, RoutedEventArgs e)
        {
            new RCServiceConnect() { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }


    }
}
