namespace ColorVision.UI
{
    /// <summary>
    /// Download service interface for cross-assembly download integration.
    /// Implementations are discovered via AssemblyHandler.LoadImplementations.
    /// </summary>
    public interface IDownloadService
    {
        /// <summary>
        /// Adds a download task. Returns immediately.
        /// </summary>
        /// <param name="url">Download URL</param>
        /// <param name="savePath">Save directory</param>
        /// <param name="authorization">Optional "username:password" for HTTP Basic auth</param>
        /// <param name="onCompleted">Callback(isSuccess, filePath) when download finishes</param>
        void AddDownload(string url, string? savePath = null, string? authorization = null, Action<bool, string>? onCompleted = null);
    }
}
