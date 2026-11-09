#pragma warning disable CA1822,CA1863
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Themes;
using ColorVision.UI.Menus;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ColorVision.UI.Desktop.Download
{

    public class DownloadRightMenuItemProvider : IRightMenuItemProvider
    {
        public IEnumerable<MenuItemMetadata> GetMenuItems()
        {
            var icon = new TextBlock
            {
                FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"),
                FontSize = 15,
                HorizontalAlignment = HorizontalAlignment.Center,
                Text = "\uE896",
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            icon.SetResourceReference(TextBlock.ForegroundProperty, "GlobalTextBrush");

            var menuItem = new MenuItemMetadata
            {
                Header = Properties.Resources.DownloadManager,
                Order = 100,
                Icon = icon,
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
                if (_instance.WindowState == WindowState.Minimized) _instance.WindowState = WindowState.Normal;
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
        private ListCollectionView? _view;
        private string? _searchKeyword;
        private DispatcherTimer? _searchTimer;
        private CancellationTokenSource? _loadCancellation;
        private bool _closed;
        private bool _recordsBusy;

        public DownloadWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
            Closed += (s, e) =>
            {
                _closed = true;
                _searchTimer?.Stop();
                _loadCancellation?.Cancel();
                _manager.StatusMessageChanged -= OnStatusMessageChanged;
                if (_view != null)
                {
                    ((INotifyCollectionChanged)_view).CollectionChanged -= OnViewChanged;
                    DownloadListView.ItemsSource = null;
                    _view.DetachFromSourceCollection();
                }
                _instance = null;
            };
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            _manager = Aria2cDownloadManager.GetInstance();
            _manager.StatusMessageChanged += OnStatusMessageChanged;
            // Filtering in memory keeps selection and scrolling while tasks change state.
            _view = new ListCollectionView(_manager.Tasks) { Filter = MatchesFilter, IsLiveFiltering = true };
            _view.LiveFilteringProperties.Add(nameof(DownloadTask.Status));
            ((INotifyCollectionChanged)_view).CollectionChanged += OnViewChanged;
            DownloadListView.ItemsSource = _view;
            UpdateTotalCount();
            UpdateStatusBar();
            _ = LoadRecordsAsync();
            _manager.PreloadAria2cAsync();
        }

        private bool MatchesFilter(object item)
        {
            if (item is not DownloadTask task) return false;
            if (FilterActive.IsChecked == true && !(task.IsDownloading || task.IsPaused)) return false;
            if (FilterFailed.IsChecked == true && task.Status != DownloadStatus.Failed) return false;
            return _searchKeyword == null
                || task.FileName.Contains(_searchKeyword, StringComparison.OrdinalIgnoreCase)
                || task.Url.Contains(_searchKeyword, StringComparison.OrdinalIgnoreCase);
        }

        private void OnViewChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdateTotalCount();
        private void UpdateTotalCount() => TotalCountText.Text = (_view?.Count ?? 0).ToString();

        private void OnStatusMessageChanged(object? sender, string message)
        {
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                if (!_closed) UpdateStatusBar();
            });
        }

        private void UpdateStatusBar()
        {
            bool running = _manager.IsAria2cRunning;
            StatusBarText.Text = _manager.ActiveTaskCount > 0 ? _manager.StatusMessage : string.Empty;
            StatusIndicator.Fill = running
                ? new SolidColorBrush(Color.FromRgb(76, 175, 80))
                : new SolidColorBrush(Color.FromRgb(158, 158, 158));
            StatusIndicator.ToolTip = string.Format(Properties.Resources.Aria2cServiceStatus,
                running ? Properties.Resources.Aria2cConnected : Properties.Resources.Aria2cDisconnected, _manager.CurrentRpcPort);
        }

        private async Task LoadRecordsAsync()
        {
            using var cancellation = new CancellationTokenSource();
            _loadCancellation = cancellation;
            try
            {
                await _manager.LoadRecordsAsync(cancellation.Token);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                log4net.LogManager.GetLogger(nameof(DownloadWindow)).Error("Download records could not be loaded.", ex);
                if (!_closed) StatusBarText.Text = ex.Message;
            }
            finally
            {
                _loadCancellation = null;
            }
        }

        private async void BtnAddUrl_Click(object sender, RoutedEventArgs e)
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
                string[] urls = dialog.DownloadUrls;
                try
                {
                    await Task.Run(() => { foreach (var url in urls) _manager.AddDownload(url, savePath, auth); });
                    FilterAll.IsChecked = true;
                }
                catch (Exception ex) { MessageBox.Show(ex.Message, "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error); }
            }
        }

        private void MoreActions_Click(object sender, RoutedEventArgs e)
        {
            var menu = MoreActionsButton.ContextMenu;
            menu.PlacementTarget = MoreActionsButton;
            menu.Placement = PlacementMode.Bottom;
            menu.IsOpen = true;
        }

        private void ClearRecords_Click(object sender, RoutedEventArgs e)
            => ClearRecords(Properties.Resources.ClearRecordsConfirm, _manager.ClearAllRecordsAsync);

        private void ClearCompletedRecords_Click(object sender, RoutedEventArgs e)
            => ClearRecords(Properties.Resources.ClearCompletedRecordsConfirm, _manager.ClearCompletedRecordsAsync);

        private async void ClearRecords(string confirmation, Func<Task> clear)
        {
            if (_recordsBusy) return;
            if (MessageBox.Show(confirmation, "ColorVision", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            _recordsBusy = true;
            try { await clear(); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error); }
            finally { _recordsBusy = false; }
        }

        private void Filter_Checked(object sender, RoutedEventArgs e) => _view?.Refresh();

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchTimer?.Stop();
            if (_searchTimer == null)
            {
                _searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
                _searchTimer.Tick += (s, args) =>
                {
                    _searchTimer.Stop();
                    _searchKeyword = SearchTextBox.Text?.Trim();
                    if (string.IsNullOrEmpty(_searchKeyword)) _searchKeyword = null;
                    _view?.Refresh();
                };
            }
            _searchTimer.Start();
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

        private void DownloadListView_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Delete) return;
            DeleteTasks(GetSelectedTasks());
            e.Handled = true;
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
            return task.IsRetryable;
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

        private async void DeleteTasks(IReadOnlyCollection<DownloadTask> tasks)
        {
            if (tasks.Count == 0 || _recordsBusy)
                return;

            if (AskDeleteFiles(tasks) is not bool deleteFiles)
                return;

            _recordsBusy = true;
            try { await _manager.DeleteRecordsAsync(tasks.Select(t => t.Id).ToArray(), deleteFiles); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error); }
            finally { _recordsBusy = false; }
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
        /// Confirms deleting records. Returns whether files should also be deleted, or null when the user cancels.
        /// </summary>
        private static bool? AskDeleteFiles(IReadOnlyCollection<DownloadTask> tasks)
        {
            var config = DownloadManagerConfig.Instance;
            bool anyFileExists = tasks.Any(t => t.HasLocalFiles);
            if (!config.PromptDeleteFile)
                return anyFileExists && config.DefaultDeleteFile;

            if (!anyFileExists)
            {
                var confirmed = MessageBox.Show(
                    string.Format(Properties.Resources.DeleteRecordsConfirm, tasks.Count),
                    Properties.Resources.DownloadManager,
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Question,
                    MessageBoxResult.Cancel);
                return confirmed == MessageBoxResult.OK ? false : null;
            }

            var result = MessageBox.Show(
                Properties.Resources.ConfirmDeleteFile,
                Properties.Resources.DownloadManager,
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question,
                config.DefaultDeleteFile ? MessageBoxResult.Yes : MessageBoxResult.No);
            return result switch
            {
                MessageBoxResult.Yes => true,
                MessageBoxResult.No => false,
                _ => null
            };
        }
    }
}
