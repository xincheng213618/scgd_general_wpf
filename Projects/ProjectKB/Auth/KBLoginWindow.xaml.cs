using System.Windows;
using System.Windows.Input;

namespace ProjectKB.Auth
{
    public partial class KBLoginWindow : Window
    {
        private int _attemptCount;
        private const int MaxAttempts = 3;

        public bool LoginSuccess { get; private set; }

        public KBLoginWindow()
        {
            InitializeComponent();
            UserNameTextBox.Text = KBAuthManager.GetInstance().AdminUserName;
            Loaded += (s, e) => PasswordBox.Focus();
        }

        private void Login_Click(object sender, RoutedEventArgs e)
        {
            AttemptLogin();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            LoginSuccess = false;
            DialogResult = false;
        }

        private void ResetPassword_Click(object sender, RoutedEventArgs e)
        {
            bool showPath = Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
            string message = $"请关闭软件，由维护人员删除本机密码文件后重新启动。\n\n重启后管理员账号和密码会恢复为：{KBAuthConfig.DefaultUserName} / {KBAuthConfig.DefaultPassword}\n登录后请立即修改密码。";

            message += showPath
                ? $"\n\n密码文件：\n{KBAuthConfig.PasswordFilePath}"
                : "\n\n维护人员可按住 Ctrl+Shift 再点击“忘记密码”查看密码文件位置。";

            MessageBox.Show(this, message, "忘记密码", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                AttemptLogin();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                LoginSuccess = false;
                DialogResult = false;
            }
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            // 清除错误提示
            ErrorText.Visibility = Visibility.Collapsed;
        }

        private void UserNameTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            ErrorText.Visibility = Visibility.Collapsed;
        }

        private void AttemptLogin()
        {
            string userName = UserNameTextBox.Text.Trim();
            string password = PasswordBox.Password;

            if (string.IsNullOrWhiteSpace(userName))
            {
                ShowError("请输入账号");
                return;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                ShowError("请输入密码");
                return;
            }

            bool success = KBAuthManager.GetInstance().Login(userName, password);

            if (success)
            {
                LoginSuccess = true;
                DialogResult = true;
            }
            else
            {
                _attemptCount++;
                int remaining = MaxAttempts - _attemptCount;

                if (remaining <= 0)
                {
                    ShowError("密码错误次数过多，窗口将关闭");
                    LoginSuccess = false;
                    DialogResult = false;
                }
                else
                {
                    ShowError("账号或密码错误");
                    AttemptText.Text = $"剩余尝试次数：{remaining}";
                    AttemptText.Visibility = Visibility.Visible;
                    PasswordBox.Clear();
                    PasswordBox.Focus();
                }
            }
        }

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorText.Visibility = Visibility.Visible;
        }
    }
}
