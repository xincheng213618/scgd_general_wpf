#pragma warning disable CA1822,CA1863
using ColorVision.Engine.Templates.POI;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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

    public sealed class ConoscopeFocusCircleEditRequestedEventArgs : EventArgs
    {
        public ConoscopeFocusCircleEditRequestedEventArgs(DVCircleText circle)
        {
            Circle = circle;
        }

        public DVCircleText Circle { get; }
    }

    public partial class ConoscopeImageHost : UserControl, IDisposable
    {
        internal const double MinimumFocusCircleRadius = 4;

        public DrawEditorContext EditorContext { get; }

        private readonly ContextMenu focusCircleContextMenu = new();
        private readonly DrawingVisualBaseDVContextMenu focusCirclePropertyContextMenu;
        private readonly MenuItem editFocusCircleByPolarMenuItem = new();
        private readonly MenuItem calculateFocusCircleMenuItem = new();
        private readonly MenuItem clearFocusCircleMenuItem = new();
        private readonly HashSet<DVCircleText> trackedFocusCircles = new();
        private readonly ConoscopeFocusCircleDrawTool focusCircleDrawTool;
        private readonly EraseManager focusCircleEraseTool;

        private DVCircleText? contextMenuFocusCircle;
        private bool isFocusCircleEditMode;
        private bool isFocusCircleSelectionEnabled;
        private bool hasFocusCircleBoundary;
        private bool isAdjustingFocusCircleBoundary;
        private bool suspendFocusCircleTracking;
        private int focusCircleSequence = 1;
        private Point focusCircleBoundaryCenter;
        private double focusCircleBoundaryRadius;

        public ConoscopeImageHost()
        {
            InitializeComponent();

            EditorContext = new DrawEditorContext(ImageCanvas, ZoomBox);

            EditorContext.SelectionVisual = new SelectEditorVisual(EditorContext);
            focusCirclePropertyContextMenu = new DrawingVisualBaseDVContextMenu(EditorContext);
            focusCircleDrawTool = new ConoscopeFocusCircleDrawTool(EditorContext, this);
            focusCircleEraseTool = new EraseManager(EditorContext);
            focusCircleEraseTool.CanEraseVisual = static visual => visual is DVCircleText;
            InitializeFocusCircleContextMenu();
            EditorContext.SelectionVisual.SelectionChanged += SelectionVisual_SelectionChanged;
            ZoomBox.ContentMatrixChanged += ZoomBox_ContentMatrixChanged;
            ImageCanvas.PreviewMouseRightButtonDown += ImageCanvas_PreviewMouseRightButtonDown;
            ImageCanvas.ContextMenuOpening += ImageCanvas_ContextMenuOpening;
            ImageCanvas.VisualsAdd += ImageCanvas_VisualsAdd;
            ImageCanvas.VisualsRemove += ImageCanvas_VisualsRemove;
        }

        public DrawCanvas ImageShow => ImageCanvas;

        public Zoombox Zoombox1 => ZoomBox;

        public IReadOnlyList<DVCircleText> FocusCircles => GetFocusCircles();

        public DVCircleText? SelectedFocusCircle => EditorContext.SelectionVisual.PrimarySelectedVisual as DVCircleText;

        public event EventHandler<ConoscopeFocusCircleCalculationRequestedEventArgs>? FocusCircleCalculationRequested;
        public event EventHandler<ConoscopeFocusCircleEditRequestedEventArgs>? FocusCircleEditRequested;
        public event EventHandler? FocusCirclesChanged;
        public event EventHandler? FocusCircleSelectionChanged;

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

        public void SetFocusCircleBoundary(Point center, double radius)
        {
            focusCircleBoundaryCenter = center;
            focusCircleBoundaryRadius = Math.Max(0, radius);
            hasFocusCircleBoundary = focusCircleBoundaryRadius > 0;
        }

        public void ClearFocusCircleBoundary()
        {
            hasFocusCircleBoundary = false;
            focusCircleBoundaryRadius = 0;
        }

        public void Dispose()
        {
            ZoomBox.ContentMatrixChanged -= ZoomBox_ContentMatrixChanged;
            EditorContext.SelectionVisual.SelectionChanged -= SelectionVisual_SelectionChanged;
            ImageCanvas.PreviewMouseRightButtonDown -= ImageCanvas_PreviewMouseRightButtonDown;
            ImageCanvas.ContextMenuOpening -= ImageCanvas_ContextMenuOpening;
            ImageCanvas.VisualsAdd -= ImageCanvas_VisualsAdd;
            ImageCanvas.VisualsRemove -= ImageCanvas_VisualsRemove;
            ClearCore(preserveFocusCircles: false);
            UntrackAllFocusCircles();
            ImageCanvas.ContextMenu = null;
            editFocusCircleByPolarMenuItem.Click -= EditFocusCircleByPolarMenuItem_Click;
            calculateFocusCircleMenuItem.Click -= CalculateFocusCircleMenuItem_Click;
            clearFocusCircleMenuItem.Click -= ClearFocusCircleMenuItem_Click;
            focusCircleDrawTool.Dispose();
            focusCircleEraseTool.Dispose();
            EditorContext.SelectionVisual.Dispose();
            EditorContext.MouseInfoProvider.Dispose();
            ZoomBox.Child = null;
            GC.SuppressFinalize(this);
        }

        private void InitializeFocusCircleContextMenu()
        {
            editFocusCircleByPolarMenuItem.Header = Properties.Resources.Conoscope_EditByAngleLength;
            editFocusCircleByPolarMenuItem.Click += EditFocusCircleByPolarMenuItem_Click;
            calculateFocusCircleMenuItem.Click += CalculateFocusCircleMenuItem_Click;
            clearFocusCircleMenuItem.Click += ClearFocusCircleMenuItem_Click;

            ImageCanvas.ContextMenu = focusCircleContextMenu;
            UpdateCanvasCursor();
        }

        private void SelectionVisual_SelectionChanged(object? sender, EventArgs e)
        {
            FocusCircleSelectionChanged?.Invoke(this, EventArgs.Empty);
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

        public void ZoomToImageRect(Rect imageRect)
        {
            if (CheckAccess())
            {
                ZoomToImageRectCore(imageRect);
            }
            else
            {
                Dispatcher.BeginInvoke(new Action(() => ZoomToImageRectCore(imageRect)));
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

        private void ZoomToImageRectCore(Rect imageRect)
        {
            ZoomBox.ZoomToContentRect(imageRect);
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
            focusCircleContextMenu.Items.Clear();
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
            calculateFocusCircleMenuItem.Header = contextMenuFocusCircle == null ? Properties.Resources.Conoscope_CalculateAllFocusPoints : string.Format(Properties.Resources.Conoscope_CalculateFocusPoint, focusCircleName);
            clearFocusCircleMenuItem.Header = focusCircles.Count > 1 ? Properties.Resources.Conoscope_ClearAllFocusPoints : Properties.Resources.Conoscope_ClearFocusPoint;

            if (contextMenuFocusCircle != null)
            {
                focusCircleContextMenu.Items.Add(editFocusCircleByPolarMenuItem);
                focusCircleContextMenu.Items.Add(new Separator());

                foreach (MenuItem menuItem in focusCirclePropertyContextMenu.GetContextMenuItems(contextMenuFocusCircle))
                {
                    focusCircleContextMenu.Items.Add(menuItem);
                }

                focusCircleContextMenu.Items.Add(new Separator());
            }

            focusCircleContextMenu.Items.Add(calculateFocusCircleMenuItem);
            focusCircleContextMenu.Items.Add(new Separator());
            focusCircleContextMenu.Items.Add(clearFocusCircleMenuItem);
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

        private void EditFocusCircleByPolarMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (contextMenuFocusCircle == null)
            {
                return;
            }

            FocusCircleEditRequested?.Invoke(this, new ConoscopeFocusCircleEditRequestedEventArgs(contextMenuFocusCircle));
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
                RadiusY = MinimumFocusCircleRadius,
                Brush = Brushes.Transparent,
                Pen = new Pen(Brushes.DeepSkyBlue, 2),
                Text = $"Focus_{id}",
            };
            properties.Foreground = Brushes.White;
            properties.FontWeight = FontWeights.SemiBold;
            properties.Msg = string.Empty;
            return new DVCircleText(properties);
        }

        internal void ReplaceFocusCirclesFromPoiPoints(IEnumerable<PoiPoint> poiPoints)
        {
            ArgumentNullException.ThrowIfNull(poiPoints);

            ClearFocusCircleSelection();
            SetFocusCircleDrawMode(false);
            SetFocusCircleEraseMode(false);
            contextMenuFocusCircle = null;

            suspendFocusCircleTracking = true;
            try
            {
                foreach (DVCircleText circle in GetFocusCircles())
                {
                    RemoveFocusCircle(circle);
                }

                focusCircleSequence = 1;
                foreach (PoiPoint poiPoint in poiPoints.Where(static item => item.PointType == GraphicTypes.Circle))
                {
                    DVCircleText circle = CreateFocusCircle(poiPoint, focusCircleSequence);
                    AttachFocusCircle(circle);
                    focusCircleSequence++;
                }
            }
            finally
            {
                suspendFocusCircleTracking = false;
            }

            FocusCirclesChanged?.Invoke(this, EventArgs.Empty);
        }

        private DVCircleText CreateFocusCircle(PoiPoint poiPoint, int id)
        {
            double radius = Math.Max(poiPoint.PixWidth / 2, MinimumFocusCircleRadius);
            double radiusY = Math.Max(poiPoint.PixHeight / 2, MinimumFocusCircleRadius);
            string text = string.IsNullOrWhiteSpace(poiPoint.Name) ? $"Focus_{id}" : poiPoint.Name;
            CircleTextProperties properties = new()
            {
                Id = id,
                Name = poiPoint.Id.ToString(),
                Center = new Point(poiPoint.PixX, poiPoint.PixY),
                Radius = radius,
                RadiusY = radiusY,
                Brush = Brushes.Transparent,
                Pen = new Pen(Brushes.DeepSkyBlue, 2),
                Text = text,
            };
            properties.Foreground = Brushes.White;
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

            TrackFocusCircle(circle);
            ImageCanvas.TopVisual(circle);
        }

        internal void SelectFocusCircle(DVCircleText circle)
        {
            EditorContext.SelectionVisual.SetRender(circle);
            ImageCanvas.TopVisual(circle);
        }

        internal void RefreshFocusCircleSelection()
        {
            EditorContext.SelectionVisual.Render();
        }

        internal bool CanCreateFocusCircleAt(Point center)
        {
            if (!hasFocusCircleBoundary)
            {
                return true;
            }

            return (center - focusCircleBoundaryCenter).Length <= focusCircleBoundaryRadius;
        }

        internal double ClampFocusCircleRadius(Point center, double radius)
        {
            if (!hasFocusCircleBoundary)
            {
                return radius;
            }

            double distance = (center - focusCircleBoundaryCenter).Length;
            double maxRadius = Math.Max(0, focusCircleBoundaryRadius - distance);
            return Math.Max(0, Math.Min(radius, maxRadius));
        }

        internal void ConstrainFocusCircleToBoundary(DVCircleText circle)
        {
            if (!hasFocusCircleBoundary || isAdjustingFocusCircleBoundary)
            {
                return;
            }

            CircleTextProperties attribute = circle.Attribute;
            double radiusX = Math.Max(attribute.Radius, MinimumFocusCircleRadius);
            double rawRadiusY = attribute.RadiusY > 0 ? attribute.RadiusY : attribute.Radius;
            double radiusY = Math.Max(rawRadiusY, MinimumFocusCircleRadius);
            double requiredRadius = Math.Max(radiusX, radiusY);
            Point center = attribute.Center;
            Vector delta = center - focusCircleBoundaryCenter;
            double distance = delta.Length;
            double maxCenterDistance = Math.Max(0, focusCircleBoundaryRadius - requiredRadius);
            bool changed = false;

            isAdjustingFocusCircleBoundary = true;
            try
            {
                if (distance > maxCenterDistance)
                {
                    if (distance <= double.Epsilon)
                    {
                        center = new Point(focusCircleBoundaryCenter.X + maxCenterDistance, focusCircleBoundaryCenter.Y);
                    }
                    else
                    {
                        double scale = maxCenterDistance / distance;
                        center = new Point(
                            focusCircleBoundaryCenter.X + delta.X * scale,
                            focusCircleBoundaryCenter.Y + delta.Y * scale);
                    }

                    attribute.Center = center;
                    changed = true;
                }

                double centerDistance = (center - focusCircleBoundaryCenter).Length;
                double maxRadius = Math.Max(0, focusCircleBoundaryRadius - centerDistance);
                double clampedRadius = Math.Max(0, Math.Min(attribute.Radius, maxRadius));
                if (!AreClose(attribute.Radius, clampedRadius))
                {
                    attribute.Radius = clampedRadius;
                    changed = true;
                }

                double clampedRadiusY = Math.Max(0, Math.Min(rawRadiusY, maxRadius));
                if (attribute.RadiusY > 0 && !AreClose(attribute.RadiusY, clampedRadiusY))
                {
                    attribute.RadiusY = clampedRadiusY;
                    changed = true;
                }
            }
            finally
            {
                isAdjustingFocusCircleBoundary = false;
            }

            if (changed)
            {
                circle.Render();
                RefreshFocusCircleSelection();
            }
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

            UntrackFocusCircle(circle);
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

        private void TrackFocusCircle(DVCircleText circle)
        {
            if (!trackedFocusCircles.Add(circle))
            {
                return;
            }

            circle.Attribute.PropertyChanged -= FocusCircleAttribute_PropertyChanged;
            circle.Attribute.PropertyChanged += FocusCircleAttribute_PropertyChanged;
        }

        private void UntrackFocusCircle(DVCircleText circle)
        {
            if (!trackedFocusCircles.Remove(circle))
            {
                return;
            }

            circle.Attribute.PropertyChanged -= FocusCircleAttribute_PropertyChanged;
        }

        private void UntrackAllFocusCircles()
        {
            foreach (DVCircleText circle in trackedFocusCircles.ToArray())
            {
                UntrackFocusCircle(circle);
            }
        }

        private void FocusCircleAttribute_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (suspendFocusCircleTracking || isAdjustingFocusCircleBoundary)
            {
                return;
            }

            if (sender is CircleTextProperties properties
                && e.PropertyName is nameof(CircleTextProperties.Center)
                    or nameof(CircleTextProperties.Radius)
                    or nameof(CircleTextProperties.RadiusY))
            {
                DVCircleText? circle = trackedFocusCircles.FirstOrDefault(item => ReferenceEquals(item.Attribute, properties));
                if (circle != null)
                {
                    ConstrainFocusCircleToBoundary(circle);
                }
            }

            if (e.PropertyName is nameof(CircleTextProperties.Center)
                or nameof(CircleTextProperties.Radius)
                or nameof(CircleTextProperties.RadiusY)
                or nameof(CircleTextProperties.Text)
                or nameof(CircleTextProperties.Id))
            {
                FocusCirclesChanged?.Invoke(this, EventArgs.Empty);
            }
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

        private static bool AreClose(double left, double right)
        {
            return Math.Abs(left - right) < 0.000001;
        }
    }
}
