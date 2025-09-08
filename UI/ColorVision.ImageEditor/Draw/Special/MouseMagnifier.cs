using System.Windows;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows.Input;
using ColorVision.Common.Utilities;
using ColorVision.ImageEditor.Draw;

namespace ColorVision.Util.Draw.Special
{
    public class ImageInfo
    {
        public Point ActPoint {get;set;}
        public Point BitmapPoint { get; set; }

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
    public enum MagnigifierType
    {
        Circle,
        Rect
    }

    public delegate void MouseMoveColorHandler(object sender, ImageInfo imageInfo);

    /// <summary>
    /// 后续优化调整，成为不同的图像格式显示不同的参数
    /// </summary>
    public class MouseMagnifier
    {
        private ZoomboxSub ZoomboxSub { get; set; }
        private DrawCanvas Image { get; set; }

        public DrawingVisual DrawVisualImage { get; set; }

        public event MouseMoveColorHandler MouseMoveColorHandler;
        public MouseMagnifier(ZoomboxSub zombox, DrawCanvas drawCanvas)
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

        public double Radius { get; set; } = 100;
        public double RectWidth { get; set; } = 120;
        public double RectHeight { get; set; } = 120;

        public MagnigifierType MagnigifierType { get; set; } = MagnigifierType.Circle;

