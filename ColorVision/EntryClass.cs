// // Copyright (c) Microsoft. All rights reserved.
// // Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
            ProgramTimer.Start();
            ArgumentParser.GetInstance().CommandLineArgs = args;
            log.Debug("args：" + string.Join(", ", args));

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

            //杀死僵尸进程
            App app;
            app = new App();
            app.InitializeComponent();
            app.Run();
        }

        private static void KillZombieProcesses()
        {
            // 获取当前进程的名称和ID
            Process currentProcess = Process.GetCurrentProcess();
            string processName = currentProcess.ProcessName;
            int currentProcessId = currentProcess.Id;

            // 获取所有同名进程
            Process[] processes = Process.GetProcessesByName(processName);
            foreach (Process process in processes)
            {
                // 跳过当前进程
                if (process.Id == currentProcessId)
                    continue;
                try
                {
                    // 2. 核心判断：检查该进程是否有主窗口句柄
                    // 如果 MainWindowHandle 为 IntPtr.Zero，说明该进程没有主窗口（即在后台运行）
                    if (process.MainWindowHandle == IntPtr.Zero)
                    {
                        log.Info(ColorVision.Properties.Resources.TerminateUnresponsiveProcess); // 或者使用自定义提示："发现后台残留进程，正在终止..."

                        // 终止僵尸进程
                        process.Kill();
                        process.WaitForExit(1000); // 等待最多1秒确认退出，避免死锁
                        log.Info($"已终止后台僵尸进程：PID {process.Id}");
                    }
                }
                catch (Exception ex)
                {
                    // 处理可能的异常，例如权限不足或进程在检查过程中已自行退出
                    log.Warn($"无法终止进程：PID {process.Id}，错误：{ex.Message}");
                }
            }
        }
    }
}