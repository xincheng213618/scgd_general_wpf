using ColorVision.Common.MVVM;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;

namespace ColorVision.UI
{
    public abstract class WindowConfig : ViewModelBase, IConfig
    {
        [DisplayName("StartRecoverUILayout")]
        public bool IsRestoreWindow { get; set; } = true;

        public double Width { get; set; }
        public double Height { get; set; }
        public double Left { get; set; }
        public double Top { get; set; }
        public int WindowStates { get; set; }
        public string ScreenDeviceName { get; set; }

        // 入口：在窗口构造后调用
        public void SetWindow(Window window)
        {
            // 在 SourceInitialized 之后恢复（此时有 CompositionTarget，可用于 DPI 转换）
            window.SourceInitialized -= OnSourceInitialized;
            window.SourceInitialized += OnSourceInitialized;

            window.Closing -= Window_Closing;
            window.Closing += Window_Closing;

            window.SizeChanged -= Window_LocationOrSizeChanged;
            window.SizeChanged += Window_LocationOrSizeChanged;

            window.LocationChanged -= Window_LocationOrSizeChanged;
            window.LocationChanged += Window_LocationOrSizeChanged;
        }

        private void OnSourceInitialized(object? sender, EventArgs e)
        {
            if (sender is Window window)
            {
                RestoreWindow(window);
            }
        }

        private void Window_LocationOrSizeChanged(object? sender, EventArgs e)
        {
            if (sender is Window window)
            {
                UpdateFromWindow(window);
            }
        }

        private void Window_Closing(object? sender, CancelEventArgs e)
        {
            if (sender is Window window)
            {
                UpdateFromWindow(window);
            }
        }

        // 从窗口读取并保存配置
        public void SetConfig(Window window) => UpdateFromWindow(window);

        private void UpdateFromWindow(Window window)
        {
            // 在最大化/最小化时用 RestoreBounds 保存“正常状态”的尺寸和位置
            Rect bounds = window.WindowState == WindowState.Normal
                ? new Rect(window.Left, window.Top, window.Width, window.Height)
                : window.RestoreBounds;

            Top = bounds.Top;
            Left = bounds.Left;
            Height = bounds.Height;
            Width = bounds.Width;
            WindowStates = (int)window.WindowState;

            // 找到当前窗口所在的屏幕（基于 DIP）
            var dipScreens = GetDipScreens(window).ToList();
            var currentDipRect = new Rect(window.Left, window.Top, window.Width, window.Height);
            var best = FindBestScreenByIntersect(dipScreens, currentDipRect);

            ScreenDeviceName = best?.Name ?? Screen.PrimaryScreen.DeviceName;
        }

        private void RestoreWindow(Window window)
        {
            if (!IsRestoreWindow) return;

            window.WindowStartupLocation = WindowStartupLocation.Manual;

            // 目标窗口状态（避免恢复为最小化）
            var savedState = Enum.IsDefined(typeof(WindowState), WindowStates) ? (WindowState)WindowStates : WindowState.Normal;
            if (savedState == WindowState.Minimized)
                savedState = WindowState.Normal;

            // 保存的“正常状态”矩形
            Rect savedRect = GetSavedBoundsRect(window);

            // 选屏（优先设备名，其次相交面积，最后主屏）
            var dipScreens = GetDipScreens(window).ToList();
            var target = FindTargetScreen(dipScreens, savedRect, ScreenDeviceName)
                         ?? GetPrimaryDipScreen(window);

            // 如果保存的矩形无效或不相交，则居中到目标屏
            if (IsInvalidRect(savedRect) || !savedRect.IntersectsWith(target.WorkingArea))
            {
                double w = Double.IsNaN(Width) || Width <= 0 ? Math.Max(window.MinWidth, 800) : Width;
                double h = Double.IsNaN(Height) || Height <= 0 ? Math.Max(window.MinHeight, 600) : Height;
                w = Math.Min(w, target.WorkingArea.Width);
                h = Math.Min(h, target.WorkingArea.Height);

                savedRect = new Rect(
                    target.WorkingArea.Left + (target.WorkingArea.Width - w) / 2,
                    target.WorkingArea.Top + (target.WorkingArea.Height - h) / 2,
                    w, h);
            }

            // 适配到屏幕工作区域内（留 16px 缓冲，避免只露出一条边）
            var fitted = FitRectInside(savedRect, target.WorkingArea, margin: 16, minWidth: Math.Max(200, window.MinWidth), minHeight: Math.Max(120, window.MinHeight));

            // 先设置位置和尺寸，再设置状态（Maximized）
            window.Left = fitted.Left;
            window.Top = fitted.Top;
            window.Width = fitted.Width;
            window.Height = fitted.Height;

            window.WindowState = savedState;
        }

