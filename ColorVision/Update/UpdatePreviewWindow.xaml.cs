using ColorVision.Common.MVVM;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using ColorVision.Themes;

namespace ColorVision.Update
{
    public enum UpdatePreviewItemKind
    {
        Other = 0,
        Application = 1,
        ApplicationIncremental = 2,
        Plugin = 3,
        Theme = 4,
    }

    public static class UpdatePreviewText
    {
        private static string Get(string key, string fallback)
        {
            return global::ColorVision.Properties.Resources.ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? fallback;
        }

        private static string Format(string key, string fallback, params object[] args)
        {
            return string.Format(CultureInfo.CurrentCulture, Get(key, fallback), args);
        }

        public static string WindowTitle => Get("CheckForUpdates", "检查更新");
        public static string CheckingHeading => Get("UpdatePreviewCheckingHeading", "正在检查更新");
        public static string CheckingSummary => Get("UpdatePreviewCheckingSummary", "正在获取主程序、插件和主题的最新版本信息，请稍候。");
        public static string ScanningTitle => Get("UpdatePreviewScanningTitle", "正在扫描可用更新项");
        public static string NoUpdatesTitle => Get("UpdatePreviewNoUpdatesTitle", "当前没有可用更新");
        public static string NoUpdatesMessage => Get("UpdatePreviewNoUpdatesMessage", "当前主程序、插件和主题均无需更新。");
        public static string FoundUpdatesHeading => Get("UpdatePreviewFoundUpdatesHeading", "发现更新");
        public static string AlreadyLatestHeading => Get("UpdatePreviewAlreadyLatestHeading", "已是最新版本");
        public static string UpdateNowButtonText => Get("UpdatePreviewUpdateNowButtonText", "立即更新");
        public static string UpdatingButtonText => Get("UpdatePreviewUpdatingButtonText", "正在更新...");
        public static string LaterButtonText => Get("UpdatePreviewLaterButtonText", "稍后");
        public static string CancelButtonText => Get("UpdatePreviewCancelButtonText", "取消");
        public static string CloseButtonText => Get("UpdatePreviewCloseButtonText", "关闭");
        public static string SkipVersionButtonText => Get("UpdatePreviewSkipVersionButtonText", "跳过此版本");
        public static string RequiredBadge => Get("UpdatePreviewRequiredBadge", "必选");
        public static string UnknownVersion => Get("UpdatePreviewUnknownVersion", "未知");
        public static string ApplicationUpdateCategory => Get("UpdatePreviewApplicationUpdateCategory", "主程序更新");
        public static string ApplicationIncrementalCategory => Get("UpdatePreviewApplicationIncrementalCategory", "主程序增量");
        public static string PluginUpdateCategory => Get("UpdatePreviewPluginUpdateCategory", "插件更新");
        public static string ApplicationFullPackageLabel => Get("UpdatePreviewApplicationFullPackageLabel", "完整主程序安装包");
        public static string UpdateKindApplication => Get("UpdatePreviewUpdateKindApplication", "主程序");
        public static string UpdateKindPlugin => Get("UpdatePreviewUpdateKindPlugin", "插件");
        public static string NoInstallableUpdatesMessage => Get("UpdatePreviewNoInstallableUpdatesMessage", "当前未发现需要安装的更新项。");
        public static string PackageDownloadFailed => Get("UpdatePreviewPackageDownloadFailed", "更新包下载失败，请稍后重试。");
        public static string PluginDownloadFailed => Get("UpdatePreviewPluginDownloadFailed", "插件下载未成功完成，请稍后重试。");
        public static string NoPluginUpdates => Get("UpdatePreviewNoPluginUpdates", "没有可更新的插件。");
        public static string CombinedPackageIncomplete => Get("UpdatePreviewCombinedPackageIncomplete", "联合更新包下载不完整，请稍后重试。");
        public static string ShowNoUpdatesLatestMessage => Get("UpdatePreviewShowNoUpdatesLatestMessage", "当前主程序和插件都已经是最新版本。");
        public static string DialogSummaryNoUpdates => Get("UpdatePreviewDialogSummaryNoUpdates", "当前主程序、插件和主题均无需更新。");
        public static string ApplicationCardSummaryFull => Get("UpdatePreviewApplicationCardSummaryFull", "将下载完整安装包并沿用当前主程序更新流程。");
        public static string SelectionIncludesApplication => Get("UpdatePreviewSelectionIncludesApplication", "包含主程序更新");
        public static string SelectionIncludesRequired => Get("UpdatePreviewSelectionIncludesRequired", "包含必选更新");
        public static string SelectionRestartRequired => Get("UpdatePreviewSelectionRestartRequired", "更新完成后将重启应用");
        public static string SelectionBackupAndRestart => Get("UpdatePreviewSelectionBackupAndRestart", "更新前会自动创建备份，完成后可能需要重启应用");

