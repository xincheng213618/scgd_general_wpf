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
        public Type SelectedEditorType { get; private set; }

        public List<EditorTypeViewModel> EditorTypes { get; private set; }

        public string FilePath { get; set; }

        public EditorSelectionWindow(List<Type> types, Type currentType,string filepath)
        {
            InitializeComponent();
            this.ApplyCaption();
            string ext = Path.GetExtension(filepath);
            EditorTypes = types.Select(t => new EditorTypeViewModel(t, ext)).ToList();
            ListEditorSelection.ItemsSource = EditorTypes;
            ListEditorSelection.SelectedIndex = types.IndexOf(currentType);
            FilePath = filepath;

        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedEditorType = EditorTypes[ListEditorSelection.SelectedIndex].Type;
            this.DialogResult = true;
            this.Close();
        }

        private void ListEditorSelection_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (ListEditorSelection.SelectedItem is EditorTypeViewModel selected)
            {
                if (Activator.CreateInstance(selected.Type) is IEditor editor)
                {
                    editor.Open(FilePath);
                } 
                this.DialogResult = false;
                this.Close();
            }
        }
    }
}
