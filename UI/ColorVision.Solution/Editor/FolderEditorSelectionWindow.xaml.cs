using ColorVision.Themes;
using System.Windows;

namespace ColorVision.Solution.Editor
{
    public class FolderEditorDescriptorViewModel
    {
        public string Name { get; set; }
        public EditorDescriptor Descriptor { get; }

        public FolderEditorDescriptorViewModel(EditorDescriptor descriptor)
        {
            Descriptor = descriptor;
            Name = EditorManager.GetEditorName(descriptor);
        }
        public override string ToString() => Name;
    }

    /// <summary>
    /// FolderEditorSelectionWindow.xaml 的交互逻辑
    /// </summary>
    public partial class FolderEditorSelectionWindow : Window
    {
        public EditorDescriptor? SelectedEditor { get; private set; }

        public bool AlwaysUseSelectedEditor => AlwaysUseCheckBox.IsChecked == true;

        public List<FolderEditorDescriptorViewModel> EditorTypes { get; private set; }

        public FolderEditorSelectionWindow(
            IReadOnlyList<EditorDescriptor> descriptors,
            string? currentEditorId,
            string folderPath)
        {
            InitializeComponent();
            this.ApplyCaption();
            EditorTypes = descriptors.Select(descriptor =>
                new FolderEditorDescriptorViewModel(descriptor)).ToList();
            ListEditorSelection.ItemsSource = EditorTypes;
            int selectedIndex = currentEditorId == null
                ? -1
                : EditorTypes.FindIndex(item => string.Equals(
                    item.Descriptor.Id,
                    currentEditorId,
                    StringComparison.OrdinalIgnoreCase));
            ListEditorSelection.SelectedIndex = selectedIndex >= 0 ? selectedIndex : 0;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            ConfirmSelection();
        }

        private void ListEditorSelection_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            ConfirmSelection();
        }

        private void ConfirmSelection()
        {
            if (ListEditorSelection.SelectedItem is not FolderEditorDescriptorViewModel selected)
                return;

            SelectedEditor = selected.Descriptor;
            DialogResult = true;
            Close();
        }
    }
}
