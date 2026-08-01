using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ColorVision.Copilot
{
    internal static class CopilotWorkspacePatchScope
    {
        private static readonly HashSet<string> ReservedWindowsDeviceNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
        };

        public static bool TryResolveNewFile(
            CopilotAgentRequest request,
            string requestedPath,
            out string fullPath,
            out string writableRoot,
            out string error)
        {
            fullPath = string.Empty;
            writableRoot = string.Empty;
            error = string.Empty;
            var writableRoots = CopilotWorkspaceSearchSupport.NormalizeSearchRoots(request.WritableLocalRootPaths);
            if (!TryResolveNewFilePath(requestedPath, writableRoots, out fullPath, out writableRoot, out error))
                return false;
            if (File.Exists(fullPath) || Directory.Exists(fullPath))
            {
                error = "The requested path already exists: " + fullPath;
                return false;
            }
            if (!CopilotWorkspaceSearchSupport.IsTextLikeFile(fullPath))
            {
                error = "The target extension is not in the workspace text-file allowlist: " + Path.GetExtension(fullPath);
                return false;
            }

            try
            {
                if (!HasSafeNewPathSegments(writableRoot, fullPath, out error))
                    return false;
                if ((File.GetAttributes(writableRoot) & FileAttributes.ReparsePoint) != 0)
                {
                    error = "Creating files under a workspace reparse point is not allowed.";
                    return false;
                }

                var parent = Path.GetDirectoryName(fullPath);
                if (string.IsNullOrWhiteSpace(parent))
                {
                    error = "The requested file has no parent directory.";
                    return false;
                }
                var current = writableRoot;
                foreach (var segment in Path.GetRelativePath(writableRoot, parent)
                    .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries))
                {
                    current = Path.Combine(current, segment);
                    if (File.Exists(current))
                    {
                        error = "A file already occupies a required parent-directory path: " + current;
                        return false;
                    }
                    if (!Directory.Exists(current))
                        break;
                    if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    {
                        error = "Creating files through a workspace directory reparse point is not allowed.";
                        return false;
                    }
                }
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                error = "The new file path could not be validated safely: " + ex.Message;
                return false;
            }
        }

        public static bool TryResolve(
            CopilotAgentRequest request,
            string requestedPath,
            int maxFileBytes,
            out string fullPath,
            out string error)
        {
            fullPath = string.Empty;
            error = string.Empty;
            var writableRoots = CopilotWorkspaceSearchSupport.NormalizeSearchRoots(request.WritableLocalRootPaths);
            if (!TryResolveExistingFilePath(requestedPath, writableRoots, out fullPath, out error))
                return false;
            if (!File.Exists(fullPath))
            {
                error = "The target file does not exist: " + fullPath;
                return false;
            }
            var resolvedPath = fullPath;
            try
            {
                if (!CopilotWorkspaceSearchSupport.IsTextLikeFile(resolvedPath))
                {
                    error = "The target extension is not in the workspace text-file allowlist: " + Path.GetExtension(resolvedPath);
                    return false;
                }
                if (new FileInfo(resolvedPath).Length > maxFileBytes)
                {
                    error = $"The target file exceeds the {maxFileBytes}-byte workspace patch limit.";
                    return false;
                }

                var exactFiles = (request.WritableLocalFilePaths ?? Array.Empty<string>())
                    .Select(NormalizePath)
                    .Where(path => path.Length > 0)
                    .ToArray();
                var isExactFile = exactFiles.Contains(resolvedPath, StringComparer.OrdinalIgnoreCase);
                var containingRoot = writableRoots.FirstOrDefault(root => IsWithinRoot(resolvedPath, root));
                if (!isExactFile && string.IsNullOrWhiteSpace(containingRoot))
                {
                    error = "The target file is neither explicitly writable nor inside a writable workspace root: " + resolvedPath;
                    return false;
                }

                if ((File.GetAttributes(resolvedPath) & FileAttributes.ReparsePoint) != 0)
                {
                    error = "Writing through a file-system reparse point is not allowed.";
                    return false;
                }
                if (!string.IsNullOrWhiteSpace(containingRoot) && ContainsReparsePoint(containingRoot, resolvedPath))
                {
                    error = "Writing through a workspace directory reparse point is not allowed.";
                    return false;
                }
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                error = "The target file could not be validated safely: " + ex.Message;
                return false;
            }
        }

        private static bool TryResolveExistingFilePath(
            string requestedPath,
            IReadOnlyList<string> writableRoots,
            out string fullPath,
            out string error)
        {
            fullPath = string.Empty;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(requestedPath))
            {
                error = "The target path is empty.";
                return false;
            }

            var path = requestedPath.Trim();
            if (Path.IsPathRooted(path) && !Path.IsPathFullyQualified(path))
            {
                error = "The target path must be workspace-relative or fully qualified: " + path;
                return false;
            }
            if (!Path.IsPathFullyQualified(path))
            {
                if (CopilotWorkspaceSearchSupport.TryResolveExistingFileWithinRoots(
                    path, writableRoots, out fullPath, out var resolutionError))
                {
                    return true;
                }

                error = resolutionError;
                return false;
            }

            try
            {
                fullPath = Path.GetFullPath(path);
                return true;
            }
            catch (Exception ex)
            {
                error = "Invalid target path: " + ex.Message;
                return false;
            }
        }

        private static bool TryResolveNewFilePath(
            string requestedPath,
            IReadOnlyList<string> writableRoots,
            out string fullPath,
            out string writableRoot,
            out string error)
        {
            fullPath = string.Empty;
            writableRoot = string.Empty;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(requestedPath))
            {
                error = "The target path is empty.";
                return false;
            }

            var path = requestedPath.Trim();
            if (Path.IsPathRooted(path) && !Path.IsPathFullyQualified(path))
            {
                error = "The target path must be workspace-relative or fully qualified: " + path;
                return false;
            }

            try
            {
                if (Path.IsPathFullyQualified(path))
                {
                    fullPath = Path.GetFullPath(path);
                    var resolvedPath = fullPath;
                    writableRoot = writableRoots.FirstOrDefault(root => IsWithinRoot(resolvedPath, root)) ?? string.Empty;
                }
                else
                {
                    if (writableRoots.Count != 1)
                    {
                        error = writableRoots.Count == 0
                            ? "No writable workspace root is available for the new file."
                            : "A relative new-file path is ambiguous across multiple writable workspace roots; use a fully qualified path.";
                        return false;
                    }

                    writableRoot = writableRoots[0];
                    fullPath = Path.GetFullPath(path, writableRoot);
                }
            }
            catch (Exception ex)
            {
                error = "Invalid target path: " + ex.Message;
                return false;
            }

            if (writableRoot.Length == 0 || !IsWithinRoot(fullPath, writableRoot))
            {
                error = "New files may be created only inside an existing writable workspace root: " + fullPath;
                return false;
            }
            return true;
        }

        private static string NormalizePath(string path)
        {
            try
            {
                return Path.GetFullPath(path);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static bool IsWithinRoot(string path, string root)
        {
            var relative = Path.GetRelativePath(root, path);
            return !Path.IsPathRooted(relative)
                && !string.Equals(relative, "..", StringComparison.Ordinal)
                && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
        }

        private static bool ContainsReparsePoint(string root, string target)
        {
            var current = root;
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                return true;
            foreach (var segment in Path.GetRelativePath(root, target)
                .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries))
            {
                current = Path.Combine(current, segment);
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    return true;
            }
            return false;
        }

        private static bool HasSafeNewPathSegments(string root, string target, out string error)
        {
            error = string.Empty;
            foreach (var segment in Path.GetRelativePath(root, target)
                .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries))
            {
                if (segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                    || segment.EndsWith(' ')
                    || segment.EndsWith('.'))
                {
                    error = "The requested path contains an unsafe Windows file-name segment: " + segment;
                    return false;
                }
                var baseName = Path.GetFileNameWithoutExtension(segment);
                if (ReservedWindowsDeviceNames.Contains(baseName))
                {
                    error = "The requested path contains a reserved Windows device name: " + segment;
                    return false;
                }
            }
            return true;
        }
    }

}
