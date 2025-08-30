using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw
{



    public class DVPolygon : DrawingVisualBase<PolygonProperties>, IDrawingVisual
    {

        public bool AutoAttributeChanged { get; set; } = true;
        public Pen Pen { get => Attribute.Pen; set => Attribute.Pen = value; }
        public bool IsComple { get; set; }

        
        public DVPolygon()
        {
            Attribute = new PolygonProperties();
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
            Pen whiteOutlinePen = new(Brushes.White, Attribute.Pen.Thickness + 2); // 描边比实际线条厚2个单位

            if (Points.Count >= 1)
            {
                for (int i = 1; i < Points.Count; i++)
                {
                    dc.DrawLine(whiteOutlinePen, Points[i - 1], Points[i]);
                    dc.DrawLine(new Pen(Attribute.Pen.Brush, Attribute.Pen.Thickness), Points[i - 1], Points[i]);
                }
                if (MovePoints != null)
                {
                    dc.DrawLine(new Pen(Brushes.Pink, Attribute.Pen.Thickness), Points[^1], (Point)MovePoints);
                }

                if (IsComple)
                    dc.DrawLine(Attribute.Pen, Attribute.Points[Attribute.Points.Count - 1], Attribute.Points[0]);
            }
        }


    }



}
