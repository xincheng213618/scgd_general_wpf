using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.Rbac;
using ColorVision.Themes;
using ColorVision.UI.Menus;
using System;
using System.Windows;

namespace ColorVision.Engine.Rbac
{
    public class ExportLogin : IMenuItem
    {
        public string? OwnerGuid => "Help";

        public string? GuidId => "Login";

        public int Order => 3;

        public string? Header => Engine.Properties.Resources.MenuLogin;

        public string? InputGestureText => null;

        public object? Icon => null;
        public Visibility Visibility => Visibility.Visible;

        public RelayCommand Command => new(A =>
        {
            if (UserConfig.Instance.UserName != null)
            {
                var user = UserConfig.Instance;
                MessageBox.Show(user.PermissionMode.ToString() + ":" + user.UserName + " 已经登录", "ColorVision");

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
            this.ApplyCaption();
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
