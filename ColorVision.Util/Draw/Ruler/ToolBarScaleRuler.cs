using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace ColorVision.Draw.Ruler
{
    public class ToolBarScaleRuler
    {
        private ZoomboxSub Zoombox1 { get; set; }
        private DrawCanvas drawCanvas { get; set; }

        private FrameworkElement Parent { get; set; }
        private Grid GridEx { get; set; }

        public ToolBarScaleRuler(FrameworkElement Parent, ZoomboxSub zombox, DrawCanvas drawCanvas)
        {
            this.Parent = Parent;
            Zoombox1 = zombox;
            this.drawCanvas = drawCanvas;
            ScalRuler = new DrawingVisualScaleHost();
            //ScalRuler.ScaleLocation = ScaleLocation.lowerright;
            if (Zoombox1.Parent is Grid grid)
            {
                GridEx = grid;
            }
        }

        private void GridEx_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ScalRuler.ParentWidth = GridEx.ActualWidth;
            ScalRuler.ParentHeight = GridEx.ActualHeight;
            ScalRuler.Render();
        }

        private void DrawCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            Render();
        }

        private void Render()
        {
            if (drawCanvas.Source is BitmapSource bitmapSource)
            {
                ///未知原因
                double X = 1 / Zoombox1.ContentMatrix.M11 * bitmapSource.PixelWidth / 100 ;

                var controlWidth = drawCanvas.ActualWidth;
                int imageWidth = bitmapSource.PixelWidth;
                X = X / controlWidth * imageWidth;

                ScalRuler.Render(X);
            }
        }

        public DrawingVisualScaleHost ScalRuler { get; set; }


        private bool _IsShow;
        public bool IsShow
        {
            get => _IsShow;
            set
            {
                if (_IsShow == value) return;
                _IsShow = value;
                if (value)
                {
                    GridEx.Children.Add(ScalRuler);
                    ScalRuler.ParentWidth = GridEx.ActualWidth;
                    ScalRuler.ParentHeight = GridEx.ActualHeight;
                    Render();
                    drawCanvas.MouseWheel += DrawCanvas_MouseWheel;
                    GridEx.SizeChanged -= GridEx_SizeChanged;

                }
                else
                {
                    GridEx.Children.Remove(ScalRuler);
                    drawCanvas.MouseWheel -= DrawCanvas_MouseWheel;
                    GridEx.SizeChanged -= GridEx_SizeChanged;
                }

            }
        }



    }
}
