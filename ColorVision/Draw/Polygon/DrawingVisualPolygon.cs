#pragma warning disable CA1711,CA2211
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.Draw
{
    public class DrawingVisualPolygon : DrawingVisualBase, IDrawingVisual
    {
        public PolygonAttribute Attribute { get; set; }

        public DrawBaseAttribute GetAttribute() => Attribute;

        public bool AutoAttributeChanged { get; set; } = true;

        public bool IsDrawing { get; set; } = true;

        public DrawingVisualPolygon()
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
        public int ID { get => Attribute.ID; set => Attribute.ID = value; }

        public override void Render()
        {
            using DrawingContext dc = RenderOpen();

            if (Attribute.Points.Count > 1)
            {
                for (int i = 0; i < Attribute.Points.Count - 1; i++)
                {
                    dc.DrawLine(Attribute.Pen, Attribute.Points[i], Attribute.Points[i + 1]);
                }
                if (!IsDrawing)
                    dc.DrawLine(Attribute.Pen, Attribute.Points[Attribute.Points.Count - 1], Attribute.Points[0]);
            }
        }


    }



}
