

using System;
using System.Runtime.InteropServices;

namespace ColorVision.Common.NativeMethods
{
    public static class Dwmapi
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct Margins
        {
            public int cxLeftWidth;
            public int cxRightWidth;
            public int cyTopHeight;
            public int cyBottomHeight;

            public Margins(int cxLeftWidth, int cxRightWidth, int cyTopHeight, int cyBottomHeight)
            {
                this.cxLeftWidth = cxLeftWidth;
                this.cxRightWidth = cxRightWidth;
                this.cyTopHeight = cyTopHeight;
                this.cyBottomHeight = cyBottomHeight;
            }
        }

        public enum Window
        {
            UseImmersiveDarkMode = 20,
            SystembackdropType = 38,
            MicaEffect = 1029
        }

        public enum SystembackdropType
        {
            Auto = 0,
            None = 1,
            MainWindow = 2,
            TransientWindow = 3,
            TabbedWindow = 4
        }

        [DllImport("DwmApi.dll")]
        private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref Margins pMarInset);

        public static int ExtendFrameIntoClientArea(IntPtr hwnd, ref Margins pMarInset) => DwmExtendFrameIntoClientArea(hwnd,ref pMarInset);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, uint dwAttribute, ref int pvAttribute, int cbAttribute);
        public static int SetWindowAttribute(IntPtr hwnd, uint dwAttribute, ref int pvAttribute, int cbAttribute) => DwmSetWindowAttribute(hwnd, dwAttribute,ref pvAttribute, cbAttribute);



    }
}
