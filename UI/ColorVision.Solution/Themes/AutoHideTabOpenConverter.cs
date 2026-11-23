using AvalonDock.Layout;
using System.Globalization;
using System.Windows.Data;

namespace ColorVision.Solution.Themes;

public sealed class AutoHideTabOpenConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        // A pane can retain keyboard focus while hovering opens a different flyout.
        // AvalonDock assigns the flyout model before changing its visibility.
        return values is [LayoutAnchorable tool, true] && ReferenceEquals(tool.Root?.Manager?.AutoHideWindow?.Model, tool);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
