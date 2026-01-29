using ColorVision.Rbac.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Rbac
{
    /// <summary>
    /// EditUserRolesWindow.xaml 的交互逻辑
    /// </summary>
    public partial class EditUserRolesWindow : Window
    {
        private RbacManager _rbacManager;
        private UserViewModel _user;
        private List<CheckBox> _roleCheckBoxes = new List<CheckBox>();

        public EditUserRolesWindow(UserViewModel user)
        {
            _user = user;
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            _rbacManager = RbacManager.GetInstance();
            TxtTitle.Text = $"编辑用户角色 - {_user.Username}";
            TxtSubtitle.Text = $"用户ID: {_user.Id} | 当前角色: {_user.RolesDisplay}";
            LoadRoles();
        }

        private void LoadRoles()
        {
            var allRoles = _rbacManager.GetRoles();
            var currentRoleIds = _user.Roles.Select(r => r.Id).ToHashSet();

            PnlRoles.Children.Clear();
            _roleCheckBoxes.Clear();

            foreach (var role in allRoles)
            {
                // Create a card for each role
                var border = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(248, 248, 248)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(12, 10,12,10),
                    Margin = new Thickness(0, 0, 0, 10)
                };

                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var stackPanel = new StackPanel();

                var cb = new CheckBox
                {
                    Content = role.Name,
                    Tag = role.Id,
                    IsChecked = currentRoleIds.Contains(role.Id),
                    FontSize = 14,
                    FontWeight = FontWeights.SemiBold
                };
                cb.Checked += RoleCheckBox_Changed;
                cb.Unchecked += RoleCheckBox_Changed;

                stackPanel.Children.Add(cb);

                if (!string.IsNullOrWhiteSpace(role.Remark))
                {
                    var remarkText = new TextBlock
                    {
                        Text = role.Remark,
                        FontSize = 11,
                        Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)),
                        Margin = new Thickness(20, 2, 0, 0),
                        TextWrapping = TextWrapping.Wrap
                    };
                    stackPanel.Children.Add(remarkText);
                }

                grid.Children.Add(stackPanel);

                // Status badge
                if (role.IsEnable)
                {
                    var badge = new Border
                    {
                        Background = new SolidColorBrush(Color.FromRgb(76, 175, 80)),
                        CornerRadius = new CornerRadius(3),
                        Padding = new Thickness(6, 2,6,2),
                        VerticalAlignment = VerticalAlignment.Top
                    };
                    var badgeText = new TextBlock
                    {
                        Text = "启用",
                        Foreground = Brushes.White,
                        FontSize = 10,
                        FontWeight = FontWeights.SemiBold
                    };
                    badge.Child = badgeText;
                    Grid.SetColumn(badge, 1);
                    grid.Children.Add(badge);
                }

                border.Child = grid;
                PnlRoles.Children.Add(border);
                _roleCheckBoxes.Add(cb);
            }

            UpdateSelectedCount();
        }

        private void RoleCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            UpdateSelectedCount();
        }

        private void UpdateSelectedCount()
        {
            int count = _roleCheckBoxes.Count(cb => cb.IsChecked == true);
            TxtSelectedCount.Text = $"已选择 {count} 个角色";
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            string searchText = TxtSearch.Text.ToLower();
            
            for (int i = 0; i < PnlRoles.Children.Count; i++)
            {
                if (PnlRoles.Children[i] is Border border)
                {
                    var cb = _roleCheckBoxes[i];
                    string roleName = cb.Content.ToString().ToLower();
                    border.Visibility = string.IsNullOrWhiteSpace(searchText) || roleName.Contains(searchText) 
                        ? Visibility.Visible 
                        : Visibility.Collapsed;
                }
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            BtnSave.IsEnabled = false;
            BtnSave.Content = "保存中...";

            try
            {
                var selectedIds = _roleCheckBoxes
                    .Where(c => c.IsChecked == true)
                    .Select(c => (int)c.Tag)
                    .ToList();

                if (_rbacManager.UpdateUserRoles(_user.Id, selectedIds))
                {
                    MessageBox.Show("角色更新成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    DialogResult = true;
                }
                else
                {
                    MessageBox.Show("角色更新失败！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    BtnSave.IsEnabled = true;
                    BtnSave.Content = "保存";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                BtnSave.IsEnabled = true;
                BtnSave.Content = "保存";
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
