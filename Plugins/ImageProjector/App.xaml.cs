using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Languages;
using System.Windows;

namespace ImageProjector
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            ConfigHandler.GetInstance();

            this.ApplyTheme(ThemeConfig.Instance.Theme);
            Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo(LanguageConfig.Instance.UICulture);

            ImageProjectorWindow imageProjectorWindow = new ImageProjectorWindow();
            imageProjectorWindow.Show();
        }
    }

}
