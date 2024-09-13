// // Copyright (c) Microsoft. All rights reserved.
// // Licensed under the MIT license. See LICENSE file in the project root for full license information.

using ColorVision.Common.NativeMethods;
using ColorVision.UI.Shell;
using log4net;
using log4net.Config;
using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

[assembly: XmlConfigurator(ConfigFile = "log4net.config", Watch = true)]
namespace ColorVision
{
    /// <summary>
    /// Main函数的解析，在程序之中，为了不影响APP，独立出来了一个类
    /// </summary>
    public partial class App
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(App));
        private static Mutex mutex;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        const uint WM_USER = 0x0400; // 用户自定义消息起始值

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern ushort GlobalAddAtom(string lpString);

        [STAThread]
        [DebuggerNonUserCode]
        [GeneratedCode("PresentationBuildTasks", "4.0.0.0")]
        public static void Main(string[] args)
        {
            ArgumentParser.GetInstance().CommandLineArgs = args;

            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            if (Environment.CurrentDirectory.Contains("C:\\Program Files"))
            {
                var fileAppender = (log4net.Appender.FileAppender)LogManager.GetRepository().GetAppenders().FirstOrDefault(a => a is log4net.Appender.FileAppender);
                if (fileAppender != null)
                {
                    fileAppender.File = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\ColorVision\\Log\\";
                    fileAppender.ActivateOptions();
                }
            }

            mutex = new Mutex(true, "ColorVision", out bool ret);
            if (!ret && !Debugger.IsAttached)
            {
                IntPtr hWnd = CheckAppRunning.Check("ColorVision");
                if (hWnd != IntPtr.Zero)
                {
                    if (args.Length > 0)  
                    {
                        char separator = '\u0001';
                        string combinedArgs = string.Join(separator.ToString(), args);
                        ushort atom = GlobalAddAtom(combinedArgs);
                        SendMessage(hWnd, WM_USER + 1, IntPtr.Zero,(IntPtr)atom);  // 发送消息
                    }
                    log.Info("程序已经打开");
                    Environment.Exit(0);
                }
                ////写在这里可以Avoid命令行多开的效果，但是没有办法检测版本，实现同版本的情况下更新条件唯一
                //Environment.Exit(0);
            }

            log.Info("程序打开");
            App app;
            app = new App();
            app.InitializeComponent();
            app.Run();
        }
    }
}