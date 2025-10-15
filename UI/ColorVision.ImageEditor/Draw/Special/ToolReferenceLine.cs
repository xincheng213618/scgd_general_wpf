using ColorVision.Common.MVVM;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw.Special
{
    public class DVLineDVContextMenu : IDVContextMenu
    {
        public Type ContextType => typeof(ReferenceLine);

        public IEnumerable<MenuItem> GetContextMenuItems(ImageViewModel imageViewModel, object obj)
        {
            List<MenuItem> MenuItems = new List<MenuItem>();
            if (obj is ReferenceLine referenceLine)
            {
                MenuItem menuItem = new() { Header = "锁定",IsChecked = referenceLine.IsLocked };
                menuItem.Click += (s, e) =>
                {
                    referenceLine.IsLocked = !referenceLine.IsLocked;
                    referenceLine.Render();
                };
                MenuItems.Add(menuItem);
            }
            return MenuItems;
        }
    }
    
    
    public class ReferenceLine: DrawingVisualBase<ReferenceLineParam>
    {
        public Pen Pen { get => Attribute.Pen; set => Attribute.Pen = value; }

        public ReferenceLine()
        {
            Attribute = new ReferenceLineParam();
            Attribute.Pen  = new Pen(Attribute.Brush, 1);
            Attribute.PropertyChanged += (s, e) => Render();
        }
        public double Ratio { get; set; }
        public double ActualWidth { get; set; }
        public double ActualHeight { get; set; }

        public bool IsRMouseDown { get; set; }
        public bool IsLMouseDown { get; set; }

        public Point RMouseDownP { get => new Point(Attribute.PointX, Attribute.PointY); set
            {
                Attribute.PointX = value.X;
                Attribute.PointY = value.Y;
            }
        }
        public Point LMouseDownP { get; set; }
        public Vector PointLen { get; set; }

        public bool IsLocked { get; set; } = true;
        public int Mode { get => Attribute.Mode; set { Attribute.Mode = value; } }

        SolidColorBrush SolidColorBrush = new SolidColorBrush(Color.FromArgb(1, 255, 255, 255));

        public override void Render()
        {
            using DrawingContext dc = RenderOpen();
            dc.DrawRectangle(SolidColorBrush, new Pen(Brushes.Transparent, 0), new Rect(0,0,ActualWidth,ActualHeight));

            Pen pen = Attribute.Pen;

            double angle = Attribute.Angle;
            Point CenterPoint = RMouseDownP;

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
                textAttribute.FontSize = 15 / Ratio;

                double a = 20 / Ratio;
                if (IsRMouseDown || IsLMouseDown)
                {
                    FormattedText formattedRText = new($"({(int)RMouseDownP.X},{(int)RMouseDownP.Y})", CultureInfo.CurrentCulture, textAttribute.FlowDirection, new Typeface(textAttribute.FontFamily, textAttribute.FontStyle, textAttribute.FontWeight, textAttribute.FontStretch), textAttribute.FontSize, textAttribute.Brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                    dc.DrawText(formattedRText, RMouseDownP + new Vector(a, 2 * a));
                }

                FormattedText formattedText = new(angle.ToString("F3") + "°", CultureInfo.CurrentCulture, textAttribute.FlowDirection, new Typeface(textAttribute.FontFamily, textAttribute.FontStyle, textAttribute.FontWeight, textAttribute.FontStretch), textAttribute.FontSize, textAttribute.Brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                dc.DrawText(formattedText, RMouseDownP + new Vector(a, a));



                int lenc = (int)Math.Sqrt(PointLen.X * PointLen.X + PointLen.Y * PointLen.Y);

                dc.DrawEllipse(Brushes.Transparent, pen, RMouseDownP, lenc, lenc);
                dc.DrawEllipse(Brushes.Transparent, pen, RMouseDownP, lenc + 10, lenc + 10);
                dc.DrawEllipse(Brushes.Transparent, pen, RMouseDownP, lenc + 30, lenc + 30);
                dc.DrawEllipse(Brushes.Transparent, pen, RMouseDownP, lenc + 50, lenc + 50);
                dc.DrawEllipse(Brushes.Transparent, pen, RMouseDownP, lenc + 100, lenc + 100);
                dc.DrawEllipse(Brushes.Transparent, pen, RMouseDownP, lenc + 200, lenc + 200);


                //dc.PushTransform(new RotateTransform(angle, RMouseDownP.X, RMouseDownP.Y));
                FormattedText formattedText1 = new(lenc.ToString("F0"), CultureInfo.CurrentCulture, textAttribute.FlowDirection, new Typeface(textAttribute.FontFamily, textAttribute.FontStyle, textAttribute.FontWeight, textAttribute.FontStretch), textAttribute.FontSize, textAttribute.Brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                dc.DrawText(formattedText1, RMouseDownP + PointLen);

                FormattedText formattedText2 = new((lenc + 10).ToString("F0"), CultureInfo.CurrentCulture, textAttribute.FlowDirection, new Typeface(textAttribute.FontFamily, textAttribute.FontStyle, textAttribute.FontWeight, textAttribute.FontStretch), textAttribute.FontSize, textAttribute.Brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                dc.DrawText(formattedText2, RMouseDownP + PointLen);

                FormattedText formattedText3 = new((lenc + 1).ToString("F0"), CultureInfo.CurrentCulture, textAttribute.FlowDirection, new Typeface(textAttribute.FontFamily, textAttribute.FontStyle, textAttribute.FontWeight, textAttribute.FontStretch), textAttribute.FontSize, textAttribute.Brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
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
                textAttribute.FontSize = 15 / Ratio;
                double a = 15 / Ratio;
                if (IsRMouseDown || IsLMouseDown)
                {
                    FormattedText formattedRText = new($"({(int)RMouseDownP.X},{(int)RMouseDownP.Y})", CultureInfo.CurrentCulture, textAttribute.FlowDirection, new Typeface(textAttribute.FontFamily, textAttribute.FontStyle, textAttribute.FontWeight, textAttribute.FontStretch), textAttribute.FontSize, textAttribute.Brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                    dc.DrawText(formattedRText, RMouseDownP + new Vector(a, 2 * a));
                }

                FormattedText formattedText = new(angle.ToString("F3") + "°", CultureInfo.CurrentCulture, textAttribute.FlowDirection, new Typeface(textAttribute.FontFamily, textAttribute.FontStyle, textAttribute.FontWeight, textAttribute.FontStretch), textAttribute.FontSize, textAttribute.Brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                dc.DrawText(formattedText, RMouseDownP + new Vector(a, a));
            }
            else if (Mode == 2)
            {
                double angle1 = (angle + 45) * Math.PI / 180.0;
                // 旋转变换
                List<Point> intersectionPoints = ReferenceLine.CalculateIntersectionPoints(ActualHeight, ActualWidth, CenterPoint + new Vector(5 / Ratio * Math.Cos(angle1), 5 / Ratio * Math.Sin(angle1)), angle);

                if (intersectionPoints.Count == 4)
                {
                    dc.DrawLine(pen, intersectionPoints[0], intersectionPoints[1]); // 水平线,
                    dc.DrawLine(pen, intersectionPoints[2], intersectionPoints[3]); // 垂直线
                }
                intersectionPoints = ReferenceLine.CalculateIntersectionPoints(ActualHeight, ActualWidth, CenterPoint - new Vector(5 / Ratio * Math.Cos(angle1), 5/ Ratio * Math.Sin(angle1)), angle);
                if (intersectionPoints.Count == 4)
                {
                    dc.DrawLine(pen, intersectionPoints[0], intersectionPoints[1]); // 水平线,
                    dc.DrawLine(pen, intersectionPoints[2], intersectionPoints[3]); // 垂直线
                }


                TextAttribute textAttribute = new();
                textAttribute.FontSize = 15 / Ratio;
                double a = 15 / Ratio;
                if (IsRMouseDown || IsLMouseDown)
                {
                    FormattedText formattedRText = new($"({(int)RMouseDownP.X},{(int)RMouseDownP.Y})", CultureInfo.CurrentCulture, textAttribute.FlowDirection, new Typeface(textAttribute.FontFamily, textAttribute.FontStyle, textAttribute.FontWeight, textAttribute.FontStretch), textAttribute.FontSize, textAttribute.Brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                    dc.DrawText(formattedRText, RMouseDownP + new Vector(a, 2 * a));
                }


                FormattedText formattedText = new(angle.ToString("F3") + "°", CultureInfo.CurrentCulture, textAttribute.FlowDirection, new Typeface(textAttribute.FontFamily, textAttribute.FontStyle, textAttribute.FontWeight, textAttribute.FontStretch), textAttribute.FontSize, textAttribute.Brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
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
                    18 / Ratio,
                    Brushes.Red,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip
                );
                dc.DrawText(lockText, new Point(10, 10));

            }
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


        public double PointX { get => _PointX; set { _PointX = value; OnPropertyChanged(); } }
        private double _PointX;

        public double PointY { get => _PointY; set { _PointY = value; OnPropertyChanged(); } }
        private double _PointY;
        public int Mode { get => _Mode; set { _Mode = value; OnPropertyChanged(); } }
        private int _Mode ;

        public double Angle { get => _Angle; set { _Angle = value; OnPropertyChanged(); } }
        private double _Angle ;
    }


    public class ToolReferenceLine: IEditorToggleToolBase
    {
        private Zoombox ZoomboxSub => EditorContext.Zoombox;
        private DrawCanvas Image => EditorContext.DrawCanvas;

        public ImageViewModel ImageViewModel => EditorContext.ImageViewModel;

        public EditorContext EditorContext { get; set; }

        public ToolReferenceLine(EditorContext editorContext)
        {
            EditorContext = editorContext;
            ToolBarLocal = ToolBarLocal.Draw;
            Order = 10;
            Icon = IEditorToolFactory.TryFindResource("ConcentricCirclesDrawImg");

        }
             
        public ReferenceLine ReferenceLine { get; set; } = new ReferenceLine();


        private void SetMode(int i)
        {
            if (i == -1)
            {
                IsChecked = false;
            }
            else
            {
                IsChecked = true;
                ReferenceLine.Mode = i;
                ReferenceLine.Render();
            }
        }


        public override bool IsChecked
        {
            get => _IsChecked; set
            {
                if (_IsChecked == value) return;
                _IsChecked = value;
                DrawVisualImageControl(_IsChecked);

                if (value)
                {
                    EditorContext.DrawEditorManager.SetCurrentDrawEditor(this);

     

                    ReferenceLine.Ratio = ZoomboxSub.ContentMatrix.M11;
                    ReferenceLine.ActualWidth = Image.ActualWidth;
                    ReferenceLine.ActualHeight = Image.ActualHeight;
                    ReferenceLine.RMouseDownP = new Point(Image.ActualWidth / 2, Image.ActualHeight / 2);
                    ReferenceLine.PointLen = new Vector();

                    ReferenceLine.Attribute.Angle = 0;
                    ReferenceLine.Attribute.Pen = new Pen(ReferenceLine.Attribute.Brush, 1 / ReferenceLine.Ratio);
                    Image.MouseMove += MouseMove;
                    Image.PreviewMouseLeftButtonDown += PreviewMouseLeftButtonDown;
                    Image.PreviewMouseUp += PreviewMouseUp;
                    Image.MouseDoubleClick += Image_MouseDoubleClick;
                    ZoomboxSub.LayoutUpdated += ZoomboxSub_LayoutUpdated;

                }
                else
                {
                    EditorContext.DrawEditorManager.SetCurrentDrawEditor(null);


                    Image.MouseMove -= MouseMove;
                    Image.PreviewMouseLeftButtonDown -= PreviewMouseLeftButtonDown;
                    Image.PreviewMouseUp -= PreviewMouseUp;
                    ZoomboxSub.LayoutUpdated -= ZoomboxSub_LayoutUpdated;
                }
                OnPropertyChanged();
            }
        }
        private void Image_MouseDoubleClick(object sender, RoutedEventArgs e)
        {
            if (sender is DrawCanvas canvas)
            {
                var position = Mouse.GetPosition(canvas);
                ReferenceLine.IsLocked = !ReferenceLine.IsLocked;
                ReferenceLine.Render();
            }
        }

        private void ZoomboxSub_LayoutUpdated(object? sender, EventArgs e)
        {
            if (ReferenceLine.Ratio != ZoomboxSub.ContentMatrix.M11)
            {
                ReferenceLine.Ratio = ZoomboxSub.ContentMatrix.M11;
                ReferenceLine.Attribute.Pen = new Pen(ReferenceLine.Attribute.Brush, 1 / ReferenceLine.Ratio);
                ReferenceLine.Render();
            }
        }


        /// <summary>
        /// Handles mouse left button down event.
        /// - Left-click: Sets the center point of the reference line
        /// - Ctrl+Left-click: Sets rotation angle (drag to rotate)
        /// </summary>
        private void PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (ReferenceLine.IsLocked) return;

            // Check if Ctrl key is pressed for rotation mode
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
            {
                // Ctrl+Left-click for rotation
                ReferenceLine.LMouseDownP = Mouse.GetPosition(Image);
                ReferenceLine.IsLMouseDown = true;
                ReferenceLine.PointLen = ReferenceLine.LMouseDownP - ReferenceLine.RMouseDownP;
                ReferenceLine.Attribute.Angle = ReferenceLine.CalculateAngle(ReferenceLine.RMouseDownP, ReferenceLine.RMouseDownP + ReferenceLine.PointLen);
                ReferenceLine.Render();
            }
            else
            {
                // Normal left-click for setting center point
                ReferenceLine.RMouseDownP = Mouse.GetPosition(Image);
                ReferenceLine.IsRMouseDown = true;
                ReferenceLine.Render();
            }
        }




        private void PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (ReferenceLine.IsLocked) return;

            ReferenceLine.IsRMouseDown = false;
            ReferenceLine.IsLMouseDown = false;
            ReferenceLine.Render();
        }





        /// <summary>
        /// Handles mouse move event to update reference line position/rotation while dragging
        /// - During normal left-click drag: Updates center point
        /// - During Ctrl+Left-click drag: Updates rotation angle
        /// </summary>
        private void MouseMove(object sender, MouseEventArgs e)
        {
            if (IsChecked && !ReferenceLine.IsLocked && (ReferenceLine.IsRMouseDown || ReferenceLine.IsLMouseDown))
            {
                if (ReferenceLine.IsRMouseDown)
                {
                    ReferenceLine.RMouseDownP = e.GetPosition(Image);
                }
                if (ReferenceLine.IsLMouseDown)
                {
                    ReferenceLine.LMouseDownP = e.GetPosition(Image);
                    ReferenceLine.PointLen = ReferenceLine.LMouseDownP - ReferenceLine.RMouseDownP;
                    ReferenceLine.Attribute.Angle = ReferenceLine.CalculateAngle(ReferenceLine.RMouseDownP, ReferenceLine.RMouseDownP + ReferenceLine.PointLen);
                }
                ReferenceLine.Render();
            }
        }


        private bool _IsChecked;

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
