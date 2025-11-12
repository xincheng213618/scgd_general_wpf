using ProjectStarKey;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Authorizations;
using ColorVision.UI.Languages;
using System.Reflection;
using System.Threading;
using System.Windows;

namespace ProjectStarKey
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
            MVSViewWindow basicDemoWindow = new MVSViewWindow();
            basicDemoWindow.Show();


        }
    }

}
