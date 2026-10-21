namespace ColorVision.UI
{
    /// <summary>
    /// Abstraction for download services, allowing cross-assembly download integration.
    /// Implemented by Aria2cDownloadManager in ColorVision.UI.Desktop.
    /// Discovered via AssemblyHandler.LoadImplementations&lt;IDownloadService&gt;().
    /// </summary>
    public interface IDownloadService
    {
        /// <summary>
        /// Downloads a file from the given URL to the specified directory.
        /// On completion, onCompleted is called with the saved file path (or null on failure).
        /// </summary>
        /// <param name="url">Download URL</param>
        /// <param name="saveDir">Directory to save the file</param>
        /// <param name="authorization">Optional auth string (e.g., "user:pass")</param>
        /// <param name="onCompleted">Callback with file path on success, null on failure</param>
        void Download(string url, string saveDir, string? authorization = null, Action<string?>? onCompleted = null);

        /// <summary>
        /// Downloads to an explicit file name, including when the URL ends in an API version or identifier.
        /// The original overload remains available to existing callers and implementations.
        /// </summary>
        void Download(string url, string saveDir, string? authorization, Action<string?>? onCompleted, string fileName)
            => throw new NotSupportedException("This download service does not support explicit file names.");

        /// <summary>
        /// Shows the download manager window.
        /// </summary>
        void ShowDownloadWindow();

        /// <summary>
        /// Closes the download manager window when the caller owns a temporary download flow.
        /// </summary>
        void CloseDownloadWindow();
    }
}
