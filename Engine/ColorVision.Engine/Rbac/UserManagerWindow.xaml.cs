using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ColorVision.Engine.Rbac
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
            var users = RbacManager.GetUsers();
            UsersListView.ItemsSource = users;
        }



        private void BtnCreateUser_Click(object sender, RoutedEventArgs e)
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

            bool result = RbacManager.CreateUser(username, password, remark, roleIds);
            if (result)
            {
                MessageBox.Show("用户创建成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("用户名已存在！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

    }
}
