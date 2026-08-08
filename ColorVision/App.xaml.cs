using ColorVision.Common.MVVM;
using ColorVision.Copilot.Mcp;
using ColorVision.Core;
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
using System.Linq;
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
        public bool IsMute
        {
            get => _IsMute;
            set
            {
                if (_IsMute == value)
                    return;

                _IsMute = value;
                OnPropertyChanged();
            }
        }
        private bool _IsMute = true;
    }

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private bool _isSessionEnding;
        private bool _isSingleInstanceReplacement;
        private ModuleCatalog? _moduleCatalog;
        private bool _ownsSingleInstanceMutex;
        private SingleInstanceRuntimeCoordinator? _singleInstanceRuntimeCoordinator;

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
            ConfigHandler configHandler = ConfigHandler.GetInstance();
            configHandler.IsAutoSave = false;
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

            string executablePath = Environment.ProcessPath
                ?? throw new InvalidOperationException("Unable to resolve the current ColorVision executable path.");
            mutex = new Mutex(true, SingleInstanceMutexName.Create(executablePath), out bool ownsMutex);
            _ownsSingleInstanceMutex = ownsMutex;
            APPConfig appConfig = configHandler.GetRequiredService<APPConfig>();
            _ = new SingleInstanceReplacementListener(
                Environment.ProcessId,
                TryCloseSingleInstanceReplacement,
                FinalizeSingleInstanceReplacementShutdown);
            bool enableAutoSave = true;
            bool allowMultipleInstances = appConfig.IsMute;
            if (SingleInstanceStartupPolicy.Decide(
                Debugger.IsAttached,
                allowMultipleInstances) == SingleInstanceStartupAction.ReplaceEarlierInstances)
            {
                int closedInstanceCount;
                try
                {
                    closedInstanceCount = Update.ApplicationUpdateProcessCoordinator.CloseEarlierApplicationProcesses(
                        SingleInstanceReplacementListener.TryRequestShutdown);
                    if (!TryAcquireSingleInstanceMutex())
                        throw new InvalidOperationException("Unable to acquire the single-instance mutex after closing earlier instances.");
                }
                catch (Exception ex)
                {
                    log.Error("Unable to replace the earlier ColorVision instance.", ex);
                    Environment.Exit(-1);
                    return;
                }

                if (Update.ExitUpdateHandoff.TryDeferLaunchForActiveUpdate(AppDomain.CurrentDomain.BaseDirectory))
                {
                    Environment.Exit(0);
                    return;
                }

                if (closedInstanceCount > 0 || !ownsMutex)
                {
                    try
                    {
                        configHandler.ReloadFromDisk();
                        appConfig = configHandler.GetRequiredService<APPConfig>();
                        appConfig.IsMute = false;
                        configHandler.Save<APPConfig>();
                        ((log4net.Repository.Hierarchy.Hierarchy)log4net.LogManager.GetRepository()).Root.Level = LogConfig.Instance.LogLevel;
                        this.ApplyTheme(ThemeConfig.Instance.Theme);
                        Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo(LanguageConfig.Instance.UICulture);
                    }
                    catch (Exception ex)
                    {
                        enableAutoSave = false;
                        log.Error(
                            "The earlier ColorVision instance exited, but its final configuration could not be fully reloaded. " +
                            "Automatic configuration saving remains disabled.",
                            ex);
                        try
                        {
                            appConfig = configHandler.GetRequiredService<APPConfig>();
                            appConfig.IsMute = false;
                        }
                        catch (Exception recoveryException)
                        {
                            log.Error("Unable to restore the single-instance configuration after the replacement handoff.", recoveryException);
                        }
                    }
                }

                log.Info(
                    $"Multiple-instance mode is disabled. Closed {closedInstanceCount} earlier " +
                    "ColorVision instance(s) from the current installation.");
            }

            configHandler.IsAutoSave = enableAutoSave;

            _singleInstanceRuntimeCoordinator = new SingleInstanceRuntimeCoordinator(
                () => Task.Run(Update.ApplicationUpdateProcessCoordinator.CloseOtherApplicationProcesses),
                TryAcquireSingleInstanceMutex,
                () => ConfigHandler.GetInstance().Save<APPConfig>());
            appConfig.PropertyChanged += AppConfig_PropertyChanged;

            Rbac.ApplicationUsageTracker.StartSession();

            CopilotMcpServer.Instance.ApplyConfig();
            LanRemoteControlService.Instance.ApplyConfig();

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
                PluginLoader.LoadPlugins(_moduleCatalog);

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

        private async void AppConfig_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(APPConfig.IsMute)
                || sender is not APPConfig appConfig
                || appConfig.IsMute
                || _isSingleInstanceReplacement
                || _singleInstanceRuntimeCoordinator == null)
            {
                return;
            }

            try
            {
                int? closedInstanceCount = await _singleInstanceRuntimeCoordinator.EnforceSingleInstanceAsync();
                if (closedInstanceCount.HasValue)
                {
                    log.Info(
                        $"Multiple-instance mode disabled. Closed {closedInstanceCount.Value} other " +
                        "ColorVision instance(s) from the current installation.");
                }
            }
            catch (Exception ex)
            {
                log.Error("Unable to disable multiple-instance mode. Restoring the previous setting.", ex);
                if (!appConfig.IsMute)
                {
                    appConfig.IsMute = true;
                    ConfigHandler.GetInstance().Save<APPConfig>();
                }
            }
        }

        private bool TryCloseSingleInstanceReplacement()
        {
            if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
                return true;

            try
            {
                return Dispatcher.Invoke(() =>
                {
                    if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
                        return true;
                    if (_isSingleInstanceReplacement)
                        return true;

                    System.Windows.ShutdownMode previousShutdownMode = ShutdownMode;
                    ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;
                    try
                    {
                        _isSingleInstanceReplacement = true;
                        Window? primaryWindow = Windows.Cast<Window>()
                            .FirstOrDefault(window => ReferenceEquals(window, MainWindow))
                            ?? Windows.Cast<Window>().FirstOrDefault(window => window.IsVisible)
                            ?? Windows.Cast<Window>().FirstOrDefault();
                        if (primaryWindow != null)
                        {
                            bool windowClosed = false;
                            void OnWindowClosed(object? sender, EventArgs e) => windowClosed = true;
                            primaryWindow.Closed += OnWindowClosed;
                            try
                            {
                                primaryWindow.Close();
                            }
                            catch (Exception ex) when (windowClosed)
                            {
                                log.Warn("The earlier ColorVision window closed with a replacement cleanup error.", ex);
                            }
                            finally
                            {
                                primaryWindow.Closed -= OnWindowClosed;
                            }

                            if (!windowClosed)
                            {
                                _isSingleInstanceReplacement = false;
                                ShutdownMode = previousShutdownMode;
                                log.Info("The earlier ColorVision instance declined the replacement shutdown request.");
                                return false;
                            }
                        }

                        try
                        {
                            APPConfig appConfig = ConfigHandler.GetInstance().GetRequiredService<APPConfig>();
                            appConfig.IsMute = false;
                            ConfigHandler.GetInstance().Save<APPConfig>();
                        }
                        catch (Exception ex)
                        {
                            log.Error("The earlier ColorVision instance accepted replacement, but its configuration could not be saved.", ex);
                        }

                        return true;
                    }
                    catch
                    {
                        _isSingleInstanceReplacement = false;
                        ShutdownMode = previousShutdownMode;
                        throw;
                    }
                });
            }
            catch (Exception) when (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
            {
                return true;
            }
            catch (InvalidOperationException ex)
            {
                _isSingleInstanceReplacement = false;
                log.Warn("Unable to close the earlier ColorVision instance for replacement.", ex);
                return false;
            }
        }

        private void FinalizeSingleInstanceReplacementShutdown()
        {
            if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
                return;

            _ = Dispatcher.BeginInvoke(() =>
            {
                if (!Dispatcher.HasShutdownStarted)
                    Shutdown();
            });
        }

        private bool TryAcquireSingleInstanceMutex()
        {
            if (_ownsSingleInstanceMutex)
                return true;

            try
            {
                _ownsSingleInstanceMutex = mutex?.WaitOne(0) == true;
            }
            catch (AbandonedMutexException)
            {
                _ownsSingleInstanceMutex = true;
            }

            return _ownsSingleInstanceMutex;
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
            bool replacementIsActive = _isSingleInstanceReplacement
                || Update.ApplicationUpdateProcessCoordinator.IsSingleInstanceReplacementRequested(Environment.ProcessId);
            if (!_isSessionEnding && !replacementIsActive && !updateIsActive)
                Update.CombinedUpdateCoordinator.TryApplyPrefetchedUpdateOnExit();
            else if (replacementIsActive)
                log.Info("Skipped exit-time prefetched update because a newer ColorVision instance is taking over.");
            else if (updateIsActive)
                log.Info("Skipped exit-time prefetched update because an external update is already active.");
            CopilotMcpServer.Instance.Stop();
            LanRemoteControlService.Instance.Stop();
            //正常结束时清除标志位
            StartupRegistryChecker.Clear();
            NativeLogBridge.Shutdown();
            log.Info($"Application exit cleanup completed in {exitStopwatch.ElapsedMilliseconds} ms.");
            //Environment.Exit(0);
        }
    }
}
