using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw
{



    public class DVPolygon : DrawingVisualBase<PolygonProperties>, IDrawingVisual, ILayoutScaleDrawingVisual
    {

        public bool AutoAttributeChanged { get; set; } = true;
        public Pen Pen { get => Attribute.Pen; set => Attribute.Pen = value; }
        public bool IsComple { get => Attribute.IsClosed; set => Attribute.IsClosed = value; }
 
        public DVPolygon()
        {
            Attribute = new PolygonProperties();
            Attribute.Pen = new Pen(Brushes.Red, 2);
            Attribute.Points = new List<Point>();
            Attribute.PropertyChanged += Attribute_PropertyChanged;

        }

        public DVPolygon(PolygonProperties attribute)
        {
            Attribute = attribute;
            Attribute.Points ??= new List<Point>();
            Attribute.PropertyChanged += Attribute_PropertyChanged;
        }

        private void Attribute_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PolygonProperties.Pen) || e.PropertyName == nameof(PolygonProperties.StrokeThickness))
            {
                LayoutBasePenThickness = null;
            }

            Render();
        }

        public List<Point> Points { get => Attribute.Points; }

        public void ApplyLayoutScale(DrawingVisualScaleContext context)
        {
            ApplyLayoutScaleCore(context, Pen, value => Pen = value);
        }

        public override void Render()
        {
            using DrawingContext dc = RenderOpen();
            dc.PushTransform(RegionGeometry.Transform(Attribute));
            if (Points.Count >= 2)
            {
                Pen pen = new(Attribute.Pen.Brush, Attribute.Pen.Thickness);
                for (int i = 1; i < Points.Count; i++)
                    dc.DrawLine(pen, Points[i - 1], Points[i]);
            }

            if (IsComple && Points.Count >= 3)
                dc.DrawLine(Attribute.Pen, Points[^1], Points[0]);
            dc.Pop();
            if (IsMessageVisible && !string.IsNullOrWhiteSpace(Attribute.Msg) && !GetRect().IsEmpty)
            {
                Rect bounds = GetRect();
                FormattedText text = new(Attribute.Msg, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"), System.Math.Max(1, Attribute.Pen.Thickness * 12), Attribute.Brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                text.TextAlignment = TextAlignment.Center;
                dc.DrawText(text, new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2 - text.Height / 2));
            }
        }

        public override Rect GetRect()
        {
            return RegionGeometry.Bounds(Attribute);
        }

        public override void SetRect(Rect rect)
        {
            rect = RegionGeometry.ResizeLocalBounds(Attribute, rect);
            if (PointCollectionGeometry.MapToRect(Points, rect))
            {
                Attribute.InvalidateMeasurementMessage();
                Render();
            }
        }


    }



}
