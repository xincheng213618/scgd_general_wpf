using ColorVision.NativeMethods;
using ColorVision.Services;
using ColorVision.SettingUp;
using ColorVision.Themes;
using log4net;
using log4net.Config;
using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

[assembly: XmlConfigurator(ConfigFile = "log4net.config", Watch = true)]
namespace ColorVision
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(App));
        private static Mutex mutex;

        public static bool IsReStart { get; set; }

        [STAThread]
        [DebuggerNonUserCode]
        [GeneratedCode("PresentationBuildTasks", "4.0.0.0")]
        public static void Main(string[] args)
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            bool IsDebug = Debugger.IsAttached;
            if (args.Count()>0)
            {
                for (int i = 0; i < args.Count(); i++)
                {
                    if (args[i].ToLower() == "-d" || args[i].ToLower() == "-debug")
                    {
                        IsDebug = true;
                    }
                    if (args[i].ToLower() == "-r" || args[i].ToLower() == "-restart")
                    {
                        IsReStart = true;
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

            mutex = new Mutex(true, "ElectronicNeedleTherapySystem", out bool ret);
            if (!IsDebug &&!ret)
            {
                IntPtr hWnd = CheckAppRunning.Check("ColorVision");
                if (hWnd != IntPtr.Zero)
                {
                    if (args.Length > 0)
                    {
                        ushort atom = GlobalAddAtom(args[0]);
                        SendMessage(hWnd, WM_USER + 1, (IntPtr)atom, IntPtr.Zero);  // 发送消息
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

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        const uint WM_USER = 0x0400; // 用户自定义消息起始值

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern ushort GlobalAddAtom(string lpString);


        public App()
        {

            Startup += (s, e) => Application_Startup(s, e);
            Exit += new ExitEventHandler(Application_Exit);
        }



        private void Application_Startup(object sender, StartupEventArgs e)
        {
            var SoftwareSetting = GlobalSetting.GetInstance().SoftwareConfig.SoftwareSetting;
            this.ApplyTheme(SoftwareSetting.Theme);
            Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo(SoftwareSetting.UICulture);
            //Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("en");
            //Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("ja");

            //这里的代码是因为WPF中引用了WinForm的控件，所以需要先初始化
            System.Windows.Forms.Application.EnableVisualStyles();
            System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);

            //代码先进入启动窗口

            if (!IsReStart)
            {
                StartWindow StartWindow = new StartWindow();
                StartWindow.Show();
            }
            else
            {
                MainWindow MainWindow = new MainWindow();
                ServiceControl.GetInstance().GenContorl();
                MainWindow.Show();
            }


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
