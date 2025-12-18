using ColorVision.Database;
using ColorVision.Engine.Services.PhyCameras.Dao;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using ColorVision.UI.Authorizations;
using ColorVision.UI.Menus;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Engine.ToolPlugins
{
    public class CalibrationConfig : IConfig
    {
        public static CalibrationConfig Instance => ConfigService.Instance.GetRequiredService<CalibrationConfig>();

        public string CalibToolsPath { get => _CalibToolsPath; set => _CalibToolsPath = value; }
        private string _CalibToolsPath = string.Empty;
    }


    public class ExporCalibrationCorrection : MenuItemBase
    {
        public override string OwnerGuid => "Tool";

        public override string GuidId => "CalibrationCorrection";

        public override int Order => 6;

        public override string Header => Properties.Resources.CalibrationCorrection;


        public DownloadFile DownloadFile { get; set; } = new DownloadFile();
        public ExporCalibrationCorrection()
        {
            DownloadFile = new DownloadFile();
            DownloadFile.DownloadTile = ColorVision.Engine.Properties.Resources.DownloadCalibrationTool+"240906";
        }

        private string url = "http://xc213618.ddns.me:9999/D%3A/ColorVision/Tool/CalibTool/generateCaliFileTool240906.zip";
        private string downloadPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\" + @"ColorVision\\generateCaliFileTool240906.zip";


        [RequiresPermission(PermissionMode.User)]
        public override void Execute()
        {
            if (!File.Exists(CalibrationConfig.Instance.CalibToolsPath))
            {
                if (MessageBox.Show(ColorVision.Engine.Properties.Resources.CalibrationToolNotFound_DownloadPrompt,"ColorVision",MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    WindowUpdate windowUpdate = new WindowUpdate(DownloadFile);
                    if (!File.Exists(downloadPath))
                    {
                        windowUpdate.Show();
                    }

                    Task.Run(async () =>
                    {
                        if (!File.Exists(downloadPath))
                        {
                            CancellationTokenSource _cancellationTokenSource = new();
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                windowUpdate.Show();
                            });
                            await DownloadFile.Download(url, downloadPath, _cancellationTokenSource.Token);
                        }
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            windowUpdate.Close();
                        });


                        Application.Current.Dispatcher.Invoke(() =>
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

                                ZipFile.ExtractToDirectory(downloadPath, folderBrowser.SelectedPath + "\\generateCaliFileTool240906", true);

                                CalibrationConfig.Instance.CalibToolsPath = folderBrowser.SelectedPath + "\\generateCaliFileTool240906\\CalibTools.exe";
                            }

                            // 启动新的实例
                            ProcessStartInfo startInfo = new();
                            startInfo.UseShellExecute = true; // 必须为true才能使用Verb属性
                            startInfo.WorkingDirectory = Environment.CurrentDirectory;
                            startInfo.FileName = CalibrationConfig.Instance.CalibToolsPath;
                            startInfo.Verb = "runas"; // "runas"指定启动程序时请求管理员权限
                                                      // 如果需要静默安装，添加静默安装参数
                                                      //quiet 没法自启，桌面图标也是空                       
                                                      //startInfo.Arguments = "/quiet";

                            try
                            {
                                Process p = Process.Start(startInfo);
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show(ex.ToString());
                                File.Delete(downloadPath);
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
