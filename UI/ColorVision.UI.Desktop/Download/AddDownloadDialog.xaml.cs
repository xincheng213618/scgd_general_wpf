using ColorVision.Themes;
using System.Linq;
using System.Windows;

namespace ColorVision.UI.Desktop.Download
{
    public partial class AddDownloadDialog : Window
    {
        public string DownloadUrl => UrlTextBox.Text?.Trim() ?? string.Empty;
        public string SaveDirectory => SaveDirTextBox.Text?.Trim() ?? string.Empty;
        public string UserName => UserNameTextBox.Text?.Trim() ?? string.Empty;
        public string Password => PasswordTextBox.Password?.Trim() ?? string.Empty;

        /// <summary>
        /// Returns multiple URLs split by ';' or newline, trimmed and filtered
        /// </summary>
        public string[] DownloadUrls => (UrlTextBox.Text ?? string.Empty)
            .Split(new[] { ';', '\r', '\n' })
            .Select(u => u.Trim())
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .ToArray();

        public AddDownloadDialog()
        {
            InitializeComponent();
            this.ApplyCaption();
            SaveDirTextBox.Text = DownloadManagerConfig.Instance.DefaultDownloadPath;
        }

        private void BrowseDir_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            dialog.SelectedPath = SaveDirTextBox.Text;
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                SaveDirTextBox.Text = dialog.SelectedPath;
            }
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            if (DownloadUrls.Length == 0)
            {
                MessageBox.Show(Properties.Resources.UrlRequired, "ColorVision", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
