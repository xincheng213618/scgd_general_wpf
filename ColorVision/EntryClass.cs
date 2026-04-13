// // Copyright (c) Microsoft. All rights reserved.
// // Licensed under the MIT license. See LICENSE file in the project root for full license information.

using ColorVision.UI.Shell;
using ColorVision.Core;
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
    /// Main�����Ľ������ڳ���֮�У�Ϊ�˲�Ӱ��APP������������һ����
    /// </summary>
    public partial class App
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(App));

        private static Mutex mutex;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        const uint WM_USER = 0x0400; // �û��Զ�����Ϣ��ʼֵ

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern ushort GlobalAddAtom(string lpString);

        [STAThread]
        [DebuggerNonUserCode]
        [GeneratedCode("PresentationBuildTasks", "4.0.0.0")]
        public static void Main(string[] args)
        {
            ProgramTimer.Start();
            ArgumentParser.GetInstance().CommandLineArgs = args;
            log.Debug("args��" + string.Join(", ", args));

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

            NativeLogBridge.Initialize((source, level, message) =>
            {
                string prefix = $"[Native:{source}] ";
                switch (level)
                {
                    case NativeLogLevel.Trace:
                        log.Logger.Log(typeof(App), log4net.Core.Level.Trace, prefix + message, null);
                        break;
                    case NativeLogLevel.Debug:
                        log.Debug(prefix + message);
                        break;
                    case NativeLogLevel.Info:
                        log.Info(prefix + message);
                        break;
                    case NativeLogLevel.Warn:
                        log.Warn(prefix + message);
                        break;
                    case NativeLogLevel.Error:
                        log.Error(prefix + message);
                        break;
                    default:
                        log.Info(prefix + message);
                        break;
                }
            }, level: NativeLogLevel.Info, enableLogs: true, enableNativeSink: false);

            //ɱ����ʬ����
            App app;
            app = new App();
            app.InitializeComponent();
            app.Run();
        }

        private static void KillZombieProcesses()
        {
            // ��ȡ��ǰ���̵����ƺ�ID
            Process currentProcess = Process.GetCurrentProcess();
            string processName = currentProcess.ProcessName;
            int currentProcessId = currentProcess.Id;

            // ��ȡ����ͬ������
            Process[] processes = Process.GetProcessesByName(processName);
            foreach (Process process in processes)
            {
                // ������ǰ����
                if (process.Id == currentProcessId)
                    continue;
                try
                {
                    // 2. �����жϣ����ý����Ƿ��������ھ��
                    // ��� MainWindowHandle Ϊ IntPtr.Zero��˵���ý���û�������ڣ����ں�̨���У�
                    if (process.MainWindowHandle == IntPtr.Zero)
                    {
                        log.Info(ColorVision.Properties.Resources.TerminateUnresponsiveProcess); // ����ʹ���Զ�����ʾ��"���ֺ�̨�������̣�������ֹ..."

                        // ��ֹ��ʬ����
                        process.Kill();
                        process.WaitForExit(1000); // �ȴ����1��ȷ���˳�����������
                        log.Info($"����ֹ��̨��ʬ���̣�PID {process.Id}");
                    }
                }
                catch (Exception ex)
                {
                    // �������ܵ��쳣������Ȩ�޲��������ڼ��������������˳�
                    log.Warn($"�޷���ֹ���̣�PID {process.Id}������{ex.Message}");
                }
            }
        }
    }
}