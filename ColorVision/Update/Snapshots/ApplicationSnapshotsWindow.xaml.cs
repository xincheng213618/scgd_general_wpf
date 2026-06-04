using ColorVision.Common.Utilities;
using ColorVision.Themes;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Update
{
    public partial class ApplicationSnapshotsWindow : Window, INotifyPropertyChanged
    {
        private readonly ApplicationSnapshotService _snapshotService = ApplicationSnapshotService.Instance;
        private ApplicationSnapshotInfo? _selectedSnapshot;
        private bool _isBusy;
        private string _statusText = string.Empty;

        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<ApplicationSnapshotInfo> Snapshots { get; } = new();

        public string SnapshotDirectory => _snapshotService.SnapshotDirectory;

        public string ProgramDirectory => AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        public string CurrentVersion => ApplicationSnapshotService.GetCurrentVersionText();

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
        }

        private async void ApplicationSnapshotsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= ApplicationSnapshotsWindow_Loaded;
            await RunBusyAsync("正在检查默认快照...", async () =>
            {
                await _snapshotService.EnsureDefaultSnapshotAsync().ConfigureAwait(true);
                await RefreshSnapshotsAsync().ConfigureAwait(true);
                StatusText = "快照已加载";
            }).ConfigureAwait(true);
        }

        private async void CreateSnapshot_Click(object sender, RoutedEventArgs e)
        {
            await RunBusyAsync("正在创建快照...", async () =>
            {
                ApplicationSnapshotInfo snapshot = await _snapshotService.CreateUserSnapshotAsync().ConfigureAwait(true);
                await RefreshSnapshotsAsync(snapshot.FilePath).ConfigureAwait(true);
                StatusText = $"已创建 {snapshot.FileName}";
            }).ConfigureAwait(true);
        }

        private async void RebuildDefault_Click(object sender, RoutedEventArgs e)
        {
            await RunBusyAsync("正在重建默认快照...", async () =>
            {
                ApplicationSnapshotInfo snapshot = await _snapshotService.CreateDefaultSnapshotAsync(force: true).ConfigureAwait(true);
                await RefreshSnapshotsAsync(snapshot.FilePath).ConfigureAwait(true);
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

            string message = selectedSnapshot.IsDefault
                ? "删除默认快照后会立即重建，确定继续？"
                : $"确定删除 {selectedSnapshot.FileName}？";

            if (MessageBox.Show(this, message, "ColorVision", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            await RunBusyAsync("正在删除快照...", async () =>
            {
                bool wasDefault = selectedSnapshot.IsDefault;
                await _snapshotService.DeleteSnapshotAsync(selectedSnapshot).ConfigureAwait(true);
                await RefreshSnapshotsAsync(wasDefault ? _snapshotService.DefaultSnapshotPath : null).ConfigureAwait(true);
                StatusText = wasDefault ? "默认快照已重建" : "快照已删除";
            }).ConfigureAwait(true);
        }

        private async void RestoreSnapshot_Click(object sender, RoutedEventArgs e)
        {
            ApplicationSnapshotInfo? selectedSnapshot = SelectedSnapshot;
            if (selectedSnapshot == null)
                return;

            string message = $"将退出 ColorVision 并还原到 {selectedSnapshot.FileName}。确定继续？";
            if (MessageBox.Show(this, message, "ColorVision", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            await RunBusyAsync("正在准备还原...", async () =>
            {
                await _snapshotService.RestoreSnapshotAsync(selectedSnapshot).ConfigureAwait(true);
            }).ConfigureAwait(true);
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            Directory.CreateDirectory(SnapshotDirectory);
            PlatformHelper.OpenFolder(SnapshotDirectory);
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
