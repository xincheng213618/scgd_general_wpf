using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ColorVision.Solution.Editor
{
    /// <summary>
    /// EditorSelectionWindow.xaml 的交互逻辑
    /// </summary>
    public partial class EditorSelectionWindow : Window
    {
        public Type SelectedEditorType { get; private set; }

        public EditorSelectionWindow(List<Type> types, Type currentType)
        {
            InitializeComponent();
            EditorComboBox.ItemsSource = types;
            EditorComboBox.SelectedItem = currentType;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedEditorType = EditorComboBox.SelectedItem as Type;
            this.DialogResult = true;
            this.Close();
        }
    }
}
