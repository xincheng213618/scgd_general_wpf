using ColorVision.Common.Utilities;
using ColorVision.Media;
using ColorVision.MQTT;
using ColorVision.MySql;
using ColorVision.Net;
using ColorVision.Services;
using ColorVision.Services.RC;
using ColorVision.Settings;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Languages;
using ColorVision.Utils;
using ColorVision.Wizards;
using log4net;
using log4net.Config;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
            ConfigHandler1.GetInstance();
            ConfigHandler.GetInstance();

            this.ApplyTheme(ThemeConfig.Instance.Theme);
            Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo(LanguageConfig.Instance.UICulture);
            //Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("en");
            //Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("ja");

            if (Sysargs.Length > 0)
            {
                for (int i = 0; i < Sysargs.Length; i++)
                {
                    if (Sysargs[i].EndsWith("cvraw", StringComparison.OrdinalIgnoreCase))
                    {
                        ImageView imageView = new();
                        CVFileUtil.ReadCVRaw(Sysargs[i], out CVCIEFile fileInfo);
                        Window window = new() { Title = "快速预览" };
                        window.Content = imageView;
                        imageView.OpenImage(fileInfo);
                        window.Show();
                        return;
                    }
                    else if (Tool.IsImageFile(Sysargs[i]))
                    {
                        ImageView imageView = new();
                        Window window = new() { Title = "快速预览" };
                        window.Content = imageView;
                        imageView.OpenImage(Sysargs[i]);
                        window.Show();
                        return;
                    }
                    else if (File.Exists(Sysargs[i]))
                    {
                        PlatformHelper.Open(Sysargs[i]);
                        return;
                    }
                }
            }

            //这里的代码是因为WPF中引用了WinForm的控件，所以需要先初始化
            System.Windows.Forms.Application.EnableVisualStyles();
            System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
            //代码先进入启动窗口
            if (!ConfigHandler.GetInstance().SoftwareConfig.SoftwareSetting.WizardCompletionKey)
            {
                WizardWindow wizardWindow = new();
                wizardWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                wizardWindow.Show();
            }
            else if (!IsReStart)
            {
                ///正常进入窗口
                StartWindow StartWindow = new();
                StartWindow.Show();
            }
            else
            {
                MySqlControl.GetInstance().Connect();
                MQTTControl.GetInstance().MQTTConnectChanged += async (s, e) =>
                {
                    await MQTTRCService.GetInstance().Connect();
                };
                Task.Run(() => MQTTControl.GetInstance().Connect());
                MainWindow MainWindow = new();
                ServiceManager.GetInstance().GenDeviceDisplayControl();
                MainWindow.Show();
            }
        }

        /// <summary>
        /// Application Close
        /// </summary>
        private void Application_Exit(object sender, ExitEventArgs e)
        {
            log.Info(ColorVision.Properties.Resource.ApplicationExit);

            Environment.Exit(0);
        }
    }
}
