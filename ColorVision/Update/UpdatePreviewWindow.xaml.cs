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

        public string VersionTransitionText => $"{FormatVersion(CurrentVersion)}  ->  {FormatVersion(TargetVersion)}";

        public string HostRequirementText => $"宿主要求：{(string.IsNullOrWhiteSpace(HostRequirement) ? "未指定" : HostRequirement)}";

        public Visibility HostRequirementVisibility => string.IsNullOrWhiteSpace(HostRequirement)
            ? Visibility.Collapsed
            : Visibility.Visible;

        public Visibility ProgressVisibility => IsUpdating
            ? Visibility.Visible
            : Visibility.Collapsed;

        private static string FormatVersion(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? "Unknown" : value.Trim();
        }
    }

    public class UpdatePreviewFact
    {
        public string Label { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }

    public class UpdatePreviewDialogContext : ViewModelBase
    {
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
            OnPropertyChanged(nameof(CanConfirm));
            OnPropertyChanged(nameof(FooterInfoVisibility));
        }

        public string WindowTitle { get => _windowTitle; set { _windowTitle = value; OnPropertyChanged(); } }
        private string _windowTitle = "更新预览";

        public string Heading { get => _heading; set { _heading = value; OnPropertyChanged(); } }
        private string _heading = string.Empty;

        public string Summary { get => _summary; set { _summary = value; OnPropertyChanged(); } }
        private string _summary = string.Empty;

        public string CheckingTitle { get => _checkingTitle; set { _checkingTitle = value; OnPropertyChanged(); } }
        private string _checkingTitle = "正在检查更新";

        public string CheckingSummary { get => _checkingSummary; set { _checkingSummary = value; OnPropertyChanged(); } }
        private string _checkingSummary = "正在获取主程序与插件的最新版本信息，请稍候。";

        public string EmptyStateTitle { get => _emptyStateTitle; set { _emptyStateTitle = value; OnPropertyChanged(); } }
        private string _emptyStateTitle = "当前没有可展示的更新项";

        public string EmptyStateMessage { get => _emptyStateMessage; set { _emptyStateMessage = value; OnPropertyChanged(); } }
        private string _emptyStateMessage = "如果这里为空，通常表示本轮检查没有拿到可应用的主体或插件更新。";

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
            }
        }
        private bool _isChecking;

        public Visibility CheckingVisibility => IsChecking
            ? Visibility.Visible
            : Visibility.Collapsed;

        public Visibility EmptyStateCenteredVisibility => !IsChecking && Items.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;

        public string HostVersionText { get => _hostVersionText; set { _hostVersionText = value; OnPropertyChanged(); } }
        private string _hostVersionText = string.Empty;

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

        public Visibility SecondaryButtonVisibility => string.IsNullOrWhiteSpace(SecondaryButtonText)
            ? Visibility.Collapsed
            : Visibility.Visible;

        public ObservableCollection<UpdatePreviewItem> Items { get; } = new();

        public ICollectionView ItemsView => _itemsViewSource.View;

        public int SelectableItemCount => Items.Count(item => item.IsSelectable);

        public int SelectedSelectableItemCount => Items.Count(item => item.IsSelectable && item.IsSelected);

        public bool HasSelectableItems => SelectableItemCount > 0;

        public bool HasAlwaysIncludedItems => Items.Any(item => !item.IsSelectable);

        public bool HasApplicationUpdates => Items.Any(item => item.Category.Contains("主体", StringComparison.OrdinalIgnoreCase));

        public string SelectionSummary
        {
            get
            {
                if (IsChecking)
                    return string.Empty;

                if (HasApplicationUpdates)
                {
                    return HasSelectableItems
                        ? $"已选择 {SelectedSelectableItemCount} / {SelectableItemCount} 个插件 · 包含主体更新，更新完成后将重启应用"
                        : "包含主体更新 · 更新完成后将重启应用并应用所选更新";
                }

                if (HasSelectableItems)
                    return $"已选择 {SelectedSelectableItemCount} / {SelectableItemCount} 个插件 · 更新完成后可能需要重启应用";

                if (HasAlwaysIncludedItems)
                    return "包含必选更新 · 更新完成后可能需要重启应用";

                return string.Empty;
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

        public double ItemsViewportMaxHeight => Items.Count > 4 ? 356d : double.PositiveInfinity;

        public string ItemsSummary
        {
            get
            {
                int hostCount = Items.Count(item => item.Category.Contains("主体"));
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

            SelectedItem = source.SelectedItem ?? Items.FirstOrDefault();
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
            DataContext = Context;
            Title = Context.WindowTitle;
            _initializeAsync = initializeAsync;

            ContentRendered += UpdatePreviewWindow_ContentRendered;
            Closing += (_, _) =>
            {
                if (Context.IsChecking)
                {
                    SuppressPostCheckMessage = true;
                }
            };
            Closed += (_, _) => IsClosed = true;

            if (Context.SelectedItem == null && Context.Items.Count > 0)
            {
                Context.SelectedItem = Context.Items[0];
            }
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