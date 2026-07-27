#pragma warning disable CA1822
using ColorVision.Common.MVVM;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVision.FloatingBall
{
    public partial class DesktopPetSettingsControl : UserControl
    {
        private readonly DesktopPetSettingsViewModel _viewModel = new();
        private HashSet<string>? _codexCreationBaselineIds;
        private DateTime _codexCreationWatchExpiresUtc;
        private DispatcherTimer? _codexCreationWatchTimer;
        private bool _isLoaded;
        private bool _isRefreshingAssets;

        public DesktopPetSettingsControl()
        {
            InitializeComponent();
            DataContext = _viewModel;
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (_isLoaded)
                return;

            _isLoaded = true;
            MainWindowConfig.Instance.PropertyChanged += MainWindowConfig_PropertyChanged;
            UpdateWakePetButton();
            await RefreshAssetsAsync(forceRefresh: false);
            ResumeCodexCreationWatch();
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded)
                return;

            _isLoaded = false;
            StopCodexCreationWatch(clearState: false);
            MainWindowConfig.Instance.PropertyChanged -= MainWindowConfig_PropertyChanged;
            ConfigHandler.GetInstance().Save<DesktopPetConfig>();
            DesktopPetService.GetInstance().RefreshCopilotIntegration();
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await RefreshAssetsAsync(forceRefresh: true);
        }

        private async void CreatePetButton_Click(object sender, RoutedEventArgs e)
        {
            var codexBaselineIds = new HashSet<string>(
                _viewModel.Assets
                    .Where(item => item.Asset.Source == DesktopPetAssetSource.CodexCustom)
                    .Select(item => item.Asset.Id),
                StringComparer.OrdinalIgnoreCase);
            var dialog = new DesktopPetCreateWindow();
            var owner = Window.GetWindow(this);
            if (owner != null)
                dialog.Owner = owner;

            if (dialog.ShowDialog() != true)
                return;

            if (dialog.CodexLaunchStarted)
            {
                BeginCodexCreationWatch(codexBaselineIds);
                StatusText.Text = "Codex 已打开并预填创建任务。发送任务后，本页会自动发现并选中新宠物。";
                return;
            }

            if (string.IsNullOrWhiteSpace(dialog.CreatedAssetId))
                return;

            await RefreshAssetsAsync(forceRefresh: true);
            var createdOption = _viewModel.Assets.FirstOrDefault(item =>
                string.Equals(item.Asset.Id, dialog.CreatedAssetId, StringComparison.OrdinalIgnoreCase));
            if (createdOption == null)
            {
                StatusText.Text = "素材已经复制，但刷新后未能读取。请打开素材目录检查 pet.json。";
                return;
            }

            SelectOption(createdOption);
            StatusText.Text = $"已创建并选中“{dialog.CreatedDisplayName ?? createdOption.DisplayName}”。";
        }

        private void SelectPetButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: DesktopPetAssetOption option } || !option.CanSelect)
                return;

            SelectOption(option);
        }

        private void WakePetButton_Click(object sender, RoutedEventArgs e)
        {
            MainWindowConfig.Instance.OpenFloatingBall = !MainWindowConfig.Instance.OpenFloatingBall;
        }

        private void MainWindowConfig_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainWindowConfig.OpenFloatingBall))
                UpdateWakePetButton();
        }

        private void UpdateWakePetButton()
        {
            WakePetButton.Content = MainWindowConfig.Instance.OpenFloatingBall
                ? "收起宠物"
                : "唤醒宠物";
        }

        private void OpenPetFolderButton_Click(object sender, RoutedEventArgs e)
        {
            Directory.CreateDirectory(DesktopPetAssetCatalog.ColorVisionPetDirectory);
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{DesktopPetAssetCatalog.ColorVisionPetDirectory}\"",
                UseShellExecute = true,
            });
        }

        private void AdvancedButton_Click(object sender, RoutedEventArgs e)
        {
            DesktopPetService.GetInstance().OpenAdvancedSettings(Window.GetWindow(this));
        }

        private async Task RefreshAssetsAsync(bool forceRefresh)
        {
            if (_isRefreshingAssets)
                return;

            _isRefreshingAssets = true;
            RefreshButton.IsEnabled = false;
            StatusText.Text = "正在查找 ColorVision、Codex 和自定义宠物素材…";
            try
            {
                var catalog = DesktopPetAssetCatalog.Shared;
                var assets = forceRefresh
                    ? await catalog.RefreshAsync()
                    : await catalog.EnsureLoadedAsync();

                _viewModel.Assets.Clear();
                foreach (var asset in assets)
                {
                    var option = new DesktopPetAssetOption(asset)
                    {
                        IsSelected = string.Equals(asset.Id, DesktopPetConfig.Instance.SelectedPetId, StringComparison.OrdinalIgnoreCase),
                    };
                    _viewModel.Assets.Add(option);
                }

                await Task.WhenAll(_viewModel.Assets.Select(LoadThumbnailAsync));

                var codexAssetCount = _viewModel.Assets.Count(item => item.Asset.Source == DesktopPetAssetSource.CodexBuiltIn);
                var customAssetCount = _viewModel.Assets.Count(item =>
                    item.Asset.Source is DesktopPetAssetSource.CodexCustom or DesktopPetAssetSource.ColorVisionCustom);
                StatusText.Text = codexAssetCount > 0
                    ? $"已加载 {_viewModel.Assets.Count} 个宠物，其中 {codexAssetCount} 个来自本机 Codex，{customAssetCount} 个来自自定义素材包。"
                    : $"已加载 {_viewModel.Assets.Count} 个宠物。未检测到可读取的 Codex 安装素材；自定义素材仍可使用相同的 pet.json 格式。";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"素材刷新失败：{ex.Message}";
            }
            finally
            {
                _isRefreshingAssets = false;
                RefreshButton.IsEnabled = true;
            }
        }

        private void BeginCodexCreationWatch(IReadOnlySet<string> baselineIds)
        {
            StopCodexCreationWatch(clearState: true);
            _codexCreationBaselineIds = new HashSet<string>(baselineIds, StringComparer.OrdinalIgnoreCase);
            _codexCreationWatchExpiresUtc = DateTime.UtcNow.AddHours(2);
            StartCodexCreationWatchTimer();
        }

        private void ResumeCodexCreationWatch()
        {
            if (_codexCreationBaselineIds != null && DateTime.UtcNow < _codexCreationWatchExpiresUtc)
                StartCodexCreationWatchTimer();
        }

        private void StartCodexCreationWatchTimer()
        {
            if (_codexCreationWatchTimer != null)
                return;

            _codexCreationWatchTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromSeconds(4),
            };
            _codexCreationWatchTimer.Tick += CodexCreationWatchTimer_Tick;
            _codexCreationWatchTimer.Start();
        }

        private void StopCodexCreationWatch(bool clearState)
        {
            if (_codexCreationWatchTimer != null)
            {
                _codexCreationWatchTimer.Stop();
                _codexCreationWatchTimer.Tick -= CodexCreationWatchTimer_Tick;
                _codexCreationWatchTimer = null;
            }

            if (clearState)
            {
                _codexCreationBaselineIds = null;
                _codexCreationWatchExpiresUtc = default;
            }
        }

        private async void CodexCreationWatchTimer_Tick(object? sender, EventArgs e)
        {
            if (_codexCreationBaselineIds == null)
                return;
            if (DateTime.UtcNow >= _codexCreationWatchExpiresUtc)
            {
                StopCodexCreationWatch(clearState: true);
                StatusText.Text = "尚未发现新的 Codex 宠物。完成创建后可点击“刷新”加载。";
                return;
            }

            var packageIds = await Task.Run(() => DesktopPetCodexService.SnapshotPetPackageIds());
            if (!packageIds.Any(id => !_codexCreationBaselineIds.Contains(id)))
                return;

            await RefreshAssetsAsync(forceRefresh: true);
            var createdOption = _viewModel.Assets
                .Where(item =>
                    item.Asset.Source == DesktopPetAssetSource.CodexCustom
                    && !_codexCreationBaselineIds.Contains(item.Asset.Id)
                    && item.CanSelect)
                .OrderByDescending(item =>
                    item.Asset.FilePath == null ? DateTime.MinValue : File.GetLastWriteTimeUtc(item.Asset.FilePath))
                .FirstOrDefault();
            if (createdOption == null)
                return;

            StopCodexCreationWatch(clearState: true);
            SelectOption(createdOption);
            StatusText.Text = $"Codex 已创建并自动选中“{createdOption.DisplayName}”。";
        }

        private void SelectOption(DesktopPetAssetOption option)
        {
            DesktopPetConfig.Instance.SelectedPetId = option.Asset.Id;
            ConfigHandler.GetInstance().Save<DesktopPetConfig>();
            foreach (var item in _viewModel.Assets)
                item.IsSelected = ReferenceEquals(item, option);

            DesktopPetService.GetInstance().ReloadSelectedAsset();
        }

        private static async Task LoadThumbnailAsync(DesktopPetAssetOption option)
        {
            try
            {
                if (!option.Asset.IsSpriteSheet)
                {
                    var image = new BitmapImage();
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.UriSource = new Uri(option.Asset.StaticImageUri!, UriKind.Absolute);
                    image.EndInit();
                    image.Freeze();
                    option.Thumbnail = image;
                    return;
                }

                option.Thumbnail = await Task.Run(() =>
                {
                    using var spriteSheet = DesktopPetSpriteSheet.Load(
                        option.Asset.ReadSpriteSheetBytes(),
                        option.Asset.SpriteVersionNumber);
                    return spriteSheet.GetFrame(0, 0);
                });
            }
            catch (Exception ex)
            {
                option.CanSelect = false;
                option.Diagnostic = $"素材不可用：{ex.Message}";
            }
        }
    }

    public sealed class DesktopPetSettingsViewModel
    {
        public ObservableCollection<DesktopPetAssetOption> Assets { get; } = new();

        public DesktopPetConfig Config => DesktopPetConfig.Instance;
    }

    public sealed class DesktopPetAssetOption : ViewModelBase
    {
        public DesktopPetAssetOption(DesktopPetAsset asset)
        {
            Asset = asset;
        }

        public DesktopPetAsset Asset { get; }

        public string DisplayName => Asset.DisplayName;

        public string Description => Asset.Description;

        public string SourceLabel => Asset.SourceLabel;

        public ImageSource? Thumbnail { get => _thumbnail; set { _thumbnail = value; OnPropertyChanged(); } }
        private ImageSource? _thumbnail;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value)
                    return;

                _isSelected = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectButtonText));
                OnPropertyChanged(nameof(CanSelect));
            }
        }
        private bool _isSelected;

        public bool CanSelect
        {
            get => _canSelect && !IsSelected;
            set
            {
                if (_canSelect == value)
                    return;

                _canSelect = value;
                OnPropertyChanged();
            }
        }
        private bool _canSelect = true;

        public string SelectButtonText => IsSelected ? "已选择" : "选择";

        public string Diagnostic { get => _diagnostic; set { _diagnostic = value; OnPropertyChanged(); } }
        private string _diagnostic = string.Empty;
    }
}
