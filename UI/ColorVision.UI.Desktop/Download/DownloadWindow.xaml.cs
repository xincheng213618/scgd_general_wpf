#pragma warning disable CA1822,CA1863
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Themes;
using ColorVision.UI.Menus;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ColorVision.UI.Desktop.Download
{

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
                _instance.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                _instance.Show();
            }
            else
            {
                _instance.Activate();
            }
        }

        public static void CloseInstance()
        {
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                if (_instance == null || !_instance.IsLoaded)
                    return;

                _instance.Close();
            });
        }

        private Aria2cDownloadManager _manager;
        private int _currentPage = 1;
        private int _pageSize = 20;
        private int _totalRecords;
        private int _totalPages = 1;
        private string? _searchKeyword;
        private DispatcherTimer? _searchTimer;

        public DownloadWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
            Closed += (s, e) =>
            {
                if (_manager != null)
                {
                    _manager.DownloadCompleted -= OnDownloadCompleted;
                    _manager.StatusMessageChanged -= OnStatusMessageChanged;
                }
                _instance = null;
            };
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            _manager ??= Aria2cDownloadManager.GetInstance();
            _manager.DownloadCompleted -= OnDownloadCompleted;
            _manager.DownloadCompleted += OnDownloadCompleted;
            _manager.StatusMessageChanged -= OnStatusMessageChanged;
            _manager.StatusMessageChanged += OnStatusMessageChanged;
            DownloadListView.ItemsSource = _manager.Tasks;
            LoadData();

            if (!string.IsNullOrEmpty(_manager.StatusMessage))
                StatusBarText.Text = _manager.StatusMessage;

            UpdateStatusIndicator();

            // Pre-load aria2c daemon so manual downloads don't wait
            _manager.PreloadAria2cAsync();
        }

        private void OnStatusMessageChanged(object? sender, string message)
        {
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                StatusBarText.Text = message;
                UpdateStatusIndicator();
            });
        }

        private void UpdateStatusIndicator()
        {
            StatusIndicator.Fill = _manager.IsAria2cRunning
                ? new SolidColorBrush(Color.FromRgb(76, 175, 80))
                : new SolidColorBrush(Color.FromRgb(158, 158, 158));
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

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchTimer?.Stop();
            _searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _searchTimer.Tick += (s, args) =>
            {
                _searchTimer.Stop();
                _searchKeyword = SearchTextBox.Text?.Trim();
                if (string.IsNullOrEmpty(_searchKeyword)) _searchKeyword = null;
                _currentPage = 1;
                LoadData();
            };
            _searchTimer.Start();
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            _searchKeyword = null;
            SearchTextBox.Text = string.Empty;
            _currentPage = 1;
            LoadData();
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            new PropertyEditorWindow(DownloadManagerConfig.Instance) { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }

        private void DownloadListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (GetPrimarySelectedTask() is DownloadTask task)
                OpenTask(task);
        }

        private void DownloadListView_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is not DependencyObject source)
                return;

            var item = FindVisualParent<ListBoxItem>(source);
            if (item == null)
                return;

            if (!item.IsSelected)
            {
                DownloadListView.SelectedItems.Clear();
                item.IsSelected = true;
            }

            DownloadListView.SelectedItem = item.DataContext;
            item.Focus();
        }

        private void DownloadContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            UpdateContextMenuState();
        }

        private void ContextMenu_OpenFile_Click(object sender, RoutedEventArgs e)
        {
            if (GetPrimarySelectedTask() is DownloadTask task)
                OpenTaskFile(task);
        }

        private void ContextMenu_OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            if (GetPrimarySelectedTask() is DownloadTask task)
                OpenTaskFolder(task);
        }

        private void ContextMenu_CopyUrl_Click(object sender, RoutedEventArgs e)
        {
            CopySelectedUrls();
        }

        private void ContextMenu_Pause_Click(object sender, RoutedEventArgs e)
        {
            ApplyToSelectedTasks(CanPause, _manager.PauseDownload);
        }

        private void ContextMenu_Resume_Click(object sender, RoutedEventArgs e)
        {
            ApplyToSelectedTasks(CanResume, _manager.ResumeDownload);
        }

        private void ContextMenu_Retry_Click(object sender, RoutedEventArgs e)
        {
            ApplyToSelectedTasks(CanRetry, _manager.RetryDownload);
        }

        private void ContextMenu_Cancel_Click(object sender, RoutedEventArgs e)
        {
            ApplyToSelectedTasks(CanCancel, _manager.CancelDownload);
        }

        private void ContextMenu_Delete_Click(object sender, RoutedEventArgs e)
        {
            DeleteTasks(GetSelectedTasks());
        }

        private void InlineOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            if (GetTaskFromSender(sender) is DownloadTask task)
                PlatformHelper.OpenFolderAndSelectFile(task.SavePath);
        }

        private void InlineDelete_Click(object sender, RoutedEventArgs e)
        {
            if (GetTaskFromSender(sender) is DownloadTask task)
                DeleteTasks(new[] { task });
        }

        private void InlinePause_Click(object sender, RoutedEventArgs e)
        {
            if (GetTaskFromSender(sender) is DownloadTask task && CanPause(task))
                _manager.PauseDownload(task);
        }

        private void InlineResume_Click(object sender, RoutedEventArgs e)
        {
            if (GetTaskFromSender(sender) is DownloadTask task && CanResume(task))
                _manager.ResumeDownload(task);
        }

        private void InlineRetry_Click(object sender, RoutedEventArgs e)
        {
            if (GetTaskFromSender(sender) is DownloadTask task && CanRetry(task))
                _manager.RetryDownload(task);
        }

        private void UpdateContextMenuState()
        {
            var selected = GetSelectedTasks();
            var primaryTask = GetPrimarySelectedTask(selected);

            MenuOpenFile.IsEnabled = CanOpenFile(primaryTask);
            MenuOpenFolder.IsEnabled = CanOpenFolder(primaryTask);
            MenuCopyUrl.IsEnabled = selected.Count > 0;
            MenuPause.IsEnabled = selected.Any(CanPause);
            MenuResume.IsEnabled = selected.Any(CanResume);
            MenuRetry.IsEnabled = selected.Any(CanRetry);
            MenuCancel.IsEnabled = selected.Any(CanCancel);
            MenuDelete.IsEnabled = selected.Count > 0;
        }

        private List<DownloadTask> GetSelectedTasks()
        {
            return DownloadListView.SelectedItems.Cast<DownloadTask>().ToList();
        }

        private DownloadTask? GetPrimarySelectedTask()
        {
            return GetPrimarySelectedTask(GetSelectedTasks());
        }

        private DownloadTask? GetPrimarySelectedTask(List<DownloadTask> selected)
        {
            if (DownloadListView.SelectedItem is DownloadTask task && selected.Contains(task))
                return task;

            return selected.Count > 0 ? selected[0] : null;
        }

        private static DownloadTask? GetTaskFromSender(object sender)
        {
            return (sender as FrameworkElement)?.DataContext as DownloadTask;
        }

        private static bool CanOpenFile(DownloadTask? task)
        {
            return task != null && File.Exists(task.SavePath);
        }

        private static bool CanOpenFolder(DownloadTask? task)
        {
            return task != null && !string.IsNullOrWhiteSpace(task.SavePath);
        }

        private static bool CanPause(DownloadTask task)
        {
            return task.IsDownloading;
        }

        private static bool CanResume(DownloadTask task)
        {
            return task.Status == DownloadStatus.Paused;
        }

        private static bool CanRetry(DownloadTask task)
        {
            return task.Status == DownloadStatus.Waiting
                || task.Status == DownloadStatus.Failed
                || task.Status == DownloadStatus.FileDeleted;
        }

        private static bool CanCancel(DownloadTask task)
        {
            return task.Status == DownloadStatus.Waiting || task.Status == DownloadStatus.Downloading;
        }

        private void OpenTask(DownloadTask task)
        {
            if (!OpenTaskFile(task))
                OpenTaskFolder(task);
        }

        private bool OpenTaskFile(DownloadTask task)
        {
            if (!File.Exists(task.SavePath))
                return false;

            try
            {
                Process.Start(new ProcessStartInfo(task.SavePath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return true;
        }

        private static void OpenTaskFolder(DownloadTask task)
        {
            PlatformHelper.OpenFolderAndSelectFile(task.SavePath);
        }

        private void CopySelectedUrls()
        {
            var urls = GetSelectedTasks()
                .Select(task => task.Url)
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .ToList();

            if (urls.Count > 0)
                Common.Clipboard.SetText(string.Join(Environment.NewLine, urls));
        }

        private void ApplyToSelectedTasks(Func<DownloadTask, bool> canApply, Action<DownloadTask> apply)
        {
            foreach (var task in GetSelectedTasks().Where(canApply).ToList())
            {
                apply(task);
            }
        }

        private void DeleteTasks(IReadOnlyCollection<DownloadTask> tasks)
        {
            if (tasks.Count == 0)
                return;

            bool deleteFiles = ShouldDeleteFiles(tasks);
            _manager.DeleteRecords(tasks.Select(t => t.Id).ToArray(), deleteFiles);
            LoadData();
        }

        private static T? FindVisualParent<T>(DependencyObject? source) where T : DependencyObject
        {
            while (source != null)
            {
                if (source is T target)
                    return target;

                source = VisualTreeHelper.GetParent(source);
            }

            return null;
        }

        /// <summary>
        /// Ask user whether to delete files when deleting download records.
        /// Returns true if files should be deleted.
        /// </summary>
        private bool ShouldDeleteFiles(IEnumerable<DownloadTask> tasks)
        {
            var config = DownloadManagerConfig.Instance;
            // Check if any file exists on disk
            bool anyFileExists = tasks.Any(t => System.IO.File.Exists(t.SavePath));
            if (!anyFileExists) return false;

            if (!config.PromptDeleteFile)
                return config.DefaultDeleteFile;

            var defaultButton = config.DefaultDeleteFile ? MessageBoxResult.Yes : MessageBoxResult.No;
            var result = MessageBox.Show(
                Properties.Resources.ConfirmDeleteFile,
                Properties.Resources.DownloadManager,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                defaultButton);
            return result == MessageBoxResult.Yes;
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
