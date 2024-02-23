using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.Draw
{
    public class DrawingVisualDatumPolygon : DrawingVisualBase<PolygonAttribute>, IDrawingVisualDatum
    {
        public DrawBaseAttribute BaseAttribute => Attribute;

        public Pen Pen { get => Attribute.Pen; set => Attribute.Pen = value; }

        public bool AutoAttributeChanged { get; set; } = true;

        public bool IsComple { get; set; }

        public DrawingVisualDatumPolygon()
        {
            Attribute = new PolygonAttribute();
            Attribute.ID = No++;
            Attribute.Brush = Brushes.Transparent;
            Attribute.Pen = new Pen(Brushes.Red, 2);
            Attribute.Points = new List<Point>();
            Attribute.PropertyChanged += (s, e) =>
            {
                if (AutoAttributeChanged)
                    Render();
            };
        }
        public Point? MovePoints { get; set; }

        public override void Render()
        {
            using DrawingContext dc = RenderOpen();

            if (Attribute.Points.Count > 1)
            {
                for (int i = 0; i < Attribute.Points.Count - 1; i++)
                {
                    dc.DrawLine(Attribute.Pen, Attribute.Points[i], Attribute.Points[i + 1]);
                }
                if (IsComple)
                    dc.DrawLine(Attribute.Pen, Attribute.Points[Attribute.Points.Count - 1], Attribute.Points[0]);
            }
        }

    }



}
