using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.Draw.Ruler
{
    public class ToolBarScaleRuler: IEditorToggleToolBase
    {
        public override ToolBarLocal ToolBarLocal => ToolBarLocal.ScaleRuler;

        private FrameworkElement Parent { get; }
        private Grid GridEx { get; set; }

        private DrawEditorContext DrawContext { get; }

        public ToolBarScaleRuler(DrawEditorContext drawContext, ImageViewConfig config)
        {
            DrawContext = drawContext;
            Parent = drawContext.Zoombox;

            ScalRuler = new DrawingVisualScaleHost(config);
            if (drawContext.Zoombox.Parent is Grid grid)
            {
                GridEx = grid;
                ScalRuler.PreviewMouseDown += (s, e) =>
                {
                    EditScaleRuler editScaleRuler = new EditScaleRuler(ScalRuler) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
                    editScaleRuler.ShowDialog();
                    Render();
                };
            }
            IsChecked = false;
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
            if (DrawContext.DrawCanvas.Source is BitmapSource bitmapSource)
            {
                ScalRuler.ParentWidth = GridEx.ActualWidth;
                ScalRuler.ParentHeight = GridEx.ActualHeight;
                ///未知原因
                double X = 1 / DrawContext.ZoomRatio * bitmapSource.PixelWidth / 100 ;

                ScalRuler.Render(X);
            }
        }

        public DrawingVisualScaleHost ScalRuler { get; set; }


        private bool _IsChecked;
        public override bool IsChecked
        {
            get => _IsChecked;
            set
            {
                if (_IsChecked == value) return;
                _IsChecked = value;
                if (value)
                {
                    Load();
                }
                else
                {
                    UnLoad();
                }

            }
        }



        public void Load()
        {
            GridEx.Children.Add(ScalRuler);
            ScalRuler.ParentWidth = GridEx.ActualWidth;
            ScalRuler.ParentHeight = GridEx.ActualHeight;
            DrawContext.DrawCanvas.MouseWheel += DrawCanvas_MouseWheel;
            GridEx.SizeChanged += GridEx_SizeChanged;

            if (Window.GetWindow(Parent) is Window window)
            {
                window.SizeChanged -= GridEx_SizeChanged;
                window.SizeChanged += GridEx_SizeChanged;
            }

            Render();
        }
        public void UnLoad()
        {
            GridEx.Children.Remove(ScalRuler);
            DrawContext.DrawCanvas.MouseWheel -= DrawCanvas_MouseWheel;
            GridEx.SizeChanged -= GridEx_SizeChanged;

            if (Window.GetWindow(Parent) is Window window)
            {
                Window.GetWindow(Parent).SizeChanged -= GridEx_SizeChanged;
            }
        }


    }
}
