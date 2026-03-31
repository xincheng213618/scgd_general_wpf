using ColorVision.Rbac.Dtos;
using ColorVision.Themes;
using ColorVision.UI.Authorizations;
using System.Windows;
using System.Windows.Input;

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
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private async void Window_Initialized(object sender, EventArgs e)
        {
            // 设置版本信息
            TxtVersion.Text = $"ColorVision RBAC v2.0 - {DateTime.Now.Year}";
            
            // 尝试自动登录（通过 SessionToken）
            var config = RbacManagerConfig.Instance;
            if (config.RememberMe && !string.IsNullOrEmpty(config.SessionToken))
            {
                // 自动填充用户名
                if (!string.IsNullOrEmpty(config.SavedUsername))
                    Account1.Text = config.SavedUsername;
                ChkRememberMe.IsChecked = true;
                
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
                
                // 使用 SessionToken 验证并恢复登录状态
                LoginResultDto? userLoginResult = await rbacManager.AuthService.LoginBySessionTokenAsync(config.SessionToken);
                
                if (userLoginResult != null)
                {
                    // SessionToken 仍然有效，直接恢复登录状态
                    RbacManagerConfig.Instance.LoginResult = userLoginResult;
                    config.SavedUsername = userLoginResult.User.Username;
                    Authorization.Instance.PermissionMode = userLoginResult.UserDetail.PermissionMode;

                    try
                    {
                        await rbacManager.AuditLogService.AddAsync(
                            userLoginResult.User.Id,
                            userLoginResult.User.Username,
                            "user.login",
                            $"用户自动登录成功（SessionToken），设备: {Environment.MachineName}");
                    }
                    catch { }

                    this.DialogResult = true;
                    this.Close();
                }
                else
                {
                    // SessionToken 已失效，清除自动登录凭据
                    config.SessionToken = string.Empty;
                    config.RememberMe = false;
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
                LoginResultDto? userLoginResult = await rbacManager.AuthService.LoginAndGetDetailAsync(username, password);
                
                if (userLoginResult == null)
                {
                    MessageBox.Show(Application.Current.MainWindow, "用户名或者密码不正确", "登录失败", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 检测默认密码（用户名==密码，如 admin/admin），强制修改
                if (username == password)
                {
                    var changeWindow = new ChangePasswordWindow(userLoginResult.User.Id, isForceChange: true)
                    {
                        Owner = this,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner
                    };
                    if (changeWindow.ShowDialog() != true)
                    {
                        // 用户拒绝修改默认密码，不允许登录
                        MessageBox.Show("首次登录必须修改默认密码。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                await CompleteLogin(userLoginResult, ChkRememberMe.IsChecked == true);

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

        private async Task CompleteLogin(LoginResultDto userLoginResult, bool rememberMe)
        {
            var rbacManager = RbacManager.GetInstance();
            
            // 创建会话
            string sessionToken = await rbacManager.SessionService.CreateSessionAsync(
                userLoginResult.User.Id,
                deviceInfo: $"{Environment.MachineName} - {Environment.OSVersion}",
                ipAddress: "127.0.0.1"
            );

            // 保存登录结果和会话Token
            var config = RbacManagerConfig.Instance;
            config.LoginResult = userLoginResult;
            config.SessionToken = sessionToken;
            config.SavedUsername = userLoginResult.User.Username;
            Authorization.Instance.PermissionMode = userLoginResult.UserDetail.PermissionMode;

            // 记住我：只保存 SessionToken，不保存密码
            config.RememberMe = rememberMe;
            if (!rememberMe)
            {
                config.SavedUsername = string.Empty;
            }

            // 记录审计日志
            try
            {
                await rbacManager.AuditLogService.AddAsync(
                    userLoginResult.User.Id,
                    userLoginResult.User.Username,
                    "user.login",
                    $"用户手动登录成功，会话ID: {sessionToken[..Math.Min(8, sessionToken.Length)]}..., 设备: {Environment.MachineName}"
                );
            }
            catch { }
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
            }
        }
    }
}
