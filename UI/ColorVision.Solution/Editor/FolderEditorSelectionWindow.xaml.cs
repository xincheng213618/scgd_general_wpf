using ColorVision.Themes;
using System.Windows;

namespace ColorVision.Solution.Editor
{
    public class FolderEditorTypeViewModel
    {
        public string Name { get; set; }
        public Type Type { get; set; }

        public FolderEditorTypeViewModel(Type type)
        {
            Type = type;
            Name = EditorManager.GetEditorName(type);
        }
        public override string ToString() => Name;
    }

    /// <summary>
    /// FolderEditorSelectionWindow.xaml 的交互逻辑
    /// </summary>
    public partial class FolderEditorSelectionWindow : Window
    {
        public Type? SelectedEditorType { get; private set; }

        public bool AlwaysUseSelectedEditor => AlwaysUseCheckBox.IsChecked == true;

        public List<FolderEditorTypeViewModel> EditorTypes { get; private set; }

        public FolderEditorSelectionWindow(List<Type> types, Type? currentType, string folderPath)
        {
            InitializeComponent();
            this.ApplyCaption();
            EditorTypes = types.Select(t => new FolderEditorTypeViewModel(t)).ToList();
            ListEditorSelection.ItemsSource = EditorTypes;
            int selectedIndex = currentType == null ? -1 : types.IndexOf(currentType);
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
            if (ListEditorSelection.SelectedItem is not FolderEditorTypeViewModel selected)
                return;

            SelectedEditorType = selected.Type;
            DialogResult = true;
            Close();
        }
    }
}
