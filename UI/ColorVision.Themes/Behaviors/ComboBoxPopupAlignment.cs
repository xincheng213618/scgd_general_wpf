using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;

namespace ColorVision.Themes;

public static class ComboBoxPopupAlignment
{
    public static readonly DependencyProperty AlignRightProperty = DependencyProperty.RegisterAttached(
        "AlignRight", typeof(bool), typeof(ComboBoxPopupAlignment), new PropertyMetadata(false, Changed));
    public static bool GetAlignRight(DependencyObject obj) => (bool)obj.GetValue(AlignRightProperty);
    public static void SetAlignRight(DependencyObject obj, bool value) => obj.SetValue(AlignRightProperty, value);

    private static void Changed(DependencyObject obj, DependencyPropertyChangedEventArgs args)
    {
        if (obj is not ComboBox combo) return;
        combo.DropDownOpened -= Opened;
        if ((bool)args.NewValue) combo.DropDownOpened += Opened;
    }

    private static void Opened(object? sender, EventArgs args)
    {
        if (sender is not ComboBox combo) return;
        combo.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            if (combo.IsDropDownOpen && combo.Template.FindName("PART_Popup", combo) is Popup { Child: FrameworkElement child } popup)
                popup.HorizontalOffset = combo.ActualWidth - child.ActualWidth;
        }));
    }
}
