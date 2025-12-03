using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace Pattern.ImageProjector
{
    /// <summary>
    /// FullscreenImageWindow.xaml interaction logic
    /// Displays an image in fullscreen on a specified monitor
    /// </summary>
    public partial class FullscreenImageWindow : Window
    {
        public FullscreenImageWindow(BitmapImage image, Screen targetScreen)
        {
            InitializeComponent();

            // Set the image
            FullscreenImage.Source = image;

            // Position window on the target screen
            var bounds = targetScreen.Bounds;

            // Set window position to the target screen
            this.WindowStartupLocation = WindowStartupLocation.Manual;
            this.Left = bounds.Left;
            this.Top = bounds.Top;
            this.Width = bounds.Width;
            this.Height = bounds.Height;

            // Set hint text
            HintText.Text = Properties.Resources.PressEscToClose;

            // Hide hint after 3 seconds
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = System.TimeSpan.FromSeconds(3)
            };
            timer.Tick += (s, e) =>
            {
                HintText.Visibility = Visibility.Collapsed;
                timer.Stop();
            };
            timer.Start();
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                this.Close();
            }
        }
    }
}
