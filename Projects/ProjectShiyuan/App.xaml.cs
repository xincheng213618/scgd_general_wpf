using ColorVision.Engine.MQTT;
using ColorVision.Engine.MySql;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.RC;
using ColorVision.Engine.Templates;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Authorizations;
using ColorVision.UI.Languages;
using System.Windows;

namespace ColorVision.Projects.ProjectShiYuan
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
            Authorization.Instance = ConfigHandler.GetInstance().GetRequiredService<Authorization>();

            this.ApplyTheme(ThemeManager.Current.AppsTheme);
            Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo(LanguageConfig.Instance.UICulture);

            MySqlControl.GetInstance().Connect();
            MQTTControl.GetInstance().MQTTConnectChanged += async (s, e) =>
            {
                await MqttRCService.GetInstance().Connect();
            };
            Task.Run(() => MQTTControl.GetInstance().Connect());
            ServiceManager.GetInstance().GenDeviceDisplayControl();
            TemplateControl.GetInstance();

            ShiyuanProjectWindow window = new ShiyuanProjectWindow();
            window.Show();
        }
    }

}
