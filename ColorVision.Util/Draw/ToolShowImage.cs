using ColorVision.Extension;
using System.Windows;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows.Input;
using ColorVision.Common.Extension;
using Microsoft.VisualBasic.Devices;

namespace ColorVision.Draw
{
    public class ToolShowImage
    {
        private ZoomboxSub ZoomboxSub { get; set; }
        private DrawCanvas Image { get; set; }

        public DrawingVisual DrawVisualImage { get; set; }

        public DrawingVisual DrawingVisualImage1 { get; set; }

        public ToolShowImage(ZoomboxSub zombox, DrawCanvas drawCanvas)
        {
            ZoomboxSub = zombox;
            Image = drawCanvas;
            DrawVisualImage = new DrawingVisual();
            DrawingVisualImage1 = new DrawingVisual();
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
            if (Image.Source is BitmapSource bitmapImage && disPoint.X > 60 && disPoint.X < bitmapImage.PixelWidth - 60 && disPoint.Y > 45 && disPoint.Y < bitmapImage.PixelHeight - 45)
            {

                CroppedBitmap croppedBitmap = new CroppedBitmap(bitmapImage, new Int32Rect(disPoint.X.ToInt32() - 60, disPoint.Y.ToInt32() - 45, 120, 90));

                using DrawingContext dc = DrawVisualImage.RenderOpen();

                double mouseX = actPoint.X; // 示例坐标
                double mouseY = actPoint.Y; // 示例坐标
                double length = 1 / ZoomboxSub.ContentMatrix.M11;
                double radius = 5 / ZoomboxSub.ContentMatrix.M11; // 直径为10，半径为5
                // 绘制空心圆
                Pen circlePen = new Pen(Brushes.Black, length); // 黑色笔刷，线宽为1
                dc.DrawEllipse(null, new Pen(Brushes.White, length*1.5), actPoint, radius, radius);
                dc.DrawEllipse(null, circlePen, actPoint, radius, radius);

                // 绘制虚线的笔刷
                Pen dashedPen = new Pen(Brushes.Black, length)
                {
                    DashStyle = new DashStyle(new double[] { 2, 2 }, 0) // 虚线样式
                };
                Pen dashedPen1 = new Pen(Brushes.White, length * 1.5)
                {
                    DashStyle = new DashStyle(new double[] { 2, 2 }, 0) // 虚线样式
                };

                // 绘制X轴虚线
                dc.DrawLine(dashedPen1, new Point(0, mouseY), new Point(mouseX - radius, mouseY)); // 左边
                dc.DrawLine(dashedPen1, new Point(mouseX + radius, mouseY), new Point(bitmapImage.PixelWidth, mouseY));


                dc.DrawLine(dashedPen, new Point(0, mouseY), new Point(mouseX - radius, mouseY)); // 左边
                dc.DrawLine(dashedPen, new Point(mouseX + radius, mouseY), new Point(bitmapImage.PixelWidth, mouseY));

                // 绘制Y轴虚线
                dc.DrawLine(dashedPen1, new Point(mouseX, 0), new Point(mouseX, mouseY - radius)); // 上边
                dc.DrawLine(dashedPen1, new Point(mouseX, mouseY + radius), new Point(mouseX, bitmapImage.PixelHeight));

                dc.DrawLine(dashedPen, new Point(mouseX, 0), new Point(mouseX, mouseY - radius)); // 上边
                dc.DrawLine(dashedPen, new Point(mouseX, mouseY + radius), new Point(mouseX, bitmapImage.PixelHeight)); 


                //var transform = new MatrixTransform(1 / ZoomboxSub.ContentMatrix.M11, ZoomboxSub.ContentMatrix.M12, ZoomboxSub.ContentMatrix.M21, 1 / ZoomboxSub.ContentMatrix.M22, (1 - 1 / ZoomboxSub.ContentMatrix.M11) * actPoint.X, (1 - 1 / ZoomboxSub.ContentMatrix.M22) * actPoint.Y);
                //dc.PushTransform(transform);

                //dc.DrawImage(croppedBitmap, new Rect(new Point(actPoint.X, actPoint.Y + 25), new Size(120, 90)));

                //dc.DrawLine(new Pen(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00B1FF")), 3), new Point(actPoint.X + 59, actPoint.Y + 25), new Point(actPoint.X + 59, actPoint.Y + 25 + 90));
                //dc.DrawLine(new Pen(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00B1FF")), 3), new Point(actPoint.X, actPoint.Y + 25 + 44), new Point(actPoint.X + 120, actPoint.Y + 25 + 44));


                //double x1 = actPoint.X;
                //double y1 = actPoint.Y + 25;

                //double width = 120;
                //double height = 90;


                //dc.DrawLine(new Pen(Brushes.Black, 0.5), new Point(x1, y1 - 0.25), new Point(x1, y1 + height + 0.25));
                //dc.DrawLine(new Pen(Brushes.Black, 0.5), new Point(x1, y1), new Point(x1 + width, y1));
                //dc.DrawLine(new Pen(Brushes.Black, 0.5), new Point(x1 + width, y1 - 0.25), new Point(x1 + width, y1 + height + 0.25));
                //dc.DrawLine(new Pen(Brushes.Black, 0.5), new Point(x1, y1 + height), new Point(x1 + width, y1 + height));

                //x1++;
                //y1++;
                //width -= 2;
                //height -= 2;
                //dc.DrawLine(new Pen(Brushes.White, 1.5), new Point(x1, y1 - 0.75), new Point(x1, y1 + height + 0.75));
                //dc.DrawLine(new Pen(Brushes.White, 1.5), new Point(x1, y1), new Point(x1 + width, y1));
                //dc.DrawLine(new Pen(Brushes.White, 1.5), new Point(x1 + width, y1 - 0.75), new Point(x1 + width, y1 + height + 0.75));
                //dc.DrawLine(new Pen(Brushes.White, 1.5), new Point(x1, y1 + height), new Point(x1 + width, y1 + height));

                //dc.DrawRectangle(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#AA000000")), new Pen(Brushes.White, 0), new Rect(x1 - 1, y1 + height + 1, width + 2, 45));

                //Brush brush = Brushes.White;
                //FontFamily fontFamily = new FontFamily("Arial");
                //double fontSize = 10;
                //FormattedText formattedText = new FormattedText($"R:{imageInfo.R}  G:{imageInfo.G}  B:{imageInfo.B}", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(fontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), fontSize, brush, VisualTreeHelper.GetDpi(DrawVisualImage).PixelsPerDip);
                //dc.DrawText(formattedText, new Point(x1 + 5, y1 + height + 5));
                //FormattedText formattedTex1 = new FormattedText($"({imageInfo.X},{imageInfo.Y})", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(fontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), fontSize, brush, VisualTreeHelper.GetDpi(DrawVisualImage).PixelsPerDip);
                //dc.DrawText(formattedTex1, new Point(x1 + 5, y1 + height + 31));

                //FormattedText formattedTex3 = new FormattedText($"{imageInfo.Hex}", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(fontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), fontSize, brush, VisualTreeHelper.GetDpi(DrawVisualImage).PixelsPerDip);
                //dc.DrawText(formattedTex3, new Point(x1 + 5, y1 + height + 18));
                //dc.Pop();
                //if (DrawVisualImage.Effect is not DropShadowEffect)
                //    DrawVisualImage.Effect = new DropShadowEffect() { Opacity = 0.5 };

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
