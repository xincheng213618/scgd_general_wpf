using ColorVision.Common.MVVM;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Forms;

namespace ColorVision.UI
{
    public abstract class WindowConfig : ViewModelBase, IConfig
    {
        public bool IsRestoreWindow { get; set; } = true;
        public double Width { get; set; }
        public double Height { get; set; }
        public double Left { get; set; }
        public double Top { get; set; }
        public int WindowState { get; set; }
        public string ScreenDeviceName { get; set; } // 新增字段，保存显示器标识

        private static double Clamp(double value, double min, double max) => Math.Max(min, Math.Min(max, value));
        public void SetWindow(Window window)
        {
            if (IsRestoreWindow && Width > 0 && Height > 0)
            {
                // 找到对应的屏幕
                var targetScreen = Screen.AllScreens.FirstOrDefault(s => s.DeviceName == ScreenDeviceName)
                                   ?? Screen.PrimaryScreen;
                var area = targetScreen.WorkingArea;

                window.Left = Clamp(Left, area.Left, area.Right - Width);
                window.Top = Clamp(Top, area.Top, area.Bottom - Height);
                window.Width = Clamp(Width, 0, area.Width);
                window.Height = Clamp(Height, 0, area.Height);

                if (Enum.IsDefined(typeof(WindowState), WindowState))
                    window.WindowState = (WindowState)WindowState;
            }
            window.Closing -= Window_Closing;
            window.Closing += Window_Closing;
            window.SizeChanged -= Window_SizeChanged;
            window.SizeChanged += Window_SizeChanged;
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is Window window)
            {
                SetConfig(window);
            }
        }

        private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (sender is Window window)
            {
                SetConfig(window);
            }
        }

        public void SetConfig(Window window)
        {
            Top = window.Top;
            Left = window.Left;
            Height = window.Height;
            Width = window.Width;
            WindowState = (int)window.WindowState;

            var windowRect = new System.Drawing.Rectangle((int)window.Left, (int)window.Top, (int)window.Width, (int)window.Height);
            var screen = Screen.AllScreens.FirstOrDefault(s => s.Bounds.IntersectsWith(windowRect))
                ?? Screen.PrimaryScreen;
            ScreenDeviceName = screen.DeviceName;
        }
    }

}
