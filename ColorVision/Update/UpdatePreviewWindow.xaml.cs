using ColorVision.Common.MVVM;
using ColorVision.Themes;
using System.Collections.ObjectModel;
using System.Windows;

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
        public string VersionSummary { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string DetailText { get; set; } = string.Empty;
    }

    public class UpdatePreviewDialogContext : ViewModelBase
    {
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

        public UpdatePreviewItem? SelectedItem
        {
            get => _selectedItem;
            set
            {
                _selectedItem = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedItemTitle));
                SelectedDetailText = value?.DetailText ?? "请选择左侧更新项查看详细说明。";
            }
        }
        private UpdatePreviewItem? _selectedItem;

        public string SelectedItemTitle => SelectedItem == null
            ? "更新详情"
            : $"{SelectedItem.Name}  {SelectedItem.VersionSummary}";

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