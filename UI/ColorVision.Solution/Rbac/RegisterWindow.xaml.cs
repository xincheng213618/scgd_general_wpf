using ColorVision.Themes;
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
            this.ApplyCaption();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            // 自动聚焦到用户名输入框
            TxtUsername.Focus();
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
                BtnRegister.Content = "正在创建...";
                TxtStatus.Text = "正在创建新用户，请稍候...";
                bool created = await RbacManager.GetInstance().UserService.CreateUserAsync(username, pwd1);
                if (!created)
                {
                    TxtStatus.Text = "用户名已存在或创建失败";
                    return;
                }
                MessageBox.Show(this, "注册成功，请使用新账户登录", "注册成功", MessageBoxButton.OK, MessageBoxImage.Information);
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
                BtnRegister.Content = "注册";
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
