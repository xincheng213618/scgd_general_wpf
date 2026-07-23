using System;
using System.IO;

namespace ColorVision.Update
{
    internal enum StartupUpdatePackageKind
    {
        None,
        Application,
        Plugin,
    }

    /// <summary>
    /// Handles update packages before application modules, configuration, plugins, or windows are initialized.
    /// </summary>
    internal static class StartupUpdatePackageHandler
    {
        internal static StartupUpdatePackageKind Classify(string? inputPath)
        {
            if (string.IsNullOrWhiteSpace(inputPath))
                return StartupUpdatePackageKind.None;

            string extension = Path.GetExtension(inputPath);
            if (string.Equals(extension, ".cvx", StringComparison.OrdinalIgnoreCase))
                return StartupUpdatePackageKind.Application;
            if (string.Equals(extension, ".cvxp", StringComparison.OrdinalIgnoreCase))
                return StartupUpdatePackageKind.Plugin;
            return StartupUpdatePackageKind.None;
        }

        /// <summary>
        /// Returns true when the input was claimed as an update-package request. A successful handoff exits
        /// the process; when preparation fails, the caller must stop startup instead of opening the main window.
        /// </summary>
        internal static bool HandleIfUpdatePackage(string? inputPath)
        {
            StartupUpdatePackageKind kind = Classify(inputPath);
            if (kind == StartupUpdatePackageKind.None)
                return false;

            string packagePath = Path.GetFullPath(inputPath!);
            AutoUpdater.TryStartIncrementalApplicationUpdate(
                kind == StartupUpdatePackageKind.Application ? new[] { packagePath } : Array.Empty<string>(),
                kind == StartupUpdatePackageKind.Plugin ? new[] { packagePath } : null,
                restartApplication: true,
                allowElevationFallback: true,
                showErrors: true);
            return true;
        }
    }
}
