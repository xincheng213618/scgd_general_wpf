using ColorVision.UI;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace WindowsServicePlugin
{
    public class InstallNavicate : WizardStepBase
    {
        public override int Order => 50;
        public override string Header => ColorVision.UI.Properties.Resources.Download +"Navicate";
        public override string Description => "Download Navicat as a third-party client for database management.";

        private string url = "http://xc213618.ddns.me:9999/D%3A/ColorVision/Tool/navicat/navicat161_premium_cs_x64.exe";
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