        public static string ListSeparator
        {
            get
            {
                string language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
                return language == "zh" || language == "ja" ? "、" : ", ";
            }
        }

        public static string HostRequirement(string value) => Format("UpdatePreviewHostRequirementFormat", "宿主要求：{0}", value);
        public static string HostVersion(string value) => Format("UpdatePreviewHostVersionFormat", "主程序版本 {0}", value);
        public static string SkippedIncompatibleUpdates(string value) => Format("UpdatePreviewSkippedIncompatibleUpdatesFormat", "以下更新因兼容性要求未显示：{0}", value);
        public static string ApplicationIncrementalPackages(int count) => Format("UpdatePreviewApplicationIncrementalPackagesFormat", "{0} 个增量更新包", count);
        public static string ApplicationCardSummaryIncremental(int count) => Format("UpdatePreviewApplicationCardSummaryIncrementalFormat", "将应用 {0} 个主程序增量包，并与所选更新一起完成本轮更新。", count);
        public static string DialogSummaryWithKinds(int count, string kinds) => Format("UpdatePreviewDialogSummaryWithKinds", "发现 {0} 个可用更新，包含{1}。", count, kinds);
        public static string DialogSummaryDefault(int count) => Format("UpdatePreviewDialogSummaryDefault", "发现 {0} 个可用更新，可按需选择后立即安装。", count);
        public static string DialogSummarySkippedCount(int count) => Format("UpdatePreviewDialogSummarySkippedCount", "另有 {0} 个更新因兼容性要求未显示。", count);
        public static string HeaderApplicationCount(int count) => Format("UpdatePreviewHeaderApplicationCount", "{0} 个主程序更新", count);
        public static string HeaderPluginCount(int count) => Format("UpdatePreviewHeaderPluginCount", "{0} 个插件更新", count);
        public static string HeaderThemeCount(int count) => Format("UpdatePreviewHeaderThemeCount", "{0} 个主题更新", count);
        public static string HeaderOtherCount(int count) => Format("UpdatePreviewHeaderOtherCount", "{0} 个其他更新", count);
        public static string SelectionSelectedPlugins(int selected, int total) => Format("UpdatePreviewSelectionSelectedPluginsFormat", "已选择 {0} / {1} 个插件", selected, total);
        public static string SelectionSelectedUpdates(int selected, int total) => Format("UpdatePreviewSelectionSelectedUpdatesFormat", "已选择 {0} / {1} 个可选更新", selected, total);
        public static string ShowNoUpdatesSkippedMessage(string value) => Format("UpdatePreviewShowNoUpdatesSkippedMessage", "当前没有可执行的联合更新。以下插件因兼容性要求被跳过：{0}", value);
    }

    public enum UpdatePreviewAction
    {
        None = 0,
        UpdateNow = 1,
        SkipVersion = 2,
    }

