using AvalonDock.Layout;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace ColorVision.Themes;

/// <summary>
/// Keeps the selected document tab inside the visible overflow window without
/// changing the document order in the AvalonDock layout model.
/// </summary>
public sealed class StableDocumentPaneTabPanel : AvalonDock.Controls.DocumentPaneTabPanel
{
    private static readonly DependencyPropertyKey IsSelectedTabAtStartPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(IsSelectedTabAtStart), typeof(bool), typeof(StableDocumentPaneTabPanel), new PropertyMetadata(false));

    public static readonly DependencyProperty IsSelectedTabAtStartProperty = IsSelectedTabAtStartPropertyKey.DependencyProperty;

    public bool IsSelectedTabAtStart => (bool)GetValue(IsSelectedTabAtStartProperty);

    private int _firstVisibleIndex;

    public StableDocumentPaneTabPanel()
    {
        ClipToBounds = true;
        AddHandler(Selector.SelectedEvent, new RoutedEventHandler(OnSelectionChanged));
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double desiredWidth = 0;
        double desiredHeight = 0;
        foreach (FrameworkElement child in InternalChildren)
        {
            if (child.Visibility == Visibility.Collapsed)
                continue;

            child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            desiredWidth += child.DesiredSize.Width;
            desiredHeight = Math.Max(desiredHeight, child.DesiredSize.Height);
        }

        return new Size(Math.Min(desiredWidth, availableSize.Width), desiredHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        TabItem[] tabs = InternalChildren.OfType<TabItem>()
            .Where(tab => tab.Visibility != Visibility.Collapsed)
            .ToArray();
        if (tabs.Length == 0)
        {
            _firstVisibleIndex = 0;
            SetValue(IsSelectedTabAtStartPropertyKey, false);
            return finalSize;
        }

        double availableWidth = Math.Max(0, finalSize.Width);
        double[] widths = tabs.Select(tab => tab.DesiredSize.Width).ToArray();
        int selectedIndex = Array.FindIndex(tabs, IsSelectedDocument);
        if (selectedIndex < 0)
            selectedIndex = Math.Clamp(_firstVisibleIndex, 0, tabs.Length - 1);

        if (widths.Sum() <= availableWidth)
        {
            _firstVisibleIndex = 0;
        }
        else
        {
            _firstVisibleIndex = Math.Clamp(_firstVisibleIndex, 0, selectedIndex);
            double selectedWindowWidth = widths
                .Skip(_firstVisibleIndex)
                .Take(selectedIndex - _firstVisibleIndex + 1)
                .Sum();
            while (_firstVisibleIndex < selectedIndex && selectedWindowWidth > availableWidth)
            {
                selectedWindowWidth -= widths[_firstVisibleIndex];
                _firstVisibleIndex++;
            }

            while (_firstVisibleIndex > 0 && selectedWindowWidth + widths[_firstVisibleIndex - 1] <= availableWidth)
            {
                _firstVisibleIndex--;
                selectedWindowWidth += widths[_firstVisibleIndex];
            }
        }

        double offset = 0;
        bool overflowed = false;
        bool selectedTabAtStart = false;
        for (int index = 0; index < tabs.Length; index++)
        {
            TabItem tab = tabs[index];
            double width = widths[index];
            bool beforeWindow = index < _firstVisibleIndex;
            bool exceedsWindow = offset > 0 && offset + width > availableWidth;
            if (beforeWindow || overflowed || exceedsWindow)
            {
                tab.Visibility = Visibility.Hidden;
                overflowed |= !beforeWindow;
                continue;
            }

            tab.Visibility = Visibility.Visible;
            selectedTabAtStart |= offset == 0 && IsSelectedDocument(tab);
            double arrangedWidth = Math.Min(width, Math.Max(0, availableWidth - offset));
            tab.Arrange(new Rect(offset, 0, arrangedWidth, finalSize.Height));
            offset += arrangedWidth;
        }

        // Only the visible selection can join the pane's left edge; overflow may
        // place a later document here without changing the model's item order.
        SetValue(IsSelectedTabAtStartPropertyKey, selectedTabAtStart);
        return finalSize;
    }

    private void OnSelectionChanged(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is TabItem)
            InvalidateArrange();
    }

    private static bool IsSelectedDocument(TabItem tab)
        => tab.IsSelected || tab.Content is LayoutContent { IsSelected: true };
}
