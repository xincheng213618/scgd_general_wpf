using ColorVision.Rbac.Dtos;
using ColorVision.Themes;
using ColorVision.UI.Authorizations;
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
            // 自动聚焦到用户名输入框
            Account1.Focus();
            
            // 设置版本信息
            TxtVersion.Text = $"ColorVision RBAC v2.0 - {DateTime.Now.Year}";
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            string username = Account1.Text.Trim();
            string password = PasswordBox1.Password.Trim();

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("请输入用户名和密码", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 显示加载状态
            BtnLogin.IsEnabled = false;
            BtnLogin.Content = "登录中...";

            try
            {
                var rbacManager = RbacManager.GetInstance();
                LoginResultDto userLoginResult = await rbacManager.AuthService.LoginAndGetDetailAsync(username, password);
                
                if (userLoginResult == null)
                {
                    MessageBox.Show(Application.Current.MainWindow, "用户名或者密码不正确", "登录失败", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 创建会话
                string sessionToken = await rbacManager.SessionService.CreateSessionAsync(
                    userLoginResult.User.Id,
                    deviceInfo: $"{Environment.MachineName} - {Environment.OSVersion}",
                    ipAddress: "127.0.0.1" // 简化实现，实际应获取真实IP
                );

                // 保存登录结果和会话Token
                RbacManagerConfig.Instance.LoginResult = userLoginResult;
                RbacManagerConfig.Instance.SessionToken = sessionToken;
                Authorization.Instance.PermissionMode = userLoginResult.UserDetail.PermissionMode;

                // 安全地记录审计日志
                try
                {
                    await rbacManager.AuditLogService.AddAsync(
                        userLoginResult.User.Id,
                        userLoginResult.User.Username,
                        "user.login",
                        $"用户登录成功，会话ID: {sessionToken.Substring(0, 8)}..., 设备: {Environment.MachineName}"
                    );
                }
                catch { }

                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"登录失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // 恢复按钮状态
                BtnLogin.IsEnabled = true;
                BtnLogin.Content = "登  录";
            }
        }

        // 打开注册窗口
        private void RegisterHyperlink_Click(object sender, RoutedEventArgs e)
        {
            var win = new RegisterWindow
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            bool? result = win.ShowDialog();
            if (result == true)
            {
                // 可在此自动填充刚注册的用户名
                // Account1.Text = win.RegisteredUsername; // 若将来需要，可在 RegisterWindow 暴露属性
            }
        }
    }
}
