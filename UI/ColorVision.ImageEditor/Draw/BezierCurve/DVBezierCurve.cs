using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw
{

    public class DVBezierCurve : DrawingVisualBase<BezierCurveProperties>, IDrawingVisual, IBezierCurve
    {
        public bool AutoAttributeChanged { get; set; }

        public List<Point> Points { get; set; }

        public Pen Pen { get => Attribute.Pen; set => Attribute.Pen = value; }


        public DVBezierCurve()
        {
            Attribute = new BezierCurveProperties();
            Attribute.Pen = new Pen(Brushes.Blue, 2);
            Points = new List<Point>();
            Attribute.PropertyChanged += (s, e) => { if (AutoAttributeChanged && e.PropertyName != "ID") Render(); };
        }
        public bool IsDrawing { get; set; }

        public override void Render()
        {
            using DrawingContext dc = RenderOpen();
            if (Points.Count <= 0) return;

            PathFigure pf = new PathFigure();
            pf.StartPoint = Points[0];

            List<Point> controls = new List<Point>();
            for (int i = 0; i < Points.Count; i++)
            {
                controls.AddRange(Control1(Points, i));
            }
            for (int i = 1; i < Points.Count; i++)
            {
                BezierSegment bs = new BezierSegment(controls[i * 2 - 1], controls[i * 2], Points[i], true);
                bs.IsSmoothJoin = true;

                pf.Segments.Add(bs);
            }

            PathGeometry pathGeometry = new();
            pathGeometry.Figures.Add(pf);

            dc.DrawGeometry(Attribute.Brush, Attribute.Pen, pathGeometry);
        }


        public static List<Point> Control1(List<Point> list, int n)
        {
            List<Point> point = new List<Point>();
            point.Add(new Point());
            point.Add(new Point());
            if (n == 0)
            {
                point[0] = list[0];
            }
            else
            {
                point[0] = Average(list[n - 1], list[n]);
            }
            if (n == list.Count - 1)
            {
                point[1] = list[list.Count - 1];
            }
            else
            {
                point[1] = Average(list[n], list[n + 1]);
            }
            Point ave = Average(point[0], point[1]);
            Point sh = Sub(list[n], ave);
            point[0] = Mul(Add(point[0], sh), list[n], 0.6);
            point[1] = Mul(Add(point[1], sh), list[n], 0.6);
            //Line line = new Line();
            //line.X1 = point[0].X;
            //line.Y1 = point[0].Y;
            //line.X2 = point[1].X;
            //line.Y2 = point[1].Y;
            //line.Stroke = Brushes.Red;
            //MapCanvas.Children.Add(line);
            return point;
        }
        public static Point Average(Point x, Point y)
        {
            return new Point((x.X + y.X) / 2, (x.Y + y.Y) / 2);
        }
        public static Point Add(Point x, Point y)
        {
            return new Point(x.X + y.X, x.Y + y.Y);
        }
        public static Point Sub(Point x, Point y)
        {
            return new Point(x.X - y.X, x.Y - y.Y);
        }
        public static Point Mul(Point x, Point y, double d)
        {
            Point temp = Sub(x, y);
            temp = new Point(temp.X * d, temp.Y * d);
            temp = Add(y, temp);
            return temp;
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
