using ColorVision.MQTT;
using ColorVision.MySql;
using ColorVision.RC;
using ColorVision.Services;
using ColorVision.Themes;
using log4net;
using log4net.Config;
using System;
using System.Threading;
using System.Threading.Tasks;
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
        public static bool IsReStart { get; set; }

        public App()
        {
            Startup += (s, e) => Application_Startup(s, e);
            Exit += new ExitEventHandler(Application_Exit);
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            var SoftwareSetting = ConfigHandler.GetInstance().SoftwareConfig.SoftwareSetting;
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
                MySqlControl.GetInstance().Connect();
                MQTTControl.GetInstance().MQTTConnectChanged += async (s, e) =>
                {
                    await RCService.GetInstance().Connect();
                };
                Task.Run(() => MQTTControl.GetInstance().Connect());
                MainWindow MainWindow = new MainWindow();
                ServiceManager.GetInstance().GenDeviceDisplayControl();
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
