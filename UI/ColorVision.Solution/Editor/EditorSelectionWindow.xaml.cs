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

        public EditorDescriptorViewModel(EditorDescriptor descriptor, string resourcePath)
        {
            Descriptor = descriptor;
            Name = EditorManager.GetEditorName(descriptor);
            if (descriptor.ResourceKind == EditorResourceKind.File
                && descriptor.EditorType == typeof(SystemEditor))
            {
                string friendlyName = FileAssociation.GetFriendlyAppName(Path.GetExtension(resourcePath));
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
            string resourcePath)
        {
            InitializeComponent();
            this.ApplyCaption();
            bool isFolder = descriptors.Count > 0
                && descriptors[0].ResourceKind == EditorResourceKind.Folder;
            OpenHintText.Text = isFolder
                ? ColorVision.Solution.Properties.Resources.Sol_OpenFolderHint
                : ColorVision.Solution.Properties.Resources.Sol_OpenFileHint;
            AlwaysUseCheckBox.Content = isFolder
                ? "始终使用此编辑器打开文件夹"
                : "始终使用此编辑器打开此类文件";
            EditorTypes = descriptors.Select(descriptor =>
                new EditorDescriptorViewModel(descriptor, resourcePath)).ToList();
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
