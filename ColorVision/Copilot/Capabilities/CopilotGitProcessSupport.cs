using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ColorVision.Copilot
{
    internal static class CopilotGitProcessSupport
    {
        public static IReadOnlyDictionary<string, string?> EnvironmentOverrides { get; } = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["GIT_DIR"] = null,
            ["GIT_WORK_TREE"] = null,
            ["GIT_COMMON_DIR"] = null,
            ["GIT_INDEX_FILE"] = null,
            ["GIT_OBJECT_DIRECTORY"] = null,
            ["GIT_ALTERNATE_OBJECT_DIRECTORIES"] = null,
            ["GIT_NAMESPACE"] = null,
            ["GIT_SHALLOW_FILE"] = null,
            ["GIT_CEILING_DIRECTORIES"] = null,
            ["GIT_DISCOVERY_ACROSS_FILESYSTEM"] = null,
            ["GIT_CONFIG_COUNT"] = null,
            ["GIT_CONFIG_PARAMETERS"] = null,
            ["GIT_PREFIX"] = null,
            ["GIT_EXEC_PATH"] = null,
            ["GIT_OPTIONAL_LOCKS"] = "0",
        };

        public static IReadOnlyList<string> GetAllowedRoots(CopilotAgentRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            return CopilotWorkspaceSearchSupport.NormalizeSearchRoots(
                request.SearchRootPaths.Concat(request.WritableLocalRootPaths));
        }

        public static bool TryResolveTargetPath(
            string? requestedPath,
            IReadOnlyList<string> allowedRoots,
            bool requireExisting,
            out string selectedPath,
            out string targetDirectory,
            out string containingRoot,
            out string error)
        {
            selectedPath = string.Empty;
            targetDirectory = string.Empty;
            containingRoot = string.Empty;
            error = string.Empty;
            if (allowedRoots.Count == 0)
            {
                error = "No current request root is available.";
                return false;
            }

            string candidate;
            if (string.IsNullOrWhiteSpace(requestedPath))
            {
                candidate = allowedRoots[0];
            }
            else if (requireExisting)
            {
                if (!CopilotWorkspaceSearchSupport.TryResolveExistingPathWithinRoots(
                    requestedPath, allowedRoots, out candidate, out error))
                {
                    return false;
                }
            }
            else if (!TryResolvePotentialPath(requestedPath, allowedRoots, out candidate, out error))
            {
                return false;
            }

            var isFile = File.Exists(candidate);
            var isDirectory = Directory.Exists(candidate);
            if (requireExisting && !isFile && !isDirectory)
            {
                error = "The selected path does not exist.";
                return false;
            }

            containingRoot = allowedRoots.FirstOrDefault(root => IsWithinRoot(candidate, root)) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(containingRoot))
            {
                error = "The selected path is not inside a current request root.";
                return false;
            }

            targetDirectory = isDirectory ? candidate : Path.GetDirectoryName(candidate) ?? string.Empty;
            if (!Directory.Exists(targetDirectory))
            {
                var current = targetDirectory;
                while (!string.IsNullOrWhiteSpace(current) && IsWithinRoot(current, containingRoot) && !Directory.Exists(current))
                    current = Path.GetDirectoryName(current) ?? string.Empty;
                targetDirectory = current;
            }
            if (!Directory.Exists(targetDirectory))
            {
                error = "The selected path has no existing parent directory inside the current request root.";
                return false;
            }
            if (ContainsNestedReparsePoint(targetDirectory, containingRoot))
            {
                error = "The selected path crosses a reparse point below the allowed root.";
                return false;
            }
            if (isFile && IsReparsePoint(candidate))
            {
                error = "The selected file is a reparse point.";
                return false;
            }

            selectedPath = candidate;
            return true;
        }

        public static (string SelectedPath, string RepositoryRoot) ResolveApprovalTarget(
            CopilotAgentRequest request,
            string? requestedPath)
        {
            var roots = GetAllowedRoots(request);
            if (TryResolveTargetPath(
                requestedPath, roots, requireExisting: true, out var selectedPath, out var directory, out var root, out _))
            {
                var repositoryRoot = FindRepositoryRoot(directory, root);
                return (selectedPath, string.IsNullOrWhiteSpace(repositoryRoot) ? "<not found>" : repositoryRoot);
            }

            return (string.IsNullOrWhiteSpace(requestedPath) ? "<current workspace root>" : requestedPath.Trim(), "<unresolved>");
        }

        private static bool TryResolvePotentialPath(
            string requestedPath,
            IReadOnlyList<string> allowedRoots,
            out string candidate,
            out string error)
        {
            candidate = string.Empty;
            error = string.Empty;
            var path = requestedPath.Trim();
            if (Path.IsPathRooted(path) && !Path.IsPathFullyQualified(path))
            {
                error = "The selected path must be workspace-relative or fully qualified.";
                return false;
            }
            if (!Path.IsPathFullyQualified(path) && allowedRoots.Count != 1)
            {
                error = "The workspace-relative selected path is ambiguous across multiple request roots.";
                return false;
            }

            try
            {
                candidate = Path.IsPathFullyQualified(path)
                    ? Path.GetFullPath(path)
                    : Path.GetFullPath(path, allowedRoots[0]);
                return true;
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                error = "The selected path is invalid.";
                return false;
            }
        }

        public static string FindRepositoryRoot(string startDirectory, string containingRoot)
        {
            var current = startDirectory;
            while (IsWithinRoot(current, containingRoot))
            {
                if (Directory.Exists(Path.Combine(current, ".git")) || File.Exists(Path.Combine(current, ".git")))
                    return current;
                if (string.Equals(current, containingRoot, StringComparison.OrdinalIgnoreCase))
                    break;
                current = Directory.GetParent(current)?.FullName ?? string.Empty;
                if (string.IsNullOrWhiteSpace(current))
                    break;
            }
            return string.Empty;
        }

        public static string GetRepositoryRelativePath(string repositoryRoot, string selectedPath)
        {
            if (string.Equals(repositoryRoot, selectedPath, StringComparison.OrdinalIgnoreCase))
                return string.Empty;
            try
            {
                var relative = Path.GetRelativePath(repositoryRoot, selectedPath);
                return relative is "." or ".."
                    || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                    || relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal)
                    ? string.Empty
                    : relative.Replace('\\', '/');
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return string.Empty;
            }
        }

        public static string? FindTrustedGitExecutable()
        {
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var candidates = new List<string>();
            if (!string.IsNullOrWhiteSpace(programFiles))
            {
                candidates.Add(Path.Combine(programFiles, "Git", "cmd", "git.exe"));
                candidates.Add(Path.Combine(programFiles, "Git", "bin", "git.exe"));
            }
            if (!string.IsNullOrWhiteSpace(programFilesX86))
                candidates.Add(Path.Combine(programFilesX86, "Git", "cmd", "git.exe"));
            if (!string.IsNullOrWhiteSpace(localAppData))
            {
                candidates.Add(Path.Combine(localAppData, "Programs", "Git", "cmd", "git.exe"));
                var githubDesktopRoot = Path.Combine(localAppData, "GitHubDesktop");
                try
                {
                    if (Directory.Exists(githubDesktopRoot))
                    {
                        candidates.AddRange(Directory.EnumerateDirectories(githubDesktopRoot, "app-*")
                            .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase)
                            .Select(path => Path.Combine(path, "resources", "app", "git", "cmd", "git.exe")));
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                }
            }
            return candidates.Select(NormalizeFile).FirstOrDefault(path => !string.IsNullOrWhiteSpace(path));
        }

        public static string NormalizeFile(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
                return string.Empty;
            try
            {
                var fullPath = Path.GetFullPath(path);
                return File.Exists(fullPath) ? fullPath : string.Empty;
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return string.Empty;
            }
        }

        private static bool ContainsNestedReparsePoint(string path, string root)
        {
            if (string.Equals(path, root, StringComparison.OrdinalIgnoreCase))
                return false;
            var relative = Path.GetRelativePath(root, path);
            var current = root;
            foreach (var segment in relative.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries))
            {
                current = Path.Combine(current, segment);
                if (IsReparsePoint(current))
                    return true;
            }
            return false;
        }

        private static bool IsReparsePoint(string path)
        {
            try
            {
                return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return true;
            }
        }

        private static bool IsWithinRoot(string path, string root)
        {
            if (string.Equals(path, root, StringComparison.OrdinalIgnoreCase))
                return true;
            var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar)
                ? root
                : root + Path.DirectorySeparatorChar;
            return path.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
        }
    }
}
