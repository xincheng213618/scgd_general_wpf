using ColorVision.NativeMethods;
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

[assembly: XmlConfigurator(ConfigFile = "ColorVision.dll.config", Watch = true)]
namespace ColorVision
{
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
            Mutex mutex = new Mutex(true, "ElectronicNeedleTherapySystem", out bool ret);
            if (!ret)
            {
                //System.Windows.MessageBox.Show("程序已经运行！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                CheckAppRunning.Check();
                log.Info("程序已经打开");
                Environment.Exit(0);
            }
            log.Info("程序打开");
            App app;
            app = new App();
            app.InitializeComponent();
            app.Run();
        }


        private App()
        {
            Startup += (s, e) => Application_Startup(s, e);
            Exit += new ExitEventHandler(Application_Exit);
        }



        private void Application_Startup(object sender, StartupEventArgs e)
        {
            //Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("en");
            Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("zh-Hans");
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
