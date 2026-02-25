using ColorVision.UI;
using ColorVision.UI.Desktop.Download;

namespace ColorVision.UI.Desktop.Download
{
    /// <summary>
    /// aria2c-based implementation of IDownloadService.
    /// Discovered by the main app via AssemblyHandler.LoadImplementations.
    /// </summary>
    public class Aria2cDownloadService : IDownloadService
    {
        public void AddDownload(string url, string? savePath = null, string? authorization = null, Action<bool, string>? onCompleted = null)
        {
            Action<DownloadTask>? taskCallback = null;
            if (onCompleted != null)
            {
                taskCallback = task => onCompleted(task.Status == DownloadStatus.Completed, task.SavePath);
            }
            Aria2cDownloadManager.GetInstance().AddDownload(url, savePath, authorization, taskCallback);
        }
    }
}
