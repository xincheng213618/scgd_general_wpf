using ColorVision.Common.ThirdPartyApps;
using ColorVision.Database;
using ColorVision.Engine.Services.PhyCameras.Licenses;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Windows;

namespace ColorVision.Engine.ToolPlugins
{

    public class CalibrationConfig : IConfig
    {
        public static CalibrationConfig Instance => ConfigService.Instance.GetRequiredService<CalibrationConfig>();

        public string CalibToolsPath { get => _CalibToolsPath; set => _CalibToolsPath = value; }
        private string _CalibToolsPath = string.Empty;
    }


    public class ExporCalibrationCorrectionAppProvider : IThirdPartyAppProvider
    {
        private const string Url = "http://xc213618.ddns.me:9999/D%3A/ColorVision/Tool/CalibTool/generateCaliFileTool240906.zip";
        private static readonly string DownloadDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ColorVision");

        public IEnumerable<ThirdPartyAppInfo> GetThirdPartyApps()
        {
            return new List<ThirdPartyAppInfo>
            {
                new ThirdPartyAppInfo
                {
                    Name = Properties.Resources.CalibrationCorrection,
                    Group = Properties.Resources.InternalTools,
                    Order = 6,
                    LaunchAction = ExecuteCalibrationCorrection,
                    GetIconPath = () => CalibrationConfig.Instance.CalibToolsPath,
                }
            };
        }

        private static void ExecuteCalibrationCorrection()
        {
            if (!File.Exists(CalibrationConfig.Instance.CalibToolsPath))
            {
                if (MessageBox.Show(ColorVision.Engine.Properties.Resources.CalibrationToolNotFound_DownloadPrompt,"ColorVision",MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    var service = AssemblyHandler.GetInstance().LoadImplementations<IDownloadService>().FirstOrDefault();
                    if (service == null) return;

                    service.ShowDownloadWindow();
                    service.Download(Url, DownloadDir, DownloadFileConfig.Instance.Authorization, filePath =>
                    {
                        if (filePath == null) return;
                        Application.Current?.Dispatcher.Invoke(() =>
                        {
                            Process.GetProcessesByName("CalibTools").ToList().ForEach(p => p.Kill());
                            using (System.Windows.Forms.FolderBrowserDialog folderBrowser = new System.Windows.Forms.FolderBrowserDialog())
                            {
                                folderBrowser.Description = ColorVision.Engine.Properties.Resources.SelectUpzipDirectry;
                                folderBrowser.ShowNewFolderButton = true;
                                folderBrowser.RootFolder = Environment.SpecialFolder.Desktop;
                                if (folderBrowser.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

                                if (!Directory.Exists(folderBrowser.SelectedPath + "\\generateCaliFileTool240906"))
                                    Directory.CreateDirectory(folderBrowser.SelectedPath + "\\generateCaliFileTool240906");

                                ZipFile.ExtractToDirectory(filePath, folderBrowser.SelectedPath + "\\generateCaliFileTool240906", true);

                                CalibrationConfig.Instance.CalibToolsPath = folderBrowser.SelectedPath + "\\generateCaliFileTool240906\\CalibTools.exe";
                            }

                            ProcessStartInfo startInfo = new()
                            {
                                UseShellExecute = true,
                                WorkingDirectory = Environment.CurrentDirectory,
                                FileName = CalibrationConfig.Instance.CalibToolsPath,
                                Verb = "runas"
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
                if (MessageBox.Show(Application.Current.GetActiveWindow(), Properties.Resources.CannotFindCalibToolsExe_HelpMeFindIt, Properties.Resources.OpenInCalibTools, MessageBoxButton.YesNo) == MessageBoxResult.No) return;
                using (System.Windows.Forms.OpenFileDialog openFileDialog = new())
                {
                    openFileDialog.Title = Properties.Resources.SelectCalibToolsExe;
                    openFileDialog.Filter = "CalibTools.exe|CalibTools.exe";
                    openFileDialog.RestoreDirectory = true;

                    if (openFileDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                    CalibrationConfig.Instance.CalibToolsPath = openFileDialog.FileName;
                }

            }
            try
            {
                var cameraLicenseModels = PhyLicenseDao.Instance.GetAll(); 
                string DirectoryPath = Path.Combine(Directory.GetParent(CalibrationConfig.Instance.CalibToolsPath).FullName, "license");

                if (!Directory.Exists(DirectoryPath))
                    Directory.CreateDirectory(DirectoryPath);

                foreach (var item in cameraLicenseModels)
                {
                    string lincesePath = Path.Combine(DirectoryPath, item.MacAddress + ".lic");
                    File.WriteAllText(lincesePath, item.LicenseValue);
               }
                Process.Start(CalibrationConfig.Instance.CalibToolsPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), ex.Message);
            }
        }
    }
}