    public class UpdatePreviewItem : ViewModelBase
    {
        public string ItemId { get; set; } = string.Empty;
        public UpdatePreviewItemKind Kind { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string SecondaryLabel { get; set; } = string.Empty;
        public string CurrentVersion { get; set; } = string.Empty;
        public string TargetVersion { get; set; } = string.Empty;
        public string HostRequirement { get; set; } = string.Empty;
        public string VersionSummary { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string DetailText { get; set; } = string.Empty;
        public ObservableCollection<UpdatePreviewFact> Facts { get; } = new();

        public bool IsSelectable
        {
            get => _isSelectable;
            set
            {
                SetProperty(ref _isSelectable, value);
                OnPropertyChanged(nameof(SelectionVisibility));
                OnPropertyChanged(nameof(RequiredTagVisibility));
            }
        }
        private bool _isSelectable = true;

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
        private bool _isSelected = true;

        public bool IsUpdating
        {
            get => _isUpdating;
            set
            {
                SetProperty(ref _isUpdating, value);
                OnPropertyChanged(nameof(ProgressVisibility));
            }
        }
        private bool _isUpdating;

        public double ProgressValue
        {
            get => _progressValue;
            set => SetProperty(ref _progressValue, value);
        }
        private double _progressValue;

        public string ProgressText
        {
            get => _progressText;
            set => SetProperty(ref _progressText, value);
        }
        private string _progressText = string.Empty;

        public Visibility SelectionVisibility => IsSelectable
            ? Visibility.Visible
            : Visibility.Collapsed;

        public Visibility RequiredTagVisibility => IsSelectable
            ? Visibility.Collapsed
            : Visibility.Visible;

        public Visibility SecondaryLabelVisibility => !string.IsNullOrWhiteSpace(SecondaryLabel)
            && !string.Equals(SecondaryLabel, Name, StringComparison.OrdinalIgnoreCase)
                ? Visibility.Visible
                : Visibility.Collapsed;

        public string VersionTransitionText => $"{FormatVersion(CurrentVersion)}  →  {FormatVersion(TargetVersion)}";

        public string HostRequirementText => HasMeaningfulHostRequirement(HostRequirement)
            ? UpdatePreviewText.HostRequirement(HostRequirement.Trim())
            : string.Empty;

        public Visibility HostRequirementVisibility => HasMeaningfulHostRequirement(HostRequirement)
            ? Visibility.Visible
            : Visibility.Collapsed;

        public Visibility ProgressVisibility => IsUpdating
            ? Visibility.Visible
            : Visibility.Collapsed;

        private static string FormatVersion(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? UpdatePreviewText.UnknownVersion : value.Trim();
        }

        private static bool HasMeaningfulHostRequirement(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            string normalized = value.Trim();
            return !string.Equals(normalized, "未指定", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(normalized, "unknown", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(normalized, "未知", StringComparison.OrdinalIgnoreCase);
        }
    }

    public class UpdatePreviewFact
    {
        public string Label { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }

    public class UpdatePreviewDialogContext : ViewModelBase
    {
        public const double StandardWindowWidth = 920d;
        public const double StandardWindowHeight = 580d;
        public const double StandardWindowMinWidth = 860d;
        public const double StandardWindowMinHeight = 520d;

        public UpdatePreviewDialogContext()
        {
            _itemsViewSource = new CollectionViewSource { Source = Items };
            _itemsViewSource.GroupDescriptions.Add(new PropertyGroupDescription(nameof(UpdatePreviewItem.Category)));

            Items.CollectionChanged += Items_CollectionChanged;
        }

        private readonly CollectionViewSource _itemsViewSource;

        private void Items_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (UpdatePreviewItem item in e.OldItems.OfType<UpdatePreviewItem>())
                {
                    item.PropertyChanged -= Item_PropertyChanged;
                }
            }

            if (e.NewItems != null)
            {
                foreach (UpdatePreviewItem item in e.NewItems.OfType<UpdatePreviewItem>())
                {
                    item.PropertyChanged += Item_PropertyChanged;
                }
            }

            RefreshItemsState();
        }

        private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(UpdatePreviewItem.IsSelected) || e.PropertyName == nameof(UpdatePreviewItem.IsSelectable))
            {
                RefreshSelectionState();
            }
        }

        private void RefreshItemsState()
        {
            OnPropertyChanged(nameof(ApplicationUpdateCount));
            OnPropertyChanged(nameof(PluginUpdateCount));
            OnPropertyChanged(nameof(ThemeUpdateCount));
            OnPropertyChanged(nameof(HeaderSummaryText));
            OnPropertyChanged(nameof(ItemsTitle));
            OnPropertyChanged(nameof(ItemsSummary));
            OnPropertyChanged(nameof(ItemsView));
            OnPropertyChanged(nameof(ItemsEmptyVisibility));
            OnPropertyChanged(nameof(ItemsListVisibility));
            OnPropertyChanged(nameof(ItemsViewportMaxHeight));
            OnPropertyChanged(nameof(EmptyStateCenteredVisibility));
            OnPropertyChanged(nameof(ConfirmButtonVisibility));
            OnPropertyChanged(nameof(FooterInfoVisibility));
            RefreshSelectionState();
        }

        private void RefreshSelectionState()
        {
            OnPropertyChanged(nameof(SelectableItemCount));
            OnPropertyChanged(nameof(SelectedSelectableItemCount));
            OnPropertyChanged(nameof(SelectionSummary));
            OnPropertyChanged(nameof(SelectionSummaryVisibility));
            OnPropertyChanged(nameof(HasSelectableItems));
            OnPropertyChanged(nameof(HasAlwaysIncludedItems));
            OnPropertyChanged(nameof(HasApplicationUpdates));
            OnPropertyChanged(nameof(AreAllSelectableItemsPlugins));
            OnPropertyChanged(nameof(CanConfirm));
            OnPropertyChanged(nameof(FooterInfoVisibility));
        }

        public string WindowTitle { get => _windowTitle; set { _windowTitle = value; OnPropertyChanged(); } }
        private string _windowTitle = UpdatePreviewText.WindowTitle;

        public double WindowWidth { get => _windowWidth; set { _windowWidth = value; OnPropertyChanged(); } }
        private double _windowWidth = StandardWindowWidth;

        public double WindowHeight { get => _windowHeight; set { _windowHeight = value; OnPropertyChanged(); } }
        private double _windowHeight = StandardWindowHeight;

        public double WindowMinWidth { get => _windowMinWidth; set { _windowMinWidth = value; OnPropertyChanged(); } }
        private double _windowMinWidth = StandardWindowMinWidth;

        public double WindowMinHeight { get => _windowMinHeight; set { _windowMinHeight = value; OnPropertyChanged(); } }
        private double _windowMinHeight = StandardWindowMinHeight;

        public double WindowMaxHeight { get => _windowMaxHeight; set { _windowMaxHeight = value; OnPropertyChanged(); } }
        private double _windowMaxHeight = StandardWindowHeight;

        public bool WindowAutoSizeHeight { get => _windowAutoSizeHeight; set { _windowAutoSizeHeight = value; OnPropertyChanged(); } }
        private bool _windowAutoSizeHeight;

        public string Heading { get => _heading; set { _heading = value; OnPropertyChanged(); } }
        private string _heading = UpdatePreviewText.CheckingHeading;

        public string Summary
        {
            get => _summary;
            set
            {
                _summary = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HeaderSummaryText));
            }
        }
        private string _summary = UpdatePreviewText.CheckingSummary;

