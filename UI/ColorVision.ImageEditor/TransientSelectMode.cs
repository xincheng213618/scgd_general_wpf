using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

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
    internal class TransientSelectMode
    {
        private readonly DrawCanvas _drawCanvas;
        private readonly Zoombox _zoombox;
        private readonly ImageViewModel _imageViewModel;
        private readonly TaskCompletionSource<SelectResult> _tcs;
        private readonly SelectShapeType _shapeType;

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

        public TransientSelectMode(DrawCanvas drawCanvas, Zoombox zoombox, ImageViewModel imageViewModel, SelectShapeType shapeType)
        {
            _drawCanvas = drawCanvas;
            _zoombox = zoombox;
            _imageViewModel = imageViewModel;
            _shapeType = shapeType;
            _tcs = new TaskCompletionSource<SelectResult>();
        }

        public Task<SelectResult> Start()
        {
            _previousCursor = _zoombox.Cursor;
            _previousActivateOn = _zoombox.ActivateOn;
            _previousEditMode = _imageViewModel.ImageEditMode;

            // Suppress edit mode so SelectEditorVisual doesn't interfere.
            // Use backing field directly to avoid the property setter's side effects
            // (cursor change, ActivateOn toggle, DrawEditorManager reset).
            if (_previousEditMode)
            {
                _imageViewModel._ImageEditMode = false;
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
                _polygonPoints.Add(pos);
                if (_visual == null)
                {
                    _visual = new DrawingVisual();
                    _drawCanvas.AddVisual(_visual);
                }

                // Quadrilateral: auto-complete after 4th point
                if (_shapeType == SelectShapeType.Quadrilateral && _polygonPoints.Count >= 4)
                {
                    RenderPolygonPreview(pos);
                    var result = BuildPolygonResult(_polygonPoints);
                    Cleanup();
                    _tcs.TrySetResult(result);
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

            // Build result
            SelectResult result = BuildDragResult(_mouseDown, mouseUp);

            // Clean up
            Cleanup();

            if (result.Rect.Width > 1 && result.Rect.Height > 1)
            {
                _tcs.TrySetResult(result);
            }
            else
            {
                _tcs.TrySetResult(null);
            }

            e.Handled = true;
        }

        private void OnMouseRightDown(object sender, MouseButtonEventArgs e)
        {
            if (IsMultiClickMode)
            {
                // Right-click completes polygon if enough points, otherwise cancels
                if (_polygonPoints != null && _polygonPoints.Count >= 2)
                {
                    var result = BuildPolygonResult(_polygonPoints);
                    Cleanup();
                    _tcs.TrySetResult(result);
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
                    // Complete polygon
                    if (_polygonPoints != null && _polygonPoints.Count >= 2)
                    {
                        var result = BuildPolygonResult(_polygonPoints);
                        Cleanup();
                        _tcs.TrySetResult(result);
                    }
                    else
                    {
                        Cleanup();
                        _tcs.TrySetResult(null);
                    }
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

        private void Cleanup()
        {
            _isDrawing = false;

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

            // Restore previous state (use backing field to avoid property setter side effects)
            _zoombox.Cursor = _previousCursor;
            _zoombox.ActivateOn = _previousActivateOn;
            if (_previousEditMode)
            {
                _imageViewModel._ImageEditMode = true;
            }
        }
    }
}
