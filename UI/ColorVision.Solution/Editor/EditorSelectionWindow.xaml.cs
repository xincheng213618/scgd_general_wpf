using ColorVision.Common.NativeMethods;
using ColorVision.Themes;
using System.IO;
using System.Windows;

namespace ColorVision.Solution.Editor
{
    public class EditorDescriptorViewModel
    {
        public string Name { get; set; }
        public EditorDescriptor Descriptor { get; }

        public EditorDescriptorViewModel(EditorDescriptor descriptor, string extension)
        {
            Descriptor = descriptor;
            Name = EditorManager.GetEditorName(descriptor);
            if (descriptor.EditorType == typeof(SystemEditor))
            {
                string friendlyName = FileAssociation.GetFriendlyAppName(extension);
                Name = $"{Name} ({friendlyName})";
            }
        }
        public override string ToString() => Name;
    }

    /// <summary>
    /// EditorSelectionWindow.xaml 的交互逻辑
    /// </summary>
    public partial class EditorSelectionWindow : Window
    {
        public EditorDescriptor? SelectedEditor { get; private set; }

        public bool AlwaysUseSelectedEditor => AlwaysUseCheckBox.IsChecked == true;

        public List<EditorDescriptorViewModel> EditorTypes { get; private set; }

        public EditorSelectionWindow(
            IReadOnlyList<EditorDescriptor> descriptors,
            string? currentEditorId,
            string filePath)
        {
            InitializeComponent();
            this.ApplyCaption();
            string extension = Path.GetExtension(filePath);
            EditorTypes = descriptors.Select(descriptor =>
                new EditorDescriptorViewModel(descriptor, extension)).ToList();
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
            if (ListEditorSelection.SelectedItem is not EditorDescriptorViewModel selected)
                return;

            SelectedEditor = selected.Descriptor;
            DialogResult = true;
            Close();
        }
    }
}
