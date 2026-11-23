using System.Windows;
using System.Windows.Controls;
using FormsCursor = System.Windows.Forms.Cursor;

namespace ColorVision.Solution.Themes;

/// <summary>Follows AvalonDock's drop preview while the dragged window owns the mouse.</summary>
public sealed class DockingGuideCanvas : Canvas
{
    public static readonly DependencyProperty HasPreviewProperty = DependencyProperty.Register(
        nameof(HasPreview), typeof(bool), typeof(DockingGuideCanvas), new PropertyMetadata(false, OnPreviewChanged));

    public bool HasPreview
    {
        get => (bool)GetValue(HasPreviewProperty);
        set => SetValue(HasPreviewProperty, value);
    }

    private DockingGuideCell? activeCell;

    private static void OnPreviewChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var canvas = (DockingGuideCanvas)sender;
        if (!(bool)e.NewValue || PresentationSource.FromVisual(canvas) == null)
        {
            canvas.SetActiveCell(null);
            return;
        }

        // The native move loop captures the mouse, so the overlay does not receive MouseEnter.
        // Sample once when AvalonDock opens the preview, in this overlay's DPI coordinates.
        var cursor = FormsCursor.Position;
        canvas.UpdateActiveCell(canvas.PointFromScreen(new Point(cursor.X, cursor.Y)));
    }

    internal void UpdateActiveCell(Point position) => SetActiveCell(FindCell(this, position));

    private void SetActiveCell(DockingGuideCell? cell)
    {
        if (ReferenceEquals(activeCell, cell)) return;
        if (activeCell != null) activeCell.IsHighlighted = false;
        activeCell = cell;
        if (cell != null) cell.IsHighlighted = true;
    }

    private static DockingGuideCell? FindCell(Panel panel, Point position)
    {
        // Use the named target's full bounds, including the margin around its painted icon.
        // Tab headers and pane titles can also produce a preview, without highlighting a guide.
        foreach (UIElement child in panel.Children)
        {
            if (!child.IsVisible) continue;
            var local = panel.TranslatePoint(position, child);
            if (child is DockingGuideCell cell && new Rect(cell.RenderSize).Contains(local)) return cell;
            if (child is Panel nested && FindCell(nested, local) is { } match) return match;
        }
        return null;
    }
}

/// <summary>A single native guide hit area and its highlighted appearance.</summary>
public sealed class DockingGuideCell : Control
{
    public static readonly DependencyProperty SideProperty = DependencyProperty.Register(
        nameof(Side), typeof(Dock?), typeof(DockingGuideCell), new PropertyMetadata(null));

    public Dock? Side
    {
        get => (Dock?)GetValue(SideProperty);
        set => SetValue(SideProperty, value);
    }

    public static readonly DependencyProperty IsHighlightedProperty = DependencyProperty.Register(
        nameof(IsHighlighted), typeof(bool), typeof(DockingGuideCell), new PropertyMetadata(false));

    public bool IsHighlighted
    {
        get => (bool)GetValue(IsHighlightedProperty);
        internal set => SetValue(IsHighlightedProperty, value);
    }
}
