using System;
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
        Circle
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
    }

    /// <summary>
    /// Provides a transient (non-recording) drawing selection mode on an existing ImageView.
    /// The user draws a single shape (rectangle or circle) inline on the image canvas.
    /// On mouse-up, the mode ends automatically and returns the drawn shape's properties.
    /// The drawn visual is NOT added to the DrawingVisualLists / undo stack.
    /// 
    /// Usage:
    ///   var result = await imageView.BeginSelectAsync(SelectShapeType.Rectangle);
    ///   if (result != null)
    ///   {
    ///       // use result.Rect, result.Center, result.Radius
    ///   }
    /// </summary>
    internal class TransientSelectMode
    {
        private readonly DrawCanvas _drawCanvas;
        private readonly Zoombox _zoombox;
        private readonly TaskCompletionSource<SelectResult> _tcs;
        private readonly SelectShapeType _shapeType;

        private DrawingVisual _visual;
        private Point _mouseDown;
        private bool _isDrawing;
        private Cursor _previousCursor;

        public TransientSelectMode(DrawCanvas drawCanvas, Zoombox zoombox, SelectShapeType shapeType)
        {
            _drawCanvas = drawCanvas;
            _zoombox = zoombox;
            _shapeType = shapeType;
            _tcs = new TaskCompletionSource<SelectResult>();
        }

        public Task<SelectResult> Start()
        {
            _previousCursor = _zoombox.Cursor;
            _zoombox.Cursor = Cursors.Cross;

            _drawCanvas.PreviewMouseLeftButtonDown += OnMouseDown;
            _drawCanvas.PreviewMouseMove += OnMouseMove;
            _drawCanvas.PreviewMouseLeftButtonUp += OnMouseUp;
            _drawCanvas.PreviewKeyDown += OnKeyDown;

            return _tcs.Task;
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            _mouseDown = e.GetPosition(_drawCanvas);
            _isDrawing = true;

            _visual = new DrawingVisual();
            // Add directly to visual tree (no undo/redo, no DrawingVisualLists)
            _drawCanvas.AddVisual(_visual);

            _drawCanvas.CaptureMouse();
            e.Handled = true;
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDrawing || _visual == null) return;

            var current = e.GetPosition(_drawCanvas);
            RenderPreview(_mouseDown, current);
            e.Handled = true;
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDrawing || _visual == null) return;

            _drawCanvas.ReleaseMouseCapture();
            var mouseUp = e.GetPosition(_drawCanvas);

            // Build result
            SelectResult result = BuildResult(_mouseDown, mouseUp);

            // Clean up
            Cleanup();

            if (result.Rect.Width > 1 && result.Rect.Height > 1)
            {
                _tcs.TrySetResult(result);
            }
            else
            {
                // Too small, treat as cancelled
                _tcs.TrySetResult(null);
            }

            e.Handled = true;
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Cleanup();
                _tcs.TrySetResult(null);
                e.Handled = true;
            }
        }

        private void RenderPreview(Point start, Point current)
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

        private SelectResult BuildResult(Point start, Point end)
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

        private void Cleanup()
        {
            _isDrawing = false;

            _drawCanvas.PreviewMouseLeftButtonDown -= OnMouseDown;
            _drawCanvas.PreviewMouseMove -= OnMouseMove;
            _drawCanvas.PreviewMouseLeftButtonUp -= OnMouseUp;
            _drawCanvas.PreviewKeyDown -= OnKeyDown;

            if (_visual != null)
            {
                _drawCanvas.RemoveVisual(_visual);
                _visual = null;
            }

            _drawCanvas.ReleaseMouseCapture();
            _zoombox.Cursor = _previousCursor;
        }
    }
}
