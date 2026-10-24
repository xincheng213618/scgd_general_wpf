namespace ColorVision.UI.Desktop.Download
{
    public class Aria2cDownloadService : IDownloadService
    {
        public void Download(string url, string saveDir, string? authorization = null, Action<string?>? onCompleted = null)
            => Download(url, saveDir, authorization, onCompleted, null);

        public void Download(string url, string saveDir, string? authorization, Action<string?>? onCompleted, string? fileName)
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
            }, fileName);
        }

        public void ShowDownloadWindow()
        {
            DownloadWindow.ShowInstance();
        }

        public void CloseDownloadWindow()
        {
            DownloadWindow.CloseInstance();
        }
    }
}
