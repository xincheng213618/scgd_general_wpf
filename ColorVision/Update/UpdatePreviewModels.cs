using ColorVision.Common.MVVM;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;

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
        public string Summary { get; set; } = string.Empty;

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

    public class UpdatePreviewDialogContext : ViewModelBase
    {
        public UpdatePreviewDialogContext()
        {
            Items.CollectionChanged += Items_CollectionChanged;
        }

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
            OnPropertyChanged(nameof(ItemsListVisibility));
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

        public string HostVersionLabel
        {
            get => _hostVersionLabel;
            set
            {
                _hostVersionLabel = value;
                OnPropertyChanged();
            }
        }
        private string _hostVersionLabel = UpdatePreviewText.HostVersionLabel;

        public string HostVersionValue
        {
            get => _hostVersionValue;
            set
            {
                _hostVersionValue = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HostVersionVisibility));
            }
        }
        private string _hostVersionValue = string.Empty;

        public Visibility HostVersionVisibility => !string.IsNullOrWhiteSpace(HostVersionValue)
            ? Visibility.Visible
            : Visibility.Collapsed;

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

                Collection<string> segments = new();

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

                Collection<string> segments = new();

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

        public bool CanConfirm => !IsUpdating
            && !IsChecking
            && (HasSelectableItems ? SelectedSelectableItemCount > 0 || HasAlwaysIncludedItems : Items.Count > 0);

        public bool CanCancel => !IsUpdating;

        public Visibility ItemsListVisibility => !IsChecking && Items.Count > 0
            ? Visibility.Visible
            : Visibility.Collapsed;

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
            HostVersionLabel = source.HostVersionLabel;
            HostVersionValue = source.HostVersionValue;
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
        }

        private static bool IsApplicationUpdate(UpdatePreviewItem item)
        {
            return item.Kind == UpdatePreviewItemKind.Application
                || item.Kind == UpdatePreviewItemKind.ApplicationIncremental
                || item.ItemId == "application";
        }
    }
}