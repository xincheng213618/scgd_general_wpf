#pragma warning disable CA1822
using ColorVision.Common.Utilities;
using ColorVision.Recovery;
using ColorVision.Themes;
using ColorVision.UI;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;

namespace ColorVision.Update
{
    public partial class ApplicationSnapshotsWindow : Window, INotifyPropertyChanged
    {
        private readonly ApplicationSnapshotService _snapshotService = ApplicationSnapshotService.Instance;
        private ApplicationSnapshotInfo? _selectedSnapshot;
        private bool _isBusy;
        private string _statusText = string.Empty;

        public event PropertyChangedEventHandler? PropertyChanged;

        internal bool IsRunningApplication { get; set; }

        internal Action? ValidateRuntimeOperation { get; set; }

        internal Action<Window>? PrepareRuntimeOperation { get; set; }

        public ObservableCollection<ApplicationSnapshotInfo> Snapshots { get; } = new();

        public string SnapshotDirectory => _snapshotService.SnapshotDirectory;

        public string AutomaticSnapshotDirectory => _snapshotService.AutomaticSnapshotDirectory;

        public string ProgramDirectory => AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        public string CurrentVersion => ApplicationSnapshotService.GetCurrentVersionText();

        public bool CreateSnapshotBeforeUpdate
        {
            get => ApplicationSnapshotConfig.Instance.CreateSnapshotBeforeUpdate;
            set
            {
                if (ApplicationSnapshotConfig.Instance.CreateSnapshotBeforeUpdate == value)
                    return;

                ApplicationSnapshotConfig.Instance.CreateSnapshotBeforeUpdate = value;
                ConfigService.Instance.SaveConfigs();
                OnPropertyChanged();
            }
        }

        public bool CreateAutomaticSnapshotAfterHealthyStartup
        {
            get => ApplicationSnapshotConfig.Instance.CreateAutomaticSnapshotAfterHealthyStartup;
            set
            {
                if (ApplicationSnapshotConfig.Instance.CreateAutomaticSnapshotAfterHealthyStartup == value)
                    return;

                ApplicationSnapshotConfig.Instance.CreateAutomaticSnapshotAfterHealthyStartup = value;
                ConfigService.Instance.SaveConfigs();
                OnPropertyChanged();
            }
        }

        public ApplicationSnapshotInfo? SelectedSnapshot
        {
            get => _selectedSnapshot;
            set
            {
                if (_selectedSnapshot == value)
                    return;

                _selectedSnapshot = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanUseSelected));
            }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (_isBusy == value)
                    return;

