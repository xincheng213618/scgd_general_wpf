using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;

namespace ColorVision.UI.Tests;

[Collection(AssemblyDiscoveryCollection.CollectionName)]
public sealed class SearchPopupAirspaceTests
{
    [Fact]
    public void AttachedPopupHasItsOwnNativeSurfaceAboveAnEmbeddedChildWithoutActivatingTheOwner()
    {
        WpfTestHost.Invoke(() =>
        {
            // An invisible, non-activating test host and an inert STATIC child HWND only.
            // No browser, device, production MainWindow, keyboard injection or mouse capture.
            using NativeChild native = new();
            var root = new Grid();
            root.Children.Add(native);
            var owner = new Window
            {
                Content = root, Width = 300, Height = 200, Left = -10000, Top = -10000,
                ShowInTaskbar = false, ShowActivated = false, Opacity = 0, WindowStyle = WindowStyle.None
            };
            var content = new Border { Width = 160, Height = 80, Background = Brushes.Transparent, Opacity = 0 };
            var popup = new Popup { PlacementTarget = root, Placement = PlacementMode.Relative, Child = content, AllowsTransparency = true, StaysOpen = true };
            root.Children.Add(popup);
            try
            {
                owner.Show();
                owner.UpdateLayout();
                popup.IsOpen = true;
                content.UpdateLayout();
                nint ownerHandle = new WindowInteropHelper(owner).Handle;
                nint popupHandle = Assert.IsType<HwndSource>(PresentationSource.FromVisual(content)).Handle;
                Assert.NotEqual(nint.Zero, native.Handle);
                Assert.NotEqual(ownerHandle, popupHandle);
                Assert.True(IsChild(ownerHandle, native.Handle));
                Assert.False(IsChild(ownerHandle, popupHandle));
                long extendedStyle = GetWindowLongPtr(popupHandle, -20).ToInt64();
                Assert.NotEqual(0, extendedStyle & 0x80); // WS_EX_TOOLWINDOW: no taskbar entry.
                Assert.Equal(0, extendedStyle & 0x40000); // Not WS_EX_APPWINDOW.
                Assert.False(owner.IsActive);
            }
            finally
            {
                popup.IsOpen = false;
                popup.Child = null;
                owner.Content = null;
                owner.Close();
            }
        });
    }

    private sealed class NativeChild : HwndHost
    {
        protected override HandleRef BuildWindowCore(HandleRef hwndParent)
        {
            nint child = CreateWindowEx(0, "STATIC", "", unchecked((int)0x50000000), 0, 0, 100, 100,
                hwndParent.Handle, nint.Zero, nint.Zero, nint.Zero);
            if (child == nint.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
            return new HandleRef(this, child);
        }
        protected override void DestroyWindowCore(HandleRef hwnd) => DestroyWindow(hwnd.Handle);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CreateWindowEx(int extendedStyle, string className, string name, int style,
        int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(nint window);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsChild(nint parent, nint child);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern nint GetWindowLongPtr(nint window, int index);
}
