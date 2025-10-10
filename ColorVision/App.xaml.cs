using ColorVision.Common.NativeMethods;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Languages;
using ColorVision.UI.Plugins;
using ColorVision.UI.Shell;
using ColorVision.Wizards;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision
{

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            Startup += Application_Startup;
            Exit += Application_Exit;
            #if(DEBUG == false)
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
            bool IsDebug = Debugger.IsAttached;
            var parser = ArgumentParser.GetInstance();

            parser.AddArgument("debug", true, "d");
            parser.AddArgument("restart", true, "r");
            parser.Parse();

            IsDebug = Debugger.IsAttached || parser.GetFlag("debug");

            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);

            //加载DLL
            if (File.Exists("ColorVision.Engine.dll"))
                Assembly.LoadFrom("ColorVision.Engine.dll"); ;
            

            ConfigHandler.GetInstance();
            ConfigHandler.GetInstance().IsAutoSave = false;
            LogConfig.Instance.SetLog();
            this.ApplyTheme(ThemeConfig.Instance.Theme);
            Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo(LanguageConfig.Instance.UICulture);
            //Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("en");
            //Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("ja");

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); // 确保 .NET Core 及以上支持 GBK
            parser.AddArgument("input", false, "i");
            parser.AddArgument("export", false, "e");

            parser.Parse();
            string exportFile = parser.GetValue("export");
            if (exportFile != null)
            {
                bool isok = FileProcessorFactory.GetInstance().ExportFile(exportFile);
                ProgramTimer.StopAndReport();
                if (isok)
                {
                    return;
                }
                else
                {
                    MessageBox.Show("不支持的文件格式");
                    Environment.Exit(0);
                    return;
                }
            }
            string inputFile = parser.GetValue("input");
            if (inputFile != null)
            {
                bool isok = FileProcessorFactory.GetInstance().HandleFile(inputFile);
                if (isok)
                {
                    ProgramTimer.StopAndReport();
                    return;
                }
                else
                {

                }
            }

            ConfigHandler.GetInstance().IsAutoSave = true;
            //单独处理文件的进程不需要关闭当前进程
            mutex = new Mutex(true, "ColorVision", out bool ret);
            if (!ret && !Debugger.IsAttached)
            {
                IntPtr hWnd = CheckAppRunning.Check("ColorVision");
                if (hWnd != IntPtr.Zero)
                {
                    if (ArgumentParser.GetInstance().CommandLineArgs.Length > 0)
                    {
                        char separator = '\u0001';
                        string combinedArgs = string.Join(separator.ToString(), ArgumentParser.GetInstance().CommandLineArgs);
                        ushort atom = GlobalAddAtom(combinedArgs);
                        //这里反了，不过没必要改了，都一样
                        SendMessage(hWnd, WM_USER + 1, IntPtr.Zero, (IntPtr)atom);  // 发送消息
                    }
                    log.Info("程序已经打开");
                    Environment.Exit(0);
                }
                ////写在这里可以Avoid命令行多开的效果，但是没有办法检测版本，实现同版本的情况下更新条件唯一
                //Environment.Exit(0);
            }

            if (!Debugger.IsAttached)
            {
                //杀死僵尸进程
                KillZombieProcesses();
            }

            log.Info($"程序打开{Assembly.GetExecutingAssembly().GetName().Version}");



            log.Info(UI.ACE.License.GetMachineCode());
            if (!UI.ACE.License.Check())
            {
#if DEBUG
                log.Info("开发模式：检测不到许可证，但允许继续运行");
                // 在开发环境中允许无许可证运行
#else
                log.Warn("未找到有效许可证");
                // 生产环境中提示用户获取许可证
                // 注意：出于安全考虑，自动许可证生成功能已移除
                // 请使用专门的许可证生成工具创建许可证
#endif
            }
            else
            {
                log.Info("许可证验证通过");
            }
            bool shouldLoadPlugins = false;

            if (StartupRegistryChecker.CheckAndSet())
            {
                shouldLoadPlugins = true;
            }
            else
            {
                var result = MessageBox.Show("检测到软件上次没有成功打开，是否禁用插件", "ColorVision", MessageBoxButton.YesNo);
                if (result == MessageBoxResult.No)
                {
                    shouldLoadPlugins = true;
                }
            }

            if (shouldLoadPlugins)
            {
                PluginLoader.LoadPlugins();
            }

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
            log.Info(ColorVision.Properties.Resources.ApplicationExit);
            //正常结束时清除标志位
            StartupRegistryChecker.Clear();
            //Environment.Exit(0);
        }
    }
}
