using CameraTest.Models;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CameraTest;

public partial class CameraTestWindow
{
    private sealed record RegionIdentity(string Id);
    private ConditionalWeakTable<IDrawingVisual, RegionIdentity> _regionIdentities = new();
    private readonly HashSet<string> _allocatedRegionIds = new(StringComparer.Ordinal);
    private readonly Dictionary<IDrawingVisual, BaseProperties> _regionSubscriptions = new();
    private bool _syncingRegions, _selectingRegion;
    private string? _regionError;

    private void DrawingsChanged(object? sender, NotifyCollectionChangedEventArgs e) => SynchronizeRegionDrawings();
    private void RegionGeometryChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName is nameof(RectangleProperties.Rect) or nameof(RegionProperties.Rotation))
            SynchronizeRegionDrawings();
    }

    private void SynchronizeRegionDrawings()
    {
        if (!_ready || _syncingRegions || _presenting || _closing || _frame == null) return;
        _syncingRegions = true;
        try
        {
            var visuals = ImageView.EditorContext.DrawEditorContext.DrawingVisualLists.Where(v => v is IRectangle).ToArray();
            foreach (var old in _regionSubscriptions.Keys.Except(visuals).ToArray())
            {
                _regionSubscriptions[old].PropertyChanged -= RegionGeometryChanged;
                _regionSubscriptions.Remove(old);
            }
            var bitmap = ImageView.ViewBitmapSource as BitmapSource;
            double scaleX = (bitmap?.DpiX ?? 96) / 96, scaleY = (bitmap?.DpiY ?? 96) / 96;
            var regions = new List<SearchRegion>();
            foreach (var visual in visuals)
            {
                if (!_regionSubscriptions.ContainsKey(visual))
                {
                    _regionSubscriptions.Add(visual, visual.BaseAttribute);
                    visual.BaseAttribute.PropertyChanged += RegionGeometryChanged;
                }
                if (!_regionIdentities.TryGetValue(visual, out var identity))
                {
                    string? preferred = (visual.BaseAttribute as RectangleTextProperties)?.Text;
                    string id = !string.IsNullOrWhiteSpace(preferred) && !_allocatedRegionIds.Contains(preferred)
                        ? preferred : NextRegionId();
                    identity = new(id);
                    _regionIdentities.Add(visual, identity);
                    _allocatedRegionIds.Add(id);
                    if (visual.BaseAttribute is RectangleTextProperties text) text.Text = id;
                }
                // Names are drawn once in the source-scoped presentation layer with readable screen sizing.
                if (visual.BaseAttribute is RectangleTextProperties label && label.IsShowText)
                {
                    label.IsShowText = false;
                    visual.Render();
                }
                Rect bounds = visual is ISelectVisual selectable ? selectable.GetRect() : ((IRectangle)visual).Rect;
                int x = 0, y = 0, width = 0, height = 0;
                if (!bounds.IsEmpty && new[] { bounds.Left, bounds.Top, bounds.Right, bounds.Bottom }.All(double.IsFinite))
                {
                    try
                    {
                        x = checked((int)Math.Floor(bounds.Left * scaleX));
                        y = checked((int)Math.Floor(bounds.Top * scaleY));
                        width = checked((int)Math.Ceiling(bounds.Right * scaleX) - x);
                        height = checked((int)Math.Ceiling(bounds.Bottom * scaleY) - y);
                    }
                    catch (OverflowException) { width = height = 0; }
                }
                regions.Add(new(identity.Id, x, y, width, height));
            }
            if (_profile.Regions.SequenceEqual(regions)) return;
            if (_profile.Regions.Count == 0 || regions.Count == 0)
            {
                _profile.ImageWidth = _frame.Data.Width;
                _profile.ImageHeight = _frame.Data.Height;
            }
            _profile.Regions = regions;
            _regionError = GetRegionError();
            _generation++;
            ResetFocus();
            InvalidateResult();
            RenderOverlays();
            StatusText.Text = _regionError ?? (regions.Count == 0 ? "请添加测量点。" : $"已添加 {regions.Count} 个测量点，可开始分析。");
            Refresh();
        }
        finally { _syncingRegions = false; }
    }

    private string? GetRegionError()
    {
        if (_frame == null || _profile.Regions.Count == 0) return null;
        if (_profile.ImageWidth != _frame.Data.Width || _profile.ImageHeight != _frame.Data.Height)
            return "图像尺寸与配置不一致，请移除旧区域或加载匹配配置。";
        if (_profile.Regions.Count > 64) return "最多支持 64 个搜索区域，请移除多余矩形。";
        var invalid = _profile.Regions.FirstOrDefault(r => r.Width < 40 || r.Height < 40 || r.X < 0 || r.Y < 0
            || (long)r.X + r.Width > _profile.ImageWidth || (long)r.Y + r.Height > _profile.ImageHeight);
        return invalid == null ? null : $"{invalid.Id} 须完整位于图像内，且至少为 40×40 像素。";
    }

    private string NextRegionId()
    {
        string id;
        // Keep deleted identities reserved: undo can restore their drawings later.
        do { id = $"Point_{_nextRegion++}"; } while (_allocatedRegionIds.Contains(id));
        return id;
    }

    private int NextDrawingId() => ImageView.EditorContext.DrawEditorContext.DrawingVisualLists.Select(v => v.BaseAttribute.Id).DefaultIfEmpty(0).Max() + 1;

    private void RestoreRegionDrawings()
    {
        _syncingRegions = true;
        try
        {
            DetachRegionProperties();
            _regionIdentities = new();
            _allocatedRegionIds.Clear();
            var draw = ImageView.EditorContext.DrawEditorContext;
            draw.SelectionVisual.ClearRender();
            foreach (var visual in draw.DrawingVisualLists.Where(v => v is IRectangle).OfType<Visual>().ToArray()) draw.DrawCanvas.RemoveVisual(visual);
            draw.DrawCanvas.ClearActionCommand();
            int number = NextDrawingId();
            foreach (var region in _profile.Regions)
            {
                var visual = new DVRectangleText(new() { Id = number++, Text = region.Id, IsShowText = false, Rect = new(region.X, region.Y, region.Width, region.Height) });
                _regionIdentities.Add(visual, new(region.Id));
                _allocatedRegionIds.Add(region.Id);
                _regionSubscriptions.Add(visual, visual.BaseAttribute);
                visual.BaseAttribute.PropertyChanged += RegionGeometryChanged;
                draw.DrawCanvas.AddVisual(visual);
                visual.Render();
            }
            _regionError = null;
        }
        finally { _syncingRegions = false; }
    }

    private void DetachRegionProperties()
    {
        foreach (var properties in _regionSubscriptions.Values) properties.PropertyChanged -= RegionGeometryChanged;
        _regionSubscriptions.Clear();
    }
}
