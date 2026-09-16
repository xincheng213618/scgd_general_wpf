using System.Windows;
using System.Windows.Controls;

namespace ColorVision.UI.Desktop.Feedback
{
    /// <summary>
    /// Collects a Web account credential for one feedback submission. The
    /// password is never persisted and the user may explicitly submit anonymously.
    /// </summary>
    internal sealed class FeedbackAccountLoginDialog : Window
    {
        private readonly TextBox _username = new();
        private readonly PasswordBox _password = new();

        public string Username => _username.Text.Trim();
        public string Password => _password.Password;
        public bool SubmitAnonymously { get; private set; }

        public FeedbackAccountLoginDialog()
        {
            Title = "登录反馈账号";
            Width = 420;
            SizeToContent = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;

            var panel = new StackPanel { Margin = new Thickness(20) };
            panel.Children.Add(new TextBlock
            {
                Text = "登录后，本次反馈会绑定到服务端账号，之后可在“我的反馈”中查看和下载。",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 16),
            });
            panel.Children.Add(new TextBlock { Text = "Web 用户名" });
            _username.Margin = new Thickness(0, 6, 0, 12);
            panel.Children.Add(_username);
            panel.Children.Add(new TextBlock { Text = "Web 密码" });
            _password.Margin = new Thickness(0, 6, 0, 18);
            panel.Children.Add(_password);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            var anonymous = new Button { Content = "匿名提交", Padding = new Thickness(14, 6, 14, 6) };
            anonymous.Click += (_, _) =>
            {
                SubmitAnonymously = true;
                DialogResult = true;
            };
            var cancel = new Button
            {
                Content = "取消",
                IsCancel = true,
                Padding = new Thickness(14, 6, 14, 6),
                Margin = new Thickness(8, 0, 0, 0),
            };
            var login = new Button
            {
                Content = "登录并提交",
                IsDefault = true,
                Padding = new Thickness(14, 6, 14, 6),
                Margin = new Thickness(8, 0, 0, 0),
            };
            login.Click += (_, _) =>
            {
                if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrEmpty(Password))
                {
                    MessageBox.Show(this, "请输入 Web 用户名和密码。", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                DialogResult = true;
            };
            buttons.Children.Add(anonymous);
            buttons.Children.Add(cancel);
            buttons.Children.Add(login);
            panel.Children.Add(buttons);
            Content = panel;
            Loaded += (_, _) => _username.Focus();
        }
    }
}
