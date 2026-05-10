using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace ColorVision.Copilot
{
    public readonly record struct CopilotSearchFileEntry(string RootPath, string FullPath);

    public static class CopilotWorkspaceSearchSupport
    {
        private static readonly HashSet<string> IgnoredDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
        {
            ".git",
            ".vs",
            "bin",
            "obj",
            "node_modules",
            "packages",
            "x64",
            "x86",
            "__pycache__",
        };

        private static readonly HashSet<string> TextFileExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".bat",
            ".cmd",
            ".config",
            ".cs",
            ".csproj",
            ".css",
            ".c",
            ".cpp",
            ".cvsln",
            ".dat",
            ".go",
            ".h",
            ".hpp",
            ".ini",
            ".java",
            ".js",
            ".json",
            ".log",
            ".md",
            ".props",
            ".ps1",
            ".py",
            ".scss",
            ".sh",
            ".sln",
            ".sql",
            ".targets",
            ".ts",
            ".tsx",
            ".txt",
            ".xml",
            ".xaml",
            ".yaml",
            ".yml",
        };

        public static IReadOnlyList<string> NormalizeSearchRoots(IEnumerable<string>? roots)
        {
            var normalized = new List<string>();

            foreach (var root in roots ?? Array.Empty<string>())
            {
                var candidate = NormalizeToExistingDirectory(root);
                if (string.IsNullOrWhiteSpace(candidate))
                    continue;

                if (normalized.Any(existing => string.Equals(existing, candidate, StringComparison.OrdinalIgnoreCase)))
                    continue;

                if (normalized.Any(existing => IsSubPathOf(candidate, existing)))
                    continue;

                normalized.RemoveAll(existing => IsSubPathOf(existing, candidate));
                normalized.Add(candidate);
            }

            return normalized;
        }

        public static IEnumerable<CopilotSearchFileEntry> EnumerateFiles(
            IEnumerable<string>? roots,
            bool textFilesOnly,
            CancellationToken cancellationToken)
        {
            foreach (var root in NormalizeSearchRoots(roots))
            {
                foreach (var file in EnumerateFilesUnderRoot(root, textFilesOnly, cancellationToken))
                {
                    yield return new CopilotSearchFileEntry(root, file);
                }
            }
        }

        public static bool IsTextLikeFile(string filePath)
        {
            var extension = Path.GetExtension(filePath);
            return !string.IsNullOrWhiteSpace(extension) && TextFileExtensions.Contains(extension);
        }

        public static string GetDisplayPath(string rootPath, string fullPath)
        {
            try
            {
                return Path.GetRelativePath(rootPath, fullPath).Replace('\\', '/');
            }
            catch
            {
                return fullPath;
            }
        }

        public static string TruncateLine(string value, int maxLength)
        {
            var normalized = (value ?? string.Empty).Replace("\t", "    ").Trim();
            if (normalized.Length <= maxLength)
                return normalized;

            return normalized[..maxLength] + "...";
        }

        private static IEnumerable<string> EnumerateFilesUnderRoot(
            string rootPath,
            bool textFilesOnly,
            CancellationToken cancellationToken)
        {
            var pendingDirectories = new Stack<string>();
            pendingDirectories.Push(rootPath);

            while (pendingDirectories.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var currentDirectory = pendingDirectories.Pop();
                IEnumerable<string> subDirectories;
                IEnumerable<string> files;

                try
                {
                    subDirectories = Directory.EnumerateDirectories(currentDirectory);
                    files = Directory.EnumerateFiles(currentDirectory);
                }
                catch
                {
                    continue;
                }

                foreach (var subDirectory in subDirectories)
                {
                    if (ShouldIgnoreDirectory(subDirectory))
                        continue;

                    pendingDirectories.Push(subDirectory);
                }

                foreach (var file in files)
                {
                    if (textFilesOnly && !IsTextLikeFile(file))
                        continue;

                    yield return file;
                }
            }
        }

        private static string NormalizeToExistingDirectory(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            try
            {
                var fullPath = Path.GetFullPath(path);
                if (File.Exists(fullPath))
                    fullPath = Path.GetDirectoryName(fullPath) ?? string.Empty;

                return Directory.Exists(fullPath) ? fullPath : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static bool ShouldIgnoreDirectory(string directoryPath)
        {
            var name = Path.GetFileName(directoryPath);
            return IgnoredDirectoryNames.Contains(name);
        }

        private static bool IsSubPathOf(string path, string parentPath)
        {
            var parentWithSeparator = parentPath.EndsWith(Path.DirectorySeparatorChar)
                ? parentPath
                : parentPath + Path.DirectorySeparatorChar;

            return path.StartsWith(parentWithSeparator, StringComparison.OrdinalIgnoreCase);
        }
    }
}