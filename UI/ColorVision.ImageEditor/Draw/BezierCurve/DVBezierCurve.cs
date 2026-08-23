using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw
{

    public class DVBezierCurve : DrawingVisualBase<BezierCurveProperties>, IDrawingVisual, IBezierCurve, ILayoutScaleDrawingVisual
    {
        public bool AutoAttributeChanged { get; set; } = true;

        public List<Point> Points
        {
            get => Attribute.Points;
            set => Attribute.Points = value;
        }

        public Pen Pen { get => Attribute.Pen; set => Attribute.Pen = value; }


        public DVBezierCurve()
        {
            Attribute = new BezierCurveProperties();
            Attribute.Pen = new Pen(Brushes.Blue, 2);
            Attribute.Points = new List<Point>();
            Attribute.PropertyChanged += Attribute_PropertyChanged;
        }

        public DVBezierCurve(BezierCurveProperties attribute)
        {
            Attribute = attribute;
            Attribute.Points ??= new List<Point>();
            Attribute.PropertyChanged += Attribute_PropertyChanged;
        }
        public bool IsDrawing { get; set; }

        private void Attribute_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(BezierCurveProperties.Pen) || e.PropertyName == nameof(BezierCurveProperties.StrokeThickness))
            {
                LayoutBasePenThickness = null;
            }

            if (AutoAttributeChanged && e.PropertyName != "ID")
            {
                Render();
            }
        }

        public void ApplyLayoutScale(DrawingVisualScaleContext context)
        {
            ApplyLayoutScaleCore(context, Pen, value => Pen = value);
        }

        public override void Render()
        {
            using DrawingContext dc = RenderOpen();
            if (Points.Count <= 0) return;

            StreamGeometry geometry = new();
            using (StreamGeometryContext geometryContext = geometry.Open())
            {
                geometryContext.BeginFigure(Points[0], isFilled: true, isClosed: false);

                GetControlPoints(Points, 0, out _, out Point previousRight);
                for (int i = 1; i < Points.Count; i++)
                {
                    GetControlPoints(Points, i, out Point currentLeft, out Point currentRight);
                    geometryContext.BezierTo(previousRight, currentLeft, Points[i], isStroked: true, isSmoothJoin: true);
                    previousRight = currentRight;
                }
            }

            geometry.Freeze();
            dc.DrawGeometry(Attribute.Brush, Attribute.Pen, geometry);
        }


        public static List<Point> Control1(List<Point> list, int n)
        {
            GetControlPoints(list, n, out Point left, out Point right);
            return new List<Point> { left, right };
        }

        private static void GetControlPoints(List<Point> list, int n, out Point left, out Point right)
        {
            if (n == 0)
            {
                left = list[0];
            }
            else
            {
                left = Average(list[n - 1], list[n]);
            }
            if (n == list.Count - 1)
            {
                right = list[list.Count - 1];
            }
            else
            {
                right = Average(list[n], list[n + 1]);
            }
            Point ave = Average(left, right);
            Point sh = Sub(list[n], ave);
            left = Mul(Add(left, sh), list[n], 0.6);
            right = Mul(Add(right, sh), list[n], 0.6);
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
            return PointCollectionGeometry.GetBounds(Points);
        }

        public override void SetRect(Rect rect)
        {
            if (PointCollectionGeometry.MapToRect(Points, rect))
                Render();
        }


    }
}
