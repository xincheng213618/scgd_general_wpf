using ColorVision.Solution;
using System;
using System.Windows.Interop;

namespace ColorVision
{

    public partial class MainWindow
    {

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            IntPtr handle = new WindowInteropHelper(this).Handle;
            HwndSource.FromHwnd(handle).AddHook(new HwndSourceHook((IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) => {
                if (msg == SingleInstanceCommandLineTransport.MessageId
                    && SingleInstanceCommandLineTransport.TryReceive(lParam, out string[] parsedArgs))
                {
                    try
                    {
                        ForwardedCommandLineHandler.Handle(parsedArgs);
                        if (WindowState == System.Windows.WindowState.Minimized)
                            WindowState = System.Windows.WindowState.Normal;
                        Activate();
                        handled = true;
                        return new IntPtr(1);
                    }
                    catch (Exception ex) 
                    { 
                        log.Error(ex);
                    }
                }
                return IntPtr.Zero;
            }));
        }

    }
}
