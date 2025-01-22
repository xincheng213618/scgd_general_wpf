using System.Collections.Generic;
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
            Attribute.Id = No++;
            Attribute.Brush = Brushes.Transparent;
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


        public List<Point> Control1(List<Point> list, int n)
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
        public Point Average(Point x, Point y)
        {
            return new Point((x.X + y.X) / 2, (x.Y + y.Y) / 2);
        }
        public Point Add(Point x, Point y)
        {
            return new Point(x.X + y.X, x.Y + y.Y);
        }
        public Point Sub(Point x, Point y)
        {
            return new Point(x.X - y.X, x.Y - y.Y);
        }
        public Point Mul(Point x, Point y, double d)
        {
            Point temp = Sub(x, y);
            temp = new Point(temp.X * d, temp.Y * d);
            temp = Add(y, temp);
            return temp;
        }



    }
}
