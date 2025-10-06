using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Solution.V
{
    public partial class TreeViewTextbox : ResourceDictionary
    {

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb && tb.Tag is VObject baseObject)
                baseObject.IsEditMode = false;
        }

        private void PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is TextBox tb && tb.Tag is VObject baseObject)
            {
                baseObject.Name = tb.Text;
                if (e.Key == Key.Escape || e.Key == Key.Enter)
                {
                    baseObject.IsEditMode = false;
                    e.Handled = true;
                }
            }
        }
        private void TextBox_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                textBox.SelectAll();
                textBox.Focus(); // 可选，确保焦点在 TextBox 上
            }
        }
        private void TreeViewItem_Initialized(object sender, EventArgs e)
        {
            if (sender is StackPanel stackPanel && stackPanel.Parent is Grid grid)
            {
                grid.ContextMenu = stackPanel.ContextMenu;
            }
        }
    }
}
