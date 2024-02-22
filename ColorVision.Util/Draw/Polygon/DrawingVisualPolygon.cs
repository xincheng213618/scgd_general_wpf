using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.Draw
{
    public class DrawingVisualPolygon : DrawingVisualBase<PolygonAttribute>, IDrawingVisual
    {
        public DrawBaseAttribute BaseAttribute => Attribute;

        public bool AutoAttributeChanged { get; set; } = true;
        public Pen Pen { get => Attribute.Pen; set => Attribute.Pen = value; }
        public bool IsDrawing { get; set; } = true;

        
        public DrawingVisualPolygon()
        {
            Version = "多边形";
            Attribute = new PolygonAttribute();
            Attribute.ID = No++;
            Attribute.Pen = new Pen(Brushes.Red, 2);
            Attribute.Points = new List<Point>();
            Attribute.PropertyChanged += (s, e) =>
            {
                if (AutoAttributeChanged)
                    Render();
            };
        }
        public List<Point> Points { get => Attribute.Points; }

        public Point? MovePoints { get; set; }


        public override void Render()
        {
            using DrawingContext dc = RenderOpen();

            if (Points.Count >= 1)
            {
                for (int i = 1; i < Points.Count; i++)
                {
                    dc.DrawLine(new Pen(Attribute.Pen.Brush, Attribute.Pen.Thickness), Points[i - 1], Points[i]);
                }
                if (MovePoints != null)
                {
                    dc.DrawLine(new Pen(Brushes.Pink, Attribute.Pen.Thickness), Points[^1], (Point)MovePoints);
                }
            }
        }


    }



}
