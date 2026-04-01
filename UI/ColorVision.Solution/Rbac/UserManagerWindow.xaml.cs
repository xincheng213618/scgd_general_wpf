using ColorVision.Rbac.ViewModels;
using ColorVision.UI.Authorizations;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Rbac
{
    /// <summary>
    /// UserManagerWindow.xaml 的交互逻辑
    /// </summary>
    public partial class UserManagerWindow : Window
    {
        RbacManager RbacManager { get; set; }
        private List<UserViewModel> _allUsers = new();
        
        public UserManagerWindow()
        {
            InitializeComponent();
            
        }

        private async void Window_Initialized(object sender, EventArgs e)
        {
            RbacManager = RbacManager.GetInstance();
            await LoadUsersAsync();
            LoadRoles();
            UpdateStatusInfo();
            UpdateSessionInfo();
            UpdateCacheStats();
        }

        private void UpdateStatusInfo()
        {
            var mode = Authorization.Instance.PermissionMode;
            TxtStatusInfo.Text = $"当前权限: {GetPermissionModeName(mode)}";
        }

        private void UpdateSessionInfo()
        {
            if (RbacManager.Config.LoginResult?.User != null)
            {
                var user = RbacManager.Config.LoginResult.User;
                TxtSessionInfo.Text = $"登录用户: {user.Username} | 会话: {(string.IsNullOrEmpty(RbacManager.Config.SessionToken) ? "无" : "有效")}";
            }
        }

        private async void UpdateCacheStats()
        {
            try
            {
                var stats = RbacManager.GetPermissionCacheStatistics();
                TxtCacheStats.Text = $"权限缓存统计: {stats}";
            }
            catch
            {
                TxtCacheStats.Text = "权限缓存统计: 不可用";
            }
        }

        private string GetPermissionModeName(PermissionMode mode)
        {
            return mode switch
            {
                PermissionMode.Administrator => "系统管理员",
                PermissionMode.PowerUser => "高级用户",
                PermissionMode.User => "普通用户",
                PermissionMode.Guest => "访客",
                _ => "未知"
            };
        }

        private async Task LoadUsersAsync()
        {
            var users = await Task.Run(() => RbacManager.GetUsers());
            _allUsers = new List<UserViewModel>();
            
            foreach (var user in users)
            {
                var userRoles = await Task.Run(() => RbacManager.GetUserRoles(user.Id));
                _allUsers.Add(UserViewModel.FromEntity(user, userRoles));
            }
            
            ApplyFilter();
            TxtTotalCount.Text = _allUsers.Count.ToString();
        }

        private void ApplyFilter()
        {
            var searchText = TxtSearch.Text.Trim();
            
            if (string.IsNullOrEmpty(searchText))
            {
                UsersListView.ItemsSource = _allUsers;
            }
            else
            {
                var filtered = _allUsers.Where(u =>
                    u.Username.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                    (u.Remark?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (u.RolesDisplay?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false)
                ).ToList();
                
                UsersListView.ItemsSource = filtered;
            }
        }

        private void LoadRoles()
        {
            var roles = RbacManager.GetRoles();
            RolesListView.ItemsSource = roles;
        }

        private async void BtnCreateUser_Click(object sender, RoutedEventArgs e)
        {
            var window = new CreateUserWindow
            {
                Owner = this
            };
            if (window.ShowDialog() == true)
            {
                await LoadUsersAsync();
            }
        }

        private async void BtnCreateRole_Click(object sender, RoutedEventArgs e)
        {
            // UI 层二次校验，防止普通用户通过窗口创建角色
            if (Authorization.Instance.PermissionMode > PermissionMode.Administrator)
            {
                MessageBox.Show("权限不足：只有管理员可以创建角色", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string roleName = TxtRoleName.Text.Trim();
            string roleCode = TxtRoleCode.Text.Trim();
            string roleRemark = TxtRoleRemark.Text.Trim();

            if (string.IsNullOrWhiteSpace(roleName) || string.IsNullOrWhiteSpace(roleCode))
            {
                MessageBox.Show("角色名称和代码不能为空。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            bool result = await RbacManager.CreateRoleAsync(roleName, roleCode, roleRemark);
            if (result)
            {
                MessageBox.Show("角色创建成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                TxtRoleName.Text = string.Empty;
                TxtRoleCode.Text = string.Empty;
                TxtRoleRemark.Text = string.Empty;
                LoadRoles();
            }
            else
            {
                MessageBox.Show("角色代码已存在或创建失败！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 新增：编辑用户角色
        private async void BtnEditRoles_Click(object sender, RoutedEventArgs e)
        {
            if (Authorization.Instance.PermissionMode > PermissionMode.Administrator)
            {
                MessageBox.Show("权限不足：只有管理员可以修改角色", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (sender is FrameworkElement fe && fe.Tag is UserViewModel vm)
            {
                var window = new EditUserRolesWindow(vm)
                {
                    Owner = this
                };
                if (window.ShowDialog() == true)
                {
                    await LoadUsersAsync();
                }
            }
        }

        // 新增：删除用户
        private async void BtnDeleteUser_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is UserViewModel vm)
            {
                var result = MessageBox.Show(
                    $"确定要删除用户 '{vm.Username}' 吗？\n\n此操作将进行逻辑删除，用户数据将被保留但标记为已删除状态。",
                    "确认删除",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    if (await RbacManager.DeleteUserAsync(vm.Id, vm.Username))
                    {
                        MessageBox.Show("用户删除成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                        await LoadUsersAsync();
                    }
                }
            }
        }

        // 新增：启用/禁用用户
        private async void BtnToggleUser_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is UserViewModel vm)
            {
                bool success;
                if (vm.IsEnable)
                {
                    success = await RbacManager.DisableUserAsync(vm.Id, vm.Username);
                    if (success)
                    {
                        MessageBox.Show($"用户 '{vm.Username}' 已禁用", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                else
                {
                    success = await RbacManager.EnableUserAsync(vm.Id, vm.Username);
                    if (success)
                    {
                        MessageBox.Show($"用户 '{vm.Username}' 已启用", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }

                if (success)
                {
                    await LoadUsersAsync();
                }
            }
        }

        // 新增：重置密码
        private async void BtnResetPassword_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is UserViewModel vm)
            {
                var result = MessageBox.Show(
                    $"确定要重置用户 '{vm.Username}' 的密码吗？\n\n将生成一个新的随机密码。",
                    "确认重置",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    string? newPassword = await RbacManager.ResetUserPasswordAsync(vm.Id, vm.Username);
                    if (!string.IsNullOrEmpty(newPassword))
                    {
                        MessageBox.Show(
                            $"密码重置成功！\n\n用户: {vm.Username}\n新密码: {newPassword}\n\n请妥善保管此密码。",
                            "密码已重置",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                }
            }
        }

        private void BtnOpenPermissionManager_Click(object sender, RoutedEventArgs e)
        {
            RbacManager.OpenPermissionManager();
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter();
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            await LoadUsersAsync();
            LoadRoles();
            UpdateSessionInfo();
            UpdateCacheStats();
        }
    }
}
