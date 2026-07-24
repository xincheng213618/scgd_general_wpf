using ColorVision.Common.MVVM;
using ColorVision.Common.NativeMethods;
using ColorVision.Copilot.Mcp;
using ColorVision.Properties;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Desktop.LanRemote;
using ColorVision.UI.Desktop.Wizards;
using ColorVision.UI.Languages;
using ColorVision.UI.Plugins;
using ColorVision.UI.Shell;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace ColorVision
{

    public class APPConfig : ViewModelBase,IConfig
    {
        [ConfigSetting]
        [DisplayName("AllowMultipleInstances")]
        [Description("AllowMultipleInstancesDescription")]
        public bool IsMute { get => _IsMute; set { _IsMute = value; OnPropertyChanged(); } }
        private bool _IsMute = true;
    }

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private bool _isSessionEnding;
        private ModuleCatalog? _moduleCatalog;

        public App()
        {
            Startup += Application_Startup;
            Exit += Application_Exit;
            SessionEnding += (_, _) => _isSessionEnding = true;
            #if(DEBUG == false)
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            Application.Current.DispatcherUnhandledException += Application_DispatcherUnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
            #endif

        }
        private void Application_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            log.Fatal("捕获到 UI Dispatcher 未处理异常，已静默记录。", e.Exception);
            //使用这一行代码告诉运行时，该异常被处理了，不再作为UnhandledException抛出了。
            e.Handled = true;
        }

        void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception exception)
            {
                log.Fatal("捕获到 AppDomain 未处理异常，已静默记录。", exception);
            }
            else
            {
                log.Fatal($"捕获到 AppDomain 未处理异常，已静默记录。ExceptionObject: {e.ExceptionObject}");
            }
        }

        private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            log.Fatal(e.Exception);
            e.SetObserved();
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            if (Update.ExitUpdateHandoff.TryDeferLaunchForActiveUpdate(AppDomain.CurrentDomain.BaseDirectory))
            {
                Environment.Exit(0);
                return;
            }

            bool IsDebug = Debugger.IsAttached;
            var parser = ArgumentParser.GetInstance();

            parser.AddArgument("debug", true, "d");
            parser.AddArgument("restart", true, "r");
            parser.Parse();

            IsDebug = Debugger.IsAttached || parser.GetFlag("debug");

            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);

            string inputFile = parser.GetValue("input");
            if (Update.StartupUpdatePackageHandler.Classify(inputFile) != Update.StartupUpdatePackageKind.None)
            {
                if (Update.StartupUpdatePackageHandler.HandleIfUpdatePackage(inputFile))
                {
                    // A successful update handoff exits the process. Reaching here means preparation failed.
                    Environment.Exit(-1);
                    return;
                }
            }

            _moduleCatalog = new ModuleCatalog(AssemblyHandler.GetInstance());
            BuiltInModules.Register(_moduleCatalog);
            ConfigHandler.GetInstance();
            ConfigHandler.GetInstance().IsAutoSave = false;
            LogConfig.Instance.SetLog();
            this.ApplyTheme(ThemeConfig.Instance.Theme);
            Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo(LanguageConfig.Instance.UICulture);
            //Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("en");
            //Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("ja");

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); // 确保 .NET Core 及以上支持 GBK
            parser.AddArgument("export", false, "e");

            parser.Parse();
            string exportFile = parser.GetValue("export");
            if (exportFile != null)
            {
                FileExportResult exportResult = FileProcessorFactory.GetInstance().TryExportFile(exportFile);
                ProgramTimer.StopAndReport();
                if (exportResult.Succeeded)
                {
                    return;
                }
                else
                {
                    MessageBox.Show(string.IsNullOrWhiteSpace(exportResult.ErrorMessage)
                        ? ColorVision.Properties.Resources.UnsupportedFileFormat
                        : exportResult.ErrorMessage);
                    Environment.Exit(0);
                    return;
                }
            }

            if (StartupFileOpenPolicy.ShouldOpenBeforeMainWindow(inputFile))
            {
                FileOpenRouteResult openResult = File.Exists(inputFile)
                    ? FileProcessorFactory.GetInstance().TryOpenFileAction(inputFile)
                    : new FileOpenRouteResult(true, false, $"文件不存在：{inputFile}");
                if (openResult.Handled)
                {
                    ConfigHandler.GetInstance().IsAutoSave = true;
                    ProgramTimer.StopAndReport();
                    if (!openResult.Succeeded)
                    {
                        MessageBox.Show(string.IsNullOrWhiteSpace(openResult.ErrorMessage)
                            ? ColorVision.Properties.Resources.UnsupportedFileFormat
                            : openResult.ErrorMessage);
                        Environment.Exit(-1);
                    }
                    return;
                }
            }

            ConfigHandler.GetInstance().IsAutoSave = true;

            mutex = new Mutex(true, "ColorVision", out bool ownsMutex);
            bool allowMultipleInstances = ConfigHandler.GetInstance().GetRequiredService<APPConfig>().IsMute;
            if (SingleInstanceStartupPolicy.Decide(
                ownsMutex,
                Debugger.IsAttached,
                allowMultipleInstances) == SingleInstanceStartupAction.ActivateExistingInstance)
            {
                IntPtr hWnd = CheckAppRunning.Check("ColorVision");
                if (hWnd != IntPtr.Zero)
                {
                    if (ArgumentParser.GetInstance().CommandLineArgs.Length > 0)
                    {
                        if (!SingleInstanceCommandLineTransport.TrySend(
                            hWnd,
                            ArgumentParser.GetInstance().CommandLineArgs))
                        {
                            log.Warn("无法将启动参数转发到现有 ColorVision 实例。");
                        }
                    }
                    log.Info("程序已经打开");
                    Environment.Exit(0);
                    return;
                }
            }

            Rbac.ApplicationUsageTracker.StartSession();

            CopilotMcpServer.Instance.ApplyConfig();
            LanRemoteControlService.Instance.ApplyConfig();

            if (!Debugger.IsAttached)
            {
                //杀死僵尸进程
                KillZombieProcesses();
            }

            log.Info($"程序打开{Assembly.GetExecutingAssembly().GetName().Version}");

            bool shouldLoadPlugins = false;

            if (StartupRegistryChecker.CheckAndSet())
            {
                shouldLoadPlugins = true;
            }
            else
            {
                var result = MessageBox.Show(ColorVision.Properties.Resources.PluginLoadFailedPrompt, "ColorVision", MessageBoxButton.YesNo);
                if (result == MessageBoxResult.No)
                {
                    shouldLoadPlugins = true;
                }
            }

            if (shouldLoadPlugins)
            {
                PluginLoader.LoadPlugins(_moduleCatalog);
                ColorVision.Copilot.CopilotPluginSubagentRoleLoader.Shared.Synchronize(
                    PluginLoader.Config.Plugins.Values,
                    ColorVision.Copilot.CopilotConfig.Instance.DisabledPluginSubagentRoles);
            }
            else
                ColorVision.Copilot.CopilotPluginSubagentRoleLoader.Shared.Synchronize(Array.Empty<PluginInfo>());

            _moduleCatalog.Seal();

            //这里的代码是因为WPF中引用了WinForm的控件，所以需要先初始化
            System.Windows.Forms.Application.EnableVisualStyles();
            System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);

            //这里显示托盘控件
            //TrayIconManager.GetInstance();


            //代码先进入启动窗口

            if (!WizardWindowConfig.Instance.WizardCompletionKey)
            {
                WizardWindow wizardWindow = new WizardWindow();
                wizardWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                wizardWindow.Show();
            }
            else 
            {
                ///正常进入窗口
                StartWindow StartWindow = new StartWindow();
                StartWindow.Show();
            }
        }

        /// <summary>
        /// Application DelayClose
        /// </summary>
        private void Application_Exit(object sender, ExitEventArgs e)
        {
            Stopwatch exitStopwatch = Stopwatch.StartNew();
            log.Info("Application exit cleanup started.");
            Rbac.ApplicationUsageTracker.StopSession();
            log.Info(ColorVision.Properties.Resources.ApplicationExit);
            bool updateIsActive = Update.ExitUpdateHandoff.IsUpdateActive(AppDomain.CurrentDomain.BaseDirectory);
            if (!_isSessionEnding && !updateIsActive)
                Update.CombinedUpdateCoordinator.TryApplyPrefetchedUpdateOnExit();
            else if (updateIsActive)
                log.Info("Skipped exit-time prefetched update because an external update is already active.");
            ColorVision.Copilot.CopilotPluginSubagentRoleLoader.Shared.Dispose();
            CopilotMcpServer.Instance.Stop();
            LanRemoteControlService.Instance.Stop();
            //正常结束时清除标志位
            StartupRegistryChecker.Clear();
            log.Info($"Application exit cleanup completed in {exitStopwatch.ElapsedMilliseconds} ms.");
            //Environment.Exit(0);
        }
    }
}
