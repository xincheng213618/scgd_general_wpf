using ColorVision.Extension;
using ColorVision.MVVM;
using System;
using System.Windows;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows.Input;
using ColorVision.Draw;
using static ColorVision.ImageView;
using System.Windows.Controls;

namespace ColorVision
{
    public class ToolBarTop : ViewModelBase
    {
        public RelayCommand ZoomUniformToFill { get; set; }
        public RelayCommand ZoomUniform { get; set; }
        public RelayCommand ZoomIncrease { get; set; }
        public RelayCommand ZoomDecrease { get; set; }
        public RelayCommand ZoomNone { get; set; }

        public RelayCommand OpenProperty { get; set; }

        private ZoomboxSub ZoomboxSub { get; set; }
        private DrawCanvas DrawImageCanvas { get; set; }
        public DrawingVisual DrawVisualImage { get; set; }

        public ToolBarTop(FrameworkElement Parent,ZoomboxSub zombox, DrawCanvas drawCanvas)
        {
            ZoomboxSub = zombox ?? throw new ArgumentNullException(nameof(zombox));
            DrawImageCanvas = drawCanvas ?? throw new ArgumentNullException(nameof(drawCanvas));
            ZoomUniformToFill = new RelayCommand(a => ZoomboxSub.ZoomUniformToFill());
            ZoomUniform = new RelayCommand(a => ZoomboxSub.ZoomUniform());
            ZoomIncrease = new RelayCommand(a => ZoomboxSub.Zoom(1.25));
            ZoomDecrease = new RelayCommand(a => ZoomboxSub.Zoom(0.8));
            ZoomNone = new RelayCommand(a =>
            {
                ZoomboxSub.ZoomNone();
            });

            DrawVisualImage = new DrawingVisual();
            OpenProperty = new RelayCommand(a => new DrawProperties().Show());
            Parent.PreviewKeyDown += PreviewKeyDown;
            drawCanvas.MouseMove += Image_MouseMove;
            drawCanvas.MouseEnter += DrawCanvas_MouseEnter;
            drawCanvas.MouseLeave += DrawCanvas_MouseLeave;
            zombox.Cursor = Cursors.Hand;
        }

        private void DrawCanvas_MouseLeave(object sender, MouseEventArgs e)
        {
            DrawVisualImageControl(false);
        }

        private void DrawCanvas_MouseEnter(object sender, MouseEventArgs e)
        {
            DrawVisualImageControl(ShowImageInfo);
        }

