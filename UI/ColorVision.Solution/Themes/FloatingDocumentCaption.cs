using AvalonDock.Layout;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ColorVision.Solution.Themes;

/// <summary>Identifies the document represented by a floating window's caption and native title.</summary>
public sealed class FloatingDocumentCaption : IMultiValueConverter
{
    public static readonly DependencyProperty DocumentProperty = DependencyProperty.RegisterAttached(
        "Document", typeof(LayoutContent), typeof(FloatingDocumentCaption));

    public static LayoutContent? GetDocument(DependencyObject element) => (LayoutContent?)element.GetValue(DocumentProperty);
    public static void SetDocument(DependencyObject element, LayoutContent? value) => element.SetValue(DocumentProperty, value);

    public object? Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values[0] is not LayoutDocumentFloatingWindow window) return null;
        if (values[1] is LayoutContent selected) return selected;

        // A split has no SinglePane. The layout root's active document may belong
        // to another window, so retain the most recently used selection in this one.
        if (values[2] is LayoutContent active && active.FindParent<LayoutFloatingWindow>() == window)
            return active;
        return window.RootPanel?.Descendents().OfType<LayoutDocumentPane>()
            .Where(pane => pane.IsVisible)
            .Select(pane => pane.SelectedContent)
            .OfType<LayoutContent>()
            .MaxBy(document => document.LastActivationTimeStamp);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Leaves the title line to the filename when the floating window is narrow.</summary>
public sealed class FloatingDocumentHostPrefix : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        => values is [string host, double width] && width >= 240 && !string.IsNullOrWhiteSpace(host)
            ? host : string.Empty;

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
