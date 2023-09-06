using ColorVision.Service;
using ColorVision.SettingUp;
using System;
using System.Windows;

namespace ColorVision
{
    public class PermissionsControl
    {
        public static PermissionsControl Current { get; set; } = new PermissionsControl();

        public PermissionsControl()
        {

        }

        public bool Login(string account,string password)
        {
            if (account == "admin" && password == "123456")
            {
                MessageBox.Show("进入管理员模式");
                new WindowService() { Owner = Application.Current.MainWindow, WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show(); ;
                return true;
            }
            else
            {
                GlobalSetting.GetInstance().SoftwareConfig.UserConfig.UserName = account;
                GlobalSetting.GetInstance().SoftwareConfig.UserConfig.UserPwd = password;

                MessageBox.Show("进入用户模式");
                new WindowDevices() { Owner = Application.Current.MainWindow, WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show(); ;
                return true;
            }
            if (GlobalSetting.GetInstance().SoftwareConfig.UserConfig.Account == account && GlobalSetting.GetInstance().SoftwareConfig.UserConfig.UserPwd == password)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }


    /// <summary>
    /// LoginWindow.xaml 的交互逻辑
    /// </summary>
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {

        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (PermissionsControl.Current.Login(Account1.Text, PasswordBox1.Password))
            {

                this.Close();
            }
            else
            {
                PasswordBox1.Password = "";
                MessageBox.Show("用户名或者密码不正确","ColorVision");
            }
        }
    }
}
