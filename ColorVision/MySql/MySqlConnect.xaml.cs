using ColorVision.MVVM;
using ColorVision.SettingUp;
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

namespace ColorVision.MySql
{
    /// <summary>
    /// MySqlConnect.xaml 的交互逻辑
    /// </summary>
    public partial class MySqlConnect : Window
    {
        public MySqlConnect()
        {
            InitializeComponent();
        }

        public void NumberValidationTextBox(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Back || e.Key == Key.Left || e.Key == Key.Right)
            {
                e.Handled = false;
                return;
            }
            if ((e.Key >= Key.D0 && e.Key <= Key.D9) || (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(MySqlConfig.Name))
            {
                MySqlConfig.Name = MySqlConfig.Host +"_" +MySqlConfig.Port;
            }
            MySqlConfig.UserPwd = PasswordBox1.Password;

            GlobalSetting.GetInstance().SaveSoftwareConfig();
            MySqlControl.GetInstance().Open();
            this.Close();
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            MySqlConfigBackUp.CopyTo(MySqlConfig);
            this.Close();
        }


        public MySqlConfig MySqlConfig { get;set;}

        private MySqlConfig MySqlConfigBackUp { get; set; }


        private void Window_Initialized(object sender, EventArgs e)
        {
            MySqlConfig= GlobalSetting.GetInstance().SoftwareConfig.MySqlConfig;
            GridMQTT.DataContext = MySqlConfig;
            MySqlConfigBackUp = new MySqlConfig();
            MySqlConfig.CopyTo(MySqlConfigBackUp);
            PasswordBox1.Password = MySqlConfig.UserPwd;
        }

        private void Button_Click_Test(object sender, RoutedEventArgs e)
        {
            MySqlConfig.UserPwd = PasswordBox1.Password;
            bool IsConnect = MySqlControl.TestConnect(MySqlConfig);
            MessageBox.Show($"连接{(IsConnect ? "成功" : "失败")}");

        }
    }
}
