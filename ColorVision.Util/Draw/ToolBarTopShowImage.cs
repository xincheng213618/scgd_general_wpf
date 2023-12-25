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
        private Vector PointLen;


        private void PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            RMouseDownP = Mouse.GetPosition(Image);
            IsRMouseDown = true;
        }
        private void Image_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            LMouseDownP = Mouse.GetPosition(Image);
            IsLMouseDown = true;
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

        private Point FindIntersection(Point line1Start, Point line1End, Point line2Start, Point line2End)
        {
            // Line AB represented as a1x + b1y = c1
            double a1 = line1End.Y - line1Start.Y;
            double b1 = line1Start.X - line1End.X;
            double c1 = a1 * (line1Start.X) + b1 * (line1Start.Y);

            // Line CD represented as a2x + b2y = c2
            double a2 = line2End.Y - line2Start.Y;
            double b2 = line2Start.X - line2End.X;
            double c2 = a2 * (line2Start.X) + b2 * (line2Start.Y);

            double determinant = a1 * b2 - a2 * b1;

            if (determinant == 0)
            {
                // The lines are parallel. This is simplified by returning a default point.
                return default(Point);
            }
            else
            {
                double x = (b2 * c1 - b1 * c2) / determinant;
                double y = (a1 * c2 - a2 * c1) / determinant;
                return new Point(x, y);
            }  
        }



    public void Render()
        {
            using DrawingContext dc = DrawVisualImage.RenderOpen();
            Brush brush = Brushes.Red;
            Pen pen = new Pen(brush, 1 / ZoomboxSub.ContentMatrix.M11);

            dc.DrawEllipse(Brushes.Transparent, pen, RMouseDownP, 130, 130);
            dc.DrawEllipse(Brushes.Transparent, pen, RMouseDownP, 160, 160);
            dc.DrawEllipse(Brushes.Transparent, pen, RMouseDownP, 190, 190);
            dc.DrawEllipse(Brushes.Transparent, pen, RMouseDownP, 250, 250);

            Point ActL = RMouseDownP + PointLen;

            double angle = CalculateAngle(RMouseDownP, ActL);
            Point CenterPoint = RMouseDownP;
            double ActualWidth = Image.ActualWidth;
            double ActualHeight = Image.ActualHeight;

            double centerX = CenterPoint.X;
            double centerY = CenterPoint.Y;
            double halfWidth = ActualWidth / 2;
            double halfHeight = ActualHeight / 2;
            double angleInRadians = angle * Math.PI / 180;

            // 旋转变换
            RotateTransform rotateTransform = new RotateTransform(angle, centerX, centerY);

            // 计算水平线和垂直线的端点（在旋转之前）
            Point horizontalStart = new Point(centerX - halfWidth, centerY);
            Point horizontalEnd = new Point(centerX + halfWidth, centerY);
            Point verticalStart = new Point(centerX, centerY - halfHeight);
            Point verticalEnd = new Point(centerX, centerY + halfHeight);

            // 计算旋转后的端点
            Point rotatedHorizontalStart = rotateTransform.Transform(horizontalStart);
            Point rotatedHorizontalEnd = rotateTransform.Transform(horizontalEnd);
            Point rotatedVerticalStart = rotateTransform.Transform(verticalStart);
            Point rotatedVerticalEnd = rotateTransform.Transform(verticalEnd);

            // 寻找与矩形边界相交的点
            Point[] intersectionPoints = new Point[4];
            intersectionPoints[0] = FindIntersection(rotatedHorizontalStart, rotatedHorizontalEnd, new Point(centerX - halfWidth, centerY - halfHeight), new Point(centerX - halfWidth, centerY + halfHeight)); // Left
            intersectionPoints[1] = FindIntersection(rotatedHorizontalStart, rotatedHorizontalEnd, new Point(centerX + halfWidth, centerY - halfHeight), new Point(centerX + halfWidth, centerY + halfHeight)) ; // Right
            intersectionPoints[2] = FindIntersection(rotatedVerticalStart, rotatedVerticalEnd, new Point(centerX - halfWidth, centerY - halfHeight), new Point(centerX + halfWidth, centerY - halfHeight)) ; // Top
            intersectionPoints[3] = FindIntersection(rotatedVerticalStart, rotatedVerticalEnd, new Point(centerX - halfWidth, centerY + halfHeight), new Point(centerX + halfWidth, centerY + halfHeight)); // Bottom

            // 绘制旋转后的十字线
            dc.DrawLine(pen, intersectionPoints[0], intersectionPoints[1]); // 水平线
            dc.DrawLine(pen, intersectionPoints[2], intersectionPoints[3]); // 垂直线


            TextAttribute textAttribute = new TextAttribute();
            textAttribute.FontSize = 15 / ZoomboxSub.ContentMatrix.M11;

            FormattedText formattedText = new FormattedText(angle.ToString("F1") + "°", CultureInfo.CurrentCulture, textAttribute.FlowDirection, new Typeface(textAttribute.FontFamily, textAttribute.FontStyle, textAttribute.FontWeight, textAttribute.FontStretch), textAttribute.FontSize, textAttribute.Brush, VisualTreeHelper.GetDpi(DrawVisualImage).PixelsPerDip);
            dc.DrawText(formattedText, RMouseDownP + new Vector(20, 20));
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
