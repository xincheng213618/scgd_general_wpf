using ColorVision.Themes;
using ColorVision.UI;
using System.Windows;

namespace CameraTest;

public partial class App : System.Windows.Application
{
    private async void Application_Startup(object sender, StartupEventArgs e)
    {
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        ConfigHandler.GetInstance("CameraTestConfig");
        this.ApplyTheme(ThemeConfig.Instance.Theme);
        // Deliberately do not discover/run Engine IInitializers: no database, MQTT or service startup.
        var window = new CameraTestWindow();
        MainWindow = window;
        window.Show();
        if (e.Args.Length == 1) await window.OpenImageAsync(e.Args[0]);
    }
}
