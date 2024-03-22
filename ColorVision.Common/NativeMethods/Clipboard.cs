using System;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;

namespace ColorVision.NativeMethods
{

    /// <summary>
    /// 粘贴板
    /// </summary>
    /// 解决粘贴板中的占用问题，参考：https://www.cnblogs.com/zhaoqingqing/p/11301873.html
    public static class Clipboard
    {
        [DllImport("User32")]
        private static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("User32")]
        private static extern bool CloseClipboard();   

        [DllImport("User32")]
        private static extern bool EmptyClipboard();

        [DllImport("User32")]
        private static extern bool IsClipboardFormatAvailable(int format);

        [DllImport("User32")]
        private static extern IntPtr GetClipboardData(int uFormat);

        [DllImport("User32", CharSet = CharSet.Unicode)]
        private static extern IntPtr SetClipboardData(int uFormat, IntPtr hMem);

        public static void SetImage(BitmapSource image)
        {
            System.Windows.Clipboard.SetImage(image);
        }

        public static string? GetText()
        {
            if (!OpenClipboard(IntPtr.Zero))
            {
                return System.Windows.Clipboard.GetText();
            }
            if (!IsClipboardFormatAvailable(13))
            {
                CloseClipboard();
                return System.Windows.Clipboard.GetText();
            }

            IntPtr handle = GetClipboardData(13);
            string text = Marshal.PtrToStringUni(handle);
            CloseClipboard();
            return text;
        }

        public static void SetText(string text)
        {
            if (!OpenClipboard(IntPtr.Zero))
            {
                System.Windows.Clipboard.SetText(text);
                return;
            }
            EmptyClipboard();
            IntPtr handle = Marshal.StringToHGlobalUni(text);
            SetClipboardData(13, handle);
            CloseClipboard();
        }
    }

}
