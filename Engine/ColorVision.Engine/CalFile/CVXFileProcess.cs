using ColorVision.Engine.Services.Devices.Camera.Configs;
using ColorVision.Engine.Services.PhyCameras;
using ColorVision.UI;
using CVCommCore.CVCamera;
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

                    ConfigCamera configCamera = null;

                    // 1. 解压读取 JSON 字符串
                    using (ZipArchive archive = ZipFile.OpenRead(targetFile))
                    {

                        var entry = archive.GetEntry("CameraConfig.cfg");
                        if (entry != null)
                        {
                            using (var stream = entry.Open())
                            using (var reader = new StreamReader(stream, Encoding.UTF8))
                            {
                                jsonContent = reader.ReadToEnd();
                                try
                                {
                                    configCamera = JsonConvert.DeserializeObject<ConfigCamera>(jsonContent);
                                }
                                catch (Exception jsonEx)
                                {
                                    MessageBox.Show($"配置文件格式错误: {jsonEx.Message}", "解析错误");
                                    return;
                                }

                                PropertyEditorWindow propertyEditorWindow = new PropertyEditorWindow(configCamera);
                                propertyEditorWindow.Submited += (s, e) =>
                                {
                                    PhyCameraManagerWindow phyCameraManagerWindow = new PhyCameraManagerWindow();
                                    phyCameraManagerWindow.Show();
                                };
                                propertyEditorWindow.ShowDialog();
                                return;
                            }
                        }
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