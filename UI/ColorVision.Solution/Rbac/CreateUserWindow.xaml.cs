using ColorVision.UI.Authorizations;
using System.Windows;

namespace ColorVision.Rbac
{
    /// <summary>
    /// CreateUserWindow.xaml 的交互逻辑
    /// </summary>
    public partial class CreateUserWindow : Window
    {
        private RbacManager _rbacManager;

        public CreateUserWindow()
        {
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            _rbacManager = RbacManager.GetInstance();
            LoadRoles();
            TxtUsername.Focus();
        }

        private void LoadRoles()
        {
            var roles = _rbacManager.GetRoles();
            CmbRole.ItemsSource = roles;
            CmbRole.DisplayMemberPath = "Name";
            CmbRole.SelectedValuePath = "Id";
        }

        private async void BtnCreate_Click(object sender, RoutedEventArgs e)
        {
            // Permission check
            if (Authorization.Instance.PermissionMode > PermissionMode.Administrator)
            {
                ShowStatus("权限不足：只有管理员可以创建用户", true);
                return;
            }

            // Validation
            string username = TxtUsername.Text.Trim();
            string password = TxtPassword.Password.Trim();
            string remark = TxtRemark.Text.Trim();
            int? roleId = CmbRole.SelectedValue as int?;

            if (string.IsNullOrWhiteSpace(username))
            {
                ShowStatus("用户名不能为空", true);
                TxtUsername.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                ShowStatus("密码不能为空", true);
                TxtPassword.Focus();
                return;
            }

            if (password.Length < 6)
            {
                ShowStatus("密码至少需要6个字符", true);
                TxtPassword.Focus();
                return;
            }

            // Disable button and show loading
            BtnCreate.IsEnabled = false;
            BtnCreate.Content = "创建中...";

            try
            {
                var roleIds = roleId.HasValue ? new System.Collections.Generic.List<int> { roleId.Value } : null;
                bool result = await _rbacManager.UserService.CreateUserAsync(username, password, remark, roleIds);

                if (result)
                {
                    // Audit log
                    try
                    {
                        if (_rbacManager.Config.LoginResult?.UserDetail?.UserId != null &&
                            _rbacManager.Config.LoginResult?.User?.Username != null)
                        {
                            await _rbacManager.AuditLogService.AddAsync(
                                _rbacManager.Config.LoginResult.UserDetail.UserId,
                                _rbacManager.Config.LoginResult.User.Username,
                                "user.create",
                                $"创建用户:{username}");
                        }
                    }
                    catch { }

                    MessageBox.Show($"用户 '{username}' 创建成功！", "成功", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    DialogResult = true;
                }
                else
                {
                    ShowStatus("用户名已存在或创建失败", true);
                    BtnCreate.IsEnabled = true;
                    BtnCreate.Content = "创建用户";
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"创建失败: {ex.Message}", true);
                BtnCreate.IsEnabled = true;
                BtnCreate.Content = "创建用户";
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void ShowStatus(string message, bool isError = false)
        {
            TxtStatus.Text = message;
            BdrStatus.Background = isError ?
                new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 244, 229)) :
                new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(232, 245, 233));
            BdrStatus.BorderBrush = isError ?
                new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 167, 38)) :
                new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(76, 175, 80));
            TxtStatus.Foreground = isError ?
                new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 124, 0)) :
                new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(46, 125, 50));
            BdrStatus.Visibility = Visibility.Visible;
        }
    }
}
