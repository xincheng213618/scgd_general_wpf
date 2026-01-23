using ColorVision.Engine.Services.PhyCameras;
using ColorVision.UI;
using log4net;
using Newtonsoft.Json; // 引入 Json
using System;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Engine.CalFile
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

            string targetFile = CVXFileProcess.FilePath;

            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    string jsonContent = null;

                    // 1. 解压读取 JSON 字符串
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
                        MessageBox.Show("配置信息读取为空。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // 2. 反序列化为对象 (关键步骤)
                    CvCameraInfoModel cameraInfo = null;
                    try
                    {
                        cameraInfo = JsonConvert.DeserializeObject<CvCameraInfoModel>(jsonContent);
                    }
                    catch (Exception jsonEx)
                    {
                        MessageBox.Show($"配置文件格式错误: {jsonEx.Message}", "解析错误");
                        return;
                    }

                    // 3. 打开确认窗口，传入对象
                    CVFileImportWindow importWindow = new CVFileImportWindow(cameraInfo);
                    bool? result = importWindow.ShowDialog();

                    // 4. 用户确认导入
                    if (result == true)
                    {
                        // ---------------------------------------------------------
                        // 在这里使用 cameraInfo 对象进行后续处理
                        // ---------------------------------------------------------
                        ImportCameraConfig(cameraInfo);

                        // 打开 PhyCameraManagerWindow
                        PhyCameraManagerWindow phyCameraManagerWindow = new PhyCameraManagerWindow();
                        phyCameraManagerWindow.Show();
                    }
                }
                catch (Exception ex)
                {
                    log.Error($"导入流程异常: {ex.Message}");
                    MessageBox.Show($"处理文件时发生错误: {ex.Message}");
                }
                finally
                {
                    CVXFileProcess.FilePath = null;
                }
            });

            return Task.CompletedTask;
        }

        /// <summary>
        /// 实际的导入逻辑方法
        /// </summary>
        private void ImportCameraConfig(CvCameraInfoModel info)
        {
            // 示例：打印日志或存入数据库
            log.Info($"正在导入相机: {info.CameraModel} (SN: {info.CameraSn})");

            // TODO: 这里写你具体的业务逻辑，比如：
            // CameraService.Add(info); 
        }
    }

    [FileExtension(".cvcal")]
    public class CVCalFileProcess : IFileProcessor
    {
        public int Order => 1;
        public void Export(string filePath) { }
        public bool Process(string filePath)
        {
            CVXFileProcess.FilePath = filePath;
            return false;
        }
    }
}