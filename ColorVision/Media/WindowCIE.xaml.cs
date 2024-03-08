using ColorVision.Draw.Ruler;
using ColorVision.Draw;
using System.Windows;
using System.Threading.Tasks;

namespace ColorVision.Media
{
    /// <summary>
    /// WindowCIE.xaml 的交互逻辑
    /// </summary>
    public partial class WindowCIE : Window
    {
        public WindowCIE()
        {
            InitializeComponent();
        }
        public ToolBarTop ToolBarTop { get; set; }

        private void Window_Initialized(object sender, System.EventArgs e)
        {
            ToolBarTop = new ToolBarTop(this, Zoombox1, ImageShow);
            ToolBar1.DataContext = ToolBarTop;
            ToolBarTop.ToolBarScaleRuler.ScalRuler.ScaleLocation = ScaleLocation.lowerright;
            ToolBarTop.CrosshairFunction = true;
            Task.Run(() => {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Zoombox1.ZoomUniform();
                });
            });
        }

        private void ImageShow_Initialized(object sender, System.EventArgs e)
        {

        }

        private void ImageShow_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {

        }

        private void ImageShow_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {

        }

        private void ImageShow_PreviewMouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {

        }

        private void ImageShow_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {

        }

        private void ImageShow_MouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {

        }

        private void ImageShow_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {

        }

        private void ImageShow_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {

        }

        private void Button7_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
