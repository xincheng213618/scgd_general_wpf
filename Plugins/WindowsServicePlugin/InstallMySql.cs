using ColorVision.Common.Utilities;
using ColorVision.UI;
using System.IO;
using System.Windows;

namespace WindowsServicePlugin
{
    public class InstallMySql : WizardStepBase
    {

        public override int Order => 99;
        public override string Header => "DownLoadMySql";

        public override string Description => "Download mysql-5.7.37-winx64.ziplocally to install later through management tools";

        private string url = "http://xc213618.ddns.me:9999/D%3A/ColorVision/Tool/Mysql/mysql-5.7.37-winx64.zip";
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
                    try
                    {
                        PlatformHelper.OpenFolder(Directory.GetParent(filePath).FullName);
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
