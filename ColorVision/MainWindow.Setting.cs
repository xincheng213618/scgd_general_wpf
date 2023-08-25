using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ColorVision
{

    public partial class MainWindow
    {

        const uint WM_USER = 0x0400; // 用户自定义消息起始值

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern int GlobalGetAtomName(ushort nAtom, char[] retVal, int size);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern short GlobalDeleteAtom(short nAtom);


        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            IntPtr handle = new WindowInteropHelper(this).Handle;
            HwndSource.FromHwnd(handle).AddHook(new HwndSourceHook((IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) => {

                if (msg == WM_USER + 1)
                {
                    try
                    {
                        char[] chars = new char[1024];
                        int size = GlobalGetAtomName((ushort)wParam, chars, chars.Length);
                        if (size > 0)
                        {

                            string result = new string(chars, 0, size);
                            MessageBox.Show(result);
                            GlobalDeleteAtom((short)wParam);
                        }
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
                }
                return IntPtr.Zero;
            }));
        }

    }
}
