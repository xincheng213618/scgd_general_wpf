using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Windowing;

/// <summary>Measures optional actions without consuming the native caption or explicit drag region.</summary>
internal static class CompactTitleBarLayout
{
    internal static void SetOpticalAlignment(FrameworkElement element, bool compact,
        Thickness ordinaryMargin, VerticalAlignment ordinaryVerticalAlignment)
    {
        element.SetCurrentValue(FrameworkElement.VerticalAlignmentProperty,
            compact ? VerticalAlignment.Center : ordinaryVerticalAlignment);
        // Centering a box with 4 DIP additional top margin lowers its visible center by 2 DIP.
        // Apply the same optical offset to every text-bearing title-bar element.
        element.SetCurrentValue(FrameworkElement.MarginProperty, compact
            ? new Thickness(ordinaryMargin.Left, ordinaryMargin.Top + 4, ordinaryMargin.Right, ordinaryMargin.Bottom)
            : ordinaryMargin);
    }

    internal static void Update(FrameworkElement titleBar, Menu menu, StackPanel rightActions,
        FrameworkElement updateNotice, FrameworkElement dragRegion,
        FrameworkElement overflowButton, bool hasPendingUpdate)
    {
        // Always measure the natural widths, not the current collapsed/allocated widths.
        // Update availability is model state: a collapsed notice must be able to return
        // when the window grows, without waiting for another update notification.
        double fixedWidth = MeasureNaturalWidth(menu) + MeasureNaturalWidth(dragRegion);
        double actionsWidth = MeasureNaturalWidth(rightActions);
        double updateWidth = hasPendingUpdate ? MeasureNaturalWidth(updateNotice) : 0;
        bool showAllActions = titleBar.ActualWidth >= fixedWidth + actionsWidth + updateWidth;
        bool showUpdateNotice = hasPendingUpdate;
        if (!showAllActions)
        {
            // Even when menus must clip at an extremely narrow width, keep the
            // overflow entry and drag strip; the host owns command and badge content.
            double overflowWidth = System.Math.Max(32, MeasureNaturalWidth(overflowButton));
            showUpdateNotice = hasPendingUpdate && titleBar.ActualWidth >= fixedWidth + overflowWidth + updateWidth;
        }

        rightActions.SetCurrentValue(UIElement.VisibilityProperty, showAllActions ? Visibility.Visible : Visibility.Collapsed);
        updateNotice.SetCurrentValue(UIElement.VisibilityProperty, showUpdateNotice ? Visibility.Visible : Visibility.Collapsed);
        overflowButton.SetCurrentValue(UIElement.VisibilityProperty, showAllActions ? Visibility.Collapsed : Visibility.Visible);
    }

    private static double MeasureNaturalWidth(FrameworkElement element)
    {
        Visibility originalVisibility = element.Visibility;
        if (originalVisibility == Visibility.Collapsed)
            element.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Hidden);
        try
        {
            element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            return element.DesiredSize.Width;
        }
        finally
        {
            if (originalVisibility == Visibility.Collapsed)
                element.SetCurrentValue(UIElement.VisibilityProperty, originalVisibility);
        }
    }
}
