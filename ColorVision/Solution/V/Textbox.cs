#pragma warning disable CA1010
using ColorVision.Solution.V;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Solution
{
    public partial class TreeViewTextbox : ResourceDictionary
    {

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb && tb.Tag is VObject baseObject)
                baseObject.IsEditMode = false;
        }

        private void TextBox_KeyUp(object sender, KeyEventArgs e)
        {
            if (sender is TextBox tb && tb.Tag is VObject baseObject)
            {
                baseObject.Name = tb.Text;
                if (e.Key == Key.Escape || e.Key == Key.Enter)
                {
                    baseObject.IsEditMode = false;
                }
            }
        }
        private void TreeViewItem_Initialized(object sender, EventArgs e) {
            if (sender is StackPanel stackPanel && stackPanel.Parent is Grid grid) {
                grid.ContextMenu = stackPanel.ContextMenu;
            }
        }
    }
}
