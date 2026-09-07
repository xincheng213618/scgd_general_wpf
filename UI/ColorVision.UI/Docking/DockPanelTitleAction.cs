using System.Globalization;
using System.Windows.Data;
using System.Windows.Input;

namespace ColorVision.UI.Docking;

/// <summary>Describes one command shown alongside the native AvalonDock pane-title commands.</summary>
public sealed record DockPanelTitleAction(ICommand Command, string Glyph, string ToolTip, object? CommandParameter = null);

/// <summary>Implemented by dock panel content that contributes commands to its pane title.</summary>
public interface IDockPanelTitleActionProvider
{
    IReadOnlyList<DockPanelTitleAction> TitleActions { get; }
}

public sealed class DockPanelTitleActionsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is IDockPanelTitleActionProvider provider
            ? provider.TitleActions
            : Array.Empty<DockPanelTitleAction>();

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
