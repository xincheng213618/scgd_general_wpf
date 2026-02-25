using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Desktop.Download;
using ColorVision.UI.Menus;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.UI.Desktop.Download
{
    public class ExportDownloadWindow : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.Tool;
        public override string GuidId => "DownloadManager";
        public override string Header => Properties.Resources.DownloadManager;
        public override int Order => 30;

        public override object? Icon => new TextBlock() { FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"), Text = "\uE896" };

        public override void Execute()
        {
            DownloadWindow.ShowInstance();
        }
    }

    public class DownloadRightMenuItemProvider : IRightMenuItemProvider
    {
        public IEnumerable<MenuItemMetadata> GetMenuItems()
        {
            var menuItem = new MenuItemMetadata
            {
                Header = Properties.Resources.DownloadManager,
                Order = 100,
                Icon = new TextBlock() { FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"), Text = "\uE896" },
                Command = new RelayCommand(a => DownloadWindow.ShowInstance())
            };
            return new[] { menuItem };
        }
    }

    public partial class DownloadWindow : Window
    {
        private static DownloadWindow? _instance;

        public static void ShowInstance()
        {
            if (_instance == null || !_instance.IsLoaded)
            {
                _instance = new DownloadWindow();
                _instance.Owner = Application.Current.GetActiveWindow();
                _instance.Show();
            }
            else
            {
                _instance.Activate();
            }
        }

        private Aria2cDownloadManager _manager;
        private int _currentPage = 1;
        private int _pageSize = 20;
        private int _totalRecords;
        private int _totalPages = 1;
        private string? _searchKeyword;

        public DownloadWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
            Closed += (s, e) =>
            {
                if (_manager != null)
                    _manager.DownloadCompleted -= OnDownloadCompleted;
            };
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            _manager ??= Aria2cDownloadManager.GetInstance();
            _manager.DownloadCompleted -= OnDownloadCompleted;
            _manager.DownloadCompleted += OnDownloadCompleted;
            DownloadListView.ItemsSource = _manager.Tasks;
            LoadData();
        }

        private void OnDownloadCompleted(object? sender, DownloadTask task)
        {
            // Skip global notification for tasks with per-task callback (e.g., programmatic downloads like auto-update)
            if (task.OnCompletedCallback != null)
            {
                Application.Current?.Dispatcher.BeginInvoke(() => LoadData());
                return;
            }

            var config = DownloadManagerConfig.Instance;

            // Auto-run file after download
            if (task.Status == DownloadStatus.Completed && config.RunFileAfterDownload)
            {
                if (System.IO.File.Exists(task.SavePath))
                {
                    try
                    {
                        Application.Current?.Dispatcher.BeginInvoke(() =>
                        {
                            Process.Start(new ProcessStartInfo(task.SavePath) { UseShellExecute = true });
                        });
                    }
                    catch (Exception ex)
                    {
                        log4net.LogManager.GetLogger(nameof(DownloadWindow)).Error($"Auto-run failed: {ex.Message}");
                    }
                }
                Application.Current?.Dispatcher.BeginInvoke(() => LoadData());
                return;
            }

            // Show notification if enabled
            if (!config.ShowCompletedNotification)
            {
                Application.Current?.Dispatcher.BeginInvoke(() => LoadData());
                return;
            }

            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                if (task.Status == DownloadStatus.Completed)
                {
                    var result = MessageBox.Show(
                        string.Format(Properties.Resources.DownloadCompletedMessage, task.FileName) + "\n\n" + Properties.Resources.OpenFileQuestion,
                        Properties.Resources.DownloadManager,
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);

                    if (result == MessageBoxResult.Yes && System.IO.File.Exists(task.SavePath))
                    {
                        try
                        {
                            Process.Start(new ProcessStartInfo(task.SavePath) { UseShellExecute = true });
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.Message, "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
                else if (task.Status == DownloadStatus.Failed)
                {
                    MessageBox.Show(
                        string.Format(Properties.Resources.DownloadFailedMessage, task.FileName, task.ErrorMessage),
                        Properties.Resources.DownloadManager,
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
                LoadData();
            });
        }

        private void LoadData()
        {
            _totalRecords = _manager.GetTotalCount(_searchKeyword);
            _totalPages = Math.Max(1, (int)Math.Ceiling((double)_totalRecords / _pageSize));
            if (_currentPage > _totalPages) _currentPage = _totalPages;
            if (_currentPage < 1) _currentPage = 1;

            _manager.LoadRecords(_searchKeyword, _pageSize, _currentPage);
            UpdatePaginationUI();
        }

        private void UpdatePaginationUI()
        {
            TotalCountText.Text = _totalRecords.ToString();
            TotalPagesText.Text = _totalPages.ToString();
            CurrentPageTextBox.Text = _currentPage.ToString();
        }

        private void BtnAddUrl_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new AddDownloadDialog { Owner = this };
            if (dialog.ShowDialog() == true && dialog.DownloadUrls.Length > 0)
            {
                string? auth = null;
                if (!string.IsNullOrWhiteSpace(dialog.UserName) && !string.IsNullOrWhiteSpace(dialog.Password))
                {
                    auth = $"{dialog.UserName}:{dialog.Password}";
                }
                string? savePath = !string.IsNullOrWhiteSpace(dialog.SaveDirectory) ? dialog.SaveDirectory : null;
                foreach (var url in dialog.DownloadUrls)
                {
                    _manager.AddDownload(url, savePath, auth);
                }
                LoadData();
            }
        }

        private void ClearRecords_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(Properties.Resources.ClearRecordsConfirm, "ColorVision", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                _manager.ClearAllRecords();
                _currentPage = 1;
                LoadData();
            }
        }

        private void DeleteSelected_Click(object sender, RoutedEventArgs e)
        {
            var selected = DownloadListView.SelectedItems.Cast<DownloadTask>().ToList();
            if (selected.Count == 0) return;

            _manager.DeleteRecords(selected.Select(t => t.Id).ToArray());
            LoadData();
        }

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            _searchKeyword = SearchTextBox.Text?.Trim();
            _currentPage = 1;
            LoadData();
        }

        private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Search_Click(sender, e);
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            _searchKeyword = null;
            SearchTextBox.Text = string.Empty;
            _currentPage = 1;
            LoadData();
        }

        private void OpenDownloadFolder_Click(object sender, RoutedEventArgs e)
        {
            string folder = DownloadManagerConfig.Instance.DefaultDownloadPath;
            if (!System.IO.Directory.Exists(folder))
                System.IO.Directory.CreateDirectory(folder);
            PlatformHelper.OpenFolder(folder);
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            new PropertyEditorWindow(DownloadManagerConfig.Instance) { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }

        private void DownloadListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DownloadListView.SelectedItem is DownloadTask task)
            {
                if (System.IO.File.Exists(task.SavePath))
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo(task.SavePath) { UseShellExecute = true });
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else if (System.IO.Directory.Exists(System.IO.Path.GetDirectoryName(task.SavePath)))
                {
                    PlatformHelper.OpenFolder(System.IO.Path.GetDirectoryName(task.SavePath)!);
                }
            }
        }

        private void ContextMenu_OpenFile_Click(object sender, RoutedEventArgs e)
        {
            if (DownloadListView.SelectedItem is DownloadTask task && System.IO.File.Exists(task.SavePath))
            {
                try
                {
                    Process.Start(new ProcessStartInfo(task.SavePath) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ContextMenu_OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            if (DownloadListView.SelectedItem is DownloadTask task)
            {
                string? dir = System.IO.Path.GetDirectoryName(task.SavePath);
                if (dir != null && System.IO.Directory.Exists(dir))
                {
                    PlatformHelper.OpenFolder(dir);
                }
            }
        }

        private void ContextMenu_CopyUrl_Click(object sender, RoutedEventArgs e)
        {
            if (DownloadListView.SelectedItem is DownloadTask task)
            {
                Clipboard.SetText(task.Url);
            }
        }

        private void ContextMenu_Retry_Click(object sender, RoutedEventArgs e)
        {
            if (DownloadListView.SelectedItem is DownloadTask task)
            {
                _manager.RetryDownload(task);
            }
        }

        private void ContextMenu_Cancel_Click(object sender, RoutedEventArgs e)
        {
            if (DownloadListView.SelectedItem is DownloadTask task)
            {
                _manager.CancelDownload(task);
            }
        }

        private void ContextMenu_Delete_Click(object sender, RoutedEventArgs e)
        {
            var selected = DownloadListView.SelectedItems.Cast<DownloadTask>().ToList();
            if (selected.Count == 0) return;
            _manager.DeleteRecords(selected.Select(t => t.Id).ToArray());
            LoadData();
        }

        private void PageSizeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsInitialized) return;
            if (PageSizeComboBox.SelectedItem is ComboBoxItem item && int.TryParse(item.Content.ToString(), out int pageSize))
            {
                _pageSize = pageSize;
                _currentPage = 1;
                LoadData();
            }
        }

        private void FirstPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage != 1) { _currentPage = 1; LoadData(); }
        }

        private void PrevPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 1) { _currentPage--; LoadData(); }
        }

        private void NextPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage < _totalPages) { _currentPage++; LoadData(); }
        }

        private void LastPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage != _totalPages) { _currentPage = _totalPages; LoadData(); }
        }

        private void CurrentPageTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && int.TryParse(CurrentPageTextBox.Text, out int page))
            {
                page = Math.Max(1, Math.Min(page, _totalPages));
                if (page != _currentPage) { _currentPage = page; LoadData(); }
            }
        }
    }
}
