using ColorVision.Themes;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace ColorVision.Database
{
    public partial class MySqlRestoreProgressWindow : Window
    {
        private bool _isRunning = true;

        public MySqlRestoreProgressWindow(string backupFile, string database)
        {
            InitializeComponent();
            this.ApplyCaption();
            BackupPathTextBox.Text = backupFile;
            DatabaseTextBox.Text = database;
        }

        public void Report(string stage, string message, int progress)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => Report(stage, message, progress));
                return;
            }

            StatusTextBlock.Text = stage;
            RestoreProgressBar.Value = Math.Clamp(progress, 0, 100);
            DetailTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
            DetailTextBox.ScrollToEnd();
        }

        public void Complete(bool success, string message)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => Complete(success, message));
                return;
            }

            _isRunning = false;
            StatusTextBlock.Text = success ? "恢复完成" : "恢复失败";
            if (success)
                RestoreProgressBar.Value = 100;
            DetailTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
            DetailTextBox.ScrollToEnd();
            RestartButton.IsEnabled = success;
            CloseButton.IsEnabled = true;
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (_isRunning)
            {
                e.Cancel = true;
                return;
            }
            base.OnClosing(e);
        }

        private void CopyDetails_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(DetailTextBox.Text))
                Clipboard.SetText(DetailTextBox.Text);
        }

        private void RestartButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string applicationPath = Path.ChangeExtension(Application.ResourceAssembly.Location, ".exe");
                Process? process = Process.Start(applicationPath, "-r");
                if (process == null)
                    throw new InvalidOperationException("未能创建新的应用进程。");
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"ColorVision 重启失败：{ex.Message}", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
