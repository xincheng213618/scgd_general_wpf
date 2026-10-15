using ColorVision.Themes;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Database
{
    /// <summary>
    /// 编辑桌面端当前的 MySQL 连接，确认前不修改共享配置。
    /// </summary>
    public partial class MySqlConnect : Window
    {
        public MySqlConfig MySqlConfig { get; }

        public MySqlConnect()
        {
            MySqlConfig current = MySqlSetting.Instance.MySqlConfig;
            MySqlConfig = MySqlDatabaseMaintenanceService.CloneConfig(current, current.Database);
            InitializeComponent();
            this.ApplyCaption();
            DataContext = MySqlConfig;
            PasswordBox1.Password = MySqlConfig.UserPwd;
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            if (Validation.GetHasError(PortTextBox)) return;
            MySqlConfig current = MySqlSetting.Instance.MySqlConfig;
            current.Host = MySqlConfig.Host;
            current.Port = MySqlConfig.Port;
            current.UserName = MySqlConfig.UserName;
            current.UserPwd = PasswordBox1.Password;
            current.Database = MySqlConfig.Database;
            _ = Task.Run(() => MySqlControl.GetInstance().Connect());
            Close();
        }

        private void Test_Click(object sender, RoutedEventArgs e)
        {
            if (Validation.GetHasError(PortTextBox)) return;
            MySqlConfig testConfig = MySqlDatabaseMaintenanceService.CloneConfig(MySqlConfig, MySqlConfig.Database);
            testConfig.UserPwd = PasswordBox1.Password;
            _ = Task.Run(() => MySqlControl.TestConnect(testConfig));
        }
    }
}