                _isBusy = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanRunCommand));
                OnPropertyChanged(nameof(CanUseSelected));
            }
        }

        public bool CanRunCommand => !IsBusy;

        public bool CanUseSelected => !IsBusy && SelectedSnapshot != null;

        public string StatusText
        {
            get => _statusText;
            set
            {
                if (_statusText == value)
                    return;

                _statusText = value;
                OnPropertyChanged();
            }
        }

        public ApplicationSnapshotsWindow()
        {
            DataContext = this;
            InitializeComponent();
            this.ApplyCaption();
            Loaded += ApplicationSnapshotsWindow_Loaded;
            Closed += ApplicationSnapshotsWindow_Closed;
            _snapshotService.SnapshotCreated += SnapshotService_SnapshotCreated;
        }

        private async void ApplicationSnapshotsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= ApplicationSnapshotsWindow_Loaded;
            await RunBusyAsync("正在加载快照...", async () =>
            {
                await RefreshSnapshotsAsync().ConfigureAwait(true);
                StatusText = "快照已加载";
            }).ConfigureAwait(true);
        }

        private async void CreateSnapshot_Click(object sender, RoutedEventArgs e)
        {
            await RunBusyAsync("正在创建快照...", async () =>
            {
                ApplicationSnapshotInfo snapshot = await _snapshotService.CreateUserSnapshotAsync().ConfigureAwait(true);
                AddOrReplaceAndRevealSnapshot(snapshot);
                StatusText = $"已创建 {snapshot.FileName}";
            }).ConfigureAwait(true);
        }

        private async void RebuildDefault_Click(object sender, RoutedEventArgs e)
        {
            await RunBusyAsync("正在重建默认快照...", async () =>
            {
                ApplicationSnapshotInfo snapshot = await _snapshotService.CreateDefaultSnapshotAsync(force: true).ConfigureAwait(true);
                AddOrReplaceAndRevealSnapshot(snapshot);
                StatusText = "默认快照已重建";
            }).ConfigureAwait(true);
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            await RunBusyAsync("正在刷新快照...", async () =>
            {
                await RefreshSnapshotsAsync(SelectedSnapshot?.FilePath).ConfigureAwait(true);
                StatusText = "快照已刷新";
            }).ConfigureAwait(true);
        }

        private async void DeleteSnapshot_Click(object sender, RoutedEventArgs e)
        {
            ApplicationSnapshotInfo? selectedSnapshot = SelectedSnapshot;
            if (selectedSnapshot == null)
                return;

            string message = selectedSnapshot.IsAutomatic
                ? $"确定删除 {selectedSnapshot.FileName}？启用自动存档时，下次正常启动会重新创建。"
                : $"确定删除 {selectedSnapshot.FileName}？删除后不会自动重建。";

            if (MessageBox.Show(this, message, "ColorVision", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            await RunBusyAsync("正在删除快照...", async () =>
            {
                await _snapshotService.DeleteSnapshotAsync(selectedSnapshot).ConfigureAwait(true);
                await RefreshSnapshotsAsync().ConfigureAwait(true);
                StatusText = "快照已删除";
            }).ConfigureAwait(true);
        }

        private async void RestoreSnapshot_Click(object sender, RoutedEventArgs e)
        {
            ApplicationSnapshotInfo? selectedSnapshot = SelectedSnapshot;
            if (selectedSnapshot == null || IsBusy || !TryValidateRuntimeOperation())
                return;

            string message = $"将退出 ColorVision 并还原到 {selectedSnapshot.FileName}。确定继续？";
            if (MessageBox.Show(this, message, "ColorVision", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            await RestoreConfirmedSnapshotAsync(selectedSnapshot,
                snapshot => _snapshotService.RestoreSnapshotAsync(snapshot)).ConfigureAwait(true);
        }

        internal Task RestoreConfirmedSnapshotAsync(ApplicationSnapshotInfo snapshot, Func<ApplicationSnapshotInfo, Task> restore)
        {
            if (IsBusy || !TryValidateRuntimeOperation())
                return Task.CompletedTask;

            if (IsRunningApplication)
            {
                try
                {
                    if (PrepareRuntimeOperation == null)
                        throw new InvalidOperationException(StartupMaintenanceText.Get("ApplicationUnavailable"));
                    PrepareRuntimeOperation(this);
                }
                catch (Exception ex)
                {
                    StatusText = ex.GetBaseException().Message;
                    return Task.CompletedTask;
                }

                if (!TryValidateRuntimeOperation())
                    return Task.CompletedTask;
            }

            return RunBusyAsync("正在准备还原...", () => restore(snapshot));
        }

        private bool TryValidateRuntimeOperation()
        {
            if (!IsRunningApplication)
                return true;

            try
            {
                if (ValidateRuntimeOperation == null)
                    throw new InvalidOperationException(StartupMaintenanceText.Get("ApplicationUnavailable"));
                ValidateRuntimeOperation();
                return true;
            }
            catch (Exception ex)
            {
                StatusText = ex.GetBaseException().Message;
                return false;
            }
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            Directory.CreateDirectory(SnapshotDirectory);
            PlatformHelper.OpenFolder(SnapshotDirectory);
        }

        private async void ChooseAutomaticSnapshotDirectory_Click(object sender, RoutedEventArgs e)
        {
            OpenFolderDialog dialog = new()
            {
                Title = "选择自动存档位置",
                Multiselect = false,
                InitialDirectory = Directory.Exists(AutomaticSnapshotDirectory) ? AutomaticSnapshotDirectory : SnapshotDirectory,
            };
            if (dialog.ShowDialog(this) != true)
                return;

            try
            {
                string directory = _snapshotService.ResolveAutomaticSnapshotDirectory(dialog.FolderName);
                ApplicationSnapshotConfig.Instance.AutomaticSnapshotDirectory = directory;
                ConfigService.Instance.SaveConfigs();
                OnPropertyChanged(nameof(AutomaticSnapshotDirectory));
                await RefreshSnapshotsAsync().ConfigureAwait(true);
                StatusText = "自动存档位置已更新";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "ColorVision", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void ResetAutomaticSnapshotDirectory_Click(object sender, RoutedEventArgs e)
        {
            ApplicationSnapshotConfig.Instance.AutomaticSnapshotDirectory = string.Empty;
            ConfigService.Instance.SaveConfigs();
            OnPropertyChanged(nameof(AutomaticSnapshotDirectory));
            await RefreshSnapshotsAsync().ConfigureAwait(true);
            StatusText = "已恢复默认自动存档位置";
        }

        private void OpenAutomaticSnapshotDirectory_Click(object sender, RoutedEventArgs e)
        {
            Directory.CreateDirectory(AutomaticSnapshotDirectory);
            PlatformHelper.OpenFolder(AutomaticSnapshotDirectory);
        }

        private async Task RefreshSnapshotsAsync(string? preferredSelectionPath = null)
        {
            preferredSelectionPath ??= SelectedSnapshot?.FilePath;
            ApplicationSnapshotInfo[] snapshots = await Task.Run(() => _snapshotService.ListSnapshots().ToArray()).ConfigureAwait(true);

            Snapshots.Clear();
            foreach (ApplicationSnapshotInfo snapshot in snapshots)
            {
                Snapshots.Add(snapshot);
            }

            SelectedSnapshot = !string.IsNullOrWhiteSpace(preferredSelectionPath)
                ? Snapshots.FirstOrDefault(item => string.Equals(item.FilePath, preferredSelectionPath, StringComparison.OrdinalIgnoreCase)) ?? Snapshots.FirstOrDefault()
                : Snapshots.FirstOrDefault();
        }

        private void AddOrReplaceAndRevealSnapshot(ApplicationSnapshotInfo snapshot)
        {
            ApplicationSnapshotInfo[] orderedSnapshots = Snapshots
                .Where(item => !string.Equals(item.FilePath, snapshot.FilePath, StringComparison.OrdinalIgnoreCase))
                .Append(snapshot)
                .OrderByDescending(item => item.IsAutomatic)
                .ThenByDescending(item => item.IsDefault)
                .ThenByDescending(item => item.CreatedAt)
                .ToArray();

            Snapshots.Clear();
            foreach (ApplicationSnapshotInfo item in orderedSnapshots)
                Snapshots.Add(item);

            SelectedSnapshot = snapshot;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                SnapshotGrid.UpdateLayout();
                SnapshotGrid.ScrollIntoView(snapshot);
            }), DispatcherPriority.Background);
        }

        private void SnapshotService_SnapshotCreated(object? sender, ApplicationSnapshotInfo snapshot)
        {
            _ = Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!IsLoaded)
                    return;

                AddOrReplaceAndRevealSnapshot(snapshot);
                StatusText = "自动存档已更新";
            }), DispatcherPriority.Background);
        }

        private void ApplicationSnapshotsWindow_Closed(object? sender, EventArgs e)
        {
            _snapshotService.SnapshotCreated -= SnapshotService_SnapshotCreated;
        }

        private async Task RunBusyAsync(string busyStatus, Func<Task> action)
        {
            if (IsBusy)
                return;

            IsBusy = true;
            StatusText = busyStatus;
            try
            {
                await action().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                StatusText = ex.Message;
                MessageBox.Show(this, ex.Message, "ColorVision", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
