using ColorVision.Common.Utilities;
using ColorVision.UI;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace WindowsServicePlugin
{
    public class InstallMQTT :  WizardStepBase
    {
        public override int Order => 99;
        public override string Header => Properties.Resources.InstallMqtt;
        public override string Description => "Install a local MQTT service. If you are using another machine for forwarding, please skip this step";  

        private string url = "http://xc213618.ddns.me:9999/D%3A/ColorVision/Tool/MQTT/mosquitto-2.0.18-install-windows-x64.exe";
        private string downloadDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ColorVision");

        public override void Execute()
        {
            var service = AssemblyHandler.GetInstance().LoadImplementations<IDownloadService>().FirstOrDefault();
            if (service == null) return;

            service.ShowDownloadWindow();
            service.Download(url, downloadDir, DownloadFileConfig.Instance.Authorization, filePath =>
            {
                if (filePath == null) return;
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    ProcessStartInfo startInfo = new()
                    {
                        UseShellExecute = true,
                        WorkingDirectory = Environment.CurrentDirectory,
                        FileName = filePath,
                        Verb = "runas"
                    };
                    try
                    {
                        Process p = Process.Start(startInfo);
                        p?.WaitForExit();
                        Tool.ExecuteCommandAsAdmin("net start mosquitto");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.ToString());
                        File.Delete(filePath);
                    }
                });
            });
        }

    }



}
