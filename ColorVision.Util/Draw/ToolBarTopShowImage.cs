using ColorVision.Extension;
using System.Windows;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows.Input;
using System;
using System.Globalization;
using System.Windows.Controls;
using System.Reflection.Metadata;
using System.Collections.Generic;
using System.Windows.Media.Media3D;

namespace ColorVision.Draw
{
    public class ToolConcentricCircle
    {
        private ZoomboxSub ZoomboxSub { get; set; }
        private DrawCanvas Image { get; set; }

        public DrawingVisual DrawVisualImage { get; set; }

        public ToolConcentricCircle(ZoomboxSub zombox, DrawCanvas drawCanvas)
        {
            ZoomboxSub = zombox;
            Image = drawCanvas;
            DrawVisualImage = new DrawingVisual();
        }
        public bool IsShow
        {
            get => _IsShow; set
            {
                if (_IsShow == value) return;
                _IsShow = value;
                DrawVisualImageControl(_IsShow);
                Image.ContextMenu = null;
                RMouseDownP = new Point(Image.ActualWidth / 2, Image.ActualHeight / 2);
                if (value)
                {
                    Image.MouseMove += MouseMove;
                    Image.PreviewMouseLeftButtonDown += PreviewMouseLeftButtonDown;
                    Image.PreviewMouseRightButtonDown += Image_PreviewMouseRightButtonDown;
                    Image.PreviewMouseUp += PreviewMouseUp; 
                }
                else
                {
                    Image.MouseMove -= MouseMove;
                    Image.PreviewMouseLeftButtonDown -= PreviewMouseLeftButtonDown;
                    Image.PreviewMouseRightButtonDown -= Image_PreviewMouseRightButtonDown;
                    Image.PreviewMouseUp -= PreviewMouseUp;
                }
            }
        }


        private bool IsRMouseDown;
        private bool IsLMouseDown;

        private Point RMouseDownP;
        private Point LMouseDownP;
        private System.Windows.Vector PointLen;


