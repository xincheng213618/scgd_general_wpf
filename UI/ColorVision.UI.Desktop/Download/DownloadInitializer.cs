namespace ColorVision.UI.Desktop.Download
{
    public class DownloadInitializer : InitializerBase
    {
        public override int Order => 50;
        public override string Name => nameof(DownloadInitializer);

        public override Task InitializeAsync()
        {
            Aria2cDownloadManager manager = Aria2cDownloadManager.GetInstance();
            DownloadCompletionNotifier.Attach(manager);
            manager.AutoRestartIncompleteDownloadsAsync();
            return Task.CompletedTask;
        }
    }
}
