using ColorVision.UI.Plugins;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ColorVision.Recovery
{
    public static class StartupRecoveryPluginScanner
    {
        private static readonly EnumerationOptions PluginFileEnumerationOptions = new()
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
            ReturnSpecialDirectories = false,
        };

        public static IReadOnlyList<StartupRecoveryPluginItem> Scan(
            string pluginsDirectory,
            StartupFailureInfo? previousFailure = null)
        {
            if (string.IsNullOrWhiteSpace(pluginsDirectory))
                return Array.Empty<StartupRecoveryPluginItem>();

            string fullPluginsDirectory;
            try
            {
                fullPluginsDirectory = Path.GetFullPath(pluginsDirectory);
            }
            catch
            {
                return Array.Empty<StartupRecoveryPluginItem>();
            }

            IReadOnlyDictionary<string, PluginInfo> configuredPlugins =
                PluginLoaderrConfig.Instance.Plugins;
            List<StartupRecoveryPluginItem> items = new();

            if (Directory.Exists(fullPluginsDirectory))
            {
                foreach (string directory in Directory.EnumerateDirectories(fullPluginsDirectory))
                    items.Add(CreateItem(directory, configuredPlugins));
            }

            AddMissingPluginBackups(items, configuredPlugins, fullPluginsDirectory);
            ApplySuspectedLabels(items, previousFailure);
            return items
                .OrderByDescending(item => item.IsSuspected)
                .ThenByDescending(item => item.LastWriteTime)
                .ThenBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }

        private static StartupRecoveryPluginItem CreateItem(
            string directory,
            IReadOnlyDictionary<string, PluginInfo> configuredPlugins)
        {
            string directoryName = Path.GetFileName(
                directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            string manifestPath = Path.Combine(directory, "manifest.json");
            PluginManifest? manifest = null;
            bool invalidManifest = false;

            if (File.Exists(manifestPath))
            {
                try
                {
                    manifest = JsonConvert.DeserializeObject<PluginManifest>(File.ReadAllText(manifestPath));
                    invalidManifest = manifest == null || string.IsNullOrWhiteSpace(manifest.Id);
                }
                catch
                {
                    invalidManifest = true;
                }
            }

            string? pluginId = !invalidManifest && !string.IsNullOrWhiteSpace(manifest?.Id)
                ? manifest.Id.Trim()
                : null;
            string pluginKey = pluginId ?? directoryName;
            PluginInfo? configuredPlugin = FindConfiguredPlugin(configuredPlugins, pluginKey, directoryName);

            return new StartupRecoveryPluginItem
            {
                PluginKey = pluginKey,
                PluginId = pluginId,
                DirectoryName = directoryName,
                DirectoryPath = Path.GetFullPath(directory),
                DisplayName = !string.IsNullOrWhiteSpace(manifest?.Name) ? manifest.Name.Trim() : directoryName,
                VersionText = !string.IsNullOrWhiteSpace(manifest?.Version)
                    ? manifest.Version.Trim()
                    : invalidManifest ? "无法读取" : "未知",
                IsEnabled = configuredPlugin?.Enabled ?? true,
                LastWriteTime = GetLatestWriteTime(directory),
                IsLegacy = !File.Exists(manifestPath) || invalidManifest,
                HasInvalidManifest = invalidManifest,
                IsSuspected = false,
            };
        }

        private static void AddMissingPluginBackups(
            List<StartupRecoveryPluginItem> items,
            IReadOnlyDictionary<string, PluginInfo> configuredPlugins,
            string pluginsDirectory)
        {
            string? programDirectory = Directory.GetParent(
                pluginsDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))?.FullName;
            if (string.IsNullOrWhiteSpace(programDirectory))
                return;

            IReadOnlyList<PluginRecoveryBackupInfo> backups;
            try
            {
                backups = PluginRecoveryBackupService.Instance.GetRecoveryBackupCandidates(programDirectory);
            }
            catch
            {
                // An inaccessible backup store must not hide plugins that are still installed.
                return;
            }

            foreach (PluginRecoveryBackupInfo backup in backups)
            {
                StartupRecoveryPluginItem? existingDirectoryItem = FindPluginByDirectory(
                    items,
                    backup.PluginDirectory);
                if (existingDirectoryItem != null)
                {
                    existingDirectoryItem.SetBackup(backup);
                    continue;
                }

                if (Directory.Exists(backup.PluginDirectory) ||
                    items.Any(item => string.Equals(
                        item.PluginId,
                        backup.PluginId,
                        StringComparison.OrdinalIgnoreCase)))
                    continue;

                string directoryName = Path.GetFileName(backup.PluginDirectory.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar));
                if (string.IsNullOrWhiteSpace(directoryName))
                    directoryName = backup.PluginId;

                PluginInfo? configuredPlugin = FindConfiguredPlugin(
                    configuredPlugins,
                    backup.PluginId,
                    directoryName);
                StartupRecoveryPluginItem item = new()
                {
                    PluginKey = backup.PluginId,
                    PluginId = backup.PluginId,
                    DirectoryName = directoryName,
                    DirectoryPath = backup.PluginDirectory,
                    DisplayName = backup.PluginName,
                    VersionText = string.IsNullOrWhiteSpace(backup.Version) ? "未知" : backup.Version,
                    IsEnabled = configuredPlugin?.Enabled ?? true,
                    LastWriteTime = backup.CreatedUtc.LocalDateTime,
                    IsLegacy = backup.Manifest == null,
                    HasInvalidManifest = false,
                    IsBackupOnly = true,
                    IsSuspected = false,
                };
                item.SetBackup(backup);
                items.Add(item);
            }
        }

        private static StartupRecoveryPluginItem? FindPluginByDirectory(
            IEnumerable<StartupRecoveryPluginItem> items,
            string pluginDirectory)
        {
            string backupDirectory = Path.GetFullPath(pluginDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return items.FirstOrDefault(item =>
                string.Equals(
                    item.DirectoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    backupDirectory,
                    StringComparison.OrdinalIgnoreCase));
        }

        private static PluginInfo? FindConfiguredPlugin(
            IReadOnlyDictionary<string, PluginInfo> configuredPlugins,
            string pluginKey,
            string directoryName)
        {
            if (configuredPlugins.TryGetValue(pluginKey, out PluginInfo? configuredPlugin))
                return configuredPlugin;

            return configuredPlugins.FirstOrDefault(pair =>
                    string.Equals(pair.Key, pluginKey, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(pair.Key, directoryName, StringComparison.OrdinalIgnoreCase))
                .Value;
        }

        private static DateTime GetLatestWriteTime(string directory)
        {
            DateTime latestUtc;
            try
            {
                latestUtc = Directory.GetLastWriteTimeUtc(directory);
            }
            catch
            {
                return DateTime.MinValue;
            }

            try
            {
                foreach (string file in Directory.EnumerateFiles(directory, "*", PluginFileEnumerationOptions))
                {
                    try
                    {
                        DateTime writeTimeUtc = File.GetLastWriteTimeUtc(file);
                        if (writeTimeUtc > latestUtc)
                            latestUtc = writeTimeUtc;
                    }
                    catch
                    {
                        // A single inaccessible file must not hide the rest of the plugin inventory.
                    }
                }
            }
            catch
            {
                // Directory metadata is still useful when recursive enumeration is unavailable.
            }

            return latestUtc == DateTime.MinValue ? DateTime.MinValue : latestUtc.ToLocalTime();
        }

        private static void ApplySuspectedLabels(
            List<StartupRecoveryPluginItem> items,
            StartupFailureInfo? previousFailure)
        {
            if (previousFailure == null || items.Count == 0)
                return;

            if (!string.IsNullOrWhiteSpace(previousFailure.Component))
            {
                StartupRecoveryPluginItem? matchingItem = items.FirstOrDefault(item =>
                    string.Equals(item.PluginKey, previousFailure.Component, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(item.PluginId, previousFailure.Component, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(item.DirectoryName, previousFailure.Component, StringComparison.OrdinalIgnoreCase));
                if (matchingItem != null)
                {
                    matchingItem.IsSuspected = true;
                    return;
                }
            }

            if (previousFailure.Stage?.Contains("plugin", StringComparison.OrdinalIgnoreCase) != true)
                return;

            StartupRecoveryPluginItem? latestEnabledPlugin = items
                .Where(item => item.IsEnabled && item.LastWriteTime != DateTime.MinValue)
                .OrderByDescending(item => item.LastWriteTime)
                .FirstOrDefault();
            if (latestEnabledPlugin != null)
                latestEnabledPlugin.IsSuspected = true;
        }
    }
}
