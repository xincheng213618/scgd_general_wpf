using System;
using System.Windows.Forms.Integration;

namespace ColorVision.Engine.Templates.Flow
{
    /// <summary>
    /// Hosts the custom-drawn node editor without exposing an unusable UI Automation peer.
    /// </summary>
    /// <remarks>
    /// WindowsFormsHost handles WM_GETOBJECT by creating a WPF automation provider for its
    /// WinForms child. UI Automation can occasionally re-enter that provider while it is
    /// resolving the host HWND, leaving the application dispatcher spinning indefinitely.
    /// STNodeEditor does not expose an accessible object model, so returning no provider is
    /// preferable to blocking the application UI thread.
    /// </remarks>
    public class STNodeEditorHost : WindowsFormsHost
    {
        private const int WmGetObject = 0x003D;

        protected override IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WmGetObject)
            {
                handled = true;
                return IntPtr.Zero;
            }

            return base.WndProc(hwnd, msg, wParam, lParam, ref handled);
        }
    }
}
