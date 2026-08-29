using ColorVision.Common.MVVM;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw
{
    public class BrushManagerConfig : ViewModelBase
    {
        [DisplayName("颜色"), JsonIgnore]
        public Brush StrokeBrush
        {
            get => _strokeBrush;
            set
            {
                Brush next = value ?? Brushes.Red;
                if (Equals(_strokeBrush, next))
                {
                    return;
                }

                _strokeBrush = next;
                OnPropertyChanged();
            }
        }
        private Brush _strokeBrush = Brushes.Red;

        [Browsable(false)]
        [JsonProperty(nameof(StrokeBrush))]
        public string SerializedStrokeBrush
        {
            get => TextStyleSerialization.SerializeBrush(StrokeBrush);
            set
            {
                StrokeBrush = TextStyleSerialization.DeserializeBrush(value, Brushes.Red);
                OnPropertyChanged();
            }
        }

        [DisplayName("荧光笔")]
        public bool IsHighlighter
        {
            get => _isHighlighter;
            set
            {
                if (_isHighlighter == value)
                {
                    return;
                }

                _isHighlighter = value;
                OnPropertyChanged();
            }
        }
        private bool _isHighlighter;

        public double StrokeThickness
        {
            get => _strokeThickness;
            set
            {
                if (!double.IsFinite(value))
                    return;

                double next = Math.Max(1, value);
                if (_strokeThickness == next)
                {
                    return;
                }

                _strokeThickness = next;
                OnPropertyChanged();
            }
        }
        private double _strokeThickness = 4;

        [DisplayName("采样间距")]
        public double SampleSpacing
        {
            get => _sampleSpacing;
            set
            {
                if (!double.IsFinite(value))
                    return;

                double next = Math.Max(0.5, value);
                if (_sampleSpacing == next)
                {
                    return;
                }

                _sampleSpacing = next;
                OnPropertyChanged();
            }
        }
        private double _sampleSpacing = 2;
    }

    public class BrushStrokeProperties : BaseProperties
    {
        [Browsable(false), Category("Brush")]
        public Pen Pen
        {
            get => _pen;
            set
            {
                _pen = value;
                OnPropertyChanged();
            }
        }
        private Pen _pen = new Pen(Brushes.Red, 1);

        [Category("Brush"), DisplayName("颜色"), JsonIgnore]
        public Brush Brush
        {
            get => Pen.Brush;
            set
            {
                Pen writablePen = EnsureWritablePen();
                Brush next = value ?? Brushes.Red;
                if (Equals(writablePen.Brush, next))
                {
                    return;
                }

                writablePen.Brush = next;
                OnPropertyChanged();
            }
        }

        [Browsable(false)]
        [JsonProperty(nameof(Brush))]
        public string SerializedBrush
        {
            get => TextStyleSerialization.SerializeBrush(Brush);
            set
            {
                Brush = TextStyleSerialization.DeserializeBrush(value, Brushes.Red);
                OnPropertyChanged();
            }
        }

        [Browsable(false)]
        public double ScreenThickness
        {
            get => _screenThickness;
            set
            {
                if (!double.IsFinite(value))
                    return;

                double next = Math.Max(1, value);
                if (_screenThickness == next)
                {
                    return;
                }

                _screenThickness = next;
                OnPropertyChanged();
            }
        }
        private double _screenThickness = 4;

        [Category("Brush"), DisplayName("笔宽")]
        public double StrokeThickness
        {
            get => ScreenThickness;
            set
            {
                if (!double.IsFinite(value))
                    return;

                double next = Math.Max(1, value);
                if (ScreenThickness == next)
                {
                    return;
                }

                ScreenThickness = next;
                Pen writablePen = EnsureWritablePen();
                writablePen.Thickness = next;
                OnPropertyChanged();
            }
        }

        [Browsable(false)]
        public List<Point> Points { get; set; } = new List<Point>();

        private Pen EnsureWritablePen()
        {
            if (_pen.IsFrozen)
            {
                _pen = _pen.Clone();
            }

            return _pen;
        }
    }

    public class DVBrushStroke : DrawingVisualBase<BrushStrokeProperties>, IDrawingVisual, ILayoutScaleDrawingVisual, ICompactInspectorProvider
    {
        public Pen Pen
        {
            get => Attribute.Pen;
            set => Attribute.Pen = value;
        }

        public List<Point> Points => Attribute.Points;

        public DVBrushStroke()
        {
            Attribute = new BrushStrokeProperties();
            Attribute.PropertyChanged += Attribute_PropertyChanged;
        }

        public DVBrushStroke(BrushStrokeProperties attribute)
        {
            Attribute = attribute;
            Attribute.Points ??= new List<Point>();
            Attribute.PropertyChanged += Attribute_PropertyChanged;
        }

        private void Attribute_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(BrushStrokeProperties.ScreenThickness))
            {
                Render();
            }
        }

        public void ApplyLayoutScale(DrawingVisualScaleContext context)
        {
            double scale = context.Scale;
            if (!double.IsFinite(scale) || scale <= 0)
            {
                scale = 1;
            }

            double targetThickness = TextRenderCore.NormalizeFontSize(Attribute.ScreenThickness * scale * 10) / 10;
            Pen pen = Pen;
            if (pen.Thickness == targetThickness)
            {
                return;
            }

            if (pen.IsFrozen)
            {
                pen = pen.Clone();
                pen.Thickness = targetThickness;
                Pen = pen;
            }
            else
            {
                pen.Thickness = targetThickness;
                Render();
            }
        }

        public override void Render()
        {
            using DrawingContext dc = RenderOpen();

            if (Points.Count == 1)
            {
                double radius = Math.Max(Pen.Thickness / 2, 0.5);
                dc.DrawEllipse(Pen.Brush, null, Points[0], radius, radius);
                return;
            }

            if (Points.Count < 2)
            {
                return;
            }

            Pen drawPen = PrepareDrawPen(Pen);

            StreamGeometry geometry = new();
            using (StreamGeometryContext geometryContext = geometry.Open())
            {
                geometryContext.BeginFigure(Points[0], isFilled: false, isClosed: false);
                for (int i = 1; i < Points.Count; i++)
                {
                    geometryContext.LineTo(Points[i], isStroked: true, isSmoothJoin: true);
                }
            }

            if (geometry.CanFreeze)
            {
                geometry.Freeze();
            }

            dc.DrawGeometry(null, drawPen, geometry);
        }

        private static Pen PrepareDrawPen(Pen pen)
        {
            if (pen.StartLineCap == PenLineCap.Round && pen.EndLineCap == PenLineCap.Round && pen.LineJoin == PenLineJoin.Round)
            {
                return pen;
            }

            Pen drawPen = pen.CloneCurrentValue();
            drawPen.StartLineCap = PenLineCap.Round;
            drawPen.EndLineCap = PenLineCap.Round;
            drawPen.LineJoin = PenLineJoin.Round;
            if (drawPen.CanFreeze)
            {
                drawPen.Freeze();
            }

            return drawPen;
        }

        public override Rect GetRect()
        {
            Rect rect = PointCollectionGeometry.GetBounds(Points);
            if (rect.IsEmpty)
                return Rect.Empty;
            double halfThickness = Pen.Thickness / 2;
            rect.Inflate(halfThickness, halfThickness);
            return rect;
        }

        public override void SetRect(Rect rect)
        {
            if (Points.Count == 0)
            {
                return;
            }

            Rect currentRect = GetRect();
            if (currentRect.IsEmpty)
            {
                return;
            }

            if (currentRect.Width == 0 && currentRect.Height == 0)
            {
                Vector offset = rect.Location - currentRect.Location;
                for (int i = 0; i < Points.Count; i++)
                {
                    Points[i] += offset;
                }
                Render();
                return;
            }

            for (int i = 0; i < Points.Count; i++)
            {
                double normalizedX = currentRect.Width == 0 ? 0 : (Points[i].X - currentRect.X) / currentRect.Width;
                double normalizedY = currentRect.Height == 0 ? 0 : (Points[i].Y - currentRect.Y) / currentRect.Height;

                double targetX = rect.X + normalizedX * rect.Width;
                double targetY = rect.Y + normalizedY * rect.Height;

                if (currentRect.Width == 0)
                {
                    targetX = rect.X + rect.Width / 2;
                }

                if (currentRect.Height == 0)
                {
                    targetY = rect.Y + rect.Height / 2;
                }

                Points[i] = new Point(targetX, targetY);
            }

            Render();
        }

        public IEnumerable<CompactInspectorItem> GetCompactInspectorItems()
        {
            return new CompactInspectorItem[]
            {
                new CompactInspectorPropertyItem { Source = Attribute, PropertyName = nameof(Attribute.Brush), Order = 10, EditorKind = CompactInspectorEditorKind.Brush, ToolTip = ColorVision.ImageEditor.Properties.Resources.Draw_Color },
                new CompactInspectorPropertyItem { Source = Attribute, PropertyName = nameof(Attribute.StrokeThickness), Icon = CompactInspectorIcons.CreateText("━"), Width = 56, Order = 20, EditorKind = CompactInspectorEditorKind.Number, ToolTip = ColorVision.ImageEditor.Properties.Resources.Draw_StrokeWidth },
            };
        }
    }

    public class BrushManager : DragDrawingToolBase
    {
        private DVBrushStroke? _currentStroke;
        private bool _previewRenderPending;

        public BrushManagerConfig Config { get; } = new BrushManagerConfig();

        public BrushManager(DrawEditorContext context) : base(context)
        {
            Order = 9;
            Icon = new TextBlock { Text = "B" };
        }

        private Brush CreateDisplayBrush()
        {
            Brush brush = Config.StrokeBrush?.CloneCurrentValue() ?? Brushes.Red.CloneCurrentValue();
            if (Config.IsHighlighter)
            {
                brush.Opacity = Math.Min(brush.Opacity, 0.35);
            }

            return brush;
        }

        protected override IEnumerable<CompactInspectorItem> BuildCompactInspectorItems()
        {
            return new CompactInspectorItem[]
            {
                new CompactInspectorPropertyItem { Source = Config, PropertyName = nameof(Config.StrokeBrush), Order = 10, EditorKind = CompactInspectorEditorKind.Brush, ToolTip = ColorVision.ImageEditor.Properties.Resources.Draw_Color },
                new CompactInspectorPropertyItem { Source = Config, PropertyName = nameof(Config.IsHighlighter), Icon = CompactInspectorIcons.CreateText("▨"), Order = 20, EditorKind = CompactInspectorEditorKind.Toggle, ToolTip = ColorVision.ImageEditor.Properties.Resources.Draw_HighlighterMode },
                new CompactInspectorPropertyItem { Source = Config, PropertyName = nameof(Config.StrokeThickness), Icon = CompactInspectorIcons.CreateText("━"), Width = 56, Order = 30, EditorKind = CompactInspectorEditorKind.Number, ToolTip = ColorVision.ImageEditor.Properties.Resources.Draw_StrokeWidth },
                new CompactInspectorPropertyItem { Source = Config, PropertyName = nameof(Config.SampleSpacing), Icon = CompactInspectorIcons.CreateText("⋯"), Width = 56, Order = 40, EditorKind = CompactInspectorEditorKind.Number, ToolTip = ColorVision.ImageEditor.Properties.Resources.Draw_SampleSpacing },
            };
        }

        protected override bool TryHandleExistingSelection(Point point)
        {
            ClearCurrentSelection();
            return false;
        }

        protected override void OnActivated()
        {
            DrawCanvas.LostMouseCapture += DrawCanvas_LostMouseCapture;
        }

        protected override void OnDeactivated()
        {
            DrawCanvas.LostMouseCapture -= DrawCanvas_LostMouseCapture;
            CancelPreviewRender();
            if (_currentStroke != null && DrawCanvas.ContainsVisual(_currentStroke))
            {
                DrawCanvas.RemoveOverlayVisual(_currentStroke);
            }

            _currentStroke = null;
        }

        private void DrawCanvas_LostMouseCapture(object sender, MouseEventArgs e)
        {
            if (IsMouseDown)
                IsChecked = false;
        }

        protected override void OnBeginDraw(Point startPoint, MouseButtonEventArgs e)
        {
            ClearCurrentSelection();

            BrushStrokeProperties properties = new BrushStrokeProperties
            {
                Id = GetNextDrawingVisualId(),
                ScreenThickness = Config.StrokeThickness,
                Pen = new Pen(CreateDisplayBrush(), Config.StrokeThickness / GetSafeZoomRatio())
                {
                    StartLineCap = PenLineCap.Round,
                    EndLineCap = PenLineCap.Round,
                    LineJoin = PenLineJoin.Round,
                },
                Points = new List<Point> { startPoint }
            };

            _currentStroke = new DVBrushStroke(properties);
            _currentStroke.Render();
            DrawCanvas.AddOverlayVisual(_currentStroke);
            e.Handled = true;
        }

        protected override void OnUpdateDraw(Point currentPoint, MouseEventArgs e)
        {
            if (_currentStroke == null || !DrawCanvas.ContainsVisual(_currentStroke))
            {
                CancelPreviewRender();
                _currentStroke = null;
                return;
            }

            double minSpacing = Config.SampleSpacing / GetSafeZoomRatio();
            Point lastPoint = _currentStroke.Points[^1];
            Vector delta = currentPoint - lastPoint;

            if (delta.X * delta.X + delta.Y * delta.Y < minSpacing * minSpacing)
            {
                return;
            }

            _currentStroke.Points.Add(currentPoint);
            RequestPreviewRender();
            e.Handled = true;
        }

        protected override void OnEndDraw(Point endPoint, MouseButtonEventArgs e)
        {
            CancelPreviewRender();
            if (_currentStroke == null || !DrawCanvas.ContainsVisual(_currentStroke))
            {
                _currentStroke = null;
                return;
            }

            if ((_currentStroke.Points[^1] - endPoint).Length > 0.1)
            {
                _currentStroke.Points.Add(endPoint);
            }

            DVBrushStroke completedStroke = _currentStroke;
            if (DrawCanvas.ContainsVisual(completedStroke))
            {
                DrawCanvas.RemoveOverlayVisual(completedStroke);
            }

            bool requireActiveTool = IsChecked;
            _currentStroke = null;
            completedStroke.Render();
            ActionCommand? creationCommand = DrawCanvas.AddVisualCommandCore(completedStroke);
            if (creationCommand == null || (requireActiveTool && !IsChecked) || !DrawCanvas.ContainsVisual(completedStroke))
            {
                if (DrawCanvas.ContainsVisual(completedStroke))
                    DrawCanvas.RemoveVisual(completedStroke);
                if (creationCommand != null)
                    DrawCanvas.DiscardActionCommand(creationCommand);
                e.Handled = true;
                return;
            }

            SelectVisual(completedStroke);
            e.Handled = true;
        }

        private void RequestPreviewRender()
        {
            if (_previewRenderPending)
            {
                return;
            }

            _previewRenderPending = true;
            CompositionTarget.Rendering += RenderPreviewOnNextFrame;
        }

        private void RenderPreviewOnNextFrame(object? sender, EventArgs e)
        {
            CancelPreviewRender();
            if (_currentStroke != null && DrawCanvas.ContainsVisual(_currentStroke))
            {
                _currentStroke.Render();
            }
        }

        private void CancelPreviewRender()
        {
            if (!_previewRenderPending)
            {
                return;
            }

            CompositionTarget.Rendering -= RenderPreviewOnNextFrame;
            _previewRenderPending = false;
        }
    }
}
