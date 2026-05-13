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
        internal const double MinimumFocusCircleRadius = 4;

        public EditorContext EditorContext { get; }

        private readonly ContextMenu focusCircleContextMenu = new();
        private readonly MenuItem calculateFocusCircleMenuItem = new();
        private readonly MenuItem deleteFocusCircleMenuItem = new();
        private readonly MenuItem clearFocusCircleMenuItem = new();
        private readonly ConoscopeFocusCircleDrawTool focusCircleDrawTool;
        private readonly EraseManager focusCircleEraseTool;

        private DVCircleText? contextMenuFocusCircle;
        private bool isFocusCircleEditMode;
        private bool isFocusCircleSelectionEnabled;
        private bool suspendFocusCircleTracking;
        private int focusCircleSequence = 1;

        public ConoscopeImageHost()
        {
            InitializeComponent();
            EditorContext = new EditorContext(ImageCanvas, ZoomBox, HostRoot);
            EditorContext.SelectionVisual = new SelectEditorVisual(EditorContext);
            focusCircleDrawTool = new ConoscopeFocusCircleDrawTool(EditorContext, this);
            focusCircleEraseTool = new EraseManager(EditorContext);
            InitializeFocusCircleContextMenu();
            ZoomBox.ContentMatrixChanged += ZoomBox_ContentMatrixChanged;
            ImageCanvas.PreviewMouseRightButtonDown += ImageCanvas_PreviewMouseRightButtonDown;
            ImageCanvas.ContextMenuOpening += ImageCanvas_ContextMenuOpening;
            ImageCanvas.VisualsAdd += ImageCanvas_VisualsAdd;
            ImageCanvas.VisualsRemove += ImageCanvas_VisualsRemove;
        }

        public DrawCanvas ImageShow => ImageCanvas;

        public Zoombox Zoombox1 => ZoomBox;

        public IReadOnlyList<DVCircleText> FocusCircles => GetFocusCircles();

        public event EventHandler<ConoscopeFocusCircleCalculationRequestedEventArgs>? FocusCircleCalculationRequested;
        public event EventHandler? FocusCirclesChanged;

        public bool IsFocusCircleEditMode
        {
            get => isFocusCircleEditMode;
            set
            {
                if (isFocusCircleEditMode == value)
                {
                    return;
                }

                isFocusCircleEditMode = value;
                if (isFocusCircleEditMode)
                {
                    ClearFocusCircleSelection();
                }

                RefreshFocusCircleInteractionState();
            }
        }

        public bool IsFocusCircleDrawMode => focusCircleDrawTool.IsChecked;

        public bool IsFocusCircleEraseMode => focusCircleEraseTool.IsChecked;

        public bool IsFocusCircleSelectionEnabled => isFocusCircleSelectionEnabled;

        public void Clear()
        {
            ClearCore(preserveFocusCircles: true);
        }

        public void ClearFocusCircles()
        {
            ClearFocusCircleSelection();
            SetFocusCircleDrawMode(false);
            SetFocusCircleEraseMode(false);

            foreach (DVCircleText circle in GetFocusCircles())
            {
                RemoveFocusCircle(circle);
            }

            contextMenuFocusCircle = null;
        }

        public void SetFocusCircleEditMode(bool isEnabled)
        {
            IsFocusCircleEditMode = isEnabled;
        }

        public void SetFocusCircleDrawMode(bool isEnabled)
        {
            focusCircleDrawTool.IsChecked = IsFocusCircleEditMode && isEnabled;
            if (isEnabled)
            {
                focusCircleEraseTool.IsChecked = false;
                ClearFocusCircleSelection();
            }

            RefreshFocusCircleInteractionState();
        }

        public void SetFocusCircleEraseMode(bool isEnabled)
        {
            focusCircleEraseTool.IsChecked = IsFocusCircleEditMode && isEnabled;
            if (isEnabled)
            {
                focusCircleDrawTool.IsChecked = false;
                ClearFocusCircleSelection();
            }

            RefreshFocusCircleInteractionState();
        }

        public void SetFocusCircleSelectionEnabled(bool isEnabled)
        {
            if (isFocusCircleSelectionEnabled == isEnabled)
            {
                return;
            }

            isFocusCircleSelectionEnabled = isEnabled;
            if (!isFocusCircleSelectionEnabled)
            {
                ClearFocusCircleSelection();
            }

            RefreshFocusCircleInteractionState();
        }

        public void Dispose()
        {
            ZoomBox.ContentMatrixChanged -= ZoomBox_ContentMatrixChanged;
            ImageCanvas.PreviewMouseRightButtonDown -= ImageCanvas_PreviewMouseRightButtonDown;
            ImageCanvas.ContextMenuOpening -= ImageCanvas_ContextMenuOpening;
            ImageCanvas.VisualsAdd -= ImageCanvas_VisualsAdd;
            ImageCanvas.VisualsRemove -= ImageCanvas_VisualsRemove;
            ClearCore(preserveFocusCircles: false);
            ImageCanvas.ContextMenu = null;
            focusCircleDrawTool.Dispose();
            focusCircleEraseTool.Dispose();
            EditorContext.SelectionVisual.Dispose();
            EditorContext.MouseInfoProvider.Dispose();
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
            ClearFocusCircleSelection();
            focusCircleDrawTool.IsChecked = false;
            focusCircleEraseTool.IsChecked = false;

            DVCircleText[] circlesToRestore = preserveFocusCircles ? GetFocusCircles() : Array.Empty<DVCircleText>();
            if (!preserveFocusCircles)
            {
                contextMenuFocusCircle = null;
            }

            suspendFocusCircleTracking = preserveFocusCircles;
            ImageCanvas.Clear();
            ImageCanvas.Source = null;

            if (preserveFocusCircles)
            {
                foreach (DVCircleText circle in circlesToRestore)
                {
                    AttachFocusCircle(circle);
                }
            }

            suspendFocusCircleTracking = false;

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

        private void RefreshFocusCircleInteractionState()
        {
            if (!IsFocusCircleEditMode)
            {
                focusCircleDrawTool.IsChecked = false;
                focusCircleEraseTool.IsChecked = false;
            }

            if (!isFocusCircleSelectionEnabled)
            {
                ClearFocusCircleSelection();
            }

            EditorContext.IsImageEditMode = isFocusCircleSelectionEnabled;
            UpdateCanvasCursor();
        }

        private void UpdateCanvasCursor()
        {
            if (focusCircleEraseTool.IsChecked)
            {
                return;
            }

            Cursor cursor = IsFocusCircleDrawMode ? Cursors.Cross : Cursors.Arrow;
            ImageCanvas.Cursor = cursor;
            ZoomBox.Cursor = cursor;
        }

        private void ImageCanvas_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            Point point = e.GetPosition(ImageCanvas);
            contextMenuFocusCircle = FindFocusCircle(point);
            if (contextMenuFocusCircle != null && isFocusCircleSelectionEnabled)
            {
                SelectFocusCircle(contextMenuFocusCircle);
            }
            else if (!isFocusCircleSelectionEnabled)
            {
                ClearFocusCircleSelection();
            }
        }

        private void ImageCanvas_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            IReadOnlyList<DVCircleText> focusCircles = FocusCircles;
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
            DVCircleText[] circles = contextMenuFocusCircle == null ? GetFocusCircles() : new[] { contextMenuFocusCircle };
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

        internal DVCircleText CreateFocusCircle(Point center)
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

        internal void AttachFocusCircle(DVCircleText circle)
        {
            if (!ImageCanvas.ContainsVisual(circle))
            {
                ImageCanvas.AddVisualCommand(circle);
            }

            ImageCanvas.TopVisual(circle);
        }

        internal void SelectFocusCircle(DVCircleText circle)
        {
            EditorContext.SelectionVisual.SetRender(circle);
            ImageCanvas.TopVisual(circle);
        }

        internal void RemoveFocusCircle(DVCircleText circle)
        {
            if (EditorContext.SelectionVisual.SelectVisuals.Contains(circle))
            {
                ClearFocusCircleSelection();
            }

            if (ImageCanvas.ContainsVisual(circle))
            {
                ImageCanvas.RemoveVisualCommand(circle);
            }
        }

        private void ImageCanvas_VisualsAdd(object? sender, VisualChangedEventArgs e)
        {
            if (suspendFocusCircleTracking || e.Visual is not DVCircleText)
            {
                return;
            }

            FocusCirclesChanged?.Invoke(this, EventArgs.Empty);
        }

        private void ImageCanvas_VisualsRemove(object? sender, VisualChangedEventArgs e)
        {
            if (suspendFocusCircleTracking || e.Visual is not DVCircleText circle)
            {
                return;
            }

            if (ReferenceEquals(contextMenuFocusCircle, circle))
            {
                contextMenuFocusCircle = null;
            }

            FocusCirclesChanged?.Invoke(this, EventArgs.Empty);
        }

        private void ClearFocusCircleSelection()
        {
            EditorContext.SelectionVisual.ClearRender();
        }

        private DVCircleText? FindFocusCircle(Point point)
        {
            IReadOnlyList<DVCircleText> focusCircles = FocusCircles;
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

        private DVCircleText[] GetFocusCircles()
        {
            return ImageCanvas.Visuals.OfType<DVCircleText>().ToArray();
        }

        private static string ResolveFocusCircleName(DVCircleText circle)
        {
            return string.IsNullOrWhiteSpace(circle.Attribute.Text) ? $"Focus_{circle.Attribute.Id}" : circle.Attribute.Text;
        }
    }
}