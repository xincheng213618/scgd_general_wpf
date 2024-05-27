using ColorVision.MQTT;
using ColorVision.MySql;
using ColorVision.Services;
using ColorVision.Services.RC;
using ColorVision.Services.Templates;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Languages;
using ColorVision.Wizards;
using log4net;
using log4net.Config;
using System;
using System.Diagnostics;
using System.IO;
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

        public App()
        {
            Startup += (s, e) => Application_Startup(s, e);
            Exit += new ExitEventHandler(Application_Exit);
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            bool IsDebug = Debugger.IsAttached;
            var parser = ArgumentParser.GetInstance();

            parser.AddArgument("debug", true, "d");
            parser.AddArgument("restart", true, "r");
            parser.AddArgument("solutionpath", false, "s");
            parser.Parse();

            IsDebug = Debugger.IsAttached || parser.GetFlag("debug");

            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);

            PluginLoader.LoadPluginsAssembly("Plugins");
            ConfigHandler.GetInstance();

            this.ApplyTheme(ThemeConfig.Instance.Theme);
            Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo(LanguageConfig.Instance.UICulture);
            //Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("en");
            //Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("ja");
            parser.AddArgument("input", false, "i");
            parser.Parse();

            string inputFile = parser.GetValue("input");
            if (inputFile != null)
            {
                bool isok = FileHandlerProcessor.GetInstance().ProcessFile(inputFile);
                if (isok) return;
            }


            //这里的代码是因为WPF中引用了WinForm的控件，所以需要先初始化
            System.Windows.Forms.Application.EnableVisualStyles();
            System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
            //代码先进入启动窗口

            bool IsReStart = parser.GetFlag("restart");
            if (!WizardConfig.Instance.WizardCompletionKey)
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
                TemplateControl.GetInstance();
                MainWindow.Show();
            }
        }

        /// <summary>
        /// Application Close
        /// </summary>
        private void Application_Exit(object sender, ExitEventArgs e)
        {
            log.Info(ColorVision.Properties.Resources.ApplicationExit);

            Environment.Exit(0);
        }
    }
}
