using ColorVision.Services;
using ColorVision.User;
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
        private string daccount = "admin";

        private string dpassword = "123456";

        public bool Login(string account,string password)
        {
            if (account == daccount && password == dpassword)
            {
                UserManager.Current.UserConfig = new UserConfig()
                {
                    Account = account,
                    UserPwd = password,
                    UserName =account,
                    PerMissionMode = PerMissionMode.Administrator,
                };
                new WindowService() { Owner = Application.Current.MainWindow, WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show(); ;
                return true;
            }
            else if (account != "admin")
            {
                UserManager.Current.UserConfig = new UserConfig()
                {
                    Account = account,
                    UserPwd = password,
                    UserName = account,
                    PerMissionMode = PerMissionMode.User,
                };

                new WindowDevices() { Owner = Application.Current.MainWindow, WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show(); ;
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
            Administrator administrator = new Administrator();
            if (administrator.CheckDatabase(Account1.Text, PasswordBox1.Password))
            {
                this.Close();
            }
            else
            {
                PasswordBox1.Password = "";
                MessageBox.Show("用户名或者密码不正确", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
            }
        }
    }
}
