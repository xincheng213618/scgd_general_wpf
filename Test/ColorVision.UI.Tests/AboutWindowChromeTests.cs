using ColorVision.UI.Views.About;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class AboutWindowChromeTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(0.65)]
    public void NativeRegionMatchesTheVisualRadiusAfterWorkAreaScaling(double expectedScale)
    {
        WpfTestHost.Invoke(() =>
        {
            const double visualRadius = 16;
            Window? previousMainWindow = Application.Current.MainWindow;
            var window = new Window
            {
                Width = (SystemParameters.WorkArea.Width - 40) / expectedScale,
                Height = (SystemParameters.WorkArea.Height - 40) / expectedScale,
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                Background = Brushes.White,
                Content = new Border { Background = Brushes.White },
                ShowActivated = false,
                ShowInTaskbar = false,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -10000,
                Top = -10000
            };
            nint region = CreateRectRgn(0, 0, 0, 0);
            try
            {
                AboutWindowChrome.FitToWorkArea(window, visualRadius);
                Assert.Equal(nint.Zero, new WindowInteropHelper(window).Handle);
                window.Show();
                window.UpdateLayout();
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);

                nint handle = new WindowInteropHelper(window).Handle;
                Assert.True(GetWindowRect(handle, out NativeRect bounds));
                Assert.True(GetWindowRgn(handle, region) > 0, "The opaque window must have a native rounding region.");
                Assert.False(window.AllowsTransparency);
                DpiScale dpi = VisualTreeHelper.GetDpi(window);
                double radius = visualRadius * expectedScale * dpi.DpiScaleX;
                int outside = (int)Math.Round(radius * 0.2);
                int inside = (int)Math.Round(radius * 0.6);
                int width = bounds.Right - bounds.Left;
                int height = bounds.Bottom - bounds.Top;

                // These probes distinguish the visual radius from the half-radius regression.
                // Check the real HWND region, which RenderTargetBitmap does not include.
                foreach ((bool right, bool bottom) in new[] { (false, false), (true, false), (false, true), (true, true) })
                {
                    int ProbeX(int offset) => right ? width - 1 - offset : offset;
                    int ProbeY(int offset) => bottom ? height - 1 - offset : offset;
                    Assert.False(PtInRegion(region, ProbeX(outside), ProbeY(outside)), $"Corner ({right}, {bottom}) extends beyond the visual curve at scale {expectedScale}.");
                    Assert.True(PtInRegion(region, ProbeX(inside), ProbeY(inside)), $"Corner ({right}, {bottom}) clips inside the visual curve at scale {expectedScale}.");
                }
                Assert.True(PtInRegion(region, width / 2, height / 2));
            }
            finally
            {
                window.Close();
                Application.Current.MainWindow = previousMainWindow;
                DeleteObject(region);
            }
        });
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left, Top, Right, Bottom;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint window, out NativeRect rect);

    [DllImport("user32.dll")]
    private static extern int GetWindowRgn(nint window, nint region);

    [DllImport("gdi32.dll")]
    private static extern nint CreateRectRgn(int left, int top, int right, int bottom);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PtInRegion(nint region, int x, int y);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(nint obj);
}
