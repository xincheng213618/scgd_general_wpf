using ColorVision.UI;
using ColorVision.UI.Menus;
using log4net;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Windows;

namespace WindowsServicePlugin.Tools
{
    public class MenuBeyondCompare : MenuItemBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MenuBeyondCompare));
        public override string OwnerGuid => MenuItemConstants.View;
        public override int Order => 99;

        public override string Header =>Properties.Resources.OpenBeyondCompare;

        public static string LatestReleaseUrl => "http://xc213618.ddns.me:9999/D%3A/ColorVision/Tool/BeyondCompare/LATEST_RELEASE";

        public override void Execute()
        {
            if (!File.Exists(ImageJConfig.Instance.BeyondComparePath))
            {
                if (MessageBox.Show(Application.Current.GetActiveWindow(), Properties.Resources.BCompareNotFound_DownloadPrompt, "ColorVision", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    string filename = "Beyond_Compare_5.zip";
                    string url = $"http://xc213618.ddns.me:9999/D%3A/ColorVision/Tool/BeyondCompare/{filename}";
                    string downloadDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ColorVision");

                    var service = AssemblyHandler.GetInstance().LoadImplementations<IDownloadService>().FirstOrDefault();
                    if (service == null) return;

                    service.ShowDownloadWindow();
                    service.Download(url, downloadDir, DownloadFileConfig.Instance.Authorization, filePath =>
                    {
                        if (filePath == null) return;
                        Application.Current?.Dispatcher.Invoke(() =>
                        {
                            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ColorVision");
                            ZipFile.ExtractToDirectory(filePath, path, true);

                            ImageJConfig.Instance.BeyondComparePath = Path.Combine(path, "Beyond Compare 5", "BCompare.exe");

                            ProcessStartInfo startInfo = new()
                            {
                                UseShellExecute = true,
                                WorkingDirectory = Environment.CurrentDirectory,
                                FileName = ImageJConfig.Instance.BeyondComparePath
                            };
                            try
                            {
                                Process.Start(startInfo);
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show(ex.ToString());
                                File.Delete(filePath);
                            }
                        });
                    });
                    return;
                }

                if (MessageBox.Show(Application.Current.GetActiveWindow(), "I can't find BCompare (BCompare.exe). Would you like to help me find it?", "Open in BCompare", MessageBoxButton.YesNo) == MessageBoxResult.No) return;
                using (System.Windows.Forms.OpenFileDialog openFileDialog = new())
                {
                    openFileDialog.Title = "Select BCompare.exe";
                    openFileDialog.Filter = "BCompare.exe|BCompare.exe";
                    openFileDialog.RestoreDirectory = true;

                    if (openFileDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                    ImageJConfig.Instance.BeyondComparePath = openFileDialog.FileName;
                }
            }

            ProcessStartInfo startInfo = new();
            startInfo.UseShellExecute = true;
            startInfo.WorkingDirectory = Environment.CurrentDirectory;
            startInfo.FileName = ImageJConfig.Instance.BeyondComparePath;
            try
            {
                Process p = Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), ex.Message);
            }
        }


    }
}
