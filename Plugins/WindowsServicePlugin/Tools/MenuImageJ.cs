using ColorVision.Common.MVVM;
using ColorVision.Engine.Media;
using ColorVision.ImageEditor;
using ColorVision.FileIO;
using ColorVision.UI;
using ColorVision.UI.Menus;
using log4net;
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

        [DisplayName("ImageJ path"),PropertyEditorType(typeof(TextSelectFilePropertiesEditor))]
        public string ImageJPath { get => _ImageJPath; set { _ImageJPath = value; } }
        private string _ImageJPath = string.Empty;

        [DisplayName("BeyondCompare path"), PropertyEditorType(typeof(TextSelectFilePropertiesEditor))]
        public string BeyondComparePath { get => _BeyondComparePath; set { _BeyondComparePath = value; } }
        private string _BeyondComparePath = string.Empty;


        public IEnumerable<ConfigSettingMetadata> GetConfigSettings()
        {
            return new List<ConfigSettingMetadata>()
            {
                new ConfigSettingMetadata()
                {
                    Name = "Thrid Party",
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



    public record class ImageViewExTension(EditorContext EditorContext) : IIEditorToolContextMenu
    {

        public List<MenuItemMetadata> GetContextMenuItems()
        {
            List<MenuItemMetadata> values = new List<MenuItemMetadata>();

            if (!File.Exists(ImageJConfig.Instance.ImageJPath)) return values;

            RelayCommand relayCommand = new RelayCommand(a =>
            {
                string shortFilePath = string.Empty;
                if (CVFileUtil.IsCIEFile(EditorContext.Config.FilePath))
                {
                    VExportCIE vExportCIE = new VExportCIE(EditorContext.Config.FilePath);
                    VExportCIE.SaveToTif(vExportCIE);
                    shortFilePath = PathHelper.GetShortPath(vExportCIE.CoverFilePath);
                }
                else
                {
                    shortFilePath = PathHelper.GetShortPath(EditorContext.Config.FilePath);
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

            if (File.Exists(EditorContext.Config.FilePath))
            {
                MenuItemMetadata menuItemMetadata = new MenuItemMetadata() { GuidId = "ImageJ", Order = 500, Header = "通过ImageJ打开", Command = relayCommand };
                values.Add(menuItemMetadata);
            }
            return values;
        }
    }






    public class MenuImageJ : MenuItemBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MenuImageJ));
        public override string OwnerGuid => MenuItemConstants.View;
        public override int Order => 99;
        public override string Header => "Open ImageJ";

        public static string LatestReleaseUrl => "http://xc213618.ddns.me:9999/D%3A/ColorVision/Tool/ImageJ/LATEST_RELEASE";

        public override void Execute()
        {
            if (!File.Exists(ImageJConfig.Instance.ImageJPath))
            {
                if (MessageBox.Show(Application.Current.GetActiveWindow(), "Cannot Found ImageJ，Download?", "ColorVision", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    string filename = "ij154-win-java8.zip";
                    string url = $"http://xc213618.ddns.me:9999/D%3A/ColorVision/Tool/ImageJ/{filename}";
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

                            ImageJConfig.Instance.ImageJPath = Path.Combine(path, "ImageJ", "ImageJ.exe");

                            ProcessStartInfo startInfo = new()
                            {
                                UseShellExecute = true,
                                WorkingDirectory = Environment.CurrentDirectory,
                                FileName = ImageJConfig.Instance.ImageJPath
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
