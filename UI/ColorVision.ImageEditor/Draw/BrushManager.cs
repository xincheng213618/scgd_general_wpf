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
            Attribute.PropertyChanged += (s, e) => Render();
        }

        public DVBrushStroke(BrushStrokeProperties attribute)
        {
            Attribute = attribute;
            Attribute.Points ??= new List<Point>();
            Attribute.PropertyChanged += (s, e) => Render();
        }

        public void ApplyLayoutScale(DrawingVisualScaleContext context)
        {
            Pen pen = Pen;
            if (pen.IsFrozen)
            {
                pen = pen.Clone();
                Pen = pen;
            }

            double scale = context.Scale;
            if (double.IsNaN(scale) || double.IsInfinity(scale) || scale <= 0)
            {
                scale = 1;
            }

            double targetThickness = Attribute.ScreenThickness * scale;
            if (pen.Thickness != targetThickness)
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

            Pen drawPen = Pen;
            drawPen.StartLineCap = PenLineCap.Round;
            drawPen.EndLineCap = PenLineCap.Round;
            drawPen.LineJoin = PenLineJoin.Round;

            for (int i = 1; i < Points.Count; i++)
            {
                dc.DrawLine(drawPen, Points[i - 1], Points[i]);
            }
        }

        public override Rect GetRect()
        {
            if (Points.Count == 0)
            {
                return Rect.Empty;
            }

            double minX = double.MaxValue;
            double minY = double.MaxValue;
            double maxX = double.MinValue;
            double maxY = double.MinValue;

            foreach (Point point in Points)
            {
                minX = Math.Min(minX, point.X);
                minY = Math.Min(minY, point.Y);
                maxX = Math.Max(maxX, point.X);
                maxY = Math.Max(maxY, point.Y);
            }

            Rect rect = new Rect(new Point(minX, minY), new Point(maxX, maxY));
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

            double scaleX = currentRect.Width == 0 ? 1 : rect.Width / currentRect.Width;
            double scaleY = currentRect.Height == 0 ? 1 : rect.Height / currentRect.Height;

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

        protected override void OnDeactivated()
        {
            if (_currentStroke != null && DrawCanvas.ContainsVisual(_currentStroke))
            {
                DrawCanvas.RemoveVisual(_currentStroke);
            }

            _currentStroke = null;
        }

        protected override void OnBeginDraw(Point startPoint, MouseButtonEventArgs e)
        {
            ClearCurrentSelection();

            BrushStrokeProperties properties = new BrushStrokeProperties
            {
                Id = GetNextDrawingVisualId(),
                ScreenThickness = Config.StrokeThickness,
                Pen = new Pen(CreateDisplayBrush(), Config.StrokeThickness / Math.Max(Zoombox.ContentMatrix.M11, 0.0001))
                {
                    StartLineCap = PenLineCap.Round,
                    EndLineCap = PenLineCap.Round,
                    LineJoin = PenLineJoin.Round,
                },
                Points = new List<Point> { startPoint }
            };

            _currentStroke = new DVBrushStroke(properties);
            _currentStroke.Render();
            DrawCanvas.AddVisual(_currentStroke);
            e.Handled = true;
        }

        protected override void OnUpdateDraw(Point currentPoint, MouseEventArgs e)
        {
            if (_currentStroke == null)
            {
                return;
            }

            double minSpacing = Config.SampleSpacing / Math.Max(Zoombox.ContentMatrix.M11, 0.0001);
            Point lastPoint = _currentStroke.Points[^1];

            if ((currentPoint - lastPoint).Length < minSpacing)
            {
                return;
            }

            _currentStroke.Points.Add(currentPoint);
            _currentStroke.Render();
            e.Handled = true;
        }

        protected override void OnEndDraw(Point endPoint, MouseButtonEventArgs e)
        {
            if (_currentStroke == null)
            {
                return;
            }

            if ((_currentStroke.Points[^1] - endPoint).Length > 0.1)
            {
                _currentStroke.Points.Add(endPoint);
            }

            if (_currentStroke.Points.Count < 2)
            {
                if (DrawCanvas.ContainsVisual(_currentStroke))
                {
                    DrawCanvas.RemoveVisual(_currentStroke);
                }

                _currentStroke = null;
                e.Handled = true;
                return;
            }

            if (DrawCanvas.ContainsVisual(_currentStroke))
            {
                DrawCanvas.RemoveVisual(_currentStroke);
            }

            _currentStroke.Render();
            DrawCanvas.AddVisualCommand(_currentStroke);
            SelectVisual(_currentStroke);
            _currentStroke = null;
            e.Handled = true;
        }
    }
}
