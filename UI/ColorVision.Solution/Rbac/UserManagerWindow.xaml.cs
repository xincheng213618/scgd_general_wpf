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
            UsersListView.ItemsSource = RbacManager.GetUsers();
        }

        private void LoadRoles()
        {
            var roles = RbacManager.GetRoles();
            RolesComboBox.ItemsSource = roles;
            RolesComboBox.DisplayMemberPath = "Name";
            RolesComboBox.SelectedValuePath = "Id";
        }

        private async void BtnCreateUser_Click(object sender, RoutedEventArgs e)
        {
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

    }
}
