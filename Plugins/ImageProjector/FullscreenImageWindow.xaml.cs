using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ImageProjector
{
    public partial class FullscreenImageWindow : Window
    {
        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

        [DllImport("shcore.dll")]
        private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        private const uint MONITOR_DEFAULTTONEAREST = 2;
        private const int MDT_EFFECTIVE_DPI = 0;

        public FullscreenImageWindow(BitmapImage image, System.Windows.Forms.Screen targetScreen, Stretch stretch = Stretch.Uniform)
        {
            InitializeComponent();

            _targetScreen = targetScreen;
            FullscreenImage.Source = image;
            FullscreenImage.Stretch = stretch;

            // Initial state as Normal
            this.WindowState = WindowState.Normal;
            this.WindowStartupLocation = WindowStartupLocation.Manual;

            // Set position in SourceInitialized event
            this.SourceInitialized += OnSourceInitialized;

            HintText.Text = Properties.Resources.PressEscToClose;

            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            timer.Tick += (s, e) =>
            {
                HintText.Visibility = Visibility.Collapsed;
                timer.Stop();
            };
            timer.Start();
        }

        private readonly Screen _targetScreen;

        private void OnSourceInitialized(object? sender, EventArgs e)
        {
            var dpiScale = GetDpiScaleForScreen(_targetScreen);
            var bounds = _targetScreen.Bounds;

            // Move to target screen
            this.Left = bounds.Left / dpiScale;
            this.Top = bounds.Top / dpiScale;
            this.Width = bounds.Width / dpiScale;
            this.Height = bounds.Height / dpiScale;
        }

        private double GetDpiScaleForScreen(System.Windows.Forms.Screen screen)
        {
            try
            {
                var point = new POINT { X = screen.Bounds.Left + 1, Y = screen.Bounds.Top + 1 };
                var monitor = MonitorFromPoint(point, MONITOR_DEFAULTTONEAREST);
                GetDpiForMonitor(monitor, MDT_EFFECTIVE_DPI, out uint dpiX, out _);
                return dpiX / 96.0;
            }
            catch
            {
                // Fallback to system DPI
                using var graphics = System.Drawing.Graphics.FromHwnd(IntPtr.Zero);
                return graphics.DpiX / 96.0;
            }
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
                this.Close();
        }

        /// <summary>
        /// Updates the displayed image without recreating the window
        /// </summary>
        /// <param name="image">The new image to display</param>
        public void UpdateImage(BitmapImage image)
        {
            FullscreenImage.Source = image;
        }

        /// <summary>
        /// Updates the stretch mode of the displayed image
        /// </summary>
        /// <param name="stretch">The new stretch mode</param>
        public void UpdateStretch(Stretch stretch)
        {
            FullscreenImage.Stretch = stretch;
        }
    }
}
