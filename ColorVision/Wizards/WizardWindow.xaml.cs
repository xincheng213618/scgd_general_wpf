using ColorVision.MQTT;
using ColorVision.MySql;
using ColorVision.RC;
using ColorVision.Settings;
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

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            StartWindow StartWindow = new StartWindow();
            StartWindow.Show();
            ConfigHandler.GetInstance().SoftwareConfig.SoftwareSetting.WizardCompletionKey = true ;
            this.Close();
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
