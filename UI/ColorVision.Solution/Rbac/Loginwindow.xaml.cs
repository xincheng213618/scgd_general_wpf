using ColorVision.Rbac.Dtos;
using ColorVision.Themes;
using ColorVision.UI.Authorizations;
using System;
using System.Windows;
using System.Windows.Controls;

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

        // 注册弹窗（动态创建，不依赖额外 XAML 文件）
        private void RegisterHyperlink_Click(object sender, RoutedEventArgs e)
        {
            var win = new Window
            {
                Title = "注册新用户",
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Width = 380,
                Height = 260,
                ResizeMode = ResizeMode.NoResize
            };

            var grid = new Grid { Margin = new Thickness(20) };
            for (int i = 0; i < 5; i++) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // Username
            var spUser = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 5) };
            spUser.Children.Add(new Label { Content = "用户名:", Width = 80, VerticalAlignment = VerticalAlignment.Center });
            var txtUser = new TextBox { Width = 200, Name = "RegUsername" };
            spUser.Children.Add(txtUser);
            Grid.SetRow(spUser, 0);
            grid.Children.Add(spUser);

            // Password
            var spPwd = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 5) };
            spPwd.Children.Add(new Label { Content = "密码:", Width = 80, VerticalAlignment = VerticalAlignment.Center });
            var pwd1 = new PasswordBox { Width = 200, Name = "RegPassword" };
            spPwd.Children.Add(pwd1);
            Grid.SetRow(spPwd, 1);
            grid.Children.Add(spPwd);

            // Confirm
            var spPwd2 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 5) };
            spPwd2.Children.Add(new Label { Content = "确认密码:", Width = 80, VerticalAlignment = VerticalAlignment.Center });
            var pwd2 = new PasswordBox { Width = 200, Name = "RegPassword2" };
            spPwd2.Children.Add(pwd2);
            Grid.SetRow(spPwd2, 2);
            grid.Children.Add(spPwd2);

            // Buttons
            var spBtn = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 15, 0, 0) };
            var btnOk = new Button { Content = "注册", Width = 100, Margin = new Thickness(10, 0, 10, 0) };
            var btnCancel = new Button { Content = "取消", Width = 100, Margin = new Thickness(10, 0, 10, 0) };
            spBtn.Children.Add(btnOk);
            spBtn.Children.Add(btnCancel);
            Grid.SetRow(spBtn, 3);
            grid.Children.Add(spBtn);

            win.Content = grid;

            btnOk.Click += async (s, args) =>
            {
                string u = txtUser.Text.Trim();
                string p1 = pwd1.Password.Trim();
                string p2 = pwd2.Password.Trim();
                if (string.IsNullOrWhiteSpace(u) || string.IsNullOrWhiteSpace(p1))
                {
                    MessageBox.Show(win, "用户名和密码不能为空", "注册", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (p1 != p2)
                {
                    MessageBox.Show(win, "两次输入的密码不一致", "注册", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                bool created = await RbacManager.GetInstance().UserService.CreateUserAsync(u, p1);
                if (!created)
                {
                    MessageBox.Show(win, "用户名已存在或创建失败", "注册", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                MessageBox.Show(win, "注册成功，请使用新账户登录", "注册", MessageBoxButton.OK, MessageBoxImage.Information);
                win.DialogResult = true;
            };
            btnCancel.Click += (s, args) => win.DialogResult = false;
            win.ShowDialog();
        }
    }
}
