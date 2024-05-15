using ColorVision.Common.MVVM;
using ColorVision.MQTT;
using ColorVision.MySql;
using ColorVision.Services.RC;
using ColorVision.Settings;
using ColorVision.UI;
using System.Diagnostics;
using System.Windows;

namespace ColorVision.Wizards
{
    public class WizardConfig : ViewModelBase ,IConfig
    {
        public static WizardConfig Instance =>ConfigHandler1.GetInstance().GetRequiredService<WizardConfig>();
        public bool WizardCompletionKey { get => _WizardCompletionKey; set { _WizardCompletionKey = value; NotifyPropertyChanged(); } }
        private bool _WizardCompletionKey;
    }

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
            DataContext = ConfigHandler.GetInstance().SoftwareConfig;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            WizardConfig.Instance.WizardCompletionKey = true;
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
