using ColorVision.Common.MVVM;
using Newtonsoft.Json;
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
    /// <summary>
    /// 参考线模式
    /// </summary>
    public enum ReferenceLineMode
    {
        /// <summary>
        /// 同心圆模式 - 显示十字参考线和同心圆
        /// </summary>
        ConcentricCircles = 0,
        
        /// <summary>
        /// 简单十字模式 - 仅显示十字参考线
        /// </summary>
        SimpleCross = 1,
        
        /// <summary>
        /// 双十字模式 - 显示双层偏移的十字参考线
        /// </summary>
        DoubleCross = 2,

        /// <summary>
        /// 斜十字模式 - 显示相对当前角度偏移45°的对角线十字（X形）
        /// </summary>
        DiagonalCross = 3,

        /// <summary>
        /// 十字遮罩模式 - 显示十字参考线和中心透明遮罩
        /// </summary>
        CrossMask = 4
    }

    /// <summary>
    /// 遮罩形状
    /// </summary>
    public enum MaskShape
    {
        /// <summary>
        /// 圆形遮罩
        /// </summary>
        Circle = 0,

        /// <summary>
        /// 矩形遮罩
        /// </summary>
        Rectangle = 1,

        /// <summary>
        /// 人脸形状（保留）
        /// </summary>
        Face = 2,

        /// <summary>
        /// 国徽形状（保留）
        /// </summary>
        Emblem = 3
    }

    /// <summary>
    /// 中心覆盖形状（光栅圆/矩形）
    /// </summary>
    public enum CenterOverlayShape
    {
        /// <summary>
        /// 无覆盖
        /// </summary>
        None = 0,

        /// <summary>
        /// 圆形光栅
        /// </summary>
        Circle = 1,

        /// <summary>
        /// 矩形光栅
        /// </summary>
        Rectangle = 2
    }

    public class DVLineDVContextMenu : IDVContextMenu
    {
        public Type ContextType => typeof(ReferenceLine);

        public IEnumerable<MenuItem> GetContextMenuItems(EditorContext context, object obj)
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
                
                MenuItem resetMenuItem = new() { Header = "重置为图像中心" };
                resetMenuItem.Click += (s, e) =>
                {
                    // Reset to image center and rotation angle to 0
                    referenceLine.RMouseDownP = new Point(referenceLine.ActualWidth / 2, referenceLine.ActualHeight / 2);
                    referenceLine.Attribute.Angle = 0;
                    referenceLine.PointLen = new Vector();
                    referenceLine.Render();
                };
                MenuItems.Add(resetMenuItem);
            }
            return MenuItems;
        }
    }
    
    
    public class ReferenceLine: DrawingVisualBase<ReferenceLineParam>
    {
        // Constants for shape approximations
        private const double FaceShapeWidthRatio = 0.8;
        
        public Pen Pen { get => Attribute.Pen; set => Attribute.Pen = value; }

        public ReferenceLine()
        {
            Attribute = new ReferenceLineParam();
            Attribute.Pen  = new Pen(Attribute.Brush, 1);
            Attribute.PropertyChanged += (s, e) => Render();
        }

        public ReferenceLine(ReferenceLineParam referenceLineParam)
        {
            Attribute = referenceLineParam;
            Attribute.Pen = new Pen(Attribute.Brush, 1);
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
        public ReferenceLineMode Mode { get => Attribute.Mode; set { Attribute.Mode = value; } }

        SolidColorBrush SolidColorBrush = new SolidColorBrush(Color.FromArgb(1, 255, 255, 255));

        public override void Render()
        {
            using DrawingContext dc = RenderOpen();
            dc.DrawRectangle(SolidColorBrush, new Pen(Brushes.Transparent, 0), new Rect(0,0,ActualWidth,ActualHeight));

            Pen pen = Attribute.Pen;

            double angle = Attribute.Angle;
            Point CenterPoint = RMouseDownP;

            if (Mode == ReferenceLineMode.ConcentricCircles)
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
            else if (Mode == ReferenceLineMode.SimpleCross)
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
            else if (Mode == ReferenceLineMode.DoubleCross)
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
            else if (Mode == ReferenceLineMode.DiagonalCross)
            {
                // 斜十字：在当前角度基础上偏移45°，绘制X形两条对角线
                List<Point> intersectionPoints = ReferenceLine.CalculateIntersectionPoints(ActualHeight, ActualWidth, CenterPoint, angle + 45);
                if (intersectionPoints.Count == 4)
                {
                    dc.DrawLine(pen, intersectionPoints[0], intersectionPoints[1]);
                    dc.DrawLine(pen, intersectionPoints[2], intersectionPoints[3]);
                }

                TextAttribute textAttribute = new();
                textAttribute.FontSize = 15 / Ratio;
                double a = 15 / Ratio;
                if (IsRMouseDown || IsLMouseDown)
                {
                    FormattedText formattedRText = new($"({(int)RMouseDownP.X},{(int)RMouseDownP.Y})", CultureInfo.CurrentCulture, textAttribute.FlowDirection, new Typeface(textAttribute.FontFamily, textAttribute.FontStyle, textAttribute.FontWeight, textAttribute.FontStretch), textAttribute.FontSize, textAttribute.Brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                    dc.DrawText(formattedRText, RMouseDownP + new Vector(a, 2 * a));
                }

                // 仍显示基础角度，便于和其他模式一致
                FormattedText formattedText = new(angle.ToString("F3") + "°", CultureInfo.CurrentCulture, textAttribute.FlowDirection, new Typeface(textAttribute.FontFamily, textAttribute.FontStyle, textAttribute.FontWeight, textAttribute.FontStretch), textAttribute.FontSize, textAttribute.Brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                dc.DrawText(formattedText, RMouseDownP + new Vector(a, a));
            }
            else if (Mode == ReferenceLineMode.CrossMask)
            {
                // 十字遮罩模式：绘制十字参考线，边缘遮罩，中心保持透明用于对焦
                
                // 1. 绘制边缘遮罩，保留中间透明区域
                double maskSize = Attribute.MaskSize;

                // 创建外部矩形（整个画布区域）
                // 修改：将矩形范围向外扩展（例如各方向扩展 2 像素），防止因抗锯齿或精度问题导致边缘出现未遮盖的细线
                RectangleGeometry outerRect = new RectangleGeometry(new Rect(-5, -5, ActualWidth +10, ActualHeight +10));
                
                // 根据遮罩形状创建中心透明区域
                Geometry innerGeometry;
                switch (Attribute.MaskShape)
                {
                    case MaskShape.Circle:
                        innerGeometry = new EllipseGeometry(CenterPoint, maskSize, maskSize);
                        break;
                    case MaskShape.Rectangle:
                        innerGeometry = new RectangleGeometry(new Rect(
                            CenterPoint.X - maskSize, 
                            CenterPoint.Y - maskSize, 
                            maskSize * 2, 
                            maskSize * 2));
                        break;
                    case MaskShape.Face:
                        // 人脸形状（保留）- 目前用椭圆近似
                        innerGeometry = new EllipseGeometry(CenterPoint, maskSize * FaceShapeWidthRatio, maskSize);
                        break;
                    case MaskShape.Emblem:
                        // 国徽形状（保留）- 目前用圆形近似
                        innerGeometry = new EllipseGeometry(CenterPoint, maskSize, maskSize);
                        break;
                    default:
                        innerGeometry = new EllipseGeometry(CenterPoint, maskSize, maskSize);
                        break;
                }
                
                // 使用GeometryGroup配合EvenOdd填充规则创建带孔的遮罩
                // EvenOdd规则：重叠区域变透明，实现边缘遮罩、中心透明的效果
                GeometryGroup maskGeometry = new GeometryGroup();
                maskGeometry.FillRule = FillRule.EvenOdd;
                maskGeometry.Children.Add(outerRect);
                maskGeometry.Children.Add(innerGeometry);

                
                // 2. 绘制十字参考线（红色）
                List<Point> intersectionPoints = ReferenceLine.CalculateIntersectionPoints(ActualHeight, ActualWidth, CenterPoint, angle);
                if (intersectionPoints.Count == 4)
                {
                    dc.DrawLine(pen, intersectionPoints[0], intersectionPoints[1]); // 水平线
                    dc.DrawLine(pen, intersectionPoints[2], intersectionPoints[3]); // 垂直线
                }

                // 绘制半透明黑色遮罩（边缘黑色，中心透明）
                SolidColorBrush maskBrush = new SolidColorBrush(Color.FromArgb(Attribute.MaskOpacity, Attribute.Color.R, Attribute.Color.G, Attribute.Color.B));
                dc.DrawGeometry(maskBrush, null, maskGeometry);

                // 3. 绘制中心覆盖光栅（黄色）
                if (Attribute.CenterOverlay != CenterOverlayShape.None)
                {
                    double overlaySize = Attribute.CenterOverlaySize;
                    
                    // 如果设置了物理尺寸，则根据像素/单位转换
                    if (Attribute.PhysicalSizeX > double.Epsilon && Attribute.PixelPerUnit > double.Epsilon)
                    {
                        overlaySize = Attribute.PhysicalSizeX * Attribute.PixelPerUnit;
                    }
                    
                    Pen overlayPen = new Pen(Attribute.OverlayBrush, Attribute.LineWidth / Ratio);
                    
                    if (Attribute.CenterOverlay == CenterOverlayShape.Circle)
                    {
                        double overlaySizeY = overlaySize;
                        if (Attribute.PhysicalSizeY > double.Epsilon && Attribute.PixelPerUnit > double.Epsilon)
                        {
                            overlaySizeY = Attribute.PhysicalSizeY * Attribute.PixelPerUnit;
                        }
                        dc.DrawEllipse(null, overlayPen, CenterPoint, overlaySize, overlaySizeY);
                    }
                    else if (Attribute.CenterOverlay == CenterOverlayShape.Rectangle)
                    {
                        double overlaySizeY = overlaySize;
                        if (Attribute.PhysicalSizeY > double.Epsilon && Attribute.PixelPerUnit > double.Epsilon)
                        {
                            overlaySizeY = Attribute.PhysicalSizeY * Attribute.PixelPerUnit;
                        }
                        dc.DrawRectangle(null, overlayPen, new Rect(
                            CenterPoint.X - overlaySize, 
                            CenterPoint.Y - overlaySizeY, 
                            overlaySize * 2, 
                            overlaySizeY * 2));
                    }
                }
                
                // 4. 显示文本信息
                TextAttribute textAttribute = new();
                textAttribute.FontSize = 15 / Ratio;
                double a = 15 / Ratio;
                
                if (IsRMouseDown || IsLMouseDown)
                {
                    FormattedText formattedRText = new($"({(int)RMouseDownP.X},{(int)RMouseDownP.Y})", CultureInfo.CurrentCulture, textAttribute.FlowDirection, new Typeface(textAttribute.FontFamily, textAttribute.FontStyle, textAttribute.FontWeight, textAttribute.FontStretch), textAttribute.FontSize, textAttribute.Brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                    dc.DrawText(formattedRText, RMouseDownP + new Vector(a, 2 * a));
                }
                
                FormattedText angleText = new(angle.ToString("F3") + "°", CultureInfo.CurrentCulture, textAttribute.FlowDirection, new Typeface(textAttribute.FontFamily, textAttribute.FontStyle, textAttribute.FontWeight, textAttribute.FontStretch), textAttribute.FontSize, textAttribute.Brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                dc.DrawText(angleText, RMouseDownP + new Vector(a, a));
            }

            if (Attribute.IsMapping)
            {
                Pen mappingPen = new Pen(Brushes.Blue, 1 / Ratio);
                // 1. 计算实际显示的宽和高 (对应 MATLAB: res * mapping)
                double mapWidth = Attribute.MappingW * Attribute.Mapping;
                double mapHeight = Attribute.MappingH * Attribute.Mapping;

                // 只有尺寸有效才绘制
                if (mapWidth > 0 && mapHeight > 0)
                {
                    // 2. 定义矩形
                    // 在 WPF 中，Rect 定义为 (Left, Top, Width, Height)
                    // 对应 MATLAB: [cx - w/2, cy - h/2, w, h]
                    Rect mapRect = new Rect(
                        CenterPoint.X - mapWidth / 2.0,
                        CenterPoint.Y - mapHeight / 2.0,
                        mapWidth,
                        mapHeight);

                    // 3. 应用旋转
                    // 使用 WPF 的变换堆栈。以 CenterPoint 为旋转中心，旋转 Angle 度。
                    // 这样矩形会跟随参考线一起旋转。
                    dc.PushTransform(new RotateTransform(angle, CenterPoint.X, CenterPoint.Y));

                    // 4. 绘制矩形
                    // Fill 为 null (透明)，Pen 使用当前属性定义的画笔
                    // 如果想要像 MATLAB 那样使用虚线，可以创建一个新的 DashStyle Pen，
                    // 但这里为了保持与 ReferenceLine 样式一致，使用了传入的 pen。
                    dc.DrawRectangle(null, mappingPen, mapRect);

                    // (可选) 如果需要绘制矩形尺寸文字，可以在这里添加 DrawText
                    // TextAttribute mapTextAttr = new TextAttribute() { FontSize = 12 / Ratio, Brush = pen.Brush };
                    // FormattedText sizeText = new FormattedText($"{Attribute.MappingW}x{Attribute.MappingH}", ...);
                    // dc.DrawText(sizeText, new Point(mapRect.Left, mapRect.Top - 20 / Ratio));

                    // 5. 恢复变换状态 (非常重要，否则后续绘制会被错误旋转)
                    dc.Pop();
                }
            }



            if (IsLocked && Attribute.IsShowLockedText)
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
        public bool IsShowLockedText { get => _IsShowLockedText; set { _IsShowLockedText = value; OnPropertyChanged(); } }
        private bool _IsShowLockedText = true;


        [Browsable(false),JsonIgnore]
        public Pen Pen { get => _Pen; set { _Pen = value; OnPropertyChanged(); } }
        private Pen _Pen;

        [DisplayName("模式")]
        public ReferenceLineMode Mode { get => _Mode; set { _Mode = value; OnPropertyChanged(); } }
        private ReferenceLineMode _Mode = ReferenceLineMode.SimpleCross;

        [DisplayName("颜色"), JsonIgnore]
        public Brush Brush { get => _Brush; set { _Brush = value; OnPropertyChanged();  if (Pen!=null) Pen.Brush = value; } }
        private Brush _Brush = Brushes.Red;

        [DisplayName("线宽")]
        public double LineWidth { get => _LineWidth; set { _LineWidth = value; OnPropertyChanged(); } }
        private double _LineWidth = 1.0;

        [DisplayName("角度")]
        public double Angle { get => _Angle; set { _Angle = value; OnPropertyChanged(); } }
        private double _Angle;

        [DisplayName("中心点X")]
        public double PointX { get => _PointX; set { _PointX = value; OnPropertyChanged(); } }
        private double _PointX;

        [DisplayName("中心点Y")]
        public double PointY { get => _PointY; set { _PointY = value; OnPropertyChanged(); } }
        private double _PointY;


        [Category("Mapping"), DisplayName("IsMapping")]
        public bool IsMapping { get => _IsMapping; set { _IsMapping = value; OnPropertyChanged(); } }
        private bool _IsMapping;

        [Category("Mapping"),DisplayName("Mapping"), PropertyVisibility(nameof(IsMapping))]
        public double Mapping { get => _Mapping; set { _Mapping = value; OnPropertyChanged(); } }
        private double _Mapping = 4;
        [Category("Mapping"), DisplayName("MappingW"), PropertyVisibility(nameof(IsMapping))]
        public double MappingW { get => _MappingW; set { _MappingW = value; OnPropertyChanged(); } }
        private double _MappingW = 2436.0;
        [Category("Mapping"), DisplayName("MappingH"), PropertyVisibility(nameof(IsMapping))]
        public double MappingH { get => _MappingH; set { _MappingH = value; OnPropertyChanged(); } }
        private double _MappingH = 1080.0;




        // 遮罩相关属性
        [Category("遮罩设置"), DisplayName("遮罩形状"),PropertyVisibility(nameof(Mode), ReferenceLineMode.CrossMask)]
        public MaskShape MaskShape { get => _MaskShape; set { _MaskShape = value; OnPropertyChanged(); } }
        private MaskShape _MaskShape = MaskShape.Circle;

        [Category("遮罩设置"), DisplayName("遮罩透明区大小"), PropertyVisibility(nameof(Mode), ReferenceLineMode.CrossMask)]
        public double MaskSize { get => _MaskSize; set { _MaskSize = value; OnPropertyChanged(); } }
        private double _MaskSize = 100.0;

        [Category("遮罩设置"), DisplayName("遮罩不透明度(0-255)"), PropertyVisibility(nameof(Mode), ReferenceLineMode.CrossMask)]
        public byte MaskOpacity { get => _MaskOpacity; set { _MaskOpacity = value; OnPropertyChanged(); } }
        private byte _MaskOpacity = 180;

        [Category("遮罩设置"), DisplayName("Color"), PropertyVisibility(nameof(Mode), ReferenceLineMode.CrossMask)]
        public Color Color { get; set; } = Color.FromArgb(255,0,0,0);
        // 中心覆盖光栅相关属性
        [Category("中心覆盖"), DisplayName("覆盖形状"), PropertyVisibility(nameof(Mode), ReferenceLineMode.CrossMask)]
        public CenterOverlayShape CenterOverlay { get => _CenterOverlay; set { _CenterOverlay = value; OnPropertyChanged(); } }
        private CenterOverlayShape _CenterOverlay = CenterOverlayShape.None;

        [Category("中心覆盖"), DisplayName("覆盖颜色"), JsonIgnore, PropertyVisibility(nameof(Mode), ReferenceLineMode.CrossMask)]
        public Brush OverlayBrush { get => _OverlayBrush; set { _OverlayBrush = value; OnPropertyChanged(); } }
        private Brush _OverlayBrush = Brushes.Yellow;

        [Category("中心覆盖"), DisplayName("覆盖大小"), PropertyVisibility(nameof(Mode), ReferenceLineMode.CrossMask)]
        public double CenterOverlaySize { get => _CenterOverlaySize; set { _CenterOverlaySize = value; OnPropertyChanged(); } }
        private double _CenterOverlaySize = 50.0;

        // 物理尺寸转换
        [Category("物理尺寸"), DisplayName("物理尺寸X(mm)"), PropertyVisibility(nameof(Mode), ReferenceLineMode.CrossMask)]
        public double PhysicalSizeX { get => _PhysicalSizeX; set { _PhysicalSizeX = value; OnPropertyChanged(); } }
        private double _PhysicalSizeX = 0.0;

        [Category("物理尺寸"), DisplayName("物理尺寸Y(mm)"), PropertyVisibility(nameof(Mode), ReferenceLineMode.CrossMask)]
        public double PhysicalSizeY { get => _PhysicalSizeY; set { _PhysicalSizeY = value; OnPropertyChanged(); } }
        private double _PhysicalSizeY = 0.0;

        [Category("物理尺寸"), DisplayName("像素/单位(px/mm)"), PropertyVisibility(nameof(Mode), ReferenceLineMode.CrossMask)]
        public double PixelPerUnit { get => _PixelPerUnit; set { _PixelPerUnit = value; OnPropertyChanged(); } }
        private double _PixelPerUnit = 1.0;

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
                    
                    // Only reset to image center when PointX and PointY are both 0 (first time)
                    if (ReferenceLine.Attribute.PointX == 0 && ReferenceLine.Attribute.PointY == 0)
                    {
                        ReferenceLine.RMouseDownP = new Point(Image.ActualWidth / 2, Image.ActualHeight / 2);
                        ReferenceLine.Attribute.Angle = 0;
                    }
                    
                    ReferenceLine.PointLen = new Vector();

                    ReferenceLine.Attribute.Pen = new Pen(ReferenceLine.Attribute.Brush, ReferenceLine.Attribute.LineWidth / ReferenceLine.Ratio);
                    Image.MouseMove += MouseMove;
                    Image.PreviewMouseLeftButtonDown += PreviewMouseLeftButtonDown;
                    Image.PreviewMouseUp += PreviewMouseUp;
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
                ReferenceLine.Attribute.Pen = new Pen(ReferenceLine.Attribute.Brush, ReferenceLine.Attribute.LineWidth / ReferenceLine.Ratio);
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
