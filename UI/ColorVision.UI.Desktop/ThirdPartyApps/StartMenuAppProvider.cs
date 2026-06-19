using ColorVision.Common.ThirdPartyApps;
using ColorVision.UI;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace ColorVision.UI.Desktop.ThirdPartyApps
{
    /// <summary>
    /// Scans Windows Start Menu shortcuts so the launcher can show locally installed apps.
    /// </summary>
    public class StartMenuAppProvider : IThirdPartyAppCacheAwareProvider
    {
        private const int CacheVersion = 1;
        private static readonly TimeSpan CacheMaxAge = TimeSpan.FromHours(12);
        private static readonly JsonSerializerOptions CacheJsonOptions = new() { WriteIndented = true };
        private static string CacheFilePath => Path.Combine(Environments.DirStateDesktop, "ThirdPartyApps.StartMenu.cache.json");

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
        private static readonly string[] BrowserKeywords =
        {
            "browser", "chrome", "edge", "firefox", "opera", "brave", "vivaldi", "safari",
            "浏览器"
        };
        private static readonly string[] CommunicationKeywords =
        {
            "wechat", "weixin", "微信", "企业微信", "qq", "腾讯会议", "meeting", "teams",
            "dingtalk", "钉钉", "feishu", "飞书", "lark", "zoom", "skype"
        };
        private static readonly string[] OfficeKeywords =
        {
            "office", "word", "excel", "powerpoint", "onenote", "outlook", "access", "visio",
            "project", "wps", "pdf", "acrobat", "reader", "foxit", "福昕", "金山文档"
        };
        private static readonly string[] DevelopmentKeywords =
        {
            "visual studio", "vscode", "vs code", "code.exe", "git", "github", "node", "python",
            "pycharm", "idea", "rider", "webstorm", "postman", "docker", "mysql", "sql",
            "navicat", "terminal", "powershell", "notepad++"
        };
        private static readonly string[] DesignMediaKeywords =
        {
            "adobe", "photoshop", "illustrator", "premiere", "after effects", "audition",
            "lightroom", "blender", "cad", "matlab", "imagej", "media", "music", "video",
            "photo", "picture", "camera", "听歌", "图像", "视频", "音乐"
        };
        private static readonly string[] UtilityKeywords =
        {
            "everything", "winrar", "7-zip", "7zip", "zip", "rar", "sscom", "serial",
            "usb", "compare", "beyond compare", "remote", "desktop", "anydesk", "teamviewer",
            "sunlogin", "向日葵", "control panel", "settings", "tool", "utility", "工具"
        };

        public IEnumerable<ThirdPartyAppInfo> GetThirdPartyApps()
        {
            return GetThirdPartyApps(forceRefresh: false);
        }

        public IEnumerable<ThirdPartyAppInfo> GetThirdPartyApps(bool forceRefresh)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return Enumerable.Empty<ThirdPartyAppInfo>();

            if (!forceRefresh && TryLoadCachedApps(out var cachedApps))
                return cachedApps;

            var scannedApps = ScanStartMenuApps();
            SaveCachedApps(scannedApps);
            return scannedApps;
        }

        private static List<ThirdPartyAppInfo> ScanStartMenuApps()
        {
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

        private static bool TryLoadCachedApps(out List<ThirdPartyAppInfo> apps)
        {
            apps = new List<ThirdPartyAppInfo>();

            try
            {
                if (!File.Exists(CacheFilePath))
                    return false;

                using var stream = File.OpenRead(CacheFilePath);
                var cache = JsonSerializer.Deserialize<StartMenuAppsCache>(stream, CacheJsonOptions);
                if (cache == null
                    || cache.Version != CacheVersion
                    || cache.CreatedAt == default
                    || DateTimeOffset.UtcNow - cache.CreatedAt > CacheMaxAge
                    || !string.Equals(cache.CultureName, CultureInfo.CurrentUICulture.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                apps = cache.Apps
                    .Where(IsValidCacheEntry)
                    .Select(CreateAppFromCacheEntry)
                    .OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();

                return apps.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        private static void SaveCachedApps(IReadOnlyCollection<ThirdPartyAppInfo> apps)
        {
            try
            {
                string? cacheDirectory = Path.GetDirectoryName(CacheFilePath);
                if (!string.IsNullOrEmpty(cacheDirectory))
                    Directory.CreateDirectory(cacheDirectory);

                var cache = new StartMenuAppsCache
                {
                    Version = CacheVersion,
                    CreatedAt = DateTimeOffset.UtcNow,
                    CultureName = CultureInfo.CurrentUICulture.Name,
                    Apps = apps.Select(CreateCacheEntry).ToList(),
                };

                using var stream = File.Create(CacheFilePath);
                JsonSerializer.Serialize(stream, cache, CacheJsonOptions);
            }
            catch
            {
            }
        }

        private static bool IsValidCacheEntry(StartMenuAppCacheEntry entry)
        {
            return !string.IsNullOrWhiteSpace(entry.Name)
                   && !string.IsNullOrWhiteSpace(entry.Group)
                   && !string.IsNullOrWhiteSpace(entry.LaunchPath)
                   && File.Exists(entry.LaunchPath);
        }

        private static ThirdPartyAppInfo CreateAppFromCacheEntry(StartMenuAppCacheEntry entry)
        {
            string? iconPath = !string.IsNullOrWhiteSpace(entry.IconPath) ? entry.IconPath : null;
            return new ThirdPartyAppInfo
            {
                Name = entry.Name,
                Group = entry.Group,
                Order = entry.Order,
                LaunchPath = entry.LaunchPath,
                InstalledExePath = File.Exists(entry.InstalledExePath) ? entry.InstalledExePath : null,
                InstallDirectory = Directory.Exists(entry.InstallDirectory) ? entry.InstallDirectory : null,
                GetIconPath = () => iconPath,
            };
        }

        private static StartMenuAppCacheEntry CreateCacheEntry(ThirdPartyAppInfo app)
        {
            return new StartMenuAppCacheEntry
            {
                Name = app.Name,
                Group = app.Group,
                Order = app.Order,
                LaunchPath = app.LaunchPath ?? string.Empty,
                InstalledExePath = app.InstalledExePath ?? string.Empty,
                InstallDirectory = app.InstallDirectory ?? string.Empty,
                IconPath = app.GetIconPath?.Invoke() ?? string.Empty,
            };
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

            if (IsHiddenManagerShortcut(name, shortcut))
                return false;

            if (!IsRunnableTarget(shortcut.TargetPath))
                return false;

            if (IsMaintenanceShortcut(shortcut.TargetPath))
                return false;

            string? targetPath = ResolveExistingPath(shortcut.TargetPath);
            string? iconPath = ResolveIconPath(shortcut.IconLocation) ?? targetPath;
            string? installDirectory = GetInstallDirectory(shortcut, targetPath);
            var category = GetLocalAppCategory(name, shortcutPath, shortcut, targetPath);

            var app = new ThirdPartyAppInfo
            {
                Name = name,
                Group = category.Group,
                Order = category.Order,
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

        private static bool IsHiddenManagerShortcut(string name, ShortcutInfo shortcut)
        {
            if (shortcut.TargetPath.Contains("services.msc", StringComparison.OrdinalIgnoreCase)
                || shortcut.Arguments.Contains("services.msc", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return name.Equals("Services", StringComparison.OrdinalIgnoreCase)
                   || name.Equals("服务", StringComparison.OrdinalIgnoreCase)
                   || name.Equals("服务管理器", StringComparison.OrdinalIgnoreCase);
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

        private static LocalAppCategory GetLocalAppCategory(
            string name,
            string shortcutPath,
            ShortcutInfo shortcut,
            string? targetPath)
        {
            string parentName = Directory.GetParent(shortcutPath)?.Name ?? string.Empty;
            string text = $"{name} {parentName} {shortcut.TargetPath} {shortcut.Arguments} {targetPath}";

            if (ContainsAny(text, BrowserKeywords))
                return new LocalAppCategory(GetLocalAppsGroupName("浏览器", "Browsers"), 510);

            if (ContainsAny(text, CommunicationKeywords))
                return new LocalAppCategory(GetLocalAppsGroupName("通讯会议", "Communication"), 520);

            if (ContainsAny(text, OfficeKeywords))
                return new LocalAppCategory(GetLocalAppsGroupName("办公文档", "Office"), 530);

            if (ContainsAny(text, DevelopmentKeywords))
                return new LocalAppCategory(GetLocalAppsGroupName("开发数据", "Development"), 540);

            if (ContainsAny(text, DesignMediaKeywords))
                return new LocalAppCategory(GetLocalAppsGroupName("设计媒体", "Design & Media"), 550);

            if (ContainsAny(text, UtilityKeywords))
                return new LocalAppCategory(GetLocalAppsGroupName("工具", "Utilities"), 560);

            return new LocalAppCategory(GetLocalAppsGroupName("其他", "Other"), 590);
        }

        private static bool ContainsAny(string text, IEnumerable<string> keywords)
        {
            return keywords.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        private static string GetLocalAppsGroupName(string zhSuffix, string enSuffix)
        {
            string suffix = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase)
                ? zhSuffix
                : enSuffix;

            return $"{Properties.Resources.CustomApp_LocalApps} - {suffix}";
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

        private sealed record LocalAppCategory(string Group, int Order);

        private sealed class StartMenuAppsCache
        {
            public int Version { get; set; }
            public DateTimeOffset CreatedAt { get; set; }
            public string CultureName { get; set; } = string.Empty;
            public List<StartMenuAppCacheEntry> Apps { get; set; } = new();
        }

        private sealed class StartMenuAppCacheEntry
        {
            public string Name { get; set; } = string.Empty;
            public string Group { get; set; } = string.Empty;
            public int Order { get; set; }
            public string LaunchPath { get; set; } = string.Empty;
            public string InstalledExePath { get; set; } = string.Empty;
            public string InstallDirectory { get; set; } = string.Empty;
            public string IconPath { get; set; } = string.Empty;
        }
    }
}
