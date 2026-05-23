using ColorVision.Common.MVVM;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace ColorVision.Update
{
    public enum UpdatePreviewAction
    {
        None = 0,
        UpdateNow = 1,
        SkipVersion = 2,
    }

    public class UpdatePreviewItem : ViewModelBase
    {
        public string ItemId { get; set; } = string.Empty;
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
            ? $"宿主要求：{HostRequirement.Trim()}"
            : string.Empty;

        public Visibility HostRequirementVisibility => HasMeaningfulHostRequirement(HostRequirement)
            ? Visibility.Visible
            : Visibility.Collapsed;

        public Visibility ProgressVisibility => IsUpdating
            ? Visibility.Visible
            : Visibility.Collapsed;

        private static string FormatVersion(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? "Unknown" : value.Trim();
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
        private string _windowTitle = "检查更新";

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
        private string _heading = "正在检查更新";

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
        private string _summary = "正在获取主程序、插件和主题的最新版本信息，请稍候。";

        public string CheckingTitle { get => _checkingTitle; set { _checkingTitle = value; OnPropertyChanged(); } }
        private string _checkingTitle = "正在扫描可用更新项";

        public string CheckingSummary { get => _checkingSummary; set { _checkingSummary = value; OnPropertyChanged(); } }
        private string _checkingSummary = "正在获取主程序、插件和主题的最新版本信息，请稍候。";

        public string EmptyStateTitle { get => _emptyStateTitle; set { _emptyStateTitle = value; OnPropertyChanged(); } }
        private string _emptyStateTitle = "当前没有可用更新";

        public string EmptyStateMessage { get => _emptyStateMessage; set { _emptyStateMessage = value; OnPropertyChanged(); } }
        private string _emptyStateMessage = "当前主程序、插件和主题均无需更新。";

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
            get => IsUpdating ? "正在更新..." : _confirmButtonBaseText;
            set
            {
                _confirmButtonBaseText = value;
                OnPropertyChanged();
            }
        }
        private string _confirmButtonBaseText = "立即更新";

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
        private string _cancelButtonText = "稍后";

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

        public int PluginUpdateCount => Items.Count(item => item.Category.Contains("插件", StringComparison.OrdinalIgnoreCase));

        public int ThemeUpdateCount => Items.Count(item => item.Category.Contains("主题", StringComparison.OrdinalIgnoreCase));

        public bool HasApplicationUpdates => ApplicationUpdateCount > 0;

        public bool AreAllSelectableItemsPlugins => !HasSelectableItems
            || Items.Where(item => item.IsSelectable)
                .All(item => item.Category.Contains("插件", StringComparison.OrdinalIgnoreCase));

        public string HeaderSummaryText
        {
            get
            {
                if (IsChecking || Items.Count == 0)
                    return Summary;

                List<string> segments = new();

                if (ApplicationUpdateCount > 0)
                    segments.Add($"{ApplicationUpdateCount} 个主程序更新");

                if (PluginUpdateCount > 0)
                    segments.Add($"{PluginUpdateCount} 个插件更新");

                if (ThemeUpdateCount > 0)
                    segments.Add($"{ThemeUpdateCount} 个主题更新");

                int otherCount = Items.Count - ApplicationUpdateCount - PluginUpdateCount - ThemeUpdateCount;
                if (otherCount > 0)
                    segments.Add($"{otherCount} 个其他更新");

                return segments.Count == 0
                    ? $"发现 {Items.Count} 个可用更新，可按需选择后立即安装。"
                    : $"发现 {Items.Count} 个可用更新，其中 {string.Join("、", segments)}。";
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
                    segments.Add("包含主程序更新");

                if (HasSelectableItems)
                {
                    string unit = AreAllSelectableItemsPlugins ? "个插件" : "个可选更新";
                    segments.Add($"已选择 {SelectedSelectableItemCount} / {SelectableItemCount} {unit}");
                }
                else if (HasAlwaysIncludedItems && !HasApplicationUpdates)
                {
                    segments.Add("包含必选更新");
                }

                if (HasApplicationUpdates)
                    segments.Add("更新完成后将重启应用");
                else if (HasSelectableItems || HasAlwaysIncludedItems)
                    segments.Add("更新前会自动创建备份，完成后可能需要重启应用");

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
                int hostCount = Items.Count(item => item.Category.Contains("主程序", StringComparison.OrdinalIgnoreCase));
                int pluginCount = Items.Count(item => item.Category == "插件更新");

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
            return item.ItemId == "application"
                || item.Category.Contains("主程序", StringComparison.OrdinalIgnoreCase);
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
            Title = string.IsNullOrWhiteSpace(Context.WindowTitle) ? "检查更新" : Context.WindowTitle;
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