        public string CheckingTitle { get => _checkingTitle; set { _checkingTitle = value; OnPropertyChanged(); } }
        private string _checkingTitle = UpdatePreviewText.ScanningTitle;

        public string CheckingSummary { get => _checkingSummary; set { _checkingSummary = value; OnPropertyChanged(); } }
        private string _checkingSummary = UpdatePreviewText.CheckingSummary;

        public string EmptyStateTitle { get => _emptyStateTitle; set { _emptyStateTitle = value; OnPropertyChanged(); } }
        private string _emptyStateTitle = UpdatePreviewText.NoUpdatesTitle;

        public string EmptyStateMessage { get => _emptyStateMessage; set { _emptyStateMessage = value; OnPropertyChanged(); } }
        private string _emptyStateMessage = UpdatePreviewText.NoUpdatesMessage;

        public string StateGlyph { get => _stateGlyph; set { _stateGlyph = value; OnPropertyChanged(); } }
        private string _stateGlyph = "\uE895";

        public bool IsChecking
        {
            get => _isChecking;
            set
            {
                _isChecking = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CheckingVisibility));
                OnPropertyChanged(nameof(ItemsEmptyVisibility));
                OnPropertyChanged(nameof(ItemsListVisibility));
                OnPropertyChanged(nameof(EmptyStateCenteredVisibility));
                OnPropertyChanged(nameof(CanConfirm));
                OnPropertyChanged(nameof(ConfirmButtonVisibility));
                OnPropertyChanged(nameof(FooterInfoVisibility));
                OnPropertyChanged(nameof(HeaderSummaryText));
                OnPropertyChanged(nameof(SecondaryButtonVisibility));
            }
        }
        private bool _isChecking;

        public Visibility CheckingVisibility => IsChecking
            ? Visibility.Visible
            : Visibility.Collapsed;

        public Visibility EmptyStateCenteredVisibility => !IsChecking && Items.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;

        public string HostVersionText
        {
            get => _hostVersionText;
            set
            {
                _hostVersionText = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HostVersionLabelText));
                OnPropertyChanged(nameof(HostVersionValueText));
                OnPropertyChanged(nameof(HostVersionVisibility));
            }
        }
        private string _hostVersionText = string.Empty;

        public Visibility HostVersionVisibility => !string.IsNullOrWhiteSpace(HostVersionText)
            ? Visibility.Visible
            : Visibility.Collapsed;

        public string HostVersionLabelText
        {
            get
            {
                if (string.IsNullOrWhiteSpace(HostVersionText))
                    return string.Empty;

                int splitIndex = HostVersionText.LastIndexOf(' ');
                return splitIndex <= 0 ? HostVersionText : HostVersionText[..splitIndex];
            }
        }

        public string HostVersionValueText
        {
            get
            {
                if (string.IsNullOrWhiteSpace(HostVersionText))
                    return string.Empty;

                int splitIndex = HostVersionText.LastIndexOf(' ');
                return splitIndex <= 0 || splitIndex >= HostVersionText.Length - 1
                    ? string.Empty
                    : HostVersionText[(splitIndex + 1)..];
            }
        }

        public string ConfirmButtonText
        {
            get => IsUpdating ? UpdatePreviewText.UpdatingButtonText : _confirmButtonBaseText;
            set
            {
                _confirmButtonBaseText = value;
                OnPropertyChanged();
            }
        }
        private string _confirmButtonBaseText = UpdatePreviewText.UpdateNowButtonText;

        public bool IsUpdating
        {
            get => _isUpdating;
            set
            {
                _isUpdating = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ConfirmButtonText));
                OnPropertyChanged(nameof(CanConfirm));
                OnPropertyChanged(nameof(CanCancel));
                OnPropertyChanged(nameof(SecondaryButtonVisibility));
            }
        }
        private bool _isUpdating;

        public string CancelButtonText { get => _cancelButtonText; set { _cancelButtonText = value; OnPropertyChanged(); } }
        private string _cancelButtonText = UpdatePreviewText.LaterButtonText;

        public string? SecondaryButtonText
        {
            get => _secondaryButtonText;
            set
            {
                _secondaryButtonText = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SecondaryButtonVisibility));
            }
        }
        private string? _secondaryButtonText;

        public Visibility SecondaryButtonVisibility => IsChecking || IsUpdating || string.IsNullOrWhiteSpace(SecondaryButtonText)
            ? Visibility.Collapsed
            : Visibility.Visible;

        public ObservableCollection<UpdatePreviewItem> Items { get; } = new();

        public ICollectionView ItemsView => _itemsViewSource.View;

        public int SelectableItemCount => Items.Count(item => item.IsSelectable);

        public int SelectedSelectableItemCount => Items.Count(item => item.IsSelectable && item.IsSelected);

        public bool HasSelectableItems => SelectableItemCount > 0;

        public bool HasAlwaysIncludedItems => Items.Any(item => !item.IsSelectable);

        public int ApplicationUpdateCount => Items.Count(IsApplicationUpdate);

        public int PluginUpdateCount => Items.Count(item => item.Kind == UpdatePreviewItemKind.Plugin);

        public int ThemeUpdateCount => Items.Count(item => item.Kind == UpdatePreviewItemKind.Theme);

        public bool HasApplicationUpdates => ApplicationUpdateCount > 0;

        public bool AreAllSelectableItemsPlugins => !HasSelectableItems
            || Items.Where(item => item.IsSelectable)
                .All(item => item.Kind == UpdatePreviewItemKind.Plugin);

        public string HeaderSummaryText
        {
            get
            {
                if (IsChecking || Items.Count == 0)
                    return Summary;

                List<string> segments = new();

                if (ApplicationUpdateCount > 0)
                    segments.Add(UpdatePreviewText.HeaderApplicationCount(ApplicationUpdateCount));

                if (PluginUpdateCount > 0)
                    segments.Add(UpdatePreviewText.HeaderPluginCount(PluginUpdateCount));

                if (ThemeUpdateCount > 0)
                    segments.Add(UpdatePreviewText.HeaderThemeCount(ThemeUpdateCount));

                int otherCount = Items.Count - ApplicationUpdateCount - PluginUpdateCount - ThemeUpdateCount;
                if (otherCount > 0)
                    segments.Add(UpdatePreviewText.HeaderOtherCount(otherCount));

                return segments.Count == 0
                    ? UpdatePreviewText.DialogSummaryDefault(Items.Count)
                    : UpdatePreviewText.DialogSummaryWithKinds(Items.Count, string.Join(UpdatePreviewText.ListSeparator, segments));
            }
        }

        public string SelectionSummary
        {
            get
            {
                if (IsChecking)
                    return string.Empty;

                List<string> segments = new();

                if (HasApplicationUpdates)
                    segments.Add(UpdatePreviewText.SelectionIncludesApplication);

                if (HasSelectableItems)
                {
                    segments.Add(AreAllSelectableItemsPlugins
                        ? UpdatePreviewText.SelectionSelectedPlugins(SelectedSelectableItemCount, SelectableItemCount)
                        : UpdatePreviewText.SelectionSelectedUpdates(SelectedSelectableItemCount, SelectableItemCount));
                }
                else if (HasAlwaysIncludedItems && !HasApplicationUpdates)
                {
                    segments.Add(UpdatePreviewText.SelectionIncludesRequired);
                }

                if (HasApplicationUpdates)
                    segments.Add(UpdatePreviewText.SelectionRestartRequired);
                else if (HasSelectableItems || HasAlwaysIncludedItems)
                    segments.Add(UpdatePreviewText.SelectionBackupAndRestart);

                return string.Join(" · ", segments);
            }
        }

        public Visibility SelectionSummaryVisibility => !string.IsNullOrWhiteSpace(SelectionSummary)
            ? Visibility.Visible
            : Visibility.Collapsed;

        public Visibility FooterInfoVisibility => !IsChecking && SelectionSummaryVisibility == Visibility.Visible
            ? Visibility.Visible
            : Visibility.Collapsed;

        public Visibility ConfirmButtonVisibility => !IsChecking && Items.Count > 0
            ? Visibility.Visible
            : Visibility.Collapsed;

        public bool CanConfirm => !IsUpdating && (HasSelectableItems
            && !IsChecking
            ? SelectedSelectableItemCount > 0 || HasAlwaysIncludedItems
            : !IsChecking && Items.Count > 0);

        public bool CanCancel => !IsUpdating;

        public string ItemsTitle => $"待更新内容 ({Items.Count})";

        public Visibility ItemsEmptyVisibility => Visibility.Collapsed;

        public Visibility ItemsListVisibility => !IsChecking && Items.Count > 0
            ? Visibility.Visible
            : Visibility.Collapsed;

        public double ItemsViewportMaxHeight => Items.Count > 4 ? 360d : double.PositiveInfinity;

        public string ItemsSummary
        {
            get
            {
                int hostCount = Items.Count(IsApplicationUpdate);
                int pluginCount = Items.Count(item => item.Kind == UpdatePreviewItemKind.Plugin);

                if (hostCount > 0 && pluginCount > 0)
                    return $"包含 {hostCount} 个主体更新项和 {pluginCount} 个插件更新项。";

                if (hostCount > 0)
                    return hostCount == 1 ? "本次仅更新主体。" : $"包含 {hostCount} 个主体更新项。";

                if (pluginCount > 0)
                    return $"本次有 {pluginCount} 个插件可更新。";

                return "当前没有可展示的更新项。";
            }
        }

        public UpdatePreviewItem? SelectedItem
        {
            get => _selectedItem;
            set
            {
                _selectedItem = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedItemTitle));
                OnPropertyChanged(nameof(SelectedItemSummary));
                OnPropertyChanged(nameof(SelectedFacts));
                OnPropertyChanged(nameof(SelectedFactsVisibility));
                OnPropertyChanged(nameof(SelectedItemMetaText));
                OnPropertyChanged(nameof(SelectedItemDetailIntro));
                SelectedDetailText = value?.DetailText ?? "请选择左侧更新项查看详细说明。";
            }
        }
        private UpdatePreviewItem? _selectedItem;

        public string SelectedItemTitle => SelectedItem == null
            ? "更新详情"
            : $"{SelectedItem.Name}  {SelectedItem.VersionSummary}";

        public string SelectedItemSummary => SelectedItem == null
            ? "左侧选择一个更新项后，这里会展示本项的执行方式、影响范围和主要变化。"
            : SelectedItem.Summary;

        public IEnumerable<UpdatePreviewFact>? SelectedFacts => SelectedItem?.Facts;

        public Visibility SelectedFactsVisibility => SelectedItem?.Facts.Count > 0
            ? Visibility.Visible
            : Visibility.Collapsed;

        public string SelectedItemMetaText => SelectedItem == null
            ? "左侧选择一个更新项后，这里会显示版本变化、执行方式和更新说明。"
            : string.Join("  ·  ", new[]
            {
                SelectedItem.Category,
                SelectedItem.SecondaryLabel,
            }.Where(text => !string.IsNullOrWhiteSpace(text)));

        public string SelectedItemDetailIntro => SelectedItem == null
            ? "右侧用于查看当前选中更新项的详细说明。"
            : "右侧用于查看当前选中更新项的版本变化、执行方式和更新说明。";

        public string SelectedDetailText { get => _selectedDetailText; private set { _selectedDetailText = value; OnPropertyChanged(); } }
        private string _selectedDetailText = "请选择左侧更新项查看详细说明。";

        public void ApplyStandardWindowMetrics()
        {
            WindowWidth = StandardWindowWidth;
            WindowHeight = StandardWindowHeight;
            WindowMinWidth = StandardWindowMinWidth;
            WindowMinHeight = StandardWindowMinHeight;
            WindowMaxHeight = StandardWindowHeight;
            WindowAutoSizeHeight = false;
        }

        public void CopyFrom(UpdatePreviewDialogContext source)
        {
            WindowTitle = source.WindowTitle;
            Heading = source.Heading;
            Summary = source.Summary;
            CheckingTitle = source.CheckingTitle;
            CheckingSummary = source.CheckingSummary;
            EmptyStateTitle = source.EmptyStateTitle;
            EmptyStateMessage = source.EmptyStateMessage;
            StateGlyph = source.StateGlyph;
            HostVersionText = source.HostVersionText;
            ConfirmButtonText = source._confirmButtonBaseText;
            CancelButtonText = source.CancelButtonText;
            SecondaryButtonText = source.SecondaryButtonText;
            IsUpdating = source.IsUpdating;
            IsChecking = source.IsChecking;

            Items.Clear();
            foreach (UpdatePreviewItem item in source.Items)
            {
                Items.Add(item);
            }

            ApplyStandardWindowMetrics();
            SelectedItem = source.SelectedItem ?? Items.FirstOrDefault();
        }

        private static bool IsApplicationUpdate(UpdatePreviewItem item)
        {
            return item.Kind == UpdatePreviewItemKind.Application
                || item.Kind == UpdatePreviewItemKind.ApplicationIncremental
                || item.ItemId == "application";
        }
    }

    public partial class UpdatePreviewWindow
    {
        private readonly Func<UpdatePreviewWindow, Task>? _initializeAsync;
        private bool _hasInitialized;

        public UpdatePreviewAction ResultAction { get; private set; } = UpdatePreviewAction.None;

        public UpdatePreviewDialogContext Context { get; }

        public Task InitializationTask { get; private set; } = Task.CompletedTask;

        public bool IsClosed { get; private set; }

        public bool SuppressPostCheckMessage { get; private set; }

        public UpdatePreviewWindow(UpdatePreviewDialogContext context, Func<UpdatePreviewWindow, Task>? initializeAsync = null)
        {
            InitializeComponent();
            this.ApplyCaption();
            Context = context;
            Context.ApplyStandardWindowMetrics();
            DataContext = Context;
            _initializeAsync = initializeAsync;
            Context.PropertyChanged += Context_PropertyChanged;
            ApplyWindowPresentation();

            ContentRendered += UpdatePreviewWindow_ContentRendered;
            Closing += (_, _) =>
            {
                if (Context.IsChecking)
                {
                    SuppressPostCheckMessage = true;
                }
            };
            Closed += (_, _) =>
            {
                IsClosed = true;
                Context.PropertyChanged -= Context_PropertyChanged;
            };

            if (Context.SelectedItem == null && Context.Items.Count > 0)
            {
                Context.SelectedItem = Context.Items[0];
            }
        }

        private void Context_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(UpdatePreviewDialogContext.WindowTitle))
            {
                ApplyWindowPresentation();
            }
        }

        private void ApplyWindowPresentation()
        {
            Title = string.IsNullOrWhiteSpace(Context.WindowTitle) ? UpdatePreviewText.WindowTitle : Context.WindowTitle;
            MinWidth = UpdatePreviewDialogContext.StandardWindowMinWidth;
            MinHeight = UpdatePreviewDialogContext.StandardWindowMinHeight;
            MaxWidth = UpdatePreviewDialogContext.StandardWindowWidth;
            MaxHeight = UpdatePreviewDialogContext.StandardWindowHeight;
            Width = UpdatePreviewDialogContext.StandardWindowWidth;
            Height = UpdatePreviewDialogContext.StandardWindowHeight;
            ResizeMode = ResizeMode.NoResize;
            SizeToContent = SizeToContent.Manual;
        }

        private async void UpdatePreviewWindow_ContentRendered(object? sender, EventArgs e)
        {
            if (_hasInitialized || _initializeAsync == null)
                return;

            _hasInitialized = true;
            InitializationTask = _initializeAsync(this);

            try
            {
                await InitializationTask;
            }
            catch
            {
                if (!IsClosed)
                {
                    DialogResult = false;
                }
            }
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            if (!Context.CanConfirm)
                return;

            ResultAction = UpdatePreviewAction.UpdateNow;
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (!Context.CanCancel)
                return;

            if (Context.IsChecking)
            {
                SuppressPostCheckMessage = true;
            }

            ResultAction = UpdatePreviewAction.None;
            DialogResult = false;
        }

        private void SecondaryButton_Click(object sender, RoutedEventArgs e)
        {
            ResultAction = UpdatePreviewAction.SkipVersion;
            DialogResult = false;
        }
    }
}