using ColorVision.Engine.Services.PhyCameras;
using ColorVision.UI;
using log4net;
using Newtonsoft.Json.Linq;
using System;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Engine
{

    public static class CVXFileProcess
    {
        public static string FilePath { get; set; } 
    }

    public class CVCalInitialized : MainWindowInitializedBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(CVCalInitialized));

        public CVCalInitialized()
        {
            Order = -1;
        }

        public override Task Initialize()
        {
            if (string.IsNullOrWhiteSpace(CVXFileProcess.FilePath)) return Task.CompletedTask;
            if (!File.Exists(CVXFileProcess.FilePath)) return Task.CompletedTask;
            if (string.IsNullOrWhiteSpace(CVXFileProcess.FilePath)) return Task.CompletedTask;
            if (!File.Exists(CVXFileProcess.FilePath)) return Task.CompletedTask;

            string targetFile = CVXFileProcess.FilePath;

            // 切换到 UI 线程执行窗口操作
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    string jsonContent = null;

                    // 1. 读取 Zip 中的 json 文件
                    using (ZipArchive archive = ZipFile.OpenRead(targetFile))
                    {
                        var entry = archive.GetEntry("cvCameraInfo.json");
                        if (entry != null)
                        {
                            using (var stream = entry.Open())
                            using (var reader = new StreamReader(stream, Encoding.UTF8))
                            {
                                jsonContent = reader.ReadToEnd();
                            }
                        }
                    }

                    if (string.IsNullOrEmpty(jsonContent))
                    {
                        MessageBox.Show("无法在文件中找到 cvCameraInfo.json", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // 2. 弹出自定义确认窗口
                    CVFileImportWindow importWindow = new CVFileImportWindow(jsonContent);
                    bool? result = importWindow.ShowDialog();

                    // 3. 如果用户点击"导入相机" (DialogResult == true)
                    if (result == true)
                    {
                        // TODO: 在这里执行实际的导入逻辑，例如将配置反序列化并保存到系统配置中
                        // ProcessCameraImport(jsonContent); 

                        // 打开相机管理器窗口
                        PhyCameraManagerWindow phyCameraManagerWindow = new PhyCameraManagerWindow();
                        phyCameraManagerWindow.Show();
                    }
                }
                catch (Exception ex)
                {
                    log.Error($"处理 .cvcal 文件出错: {ex.Message}");
                    MessageBox.Show($"文件处理出错: {ex.Message}", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    // 处理完毕后清空路径，防止重复触发
                    CVXFileProcess.FilePath = null;
                }
            });

            return Task.CompletedTask;
        }
    }

    [FileExtension(".cvcal")]
    public class CVCalFileProcess : IFileProcessor
    {
        public int Order => 1;

        public void Export(string filePath)
        {

        }
        public bool Process(string filePath)
        {
            CVXFileProcess.FilePath = filePath;
            return false;
        }
    }
}
