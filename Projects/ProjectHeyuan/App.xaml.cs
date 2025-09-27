using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Authorizations;
using ColorVision.UI.Languages;
using System.Reflection;
using System.Windows;

namespace ColorVision.Projects.ProjectHeyuan
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            Startup += (s, e) => Application_Startup(s, e);
            Exit += new ExitEventHandler(Application_Exit);
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {

        }


        private async void Application_Startup(object s, StartupEventArgs e)
        {
            ConfigHandler.GetInstance();
            Authorization.Instance = ConfigHandler.GetInstance().GetRequiredService<Authorization>();

            LogConfig.Instance.SetLog();
            this.ApplyTheme(ThemeManager.Current.AppsTheme);
            Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo(LanguageConfig.Instance.UICulture);


            Assembly.LoadFrom("ColorVision.Engine.dll"); ;

            var _IComponentInitializers = new List<UI.IInitializer>();
            foreach (var assembly in AssemblyHandler.GetInstance().GetAssemblies())
            {
                foreach (Type type in assembly.GetTypes().Where(t => typeof(IInitializer).IsAssignableFrom(t) && !t.IsAbstract))
                {
                    if (Activator.CreateInstance(type) is IInitializer componentInitialize)
                    {
                        _IComponentInitializers.Add(componentInitialize);
                    }
                }
            }
            _IComponentInitializers = _IComponentInitializers.OrderBy(handler => handler.Order).ToList();

            foreach (var item in _IComponentInitializers)
            {
                await item.InitializeAsync();
            }
            ProjectHeyuanWindow window = new ProjectHeyuanWindow();
            window.Show();
        }
    }

}
