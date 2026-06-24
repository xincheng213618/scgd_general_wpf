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
        /// Short optional text shown under the collector name in the feedback window.
        /// </summary>
        string Description => string.Empty;

        /// <summary>
        /// Collection order (lower = collected first).
        /// </summary>
        int Order { get; }

        /// <summary>
        /// Whether this source should be selected by default when users package feedback logs.
        /// </summary>
        bool IsSelectedByDefault => true;

        /// <summary>
        /// Collects log/diagnostic files and returns their paths.
        /// Implementations should handle errors internally and return only the files that were successfully collected.
        /// Files may be temporary copies — the caller will add them to a zip archive.
        /// Each entry is (entryPath, filePath) where entryPath is the relative path inside the zip.
        /// </summary>
        IEnumerable<(string EntryPath, string FilePath)> CollectFiles();
    }
}
