using ColorVision.Common.MVVM;
using ColorVision.Themes;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
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

    public class UpdatePreviewItem
    {
        public string Category { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string SecondaryLabel { get; set; } = string.Empty;
        public string VersionSummary { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string DetailText { get; set; } = string.Empty;
        public ObservableCollection<UpdatePreviewFact> Facts { get; } = new();
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

            Items.CollectionChanged += (_, _) =>
            {
                OnPropertyChanged(nameof(ItemsTitle));
                OnPropertyChanged(nameof(ItemsSummary));
                OnPropertyChanged(nameof(ItemsView));
                OnPropertyChanged(nameof(ItemsEmptyVisibility));
                OnPropertyChanged(nameof(ItemsListVisibility));
            };
        }

        private readonly CollectionViewSource _itemsViewSource;

        public string WindowTitle { get => _windowTitle; set { _windowTitle = value; OnPropertyChanged(); } }
        private string _windowTitle = "更新预览";

        public string Heading { get => _heading; set { _heading = value; OnPropertyChanged(); } }
        private string _heading = string.Empty;

        public string Summary { get => _summary; set { _summary = value; OnPropertyChanged(); } }
        private string _summary = string.Empty;

        public string ConfirmButtonText { get => _confirmButtonText; set { _confirmButtonText = value; OnPropertyChanged(); } }
        private string _confirmButtonText = "立即更新";

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

        public string ItemsTitle => $"待更新内容 ({Items.Count})";

        public Visibility ItemsEmptyVisibility => Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        public Visibility ItemsListVisibility => Items.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

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
    }

    public partial class UpdatePreviewWindow : Window
    {
        public UpdatePreviewAction ResultAction { get; private set; } = UpdatePreviewAction.None;

        public UpdatePreviewDialogContext Context { get; }

        public UpdatePreviewWindow(UpdatePreviewDialogContext context)
        {
            InitializeComponent();
            this.ApplyCaption();

            Context = context;
            DataContext = Context;
            Title = Context.WindowTitle;

            if (Context.SelectedItem == null && Context.Items.Count > 0)
            {
                Context.SelectedItem = Context.Items[0];
            }
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            ResultAction = UpdatePreviewAction.UpdateNow;
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
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