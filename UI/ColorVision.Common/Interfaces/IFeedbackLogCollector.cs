using System.Collections.Generic;

namespace ColorVision.UI
{
    /// <summary>
    /// Interface for collecting diagnostic log files for the feedback system.
    /// Implementations are discovered at runtime via AssemblyHandler.LoadImplementations&lt;IFeedbackLogCollector&gt;().
    /// Each implementation provides log files from a specific source (app logs, service logs, dumps, system info, etc.).
    /// </summary>
    public interface IFeedbackLogCollector
    {
        /// <summary>
        /// Display name of this log source (e.g., "Application Logs", "x64 Service Logs").
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Collection order (lower = collected first).
        /// </summary>
        int Order { get; }

        /// <summary>
        /// Collects log/diagnostic files and returns their paths.
        /// Implementations should handle errors internally and return only the files that were successfully collected.
        /// Files may be temporary copies — the caller will add them to a zip archive.
        /// Each entry is (entryPath, filePath) where entryPath is the relative path inside the zip.
        /// </summary>
        IEnumerable<(string EntryPath, string FilePath)> CollectFiles();
    }
}
