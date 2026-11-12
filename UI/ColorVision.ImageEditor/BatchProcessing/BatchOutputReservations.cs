using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace ColorVision.ImageEditor.BatchProcessing
{
    internal sealed class BatchOutputReservations
    {
        private readonly ISet<string> _paths;
        private readonly Dictionary<string, string>? _directories;

        internal BatchOutputReservations(ISet<string> paths) => _paths = paths;

        internal BatchOutputReservations(IEnumerable<BatchImageItem> items, bool allowOverwrite, CancellationToken cancellationToken)
            : this(new HashSet<string>(StringComparer.OrdinalIgnoreCase))
        {
            // When overwrites are disabled, the final move already protects existing files.
            if (allowOverwrite)
                _directories = new(StringComparer.OrdinalIgnoreCase);
            foreach (BatchImageItem item in items)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;
                try
                {
                    string sourcePath = Path.GetFullPath(item.FilePath);
                    Add(sourcePath);
                    if (allowOverwrite)
                        ReserveLinkedSource(sourcePath);
                }
                catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException)
                {
                    // The processing loop reports invalid paths as individual file failures.
                }
            }
        }

        private void ReserveLinkedSource(string sourcePath)
        {
            var file = new FileInfo(sourcePath);
            if (!file.Exists || (file.Attributes & FileAttributes.ReparsePoint) == 0)
                return;

            FileSystemInfo? target;
            try { target = file.ResolveLinkTarget(returnFinalTarget: true); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // An unreadable link remains reserved by its own path.
                return;
            }
            if (target != null)
                Add(target.FullName);
        }

        internal bool Contains(string fullPath, string? originalPath = null) => _paths.Contains(fullPath)
            || (originalPath != null && _paths.Contains(originalPath))
            || (_directories != null && _paths.Contains(Resolve(fullPath)));

        internal void Add(string fullPath)
        {
            _paths.Add(fullPath);
            if (_directories != null)
                _paths.Add(Resolve(fullPath));
        }

        internal string Resolve(string fullPath)
        {
            if (_directories == null || Path.GetDirectoryName(fullPath) is not string directory)
                return fullPath;
            string resolved = ResolveDirectory(directory);
            return string.Equals(directory, resolved, StringComparison.OrdinalIgnoreCase)
                ? fullPath
                : Path.Combine(resolved, Path.GetFileName(fullPath));
        }

        private string ResolveDirectory(string directory)
        {
            if (_directories!.TryGetValue(directory, out string? resolved))
                return resolved;
            if (Path.GetDirectoryName(directory) is not string parent)
                return directory;

            // Resolve ancestors first so a regular child of a linked directory gets the same key.
            string path = Path.Combine(ResolveDirectory(parent), Path.GetFileName(directory));
            var info = new DirectoryInfo(path);
            if (info.Exists && (info.Attributes & FileAttributes.ReparsePoint) != 0
                && info.ResolveLinkTarget(returnFinalTarget: true) is FileSystemInfo target)
            {
                path = ResolveDirectory(Path.TrimEndingDirectorySeparator(target.FullName));
            }
            _directories.Add(directory, path);
            return path;
        }
    }
}
