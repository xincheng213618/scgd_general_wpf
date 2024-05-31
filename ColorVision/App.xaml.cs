using ColorVision.Engine.MySql;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Languages;
using ColorVision.Wizards;
using log4net;
using log4net.Config;
using System;
using System.Collections.Generic;
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
                StartWindow StartWindow = new StartWindow();
                StartWindow.Show();
            }
            else
            {
                MySqlControl.GetInstance().Connect();
                var _IComponentInitializers = new List<UI.IInitializer>();
                MessageUpdater messageUpdater = new MessageUpdater();
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    foreach (Type type in assembly.GetTypes().Where(t => typeof(IInitializer).IsAssignableFrom(t) && !t.IsAbstract))
                    {
                        if (Activator.CreateInstance(type, messageUpdater) is IInitializer componentInitialize)
                        {
                            _IComponentInitializers.Add(componentInitialize);
                        }
                    }
                }
                _IComponentInitializers = _IComponentInitializers.OrderBy(handler => handler.Order).ToList();

                Task.Run( async () =>
                {
                    foreach (var item in _IComponentInitializers)
                    {
                        await item.InitializeAsync();
                    }
                });


                MainWindow MainWindow = new MainWindow();
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
