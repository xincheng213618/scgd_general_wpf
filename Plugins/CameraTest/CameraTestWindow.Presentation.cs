using CameraTest.Application;
using CameraTest.Models;
using ColorVision.Algorithms;
using ColorVision.Core;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.SFR;
using ColorVision.UI;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CameraTest;

public partial class CameraTestWindow
{
    private double _overlayZoom = double.NaN;
    private ContextMenu? _edgeMenu;

    private void Exit_Click(object sender, RoutedEventArgs e) => Close();
    private void FitImage_Click(object sender, RoutedEventArgs e) => ImageView.UpdateZoomAndScale();

    private async void DisplaySettings_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _live || _closing) return;
        var edited = new BmwSfrViewSettings { Display = _profile.Display.Copy(), MeasurementRoi = _profile.MeasurementRoi with { } };
        var window = new PropertyEditorWindow(edited, PropertyEditorEditMode.Transactional)
        { Owner = this, Title = "BMW 测量与显示", Width = 820, Height = 680, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        bool submitted = false;
        window.Submitted += (_, _) => submitted = true;
        window.ShowDialog();
        if (!submitted) return;
        try { edited.Validate(); }
        catch (ArgumentException error) { StatusText.Text = error.Message; return; }
        bool geometryChanged = edited.MeasurementRoi != _profile.MeasurementRoi;
        _profile.Display = edited.Display;
        _profile.MeasurementRoi = edited.MeasurementRoi;
        if (geometryChanged) { InvalidateResult(); ResetFocus(); Refresh(); }
        RenderOverlays();
        if (geometryChanged && _frame != null && _profile.Regions.Count > 0 && GetRegionError() == null)
            await PerformAsync(() => AnalyzeFrameAsync(_frame, _generation));
    }

    private void RegionList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Geometry synchronization rebuilds the list during dragging; it must not reselect the canvas.
        if (!_ready || _syncingRegions) return;
        RemoveRegionMenuItem.IsEnabled = !_busy && !_live && !_closing && RegionList.SelectedItem != null;
        if (RegionList.SelectedItem is not SearchRegion region) return;
        var draw = ImageView.EditorContext.DrawEditorContext;
        var visual = draw.DrawingVisualLists.FirstOrDefault(v => _regionIdentities.TryGetValue(v, out var identity) && identity.Id == region.Id);
        if (visual is ISelectVisual selected && !_busy && !_live && !ReferenceEquals(draw.SelectionVisual.PrimarySelectedVisual, selected)) draw.SelectionVisual.SetRender(selected);
        var current = Metrics.SelectedItem as MetricRow;
        var rows = Metrics.Items.OfType<MetricRow>().Where(row => row.Target == region.Id).ToArray();
        if (rows.Length > 0) Metrics.SelectedItem = rows.FirstOrDefault(row => row.Edge == current?.Edge && row.Channel == current.Channel) ?? rows[0];
    }

    private void DrawingSelectionChanged(object? sender, EventArgs e)
    {
        if (!_ready || _presenting || _syncingRegions || _closing) return;
        if (ImageView.EditorContext.DrawEditorContext.SelectionVisual.PrimarySelectedVisual is not IDrawingVisual drawing
            || !_regionIdentities.TryGetValue(drawing, out var identity)) return;
        RegionList.SelectedItem = _profile.Regions.FirstOrDefault(r => r.Id == identity.Id);
    }

    private void OverlayZoomChanged(object? sender, EventArgs e)
    {
        if (!_ready || _closing || _presenting) return;
        if (ImageView.EditorContext.DrawEditorContext.ZoomRatio != _overlayZoom) RenderOverlays();
    }

    private static string EdgeName(BmwEdgeId edge) => edge switch { BmwEdgeId.Left => "左", BmwEdgeId.Top => "上", BmwEdgeId.Right => "右", _ => "下" };

    private void RenderOverlays()
    {
        _overlays?.Dispose();
        _overlays = null;
        if (_frame == null || _profile.ImageWidth != _frame.Data.Width || _profile.ImageHeight != _frame.Data.Height) return;
        var draw = ImageView.EditorContext.DrawEditorContext;
        var image = ImageView.EditorContext.ProcessingContext;
        _overlayZoom = draw.ZoomRatio;
        var scope = new ImageSelectionScope(image.DocumentInstanceId, image.ImageRevision, _frame.Data.Width, _frame.Data.Height, 96, 96);
        var selected = Metrics.SelectedItem as MetricRow;
        string channel = selected?.Channel is "R" or "G" or "B" ? selected.Channel : "L";
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
            foreach (var region in _profile.Regions)
            {
                var target = _result?.Targets.FirstOrDefault(t => t.Id == region.Id)
                    ?? new BmwTargetAnalysis(region.Id, region.Roi, false, "", default, 0, 0, []);
                BmwEdgeId? selectedEdge = target.Id == selected?.Target && Enum.TryParse<BmwEdgeId>(selected.Edge, out var edge) ? edge : null;
                dc.DrawDrawing(BmwSfrOverlayRenderer.CreateVisual(target, scope, _overlayZoom, channel, _profile.Display, selectedEdge, false).Drawing);
            }
        _overlays = AlgorithmOverlayRenderer.RegisterVisual(image,
            new AlgorithmOverlayArtifact("camera-test-overlay", AlgorithmOverlayLifetime.Transient, []), visual);
    }
    private (string Target, BmwEdgeAnalysis Edge)? HitMeasurementEdge(Point point)
    {
        if (_result == null || _result.FrameId != _frame?.Id) return null;
        foreach (var target in _result.Targets)
            foreach (var edge in target.Edges)
                if (edge.Roi.Width > 0 && edge.Roi.Height > 0 && new Rect(edge.Roi.X, edge.Roi.Y, edge.Roi.Width, edge.Roi.Height).Contains(point)) return (target.Id, edge);
        return null;
    }

    private void SelectMeasurementEdge(string target, BmwEdgeId edge)
    {
        RegionList.SelectedItem = _profile.Regions.FirstOrDefault(r => r.Id == target);
        ImageView.EditorContext.DrawEditorContext.SelectionVisual.ClearRender();
        var current = Metrics.SelectedItem as MetricRow;
        var rows = Metrics.Items.OfType<MetricRow>().Where(row => row.Target == target && row.Edge == edge.ToString()).ToArray();
        Metrics.SelectedItem = rows.FirstOrDefault(row => row.Channel == current?.Channel) ?? rows.FirstOrDefault();
        if (Metrics.SelectedItem != null) Metrics.ScrollIntoView(Metrics.SelectedItem);
        var colorPair = (ColorMetrics.SelectedItem as ColorShiftRow)?.Pair;
        ColorMetrics.SelectedItem = ColorMetrics.Items.OfType<ColorShiftRow>().FirstOrDefault(row => row.Target == target && row.Edge == edge.ToString() && row.Pair == colorPair)
            ?? ColorMetrics.Items.OfType<ColorShiftRow>().FirstOrDefault(row => row.Target == target && row.Edge == edge.ToString());
        if (AnalysisTabs.SelectedIndex != 1) AnalysisTabs.SelectedIndex = 0;
        StatusText.Text = $"{target} · {EdgeName(edge)}边；右键可独立分析此矩形。";
        RenderOverlays();
    }

    private void MeasurementEdge_MouseDown(object sender, MouseButtonEventArgs e)
    {
        var draw = ImageView.EditorContext.DrawEditorContext;
        if (e.ChangedButton != MouseButton.Left || _busy || _closing || _selectingRegion || Keyboard.Modifiers != ModifierKeys.None || draw.DrawEditorManager.Current != null) return;
        var hit = HitMeasurementEdge(e.GetPosition(draw.DrawCanvas));
        if (hit == null) return;
        SelectMeasurementEdge(hit.Value.Target, hit.Value.Edge.Id);
        e.Handled = true;
    }

    private void MeasurementEdge_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        var draw = ImageView.EditorContext.DrawEditorContext;
        if (_busy || _closing || _selectingRegion || draw.DrawEditorManager.Current != null) return;
        var hit = HitMeasurementEdge(Mouse.GetPosition(draw.DrawCanvas));
        if (hit == null) return;
        SelectMeasurementEdge(hit.Value.Target, hit.Value.Edge.Id);
        var custom = CreateEdgeMenu(hit.Value.Target, hit.Value.Edge);
        _edgeMenu = ImageView.EditorContext.ContextMenu;
        _edgeMenu.Items.Clear();
        while (custom.Items.Count > 0)
        {
            var item = custom.Items[0];
            custom.Items.RemoveAt(0);
            _edgeMenu.Items.Add(item);
        }
        e.Handled = false;
    }

    private ContextMenu CreateEdgeMenu(string target, BmwEdgeAnalysis edge)
    {
        var rectangle = new DVRectangle(new() { Rect = new(edge.Roi.X, edge.Roi.Y, edge.Roi.Width, edge.Roi.Height) });
        var provider = new SFRIDVContextMenu(ImageView.EditorContext.ProcessingContext, ImageView.Config);
        var menu = new ContextMenu();
        menu.Items.Add(new MenuItem { Header = $"{target} / {EdgeName(edge.Id)}边", IsEnabled = false });
        menu.Items.Add(new Separator());
        foreach (var item in provider.GetContextMenuItems(rectangle))
        {
            item.IsEnabled = !_live;
            item.ToolTip = _live ? "停止实时分析后可独立测量此矩形。" : "使用当前小矩形打开单边 SFR 窗口，无需重新框选。";
            menu.Items.Add(item);
        }
        return menu;
    }

    private void CloseEdgeMenu()
    {
        if (_edgeMenu != null) _edgeMenu.IsOpen = false;
        _edgeMenu = null;
    }
}
