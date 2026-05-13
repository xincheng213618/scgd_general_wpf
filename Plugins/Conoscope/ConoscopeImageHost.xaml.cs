using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Conoscope
{
    public sealed class ConoscopeFocusCircleCalculationRequestedEventArgs : EventArgs
    {
        public ConoscopeFocusCircleCalculationRequestedEventArgs(IReadOnlyList<DVCircleText> circles)
        {
            Circles = circles;
        }

        public IReadOnlyList<DVCircleText> Circles { get; }
    }

    public partial class ConoscopeImageHost : UserControl, IDisposable
    {
        private const double MinimumFocusCircleRadius = 4;

        private readonly List<DVCircleText> focusCircles = new();
        private readonly ContextMenu focusCircleContextMenu = new();
        private readonly MenuItem calculateFocusCircleMenuItem = new();
        private readonly MenuItem deleteFocusCircleMenuItem = new();
        private readonly MenuItem clearFocusCircleMenuItem = new();

        private DVCircleText? activeFocusCircle;
        private DVCircleText? contextMenuFocusCircle;
        private bool isFocusCircleDrawMode;
        private int focusCircleSequence = 1;

        public ConoscopeImageHost()
        {
            InitializeComponent();
            InitializeFocusCircleContextMenu();
            ZoomBox.ContentMatrixChanged += ZoomBox_ContentMatrixChanged;
            ImageCanvas.PreviewMouseLeftButtonDown += ImageCanvas_PreviewMouseLeftButtonDown;
            ImageCanvas.MouseMove += ImageCanvas_MouseMove;
            ImageCanvas.PreviewMouseLeftButtonUp += ImageCanvas_PreviewMouseLeftButtonUp;
            ImageCanvas.PreviewMouseRightButtonDown += ImageCanvas_PreviewMouseRightButtonDown;
            ImageCanvas.ContextMenuOpening += ImageCanvas_ContextMenuOpening;
        }

        public DrawCanvas ImageShow => ImageCanvas;

        public Zoombox Zoombox1 => ZoomBox;

        public IReadOnlyList<DVCircleText> FocusCircles => focusCircles;

        public event EventHandler<ConoscopeFocusCircleCalculationRequestedEventArgs>? FocusCircleCalculationRequested;

        public bool IsFocusCircleDrawMode
        {
            get => isFocusCircleDrawMode;
            set
            {
                if (isFocusCircleDrawMode == value)
                {
                    return;
                }

                isFocusCircleDrawMode = value;
                if (!isFocusCircleDrawMode)
                {
                    CancelFocusCircleDrawing();
                }

                UpdateCanvasCursor();
            }
        }

        public void Clear()
        {
            ClearCore(preserveFocusCircles: true);
        }

        public void ClearFocusCircles()
        {
            CancelFocusCircleDrawing();

            foreach (DVCircleText circle in focusCircles.ToArray())
            {
                ImageCanvas.RemoveVisual(circle);
            }

            focusCircles.Clear();
            contextMenuFocusCircle = null;
        }

        public void SetFocusCircleDrawMode(bool isEnabled)
        {
            IsFocusCircleDrawMode = isEnabled;
        }

        public void Dispose()
        {
            ZoomBox.ContentMatrixChanged -= ZoomBox_ContentMatrixChanged;
            ImageCanvas.PreviewMouseLeftButtonDown -= ImageCanvas_PreviewMouseLeftButtonDown;
            ImageCanvas.MouseMove -= ImageCanvas_MouseMove;
            ImageCanvas.PreviewMouseLeftButtonUp -= ImageCanvas_PreviewMouseLeftButtonUp;
            ImageCanvas.PreviewMouseRightButtonDown -= ImageCanvas_PreviewMouseRightButtonDown;
            ImageCanvas.ContextMenuOpening -= ImageCanvas_ContextMenuOpening;
            ClearCore(preserveFocusCircles: false);
            ImageCanvas.ContextMenu = null;
            ZoomBox.Child = null;
            GC.SuppressFinalize(this);
        }

        private void InitializeFocusCircleContextMenu()
        {
            calculateFocusCircleMenuItem.Click += CalculateFocusCircleMenuItem_Click;
            deleteFocusCircleMenuItem.Click += DeleteFocusCircleMenuItem_Click;
            clearFocusCircleMenuItem.Click += ClearFocusCircleMenuItem_Click;

            focusCircleContextMenu.Items.Add(calculateFocusCircleMenuItem);
            focusCircleContextMenu.Items.Add(deleteFocusCircleMenuItem);
            focusCircleContextMenu.Items.Add(new Separator());
            focusCircleContextMenu.Items.Add(clearFocusCircleMenuItem);
            ImageCanvas.ContextMenu = focusCircleContextMenu;
            UpdateCanvasCursor();
        }

        private void ZoomBox_ContentMatrixChanged(object? sender, EventArgs e)
        {
            UpdateDrawingVisualScale();
            ImageCanvas.ApplyLayoutScaleToVisuals();
        }

        private void ClearCore(bool preserveFocusCircles)
        {
            CancelFocusCircleDrawing();

            DVCircleText[] circlesToRestore = preserveFocusCircles ? focusCircles.ToArray() : Array.Empty<DVCircleText>();
            if (!preserveFocusCircles)
            {
                focusCircles.Clear();
                contextMenuFocusCircle = null;
            }

            ImageCanvas.Clear();
            ImageCanvas.Source = null;

            if (preserveFocusCircles)
            {
                foreach (DVCircleText circle in circlesToRestore)
                {
                    AttachFocusCircle(circle);
                }
            }

            ImageCanvas.UpdateLayout();
        }

        public void SetImageSource(ImageSource imageSource)
        {
            ImageCanvas.Source = imageSource;
            ImageCanvas.RaiseImageInitialized();
        }

        public void UpdateZoomAndScale()
        {
            if (CheckAccess())
            {
                UpdateZoomAndScaleCore();
            }
            else
            {
                Dispatcher.BeginInvoke(new Action(UpdateZoomAndScaleCore));
            }
        }

        private void UpdateZoomAndScaleCore()
        {
            ZoomBox.ZoomUniform();
            Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
            {
                UpdateDrawingVisualScale();
                ImageCanvas.ApplyLayoutScaleToVisuals();
            }));
        }

        private void UpdateDrawingVisualScale()
        {
            double zoomRatio = ZoomBox.ContentMatrix.M11;
            ImageCanvas.Sacle = double.IsNaN(zoomRatio) || double.IsInfinity(zoomRatio) || zoomRatio <= 0 ? 1 : 1 / zoomRatio;
        }

        private void UpdateCanvasCursor()
        {
            ImageCanvas.Cursor = IsFocusCircleDrawMode ? Cursors.Cross : Cursors.Arrow;
        }

        private void ImageCanvas_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!IsFocusCircleDrawMode || ImageCanvas.Source == null)
            {
                return;
            }

            Point point = e.GetPosition(ImageCanvas);
            DVCircleText circle = CreateFocusCircle(point);
            focusCircles.Add(circle);
            AttachFocusCircle(circle);
            activeFocusCircle = circle;
            ImageCanvas.CaptureMouse();
            e.Handled = true;
        }

        private void ImageCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (activeFocusCircle == null || !ImageCanvas.IsMouseCaptured)
            {
                return;
            }

            Point point = e.GetPosition(ImageCanvas);
            Point center = activeFocusCircle.Attribute.Center;
            double radius = Math.Sqrt(Math.Pow(point.X - center.X, 2) + Math.Pow(point.Y - center.Y, 2));
            activeFocusCircle.Attribute.Radius = radius;
            activeFocusCircle.Render();
            e.Handled = true;
        }

        private void ImageCanvas_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (activeFocusCircle == null)
            {
                return;
            }

            DVCircleText circle = activeFocusCircle;
            activeFocusCircle = null;
            if (ImageCanvas.IsMouseCaptured)
            {
                ImageCanvas.ReleaseMouseCapture();
            }

            if (circle.Attribute.Radius < MinimumFocusCircleRadius)
            {
                RemoveFocusCircle(circle);
            }
            else
            {
                circle.Render();
            }

            e.Handled = true;
        }

        private void ImageCanvas_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            Point point = e.GetPosition(ImageCanvas);
            contextMenuFocusCircle = FindFocusCircle(point);
        }

        private void ImageCanvas_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (focusCircles.Count == 0)
            {
                e.Handled = true;
                return;
            }

            if (contextMenuFocusCircle == null)
            {
                contextMenuFocusCircle = FindFocusCircle(Mouse.GetPosition(ImageCanvas));
            }

            string focusCircleName = contextMenuFocusCircle == null ? string.Empty : ResolveFocusCircleName(contextMenuFocusCircle);
            calculateFocusCircleMenuItem.Header = contextMenuFocusCircle == null ? "计算全部关注点" : $"计算关注点: {focusCircleName}";
            deleteFocusCircleMenuItem.Header = contextMenuFocusCircle == null ? "删除当前关注点" : $"删除关注点: {focusCircleName}";
            deleteFocusCircleMenuItem.Visibility = contextMenuFocusCircle == null ? Visibility.Collapsed : Visibility.Visible;
            clearFocusCircleMenuItem.Header = focusCircles.Count > 1 ? "清空全部关注点" : "清空关注点";
        }

        private void CalculateFocusCircleMenuItem_Click(object sender, RoutedEventArgs e)
        {
            DVCircleText[] circles = contextMenuFocusCircle == null ? focusCircles.ToArray() : new[] { contextMenuFocusCircle };
            if (circles.Length == 0)
            {
                return;
            }

            FocusCircleCalculationRequested?.Invoke(this, new ConoscopeFocusCircleCalculationRequestedEventArgs(circles));
        }

        private void DeleteFocusCircleMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (contextMenuFocusCircle == null)
            {
                return;
            }

            RemoveFocusCircle(contextMenuFocusCircle);
            contextMenuFocusCircle = null;
        }

        private void ClearFocusCircleMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ClearFocusCircles();
        }

        private DVCircleText CreateFocusCircle(Point center)
        {
            int id = focusCircleSequence++;
            CircleTextProperties properties = new()
            {
                Id = id,
                Center = center,
                Radius = MinimumFocusCircleRadius,
                Brush = Brushes.Transparent,
                Pen = new Pen(Brushes.DeepSkyBlue, 2),
                Text = $"Focus_{id}",
            };
            properties.Foreground = Brushes.DeepSkyBlue;
            properties.FontWeight = FontWeights.SemiBold;
            properties.Msg = string.Empty;
            return new DVCircleText(properties);
        }

        private void AttachFocusCircle(DVCircleText circle)
        {
            ImageCanvas.AddVisual(circle);
            ImageCanvas.TopVisual(circle);
        }

        private void CancelFocusCircleDrawing()
        {
            if (activeFocusCircle != null)
            {
                RemoveFocusCircle(activeFocusCircle);
                activeFocusCircle = null;
            }

            if (ImageCanvas.IsMouseCaptured)
            {
                ImageCanvas.ReleaseMouseCapture();
            }
        }

        private void RemoveFocusCircle(DVCircleText circle)
        {
            focusCircles.Remove(circle);
            ImageCanvas.RemoveVisual(circle);
        }

        private DVCircleText? FindFocusCircle(Point point)
        {
            for (int index = focusCircles.Count - 1; index >= 0; index--)
            {
                DVCircleText circle = focusCircles[index];
                Point center = circle.Attribute.Center;
                double dx = point.X - center.X;
                double dy = point.Y - center.Y;
                double distance = Math.Sqrt(dx * dx + dy * dy);
                double hitTolerance = 8 * Math.Max(ImageCanvas.Sacle, 0.2);
                if (distance <= circle.Attribute.Radius + hitTolerance)
                {
                    return circle;
                }
            }

            return null;
        }

        private static string ResolveFocusCircleName(DVCircleText circle)
        {
            return string.IsNullOrWhiteSpace(circle.Attribute.Text) ? $"Focus_{circle.Attribute.Id}" : circle.Attribute.Text;
        }
    }
}