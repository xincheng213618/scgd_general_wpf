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
            IgnoreInaccessible = false,
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

        // Business exports can supply evidence without expanding the separate patch allowlist.
        private static readonly HashSet<string> ReadOnlyTextFileExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".csv", ".tsv", ".jsonl", ".ndjson",
        };

        public static IReadOnlyList<string> NormalizeSearchRoots(IEnumerable<string>? roots)
        {
            var normalized = new List<string>();

            foreach (var root in roots ?? Array.Empty<string>())
            {
                var candidate = NormalizeToExistingDirectory(root);
                if (string.IsNullOrWhiteSpace(candidate))
                    continue;
                if (IsReparsePoint(candidate))
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

        public static IReadOnlyList<string> NormalizeSearchScopes(IEnumerable<string>? paths)
        {
            var normalized = new List<string>();

            foreach (var path in paths ?? Array.Empty<string>())
            {
                string candidate;
                try
                {
                    candidate = Path.GetFullPath(path);
                }
                catch
                {
                    continue;
                }

                var isDirectory = Directory.Exists(candidate);
                if (!isDirectory && !File.Exists(candidate))
                    continue;
                if (IsReparsePoint(candidate))
                    continue;
                if (normalized.Any(existing => string.Equals(existing, candidate, StringComparison.OrdinalIgnoreCase)))
                    continue;
                if (normalized.Any(existing => Directory.Exists(existing) && IsSubPathOf(candidate, existing)))
                    continue;

                if (isDirectory)
                    normalized.RemoveAll(existing => IsSubPathOf(existing, candidate));
                normalized.Add(candidate);
            }

            return normalized;
        }

        public static IEnumerable<CopilotSearchFileEntry> EnumerateFiles(
            IEnumerable<string>? roots,
            bool textFilesOnly,
            CancellationToken cancellationToken,
            Action<string, string>? reportIncompletePath = null)
        {
            foreach (var root in NormalizeSearchScopes(roots))
            {
                if (File.Exists(root))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (textFilesOnly && !IsReadableTextFile(root))
                        reportIncompletePath?.Invoke(root, "The explicit file extension is outside the text-search allowlist. Use ReadLocalFile to inspect this file.");
                    if (!textFilesOnly || IsSearchableTextFile(root, reportIncompletePath))
                        yield return new CopilotSearchFileEntry(root, root);
                    continue;
                }

                foreach (var file in EnumerateFilesUnderRoot(root, textFilesOnly, cancellationToken, reportIncompletePath))
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

        public static bool IsReadableTextFile(string filePath) =>
            IsTextLikeFile(filePath) || ReadOnlyTextFileExtensions.Contains(Path.GetExtension(filePath));

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

        public static bool IsExplicitlyAllowedPath(string? path, IEnumerable<string>? allowedPaths)
        {
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
                return false;

            try
            {
                var fullPath = Path.GetFullPath(path);
                return (allowedPaths ?? Array.Empty<string>()).Any(allowedPath =>
                    !string.IsNullOrWhiteSpace(allowedPath)
                    && string.Equals(Path.GetFullPath(allowedPath), fullPath, StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return false;
            }
        }

        public static bool HasReparsePointInPath(string? path)
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
                return true;
            }

            var current = fullPath;
            if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
                current = Path.GetDirectoryName(fullPath) ?? string.Empty;

            while (!string.IsNullOrWhiteSpace(current))
            {
                if (IsReparsePoint(current))
                    return true;

                var parent = Path.GetDirectoryName(current);
                if (string.IsNullOrWhiteSpace(parent)
                    || string.Equals(parent, current, StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                current = parent;
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

        public static bool TryResolveExistingPathWithinRoots(
            string? path,
            IEnumerable<string>? roots,
            out string fullPath,
            out string errorMessage)
        {
            return TryResolveExistingPathWithinRoots(
                path,
                roots,
                candidate => File.Exists(candidate) || Directory.Exists(candidate),
                "target",
                out fullPath,
                out errorMessage);
        }

        public static bool TryResolveExistingDirectoryWithinRoots(
            string? path,
            IEnumerable<string>? roots,
            out string fullPath,
            out string errorMessage)
        {
            return TryResolveExistingPathWithinRoots(path, roots, Directory.Exists, "directory", out fullPath, out errorMessage);
        }

        public static bool TryResolveDirectoryScope(
            string? path,
            IEnumerable<string>? roots,
            out IReadOnlyList<string> scopedRoots,
            out string errorMessage)
        {
            var normalizedRoots = NormalizeSearchRoots(roots);
            if (string.IsNullOrWhiteSpace(path))
            {
                scopedRoots = normalizedRoots;
                errorMessage = normalizedRoots.Count == 0 ? "No searchable workspace root is available." : string.Empty;
                return normalizedRoots.Count > 0;
            }

            if (TryResolveExistingDirectoryWithinRoots(path, normalizedRoots, out var directoryPath, out errorMessage))
            {
                scopedRoots = [directoryPath];
                return true;
            }

            scopedRoots = Array.Empty<string>();
            return false;
        }

        public static bool TryResolveFileOrDirectoryScope(
            string? path,
            IEnumerable<string>? roots,
            out IReadOnlyList<string> scopedPaths,
            out string errorMessage)
        {
            var normalizedRoots = NormalizeSearchRoots(roots);
            if (string.IsNullOrWhiteSpace(path))
            {
                scopedPaths = normalizedRoots;
                errorMessage = normalizedRoots.Count == 0 ? "No searchable workspace root is available." : string.Empty;
                return normalizedRoots.Count > 0;
            }

            if (TryResolveExistingPathWithinRoots(path, normalizedRoots, out var fullPath, out errorMessage))
            {
                scopedPaths = [fullPath];
                return true;
            }

            scopedPaths = Array.Empty<string>();
            return false;
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
            CancellationToken cancellationToken,
            Action<string, string>? reportIncompletePath)
        {
            var pendingDirectories = new Stack<string>();
            pendingDirectories.Push(rootPath);

            while (pendingDirectories.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var currentDirectory = pendingDirectories.Pop();

                foreach (var subDirectory in EnumerateSafely(
                    () => Directory.EnumerateDirectories(currentDirectory, "*", SearchEnumerationOptions),
                    cancellationToken,
                    () => reportIncompletePath?.Invoke(currentDirectory, "The directory could not be fully enumerated.")))
                {
                    if (ShouldIgnoreDirectory(subDirectory))
                        continue;

                    pendingDirectories.Push(subDirectory);
                }

                foreach (var file in EnumerateSafely(
                    () => Directory.EnumerateFiles(currentDirectory, "*", SearchEnumerationOptions),
                    cancellationToken,
                    () => reportIncompletePath?.Invoke(currentDirectory, "The directory could not be fully enumerated.")))
                {
                    if (textFilesOnly && !IsSearchableTextFile(file, reportIncompletePath))
                        continue;

                    yield return file;
                }
            }
        }

        private static IEnumerable<string> EnumerateSafely(
            Func<IEnumerable<string>> createEntries,
            CancellationToken cancellationToken,
            Action reportFailure)
        {
            IEnumerator<string>? enumerator;
            try
            {
                enumerator = createEntries().GetEnumerator();
            }
            catch
            {
                reportFailure();
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
                        reportFailure();
                        yield break;
                    }

                    yield return current;
                }
            }
        }

        private static bool IsSearchableTextFile(string filePath, Action<string, string>? reportIncompletePath)
        {
            if (!IsReadableTextFile(filePath))
                return false;

            try
            {
                if (new FileInfo(filePath).Length <= MaxTextSearchFileBytes)
                    return true;
                reportIncompletePath?.Invoke(filePath, "The file exceeds the 8 MiB text-search limit. Use bounded ReadLocalFile ranges to inspect it.");
                return false;
            }
            catch
            {
                reportIncompletePath?.Invoke(filePath, "The file metadata could not be read.");
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
                        errorMessage = DescribePathResolutionFailure(candidate, searchRoots, pathKind);
                        return false;
                    }

                    if (!exists(candidate))
                    {
                        errorMessage = DescribePathResolutionFailure(candidate, searchRoots, pathKind);
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
            var unresolvedPaths = new List<(string Path, string Root)>();
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
                    unresolvedPaths.Add((candidate, root));
                    continue;
                }

                if (exists(candidate))
                    matches.Add(candidate);
                else
                    unresolvedPaths.Add((candidate, root));
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

            errorMessage = string.Join(Environment.NewLine, unresolvedPaths.Take(3)
                .Select(candidate => DescribePathResolutionFailure(candidate.Path, [candidate.Root], pathKind)));
            if (unresolvedPaths.Count > 3)
                errorMessage += $" No accessible match was found in {unresolvedPaths.Count - 3} additional roots.";
            return false;
        }

        // Diagnostics only: the existing path resolution has already failed.
        // Inspect from the allowed root outwards, stopping before following a link.
        internal static string DescribePathResolutionFailure(string fullPath, IReadOnlyList<string> roots, string pathKind)
        {
            var root = roots.FirstOrDefault(candidate => string.Equals(fullPath, candidate, StringComparison.OrdinalIgnoreCase)
                || IsSubPathOf(fullPath, candidate));
            if (root == null)
                return $"The {pathKind} path is outside the allowed workspace roots: {fullPath}";

            var components = new List<string> { root };
            var current = root;
            foreach (var segment in Path.GetRelativePath(root, fullPath)
                .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries))
            {
                if (segment == ".") continue;
                current = Path.Combine(current, segment);
                components.Add(current);
            }

            foreach (var component in components)
            {
                FileAttributes attributes;
                try { attributes = File.GetAttributes(component); }
                catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
                {
                    return $"The {pathKind} does not exist: {fullPath}. Relative paths start at an allowed workspace root; '.' refers to that root. Inspect a root with the directory-listing tool and use an exact listed path.";
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                {
                    return $"The {pathKind} path could not be verified safely: {fullPath}. {ex.Message}";
                }

                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    return $"The {pathKind} path crosses a file-system reparse point and is not allowed: {fullPath}";
                if ((attributes & FileAttributes.Directory) == 0 && !string.Equals(component, fullPath, StringComparison.OrdinalIgnoreCase))
                    return $"The {pathKind} path cannot be resolved because a parent component is a file: {component}";
                if (string.Equals(component, fullPath, StringComparison.OrdinalIgnoreCase))
                {
                    if (pathKind == "directory" && (attributes & FileAttributes.Directory) == 0)
                        return $"The path refers to a file; a directory is required: {fullPath}";
                    if (pathKind == "file" && (attributes & FileAttributes.Directory) != 0)
                        return $"The path refers to a directory; a file is required: {fullPath}";
                }
            }

            return $"The {pathKind} path could not be verified as accessible: {fullPath}. Re-list the allowed workspace root before choosing another path.";
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
            if (IsReparsePoint(rootPath))
                return true;

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
