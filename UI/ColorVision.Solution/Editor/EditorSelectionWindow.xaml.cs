using ColorVision.Common.NativeMethods;
using ColorVision.Themes;
using System.IO;
using System.Windows;

namespace ColorVision.Solution.Editor
{
    public class EditorTypeViewModel
    {
        public string Name { get; set; }
        public Type Type { get; set; }

        public EditorTypeViewModel(Type type,string ext)
        {
            Type = type;
            Name = EditorManager.GetEditorName(type); // 你已实现的 Name 获取逻辑
            if (type == typeof(SystemEditor))
            {
                string friendlyName = FileAssociation.GetFriendlyAppName(ext);

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
        public Type? SelectedEditorType { get; private set; }

        public bool AlwaysUseSelectedEditor => AlwaysUseCheckBox.IsChecked == true;

        public List<EditorTypeViewModel> EditorTypes { get; private set; }

        public EditorSelectionWindow(List<Type> types, Type? currentType, string filepath)
        {
            InitializeComponent();
            this.ApplyCaption();
            string ext = Path.GetExtension(filepath);
            EditorTypes = types.Select(t => new EditorTypeViewModel(t, ext)).ToList();
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
            if (ListEditorSelection.SelectedItem is not EditorTypeViewModel selected)
                return;

            SelectedEditorType = selected.Type;
            DialogResult = true;
            Close();
        }
    }
}
