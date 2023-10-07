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
                ScalRuler.ParentWidth = GridEx.ActualWidth;
                ScalRuler.ParentHeight = GridEx.ActualHeight;
                grid.Children.Add(ScalRuler);
                drawCanvas.ImageInitialized += (s, e) => Render();
                GridEx.SizeChanged += GridEx_SizeChanged;
                drawCanvas.MouseWheel += DrawCanvas_MouseWheel;
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
                double X = 1 / Zoombox1.ContentMatrix.M11 * bitmapSource.PixelWidth / 100;
                ScalRuler.Render(X);
            }
        }

        public DrawingVisualScaleHost ScalRuler { get; set; }


        private bool _IsShow = true;
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
