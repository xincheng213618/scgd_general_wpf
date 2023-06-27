using ColorVision.NativeMethods;
using ColorVision.SettingUp;
using log4net;
using log4net.Config;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;

[assembly: XmlConfigurator(ConfigFile = "log4net.config", Watch = true)]
namespace ColorVision
{
    [StructLayout(LayoutKind.Sequential)]
    struct COPYDATASTRUCT
    {
        public IntPtr dwData;
        public int cbData;
        public IntPtr lpData;
    }



    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(App));
        [STAThread]
        [DebuggerNonUserCode]
        [GeneratedCode("PresentationBuildTasks", "4.0.0.0")]
        public static void Main(string[] args)
        {

            bool IsCheck =true;
            if (args.Count()>0)
            {
                for (int i = 0; i < args.Count(); i++)
                {
                    if (args[i] == "-r")
                    {
                        IsCheck = false;
                    }
                }
            }
            if (Environment.CurrentDirectory.Contains("C:\\Program Files"))
            {
                var fileAppender = (log4net.Appender.FileAppender)LogManager.GetRepository().GetAppenders().FirstOrDefault(a => a is log4net.Appender.FileAppender);
                if (fileAppender != null)
                {
                    fileAppender.File = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\ColorVision\\";
                    fileAppender.ActivateOptions();
                }
            }

            Mutex mutex = new Mutex(true, "ElectronicNeedleTherapySystem", out bool ret);
            if (IsCheck &&!ret)
            {
                //System.Windows.MessageBox.Show("程序已经运行！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                IntPtr hWnd = CheckAppRunning.Check("ColorVision");

                if (args.Length > 0 && hWnd != IntPtr.Zero)
                {
                    ushort atom = GlobalAddAtom(args[0]);
                    SendMessage(hWnd, WM_USER + 1, (IntPtr)atom, IntPtr.Zero);  // 发送消息
                }
                log.Info("程序已经打开");
                Environment.Exit(0);
            }

            log.Info("程序打开");
            App app;
            app = new App();
            app.InitializeComponent();
            app.Run();
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        const uint WM_USER = 0x0400; // 用户自定义消息起始值

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern ushort GlobalAddAtom(string lpString);

        private App()
        {
            Startup += (s, e) => Application_Startup(s, e);
            Exit += new ExitEventHandler(Application_Exit);
        }



        private void Application_Startup(object sender, StartupEventArgs e)
        {
            GlobalSetting.GetInstance();


            //Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("en");
            Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("zh-Hans");

            System.Windows.Forms.Application.EnableVisualStyles();
            System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);

            MainWindow mainWindow = new MainWindow();
            mainWindow.Show();
        }

        /// <summary>
        /// Application Close
        /// </summary>
        private void Application_Exit(object sender, ExitEventArgs e)
        {
            log.Info("程序关闭");
            Environment.Exit(0);
        }
    }
}
