using ColorVision.Common.MVVM;
using HandyControl.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw.Special
{
    public class ReferenceLine: DrawingVisualBase<ReferenceLineParam>
    {
        public Pen Pen { get => Attribute.Pen; set => Attribute.Pen = value; }

        public ReferenceLine()
        {
            Attribute = new ReferenceLineParam();
            Attribute.Pen  = new Pen(Attribute.Brush, 1);
            Attribute.PropertyChanged += (s, e) => Render();
        }

        public override void Render()
        {
            using DrawingContext dc = RenderOpen();


        }

        public static double CalculateAngle(Point point1, Point point2)
        {
            // 计算向量差
            double deltaX = point2.X - point1.X;
            double deltaY = point2.Y - point1.Y;

            // 使用Atan2计算弧度
            double angleInRadians = Math.Atan2(deltaY, deltaX);

            // 将弧度转换为度
            double angleInDegrees = angleInRadians * (180.0 / Math.PI);

            // 标准化角度到[0, 360)范围，如果需要的话
            //if (angleInDegrees < 0) angleInDegrees += 360;

            return angleInDegrees;
        }

        private static double Det(double a, double b, double c, double d)
        {
            return a * d - b * c;
        }

        public static Point? GetIntersection(Point p, double angle, Point p1, Point p2)
        {
            // Convert angle to radians
            double angleRad = angle * Math.PI / 180.0;

            // Define the second point for the line from the given point and angle
            Point pAngle = new Point(p.X + Math.Cos(angleRad), p.Y + Math.Sin(angleRad));

            // Calculate the direction vectors
            double dx1 = pAngle.X - p.X;
            double dy1 = pAngle.Y - p.Y;
            double dx2 = p2.X - p1.X;
            double dy2 = p2.Y - p1.Y;

            // Calculate the determinant
            double denom = dx1 * dy2 - dy1 * dx2;

            // Check if lines are parallel
            const double epsilon = 1e-10;
            if (Math.Abs(denom) < epsilon)
            {
                return null;
            }

            // Calculate the intersection point using parameter t
            double t = ((p1.X - p.X) * dy2 - (p1.Y - p.Y) * dx2) / denom;
            double x = p.X + t * dx1;
            double y = p.Y + t * dy1;

            Point intersection = new Point(x, y);

            // Check if the intersection point lies on the segment p1-p2
            if (!IsBetween(p1, p2, intersection))
            {
                return null;
            }

            return intersection;
        }

        private static bool IsBetween(Point p1, Point p2, Point p)
        {
            const double epsilon = 1e-10;
            return (Math.Min(p1.X, p2.X) - epsilon <= p.X && p.X <= Math.Max(p1.X, p2.X) + epsilon) &&
                   (Math.Min(p1.Y, p2.Y) - epsilon <= p.Y && p.Y <= Math.Max(p1.Y, p2.Y) + epsilon);
        }

        public static List<Point> CalculateIntersectionPoints(double width, double height, Point point, double angle)
        {
            List<Point> points = new();
            if (GetIntersection(point, angle, new Point(0, 0), new Point(0, width)) is Point point1)
                points.Add(point1);
            if (GetIntersection(point, angle, new Point(0, width), new Point(height, width)) is Point point2)
                points.Add(point2);
            if (GetIntersection(point, angle, new Point(height, width), new Point(height, 0)) is Point point3)
                points.Add(point3);
            if (GetIntersection(point, angle, new Point(height, 0), new Point(0, 0)) is Point point4)
                points.Add(point4);


            if (GetIntersection(point, angle + 90, new Point(0, 0), new Point(0, width)) is Point point5)
                points.Add(point5);
            if (GetIntersection(point, angle + 90, new Point(0, width), new Point(height, width)) is Point point6)
                points.Add(point6);
            if (GetIntersection(point, angle + 90, new Point(height, width), new Point(height, 0)) is Point point7)
                points.Add(point7);
            if (GetIntersection(point, angle + 90, new Point(height, 0), new Point(0, 0)) is Point point8)
                points.Add(point8);

            points = points.Distinct().ToList();
            return points;
        }

    }

    public class ReferenceLineParam:BaseProperties
    {
        [Browsable(false)]
        public Pen Pen { get => _Pen; set { _Pen = value; OnPropertyChanged(); } }
        private Pen _Pen;


        [Category("RectangleAttribute"), DisplayName("颜色")]
        public Brush Brush { get => _Brush; set { _Brush = value; OnPropertyChanged(); } }
        private Brush _Brush = Brushes.Red;

        public int Mode { get => _Mode; set { _Mode = value; } }
        private int _Mode = 2;

        public double ActualWidth { get => _ActualWidth; set { _ActualWidth = value; OnPropertyChanged(); } }
        private double _ActualWidth;
        public double ActualHeight { get => _ActualHeight; set { _ActualHeight = value; OnPropertyChanged(); } }
        private double _ActualHeight;
    }


    public class ToolReferenceLine
    {
        private ZoomboxSub ZoomboxSub { get; set; }
        private DrawCanvas Image { get; set; }

        public ReferenceLine ReferenceLine { get; set; }

        public int Mode { get => ReferenceLine.Attribute.Mode; set { ReferenceLine.Attribute.Mode = value; Render(); } } 

        public RelayCommand SelectNoneCommand { get; set; }
        public RelayCommand Select0Command { get; set; }
        public RelayCommand Select1Command { get; set; }
        public RelayCommand Select2Command { get; set; }
        public RelayCommand LockCommand { get; set; }

        public ImageViewModel Paraent { get; set; }

        public ToolReferenceLine(ImageViewModel imageEditViewMode, ZoomboxSub zombox, DrawCanvas drawCanvas)
        {
            ZoomboxSub = zombox;
            Image = drawCanvas;
            Paraent = imageEditViewMode;
            ReferenceLine = new ReferenceLine();

            SelectNoneCommand = new RelayCommand(a => SetMode(-1));
            Select0Command = new RelayCommand(a => SetMode(0));
            Select1Command = new RelayCommand(a => SetMode(1));
            Select2Command = new RelayCommand(a => SetMode(2));
            LockCommand = new RelayCommand(a => { IsLocked = !IsLocked; Render(); });
        }


        private void SetMode(int i)
        {
            if (i == -1)
            {
                Paraent.ConcentricCircle = false;
            }
            else
            {
                Paraent.ConcentricCircle = true;
                ReferenceLine.Attribute.Mode = i;
                Render();
            }
        }



        public bool IsShow
        {
            get => _IsShow; set
            {
                if (_IsShow == value) return;
                _IsShow = value;
                DrawVisualImageControl(_IsShow);
                if (value)
                {
                    ReferenceLine.Attribute.ActualWidth = Image.ActualWidth;
                    ReferenceLine.Attribute.ActualHeight = Image.ActualHeight;
                    RMouseDownP = new Point(Image.ActualWidth / 2, Image.ActualHeight / 2);
                    PointLen = new Vector();
                    Image.MouseMove += MouseMove;
                    Image.PreviewMouseLeftButtonDown += PreviewMouseLeftButtonDown;
                    Image.PreviewMouseRightButtonDown += Image_PreviewMouseRightButtonDown;
                    Image.PreviewMouseUp += PreviewMouseUp;
                    Image.MouseDoubleClick += Image_MouseDoubleClick;
                    ZoomboxSub.LayoutUpdated += ZoomboxSub_LayoutUpdated;

                }
                else
                {
                    Image.MouseMove -= MouseMove;
                    Image.PreviewMouseLeftButtonDown -= PreviewMouseLeftButtonDown;
                    Image.PreviewMouseRightButtonDown -= Image_PreviewMouseRightButtonDown;
                    Image.PreviewMouseUp -= PreviewMouseUp;
                    ZoomboxSub.LayoutUpdated -= ZoomboxSub_LayoutUpdated;
                }
            }
        }
        private void Image_MouseDoubleClick(object sender, RoutedEventArgs e)
        {
            if (sender is DrawCanvas canvas)
            {
                var position = Mouse.GetPosition(canvas);
                IsLocked = !IsLocked;
                Render();
            }
        }

        private void ZoomboxSub_LayoutUpdated(object? sender, EventArgs e)
        {
            if (Ratio != ZoomboxSub.ContentMatrix.M11)
            {
                Ratio = ZoomboxSub.ContentMatrix.M11;
                double ratio = 1 / Ratio;
                ReferenceLine.Attribute.Pen = new Pen(ReferenceLine.Attribute.Brush, ratio);
            }
        }
        double Ratio;

        private bool IsRMouseDown;
        private bool IsLMouseDown;

        private Point RMouseDownP;
        private Point LMouseDownP;
        private Vector PointLen;


        private void PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (IsLocked) return;

            RMouseDownP = Mouse.GetPosition(Image);
            IsRMouseDown = true;
            Render();
        }


        private void Image_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (IsLocked) return;


            LMouseDownP = Mouse.GetPosition(Image);
            IsLMouseDown = true;
            PointLen = LMouseDownP - RMouseDownP;
            Render();
        }

        private void PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (IsLocked) return;


            IsRMouseDown = false;
            IsLMouseDown = false;
            Render();
        }



        // 1. 添加锁定字段
        private bool IsLocked = true;

        // 2. 修改 MouseMove 方法
        private void MouseMove(object sender, MouseEventArgs e)
        {
            if (IsShow && !IsLocked && (IsRMouseDown || IsLMouseDown))
            {
                if (IsRMouseDown)
                {
                    RMouseDownP = e.GetPosition(Image);
                }
                if (IsLMouseDown)
                {
                    LMouseDownP = e.GetPosition(Image);
                    PointLen = LMouseDownP - RMouseDownP;
                }
                Render();
            }
        }

        public void Render()
        {
            using DrawingContext dc = ReferenceLine.RenderOpen();
            Pen pen = ReferenceLine.Attribute.Pen;


            Point ActL = RMouseDownP + PointLen;


            double angle = ReferenceLine.CalculateAngle(RMouseDownP, ActL);
            Point CenterPoint = RMouseDownP;
            double ActualWidth = ReferenceLine.Attribute.ActualWidth;
            double ActualHeight = ReferenceLine.Attribute.ActualHeight;

            if (Mode == 0)
            {
                // 旋转变换
                List<Point> intersectionPoints = ReferenceLine.CalculateIntersectionPoints(ActualHeight, ActualWidth, CenterPoint, angle);

                if (intersectionPoints.Count == 4)
                {
                    dc.DrawLine(pen, intersectionPoints[0], intersectionPoints[1]); // 水平线
                    dc.DrawLine(pen, intersectionPoints[2], intersectionPoints[3]); // 垂直线
                }

                TextAttribute textAttribute = new();
                textAttribute.FontSize = 15 / ZoomboxSub.ContentMatrix.M11;

                double a = 20 / ZoomboxSub.ContentMatrix.M11;
                if (IsRMouseDown || IsLMouseDown)
                {
                    FormattedText formattedRText = new($"({(int)RMouseDownP.X},{(int)RMouseDownP.Y})", CultureInfo.CurrentCulture, textAttribute.FlowDirection, new Typeface(textAttribute.FontFamily, textAttribute.FontStyle, textAttribute.FontWeight, textAttribute.FontStretch), textAttribute.FontSize, textAttribute.Brush, VisualTreeHelper.GetDpi(ReferenceLine).PixelsPerDip);
                    dc.DrawText(formattedRText, RMouseDownP + new Vector(a, 2 * a));
                }

                FormattedText formattedText = new(angle.ToString("F3") + "°", CultureInfo.CurrentCulture, textAttribute.FlowDirection, new Typeface(textAttribute.FontFamily, textAttribute.FontStyle, textAttribute.FontWeight, textAttribute.FontStretch), textAttribute.FontSize, textAttribute.Brush, VisualTreeHelper.GetDpi(ReferenceLine).PixelsPerDip);
                dc.DrawText(formattedText, RMouseDownP + new Vector(a, a));



                int lenc = (int)Math.Sqrt(PointLen.X * PointLen.X + PointLen.Y * PointLen.Y);

                dc.DrawEllipse(Brushes.Transparent, pen, RMouseDownP, lenc, lenc);
                dc.DrawEllipse(Brushes.Transparent, pen, RMouseDownP, lenc + 10, lenc + 10);
                dc.DrawEllipse(Brushes.Transparent, pen, RMouseDownP, lenc + 30, lenc + 30);
                dc.DrawEllipse(Brushes.Transparent, pen, RMouseDownP, lenc + 50, lenc + 50);
                dc.DrawEllipse(Brushes.Transparent, pen, RMouseDownP, lenc + 100, lenc + 100);
                dc.DrawEllipse(Brushes.Transparent, pen, RMouseDownP, lenc + 200, lenc + 200);


                //dc.PushTransform(new RotateTransform(angle, RMouseDownP.X, RMouseDownP.Y));
                FormattedText formattedText1 = new(lenc.ToString("F0"), CultureInfo.CurrentCulture, textAttribute.FlowDirection, new Typeface(textAttribute.FontFamily, textAttribute.FontStyle, textAttribute.FontWeight, textAttribute.FontStretch), textAttribute.FontSize, textAttribute.Brush, VisualTreeHelper.GetDpi(ReferenceLine).PixelsPerDip);
                dc.DrawText(formattedText1, RMouseDownP + PointLen);

                FormattedText formattedText2 = new((lenc + 10).ToString("F0"), CultureInfo.CurrentCulture, textAttribute.FlowDirection, new Typeface(textAttribute.FontFamily, textAttribute.FontStyle, textAttribute.FontWeight, textAttribute.FontStretch), textAttribute.FontSize, textAttribute.Brush, VisualTreeHelper.GetDpi(ReferenceLine).PixelsPerDip);
                dc.DrawText(formattedText2, RMouseDownP + PointLen);

                FormattedText formattedText3 = new((lenc + 1).ToString("F0"), CultureInfo.CurrentCulture, textAttribute.FlowDirection, new Typeface(textAttribute.FontFamily, textAttribute.FontStyle, textAttribute.FontWeight, textAttribute.FontStretch), textAttribute.FontSize, textAttribute.Brush, VisualTreeHelper.GetDpi(ReferenceLine).PixelsPerDip);
                dc.DrawText(formattedText3, RMouseDownP - PointLen);
            }
            else if (Mode == 1)
            {

                // 旋转变换
                List<Point> intersectionPoints = ReferenceLine.CalculateIntersectionPoints(ActualHeight, ActualWidth, CenterPoint, angle);

                if (intersectionPoints.Count == 4)
                {
                    dc.DrawLine(pen, intersectionPoints[0], intersectionPoints[1]); // 水平线
                    dc.DrawLine(pen, intersectionPoints[2], intersectionPoints[3]); // 垂直线
                }


                TextAttribute textAttribute = new();
                textAttribute.FontSize = 15 / ZoomboxSub.ContentMatrix.M11;
                double a = 15 / ZoomboxSub.ContentMatrix.M11;
                if (IsRMouseDown || IsLMouseDown)
                {
                    FormattedText formattedRText = new($"({(int)RMouseDownP.X},{(int)RMouseDownP.Y})", CultureInfo.CurrentCulture, textAttribute.FlowDirection, new Typeface(textAttribute.FontFamily, textAttribute.FontStyle, textAttribute.FontWeight, textAttribute.FontStretch), textAttribute.FontSize, textAttribute.Brush, VisualTreeHelper.GetDpi(ReferenceLine).PixelsPerDip);
                    dc.DrawText(formattedRText, RMouseDownP + new Vector(a, 2 * a));
                }

                FormattedText formattedText = new(angle.ToString("F3") + "°", CultureInfo.CurrentCulture, textAttribute.FlowDirection, new Typeface(textAttribute.FontFamily, textAttribute.FontStyle, textAttribute.FontWeight, textAttribute.FontStretch), textAttribute.FontSize, textAttribute.Brush, VisualTreeHelper.GetDpi(ReferenceLine).PixelsPerDip);
                dc.DrawText(formattedText, RMouseDownP + new Vector(a, a));
            }
            else if (Mode == 2)
            {
                double angle1 = (angle + 45) * Math.PI / 180.0;
                // 旋转变换
                List<Point> intersectionPoints = ReferenceLine.CalculateIntersectionPoints(ActualHeight, ActualWidth, CenterPoint + new Vector(5 * Ratio * Math.Cos(angle1), 5 * Ratio * Math.Sin(angle1)), angle);

                if (intersectionPoints.Count == 4)
                {
                    dc.DrawLine(pen, intersectionPoints[0], intersectionPoints[1]); // 水平线,
                    dc.DrawLine(pen, intersectionPoints[2], intersectionPoints[3]); // 垂直线
                }
                intersectionPoints = ReferenceLine.CalculateIntersectionPoints(ActualHeight, ActualWidth, CenterPoint - new Vector(5 * Ratio * Math.Cos(angle1), 5 * Ratio * Math.Sin(angle1)), angle);
                if (intersectionPoints.Count == 4)
                {
                    dc.DrawLine(pen, intersectionPoints[0], intersectionPoints[1]); // 水平线,
                    dc.DrawLine(pen, intersectionPoints[2], intersectionPoints[3]); // 垂直线
                }


                TextAttribute textAttribute = new();
                textAttribute.FontSize = 15 / ZoomboxSub.ContentMatrix.M11;
                double a = 15 / ZoomboxSub.ContentMatrix.M11;
                if (IsRMouseDown || IsLMouseDown)
                {
                    FormattedText formattedRText = new($"({(int)RMouseDownP.X},{(int)RMouseDownP.Y})", CultureInfo.CurrentCulture, textAttribute.FlowDirection, new Typeface(textAttribute.FontFamily, textAttribute.FontStyle, textAttribute.FontWeight, textAttribute.FontStretch), textAttribute.FontSize, textAttribute.Brush, VisualTreeHelper.GetDpi(ReferenceLine).PixelsPerDip);
                    dc.DrawText(formattedRText, RMouseDownP + new Vector(a, 2 * a));
                }


                FormattedText formattedText = new(angle.ToString("F3") + "°", CultureInfo.CurrentCulture, textAttribute.FlowDirection, new Typeface(textAttribute.FontFamily, textAttribute.FontStyle, textAttribute.FontWeight, textAttribute.FontStretch), textAttribute.FontSize, textAttribute.Brush, VisualTreeHelper.GetDpi(ReferenceLine).PixelsPerDip);
                dc.DrawText(formattedText, RMouseDownP + new Vector(a, a));
            }

            if (IsLocked)
            {
                // 画一个小锁图标或者文字
                FormattedText lockText = new(
                    "锁定",
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"),
                    18 / ZoomboxSub.ContentMatrix.M11,
                    Brushes.Red,
                    VisualTreeHelper.GetDpi(ReferenceLine).PixelsPerDip
                );
                dc.DrawText(lockText, new Point(10, 10));

            }
        }

        private bool _IsShow;

        public void DrawVisualImageControl(bool Control)
        {
            if (Control)
            {
                if (!Image.ContainsVisual(ReferenceLine))
                    Image.AddVisualCommand(ReferenceLine);
            }
            else
            {
                if (Image.ContainsVisual(ReferenceLine))
                    Image.RemoveVisualCommand(ReferenceLine);
            }
        }



    }
}
