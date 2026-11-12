using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Solution.MultiImageViewer;

internal sealed class ThumbnailViewportListBox : ListBox
{
    public event Action<ImageFileInfo>? ItemRealized;
    public event Action<ImageFileInfo>? ItemUnrealized;

    protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
    {
        base.PrepareContainerForItemOverride(element, item);
        if (item is ImageFileInfo file) ItemRealized?.Invoke(file);
    }

    protected override void ClearContainerForItemOverride(DependencyObject element, object item)
    {
        base.ClearContainerForItemOverride(element, item);
        if (item is ImageFileInfo file) ItemUnrealized?.Invoke(file);
    }
}
