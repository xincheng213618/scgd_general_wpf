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
        public Type SelectedEditorType { get; private set; }

        public List<FolderEditorTypeViewModel> EditorTypes { get; private set; }

        public string FolderPath { get; set; }

        public FolderEditorSelectionWindow(List<Type> types, Type? currentType, string folderPath)
        {
            InitializeComponent();
            this.ApplyCaption();
            EditorTypes = types.Select(t => new FolderEditorTypeViewModel(t)).ToList();
            ListEditorSelection.ItemsSource = EditorTypes;
            ListEditorSelection.SelectedIndex = currentType != null ? types.IndexOf(currentType) : 0;
            FolderPath = folderPath;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (ListEditorSelection.SelectedIndex >= 0)
            {
                SelectedEditorType = EditorTypes[ListEditorSelection.SelectedIndex].Type;
                this.DialogResult = true;
            }
            this.Close();
        }

        private void ListEditorSelection_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (ListEditorSelection.SelectedItem is FolderEditorTypeViewModel selected)
            {
                if (Activator.CreateInstance(selected.Type) is IEditor editor)
                {
                    editor.Open(FolderPath);
                }
                this.DialogResult = false;
                this.Close();
            }
        }
    }
}
