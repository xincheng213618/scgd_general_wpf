using ColorVision.Themes;
using ColorVision.UI.Languages;
using ColorVision.UI;
using System.Configuration;
using System.Data;
using System.Windows;
using ColorVision.MQTT;
using ColorVision.MySql;
using ColorVision.Services.Templates;
using ColorVision.Services;
using ColorVision.Services.RC;
using ColorVision.Projects;

namespace ProjectHeyuan
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

        private void Application_Startup(object s, StartupEventArgs e)
        {
            ConfigHandler.GetInstance();

            this.ApplyTheme(ThemeConfig.Instance.Theme);
            Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo(LanguageConfig.Instance.UICulture);

            MySqlControl.GetInstance().Connect();
            MQTTControl.GetInstance().MQTTConnectChanged += async (s, e) =>
            {
                await MQTTRCService.GetInstance().Connect();
            };
            Task.Run(() => MQTTControl.GetInstance().Connect());
            ServiceManager.GetInstance().GenDeviceDisplayControl();
            TemplateControl.GetInstance();

            ProjectHeyuanWindow projectHeyuanWindow = new ProjectHeyuanWindow();
            projectHeyuanWindow.Show();
        }
    }

}
