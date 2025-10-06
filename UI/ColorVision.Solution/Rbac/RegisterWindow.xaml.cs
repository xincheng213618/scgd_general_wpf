using System.Windows;

namespace ColorVision.Rbac
{
    /// <summary>
    /// RegisterWindow.xaml 的交互逻辑
    /// </summary>
    public partial class RegisterWindow : Window
    {
        private bool _isSubmitting;
        public RegisterWindow()
        {
            InitializeComponent();
        }

        private async void BtnRegister_Click(object sender, RoutedEventArgs e)
        {
            if (_isSubmitting) return;
            string username = TxtUsername.Text.Trim();
            string pwd1 = Pwd1.Password.Trim();
            string pwd2 = Pwd2.Password.Trim();

            TxtStatus.Text = string.Empty;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(pwd1))
            {
                TxtStatus.Text = "用户名和密码不能为空";
                return;
            }
            if (pwd1.Length < 3)
            {
                TxtStatus.Text = "密码长度至少 3 位";
                return;
            }
            if (pwd1 != pwd2)
            {
                TxtStatus.Text = "两次输入的密码不一致";
                return;
            }

            try
            {
                _isSubmitting = true;
                BtnRegister.IsEnabled = false;
                TxtStatus.Text = "正在创建...";
                bool created = await RbacManager.GetInstance().UserService.CreateUserAsync(username, pwd1);
                if (!created)
                {
                    TxtStatus.Text = "用户名已存在或创建失败";
                    return;
                }
                MessageBox.Show(this, "注册成功，请使用新账户登录", "注册", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"发生错误: {ex.Message}";
            }
            finally
            {
                _isSubmitting = false;
                BtnRegister.IsEnabled = true;
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