        private void PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            RMouseDownP = Mouse.GetPosition(Image);
            IsRMouseDown = true;
            Render();
        }
        private void Image_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            LMouseDownP = Mouse.GetPosition(Image);
            IsLMouseDown = true;
            PointLen = LMouseDownP - RMouseDownP;
            Render();
        }

        private void PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            IsRMouseDown = false;
            IsLMouseDown = false;
        }

       

        private void MouseMove(object sender, MouseEventArgs e)
        {
            if (IsShow && (IsRMouseDown|| IsLMouseDown))
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

            // Define the second lenc for the line from the given lenc and angle
            Point pAngle = new Point(p.X + Math.Cos(angleRad), p.Y + Math.Sin(angleRad));

            // Calculate the intersection of the two lines
            double detL1 = Det(p.X, p.Y, pAngle.X, pAngle.Y);
            double detL2 = Det(p1.X, p1.Y, p2.X, p2.Y);
            double x1mx2 = p.X - pAngle.X;
            double x3mx4 = p1.X - p2.X;
            double y1my2 = p.Y - pAngle.Y;
            double y3my4 = p1.Y - p2.Y;

            double xnom = Det(detL1, x1mx2, detL2, x3mx4);
            double ynom = Det(detL1, y1my2, detL2, y3my4);
            double denom = Det(x1mx2, y1my2, x3mx4, y3my4);

            if (denom == 0.0) // Lines are parallel
            {
                return null;
            }

            double x = xnom / denom;
            double y = ynom / denom;
            Point intersection = new Point(x, y);

            // Check if the intersection lenc lies on the line segment p1-p2
            if (!IsBetween(p1, p2, intersection))
            {
                return null; // Intersection is not within the line segment
            }

            return intersection;
        }

        private static bool IsBetween(Point A, Point B, Point C)
        {
            bool withinX = (Math.Min(A.X, B.X) <= C.X) && (C.X <= Math.Max(A.X, B.X));
            bool withinY = (Math.Min(A.Y, B.Y) <= C.Y) && (C.Y <= Math.Max(A.Y, B.Y));
            return withinX && withinY;
        }

        public List<Point> CalculateIntersectionPoints(double width ,double height, Point point,double angle)
        {
            List<Point> points = new List<Point>();
            if (GetIntersection(point, angle, new Point(0, 0), new Point(0, width)) is Point point1)
                points.Add(point1);
            if (GetIntersection(point, angle, new Point(0, width), new Point(height, width)) is Point point2)
                points.Add(point2);
            if (GetIntersection(point, angle, new Point(height, width), new Point(height, 0)) is Point point3)
                points.Add(point3);
            if (GetIntersection(point, angle, new Point(height, 0), new Point(0, 0)) is Point point4)
                points.Add(point4);


            if (GetIntersection(point, angle +90, new Point(0, 0), new Point(0, width)) is Point point5)
                points.Add(point5);
            if (GetIntersection(point, angle + 90, new Point(0, width), new Point(height, width)) is Point point6)
                points.Add(point6);
            if (GetIntersection(point, angle + 90, new Point(height, width), new Point(height, 0)) is Point point7)
                points.Add(point7);
            if (GetIntersection(point, angle + 90, new Point(height, 0), new Point(0, 0)) is Point point8)
                points.Add(point8);

            return points;
        }

        public void Render()
        {
            using DrawingContext dc = DrawVisualImage.RenderOpen();
            Brush brush = Brushes.Red;
            Pen pen = new Pen(brush, 1 / ZoomboxSub.ContentMatrix.M11);


            Point ActL = RMouseDownP + PointLen;

            int lenc = (int)Math.Sqrt(PointLen.X * PointLen.X + PointLen.Y * PointLen.Y);

            dc.DrawEllipse(Brushes.Transparent, pen, RMouseDownP, lenc, lenc);
            dc.DrawEllipse(Brushes.Transparent, pen, RMouseDownP, lenc+10, lenc+ 10);
            dc.DrawEllipse(Brushes.Transparent, pen, RMouseDownP, lenc +30, lenc+30);
            dc.DrawEllipse(Brushes.Transparent, pen, RMouseDownP, lenc+50, lenc + 50);
            dc.DrawEllipse(Brushes.Transparent, pen, RMouseDownP, lenc + 100, lenc + 100);
            dc.DrawEllipse(Brushes.Transparent, pen, RMouseDownP, lenc + 200, lenc + 200);



            double angle = CalculateAngle(RMouseDownP, ActL);
            Point CenterPoint = RMouseDownP;
            double ActualWidth = Image.ActualWidth;
            double ActualHeight = Image.ActualHeight;


            // 旋转变换
            List<Point> intersectionPoints = CalculateIntersectionPoints(ActualHeight, ActualWidth, CenterPoint, angle);

            if (intersectionPoints.Count == 4)
            {
                dc.DrawLine(pen, intersectionPoints[0], intersectionPoints[1]); // 水平线
                dc.DrawLine(pen, intersectionPoints[2], intersectionPoints[3]); // 垂直线
            }

            TextAttribute textAttribute = new TextAttribute();
            textAttribute.FontSize = 15 / ZoomboxSub.ContentMatrix.M11;

            FormattedText formattedText = new FormattedText(angle.ToString("F1") + "°", CultureInfo.CurrentCulture, textAttribute.FlowDirection, new Typeface(textAttribute.FontFamily, textAttribute.FontStyle, textAttribute.FontWeight, textAttribute.FontStretch), textAttribute.FontSize, textAttribute.Brush, VisualTreeHelper.GetDpi(DrawVisualImage).PixelsPerDip);
            dc.DrawText(formattedText, RMouseDownP + new System.Windows.Vector(20, 20));


            

            //dc.PushTransform(new RotateTransform(angle, RMouseDownP.X, RMouseDownP.Y));
            FormattedText formattedText1 = new FormattedText(lenc.ToString("F1"), CultureInfo.CurrentCulture, textAttribute.FlowDirection, new Typeface(textAttribute.FontFamily, textAttribute.FontStyle, textAttribute.FontWeight, textAttribute.FontStretch), textAttribute.FontSize, textAttribute.Brush, VisualTreeHelper.GetDpi(DrawVisualImage).PixelsPerDip);
            dc.DrawText(formattedText1, RMouseDownP + PointLen);

            FormattedText formattedText2 = new FormattedText((lenc + 10).ToString("F1"), CultureInfo.CurrentCulture, textAttribute.FlowDirection, new Typeface(textAttribute.FontFamily, textAttribute.FontStyle, textAttribute.FontWeight, textAttribute.FontStretch), textAttribute.FontSize, textAttribute.Brush, VisualTreeHelper.GetDpi(DrawVisualImage).PixelsPerDip);
            dc.DrawText(formattedText2, RMouseDownP + PointLen );

            FormattedText formattedText3 = new FormattedText((lenc+1).ToString("F1"), CultureInfo.CurrentCulture, textAttribute.FlowDirection, new Typeface(textAttribute.FontFamily, textAttribute.FontStyle, textAttribute.FontWeight, textAttribute.FontStretch), textAttribute.FontSize, textAttribute.Brush, VisualTreeHelper.GetDpi(DrawVisualImage).PixelsPerDip);
            dc.DrawText(formattedText3, RMouseDownP - PointLen );



            //dc.Pop();
        }

        private bool _IsShow;

        public void DrawVisualImageControl(bool Control)
        {
            if (Control)
            {
                if (!Image.ContainsVisual(DrawVisualImage))
                    Image.AddVisual(DrawVisualImage);
            }
            else
            {
                if (Image.ContainsVisual(DrawVisualImage))
                    Image.RemoveVisual(DrawVisualImage);
            }
        }

    }



    public class ToolBarTopShowImage
    {
        private ZoomboxSub ZoomboxSub { get; set; }
        private DrawCanvas Image { get; set; }

        public DrawingVisual DrawVisualImage { get; set; }


        public ToolBarTopShowImage(ZoomboxSub zombox, DrawCanvas drawCanvas)
        {
            ZoomboxSub = zombox;
            Image = drawCanvas;
            DrawVisualImage = new DrawingVisual();
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
                    Image.MouseMove += MouseMove;
                    Image.MouseEnter += MouseEnter;
                    Image.MouseLeave += MouseLeave;
                }
                else
                {
                    Image.MouseMove -= MouseMove;
                    Image.MouseEnter -= MouseEnter;
                    Image.MouseLeave -= MouseLeave;
                }
            }
        }
        private bool _IsShow;


        public class ImageInfo
        {
            public int X { get; set; }
            public int Y { get; set; }
            public double X1 { get; set; }
            public double Y1 { get; set; }
            public int R { get; set; }
            public int G { get; set; }
            public int B { get; set; }
            public string Hex { get; set; }
            public SolidColorBrush Color { get; set; }
        }

        public void DrawImage(Point actPoint, Point disPoint, ImageInfo imageInfo)
        {
            if (Image.Source is BitmapImage bitmapImage && disPoint.X > 60 && disPoint.X < bitmapImage.PixelWidth - 60 && disPoint.Y > 45 && disPoint.Y < bitmapImage.PixelHeight - 45)
            {
                CroppedBitmap croppedBitmap = new CroppedBitmap(bitmapImage, new Int32Rect(disPoint.X.ToInt32() - 60, disPoint.Y.ToInt32() - 45, 120, 90));

                using DrawingContext dc = DrawVisualImage.RenderOpen();

                var transform = new MatrixTransform(1 / ZoomboxSub.ContentMatrix.M11, ZoomboxSub.ContentMatrix.M12, ZoomboxSub.ContentMatrix.M21, 1 / ZoomboxSub.ContentMatrix.M22, (1 - 1 / ZoomboxSub.ContentMatrix.M11) * actPoint.X, (1 - 1 / ZoomboxSub.ContentMatrix.M22) * actPoint.Y);
                dc.PushTransform(transform);

                dc.DrawImage(croppedBitmap, new Rect(new Point(actPoint.X, actPoint.Y + 25), new Size(120, 90)));

                dc.DrawLine(new Pen(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00B1FF")), 3), new Point(actPoint.X + 59, actPoint.Y + 25), new Point(actPoint.X + 59, actPoint.Y + 25 + 90));
                dc.DrawLine(new Pen(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00B1FF")), 3), new Point(actPoint.X, actPoint.Y + 25 + 44), new Point(actPoint.X + 120, actPoint.Y + 25 + 44));


                double x1 = actPoint.X;
                double y1 = actPoint.Y + 25;

                double width = 120;
                double height = 90;


                dc.DrawLine(new Pen(Brushes.Black, 0.5), new Point(x1, y1 - 0.25), new Point(x1, y1 + height + 0.25));
                dc.DrawLine(new Pen(Brushes.Black, 0.5), new Point(x1, y1), new Point(x1 + width, y1));
                dc.DrawLine(new Pen(Brushes.Black, 0.5), new Point(x1 + width, y1 - 0.25), new Point(x1 + width, y1 + height + 0.25));
                dc.DrawLine(new Pen(Brushes.Black, 0.5), new Point(x1, y1 + height), new Point(x1 + width, y1 + height));

                x1++;
                y1++;
                width -= 2;
                height -= 2;
                dc.DrawLine(new Pen(Brushes.White, 1.5), new Point(x1, y1 - 0.75), new Point(x1, y1 + height + 0.75));
                dc.DrawLine(new Pen(Brushes.White, 1.5), new Point(x1, y1), new Point(x1 + width, y1));
                dc.DrawLine(new Pen(Brushes.White, 1.5), new Point(x1 + width, y1 - 0.75), new Point(x1 + width, y1 + height + 0.75));
                dc.DrawLine(new Pen(Brushes.White, 1.5), new Point(x1, y1 + height), new Point(x1 + width, y1 + height));

                dc.DrawRectangle(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#AA000000")), new Pen(Brushes.White, 0), new Rect(x1 - 1, y1 + height + 1, width + 2, 45));

                Brush brush = Brushes.White;
                FontFamily fontFamily = new FontFamily("Arial");
                double fontSize = 10;
                FormattedText formattedText = new FormattedText($"R:{imageInfo.R}  G:{imageInfo.G}  B:{imageInfo.B}", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(fontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), fontSize, brush, VisualTreeHelper.GetDpi(DrawVisualImage).PixelsPerDip);
                dc.DrawText(formattedText, new Point(x1 + 5, y1 + height + 5));
                FormattedText formattedTex1 = new FormattedText($"({imageInfo.X},{imageInfo.Y})", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(fontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), fontSize, brush, VisualTreeHelper.GetDpi(DrawVisualImage).PixelsPerDip);
                dc.DrawText(formattedTex1, new Point(x1 + 5, y1 + height + 31));

                FormattedText formattedTex3 = new FormattedText($"{imageInfo.Hex}", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(fontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), fontSize, brush, VisualTreeHelper.GetDpi(DrawVisualImage).PixelsPerDip);
                dc.DrawText(formattedTex3, new Point(x1 + 5, y1 + height + 18));
                dc.Pop();
                if (DrawVisualImage.Effect is not DropShadowEffect)
                    DrawVisualImage.Effect = new DropShadowEffect() { Opacity = 0.5 };

            }
        }


        public void MouseMove(object sender, MouseEventArgs e)
        {
            if (IsShow && sender is DrawCanvas drawCanvas && drawCanvas.Source is BitmapSource bitmap)
            {
                var point = e.GetPosition(drawCanvas);

                var controlWidth = drawCanvas.ActualWidth;
                var controlHeight = drawCanvas.ActualHeight;


                int imageWidth = bitmap.PixelWidth;
                int imageHeight = bitmap.PixelHeight;
                var actPoint = new Point(point.X, point.Y);

                point.X = point.X / controlWidth * imageWidth;
                point.Y = point.Y / controlHeight * imageHeight;

                var bitPoint = new Point(point.X.ToInt32(), point.Y.ToInt32());

                if (point.X.ToInt32() >= 0 && point.X.ToInt32() < bitmap.PixelWidth && point.Y.ToInt32() >= 0 && point.Y.ToInt32() < bitmap.PixelHeight)
                {
                    var color = bitmap.GetPixelColor(point.X.ToInt32(), point.Y.ToInt32());
                    DrawImage(actPoint, bitPoint, new ImageInfo
                    {
                        X = point.X.ToInt32(),
                        Y = point.Y.ToInt32(),
                        X1 = point.X,
                        Y1 = point.Y,

                        R = color.R,
                        G = color.G,
                        B = color.B,
                        Color = new SolidColorBrush(color),
                        Hex = color.ToHex()
                    });
                }
            }


        }



        public void MouseEnter(object sender, MouseEventArgs e) => DrawVisualImageControl(true);

        public void MouseLeave(object sender, MouseEventArgs e) => DrawVisualImageControl(false);

        public void DrawVisualImageControl(bool Control)
        {
            if (Control)
            {
                if (!Image.ContainsVisual(DrawVisualImage))
                    Image.AddVisual(DrawVisualImage);
            }
            else
            {
                if (Image.ContainsVisual(DrawVisualImage))
                    Image.RemoveVisual(DrawVisualImage);
            }
        }
    }
}
