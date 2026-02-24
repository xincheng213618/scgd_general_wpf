using ColorVision.Themes;
using System.Windows;

namespace ColorVision.Solution.Download
{
    public partial class AddDownloadDialog : Window
    {
        public string DownloadUrl => UrlTextBox.Text?.Trim() ?? string.Empty;
        public string SaveDirectory => SaveDirTextBox.Text?.Trim() ?? string.Empty;
        public string UserName => UserNameTextBox.Text?.Trim() ?? string.Empty;
        public string Password => PasswordTextBox.Password?.Trim() ?? string.Empty;

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
            if (string.IsNullOrWhiteSpace(DownloadUrl))
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
