#pragma warning disable CS8604,CS8625
using ColorVision.Engine.Services.Devices.Camera.Configs;
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
                    if (!TryImport(targetFile, out string errorMessage))
                        MessageBox.Show(errorMessage, ColorVision.Engine.Properties.Resources.Engine_Msg_ParseError);
                }
                finally
                {
                    CVXFileProcess.FilePath = null;
                }
            });

            return Task.CompletedTask;
        }

        internal static bool TryImport(string targetFile, out string errorMessage)
        {
            errorMessage = string.Empty;
            try
            {
                using ZipArchive archive = ZipFile.OpenRead(targetFile);
                ZipArchiveEntry? entry = archive.GetEntry("CameraConfig.cfg");
                if (entry == null)
                {
                    errorMessage = "校准文件中缺少 CameraConfig.cfg。";
                    return false;
                }

                using Stream stream = entry.Open();
                using var reader = new StreamReader(stream, Encoding.UTF8);
                string jsonContent = reader.ReadToEnd();
                ConfigCamera? configCamera;
                try
                {
                    configCamera = JsonConvert.DeserializeObject<ConfigCamera>(jsonContent);
                }
                catch (Exception jsonException)
                {
                    errorMessage = $"{ColorVision.Engine.Properties.Resources.Engine_Msg_ConfigFileFormatError}: {jsonException.Message}";
                    return false;
                }
                if (configCamera == null)
                {
                    errorMessage = ColorVision.Engine.Properties.Resources.Engine_Msg_ConfigFileFormatError;
                    return false;
                }

                var propertyEditorWindow = new PropertyEditorWindow(configCamera);
                propertyEditorWindow.Submited += (s, e) => new PhyCameraManagerWindow().Show();
                propertyEditorWindow.ShowDialog();
                return true;
            }
            catch (Exception ex)
            {
                log.Error($"导入流程异常: {ex.Message}");
                errorMessage = $"{ColorVision.Engine.Properties.Resources.Engine_Msg_FileProcessError}: {ex.Message}";
                return false;
            }
        }
    }

    [FileExtension(".cvcal")]
    public class CVCalFileProcess : IFileOpenActionProcessor
    {
        public int Order => 1;
        public void Export(string filePath) { }
        public bool Process(string filePath)
        {
            CVXFileProcess.FilePath = filePath;
            return false;
        }

        public FileOpenRouteResult OpenFile(string filePath)
        {
            if (Application.Current?.Dispatcher is not { } dispatcher)
                return new FileOpenRouteResult(true, false, "应用程序尚未完成初始化，无法导入校准文件。");

            bool succeeded = false;
            string errorMessage = string.Empty;
            void Import() => succeeded = CVCalInitialized.TryImport(filePath, out errorMessage);
            if (dispatcher.CheckAccess())
                Import();
            else
                dispatcher.Invoke(Import);
            return new FileOpenRouteResult(true, succeeded, errorMessage);
        }
    }
}
