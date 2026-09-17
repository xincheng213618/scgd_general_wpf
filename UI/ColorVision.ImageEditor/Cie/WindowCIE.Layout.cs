using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace ColorVision.ImageEditor;

public partial class WindowCIE
{
    private void CieTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitialized && ReferenceEquals(e.OriginalSource, CieTabs)) EnsurePageSpace();
    }

    private void EnsurePageSpace()
    {
        // Reserve space for the active page; returning to a smaller page never
        // shrinks a window that the user has already arranged.
        Size minimum = CieTabs.SelectedIndex switch
        {
            1 => new(940, 680),
            2 => new(1080, 780),
            _ => new(720, 520)
        };
        Rect workArea = GetCurrentWorkArea();
        MinWidth = Math.Min(minimum.Width, workArea.Width);
        MinHeight = Math.Min(minimum.Height, workArea.Height);
        if (WindowState != WindowState.Normal) return;
        double width = Math.Max(Width, MinWidth), height = Math.Max(Height, MinHeight);
        bool growing = width > ActualWidth || height > ActualHeight;
        SetCurrentValue(WidthProperty, width);
        SetCurrentValue(HeightProperty, height);
        if (!IsLoaded || !growing) return;
        // Keep the enlarged window on its current monitor, including at high DPI.
        Left = Math.Clamp(Left, workArea.Left, Math.Max(workArea.Left, workArea.Right - width));
        Top = Math.Clamp(Top, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - height));
    }

    private Rect GetCurrentWorkArea()
    {
        IntPtr handle = new WindowInteropHelper(this).Handle;
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (handle == IntPtr.Zero || !GetMonitorInfo(MonitorFromWindow(handle, 2), ref info)) return SystemParameters.WorkArea;
        Matrix transform = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        return new Rect(transform.Transform(new Point(info.Work.Left, info.Work.Top)), transform.Transform(new Point(info.Work.Right, info.Work.Bottom)));
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo { public int Size; public NativeRect Monitor, Work; public uint Flags; }
    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
}
