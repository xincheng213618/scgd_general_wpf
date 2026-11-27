using ColorVision.Algorithms;
using ColorVision.ImageEditor.Draw;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.Algorithms
{
    /// <summary>WPF adapter for host-neutral Geometry/Overlay artifacts.</summary>
    public static class AlgorithmOverlayRenderer
    {
        /// <summary>Registers an application-rendered overlay with the same source lifetime as neutral overlays.</summary>
        public static IDisposable RegisterVisual(ImageProcessingContext image, AlgorithmOverlayArtifact artifact, DrawingVisual visual)
        {
            ArgumentNullException.ThrowIfNull(image);
            ArgumentNullException.ThrowIfNull(artifact);
            ArgumentNullException.ThrowIfNull(visual);
            return image.TryRegisterAlgorithmOverlay(artifact, visual, image.DocumentInstanceId, image.ImageRevision, out var registration)
                ? registration : new RenderSession([]);
        }

        public static IDisposable Apply(ImageProcessingContext image, DrawEditorContext draw, AlgorithmResult result)
        {
            ArgumentNullException.ThrowIfNull(image);
            ArgumentNullException.ThrowIfNull(draw);
            ArgumentNullException.ThrowIfNull(result);
            AlgorithmOverlayArtifact[] overlays = result.Artifacts.OfType<AlgorithmOverlayArtifact>().ToArray();
            if (overlays.Length == 0) return new RenderSession([]);

            var geometryArtifacts = result.Artifacts.OfType<AlgorithmGeometryArtifact>();
            long geometryCount = geometryArtifacts.Sum(artifact => (long)artifact.Geometries.Count);
            long itemCount = overlays.Sum(overlay => (long)overlay.Items.Count);
            // Large fit results may retain far more measured points than their overlay displays.
            HashSet<string>? visibleIds = geometryCount > itemCount * 2
                ? overlays.SelectMany(overlay => overlay.Items).Select(item => item.GeometryId).ToHashSet(StringComparer.Ordinal)
                : null;
            Dictionary<string, (AlgorithmGeometry Geometry, AlgorithmCoordinateSpace Space)> geometries = new(StringComparer.Ordinal);
            foreach (AlgorithmGeometryArtifact artifact in geometryArtifacts)
            foreach (AlgorithmGeometry geometry in artifact.Geometries)
                if (visibleIds == null || visibleIds.Contains(geometry.Id))
                    geometries[geometry.Id] = (geometry, artifact.CoordinateSpace);

            Guid documentInstanceId = image.DocumentInstanceId;
            long sourceRevision = image.ImageRevision;
            List<IAlgorithmOverlayRegistration> registrations = new();
            try
            {
                foreach (AlgorithmOverlayArtifact overlay in overlays)
                {
                    DrawingVisual visual = new();
                    OverlayPaint paint = default;
                    BitmapSource? bitmap = image.ViewBitmapSource as BitmapSource;
                    double dpiX = bitmap?.DpiX ?? 96;
                    double dpiY = bitmap?.DpiY ?? 96;
                    double zoom = draw.ZoomRatio;
                    double safeZoom = double.IsFinite(zoom) && zoom > 0 ? zoom : 1;
                    using (DrawingContext context = visual.RenderOpen())
                    {
                        foreach (AlgorithmOverlayItem item in overlay.Items)
                        {
                            if (!geometries.TryGetValue(item.GeometryId, out var entry)) continue;
                            DrawGeometry(context, entry.Geometry, entry.Space, item.Style, dpiX, dpiY, safeZoom, ref paint);
                        }
                    }
                    if (image.TryRegisterAlgorithmOverlay(
                        overlay,
                        visual,
                        documentInstanceId,
                        sourceRevision,
                        out IAlgorithmOverlayRegistration? registration))
                    {
                        registrations.Add(registration);
                    }
                }
                return new RenderSession(registrations);
            }
            catch
            {
                foreach (IAlgorithmOverlayRegistration registration in registrations)
                    registration.Remove();
                throw;
            }
        }

        private static void DrawGeometry(
            DrawingContext context,
            AlgorithmGeometry geometry,
            AlgorithmCoordinateSpace space,
            AlgorithmOverlayStyle style,
            double dpiX,
            double dpiY,
            double safeZoom,
            ref OverlayPaint paint)
        {
            IReadOnlyList<AlgorithmPoint> points = geometry.Points;
            int count = points.Count;
            Point first = count == 0 ? default : ToPoint(points[0], space, dpiX, dpiY);
            var (pen, fill) = paint.Get(style, Math.Max(0.25, style.StrokeWidth) / safeZoom);
            switch (geometry.Kind)
            {
                case AlgorithmGeometryKind.Point when count >= 1:
                    double radius = 3 / safeZoom;
                    context.DrawEllipse(fill ?? pen.Brush, pen, first, radius, radius);
                    break;
                case AlgorithmGeometryKind.Line when count >= 2:
                    context.DrawLine(pen, first, ToPoint(points[1], space, dpiX, dpiY));
                    break;
                case AlgorithmGeometryKind.Circle when count >= 1 && geometry.Radius is double sourceRadius:
                    double radiusX = space == AlgorithmCoordinateSpace.Pixel ? sourceRadius : sourceRadius * dpiX / 25.4;
                    double radiusY = space == AlgorithmCoordinateSpace.Pixel ? sourceRadius : sourceRadius * dpiY / 25.4;
                    context.DrawEllipse(fill, pen, first, radiusX, radiusY);
                    break;
                case AlgorithmGeometryKind.Rectangle when count >= 2:
                    context.DrawRectangle(fill, pen, new Rect(first, ToPoint(points[1], space, dpiX, dpiY)));
                    break;
                case AlgorithmGeometryKind.Polygon when count >= 3:
                    context.DrawGeometry(fill, pen, Path(points, first, space, dpiX, dpiY, closed: true));
                    break;
                case AlgorithmGeometryKind.Polyline when count >= 2:
                    context.DrawGeometry(null, pen, Path(points, first, space, dpiX, dpiY, closed: false));
                    break;
            }
            if (!string.IsNullOrWhiteSpace(style.Label) && count > 0)
            {
                FormattedText text = new(
                    style.Label,
                    CultureInfo.CurrentUICulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"),
                    11 / safeZoom,
                    pen.Brush,
                    1);
                context.DrawText(text, first);
            }
        }

        private static PathGeometry Path(IReadOnlyList<AlgorithmPoint> points, Point first, AlgorithmCoordinateSpace space, double dpiX, double dpiY, bool closed)
        {
            Point[] vertices = new Point[points.Count - 1];
            for (int index = 1; index < points.Count; index++)
                vertices[index - 1] = ToPoint(points[index], space, dpiX, dpiY);
            PathFigure figure = new() { StartPoint = first, IsClosed = closed, IsFilled = closed };
            figure.Segments.Add(new PolyLineSegment(vertices, true));
            return new PathGeometry([figure]);
        }

        private static Point ToPoint(AlgorithmPoint point, AlgorithmCoordinateSpace space, double dpiX, double dpiY)
        {
            AlgorithmPoint pixel = AlgorithmCoordinates.ToPixel(point, space, dpiX, dpiY);
            return new Point(pixel.X, pixel.Y);
        }

        private static Brush ParseBrush(string value, Brush fallback)
        {
            try
            {
                if (ColorConverter.ConvertFromString(value) is Color color)
                {
                    SolidColorBrush brush = new(color);
                    brush.Freeze();
                    return brush;
                }
            }
            catch (FormatException) { }
            return fallback;
        }

        // Consecutive markers often use the same paint. Keep a single entry per overlay.
        private struct OverlayPaint
        {
            private (string Stroke, string? Fill, double Width) _key;
            private Pen? _pen;
            private Brush? _fill;

            public (Pen Pen, Brush? Fill) Get(AlgorithmOverlayStyle style, double width)
            {
                var key = (style.Stroke, style.Fill, width);
                if (_pen != null && _key == key)
                {
                    if (!_pen.IsFrozen) _pen.Freeze();
                    return (_pen, _fill);
                }

                _pen = new Pen(ParseBrush(style.Stroke, Brushes.Orange), width);
                _fill = string.IsNullOrWhiteSpace(style.Fill) ? null : ParseBrush(style.Fill, Brushes.Transparent);
                _key = key;
                return (_pen, _fill);
            }
        }

        private sealed class RenderSession(IReadOnlyList<IAlgorithmOverlayRegistration> registrations) : IDisposable
        {
            private bool _disposed;

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                foreach (IAlgorithmOverlayRegistration registration in registrations)
                    registration.Dispose();
            }
        }
    }
}
