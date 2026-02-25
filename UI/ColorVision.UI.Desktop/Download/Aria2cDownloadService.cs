using ColorVision.UI;

namespace ColorVision.UI.Desktop.Download
{
    public class Aria2cDownloadService : IDownloadService
    {
        public void Download(string url, string saveDir, string? authorization = null, Action<string?>? onCompleted = null)
        {
            Aria2cDownloadManager.GetInstance().AddDownload(url, saveDir, authorization, task =>
            {
                if (task.Status == DownloadStatus.Completed)
                {
                    onCompleted?.Invoke(task.SavePath);
                }
                else
                {
                    onCompleted?.Invoke(null);
                }
            });
        }

        public void ShowDownloadWindow()
        {
            DownloadWindow.ShowInstance();
        }
    }
}
