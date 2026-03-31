using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Solution.Explorer
{
    public partial class SolutionTreeViewBehavior : ResourceDictionary
    {

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb && tb.Tag is SolutionNode baseObject)
                baseObject.IsEditMode = false;
        }

        private void PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is TextBox tb && tb.Tag is SolutionNode baseObject)
            {
                if (e.Key == Key.Enter)
                {
                    baseObject.Name = tb.Text;
                    baseObject.IsEditMode = false;
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
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
                textBox.Focus();
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
