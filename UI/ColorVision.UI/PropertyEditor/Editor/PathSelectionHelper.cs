using System.IO;

namespace System.ComponentModel
{
    public static class PathSelectionHelper
    {
        public static (string? Directory, string? FileName) GetFileDialogStart(string? currentPath, string? lastOpenDirectory)
        {
            if (File.Exists(currentPath))
                return (Path.GetDirectoryName(Path.GetFullPath(currentPath)), Path.GetFileName(currentPath));
            if (Directory.Exists(currentPath))
                return (Path.GetFullPath(currentPath), null);

            string? expandedPath = string.IsNullOrWhiteSpace(currentPath) ? null : Environment.ExpandEnvironmentVariables(currentPath.Trim().Trim('"'));
            if (!string.Equals(expandedPath, currentPath, StringComparison.Ordinal))
            {
                if (File.Exists(expandedPath))
                    return (Path.GetDirectoryName(Path.GetFullPath(expandedPath)), Path.GetFileName(expandedPath));
                if (Directory.Exists(expandedPath))
                    return (Path.GetFullPath(expandedPath), null);
            }

            return (Directory.Exists(lastOpenDirectory) ? lastOpenDirectory : GetExistingDirectory(currentPath), null);
        }

        public static string? GetExistingDirectory(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            try
            {
                var candidate = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
                return GetExistingDirectoryCore(candidate);
            }
            catch
            {
                return null;
            }
        }

        public static string? GetFileName(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            try
            {
                return Path.GetFileName(path.Trim().Trim('"'));
            }
            catch
            {
                return null;
            }
        }

        private static string? GetExistingDirectoryCore(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            if (Directory.Exists(path))
            {
                return Path.GetFullPath(path);
            }

            if (File.Exists(path))
            {
                var parent = Path.GetDirectoryName(Path.GetFullPath(path));
                return string.IsNullOrWhiteSpace(parent) ? null : parent;
            }

            var trimmedPath = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var parentPath = Path.GetDirectoryName(trimmedPath);
            return string.IsNullOrWhiteSpace(parentPath) ? null : GetExistingDirectoryCore(parentPath);
        }
    }
}
