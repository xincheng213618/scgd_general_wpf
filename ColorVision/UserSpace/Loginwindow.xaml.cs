using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Properties;
using ColorVision.Settings;
using ColorVision.UI;
using ColorVision.UserSpace.Dao;
using System;
using System.Windows;

namespace ColorVision.UserSpace
{
    public class ExportLogin : IMenuItem
    {
        public string? OwnerGuid => "Tool";

        public string? GuidId => "Login";

        public int Order => 3;

        public string? Header =>ColorVision.Properties.Resource.MenuLogin;

        public string? InputGestureText => null;

        public object? Icon => null;
        public RelayCommand Command => new RelayCommand(A =>
        {
            if (UserManager.Current.UserConfig != null)
            {
                var user = UserManager.Current.UserConfig;
                MessageBox.Show(user.PerMissionMode.ToString() + ":" + user.UserName + " 已经登录", "ColorVision");

            }
            else
            {
                new LoginWindow() { Owner =Application.Current.GetActiveWindow() , WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
            }
        });
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
            UserDao userDao = new UserDao();
            if (userDao.Checklogin(Account1.Text, PasswordBox1.Password))
            {
                Close();
            }
            else
            {
                PasswordBox1.Password = "";
                MessageBox.Show(Application.Current.MainWindow,"用户名或者密码不正确", "ColorVision");
            }
        }
    }
}
