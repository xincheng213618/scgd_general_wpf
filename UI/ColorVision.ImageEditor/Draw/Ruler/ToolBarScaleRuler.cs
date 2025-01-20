using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.Draw.Ruler
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
                ScalRuler.PreviewMouseDown += (s, e) =>
                {
                    EditScaleRuler editScaleRuler = new(ScalRuler) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
                    editScaleRuler.ShowDialog();
                    Render();
                };
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

        public void Render()
        {
            if (drawCanvas.Source is BitmapSource bitmapSource)
            {
                ScalRuler.ParentWidth = GridEx.ActualWidth;
                ScalRuler.ParentHeight = GridEx.ActualHeight;
                ///未知原因
                double X = 1 / Zoombox1.ContentMatrix.M11 * bitmapSource.PixelWidth / 100 ;

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
                    drawCanvas.MouseWheel += DrawCanvas_MouseWheel;
                    GridEx.SizeChanged += GridEx_SizeChanged;

                    if (Window.GetWindow(Parent) is Window window)
                    {
                        window.SizeChanged -= GridEx_SizeChanged;
                        window.SizeChanged += GridEx_SizeChanged;
                    }

                    Render();
                }
                else
                {
                    GridEx.Children.Remove(ScalRuler);
                    drawCanvas.MouseWheel -= DrawCanvas_MouseWheel;
                    GridEx.SizeChanged -= GridEx_SizeChanged;

                    if (Window.GetWindow(Parent) is Window window)
                    {
                        Window.GetWindow(Parent).SizeChanged -= GridEx_SizeChanged;
                    }
                }

            }
        }



    }
}
