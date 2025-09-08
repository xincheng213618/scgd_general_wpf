using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw
{
    public class DVLine : DrawingVisualBase<LineProperties>, IDrawingVisual
    {

        public Pen Pen { get => Attribute.Pen; set => Attribute.Pen = value; }

        public DVLine()
        {
            Attribute = new LineProperties();
            Attribute.PropertyChanged += (s, e) => Render();
        }

        public DVLine(LineProperties attribute)
        {
            Attribute = attribute;
            Attribute.PropertyChanged += (s, e) => Render();
        }


        public List<Point> Points { get => Attribute.Points; }

        public override void Render()
        {
            using DrawingContext dc = RenderOpen();

            if (Points.Count >= 1)
            {
                for (int i = 1; i < Points.Count; i++)
                {
                    dc.DrawLine(new Pen(Attribute.Pen.Brush, Attribute.Pen.Thickness), Points[i - 1], Points[i]);
                }
            }
        }




    }



}
