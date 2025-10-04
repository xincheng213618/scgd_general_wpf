using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows.Input;
using System.Globalization;
using System.Linq;
using ColorVision.Common.Utilities;

namespace ColorVision.ImageEditor.Draw.Special
{
    public class Gridline
    {
        private Zoombox ZoomboxSub { get; set; }
        private DrawCanvas DrawCanvas { get; set; }

        public DrawingVisual DrawVisualImage { get; set; }
        public Gridline(Zoombox zombox, DrawCanvas drawCanvas)
        {
            ZoomboxSub = zombox;
            DrawCanvas = drawCanvas;
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
                    DrawCanvas.MouseMove += MouseMove;
                    DrawCanvas.MouseEnter += MouseEnter;
                    DrawCanvas.MouseLeave += MouseLeave;
                    ZoomboxSub.LayoutUpdated += ZoomboxSub_LayoutUpdated;
                    DefalutTextAttribute.Defalut.PropertyChanged += Defalut_PropertyChanged;
                }
                else
                {
                    DrawCanvas.MouseMove -= MouseMove;
                    DrawCanvas.MouseEnter -= MouseEnter;
                    DrawCanvas.MouseLeave -= MouseLeave;
                    ZoomboxSub.LayoutUpdated -= ZoomboxSub_LayoutUpdated;
                    DefalutTextAttribute.Defalut.PropertyChanged -= Defalut_PropertyChanged;

                }
            }
        }

        private void Defalut_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            Radio = ZoomboxSub.ContentMatrix.M11;
            DrawImage();
        }

        private void ZoomboxSub_LayoutUpdated(object? sender, System.EventArgs e)
        {
            DebounceTimer.AddOrResetTimerDispatcher("GridlineLayoutUpdated", 50, LayoutUpdated);
        }
        Matrix MatrixBackup;
        public void LayoutUpdated()
        {
            var currentMatrix = ZoomboxSub.ContentMatrix;

            if (MatrixBackup != currentMatrix)
            {
                MatrixBackup = new Matrix(currentMatrix.M11, currentMatrix.M12, currentMatrix.M21, currentMatrix.M22, currentMatrix.OffsetX, currentMatrix.OffsetY);
                Radio = currentMatrix.M11;
                DrawImage();
            }
        }

        public static double ActualLength { get => DefalutTextAttribute.Defalut.IsUsePhysicalUnit ? DefalutTextAttribute.Defalut.ActualLength : 1; set { DefalutTextAttribute.Defalut.ActualLength = value; } }
        public static string PhysicalUnit { get => DefalutTextAttribute.Defalut.IsUsePhysicalUnit ? DefalutTextAttribute.Defalut.PhysicalUnit : "Px"; set { DefalutTextAttribute.Defalut.PhysicalUnit = value; } }

        private bool _IsShow;

        double Radio;
        public void DrawImage()
        {
            if (DrawCanvas.Source is BitmapSource bitmapSource)
            {

                Brush brush = Brushes.Red;
                FontFamily fontFamily = new("Arial");
                double ratio = 1 / ZoomboxSub.ContentMatrix.M11;
                Pen pen = new(brush, ratio);

                double lenindex = 40 * ratio;
                if (lenindex > 1) lenindex = (int)lenindex;
                double fontSize = 15 / ZoomboxSub.ContentMatrix.M11; 
                using DrawingContext dc = DrawVisualImage.RenderOpen();

                double OffsetX = ZoomboxSub.ContentMatrix.OffsetX;
                double OffsetY = ZoomboxSub.ContentMatrix.OffsetY;

                double visibleX =  -OffsetX / Radio;
                double visibleY = - OffsetY / Radio;
                double visibleWidth = ZoomboxSub.ActualWidth / Radio;
                double visibleHeight = ZoomboxSub.ActualHeight / Radio;
                visibleWidth = visibleWidth + visibleX > bitmapSource.Width ? bitmapSource.Width : visibleWidth + visibleX;
                visibleHeight = visibleHeight + visibleY > bitmapSource.Height ? bitmapSource.Height : visibleHeight + visibleY;
                visibleX = visibleX < 0 ? 0 : visibleX;
                visibleY = visibleY < 0 ? 0 : visibleY;
   


                Rect visibleRect = new Rect(visibleX, visibleY, visibleWidth, visibleHeight);

                for (double i = visibleY; i < visibleHeight; i += lenindex)
                {
                    string text = (i * ActualLength).ToString("F0") ;
                    FormattedText formattedText = new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(fontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), fontSize, brush, VisualTreeHelper.GetDpi(DrawVisualImage).PixelsPerDip);
                    if (DrawCanvas.RenderTransform is TransformGroup transformGroup && transformGroup.Children.OfType<ScaleTransform>().FirstOrDefault() is ScaleTransform scaleTransform)
                    {
                        dc.PushTransform(scaleTransform);
                        dc.DrawText(formattedText, new Point(-40 / ZoomboxSub.ContentMatrix.M11, i - 10 / ZoomboxSub.ContentMatrix.M11));
                        dc.Pop();
                    }
                    else
                    {
                        dc.DrawText(formattedText, new Point(-40 / ZoomboxSub.ContentMatrix.M11, i - 10 / ZoomboxSub.ContentMatrix.M11));
                    }
                    dc.DrawLine(pen, new Point(0, i), new Point(bitmapSource.Width, i));
                }

                for (double i = visibleX; i < visibleWidth; i += lenindex)
                {
                    string text = (i * ActualLength).ToString("F0");
                    FormattedText formattedText = new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(fontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), fontSize, brush, VisualTreeHelper.GetDpi(DrawVisualImage).PixelsPerDip);

                    if (DrawCanvas.RenderTransform is TransformGroup transformGroup && transformGroup.Children.OfType<ScaleTransform>().FirstOrDefault() is ScaleTransform scaleTransform)
                    {
                        dc.PushTransform(scaleTransform);
                        dc.DrawText(formattedText, new Point(i -10 / ZoomboxSub.ContentMatrix.M11,- 20 / ZoomboxSub.ContentMatrix.M11));
                        dc.Pop();
                    }
                    else
                    {
                        dc.DrawText(formattedText, new Point(i -10 / ZoomboxSub.ContentMatrix.M11,  -20 / ZoomboxSub.ContentMatrix.M11));
                    }
                    dc.DrawLine(pen, new Point(i, 0), new Point(i, bitmapSource.Height));
                }
            }
        }

        public double Ratio { get; set;}

        public void MouseMove(object sender, MouseEventArgs e)
        {

        }

        public void MouseEnter(object sender, MouseEventArgs e) => DrawVisualImageControl(true);

        public void MouseLeave(object sender, MouseEventArgs e) => DrawVisualImageControl(true);

        public void DrawVisualImageControl(bool Control)
        {
            if (Control)
            {
                if (!DrawCanvas.ContainsVisual(DrawVisualImage))
                    DrawCanvas.AddVisualCommand(DrawVisualImage);
            }
            else
            {
                if (DrawCanvas.ContainsVisual(DrawVisualImage))
                    DrawCanvas.RemoveVisualCommand(DrawVisualImage);
            }
        }
    }
}
