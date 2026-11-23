using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using ColorVision.ImageEditor.Properties;

namespace ColorVision.ImageEditor.Draw
{

    public class DrawingVisualRuler:DrawingVisualBase<RulerTextProperties>,IDrawingVisual, ILayoutScaleDrawingVisual
    {
        private static readonly Typeface LabelTypeface = new(new FontFamily("Arial"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        private (Point[] Points, LabelStyle Style, DrawingGroup? Drawing)? _previewLabels;

        public TextAttribute TextAttribute { get => Attribute.TextAttribute; }

        public DrawingVisualRuler()
        {
            Attribute = new RulerTextProperties();
            Attribute.Pen = new Pen(Brushes.Red,10);
        }
        public Pen Pen { get => Attribute.Pen; set => Attribute.Pen = value; }
        public bool AutoAttributeChanged { get; set; }

        public static double ActualLength { get => DefalutTextAttribute.Defalut.IsUsePhysicalUnit ? DefalutTextAttribute.Defalut.ActualLength :1; set { DefalutTextAttribute.Defalut.ActualLength = value;} }
        public static string PhysicalUnit { get => DefalutTextAttribute.Defalut.IsUsePhysicalUnit ? DefalutTextAttribute.Defalut.PhysicalUnit : "Px"; set { DefalutTextAttribute.Defalut.PhysicalUnit = value; } }

        private DefalutTextAttribute? _calibration;
        [Browsable(false), Newtonsoft.Json.JsonIgnore]
        public DefalutTextAttribute? Calibration
        {
            get => _calibration;
            set
            {
                if (ReferenceEquals(_calibration, value)) return;
                if (_calibration != null) PropertyChangedEventManager.RemoveHandler(_calibration, CalibrationChanged, string.Empty);
                _calibration = value;
                if (_calibration != null) PropertyChangedEventManager.AddHandler(_calibration, CalibrationChanged, string.Empty);
                Render();
            }
        }

        public double EffectiveActualLength => Calibration is { } calibration ? (calibration.IsUsePhysicalUnit ? calibration.ActualLength : 1) : ActualLength;
        public string EffectivePhysicalUnit => Calibration is { } calibration ? (calibration.IsUsePhysicalUnit ? calibration.PhysicalUnit : "Px") : PhysicalUnit;
        private void CalibrationChanged(object? sender, PropertyChangedEventArgs e) => Render();

        public List<Point> Points { get => Attribute.Points;}

        public Point? MovePoints { get; set; }

        public void ApplyLayoutScale(DrawingVisualScaleContext context)
        {
            ApplyLayoutScaleCore(context, Pen, value => Pen = value);
        }

        public override void Render()
        {
            double thickness = Pen.Thickness;
            using DrawingContext dc = RenderOpen();
            if (Points.Count == 0)
            {
                _previewLabels = null;
                return;
            }

            if (Points.Count >= 2)
            {
                Pen pen = new(Brushes.Red, thickness);
                for (int i = 1; i < Points.Count; i++)
                    dc.DrawLine(pen, Points[i - 1], Points[i]);
            }

            if (MovePoints is { } pointer)
                dc.DrawLine(new Pen(Brushes.Pink, thickness), Points[^1], pointer);

            var culture = CultureInfo.CurrentCulture;
            var style = new LabelStyle(culture, VisualTreeHelper.GetDpi(this).PixelsPerDip, thickness * 10,
                Resources.Ruler_StartPoint,
                Points.Count > 1 && MovePoints == null ? Resources.Ruler_TotalLength ?? string.Empty : null,
                Points.Count > 1 ? EffectiveActualLength : 1,
                Points.Count > 1 ? EffectivePhysicalUnit : string.Empty);
            var points = CollectionsMarshal.AsSpan(Points);
            if (MovePoints == null || !culture.IsReadOnly)
            {
                _previewLabels = null;
                DrawLabels(dc, points, style);
                return;
            }

            if (_previewLabels is not { } labels || !ReferenceEquals(labels.Style.Culture, culture)
                || labels.Style != style || !points.SequenceEqual(labels.Points))
            {
                // A matching preview frame confirms the labels can be reused.
                _previewLabels = (points.ToArray(), style, null);
                DrawLabels(dc, points, style);
                return;
            }

            if (labels.Drawing == null)
            {
                var drawing = new DrawingGroup();
                using (DrawingContext labelContext = drawing.Open())
                    DrawLabels(labelContext, points, style);
                drawing.Freeze();
                labels.Drawing = drawing;
                _previewLabels = labels;
            }

            dc.DrawDrawing(labels.Drawing);
        }

        private static void DrawLabels(DrawingContext dc, ReadOnlySpan<Point> points, LabelStyle style)
        {
            var start = FormatLabel(style.StartText, style);
            start.TextAlignment = TextAlignment.Center;
            dc.DrawText(start, points[0]);

            double total = 0;
            for (int i = 1; i < points.Length - 1; i++)
            {
                double length = GetDistance(points[i], points[i - 1]) * style.Scale;
                total += length;
                dc.DrawText(FormatLabel(length.ToString("F2", style.Culture) + style.Unit, style), points[i]);
            }

            if (points.Length > 1)
            {
                double length = GetDistance(points[^1], points[^2]) * style.Scale;
                string text = style.TotalText is { } prefix
                    ? prefix + (total + length).ToString("F2", style.Culture) + style.Unit
                    : length.ToString("F2", style.Culture) + style.Unit;
                dc.DrawText(FormatLabel(text, style), points[^1]);
            }
        }

        private static FormattedText FormatLabel(string text, LabelStyle style)
            => new(text, style.Culture, FlowDirection.LeftToRight, LabelTypeface, style.FontSize, Brushes.Red, style.PixelsPerDip);

        private readonly record struct LabelStyle(CultureInfo Culture, double PixelsPerDip, double FontSize,
            string StartText, string? TotalText, double Scale, string Unit);

        public static double GetDistance(Point startPoint, Point endPoint)
        {
            double x = Math.Abs(endPoint.X - startPoint.X);
            double y = Math.Abs(endPoint.Y - startPoint.Y);
            return Math.Sqrt(x * x + y * y);
        }


    }
}
