using ColorVision.Engine.Services.PhyCameras;
using ColorVision.UI;
using log4net;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Engine
{

    public static class CVXFileProcess
    {
        public static string FilePath { get; set; } 
    }

    public class CVCalInitialized : IMainWindowInitialized
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(CVCalInitialized));

        public Task Initialize()
        {
            if (string.IsNullOrWhiteSpace(CVXFileProcess.FilePath)) return Task.CompletedTask;
            if (!File.Exists(CVXFileProcess.FilePath)) return Task.CompletedTask;

            PhyCameraManagerWindow phyCameraManagerWindow = new PhyCameraManagerWindow();
            phyCameraManagerWindow.Show();
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
