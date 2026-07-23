using ColorVision.SocketProtocol;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Authorizations;
using ColorVision.UI.Languages;
using log4net;
using log4net.Config;
using Spectrum.License;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
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
            Stopwatch startupStopwatch = Stopwatch.StartNew();

            if (Environment.CurrentDirectory.Contains("C:\\Program Files"))
            {
                var fileAppender = (log4net.Appender.FileAppender)LogManager.GetRepository().GetAppenders().FirstOrDefault(a => a is log4net.Appender.FileAppender);
                if (fileAppender != null)
                {
                    fileAppender.File = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\Spectromer\\Log\\";
                    fileAppender.ActivateOptions();
                }
            }

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); // 确保 .NET Core 及以上支持 GBK
                                                                           //加载DLL
            if (File.Exists("ColorVision.Scheduler.dll"))
                Assembly.LoadFrom("ColorVision.Scheduler.dll"); 
            if (File.Exists("ColorVision.SocketProtocol.dll"))
                Assembly.LoadFrom("ColorVision.SocketProtocol.dll"); 
            if (File.Exists("ColorVision.Database.dll"))
                Assembly.LoadFrom("ColorVision.Database.dll"); 
            if (File.Exists("ColorVision.UI.Desktop.dll"))
                Assembly.LoadFrom("ColorVision.UI.Desktop.dll"); 




            log.Info($"程序打开{Assembly.GetExecutingAssembly().GetName().Version}");
            ConfigHandler.GetInstance();
            Authorization.Instance = ConfigService.Instance.GetRequiredService<Authorization>();
            LogConfig.Instance.SetLog();
            LicenseSync.SyncLicenses();
            this.ApplyTheme(ThemeConfig.Instance.Theme);
            Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo(LanguageConfig.Instance.UICulture);

            new SocketInitializer().InitializeAsync();
            log.Info($"启动基础配置完成，耗时 {startupStopwatch.ElapsedMilliseconds} ms");

            Stopwatch windowStopwatch = Stopwatch.StartNew();
            MainWindow mainWindow = new MainWindow();
            log.Info($"主窗口对象创建完成，耗时 {windowStopwatch.ElapsedMilliseconds} ms，启动累计 {startupStopwatch.ElapsedMilliseconds} ms");
            mainWindow.Show();
            log.Info($"主窗口 Show 调用完成，启动累计 {startupStopwatch.ElapsedMilliseconds} ms");

        }
    }
}
