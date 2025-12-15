using ColorVision.Rbac.Dtos;
using ColorVision.Themes;
using ColorVision.UI.Authorizations;
using System.Security.Cryptography;
using System.Text;
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

        private async void Window_Initialized(object sender, EventArgs e)
        {
            // 设置版本信息
            TxtVersion.Text = $"ColorVision RBAC v2.0 - {DateTime.Now.Year}";
            
            // 尝试自动登录
            var config = RbacManagerConfig.Instance;
            if (config.RememberMe && !string.IsNullOrEmpty(config.SavedUsername) && !string.IsNullOrEmpty(config.SavedPasswordHash))
            {
                // 自动填充用户名
                Account1.Text = config.SavedUsername;
                ChkRememberMe.IsChecked = true;
                
                // 尝试自动登录
                await TryAutoLogin();
            }
            else
            {
                // 自动聚焦到用户名输入框
                Account1.Focus();
            }
        }

        private async Task TryAutoLogin()
        {
            try
            {
                BtnLogin.IsEnabled = false;
                BtnLogin.Content = "自动登录中...";
                
                var config = RbacManagerConfig.Instance;
                var rbacManager = RbacManager.GetInstance();
                
                // 使用保存的凭据尝试登录
                LoginResultDto userLoginResult = await rbacManager.AuthService.LoginAndGetDetailAsync(
                    config.SavedUsername, 
                    config.SavedPasswordHash);
                
                if (userLoginResult != null)
                {
                    await CompleteLogin(userLoginResult, true);
                    this.DialogResult = true;
                    this.Close();
                }
                else
                {
                    // 自动登录失败，清除保存的凭据
                    config.RememberMe = false;
                    config.SavedPasswordHash = string.Empty;
                    ChkRememberMe.IsChecked = false;
                    Account1.Focus();
                }
            }
            catch
            {
                // 自动登录失败，继续手动登录流程
                Account1.Focus();
            }
            finally
            {
                BtnLogin.IsEnabled = true;
                BtnLogin.Content = "登  录";
            }
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

                // 如果勾选了"记住我"，保存登录凭据
                if (ChkRememberMe.IsChecked == true)
                {
                    var config = RbacManagerConfig.Instance;
                    config.RememberMe = true;
                    config.SavedUsername = username;
                    // 保存密码的Hash（简化实现，实际应使用更安全的方式）
                    config.SavedPasswordHash = ComputePasswordHash(password);
                }

                await CompleteLogin(userLoginResult, false);
                
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

        private async Task CompleteLogin(LoginResultDto userLoginResult, bool isAutoLogin)
        {
            var rbacManager = RbacManager.GetInstance();
            
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
                string loginMethod = isAutoLogin ? "自动登录" : "手动登录";
                await rbacManager.AuditLogService.AddAsync(
                    userLoginResult.User.Id,
                    userLoginResult.User.Username,
                    "user.login",
                    $"用户{loginMethod}成功，会话ID: {sessionToken.Substring(0, 8)}..., 设备: {Environment.MachineName}"
                );
            }
            catch { }
        }

        private string ComputePasswordHash(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(hashBytes);
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
