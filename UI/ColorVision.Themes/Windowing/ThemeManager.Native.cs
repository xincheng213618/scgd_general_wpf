#pragma warning disable CA1707 // Preserve the public Win32 enum names.
using System;
using System.Runtime.InteropServices;

namespace ColorVision.Themes;

public partial class ThemeManager
{
        public static void SetWindowTitleBarColor(IntPtr hwnd, Theme theme)
        {
            uint attribute;
            uint attributeSize = (uint)Marshal.SizeOf<uint>();

            switch (theme)
            {
                case Theme.Dark:
                    // Reset caption color to system default
                    ResetCaptionColor(hwnd);

                    // Enable dark mode
                    attribute = 1;
                    _ = DwmSetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref attribute, attributeSize);
                    _ = DwmSetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE, ref attribute, attributeSize);
                    break;

                case Theme.Light:
                case Theme.UseSystem:
                default:
                    // Reset caption color to system default
                    ResetCaptionColor(hwnd);

                    // Disable dark mode
                    attribute = 0;
                    _ = DwmSetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref attribute, attributeSize);
                    _ = DwmSetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE, ref attribute, attributeSize);
                    break;
            }
        }

        private static void ResetCaptionColor(IntPtr hwnd)
        {
            ///DWMWA_COLOR_DEFAULT
            uint attribute = 0xFFFFFFFF;
            uint attributeSize = (uint)Marshal.SizeOf<uint>();
            //Specifying DWMWA_COLOR_DEFAULT (value 0xFFFFFFFF) for the color will reset the window back to using the system's default behavior for the caption color.
            _ = DwmSetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_CAPTION_COLOR, ref attribute, attributeSize);
            //Specifying DWMWA_COLOR_NONE (value 0xFFFFFFFE) for the color will suppress the drawing of the window border. This makes it possible to have a rounded window with no border.
            _ = DwmSetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_BORDER_COLOR, ref attribute, attributeSize);
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, DWMWINDOWATTRIBUTE attribute, ref uint pvAttribute, uint cbAttribute);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, DWMWINDOWATTRIBUTE attribute, IntPtr pvAttribute, uint cbAttribute);

        [Flags]
        public enum DWMWINDOWATTRIBUTE : uint
        {
            //沉浸式暗模式20H1
            DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19,
            //沉浸式暗模式
            DWMWA_USE_IMMERSIVE_DARK_MODE = 20,
            ///Might require Windows SDK 10.0.22000.0 (aka first Windows 11 SDK)
            //设置窗口边框颜色
            DWMWA_BORDER_COLOR = 34,
            //设置窗口标题栏颜色。
            DWMWA_CAPTION_COLOR = 35,
            //设置窗口标题栏文本颜色。
            DWMWA_TEXT_COLOR = 36,
        }
}