        public void DrawImage(ImageInfo imageInfo,string text1,string text2 )
        {
            Point actPoint = imageInfo.ActPoint;
            Point disPoint = imageInfo.BitmapPoint;

            if (Image.Source is BitmapSource bitmapImage)
            {
                using DrawingContext dc = DrawVisualImage.RenderOpen();

                if (MagnigifierType ==MagnigifierType.Circle)
                {
                    dc.DrawEllipse(Brushes.Transparent, new Pen(Brushes.Black, 2 / ZoomboxSub.ContentMatrix.M11), new Point(actPoint.X, actPoint.Y), Radius, Radius);
                    dc.DrawEllipse(Brushes.Transparent, new Pen(Brushes.White, 1 / ZoomboxSub.ContentMatrix.M11), new Point(actPoint.X, actPoint.Y), Radius, Radius);
                }else if (MagnigifierType == MagnigifierType.Rect)
                {
                    dc.DrawRectangle(Brushes.Transparent, new Pen(Brushes.Black, 2 / ZoomboxSub.ContentMatrix.M11), new Rect(actPoint.X - Radius / 2, actPoint.Y - Radius / 2, Radius, Radius));
                    dc.DrawRectangle(Brushes.Transparent, new Pen(Brushes.White, 1 / ZoomboxSub.ContentMatrix.M11),new Rect(actPoint.X - Radius/2, actPoint.Y -Radius/2, Radius,Radius));
                }

                    var transform = new MatrixTransform(1 / ZoomboxSub.ContentMatrix.M11, ZoomboxSub.ContentMatrix.M12, ZoomboxSub.ContentMatrix.M21, 1 / ZoomboxSub.ContentMatrix.M22, (1 - 1 / ZoomboxSub.ContentMatrix.M11) * actPoint.X, (1 - 1 / ZoomboxSub.ContentMatrix.M22) * actPoint.Y);
                dc.PushTransform(transform);

                double x1 = actPoint.X;
                double y1 = actPoint.Y + 25;

                double width = 120;
                double height = 0;

                x1++;
                y1++;
                width -= 2;
                height -= 2;

                dc.DrawRectangle(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#AA000000")), new Pen(Brushes.White, 0), new Rect(x1 - 1, y1 + height + 1, width + 2, 70));

                Brush brush = Brushes.White;
                FontFamily fontFamily = new("Arial");
                double fontSize = 10;
                FormattedText formattedText = new($"R:{imageInfo.R}  G:{imageInfo.G}  B:{imageInfo.B}", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(fontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), fontSize, brush, VisualTreeHelper.GetDpi(DrawVisualImage).PixelsPerDip);
                dc.DrawText(formattedText, new Point(x1 + 5, y1 + height + 5));
                FormattedText formattedTex1 = new($"({imageInfo.X},{imageInfo.Y})", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(fontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), fontSize, brush, VisualTreeHelper.GetDpi(DrawVisualImage).PixelsPerDip);
                dc.DrawText(formattedTex1, new Point(x1 + 5, y1 + height + 31));

                FormattedText formattedTex3 = new($"{imageInfo.Hex}", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(fontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), fontSize, brush, VisualTreeHelper.GetDpi(DrawVisualImage).PixelsPerDip);
                dc.DrawText(formattedTex3, new Point(x1 + 5, y1 + height + 18));

                FormattedText formattedTex4 = new(text1, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(fontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), fontSize, brush, VisualTreeHelper.GetDpi(DrawVisualImage).PixelsPerDip);
                dc.DrawText(formattedTex4, new Point(x1 + 5, y1 + height + 44));

                FormattedText formattedTex5 = new(text2, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(fontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), fontSize, brush, VisualTreeHelper.GetDpi(DrawVisualImage).PixelsPerDip);
                dc.DrawText(formattedTex5, new Point(x1 + 5, y1 + height + 57));

                dc.Pop();
                if (DrawVisualImage.Effect is not DropShadowEffect)
                    DrawVisualImage.Effect = new DropShadowEffect() { Opacity = 0.5 };

            }


        }


        public void DrawImage(ImageInfo imageInfo)
        {
            Point actPoint = imageInfo.ActPoint;
            Point disPoint =imageInfo.BitmapPoint;



            if (Image.Source is BitmapSource bitmapImage && disPoint.X > 60 && disPoint.X < bitmapImage.PixelWidth - 60 && disPoint.Y > 45 && disPoint.Y < bitmapImage.PixelHeight - 45)
            {

                CroppedBitmap croppedBitmap = new(bitmapImage, new Int32Rect(disPoint.X.ToInt32() - 60, disPoint.Y.ToInt32() - 45, 120, 90));

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
                FontFamily fontFamily = new("Arial");
                double fontSize = 10;
                FormattedText formattedText = new($"R:{imageInfo.R}  G:{imageInfo.G}  B:{imageInfo.B}", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(fontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), fontSize, brush, VisualTreeHelper.GetDpi(DrawVisualImage).PixelsPerDip);
                dc.DrawText(formattedText, new Point(x1 + 5, y1 + height + 5));
                FormattedText formattedTex1 = new($"({imageInfo.X},{imageInfo.Y})", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(fontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), fontSize, brush, VisualTreeHelper.GetDpi(DrawVisualImage).PixelsPerDip);
                dc.DrawText(formattedTex1, new Point(x1 + 5, y1 + height + 31));

                FormattedText formattedTex3 = new($"{imageInfo.Hex}", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(fontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), fontSize, brush, VisualTreeHelper.GetDpi(DrawVisualImage).PixelsPerDip);
                dc.DrawText(formattedTex3, new Point(x1 + 5, y1 + height + 18));
                dc.Pop();
                if (DrawVisualImage.Effect is not DropShadowEffect)
                    DrawVisualImage.Effect = new DropShadowEffect() { Opacity = 0.5 };

            }
        }


        public void MouseMove(object sender, MouseEventArgs e)
        {
            if (IsShow && Image.Source is BitmapSource bitmap)
            {
                var point = e.GetPosition(Image);

                var actPoint = new Point(point.X, point.Y);
                point.X = point.X / Image.ActualWidth * bitmap.PixelWidth;
                point.Y = point.Y / Image.ActualHeight * bitmap.PixelHeight;
                var bitPoint = new Point(point.X.ToInt32(), point.Y.ToInt32());

                if (point.X.ToInt32() >= 0 && point.X.ToInt32() < bitmap.PixelWidth && point.Y.ToInt32() >= 0 && point.Y.ToInt32() < bitmap.PixelHeight)
                {
                    var color = bitmap.GetPixelColor(point.X.ToInt32(), point.Y.ToInt32());
                    ImageInfo imageInfo = new()
                    {
                        ActPoint  = actPoint,
                        BitmapPoint =bitPoint,
                        X = point.X.ToInt32(),
                        Y = point.Y.ToInt32(),
                        R = color.R,
                        G = color.G,
                        B = color.B,
                        Color = new SolidColorBrush(color),
                        Hex = color.ToHex()
                    };
                    DrawImage(imageInfo);
                    MouseMoveColorHandler?.Invoke(this, imageInfo);
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
                    Image.AddVisualCommand(DrawVisualImage);
            }
            else
            {
                if (Image.ContainsVisual(DrawVisualImage))
                    Image.RemoveVisualCommand(DrawVisualImage);
            }
        }
    }
}
