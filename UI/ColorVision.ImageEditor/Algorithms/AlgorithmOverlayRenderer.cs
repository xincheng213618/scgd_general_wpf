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
        public static IDisposable Apply(ImageProcessingContext image, DrawEditorContext draw, AlgorithmResult result)
        {
            ArgumentNullException.ThrowIfNull(image);
            ArgumentNullException.ThrowIfNull(draw);
            ArgumentNullException.ThrowIfNull(result);
            Dictionary<string, (AlgorithmGeometry Geometry, AlgorithmCoordinateSpace Space)> geometries = result.Artifacts
                .OfType<AlgorithmGeometryArtifact>()
                .SelectMany(artifact => artifact.Geometries.Select(geometry => (geometry.Id, geometry, artifact.CoordinateSpace)))
                .GroupBy(item => item.Id, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => (group.Last().geometry, group.Last().CoordinateSpace), StringComparer.Ordinal);
            Guid documentInstanceId = image.DocumentInstanceId;
            long sourceRevision = image.ImageRevision;
            List<IAlgorithmOverlayRegistration> registrations = new();
            try
            {
                foreach (AlgorithmOverlayArtifact overlay in result.Artifacts.OfType<AlgorithmOverlayArtifact>())
                {
                    DrawingVisual visual = new();
                    using (DrawingContext context = visual.RenderOpen())
                    {
                        foreach (AlgorithmOverlayItem item in overlay.Items)
                        {
                            if (!geometries.TryGetValue(item.GeometryId, out var entry)) continue;
                            DrawGeometry(context, entry.Geometry, entry.Space, item.Style, image, draw.ZoomRatio);
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
            ImageProcessingContext image,
            double zoom)
        {
            double dpiX = image.ViewBitmapSource is BitmapSource bitmap ? bitmap.DpiX : 96;
            double dpiY = image.ViewBitmapSource is BitmapSource bitmapY ? bitmapY.DpiY : 96;
            AlgorithmPoint[] points = geometry.Points.Select(point => AlgorithmCoordinates.ToPixel(point, space, dpiX, dpiY)).ToArray();
            double safeZoom = double.IsFinite(zoom) && zoom > 0 ? zoom : 1;
            Pen pen = new(ParseBrush(style.Stroke, Brushes.Orange), Math.Max(0.25, style.StrokeWidth) / safeZoom);
            Brush? fill = string.IsNullOrWhiteSpace(style.Fill) ? null : ParseBrush(style.Fill, Brushes.Transparent);
            switch (geometry.Kind)
            {
                case AlgorithmGeometryKind.Point when points.Length >= 1:
                    double radius = 3 / safeZoom;
                    context.DrawEllipse(fill ?? pen.Brush, pen, ToPoint(points[0]), radius, radius);
                    break;
                case AlgorithmGeometryKind.Line when points.Length >= 2:
                    context.DrawLine(pen, ToPoint(points[0]), ToPoint(points[1]));
                    break;
                case AlgorithmGeometryKind.Circle when points.Length >= 1 && geometry.Radius is double sourceRadius:
                    double radiusX = space == AlgorithmCoordinateSpace.Pixel ? sourceRadius : sourceRadius * dpiX / 25.4;
                    double radiusY = space == AlgorithmCoordinateSpace.Pixel ? sourceRadius : sourceRadius * dpiY / 25.4;
                    context.DrawEllipse(fill, pen, ToPoint(points[0]), radiusX, radiusY);
                    break;
                case AlgorithmGeometryKind.Rectangle when points.Length >= 2:
                    context.DrawRectangle(fill, pen, new Rect(ToPoint(points[0]), ToPoint(points[1])));
                    break;
                case AlgorithmGeometryKind.Polygon when points.Length >= 3:
                    context.DrawGeometry(fill, pen, Path(points, closed: true));
                    break;
                case AlgorithmGeometryKind.Polyline when points.Length >= 2:
                    context.DrawGeometry(null, pen, Path(points, closed: false));
                    break;
            }
            if (!string.IsNullOrWhiteSpace(style.Label) && points.Length > 0)
            {
                FormattedText text = new(
                    style.Label,
                    CultureInfo.CurrentUICulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"),
                    11 / safeZoom,
                    pen.Brush,
                    1);
                context.DrawText(text, ToPoint(points[0]));
            }
        }

        private static PathGeometry Path(IReadOnlyList<AlgorithmPoint> points, bool closed)
        {
            PathFigure figure = new() { StartPoint = ToPoint(points[0]), IsClosed = closed, IsFilled = closed };
            figure.Segments.Add(new PolyLineSegment(points.Skip(1).Select(ToPoint), true));
            return new PathGeometry([figure]);
        }

        private static Point ToPoint(AlgorithmPoint point) => new(point.X, point.Y);

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
