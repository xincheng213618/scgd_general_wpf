using ColorVision.Themes;
using System;
using System.Windows;

namespace ColorVision.Engine.Rbac
{

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
            MessageBox.Show(Application.Current.MainWindow, "用户名或者密码不正确", "ColorVision");
        }
    }
}
