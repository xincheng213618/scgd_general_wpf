using log4net;
using System.IO;

namespace ColorVision.UI.LogImp
{
    public readonly record struct DiagnosticFileCleanupResult(int DeletedFileCount, int FailedFileCount, long ReleasedBytes);

    /// <summary>
    /// Deletes historical files explicitly owned by feedback diagnostic collectors.
    /// </summary>
    public static class DiagnosticFileCleanupService
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(DiagnosticFileCleanupService));

        public static DiagnosticFileCleanupResult Cleanup(
            IEnumerable<IFeedbackDiagnosticCleanupSource> sources,
            DateTime preserveFromUtc)
        {
            ArgumentNullException.ThrowIfNull(sources);
            preserveFromUtc = preserveFromUtc.Kind == DateTimeKind.Utc
                ? preserveFromUtc
                : preserveFromUtc.ToUniversalTime();

            var candidatePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int failedFileCount = 0;
            foreach (IFeedbackDiagnosticCleanupSource source in sources)
            {
                try
                {
                    foreach (string filePath in source.GetHistoricalDiagnosticFiles(preserveFromUtc))
                    {
                        if (!string.IsNullOrWhiteSpace(filePath))
                            candidatePaths.Add(Path.GetFullPath(filePath));
                    }
                }
                catch (Exception ex)
                {
                    failedFileCount++;
                    log.Warn($"Could not enumerate historical diagnostics from {source.GetType().Name}.", ex);
                }
            }

            int deletedFileCount = 0;
            long releasedBytes = 0;
            foreach (string filePath in candidatePaths)
            {
                try
                {
                    if (!File.Exists(filePath))
                        continue;

                    long length = new FileInfo(filePath).Length;
                    File.Delete(filePath);
                    deletedFileCount++;
                    releasedBytes += length;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                {
                    failedFileCount++;
                    log.Debug($"Could not delete historical diagnostic file '{filePath}': {ex.Message}");
                }
            }

            log.Info($"Historical diagnostic cleanup completed. Deleted={deletedFileCount}, Failed={failedFileCount}, ReleasedBytes={releasedBytes}.");
            return new DiagnosticFileCleanupResult(deletedFileCount, failedFileCount, releasedBytes);
        }
    }
}
