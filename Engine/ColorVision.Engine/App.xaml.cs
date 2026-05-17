using ColorVision.Database;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.RC;
using ColorVision.Engine.Templates;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Languages;
using ColorVision.UI.Plugins;
using log4net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Engine
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(App));

        private async void Application_Startup(object sender, StartupEventArgs e)
        {
            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
            ConfigHandler.GetInstance("ColorVisionConfig");
            LogConfig.Instance.SetLog();

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            System.Windows.Forms.Application.EnableVisualStyles();
            System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);

            this.ApplyTheme(ThemeConfig.Instance.Theme);
            Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo(LanguageConfig.Instance.UICulture);

            PluginLoader.LoadPlugins("Plugins");

            await InitializeStandaloneEngineAsync();

            MainWindow mainWindow = new MainWindow();
            mainWindow.Show();
        }

        private static async Task InitializeStandaloneEngineAsync()
        {
            foreach (var initializer in GetStandaloneInitializers())
            {
                try
                {
                    log.Info($"Standalone initializer {initializer.Name}");
                    await initializer.InitializeAsync();
                }
                catch (Exception ex)
                {
                    log.Error($"Standalone initializer failed: {initializer.Name}", ex);
                }
            }
        }

        private static List<IInitializer> GetStandaloneInitializers()
        {
            return GetLoadableTypes(typeof(App).Assembly)
                .Where(type => typeof(IInitializer).IsAssignableFrom(type)
                               && !type.IsAbstract
                               && !type.IsInterface
                               && !type.ContainsGenericParameters)
                .Select(type =>
                {
                    try
                    {
                        return Activator.CreateInstance(type) as IInitializer;
                    }
                    catch (Exception ex)
                    {
                        log.Error($"Create standalone initializer failed: {type.FullName}", ex);
                        return null;
                    }
                })
                .Where(initializer => initializer != null)
                .OrderBy(initializer => initializer!.Order)
                .ThenBy(initializer => initializer!.Name, StringComparer.Ordinal)
                .Cast<IInitializer>()
                .ToList();
        }

        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(type => type != null)!;
            }
        }
    }

}
