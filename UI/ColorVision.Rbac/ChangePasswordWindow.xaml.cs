using System.Windows;
using System.Windows.Input;

namespace ColorVision.Rbac
{
    /// <summary>
    /// ChangePasswordWindow.xaml 的交互逻辑
    /// </summary>
    public partial class ChangePasswordWindow : Window
    {
        private readonly RbacManager _rbacManager;
        private readonly int _userId;

        /// <summary>
        /// 是否为强制修改模式（首次登录默认密码）
        /// </summary>
        public bool IsForceChange { get; set; }

        public ChangePasswordWindow(int userId, bool isForceChange = false)
        {
            InitializeComponent();
            _rbacManager = RbacManager.GetInstance();
            _userId = userId;
            IsForceChange = isForceChange;
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            if (IsForceChange)
            {
                TxtTitle.Text = "首次登录 - 请修改默认密码";
                BdrHint.Visibility = Visibility.Visible;
                TxtHint.Text = "您正在使用默认密码登录，为了账户安全，请立即修改密码。";
                BtnCancel.Visibility = Visibility.Collapsed;
            }

            TxtOldPassword.Focus();
        }

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            BdrError.Visibility = Visibility.Collapsed;

            string oldPassword = TxtOldPassword.Password.Trim();
            string newPassword = TxtNewPassword.Password.Trim();
            string confirmPassword = TxtConfirmPassword.Password.Trim();

            if (string.IsNullOrWhiteSpace(oldPassword))
            {
                ShowError("请输入当前密码");
                return;
            }

            if (string.IsNullOrWhiteSpace(newPassword))
            {
                ShowError("请输入新密码");
                return;
            }

            if (newPassword.Length < 6)
            {
                ShowError("新密码至少需要6个字符");
                return;
            }

            if (newPassword != confirmPassword)
            {
                ShowError("两次输入的新密码不一致");
                return;
            }

            if (oldPassword == newPassword)
            {
                ShowError("新密码不能与当前密码相同");
                return;
            }

            BtnSave.IsEnabled = false;
            BtnSave.Content = "保存中...";

            try
            {
                bool result = await _rbacManager.UserService.ChangePasswordAsync(_userId, oldPassword, newPassword);
                if (result)
                {
                    // 记录审计日志
                    try
                    {
                        if (_rbacManager.Config.LoginResult?.User != null)
                        {
                            await _rbacManager.AuditLogService.AddAsync(
                                _rbacManager.Config.LoginResult.User.Id,
                                _rbacManager.Config.LoginResult.User.Username,
                                "user.change_password",
                                "用户修改了密码");
                        }
                    }
                    catch { }

                    MessageBox.Show("密码修改成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    DialogResult = true;
                }
                else
                {
                    ShowError("当前密码不正确");
                }
            }
            catch (Exception ex)
            {
                ShowError($"修改密码失败: {ex.Message}");
            }
            finally
            {
                BtnSave.IsEnabled = true;
                BtnSave.Content = "保存";
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void ShowError(string message)
        {
            TxtError.Text = message;
            BdrError.Visibility = Visibility.Visible;
        }
    }
}
