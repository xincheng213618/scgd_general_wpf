using ColorVision.Engine.MQTT;
using ColorVision.Engine.MySql;
using ColorVision.Services;
using ColorVision.Themes;
using ColorVision.UI.Languages;
using ColorVision.UI;
using System.Configuration;
using System.Data;
using System.Threading.Tasks;
using System.Threading;
using System.Windows;
using ColorVision.Services.RC;
using ColorVision.Engine.Templates;

namespace ColorVision.Engine
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
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
        }
    }

}
