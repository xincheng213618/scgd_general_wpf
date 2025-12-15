using ColorVision.Rbac.ViewModels;
using ColorVision.UI.Authorizations;
using System.Windows;

namespace ColorVision.Rbac
{
    /// <summary>
    /// UserManagerWindow.xaml 的交互逻辑
    /// </summary>
    public partial class UserManagerWindow : Window
    {

        RbacManager RbacManager { get; set; }
        public UserManagerWindow()
        {
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            RbacManager = RbacManager.GetInstance();
            LoadUsers();
            LoadRoles();
        }

        private void LoadUsers()
        {
            var users = RbacManager.GetUsers();
            var userViewModels = new List<UserViewModel>();
            
            foreach (var user in users)
            {
                var userRoles = RbacManager.GetUserRoles(user.Id);
                userViewModels.Add(UserViewModel.FromEntity(user, userRoles));
            }
            
            UsersListView.ItemsSource = userViewModels;
        }

        private void LoadRoles()
        {
            var roles = RbacManager.GetRoles();
            RolesComboBox.ItemsSource = roles;
            RolesComboBox.DisplayMemberPath = "Name";
            RolesComboBox.SelectedValuePath = "Id";
            
            RolesListView.ItemsSource = roles;
        }

        private async void BtnCreateUser_Click(object sender, RoutedEventArgs e)
        {
            // UI 层二次校验，防止普通用户通过窗口创建
            if (Authorization.Instance.PermissionMode > PermissionMode.Administrator)
            {
                MessageBox.Show("权限不足：只有管理员可以创建用户", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string username = TxtUsername.Text.Trim();
            string password = TxtPassword.Password.Trim();
            string remark = TxtRemark.Text.Trim();
            int? roleId = RolesComboBox.SelectedValue as int?;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("用户名和密码不能为空。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var roleIds = roleId.HasValue ? new List<int> { roleId.Value } : null;

            bool result = await RbacManager.UserService.CreateUserAsync(username, password, remark, roleIds);
            if (result)
            {
                try { RbacManager.AuditLogService.AddAsync(RbacManager.Config.LoginResult?.UserDetail?.UserId, RbacManager.Config.LoginResult?.User?.Username, "user.create", $"UI创建用户:{username}"); } catch { }
                MessageBox.Show("用户创建成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                TxtUsername.Text = string.Empty;
                TxtPassword.Password = string.Empty;
                TxtRemark.Text = string.Empty;
                RolesComboBox.SelectedIndex = -1;
                LoadUsers();
            }
            else
            {
                MessageBox.Show("用户名已存在或创建失败！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCreateRole_Click(object sender, RoutedEventArgs e)
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

            bool result = RbacManager.CreateRole(roleName, roleCode, roleRemark);
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
        private void BtnEditRoles_Click(object sender, RoutedEventArgs e)
        {
            if (Authorization.Instance.PermissionMode > PermissionMode.Administrator)
            {
                MessageBox.Show("权限不足：只有管理员可以修改角色", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (sender is FrameworkElement fe && fe.Tag is UserViewModel vm)
            {
                var allRoles = RbacManager.GetRoles();
                var currentRoleIds = vm.Roles.Select(r => r.Id).ToHashSet();

                var win = new Window
                {
                    Title = $"编辑用户角色 - {vm.Username}",
                    Owner = this,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Width = 400,
                    Height = 500
                };
                var grid = new System.Windows.Controls.Grid();
                grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });
                var sv = new System.Windows.Controls.ScrollViewer();
                var panel = new System.Windows.Controls.StackPanel { Margin = new Thickness(10) };
                foreach (var role in allRoles)
                {
                    var cb = new System.Windows.Controls.CheckBox
                    {
                        Content = role.Name,
                        Tag = role.Id,
                        IsChecked = currentRoleIds.Contains(role.Id),
                        Margin = new Thickness(2)
                    };
                    panel.Children.Add(cb);
                }
                sv.Content = panel;
                System.Windows.Controls.Grid.SetRow(sv, 0);
                grid.Children.Add(sv);

                var btnPanel = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(10) };
                var btnOk = new System.Windows.Controls.Button { Content = "确定", Width = 70, Margin = new Thickness(5) };
                var btnCancel = new System.Windows.Controls.Button { Content = "取消", Width = 70, Margin = new Thickness(5) };
                btnPanel.Children.Add(btnOk);
                btnPanel.Children.Add(btnCancel);
                System.Windows.Controls.Grid.SetRow(btnPanel, 1);
                grid.Children.Add(btnPanel);
                win.Content = grid;

                btnOk.Click += (s, args) =>
                {
                    var selectedIds = panel.Children.OfType<System.Windows.Controls.CheckBox>()
                        .Where(c => c.IsChecked == true)
                        .Select(c => (int)c.Tag)
                        .ToList();
                    if (RbacManager.UpdateUserRoles(vm.Id, selectedIds))
                    {
                        MessageBox.Show("更新成功", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                        LoadUsers();
                        win.DialogResult = true;
                    }
                };
                btnCancel.Click += (s, args) => win.DialogResult = false;
                win.ShowDialog();
            }
        }

        private void BtnOpenPermissionManager_Click(object sender, RoutedEventArgs e)
        {
            RbacManager.OpenPermissionManager();
        }

    }
}
