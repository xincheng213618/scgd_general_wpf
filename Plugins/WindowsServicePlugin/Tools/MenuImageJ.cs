#pragma warning disable SYSLIB0014
using ColorVision.Common.MVVM;
using ColorVision.Engine.Media;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using ColorVision.Net;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using ColorVision.UI.Menus;
using log4net;
using Microsoft.Extensions.Configuration.UserSecrets;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Windows;


namespace WindowsServicePlugin.Tools
{
    public class ImageJConfig : ViewModelBase, IConfig, IConfigSettingProvider
    {
        public static ImageJConfig Instance => ConfigService.Instance.GetRequiredService<ImageJConfig>();

        [DisplayName("ImageJ路径"),PropertyEditorType(PropertyEditorType.TextSelectFile)]
        public string ImageJPath { get => _ImageJPath; set { _ImageJPath = value; } }
        private string _ImageJPath = string.Empty;

        [DisplayName("BeyondCompare路径"), PropertyEditorType(PropertyEditorType.TextSelectFile)]
        public string BeyondComparePath { get => _BeyondComparePath; set { _BeyondComparePath = value; } }
        private string _BeyondComparePath = string.Empty;


        public IEnumerable<ConfigSettingMetadata> GetConfigSettings()
        {
            return new List<ConfigSettingMetadata>()
            {
                new ConfigSettingMetadata()
                {
                    Name = "第三方",
                    Description = "ImageJ",
                    Type = ConfigSettingType.Class,
                    Source = Instance
                }
            };
        }
    }
    public static class PathHelper
    {
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern int GetShortPathName(
            string lpszLongPath,
            System.Text.StringBuilder lpszShortPath,
            int cchBuffer);

        public static string GetShortPath(string longPath)
        {
            var shortPath = new System.Text.StringBuilder(260);
            GetShortPathName(longPath, shortPath, shortPath.Capacity);
            return shortPath.ToString();
        }
    }



    public class ImageViewExTension: IImageContentMenu
    {


        public List<MenuItemMetadata> GetContextMenuItems(ImageViewConfig config)
        {
            if (!File.Exists(ImageJConfig.Instance.ImageJPath)) return new List<MenuItemMetadata>();
            RelayCommand relayCommand = new RelayCommand(a =>
            {
                string shortFilePath = string.Empty;
                if (CVFileUtil.IsCIEFile(config.FilePath))
                {
                    VExportCIE vExportCIE = new VExportCIE(config.FilePath);
                    VExportCIE.SaveToTif(vExportCIE);
                    shortFilePath = PathHelper.GetShortPath(vExportCIE.CoverFilePath);
                }
                else
                {
                    shortFilePath = PathHelper.GetShortPath(config.FilePath);
                }

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = ImageJConfig.Instance.ImageJPath,
                    Arguments = $"\"{shortFilePath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                Process.Start(startInfo);
            });
            MenuItemMetadata menuItemMetadata = new MenuItemMetadata() { GuidId = "ImageJ", Order = 500, Header = "通过ImageJ打开", Command = relayCommand };
            return new List<MenuItemMetadata>() { menuItemMetadata };
        }
    }






    public class MenuImageJ : MenuItemBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MenuImageJ));
        public override string OwnerGuid => MenuItemConstants.View;
        public override int Order => 99;
        public override string Header => "打开ImageJ";
        public DownloadFile DownloadFile { get; set; } = new DownloadFile();

        public static string LatestReleaseUrl => "http://xc213618.ddns.me:9999/D%3A/ColorVision/Tool/ImageJ/LATEST_RELEASE";
        private string downloadPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\" + @"ColorVision\\";

        public MenuImageJ()
        {
            DownloadFile = new DownloadFile();
            DownloadFile.DownloadTile = "下载ImageJ";
        }
        public override void Execute()
        {
            if (!File.Exists(ImageJConfig.Instance.ImageJPath))
            {
                if (MessageBox.Show(Application.Current.GetActiveWindow(), "找不到ImageJ，是否下载", "ColorVision", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    string filename = "ij154-win-java8.zip";
                    string url = $"http://xc213618.ddns.me:9999/D%3A/ColorVision/Tool/ImageJ/{filename}";
                    downloadPath = Path.Combine(downloadPath, filename);
                    Task.Run(() =>
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            WindowUpdate windowUpdate = new WindowUpdate(DownloadFile) {  Owner =Application.Current.GetActiveWindow(),WindowStartupLocation =WindowStartupLocation.CenterOwner};
                            if (!File.Exists(downloadPath))
                            {
                                windowUpdate.Show();
                            }
                            Task.Run(async () =>
                            {
                                if (!File.Exists(downloadPath))
                                {
                                    await DownloadFile.GetIsPassWorld();
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
                                    string path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\" + @"ColorVision\\";
                                    ZipFile.ExtractToDirectory(downloadPath, path, true);

                                    ImageJConfig.Instance.ImageJPath = path + "\\ImageJ\\ImageJ.exe";

                                    // 启动新的实例
                                    ProcessStartInfo startInfo = new();
                                    startInfo.UseShellExecute = true; // 必须为true才能使用Verb属性
                                    startInfo.WorkingDirectory = Environment.CurrentDirectory;
                                    startInfo.FileName = ImageJConfig.Instance.ImageJPath;

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
                        });


                    });
                    return;
                }

                if (MessageBox.Show(Application.Current.GetActiveWindow(), "I can't find ImageJ (ImageJ.exe). Would you like to help me find it?", "Open in ImageJ", MessageBoxButton.YesNo) == MessageBoxResult.No) return;
                using (System.Windows.Forms.OpenFileDialog openFileDialog = new())
                {
                    openFileDialog.Title = "Select ImageJ.exe";
                    openFileDialog.Filter = "ImageJ.exe|ImageJ.exe";
                    openFileDialog.RestoreDirectory = true;

                    if (openFileDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                    ImageJConfig.Instance.ImageJPath = openFileDialog.FileName;
                }
            }

            ProcessStartInfo startInfo = new();
            startInfo.UseShellExecute = true; // 必须为true才能使用Verb属性
            startInfo.WorkingDirectory = Environment.CurrentDirectory;
            startInfo.FileName = ImageJConfig.Instance.ImageJPath;
            //startInfo.Verb = "runas"; // "runas"指定启动程序时请求管理员权限
            //                          // 如果需要静默安装，添加静默安装参数
            //                          //quiet 没法自启，桌面图标也是空                       
            //                          //startInfo.Arguments = "/quiet";
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
