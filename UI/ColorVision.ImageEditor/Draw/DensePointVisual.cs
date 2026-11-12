using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.Draw;

/// <summary>A static detection layer. Viewport changes only transform its cached tiles.</summary>
public sealed class DensePointVisual : DrawingVisual
{
    private const int TileSize = 512;
    private const int MaxCachedTileDimension = 1024;
    private readonly Point[] centers;
    private readonly Point[] candidates;

    public int PointCount => centers.Length + candidates.Length;
    public int RenderedPointCount => PointCount;
    internal DrawingGroup FrozenDrawing { get; }
    internal int TileCount => Children.Count;

    // scale remains in this released constructor for binary compatibility. Static
    // geometry and line widths are now image pixels, independent of the initial zoom.
    public DensePointVisual(IEnumerable<Point> centers, IEnumerable<Point> candidates, double radius, double scale = 1, Brush? normalBrush = null, Brush? defectBrush = null)
    {
        ArgumentNullException.ThrowIfNull(centers);
        ArgumentNullException.ThrowIfNull(candidates);
        if (!double.IsFinite(radius) || radius <= 0) throw new ArgumentOutOfRangeException(nameof(radius));
        this.centers = centers.ToArray();
        this.candidates = candidates.ToArray();
        if (this.centers.Concat(this.candidates).Any(point => !double.IsFinite(point.X) || !double.IsFinite(point.Y)))
            throw new ArgumentException("点坐标必须是有限数值。");
        FrozenDrawing = new DrawingGroup();
        AddTiles(this.centers, normalBrush ?? Brushes.Red, radius);
        AddTiles(this.candidates, defectBrush ?? Brushes.Yellow, radius);
        FrozenDrawing.Freeze();
    }

    // Previously compiled callers may still invoke this method. There is no
    // viewport-dependent work: the layer no longer implements the scale interface.
    public void ApplyLayoutScale(DrawingVisualScaleContext context)
    {
    }

    protected override HitTestResult? HitTestCore(PointHitTestParameters hitTestParameters) => null;
    protected override GeometryHitTestResult? HitTestCore(GeometryHitTestParameters hitTestParameters) => null;

    private void AddTiles(Point[] points, Brush color, double radius)
    {
        var tiles = new Dictionary<(long X, long Y), List<Point>>();
        foreach (Point point in points)
        {
            var key = ((long)Math.Floor(point.X / TileSize), (long)Math.Floor(point.Y / TileSize));
            if (!tiles.TryGetValue(key, out List<Point>? tile)) tiles[key] = tile = new();
            tile.Add(point);
        }
        var pen = new Pen(color.IsFrozen ? color : color.CloneCurrentValue(), 1);
        pen.Freeze();
        foreach (var (key, pointsInTile) in tiles)
        {
            double x = key.X * (double)TileSize, y = key.Y * (double)TileSize;
            var geometry = new StreamGeometry();
            using (StreamGeometryContext path = geometry.Open())
            {
                foreach (Point point in pointsInTile)
                {
                    Point center = new(point.X - x, point.Y - y);
                    path.BeginFigure(new Point(center.X + radius, center.Y), false, true);
                    path.ArcTo(new Point(center.X - radius, center.Y), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, false);
                    path.ArcTo(new Point(center.X + radius, center.Y), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, false);
                }
            }
            geometry.Freeze();
            var drawing = new DrawingGroup { Transform = new TranslateTransform(x, y) };
            drawing.Children.Add(new GeometryDrawing(null, pen, geometry));
            drawing.Freeze();
            FrozenDrawing.Children.Add(drawing);
            var visual = new MarkerTileVisual();
            using (DrawingContext context = visual.RenderOpen()) DrawCachedTile(context, drawing);
            RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.LowQuality);
            Children.Add(visual);
        }
    }

    private static void DrawCachedTile(DrawingContext context, DrawingGroup drawing)
    {
        Rect bounds = drawing.Bounds;
        double left = Math.Floor(bounds.Left), top = Math.Floor(bounds.Top);
        double width = Math.Ceiling(bounds.Right) - left, height = Math.Ceiling(bounds.Bottom) - top;
        // A very large user-selected radius must not allocate an unbounded bitmap.
        // Its frozen vector drawing is still static and remains available to exports.
        if (width <= 0 || height <= 0 || width > MaxCachedTileDimension || height > MaxCachedTileDimension)
        {
            context.DrawDrawing(drawing);
            return;
        }
        var source = new DrawingVisual();
        using (DrawingContext raster = source.RenderOpen())
        {
            raster.PushTransform(new TranslateTransform(-left, -top));
            raster.DrawDrawing(drawing);
            raster.Pop();
        }
        var bitmap = new RenderTargetBitmap((int)width, (int)height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(source);
        bitmap.Freeze();
        context.DrawImage(bitmap, new Rect(left, top, width, height));
    }

    private sealed class MarkerTileVisual : DrawingVisual
    {
        protected override HitTestResult? HitTestCore(PointHitTestParameters hitTestParameters) => null;
        protected override GeometryHitTestResult? HitTestCore(GeometryHitTestParameters hitTestParameters) => null;
    }
}
