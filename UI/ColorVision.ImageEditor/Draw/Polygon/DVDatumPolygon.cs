using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw
{
    public class DVDatumPolygon : DrawingVisualBase<PolygonProperties>, IDrawingVisualDatum
    {
        public Pen Pen { get => Attribute.Pen; set => Attribute.Pen = value; }


        public bool IsComple { get; set; }

        public DVDatumPolygon()
        {
            Attribute = new PolygonProperties();

            Attribute.Pen = new Pen(Brushes.Red, 2);
            Attribute.Points = new List<Point>();
            Attribute.PropertyChanged += (s, e) => Render();
        }
        public List<Point> Points { get => Attribute.Points; }


        public override void Render()
        {
            using DrawingContext dc = RenderOpen();

            if (Attribute.Points.Count > 1)
            {
                for (int i = 0; i < Attribute.Points.Count - 1; i++)
                    dc.DrawLine(Attribute.Pen, Attribute.Points[i], Attribute.Points[i + 1]);

                if (IsComple)
                    dc.DrawLine(Attribute.Pen, Attribute.Points[Attribute.Points.Count - 1], Attribute.Points[0]);
            }
        }

    }



}
