using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Authorizations;
using ColorVision.UI.Languages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;

namespace ProjectStarkSemi
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
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            ConfigHandler.GetInstance();

            Authorization.Instance = ConfigHandler.GetInstance().GetRequiredService<Authorization>();

            LogConfig.Instance.SetLog();
            this.ApplyTheme(ThemeManager.Current.AppsTheme);
            Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo(LanguageConfig.Instance.UICulture);

            Assembly.LoadFrom("ColorVision.Engine.dll"); ;


            ConoscopeWindow conoscopeWindow = new ConoscopeWindow();
            conoscopeWindow.Show();


        }
    }

}
