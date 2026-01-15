using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Authorizations;
using ColorVision.UI.Languages;
using log4net;
using log4net.Config;
using System.Reflection;
using System.Windows;

[assembly: XmlConfigurator(ConfigFile = "log4net.config", Watch = true)]
namespace Spectrum
{
    public partial class App : Application
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(App));

        public App()
        {

#if (DEBUG == false)
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            Application.Current.DispatcherUnhandledException += Application_DispatcherUnhandledException;
#endif

        }
        private void Application_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            log.Fatal(e.Exception);
            MessageBox.Show(e.Exception.Message);
            //使用这一行代码告诉运行时，该异常被处理了，不再作为UnhandledException抛出了。
            e.Handled = true;
        }

        void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            log.Fatal(e.ExceptionObject);
        }


        private void Application_Startup(object sender, StartupEventArgs e)
        {
            if (Environment.CurrentDirectory.Contains("C:\\Program Files"))
            {
                var fileAppender = (log4net.Appender.FileAppender)LogManager.GetRepository().GetAppenders().FirstOrDefault(a => a is log4net.Appender.FileAppender);
                if (fileAppender != null)
                {
                    fileAppender.File = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\Spectromer\\Log\\";
                    fileAppender.ActivateOptions();
                }
            }

            log.Info($"程序打开{Assembly.GetExecutingAssembly().GetName().Version}");
            ConfigHandler.GetInstance();
            Authorization.Instance = ConfigService.Instance.GetRequiredService<Authorization>();
            LogConfig.Instance.SetLog();
            LicenseSync.SyncLicenses();
            this.ApplyTheme(ThemeConfig.Instance.Theme);
            Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo(LanguageConfig.Instance.UICulture);
            MainWindow MainWindow = new MainWindow();
            MainWindow.Show();

        }
    }
}