        private Rect GetSavedBoundsRect(Window window)
        {
            // 使用保存的 DIP 值，若无则给出一个空矩形
            if (Width > 0 && Height > 0)
            {
                return new Rect(Left, Top, Width, Height);
            }
            return Rect.Empty;
        }

        private static bool IsInvalidRect(Rect rect)
        {
            return rect.IsEmpty || double.IsNaN(rect.Left) || double.IsNaN(rect.Top) || rect.Width <= 0 || rect.Height <= 0;
        }

        private static Rect FitRectInside(Rect rect, Rect bounds, double margin, double minWidth, double minHeight)
        {
            double width = Math.Max(minWidth, Math.Min(rect.Width, Math.Max(0, bounds.Width - 2 * margin)));
            double height = Math.Max(minHeight, Math.Min(rect.Height, Math.Max(0, bounds.Height - 2 * margin)));

            double left = Clamp(rect.Left, bounds.Left + margin, bounds.Right - margin - width);
            double top = Clamp(rect.Top, bounds.Top + margin, bounds.Bottom - margin - height);

            // 若 bounds 太小导致 clamp 上界小于下界，回退到左上角
            if (double.IsNaN(left) || double.IsInfinity(left)) left = bounds.Left + margin;
            if (double.IsNaN(top) || double.IsInfinity(top)) top = bounds.Top + margin;

            return new Rect(left, top, width, height);
        }

        private class DipScreen
        {
            public string Name { get; set; } = "";
            public Rect WorkingArea { get; set; }
            public Rect Bounds { get; set; }
        }

        private static IEnumerable<DipScreen> GetDipScreens(Visual visual)
        {
            // 将 WinForms 的像素矩形转换成 WPF 的 DIP 矩形
            var source = PresentationSource.FromVisual(visual);
            var tf = source?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;

            foreach (var s in Screen.AllScreens)
            {
                var waTL = tf.Transform(new System.Windows.Point(s.WorkingArea.Left, s.WorkingArea.Top));
                var waBR = tf.Transform(new System.Windows.Point(s.WorkingArea.Right, s.WorkingArea.Bottom));
                var bTL = tf.Transform(new System.Windows.Point(s.Bounds.Left, s.Bounds.Top));
                var bBR = tf.Transform(new System.Windows.Point(s.Bounds.Right, s.Bounds.Bottom));

                yield return new DipScreen
                {
                    Name = s.DeviceName,
                    WorkingArea = new Rect(waTL, waBR),
                    Bounds = new Rect(bTL, bBR)
                };
            }
        }

        private static DipScreen? FindTargetScreen(IEnumerable<DipScreen> screens, Rect savedRect, string? savedName)
        {
            var list = screens.ToList();
            // 1) 设备名匹配
            var byName = list.FirstOrDefault(x => !string.IsNullOrEmpty(savedName) && string.Equals(x.Name, savedName, StringComparison.OrdinalIgnoreCase));
            if (byName != null) return byName;

            // 2) 相交面积最大
            if (!IsInvalidRect(savedRect))
            {
                var best = FindBestScreenByIntersect(list, savedRect);
                if (best != null) return best;
            }

            // 3) 主屏
            return null;
        }

        private static DipScreen? FindBestScreenByIntersect(IEnumerable<DipScreen> screens, Rect rect)
        {
            DipScreen? best = null;
            double bestArea = -1;

            foreach (var s in screens)
            {
                var inter = Rect.Intersect(s.Bounds, rect);
                if (!inter.IsEmpty)
                {
                    double area = inter.Width * inter.Height;
                    if (area > bestArea)
                    {
                        bestArea = area;
                        best = s;
                    }
                }
            }
            return best;
        }

        private static DipScreen GetPrimaryDipScreen(Visual visual)
        {
            var source = PresentationSource.FromVisual(visual);
            var tf = source?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;

            var p = Screen.PrimaryScreen;
            var waTL = tf.Transform(new System.Windows.Point(p.WorkingArea.Left, p.WorkingArea.Top));
            var waBR = tf.Transform(new System.Windows.Point(p.WorkingArea.Right, p.WorkingArea.Bottom));
            var bTL = tf.Transform(new System.Windows.Point(p.Bounds.Left, p.Bounds.Top));
            var bBR = tf.Transform(new System.Windows.Point(p.Bounds.Right, p.Bounds.Bottom));

            return new DipScreen
            {
                Name = p.DeviceName,
                WorkingArea = new Rect(waTL, waBR),
                Bounds = new Rect(bTL, bBR)
            };
        }

        private static double Clamp(double value, double min, double max)
        {
            if (double.IsNaN(value)) return min;
            if (min > max) return min;
            return Math.Max(min, Math.Min(max, value));
        }
    }
}