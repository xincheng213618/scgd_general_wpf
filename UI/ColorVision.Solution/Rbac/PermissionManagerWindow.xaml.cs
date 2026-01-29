using ColorVision.Rbac.Entity;
using ColorVision.Rbac.Exceptions;
using ColorVision.Themes;
using ColorVision.UI.Authorizations;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Rbac
{
    /// <summary>
    /// 权限管理窗口 - 用于管理角色的权限分配
    /// </summary>
    public partial class PermissionManagerWindow : Window
    {
        private readonly RbacManager _rbacManager;
        private List<PermissionGroup> _permissionGroups = new();
        private RoleEntity? _selectedRole;

        public PermissionManagerWindow()
        {
            _rbacManager = RbacManager.GetInstance();
            InitializeComponent();
            this.ApplyCaption();
        }

        private async void Window_Initialized(object sender, EventArgs e)
        {
            // 权限检查
            if (Authorization.Instance.PermissionMode > PermissionMode.Administrator)
            {
                MessageBox.Show("只有管理员才能访问权限管理功能。", "权限不足", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                Close();
                return;
            }

            await LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            try
            {
                StatusText.Text = "正在加载数据...";
                
                // 加载角色列表
                var roles = await _rbacManager.RoleService.GetAllRolesAsync();
                RolesListBox.ItemsSource = roles;
                
                // 加载所有权限并按组分类
                var allPermissions = await _rbacManager.PermissionService.GetAllAsync();
                _permissionGroups = GroupPermissions(allPermissions);
                
                StatusText.Text = $"就绪 - 共{roles.Count}个角色，{allPermissions.Count}个权限";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载数据失败: {ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "加载失败";
            }
        }

        private List<PermissionGroup> GroupPermissions(List<PermissionEntity> permissions)
        {
            var groups = permissions
                .GroupBy(p => p.Group ?? "其他")
                .Select(g => new PermissionGroup
                {
                    GroupName = g.Key,
                    Permissions = new ObservableCollection<PermissionItem>(
                        g.Select(p => new PermissionItem
                        {
                            Id = p.Id,
                            Name = p.Name,
                            Code = p.Code,
                            Remark = p.Remark,
                            IsSelected = false
                        }).ToList()
                    )
                })
                .ToList();

            return groups;
        }

        private async void RolesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RolesListBox.SelectedItem is not RoleEntity role)
            {
                _selectedRole = null;
                SaveButton.IsEnabled = false;
                CurrentRoleText.Text = "未选择";
                return;
            }

            _selectedRole = role;
            CurrentRoleText.Text = role.Name;
            SaveButton.IsEnabled = true;

            try
            {
                StatusText.Text = $"正在加载角色 [{role.Name}] 的权限...";
                
                // 获取该角色已有的权限
                var rolePermissions = await _rbacManager.RoleService.GetRolePermissionsAsync(role.Id);
                var rolePermissionIds = new HashSet<int>(rolePermissions.Select(p => p.Id));

                // 重新生成权限组（清除之前的选择状态）
                var allPermissions = await _rbacManager.PermissionService.GetAllAsync();
                _permissionGroups = GroupPermissions(allPermissions);

                // 设置已分配权限的选中状态
                foreach (var group in _permissionGroups)
                {
                    foreach (var perm in group.Permissions)
                    {
                        perm.IsSelected = rolePermissionIds.Contains(perm.Id);
                    }
                    group.UpdateGroupCheckState();
                }

                // 绑定到TreeView
                PermissionsTreeView.ItemsSource = _permissionGroups;

                StatusText.Text = $"已加载角色 [{role.Name}] 的权限配置（已分配 {rolePermissionIds.Count} 个权限）";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载角色权限失败: {ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "加载失败";
            }
        }

        private async void SavePermissions_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedRole == null)
                return;

            try
            {
                SaveButton.IsEnabled = false;
                StatusText.Text = "正在保存权限分配...";

                // 收集所有选中的权限ID
                var selectedPermissionIds = _permissionGroups
                    .SelectMany(g => g.Permissions)
                    .Where(p => p.IsSelected)
                    .Select(p => p.Id)
                    .ToList();

                // 调用服务保存
                var success = await _rbacManager.RoleService.AssignPermissionsToRoleAsync(
                    _selectedRole.Id, 
                    selectedPermissionIds);

                if (success)
                {
                    // 清除该角色相关用户的权限缓存
                    _rbacManager.PermissionChecker.InvalidateAllCache();
                    
                    MessageBox.Show($"成功为角色 [{_selectedRole.Name}] 分配了 {selectedPermissionIds.Count} 个权限。", 
                        "保存成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    
                    StatusText.Text = $"权限保存成功 - 已分配 {selectedPermissionIds.Count} 个权限";
                }
                else
                {
                    MessageBox.Show("保存权限失败，请重试。", "错误", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    StatusText.Text = "保存失败";
                }
            }
            catch (PermissionDeniedException ex)
            {
                MessageBox.Show(ex.Message, "权限不足", MessageBoxButton.OK, MessageBoxImage.Warning);
                StatusText.Text = "权限不足";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存失败: {ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "保存失败";
            }
            finally
            {
                SaveButton.IsEnabled = true;
            }
        }

        private void RefreshRoles_Click(object sender, RoutedEventArgs e)
        {
            _ = LoadDataAsync();
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var group in _permissionGroups)
            {
                foreach (var perm in group.Permissions)
                {
                    perm.IsSelected = true;
                }
                group.UpdateGroupCheckState();
            }
        }

        private void DeselectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var group in _permissionGroups)
            {
                foreach (var perm in group.Permissions)
                {
                    perm.IsSelected = false;
                }
                group.UpdateGroupCheckState();
            }
        }

        private void GroupCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox && checkBox.DataContext is PermissionGroup group)
            {
                foreach (var perm in group.Permissions)
                {
                    perm.IsSelected = true;
                }
            }
        }

        private void GroupCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox && checkBox.DataContext is PermissionGroup group)
            {
                foreach (var perm in group.Permissions)
                {
                    perm.IsSelected = false;
                }
            }
        }
    }

    #region View Models

    /// <summary>
    /// 权限组视图模型
    /// </summary>
    public class PermissionGroup : INotifyPropertyChanged
    {
        public string GroupName { get; set; } = string.Empty;
        public ObservableCollection<PermissionItem> Permissions { get; set; } = new();

        private bool? _isAllSelected;
        public bool? IsAllSelected
        {
            get => _isAllSelected;
            set
            {
                _isAllSelected = value;
                OnPropertyChanged();
            }
        }

        public void UpdateGroupCheckState()
        {
            if (Permissions.All(p => p.IsSelected))
                IsAllSelected = true;
            else if (Permissions.All(p => !p.IsSelected))
                IsAllSelected = false;
            else
                IsAllSelected = null; // 部分选中状态
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// 权限项视图模型
    /// </summary>
    public class PermissionItem : INotifyPropertyChanged
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string? Remark { get; set; }

        public bool HasRemark => !string.IsNullOrWhiteSpace(Remark);
        
        public Visibility HasRemarkVisibility => HasRemark ? Visibility.Visible : Visibility.Collapsed;

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    #endregion
}
