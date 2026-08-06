// // Copyright (c) Microsoft. All rights reserved.
// // Licensed under the MIT license. See LICENSE file in the project root for full license information.

using ColorVision.UI.Shell;
using log4net;
using log4net.Config;
using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.Linq;
using System.Threading;

[assembly: XmlConfigurator(ConfigFile = "log4net.config", Watch = true)]
namespace ColorVision
{
    /// <summary>
    /// Main�����Ľ������ڳ���֮�У�Ϊ�˲�Ӱ��APP������������һ����
    /// </summary>
    public partial class App
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(App));

        private static Mutex mutex;

        [STAThread]
        [DebuggerNonUserCode]
        [GeneratedCode("PresentationBuildTasks", "4.0.0.0")]
        public static void Main(string[] args)
        {
            ProgramTimer.Start();
            ArgumentParser.GetInstance().CommandLineArgs = args;
            log.Debug("args" + string.Join(", ", args));

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

            App app;
            app = new App();
            app.InitializeComponent();
            app.Run();
        }

        private static void KillZombieProcesses()
        {
            Process currentProcess = Process.GetCurrentProcess();
            string processName = currentProcess.ProcessName;
            int currentProcessId = currentProcess.Id;

            Process[] processes = Process.GetProcessesByName(processName);
            foreach (Process process in processes)
            {
                if (process.Id == currentProcessId)
                    continue;
                try
                {
                    if (process.MainWindowHandle == IntPtr.Zero)
                    {
                        log.Info(ColorVision.Properties.Resources.TerminateUnresponsiveProcess); 
                        process.Kill();
                        process.WaitForExit(1000); 
                    }
                }
                catch (Exception)
                {
                }
            }
        }
    }
}
