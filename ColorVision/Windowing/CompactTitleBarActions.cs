using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Shell;

namespace ColorVision.Windowing;

/// <summary>Shares the existing action commands and update click path with a compact overflow menu.</summary>
internal static class CompactTitleBarActions
{
    internal static void ConfigureButton(Button button, Style style)
    {
        button.Style = style;
        button.Width = 32;
        button.Height = 28;
        button.Margin = new Thickness(1, 0, 1, 0);
        button.VerticalAlignment = VerticalAlignment.Center;
        WindowChrome.SetIsHitTestVisibleInChrome(button, true);
    }

    internal static ContextMenu CreateMenu(IEnumerable<Button> actionButtons, Button updateNotice, bool hasPendingUpdate)
    {
        var menu = new ContextMenu();
        foreach (Button button in actionButtons)
        {
            var item = new MenuItem { Header = button.ToolTip ?? Properties.Resources.CompactTitleBarMoreActions };
            item.SetBinding(MenuItem.CommandProperty, new Binding(nameof(Button.Command)) { Source = button });
            item.SetBinding(MenuItem.CommandParameterProperty, new Binding(nameof(Button.CommandParameter)) { Source = button });
            item.SetBinding(MenuItem.CommandTargetProperty, new Binding(nameof(Button.CommandTarget)) { Source = button, TargetNullValue = button });
            item.SetBinding(UIElement.IsEnabledProperty, new Binding(nameof(Button.IsEnabled)) { Source = button });
            menu.Items.Add(item);
        }
        if (hasPendingUpdate)
        {
            if (menu.Items.Count > 0)
                menu.Items.Add(new Separator());
            var updateItem = new MenuItem();
            updateItem.SetBinding(HeaderedItemsControl.HeaderProperty, new Binding(nameof(Button.Content)) { Source = updateNotice });
            updateItem.SetBinding(UIElement.IsEnabledProperty, new Binding(nameof(Button.IsEnabled)) { Source = updateNotice });
            updateItem.Click += (_, _) =>
            {
                if (updateNotice.IsEnabled)
                    updateNotice.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, updateNotice));
            };
            menu.Items.Add(updateItem);
        }
        return menu;
    }
}
