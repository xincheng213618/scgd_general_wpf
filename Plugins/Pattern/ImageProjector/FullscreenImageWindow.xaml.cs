using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace Pattern.ImageProjector
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

        public FullscreenImageWindow(BitmapImage image, System.Windows.Forms.Screen targetScreen)
        {
            InitializeComponent();

            _targetScreen = targetScreen;
            FullscreenImage.Source = image;

            // 初始状态设为 Normal
            this.WindowState = WindowState.Normal;
            this.WindowStartupLocation = WindowStartupLocation.Manual;

            // 在 SourceInitialized 事件中设置位置
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

            // 先移动到目标屏幕
            this.Left = bounds.Left / dpiScale;
            this.Top = bounds.Top / dpiScale;
            this.Width = bounds.Width / dpiScale;
            this.Height = bounds.Height / dpiScale;

            // 如果需要使用系统最大化，可以在这里设置
            // this.WindowState = WindowState. Maximized;
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
    }
}