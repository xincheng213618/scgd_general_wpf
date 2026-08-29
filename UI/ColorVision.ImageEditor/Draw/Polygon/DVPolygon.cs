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
        public bool IsComple { get; set; }
 
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
            if (Points.Count >= 2)
            {
                Pen pen = new(Attribute.Pen.Brush, Attribute.Pen.Thickness);
                for (int i = 1; i < Points.Count; i++)
                    dc.DrawLine(pen, Points[i - 1], Points[i]);
            }

            if (IsComple && Points.Count >= 1)
                dc.DrawLine(Attribute.Pen, Attribute.Points[Attribute.Points.Count - 1], Attribute.Points[0]);
        }

        public override Rect GetRect()
        {
            return PointCollectionGeometry.GetBounds(Points);
        }

        public override void SetRect(Rect rect)
        {
            if (PointCollectionGeometry.MapToRect(Points, rect))
                Render();
        }


    }



}
