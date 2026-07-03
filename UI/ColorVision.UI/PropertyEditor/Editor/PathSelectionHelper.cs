using System;
using System.IO;

namespace System.ComponentModel
{
    public static class PathSelectionHelper
    {
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