        private void PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Left)
            {
                TranslateTransform translateTransform = new TranslateTransform();
                Vector vector = new Vector(-10, 0);
                translateTransform.SetCurrentValue(System.Windows.Media.TranslateTransform.XProperty, vector.X);
                translateTransform.SetCurrentValue(System.Windows.Media.TranslateTransform.YProperty, vector.Y);
                ZoomboxSub.SetCurrentValue(ZoomboxSub.ContentMatrixProperty, Matrix.Multiply(ZoomboxSub.ContentMatrix, translateTransform.Value));
            }
            else if (e.Key == Key.Right)
            {
                TranslateTransform translateTransform = new TranslateTransform();
                Vector vector = new Vector(10, 0);
                translateTransform.SetCurrentValue(System.Windows.Media.TranslateTransform.XProperty, vector.X);
                translateTransform.SetCurrentValue(System.Windows.Media.TranslateTransform.YProperty, vector.Y);
                ZoomboxSub.SetCurrentValue(ZoomboxSub.ContentMatrixProperty, Matrix.Multiply(ZoomboxSub.ContentMatrix, translateTransform.Value));
            }
            else if (e.Key == Key.Up)
            {
                TranslateTransform translateTransform = new TranslateTransform();
                Vector vector = new Vector(0, -10);
                translateTransform.SetCurrentValue(System.Windows.Media.TranslateTransform.XProperty, vector.X);
                translateTransform.SetCurrentValue(System.Windows.Media.TranslateTransform.YProperty, vector.Y);
                ZoomboxSub.SetCurrentValue(ZoomboxSub.ContentMatrixProperty, Matrix.Multiply(ZoomboxSub.ContentMatrix, translateTransform.Value));
            }
            else if (e.Key == Key.Down)
            {
                TranslateTransform translateTransform = new TranslateTransform();
                Vector vector = new Vector(0, 10);
                translateTransform.SetCurrentValue(System.Windows.Media.TranslateTransform.XProperty, vector.X);
                translateTransform.SetCurrentValue(System.Windows.Media.TranslateTransform.YProperty, vector.Y);
                ZoomboxSub.SetCurrentValue(ZoomboxSub.ContentMatrixProperty, Matrix.Multiply(ZoomboxSub.ContentMatrix, translateTransform.Value));
            }
            else if (e.Key == Key.Add)
            {
                ZoomboxSub.Zoom(1.1);
            }
            else if (e.Key == Key.Subtract)
            {
                ZoomboxSub.Zoom(0.9);
            }
        }


        private void Image_MouseMove(object sender, MouseEventArgs e)
        {
            if (ShowImageInfo &&sender is DrawCanvas drawCanvas && drawCanvas.Source is BitmapSource bitmap)
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



        /// <summary>
        /// 当前的缩放分辨率
        /// </summary>
        public double ZoomRatio
        {
            get => ZoomboxSub.ContentMatrix.M11;
            set => ZoomboxSub.Zoom(value);
        }

        private bool _ShowImageInfo;
        public bool ShowImageInfo
        {
            get => _ShowImageInfo; set
            {
                if (_ShowImageInfo == value) return;
                if (value) Activate = false;
                _ShowImageInfo = value;
                DrawVisualImageControl(_ShowImageInfo);
                NotifyPropertyChanged();
            }
        }

        public void DrawVisualImageControl(bool Control)
        {
            if (Control)
            {
                if (!DrawImageCanvas.ContainsVisual(DrawVisualImage))
                    DrawImageCanvas.AddVisual(DrawVisualImage);
            }
            else
            {
                if (DrawImageCanvas.ContainsVisual(DrawVisualImage))
                    DrawImageCanvas.RemoveVisual(DrawVisualImage);
            }
        }


        private bool _Activate;

        public bool Activate
        {
            get => _Activate;
            set
            {
                if (_Activate == value) return;
                if (value) ShowImageInfo = false;
                _Activate = value;
                if (_Activate)
                {
                    ZoomboxSub.ActivateOn = ModifierKeys.Control;
                    ZoomboxSub.Cursor = Cursors.Cross;
                }
                else
                {
                    ZoomboxSub.ActivateOn = ModifierKeys.None;
                    ZoomboxSub.Cursor = Cursors.Hand;
                }
                NotifyPropertyChanged();
            }
        }




        public void DrawImage(Point actPoint, Point disPoint, ImageInfo imageInfo)
        {
            if (DrawImageCanvas.Source is BitmapImage bitmapImage && disPoint.X > 60 && disPoint.X < bitmapImage.PixelWidth - 60 && disPoint.Y > 45 && disPoint.Y < bitmapImage.PixelHeight - 45)
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


        private bool _DrawCircle;
        /// <summary>
        /// 是否画圆形
        /// </summary>
        public bool DrawCircle {  get => _DrawCircle;
            set
            {
                if (_DrawCircle == value) return;
                _DrawCircle = value;
                if (value)
                {
                    DrawRect = false;
                    DrawPolygon = false;
                    Activate = true;
                }
                NotifyPropertyChanged(); 
            }
        }

        private bool _DrawRect;
        /// <summary>
        /// 是否画圆形
        /// </summary>
        public bool DrawRect
        {
            get => _DrawRect;
            set
            {
                if (_DrawRect == value) return;
                _DrawRect = value;
                if (value)
                {
                    DrawCircle = false;
                    DrawPolygon = false;
                    Activate = true;
                }
                NotifyPropertyChanged();
            }
        }

        public bool Measure {
            get => _Measure;
            set 
                {
                if (_Measure == value) return;
                _Measure = value;
                if (value)
                {
                    DrawCircle = false;
                    DrawRect = false;
                    DrawPolygon = false;
                    Activate = true;
                }
                NotifyPropertyChanged();
            }
        }
        private bool _Measure;



        private bool _DrawPolygon;

        public bool DrawPolygon
        {
            get => _DrawPolygon;
            set
            {
                if (_DrawPolygon == value) return;
                _DrawPolygon = value;
                if (value)
                {
                    DrawCircle = false;
                    DrawRect = false;
                    Activate = true;
                }
                NotifyPropertyChanged();
            }
        }

        private bool _EraseVisual;
        public bool EraseVisual {  get => _EraseVisual;
            set
            {
                if (_EraseVisual == value) return;
                    _EraseVisual = value;
                if (value)
                {
                    ZoomboxSub.Cursor = Input.Cursors.Eraser;
                }
                else
                {
                    ZoomboxSub.Cursor = Cursors.Arrow;
                }



                NotifyPropertyChanged();
            }
        }




    }
}
