using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace ColorVision.UI;

/// <summary>A separate header action; never covers the device settings/status action.</summary>
public partial class DisplayPinButton : ToggleButton
{
    public DisplayPinButton() => InitializeComponent();

    private IDisPlayControl? Owner
    {
        get
        {
            for (DependencyObject? parent = this; parent != null; parent = GetParent(parent))
                if (parent is IDisPlayControl control)
                    return control;
            return null;
        }
    }

    private void Pin_Loaded(object sender, RoutedEventArgs e)
    {
        if (Owner is { } owner)
            IsChecked = DisPlayManager.IsPinned(owner);
    }

    private void Pin_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (Owner is { } owner)
            DisPlayManager.GetInstance().SetPinned(owner, IsChecked == true);
    }

    internal static bool IsPinInput(DependencyObject? source)
    {
        for (; source != null; source = GetParent(source))
            if (source is DisplayPinButton)
                return true;
        return false;
    }

    private static DependencyObject? GetParent(DependencyObject source) => source is Visual
        ? VisualTreeHelper.GetParent(source) ?? LogicalTreeHelper.GetParent(source)
        : LogicalTreeHelper.GetParent(source);
}
