#pragma warning disable CA1822,CA1863
using ColorVision.Engine.Templates.POI;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Conoscope
{
    public enum FocusCircleInteractionMode
    {
        Browse,
        Select,
        Draw,
        Erase
    }

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

        private readonly FocusCircleEditor focusCircleEditor;
        private int disposed;

        public ConoscopeImageHost()
        {
            InitializeComponent();

            EditorContext = new DrawEditorContext(ImageCanvas, ZoomBox);
            EditorContext.SelectionVisual = new SelectEditorVisual(EditorContext);
            focusCircleEditor = new FocusCircleEditor(EditorContext);
            focusCircleEditor.CalculationRequested += FocusCircleEditor_CalculationRequested;
            focusCircleEditor.EditRequested += FocusCircleEditor_EditRequested;
            focusCircleEditor.CirclesChanged += FocusCircleEditor_CirclesChanged;
            focusCircleEditor.SelectionChanged += FocusCircleEditor_SelectionChanged;
            ZoomBox.ContentMatrixChanged += ZoomBox_ContentMatrixChanged;
        }

        private DrawEditorContext EditorContext { get; }

        internal DrawCanvas DrawingCanvas => ImageCanvas;

        internal Zoombox Viewport => ZoomBox;

        public ImageSource? Source => ImageCanvas.Source;

        public IReadOnlyList<DVCircleText> FocusCircles => focusCircleEditor.Circles;

        public DVCircleText? SelectedFocusCircle => focusCircleEditor.SelectedCircle;

        public event EventHandler<ConoscopeFocusCircleCalculationRequestedEventArgs>? FocusCircleCalculationRequested;
        public event EventHandler<ConoscopeFocusCircleEditRequestedEventArgs>? FocusCircleEditRequested;
        public event EventHandler? FocusCirclesChanged;
        public event EventHandler? FocusCircleSelectionChanged;
        public event EventHandler? ZoomChanged;

        public FocusCircleInteractionMode InteractionMode
        {
            get => focusCircleEditor.InteractionMode;
            set => focusCircleEditor.SetInteractionMode(value);
        }

        public void ResetDocument()
        {
            ClearCore(preserveFocusCircles: false);
            focusCircleEditor.ResetDocumentState();
        }

        public void ReplaceDisplayedImage(ImageSource imageSource)
        {
            ArgumentNullException.ThrowIfNull(imageSource);
            ClearCore(preserveFocusCircles: true);
            ImageCanvas.Source = imageSource;
            ImageCanvas.RaiseImageInitialized();
        }

        public void ClearFocusCircles()
        {
            focusCircleEditor.ClearCircles();
        }

        public void SetFocusCircleBoundary(Point center, double radius)
        {
            focusCircleEditor.SetBoundary(center, radius);
        }

        public void ClearFocusCircleBoundary()
        {
            focusCircleEditor.ClearBoundary();
        }

        internal void ReplaceFocusCirclesFromPoiPoints(IEnumerable<PoiPoint> poiPoints)
        {
            focusCircleEditor.ReplaceFromPoiPoints(poiPoints);
        }

        internal void RefreshFocusCircleSelection()
        {
            focusCircleEditor.RefreshSelection();
        }

        internal void ConstrainFocusCircleToBoundary(DVCircleText circle)
        {
            focusCircleEditor.ConstrainCircleToBoundary(circle);
        }

        public Point GetPointerPosition()
        {
            return Mouse.GetPosition(ImageCanvas);
        }

        public void SetPanModifier(ModifierKeys modifiers)
        {
            ZoomBox.ActivateOn = modifiers;
        }

        public void ResetInteractionCursor()
        {
            ZoomBox.Cursor = Cursors.Arrow;
            ImageCanvas.Cursor = Cursors.Arrow;
        }

        public void ZoomActualSize()
        {
            ZoomBox.ZoomNone();
        }

        public void ZoomToFill()
        {
            ZoomBox.ZoomUniformToFill();
        }

        public void ZoomToFit()
        {
            ZoomBox.ZoomUniform();
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
                ZoomBox.ZoomToContentRect(imageRect);
            }
            else
            {
                Dispatcher.BeginInvoke(new Action(() => ZoomBox.ZoomToContentRect(imageRect)));
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0)
            {
                return;
            }

            ZoomBox.ContentMatrixChanged -= ZoomBox_ContentMatrixChanged;
            focusCircleEditor.CalculationRequested -= FocusCircleEditor_CalculationRequested;
            focusCircleEditor.EditRequested -= FocusCircleEditor_EditRequested;
            focusCircleEditor.CirclesChanged -= FocusCircleEditor_CirclesChanged;
            focusCircleEditor.SelectionChanged -= FocusCircleEditor_SelectionChanged;
            focusCircleEditor.Dispose();
            EditorContext.MouseInfoProvider.Dispose();
            ImageCanvas.Source = null;
            ImageCanvas.Dispose();
            ZoomBox.Child = null;
            FocusCircleCalculationRequested = null;
            FocusCircleEditRequested = null;
            FocusCirclesChanged = null;
            FocusCircleSelectionChanged = null;
            ZoomChanged = null;
            GC.SuppressFinalize(this);
        }

        private void ClearCore(bool preserveFocusCircles)
        {
            FocusCircleInteractionMode retainedInteractionMode = InteractionMode;
            DVCircleText[] retainedCircles = focusCircleEditor.BeginViewportClear(preserveFocusCircles);
            try
            {
                ImageCanvas.Clear();
                ImageCanvas.Source = null;
                ImageCanvas.UpdateLayout();
            }
            finally
            {
                focusCircleEditor.EndViewportClear(preserveFocusCircles, retainedCircles);
                InteractionMode = preserveFocusCircles
                    ? retainedInteractionMode
                    : FocusCircleInteractionMode.Browse;
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

        private void ZoomBox_ContentMatrixChanged(object? sender, EventArgs e)
        {
            UpdateDrawingVisualScale();
            ImageCanvas.ApplyLayoutScaleToVisuals();
            ZoomChanged?.Invoke(this, EventArgs.Empty);
        }

        private void UpdateDrawingVisualScale()
        {
            double zoomRatio = ZoomBox.ContentMatrix.M11;
            ImageCanvas.Scale = double.IsNaN(zoomRatio) || double.IsInfinity(zoomRatio) || zoomRatio <= 0 ? 1 : 1 / zoomRatio;
        }

        private void FocusCircleEditor_CalculationRequested(object? sender, ConoscopeFocusCircleCalculationRequestedEventArgs e)
        {
            FocusCircleCalculationRequested?.Invoke(this, e);
        }

        private void FocusCircleEditor_EditRequested(object? sender, ConoscopeFocusCircleEditRequestedEventArgs e)
        {
            FocusCircleEditRequested?.Invoke(this, e);
        }

        private void FocusCircleEditor_CirclesChanged(object? sender, EventArgs e)
        {
            FocusCirclesChanged?.Invoke(this, EventArgs.Empty);
        }

        private void FocusCircleEditor_SelectionChanged(object? sender, EventArgs e)
        {
            FocusCircleSelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        private sealed class FocusCircleEditor : IDisposable
        {
            private readonly DrawEditorContext editorContext;
            private readonly DrawCanvas canvas;
            private readonly Zoombox zoombox;
            private readonly ContextMenu contextMenu = new();
            private readonly DrawingVisualBaseDVContextMenu propertyContextMenu;
            private readonly MenuItem editByPolarMenuItem = new();
            private readonly MenuItem calculateMenuItem = new();
            private readonly MenuItem clearMenuItem = new();
            private readonly HashSet<DVCircleText> trackedCircles = new();
            private readonly FocusCircleDrawTool drawTool;
            private readonly EraseManager eraseTool;
            private readonly DispatcherTimer changedTimer;

            private DVCircleText? contextMenuCircle;
            private bool isEditMode;
            private bool isSelectionEnabled;
            private bool hasBoundary;
            private bool isAdjustingBoundary;
            private bool suspendTracking;
            private int circleSequence = 1;
            private int disposed;
            private Point boundaryCenter;
            private double boundaryRadius;

            public FocusCircleEditor(DrawEditorContext editorContext)
            {
                this.editorContext = editorContext;
                canvas = editorContext.DrawCanvas;
                zoombox = editorContext.Zoombox;
                propertyContextMenu = new DrawingVisualBaseDVContextMenu(editorContext);
                drawTool = new FocusCircleDrawTool(editorContext, this);
                eraseTool = new EraseManager(editorContext)
                {
                    CanEraseVisual = static visual => visual is DVCircleText
                };
                changedTimer = new DispatcherTimer(DispatcherPriority.Background)
                {
                    Interval = TimeSpan.FromMilliseconds(75)
                };

                changedTimer.Tick += ChangedTimer_Tick;
                InitializeContextMenu();
                editorContext.SelectionVisual.SelectionChanged += SelectionVisual_SelectionChanged;
                canvas.PreviewMouseRightButtonDown += Canvas_PreviewMouseRightButtonDown;
                canvas.PreviewMouseLeftButtonUp += Canvas_PreviewMouseLeftButtonUp;
                canvas.ContextMenuOpening += Canvas_ContextMenuOpening;
                canvas.VisualsAdd += Canvas_VisualsAdd;
                canvas.VisualsRemove += Canvas_VisualsRemove;
            }

            public IReadOnlyList<DVCircleText> Circles => GetCircles();

            public DVCircleText? SelectedCircle => editorContext.SelectionVisual.PrimarySelectedVisual as DVCircleText;

            public bool IsEditMode
            {
                get => isEditMode;
                set
                {
                    if (isEditMode == value)
                    {
                        return;
                    }

                    isEditMode = value;
                    if (isEditMode)
                    {
                        ClearSelection();
                    }

                    RefreshInteractionState();
                }
            }

            public bool IsDrawMode => drawTool.IsChecked;

            public bool IsEraseMode => eraseTool.IsChecked;

            public bool IsSelectionEnabled => isSelectionEnabled;

            public FocusCircleInteractionMode InteractionMode => IsDrawMode
                ? FocusCircleInteractionMode.Draw
                : IsEraseMode
                    ? FocusCircleInteractionMode.Erase
                    : isSelectionEnabled
                        ? FocusCircleInteractionMode.Select
                        : FocusCircleInteractionMode.Browse;

            public void SetInteractionMode(FocusCircleInteractionMode mode)
            {
                SetDrawMode(false);
                SetEraseMode(false);
                SetSelectionEnabled(false);
                IsEditMode = mode != FocusCircleInteractionMode.Browse;

                switch (mode)
                {
                    case FocusCircleInteractionMode.Draw:
                        SetDrawMode(true);
                        break;
                    case FocusCircleInteractionMode.Erase:
                        SetEraseMode(true);
                        break;
                    case FocusCircleInteractionMode.Select:
                        SetSelectionEnabled(true);
                        break;
                }
            }

            public event EventHandler<ConoscopeFocusCircleCalculationRequestedEventArgs>? CalculationRequested;
            public event EventHandler<ConoscopeFocusCircleEditRequestedEventArgs>? EditRequested;
            public event EventHandler? CirclesChanged;
            public event EventHandler? SelectionChanged;

            public void ClearCircles()
            {
                ClearSelection();

                foreach (DVCircleText circle in GetCircles())
                {
                    RemoveCircle(circle);
                }

                contextMenuCircle = null;
            }

            public void SetDrawMode(bool isEnabled)
            {
                drawTool.IsChecked = IsEditMode && isEnabled;
                if (isEnabled)
                {
                    eraseTool.IsChecked = false;
                    ClearSelection();
                }

                RefreshInteractionState();
            }

            public void SetEraseMode(bool isEnabled)
            {
                eraseTool.IsChecked = IsEditMode && isEnabled;
                if (isEnabled)
                {
                    drawTool.IsChecked = false;
                    ClearSelection();
                }

                RefreshInteractionState();
            }

            public void SetSelectionEnabled(bool isEnabled)
            {
                if (isSelectionEnabled == isEnabled)
                {
                    return;
                }

                isSelectionEnabled = isEnabled;
                if (!isSelectionEnabled)
                {
                    ClearSelection();
                }

                RefreshInteractionState();
            }

            public void SetBoundary(Point center, double radius)
            {
                boundaryCenter = center;
                boundaryRadius = Math.Max(0, radius);
                hasBoundary = boundaryRadius > 0;
            }

            public void ClearBoundary()
            {
                hasBoundary = false;
                boundaryRadius = 0;
            }

            public void ResetDocumentState()
            {
                contextMenuCircle = null;
                circleSequence = 1;
                ClearBoundary();
            }

            public DVCircleText[] BeginViewportClear(bool preserveCircles)
            {
                ClearSelection();
                drawTool.IsChecked = false;
                eraseTool.IsChecked = false;

                DVCircleText[] retainedCircles = preserveCircles ? GetCircles() : Array.Empty<DVCircleText>();
                if (!preserveCircles)
                {
                    contextMenuCircle = null;
                }

                suspendTracking = preserveCircles;
                return retainedCircles;
            }

            public void EndViewportClear(bool preserveCircles, IReadOnlyList<DVCircleText> retainedCircles)
            {
                try
                {
                    if (preserveCircles)
                    {
                        foreach (DVCircleText circle in retainedCircles)
                        {
                            AttachCircle(circle);
                        }
                    }
                }
                finally
                {
                    suspendTracking = false;
                }
            }

            public void ReplaceFromPoiPoints(IEnumerable<PoiPoint> poiPoints)
            {
                ArgumentNullException.ThrowIfNull(poiPoints);

                ClearSelection();
                contextMenuCircle = null;

                suspendTracking = true;
                try
                {
                    foreach (DVCircleText circle in GetCircles())
                    {
                        RemoveCircle(circle);
                    }

                    circleSequence = 1;
                    foreach (PoiPoint poiPoint in poiPoints.Where(static item => item.PointType == PoiShape.Circle))
                    {
                        DVCircleText circle = CreateCircle(poiPoint, circleSequence);
                        AttachCircle(circle);
                        circleSequence++;
                    }
                }
                finally
                {
                    suspendTracking = false;
                }

                CirclesChanged?.Invoke(this, EventArgs.Empty);
            }

            public void RefreshSelection()
            {
                editorContext.SelectionVisual.Render();
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref disposed, 1) != 0)
                {
                    return;
                }

                editorContext.SelectionVisual.SelectionChanged -= SelectionVisual_SelectionChanged;
                canvas.PreviewMouseRightButtonDown -= Canvas_PreviewMouseRightButtonDown;
                canvas.PreviewMouseLeftButtonUp -= Canvas_PreviewMouseLeftButtonUp;
                canvas.ContextMenuOpening -= Canvas_ContextMenuOpening;
                canvas.VisualsAdd -= Canvas_VisualsAdd;
                canvas.VisualsRemove -= Canvas_VisualsRemove;
                changedTimer.Stop();
                changedTimer.Tick -= ChangedTimer_Tick;
                editByPolarMenuItem.Click -= EditByPolarMenuItem_Click;
                calculateMenuItem.Click -= CalculateMenuItem_Click;
                clearMenuItem.Click -= ClearMenuItem_Click;
                drawTool.Dispose();
                eraseTool.Dispose();
                editorContext.SelectionVisual.Dispose();
                UntrackAllCircles();
                contextMenu.Items.Clear();
                canvas.ContextMenu = null;
                CalculationRequested = null;
                EditRequested = null;
                CirclesChanged = null;
                SelectionChanged = null;
                GC.SuppressFinalize(this);
            }

            private void InitializeContextMenu()
            {
                editByPolarMenuItem.Header = Properties.Resources.Conoscope_EditByAngleLength;
                editByPolarMenuItem.Click += EditByPolarMenuItem_Click;
                calculateMenuItem.Click += CalculateMenuItem_Click;
                clearMenuItem.Click += ClearMenuItem_Click;
                canvas.ContextMenu = contextMenu;
                UpdateCanvasCursor();
            }

            private void SelectionVisual_SelectionChanged(object? sender, EventArgs e)
            {
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }

            private void RefreshInteractionState()
            {
                if (!IsEditMode)
                {
                    drawTool.IsChecked = false;
                    eraseTool.IsChecked = false;
                }

                if (!isSelectionEnabled)
                {
                    ClearSelection();
                }

                editorContext.IsImageEditMode = isSelectionEnabled;
                UpdateCanvasCursor();
            }

            private void UpdateCanvasCursor()
            {
                if (eraseTool.IsChecked)
                {
                    return;
                }

                Cursor cursor = IsDrawMode ? Cursors.Cross : Cursors.Arrow;
                canvas.Cursor = cursor;
                zoombox.Cursor = cursor;
            }

            private void Canvas_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
            {
                Point point = e.GetPosition(canvas);
                contextMenuCircle = FindCircle(point);
                if (contextMenuCircle != null && isSelectionEnabled)
                {
                    SelectCircle(contextMenuCircle);
                }
                else if (!isSelectionEnabled)
                {
                    ClearSelection();
                }
            }

            private void Canvas_ContextMenuOpening(object sender, ContextMenuEventArgs e)
            {
                contextMenu.Items.Clear();
                IReadOnlyList<DVCircleText> circles = Circles;
                if (circles.Count == 0)
                {
                    e.Handled = true;
                    return;
                }

                if (contextMenuCircle == null)
                {
                    contextMenuCircle = FindCircle(Mouse.GetPosition(canvas));
                }

                string circleName = contextMenuCircle == null ? string.Empty : ResolveCircleName(contextMenuCircle);
                calculateMenuItem.Header = contextMenuCircle == null
                    ? Properties.Resources.Conoscope_CalculateAllFocusPoints
                    : string.Format(Properties.Resources.Conoscope_CalculateFocusPoint, circleName);
                clearMenuItem.Header = circles.Count > 1
                    ? Properties.Resources.Conoscope_ClearAllFocusPoints
                    : Properties.Resources.Conoscope_ClearFocusPoint;

                if (contextMenuCircle != null)
                {
                    contextMenu.Items.Add(editByPolarMenuItem);
                    contextMenu.Items.Add(new Separator());

                    foreach (MenuItem menuItem in propertyContextMenu.GetContextMenuItems(contextMenuCircle))
                    {
                        contextMenu.Items.Add(menuItem);
                    }

                    contextMenu.Items.Add(new Separator());
                }

                contextMenu.Items.Add(calculateMenuItem);
                contextMenu.Items.Add(new Separator());
                contextMenu.Items.Add(clearMenuItem);
            }

            private void CalculateMenuItem_Click(object sender, RoutedEventArgs e)
            {
                DVCircleText[] circles = contextMenuCircle == null ? GetCircles() : new[] { contextMenuCircle };
                if (circles.Length > 0)
                {
                    CalculationRequested?.Invoke(this, new ConoscopeFocusCircleCalculationRequestedEventArgs(circles));
                }
            }

            private void EditByPolarMenuItem_Click(object sender, RoutedEventArgs e)
            {
                if (contextMenuCircle != null)
                {
                    EditRequested?.Invoke(this, new ConoscopeFocusCircleEditRequestedEventArgs(contextMenuCircle));
                }
            }

            private void ClearMenuItem_Click(object sender, RoutedEventArgs e)
            {
                ClearCircles();
            }

            private DVCircleText CreateCircle(Point center)
            {
                int id = circleSequence++;
                return new DVCircleText(CreateCircleProperties(
                    id,
                    center,
                    MinimumFocusCircleRadius,
                    MinimumFocusCircleRadius,
                    $"Focus_{id}"));
            }

            private static DVCircleText CreateCircle(PoiPoint poiPoint, int id)
            {
                double radius = Math.Max(poiPoint.PixWidth / 2, MinimumFocusCircleRadius);
                double radiusY = Math.Max(poiPoint.PixHeight / 2, MinimumFocusCircleRadius);
                string text = string.IsNullOrWhiteSpace(poiPoint.Name) ? $"Focus_{id}" : poiPoint.Name;
                CircleTextProperties properties = CreateCircleProperties(
                    id,
                    new Point(poiPoint.PixX, poiPoint.PixY),
                    radius,
                    radiusY,
                    text);
                properties.Name = poiPoint.Id.ToString();
                return new DVCircleText(properties);
            }

            private static CircleTextProperties CreateCircleProperties(int id, Point center, double radius, double radiusY, string text)
            {
                CircleTextProperties properties = new()
                {
                    Id = id,
                    Center = center,
                    Radius = radius,
                    RadiusY = radiusY,
                    Brush = Brushes.Transparent,
                    Pen = new Pen(Brushes.DeepSkyBlue, 2),
                    Text = text
                };
                properties.Foreground = Brushes.White;
                properties.FontWeight = FontWeights.SemiBold;
                properties.Msg = string.Empty;
                return properties;
            }

            private void AttachCircle(DVCircleText circle)
            {
                if (!canvas.ContainsVisual(circle))
                {
                    canvas.AddVisualCommand(circle);
                }

                TrackCircle(circle);
                canvas.TopVisual(circle);
            }

            private void SelectCircle(DVCircleText circle)
            {
                editorContext.SelectionVisual.SetRender(circle);
                canvas.TopVisual(circle);
            }

            private bool CanCreateCircleAt(Point center)
            {
                return !hasBoundary || (center - boundaryCenter).Length <= boundaryRadius;
            }

            private double ClampCircleRadius(Point center, double radius)
            {
                if (!hasBoundary)
                {
                    return radius;
                }

                double distance = (center - boundaryCenter).Length;
                double maxRadius = Math.Max(0, boundaryRadius - distance);
                return Math.Max(0, Math.Min(radius, maxRadius));
            }

            public void ConstrainCircleToBoundary(DVCircleText circle)
            {
                if (!hasBoundary || isAdjustingBoundary)
                {
                    return;
                }

                CircleTextProperties attribute = circle.Attribute;
                double radiusX = Math.Max(attribute.Radius, MinimumFocusCircleRadius);
                double rawRadiusY = attribute.RadiusY > 0 ? attribute.RadiusY : attribute.Radius;
                double radiusY = Math.Max(rawRadiusY, MinimumFocusCircleRadius);
                double requiredRadius = Math.Max(radiusX, radiusY);
                Point center = attribute.Center;
                Vector delta = center - boundaryCenter;
                double distance = delta.Length;
                double maxCenterDistance = Math.Max(0, boundaryRadius - requiredRadius);
                bool changed = false;

                isAdjustingBoundary = true;
                try
                {
                    if (distance > maxCenterDistance)
                    {
                        if (distance <= double.Epsilon)
                        {
                            center = new Point(boundaryCenter.X + maxCenterDistance, boundaryCenter.Y);
                        }
                        else
                        {
                            double scale = maxCenterDistance / distance;
                            center = new Point(
                                boundaryCenter.X + delta.X * scale,
                                boundaryCenter.Y + delta.Y * scale);
                        }

                        attribute.Center = center;
                        changed = true;
                    }

                    double centerDistance = (center - boundaryCenter).Length;
                    double maxRadius = Math.Max(0, boundaryRadius - centerDistance);
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
                    isAdjustingBoundary = false;
                }

                if (changed)
                {
                    circle.Render();
                    RefreshSelection();
                }
            }

            private void RemoveCircle(DVCircleText circle)
            {
                if (editorContext.SelectionVisual.SelectVisuals.Contains(circle))
                {
                    ClearSelection();
                }

                if (canvas.ContainsVisual(circle))
                {
                    canvas.RemoveVisualCommand(circle);
                }

                UntrackCircle(circle);
            }

            private void Canvas_VisualsAdd(object? sender, VisualChangedEventArgs e)
            {
                if (suspendTracking || e.Visual is not DVCircleText circle)
                {
                    return;
                }

                TrackCircle(circle);
                CirclesChanged?.Invoke(this, EventArgs.Empty);
            }

            private void Canvas_VisualsRemove(object? sender, VisualChangedEventArgs e)
            {
                if (suspendTracking || e.Visual is not DVCircleText circle)
                {
                    return;
                }

                if (ReferenceEquals(contextMenuCircle, circle))
                {
                    contextMenuCircle = null;
                }

                UntrackCircle(circle);
                CirclesChanged?.Invoke(this, EventArgs.Empty);
            }

            private void TrackCircle(DVCircleText circle)
            {
                if (!trackedCircles.Add(circle))
                {
                    return;
                }

                circle.Attribute.PropertyChanged -= CircleAttribute_PropertyChanged;
                circle.Attribute.PropertyChanged += CircleAttribute_PropertyChanged;
            }

            private void UntrackCircle(DVCircleText circle)
            {
                if (trackedCircles.Remove(circle))
                {
                    circle.Attribute.PropertyChanged -= CircleAttribute_PropertyChanged;
                }
            }

            private void UntrackAllCircles()
            {
                foreach (DVCircleText circle in trackedCircles.ToArray())
                {
                    UntrackCircle(circle);
                }
            }

            private void CircleAttribute_PropertyChanged(object? sender, PropertyChangedEventArgs e)
            {
                if (suspendTracking || isAdjustingBoundary)
                {
                    return;
                }

                if (sender is CircleTextProperties properties
                    && e.PropertyName is nameof(CircleTextProperties.Center)
                        or nameof(CircleTextProperties.Radius)
                        or nameof(CircleTextProperties.RadiusY))
                {
                    DVCircleText? circle = trackedCircles.FirstOrDefault(item => ReferenceEquals(item.Attribute, properties));
                    if (circle != null)
                    {
                        ConstrainCircleToBoundary(circle);
                    }
                }

                if (e.PropertyName is nameof(CircleTextProperties.Center)
                    or nameof(CircleTextProperties.Radius)
                    or nameof(CircleTextProperties.RadiusY)
                    or nameof(CircleTextProperties.Text)
                    or nameof(CircleTextProperties.Id))
                {
                    changedTimer.Stop();
                    changedTimer.Start();
                }
            }

            private void Canvas_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
            {
                FlushPendingChange();
            }

            private void ChangedTimer_Tick(object? sender, EventArgs e)
            {
                FlushPendingChange();
            }

            private void FlushPendingChange()
            {
                if (!changedTimer.IsEnabled)
                {
                    return;
                }

                changedTimer.Stop();
                CirclesChanged?.Invoke(this, EventArgs.Empty);
            }

            private void ClearSelection()
            {
                editorContext.SelectionVisual.ClearRender();
            }

            private DVCircleText? FindCircle(Point point)
            {
                IReadOnlyList<DVCircleText> circles = Circles;
                double hitTolerance = 8 * Math.Max(canvas.Scale, 0.2);
                for (int index = circles.Count - 1; index >= 0; index--)
                {
                    DVCircleText circle = circles[index];
                    Point center = circle.Attribute.Center;
                    double radiusX = Math.Max(Math.Abs(circle.Attribute.Radius), MinimumFocusCircleRadius) + hitTolerance;
                    double sourceRadiusY = circle.Attribute.RadiusY > 0 ? circle.Attribute.RadiusY : circle.Attribute.Radius;
                    double radiusY = Math.Max(Math.Abs(sourceRadiusY), MinimumFocusCircleRadius) + hitTolerance;
                    double normalizedX = (point.X - center.X) / radiusX;
                    double normalizedY = (point.Y - center.Y) / radiusY;
                    if (normalizedX * normalizedX + normalizedY * normalizedY <= 1)
                    {
                        return circle;
                    }
                }

                return null;
            }

            private DVCircleText[] GetCircles()
            {
                return canvas.Visuals.OfType<DVCircleText>().ToArray();
            }

            private static string ResolveCircleName(DVCircleText circle)
            {
                return string.IsNullOrWhiteSpace(circle.Attribute.Text)
                    ? $"Focus_{circle.Attribute.Id}"
                    : circle.Attribute.Text;
            }

            private static bool AreClose(double left, double right)
            {
                return Math.Abs(left - right) < 0.000001;
            }

            private sealed class FocusCircleDrawTool : DragDrawingToolBase
            {
                private readonly FocusCircleEditor editor;
                private DVCircleText? draftCircle;

                public FocusCircleDrawTool(DrawEditorContext editorContext, FocusCircleEditor editor) : base(editorContext)
                {
                    this.editor = editor;
                    Order = 3;
                }

                protected override bool TryHandleExistingSelection(Point point)
                {
                    return false;
                }

                protected override void OnBeginDraw(Point startPoint, MouseButtonEventArgs e)
                {
                    if (draftCircle != null || !editor.CanCreateCircleAt(startPoint))
                    {
                        e.Handled = true;
                        return;
                    }

                    ClearCurrentSelection();
                    draftCircle = editor.CreateCircle(startPoint);
                    editor.AttachCircle(draftCircle);
                    e.Handled = true;
                }

                protected override void OnUpdateDraw(Point currentPoint, MouseEventArgs e)
                {
                    if (draftCircle == null)
                    {
                        return;
                    }

                    Point center = draftCircle.Attribute.Center;
                    double radius = Math.Sqrt(Math.Pow(currentPoint.X - center.X, 2) + Math.Pow(currentPoint.Y - center.Y, 2));
                    radius = editor.ClampCircleRadius(center, radius);
                    draftCircle.Attribute.Radius = radius;
                    draftCircle.Attribute.RadiusY = radius;
                    draftCircle.Render();
                    e.Handled = true;
                }

                protected override void OnEndDraw(Point endPoint, MouseButtonEventArgs e)
                {
                    if (draftCircle == null)
                    {
                        return;
                    }

                    DVCircleText circle = draftCircle;
                    draftCircle = null;

                    if (circle.Attribute.Radius < MinimumFocusCircleRadius)
                    {
                        editor.RemoveCircle(circle);
                    }
                    else
                    {
                        editor.ConstrainCircleToBoundary(circle);
                        circle.Render();
                    }

                    e.Handled = true;
                }

                protected override void OnDeactivated()
                {
                    if (draftCircle == null)
                    {
                        return;
                    }

                    editor.RemoveCircle(draftCircle);
                    draftCircle = null;
                }
            }
        }
    }
}
