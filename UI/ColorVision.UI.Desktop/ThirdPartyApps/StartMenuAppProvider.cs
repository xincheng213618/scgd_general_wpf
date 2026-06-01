using ColorVision.Common.ThirdPartyApps;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace ColorVision.UI.Desktop.ThirdPartyApps
{
    /// <summary>
    /// Scans Windows Start Menu shortcuts so the launcher can show locally installed apps.
    /// </summary>
    public class StartMenuAppProvider : IThirdPartyAppProvider
    {
        private static readonly string[] ShortcutExtensions = { ".lnk", ".appref-ms" };
        private static readonly string[] RunnableTargetExtensions =
        {
            ".exe", ".com", ".bat", ".cmd", ".ps1", ".msc", ".cpl", ".appref-ms"
        };
        private static readonly string[] MaintenanceKeywords =
        {
            "uninstall", "uninstaller", "remove", "repair", "readme", "read me",
            "license", "manual", "documentation", "help",
            "卸载", "移除", "修复", "自述", "说明", "许可", "帮助", "文档"
        };

        public IEnumerable<ThirdPartyAppInfo> GetThirdPartyApps()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return Enumerable.Empty<ThirdPartyAppInfo>();

            var apps = new Dictionary<string, StartMenuAppCandidate>(StringComparer.OrdinalIgnoreCase);

            foreach (var shortcutPath in EnumerateShortcutFiles())
            {
                if (TryCreateApp(shortcutPath, out StartMenuAppCandidate? candidate) && candidate != null)
                {
                    if (!apps.TryGetValue(candidate.IdentityKey, out var existingCandidate)
                        || candidate.Score > existingCandidate.Score)
                    {
                        apps[candidate.IdentityKey] = candidate;
                    }
                }
            }

            return apps.Values
                .Select(candidate => candidate.App)
                .OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        private static IEnumerable<string> EnumerateShortcutFiles()
        {
            foreach (var root in GetStartMenuRoots())
            {
                foreach (var file in EnumerateFilesSafely(root))
                {
                    string extension = Path.GetExtension(file);
                    if (ShortcutExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                        yield return file;
                }
            }
        }

        private static IEnumerable<string> EnumerateFilesSafely(string root)
        {
            var pendingDirectories = new Stack<string>();
            pendingDirectories.Push(root);

            while (pendingDirectories.Count > 0)
            {
                string directory = pendingDirectories.Pop();

                IEnumerable<string> files;
                try
                {
                    files = Directory.EnumerateFiles(directory);
                }
                catch
                {
                    continue;
                }

                foreach (var file in files)
                    yield return file;

                IEnumerable<string> subDirectories;
                try
                {
                    subDirectories = Directory.EnumerateDirectories(directory);
                }
                catch
                {
                    continue;
                }

                foreach (var subDirectory in subDirectories)
                    pendingDirectories.Push(subDirectory);
            }
        }

        private static IEnumerable<string> GetStartMenuRoots()
        {
            var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddSpecialFolder(roots, Environment.SpecialFolder.StartMenu);
            AddSpecialFolder(roots, Environment.SpecialFolder.CommonStartMenu);

            foreach (var root in roots)
                yield return root;
        }

        private static void AddSpecialFolder(HashSet<string> roots, Environment.SpecialFolder folder)
        {
            string path = Environment.GetFolderPath(folder);
            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                roots.Add(path);
        }

        private static bool TryCreateApp(string shortcutPath, out StartMenuAppCandidate? candidate)
        {
            candidate = null;

            string name = Path.GetFileNameWithoutExtension(shortcutPath).Trim();
            if (string.IsNullOrWhiteSpace(name) || IsMaintenanceShortcut(name))
                return false;

            ShortcutInfo? shortcut = Path.GetExtension(shortcutPath).Equals(".lnk", StringComparison.OrdinalIgnoreCase)
                ? ReadShortcut(shortcutPath)
                : new ShortcutInfo(shortcutPath, string.Empty, string.Empty, shortcutPath);

            if (shortcut == null)
                return false;

            if (!IsRunnableTarget(shortcut.TargetPath))
                return false;

            if (IsMaintenanceShortcut(shortcut.TargetPath))
                return false;

            string? targetPath = ResolveExistingPath(shortcut.TargetPath);
            string? iconPath = ResolveIconPath(shortcut.IconLocation) ?? targetPath;
            string? installDirectory = GetInstallDirectory(shortcut, targetPath);

            var app = new ThirdPartyAppInfo
            {
                Name = name,
                Group = GetLocalAppsGroupName(),
                Order = 500,
                LaunchPath = shortcutPath,
                InstalledExePath = targetPath,
                InstallDirectory = installDirectory,
                GetIconPath = () => iconPath,
            };

            candidate = new StartMenuAppCandidate(
                app,
                BuildShortcutKey(app, shortcutPath, shortcut),
                GetShortcutScore(app, shortcutPath));

            return true;
        }

        private static bool IsRunnableTarget(string targetPath)
        {
            if (string.IsNullOrWhiteSpace(targetPath))
            {
                // Some packaged app shortcuts do not expose a classic target path but still launch correctly.
                return true;
            }

            string extension = Path.GetExtension(targetPath);
            return RunnableTargetExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
        }

        private static bool IsMaintenanceShortcut(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            return MaintenanceKeywords.Any(keyword =>
                text.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        private static ShortcutInfo? ReadShortcut(string shortcutPath)
        {
            object? shell = null;
            object? shortcut = null;

            try
            {
                Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null)
                    return null;

                shell = Activator.CreateInstance(shellType);
                if (shell == null)
                    return null;

                shortcut = shellType.InvokeMember(
                    "CreateShortcut",
                    BindingFlags.InvokeMethod,
                    null,
                    shell,
                    new object[] { shortcutPath },
                    CultureInfo.InvariantCulture);

                return new ShortcutInfo(
                    GetComString(shortcut, "TargetPath"),
                    GetComString(shortcut, "Arguments"),
                    GetComString(shortcut, "WorkingDirectory"),
                    GetComString(shortcut, "IconLocation"));
            }
            catch
            {
                return null;
            }
            finally
            {
                ReleaseComObject(shortcut);
                ReleaseComObject(shell);
            }
        }

        private static string GetComString(object? target, string propertyName)
        {
            if (target == null)
                return string.Empty;

            try
            {
                object? value = target.GetType().InvokeMember(
                    propertyName,
                    BindingFlags.GetProperty,
                    null,
                    target,
                    null,
                    CultureInfo.InvariantCulture);

                return value?.ToString() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static void ReleaseComObject(object? value)
        {
            if (value != null && Marshal.IsComObject(value))
                Marshal.ReleaseComObject(value);
        }

        private static string? ResolveIconPath(string iconLocation)
        {
            if (string.IsNullOrWhiteSpace(iconLocation))
                return null;

            string iconPath = iconLocation.Trim();
            if (iconPath.StartsWith('"'))
            {
                int endQuote = iconPath.IndexOf('"', 1);
                if (endQuote > 1)
                    iconPath = iconPath.Substring(1, endQuote - 1);
            }
            else
            {
                int commaIndex = iconPath.LastIndexOf(',');
                if (commaIndex > 0)
                    iconPath = iconPath.Substring(0, commaIndex);
            }

            return ResolveExistingPath(iconPath);
        }

        private static string? ResolveExistingPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            try
            {
                string expanded = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
                string fullPath = Path.GetFullPath(expanded);
                return File.Exists(fullPath) ? fullPath : null;
            }
            catch
            {
                return File.Exists(path) ? path : null;
            }
        }

        private static string? GetInstallDirectory(ShortcutInfo shortcut, string? targetPath)
        {
            if (!string.IsNullOrWhiteSpace(shortcut.WorkingDirectory) && Directory.Exists(shortcut.WorkingDirectory))
                return shortcut.WorkingDirectory;

            if (!string.IsNullOrWhiteSpace(targetPath))
                return Path.GetDirectoryName(targetPath);

            return null;
        }

        private static string BuildShortcutKey(ThirdPartyAppInfo app, string shortcutPath, ShortcutInfo shortcut)
        {
            string? path = app.InstalledExePath ?? app.LaunchPath;
            string normalizedPath = !string.IsNullOrWhiteSpace(path)
                ? NormalizeIdentityPath(path)
                : NormalizeIdentityPath(shortcutPath);

            return $"{normalizedPath}|args:{shortcut.Arguments}";
        }

        private static int GetShortcutScore(ThirdPartyAppInfo app, string shortcutPath)
        {
            int score = 0;
            string? parentName = Directory.GetParent(shortcutPath)?.Name;
            if (!string.IsNullOrWhiteSpace(parentName)
                && app.Name.Equals(parentName, StringComparison.OrdinalIgnoreCase))
            {
                score += 50;
            }

            if (!string.IsNullOrWhiteSpace(app.InstalledExePath))
            {
                string executableName = Path.GetFileNameWithoutExtension(app.InstalledExePath);
                if (app.Name.Equals(executableName, StringComparison.OrdinalIgnoreCase))
                    score += 20;
            }

            return score;
        }

        private static string NormalizeIdentityPath(string path)
        {
            try
            {
                return Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim().Trim('"')));
            }
            catch
            {
                return path;
            }
        }

        private static string GetLocalAppsGroupName()
        {
            return Properties.Resources.CustomApp_LocalApps;
        }

        private sealed record ShortcutInfo(
            string TargetPath,
            string Arguments,
            string WorkingDirectory,
            string IconLocation);

        private sealed record StartMenuAppCandidate(
            ThirdPartyAppInfo App,
            string IdentityKey,
            int Score);
    }
}
