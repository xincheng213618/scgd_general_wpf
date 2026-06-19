using System.Windows;
using System.Windows.Input;

namespace ProjectKB.Auth
{
    public partial class KBChangePasswordWindow : Window
    {
        public KBChangePasswordWindow()
        {
            InitializeComponent();
            Loaded += (s, e) => OldPasswordBox.Focus();
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            ChangePassword();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ChangePassword();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                DialogResult = false;
            }
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            ErrorText.Visibility = Visibility.Collapsed;
        }

        private void ChangePassword()
        {
            string oldPassword = OldPasswordBox.Password;
            string newPassword = NewPasswordBox.Password;
            string confirmPassword = ConfirmPasswordBox.Password;

            if (string.IsNullOrWhiteSpace(oldPassword))
            {
                ShowError("请输入旧密码");
                return;
            }

            if (string.IsNullOrWhiteSpace(newPassword))
            {
                ShowError("请输入新密码");
                return;
            }

            if (newPassword.Length < 4)
            {
                ShowError("新密码至少需要4位");
                return;
            }

            if (newPassword != confirmPassword)
            {
                ShowError("两次输入的新密码不一致");
                return;
            }

            if (KBAuthManager.GetInstance().ChangePassword(oldPassword, newPassword))
            {
                MessageBox.Show(this, "管理员密码已修改。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                return;
            }

            ShowError("旧密码错误");
            OldPasswordBox.Clear();
            OldPasswordBox.Focus();
        }

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorText.Visibility = Visibility.Visible;
        }
    }
}
