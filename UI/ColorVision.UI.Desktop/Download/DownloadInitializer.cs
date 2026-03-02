using ColorVision.UI;

namespace ColorVision.UI.Desktop.Download
{
    public class DownloadInitializer : InitializerBase
    {
        public override int Order => 50;
        public override string Name => nameof(DownloadInitializer);

        public override async Task InitializeAsync()
        {
            await Task.Run(() => Aria2cDownloadManager.GetInstance().AutoRestartIncompleteDownloads());
        }
    }
}
