using ColorVision.Rbac.Dtos;
using ColorVision.Themes;
using ColorVision.UI.Authorizations;
using System;
using System.Windows;

namespace ColorVision.Rbac
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

        private async void Button_Click(object sender, RoutedEventArgs e)
        {

            string username = Account1.Text.Trim();
            string password = PasswordBox1.Password.Trim();
            LoginResultDto userLoginResult = await RbacManager.GetInstance().AuthService.LoginAndGetDetailAsync(username, password);
            if (userLoginResult == null)
            {
                MessageBox.Show(Application.Current.MainWindow, "用户名或者密码不正确", "ColorVision");
                return;
            }
            RbacManagerConfig.Instance.LoginResult = userLoginResult;
            Authorization.Instance.PermissionMode = userLoginResult.UserDetail.PermissionMode;
            this.Close();
        }
    }
}
