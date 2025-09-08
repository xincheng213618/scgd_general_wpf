using System;
using System.Collections.Generic;
using System.Linq;
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
            Attribute.PropertyChanged += (s, e) => Render();

        }
        public List<Point> Points { get => Attribute.Points; }

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

                if (IsComple)
                    dc.DrawLine(Attribute.Pen, Attribute.Points[Attribute.Points.Count - 1], Attribute.Points[0]);
            }
        }

        public override Rect GetRect()
        {
            if (Points == null || Points.Count == 0)
                return Rect.Empty;

            double minX = Points.Min(p => p.X);
            double minY = Points.Min(p => p.Y);
            double maxX = Points.Max(p => p.X);
            double maxY = Points.Max(p => p.Y);

            return new Rect(new Point(minX, minY), new Point(maxX, maxY));
        }

        public override void SetRect(Rect rect)
        {
            if (Points == null || Points.Count == 0)
                return;

            Rect oldRect = GetRect();
            if (oldRect == Rect.Empty)
                return;

            double scaleX = rect.Width / oldRect.Width;
            double scaleY = rect.Height / oldRect.Height;

            for (int i = 0; i < Points.Count; i++)
            {
                double relativeX = (Points[i].X - oldRect.X) * scaleX + rect.X;
                double relativeY = (Points[i].Y - oldRect.Y) * scaleY + rect.Y;

                // 限制在目标 rect 范围内
                relativeX = Math.Max(rect.X, Math.Min(relativeX, rect.X + rect.Width));
                relativeY = Math.Max(rect.Y, Math.Min(relativeY, rect.Y + rect.Height));

                Points[i] = new Point(relativeX, relativeY);
            }
            Render();
        }


    }



}
