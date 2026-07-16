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
        private const long MaxTextSearchFileBytes = 8L * 1024 * 1024;

        private static readonly EnumerationOptions SearchEnumerationOptions = new()
        {
            AttributesToSkip = FileAttributes.ReparsePoint,
            IgnoreInaccessible = true,
            RecurseSubdirectories = false,
            ReturnSpecialDirectories = false,
        };

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

        public static bool IsPathWithinRoots(string? path, IEnumerable<string>? roots)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(path);
            }
            catch
            {
                return false;
            }

            foreach (var root in NormalizeSearchRoots(roots))
            {
                if (!string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase)
                    && !IsSubPathOf(fullPath, root))
                {
                    continue;
                }

                return !CrossesReparsePoint(root, fullPath);
            }

            return false;
        }

        public static bool TryResolveExistingFileWithinRoots(
            string? path,
            IEnumerable<string>? roots,
            out string fullPath,
            out string errorMessage)
        {
            return TryResolveExistingPathWithinRoots(path, roots, File.Exists, "file", out fullPath, out errorMessage);
        }

        public static bool TryResolveExistingDirectoryWithinRoots(
            string? path,
            IEnumerable<string>? roots,
            out string fullPath,
            out string errorMessage)
        {
            return TryResolveExistingPathWithinRoots(path, roots, Directory.Exists, "directory", out fullPath, out errorMessage);
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

                foreach (var subDirectory in EnumerateSafely(
                    () => Directory.EnumerateDirectories(currentDirectory, "*", SearchEnumerationOptions),
                    cancellationToken))
                {
                    if (ShouldIgnoreDirectory(subDirectory))
                        continue;

                    pendingDirectories.Push(subDirectory);
                }

                foreach (var file in EnumerateSafely(
                    () => Directory.EnumerateFiles(currentDirectory, "*", SearchEnumerationOptions),
                    cancellationToken))
                {
                    if (textFilesOnly && !IsSearchableTextFile(file))
                        continue;

                    yield return file;
                }
            }
        }

        private static IEnumerable<string> EnumerateSafely(
            Func<IEnumerable<string>> createEntries,
            CancellationToken cancellationToken)
        {
            IEnumerator<string>? enumerator;
            try
            {
                enumerator = createEntries().GetEnumerator();
            }
            catch
            {
                yield break;
            }

            using (enumerator)
            {
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    string current;
                    try
                    {
                        if (!enumerator.MoveNext())
                            yield break;

                        current = enumerator.Current;
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch
                    {
                        yield break;
                    }

                    yield return current;
                }
            }
        }

        private static bool IsSearchableTextFile(string filePath)
        {
            if (!IsTextLikeFile(filePath))
                return false;

            try
            {
                return new FileInfo(filePath).Length <= MaxTextSearchFileBytes;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryResolveExistingPathWithinRoots(
            string? path,
            IEnumerable<string>? roots,
            Func<string, bool> exists,
            string pathKind,
            out string fullPath,
            out string errorMessage)
        {
            fullPath = string.Empty;
            errorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(path))
            {
                errorMessage = $"The {pathKind} path is empty.";
                return false;
            }

            var searchRoots = NormalizeSearchRoots(roots);
            if (searchRoots.Count == 0)
            {
                errorMessage = $"No workspace root is available to resolve the {pathKind} path.";
                return false;
            }

            var requestedPath = path.Trim();
            if (Path.IsPathRooted(requestedPath) && !Path.IsPathFullyQualified(requestedPath))
            {
                errorMessage = $"The {pathKind} path must be workspace-relative or fully qualified: {requestedPath}";
                return false;
            }

            if (Path.IsPathFullyQualified(requestedPath))
            {
                try
                {
                    var candidate = Path.GetFullPath(requestedPath);
                    if (!IsPathWithinRoots(candidate, searchRoots))
                    {
                        errorMessage = $"The {pathKind} path is outside the allowed workspace roots: {candidate}";
                        return false;
                    }

                    if (!exists(candidate))
                    {
                        errorMessage = $"The {pathKind} does not exist: {candidate}";
                        return false;
                    }

                    fullPath = candidate;
                    return true;
                }
                catch (Exception ex)
                {
                    errorMessage = $"Invalid {pathKind} path: {ex.Message}";
                    return false;
                }
            }

            var matches = new List<string>();
            var escapedWorkspace = false;
            foreach (var root in searchRoots)
            {
                string candidate;
                try
                {
                    candidate = Path.GetFullPath(requestedPath, root);
                }
                catch (Exception ex)
                {
                    errorMessage = $"Invalid {pathKind} path: {ex.Message}";
                    return false;
                }

                if (!IsPathWithinRoots(candidate, [root]))
                {
                    escapedWorkspace = true;
                    continue;
                }

                if (exists(candidate))
                    matches.Add(candidate);
            }

            var distinctMatches = matches.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (distinctMatches.Length == 1)
            {
                fullPath = distinctMatches[0];
                return true;
            }

            if (distinctMatches.Length > 1)
            {
                errorMessage = $"The workspace-relative {pathKind} path is ambiguous across multiple roots: {requestedPath}";
                return false;
            }

            errorMessage = escapedWorkspace
                ? $"The workspace-relative {pathKind} path escapes the allowed workspace roots: {requestedPath}"
                : $"The {pathKind} does not exist in the allowed workspace roots: {requestedPath}";
            return false;
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
            if (IgnoredDirectoryNames.Contains(name))
                return true;

            return IsReparsePoint(directoryPath);
        }

        private static bool CrossesReparsePoint(string rootPath, string fullPath)
        {
            if (File.Exists(fullPath) && IsReparsePoint(fullPath))
                return true;

            var current = Directory.Exists(fullPath) ? fullPath : Path.GetDirectoryName(fullPath);
            while (!string.IsNullOrWhiteSpace(current)
                && !string.Equals(current, rootPath, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                        return true;
                }
                catch
                {
                    return true;
                }

                current = Path.GetDirectoryName(current);
            }

            return false;
        }

        private static bool IsReparsePoint(string path)
        {
            try
            {
                return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
            }
            catch
            {
                return true;
            }
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
