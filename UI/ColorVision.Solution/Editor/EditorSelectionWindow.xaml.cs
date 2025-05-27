using System.Windows;

namespace ColorVision.Solution.Editor
{
    public class EditorTypeViewModel
    {
        public string Name { get; set; }
        public Type Type { get; set; }

        public EditorTypeViewModel(Type type)
        {
            Type = type;
            Name = EditorManager.GetEditorName(type); // 你已实现的 Name 获取逻辑

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

        public EditorSelectionWindow(List<Type> types, Type currentType)
        {
            InitializeComponent();
            EditorTypes = types.Select(t => new EditorTypeViewModel(t)).ToList();
            ListEditorSelection.ItemsSource = EditorTypes;
            ListEditorSelection.SelectedIndex = types.IndexOf(currentType);
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedEditorType = EditorTypes[ListEditorSelection.SelectedIndex].Type;
            this.DialogResult = true;
            this.Close();
        }
    }
}
