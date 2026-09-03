#pragma warning disable CA1852,CS8625
using ColorVision.ImageEditor.Draw;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor
{
    /// <summary>
    /// The type of shape for transient selection
    /// </summary>
    public enum SelectShapeType
    {
        Rectangle,
        Circle,
        /// <summary>
        /// Free-form polygon: click to add points, Enter/Space/right-click to complete, Escape to cancel.
        /// </summary>
        Polygon,
        /// <summary>
        /// Quadrilateral (4-point polygon): auto-completes after the 4th click.
        /// Right-click or Escape cancels.
        /// </summary>
        Quadrilateral
    }

    /// <summary>
    /// Result of a transient selection operation on ImageView.
    /// </summary>
    public class SelectResult
    {
        /// <summary>
        /// The bounding rectangle of the selected area (always available)
        /// </summary>
        public Rect Rect { get; set; }

        /// <summary>
        /// The center point (useful for circles; for rectangles this is the rect center)
        /// </summary>
        public Point Center { get; set; }

        /// <summary>
        /// For circle selections: the radius
        /// </summary>
        public double Radius { get; set; }

        /// <summary>
        /// The shape type that was selected
        /// </summary>
        public SelectShapeType ShapeType { get; set; }

        /// <summary>
        /// For polygon/quadrilateral selections: the collection of points
        /// </summary>
        public List<Point> Points { get; set; } = new();

        /// <summary>The immutable image scope on which these coordinates were collected.</summary>
        public ImageSelectionScope? SourceScope { get; internal set; }
    }

    public sealed record ImageSelectionScope(
        Guid DocumentInstanceId,
        long SourceRevision,
        int PixelWidth,
        int PixelHeight,
        double DpiX,
        double DpiY)
    {
        /// <summary>Logical canvas dimensions in WPF units, including non-bitmap sources.</summary>
        public double CanvasWidth { get; init; } = PixelWidth * 96d / DpiX;
        public double CanvasHeight { get; init; } = PixelHeight * 96d / DpiY;

        /// <summary>False for a drawable canvas without source pixels; pixel dimensions are then zero.</summary>
        public bool HasPixels => PixelWidth > 0 && PixelHeight > 0;
    }

    /// <summary>
    /// Provides a transient (non-recording) drawing selection mode on an existing ImageView.
    /// The user draws a single shape (rectangle, circle, polygon, or quadrilateral) inline on the image canvas.
    /// For Rectangle/Circle: on mouse-up, the mode ends automatically.
    /// For Polygon: each click adds a point; press Enter/Space/right-click to complete, Escape to cancel.
    /// For Quadrilateral: each click adds a point; auto-completes after the 4th click, right-click/Escape cancels.
    /// The drawn visual is NOT added to the DrawingVisualLists / undo stack.
    /// 
    /// Usage:
    ///   var result = await imageView.BeginSelectAsync(SelectShapeType.Rectangle);
    ///   if (result != null)
    ///   {
    ///       // use result.Rect, result.Center, result.Radius, result.Points
    ///   }
    /// </summary>
    internal class TransientRoiSelectionSession
    {
        private readonly DrawEditorContext _drawContext;
        private readonly DrawCanvas _drawCanvas;
        private readonly Zoombox _zoombox;
        private readonly TaskCompletionSource<SelectResult> _tcs;
        private readonly SelectShapeType _shapeType;
        private readonly ImageProcessingContext? _processingContext;
        private ImageSelectionScope? _sourceScope;
        private bool _cleanedUp;

        private DrawingVisual _visual;
        private Point _mouseDown;
        private bool _isDrawing;
        private Cursor _previousCursor;
        private ModifierKeys _previousActivateOn;
        private bool _previousEditMode;

        // Polygon/Quadrilateral mode state
        private List<Point> _polygonPoints;

        /// <summary>
        /// Whether the shape type is a multi-click mode (Polygon or Quadrilateral).
        /// </summary>
        private bool IsMultiClickMode => _shapeType == SelectShapeType.Polygon || _shapeType == SelectShapeType.Quadrilateral;

        public TransientRoiSelectionSession(DrawEditorContext drawContext, SelectShapeType shapeType)
        {
            _drawContext = drawContext;
            _drawCanvas = drawContext.DrawCanvas;
            _zoombox = drawContext.Zoombox;
            _shapeType = shapeType;
            _processingContext = drawContext.ProcessingContext;
            _tcs = new TaskCompletionSource<SelectResult>();
        }

        public Task<SelectResult> Start()
        {
            _sourceScope = CaptureSourceScope(_processingContext);
            if (_processingContext != null)
            {
                if (_sourceScope == null || !IsSourceScopeCurrent(_processingContext, _sourceScope))
                {
                    // Nothing has been activated yet: do not restore uninitialized interaction
                    // state or release another operation's mouse capture on this early exit.
                    _tcs.TrySetResult(null);
                    return _tcs.Task;
                }
                _processingContext.DocumentScopeChanged += OnDocumentScopeChanged;
            }

            _previousCursor = _zoombox.Cursor;
            _previousActivateOn = _zoombox.ActivateOn;
            _previousEditMode = _drawContext.IsImageEditMode;

            // Suppress edit mode so SelectEditorVisual doesn't interfere without
            // running the full ImageView mode toggle side effects.
            if (_previousEditMode)
            {
                _drawContext.IsImageEditMode = false;
            }
            // Ensure zoombox is in draw-friendly mode
            _zoombox.ActivateOn = ModifierKeys.Control;
            _zoombox.Cursor = Cursors.Cross;

            _drawCanvas.PreviewMouseLeftButtonDown += OnMouseDown;
            _drawCanvas.PreviewMouseMove += OnMouseMove;
            _drawCanvas.PreviewMouseLeftButtonUp += OnMouseUp;
            _drawCanvas.PreviewMouseRightButtonDown += OnMouseRightDown;
            _drawCanvas.PreviewKeyDown += OnKeyDown;

            if (IsMultiClickMode)
            {
                _polygonPoints = new List<Point>();
            }

            return _tcs.Task;
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            var pos = e.GetPosition(_drawCanvas);

            if (IsMultiClickMode)
            {
                // Multi-click mode: each click adds a point
                if (_shapeType == SelectShapeType.Quadrilateral && _polygonPoints.Count >= 4)
                {
                    // An invalid fourth point keeps the session active; the next click corrects it.
                    _polygonPoints[^1] = pos;
                }
                else
                {
                    _polygonPoints.Add(pos);
                }
                if (_visual == null)
                {
                    _visual = new DrawingVisual();
                    _drawCanvas.AddVisual(_visual);
                }

                // Quadrilateral: auto-complete after 4th point
                if (_shapeType == SelectShapeType.Quadrilateral && _polygonPoints.Count >= 4)
                {
                    RenderPolygonPreview(pos);
                    TryCompletePolygon();
                    e.Handled = true;
                    return;
                }

                RenderPolygonPreview(pos);
                _drawCanvas.CaptureMouse();
                e.Handled = true;
                return;
            }

            // Rectangle / Circle: single drag
            _mouseDown = pos;
            _isDrawing = true;

            _visual = new DrawingVisual();
            _drawCanvas.AddVisual(_visual);

            _drawCanvas.CaptureMouse();
            e.Handled = true;
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            var current = e.GetPosition(_drawCanvas);

            if (IsMultiClickMode)
            {
                if (_visual != null && _polygonPoints.Count > 0)
                {
                    RenderPolygonPreview(current);
                }
                e.Handled = true;
                return;
            }

            if (!_isDrawing || _visual == null) return;

            RenderDragPreview(_mouseDown, current);
            e.Handled = true;
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (IsMultiClickMode)
            {
                // Multi-click mode: mouse-up just finalizes the point position (already added in OnMouseDown)
                _drawCanvas.ReleaseMouseCapture();
                e.Handled = true;
                return;
            }

            if (!_isDrawing || _visual == null) return;

            _drawCanvas.ReleaseMouseCapture();
            var mouseUp = e.GetPosition(_drawCanvas);

            if (!TryCompleteDrag(_mouseDown, mouseUp))
            {
                ResetDragAttempt();
            }

            e.Handled = true;
        }

        private void OnMouseRightDown(object sender, MouseButtonEventArgs e)
        {
            if (IsMultiClickMode)
            {
                // An invalid free-form polygon remains active so the user can add/correct points.
                if (_shapeType == SelectShapeType.Polygon)
                {
                    TryCompletePolygon();
                }
                else if (_polygonPoints != null && _polygonPoints.Count >= 4)
                {
                    TryCompletePolygon();
                }
                else
                {
                    Cleanup();
                    _tcs.TrySetResult(null);
                }
                // Suppress context menu
                e.Handled = true;
                return;
            }
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            Key realKey = e.Key;
            if (realKey == Key.ImeProcessed)
                realKey = e.ImeProcessedKey;

            if (realKey == Key.Escape)
            {
                Cleanup();
                _tcs.TrySetResult(null);
                e.Handled = true;
                return;
            }

            if (IsMultiClickMode)
            {
                if (realKey == Key.Enter || realKey == Key.Space || realKey == Key.End || realKey == Key.Tab)
                {
                    TryCompletePolygon();
                    e.Handled = true;
                }
            }
        }

        private void RenderDragPreview(Point start, Point current)
        {
            using var dc = _visual.RenderOpen();
            double thickness = 1 / _zoombox.ContentMatrix.M11;
            var pen = new Pen(Brushes.DodgerBlue, thickness) { DashStyle = DashStyles.Dash };
            var fill = new SolidColorBrush(Color.FromArgb(30, 30, 144, 255));

            switch (_shapeType)
            {
                case SelectShapeType.Rectangle:
                    var rect = new Rect(start, current);
                    dc.DrawRectangle(fill, pen, rect);
                    break;

                case SelectShapeType.Circle:
                    double radius = Math.Sqrt(Math.Pow(current.X - start.X, 2) + Math.Pow(current.Y - start.Y, 2));
                    dc.DrawEllipse(fill, pen, start, radius, radius);
                    break;
            }
        }

        private void RenderPolygonPreview(Point currentMouse)
        {
            using var dc = _visual.RenderOpen();
            double thickness = 1 / _zoombox.ContentMatrix.M11;
            var pen = new Pen(Brushes.DodgerBlue, thickness) { DashStyle = DashStyles.Dash };
            var fill = new SolidColorBrush(Color.FromArgb(30, 30, 144, 255));
            double dotRadius = 3 / _zoombox.ContentMatrix.M11;

            // Draw existing lines between points
            for (int i = 1; i < _polygonPoints.Count; i++)
            {
                dc.DrawLine(pen, _polygonPoints[i - 1], _polygonPoints[i]);
            }

            // Draw line from last point to current mouse position
            if (_polygonPoints.Count > 0)
            {
                dc.DrawLine(pen, _polygonPoints[_polygonPoints.Count - 1], currentMouse);
            }

            // Draw dots at each point
            foreach (var pt in _polygonPoints)
            {
                dc.DrawEllipse(fill, pen, pt, dotRadius, dotRadius);
            }
        }

        private SelectResult BuildDragResult(Point start, Point end)
        {
            switch (_shapeType)
            {
                case SelectShapeType.Circle:
                    double radius = Math.Sqrt(Math.Pow(end.X - start.X, 2) + Math.Pow(end.Y - start.Y, 2));
                    return new SelectResult
                    {
                        ShapeType = SelectShapeType.Circle,
                        Center = start,
                        Radius = radius,
                        Rect = new Rect(start.X - radius, start.Y - radius, radius * 2, radius * 2)
                    };

                case SelectShapeType.Rectangle:
                default:
                    var rect = new Rect(start, end);
                    return new SelectResult
                    {
                        ShapeType = SelectShapeType.Rectangle,
                        Rect = rect,
                        Center = new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2),
                        Radius = Math.Min(rect.Width, rect.Height) / 2
                    };
            }
        }

        private SelectResult BuildPolygonResult(List<Point> points)
        {
            double minX = points.Min(p => p.X);
            double minY = points.Min(p => p.Y);
            double maxX = points.Max(p => p.X);
            double maxY = points.Max(p => p.Y);
            var rect = new Rect(new Point(minX, minY), new Point(maxX, maxY));

            return new SelectResult
            {
                ShapeType = _shapeType,
                Rect = rect,
                Center = new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2),
                Radius = Math.Min(rect.Width, rect.Height) / 2,
                Points = new List<Point>(points)
            };
        }

        private bool TryCompleteDrag(Point start, Point end)
        {
            SelectResult result = BuildDragResult(start, end);
            if (!IsValidDragResult(result) || !TryBindSourceScope(result))
                return false;

            Cleanup();
            _tcs.TrySetResult(result);
            return true;
        }

        private bool TryCompletePolygon()
        {
            if (_polygonPoints == null || !IsValidPolygon(_polygonPoints))
            {
                return false;
            }

            SelectResult result = BuildPolygonResult(_polygonPoints);
            if (!TryBindSourceScope(result))
            {
                return false;
            }
            Cleanup();
            _tcs.TrySetResult(result);
            return true;
        }

        private void ResetDragAttempt()
        {
            _isDrawing = false;
            if (_visual != null)
            {
                _drawCanvas.RemoveVisual(_visual);
                _visual = null;
            }
        }

        internal static bool IsValidDragResult(SelectResult result)
            => result != null
                && IsFinite(result.Rect.X)
                && IsFinite(result.Rect.Y)
                && IsFinite(result.Rect.Width)
                && IsFinite(result.Rect.Height)
                && result.Rect.Width > 1
                && result.Rect.Height > 1;

        internal static bool IsValidPolygon(IReadOnlyList<Point> points)
        {
            if (points == null || points.Count < 3 || points.Any(point => !IsFinite(point.X) || !IsFinite(point.Y)))
            {
                return false;
            }

            double twiceArea = 0;
            for (int index = 0; index < points.Count; index++)
            {
                Point current = points[index];
                Point next = points[(index + 1) % points.Count];
                if (current == next)
                {
                    return false;
                }
                twiceArea += current.X * next.Y - next.X * current.Y;
            }
            if (!IsFinite(twiceArea) || Math.Abs(twiceArea) <= 1e-6)
            {
                return false;
            }

            // V1 accepts simple polygons only. Self-intersections have ambiguous fill/ROI semantics.
            for (int first = 0; first < points.Count; first++)
            {
                int firstNext = (first + 1) % points.Count;
                for (int second = first + 1; second < points.Count; second++)
                {
                    int secondNext = (second + 1) % points.Count;
                    if (first == second || firstNext == second || secondNext == first)
                    {
                        continue;
                    }
                    if (SegmentsIntersect(points[first], points[firstNext], points[second], points[secondNext]))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private static bool SegmentsIntersect(Point a, Point b, Point c, Point d)
        {
            double abC = Cross(a, b, c);
            double abD = Cross(a, b, d);
            double cdA = Cross(c, d, a);
            double cdB = Cross(c, d, b);
            const double tolerance = 1e-9;
            if (Math.Abs(abC) <= tolerance && IsWithinSegment(a, b, c)) return true;
            if (Math.Abs(abD) <= tolerance && IsWithinSegment(a, b, d)) return true;
            if (Math.Abs(cdA) <= tolerance && IsWithinSegment(c, d, a)) return true;
            if (Math.Abs(cdB) <= tolerance && IsWithinSegment(c, d, b)) return true;
            return (abC > 0) != (abD > 0) && (cdA > 0) != (cdB > 0);
        }

        private static double Cross(Point start, Point end, Point point)
            => (end.X - start.X) * (point.Y - start.Y) - (end.Y - start.Y) * (point.X - start.X);

        private static bool IsWithinSegment(Point start, Point end, Point point)
            => point.X >= Math.Min(start.X, end.X) - 1e-9
                && point.X <= Math.Max(start.X, end.X) + 1e-9
                && point.Y >= Math.Min(start.Y, end.Y) - 1e-9
                && point.Y <= Math.Max(start.Y, end.Y) + 1e-9;

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        private bool TryBindSourceScope(SelectResult result)
        {
            if (_processingContext == null)
            {
                return true;
            }
            if (_sourceScope == null || !IsSourceScopeCurrent(_processingContext, _sourceScope))
            {
                InvalidateSourceScope();
                return false;
            }

            result.SourceScope = _sourceScope;
            return true;
        }

        private void OnDocumentScopeChanged(object? sender, EventArgs e)
        {
            if (_processingContext != null
                && _sourceScope != null
                && !IsSourceScopeCurrent(_processingContext, _sourceScope))
            {
                InvalidateSourceScope();
            }
        }

        private void InvalidateSourceScope()
        {
            Cleanup();
            _tcs.TrySetResult(null);
        }

        internal static ImageSelectionScope? CaptureSourceScope(ImageProcessingContext? context)
        {
            if (context == null || context.IsDisposed || context.ViewBitmapSource is not ImageSource source
                || !IsFinite(source.Width) || !IsFinite(source.Height) || source.Width <= 0 || source.Height <= 0)
            {
                return null;
            }

            if (source is not BitmapSource bitmap)
            {
                // Manual selection needs geometry, not readable pixels. Keep the document
                // scope so changing a vector canvas still invalidates its selected coordinates.
                return new ImageSelectionScope(context.DocumentInstanceId, context.ImageRevision, 0, 0, 96, 96)
                {
                    CanvasWidth = source.Width,
                    CanvasHeight = source.Height,
                };
            }
            return new ImageSelectionScope(
                context.DocumentInstanceId,
                context.ImageRevision,
                bitmap.PixelWidth,
                bitmap.PixelHeight,
                bitmap.DpiX,
                bitmap.DpiY);
        }

        internal static bool IsSourceScopeCurrent(ImageProcessingContext context, ImageSelectionScope scope)
            => !context.IsDisposed
                && context.DocumentInstanceId == scope.DocumentInstanceId
                && context.IsCurrentImageRevision(scope.SourceRevision)
                && CaptureSourceScope(context) == scope;

        private void Cleanup()
        {
            if (_cleanedUp)
            {
                return;
            }
            _cleanedUp = true;
            _isDrawing = false;

            if (_processingContext != null)
            {
                _processingContext.DocumentScopeChanged -= OnDocumentScopeChanged;
            }

            _drawCanvas.PreviewMouseLeftButtonDown -= OnMouseDown;
            _drawCanvas.PreviewMouseMove -= OnMouseMove;
            _drawCanvas.PreviewMouseLeftButtonUp -= OnMouseUp;
            _drawCanvas.PreviewMouseRightButtonDown -= OnMouseRightDown;
            _drawCanvas.PreviewKeyDown -= OnKeyDown;

            if (_visual != null)
            {
                _drawCanvas.RemoveVisual(_visual);
                _visual = null;
            }

            _drawCanvas.ReleaseMouseCapture();

            // Restore previous state without running the full ImageView mode toggle side effects.
            _zoombox.Cursor = _previousCursor;
            _zoombox.ActivateOn = _previousActivateOn;
            _drawContext.IsImageEditMode = _previousEditMode;
        }
    }
}
