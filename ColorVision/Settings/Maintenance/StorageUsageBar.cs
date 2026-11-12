using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.Settings.Maintenance;

public sealed class StorageUsageBar : FrameworkElement
{
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable<StorageUsageItem>), typeof(StorageUsageBar), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty RefreshKeyProperty = DependencyProperty.Register(nameof(RefreshKey), typeof(int), typeof(StorageUsageBar), new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));
    public IEnumerable<StorageUsageItem>? ItemsSource { get => (IEnumerable<StorageUsageItem>?)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
    public int RefreshKey { get => (int)GetValue(RefreshKeyProperty); set => SetValue(RefreshKeyProperty, value); }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        if (ActualWidth <= 0 || ActualHeight <= 0) return;
        Rect bounds = new(0, 0, ActualWidth, ActualHeight);
        drawingContext.PushClip(new RectangleGeometry(bounds, 7, 7));
        var background = TryFindResource("ButtonBorderBrush") as Brush ?? Brushes.Gray;
        drawingContext.DrawRectangle(background, null, bounds);
        StorageUsageItem[] items = ItemsSource?.Where(item => item.Bytes > 0).ToArray() ?? [];
        double total = items.Sum(item => (double)item.Bytes);
        double left = 0;
        foreach (var item in items)
        {
            double width = ActualWidth * item.Bytes / total;
            drawingContext.DrawRectangle(item.Color, null, new Rect(left, 0, width, ActualHeight));
            left += width;
        }
        drawingContext.Pop();
    }
}
