using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Languages;
using System.Windows;

namespace Pattern
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            ConfigHandler.GetInstance();

            this.ApplyTheme(ThemeManager.Current.AppsTheme);
            Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo(LanguageConfig.Instance.UICulture);

            PatternWindow patternWindow = new PatternWindow();
            patternWindow.Show();
        }
    }

}
