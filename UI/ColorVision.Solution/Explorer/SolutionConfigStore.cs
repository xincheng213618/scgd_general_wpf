using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;

namespace ColorVision.Solution.Explorer
{
    internal sealed record SolutionConfigLoadResult(
        SolutionConfig Config,
        int SourceSchemaVersion,
        bool RecoveredFromBackup,
        string? CorruptCopyPath);

    /// <summary>
    /// Owns the durable .cvsln boundary: schema migration, normalization,
    /// atomic replacement, backup creation, and recovery from malformed JSON.
    /// </summary>
    internal static class SolutionConfigStore
    {
        public const int CurrentSchemaVersion = 4;

        public static SolutionConfigLoadResult Load(string filePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
            try
            {
                string json = File.ReadAllText(filePath);
                SolutionConfig config = DeserializeAndMigrate(json, out int sourceVersion);
                return new SolutionConfigLoadResult(config, sourceVersion, false, null);
            }
            catch (Exception primaryException) when (CanRecoverFromBackup(primaryException))
            {
                string backupPath = GetBackupPath(filePath);
                if (!File.Exists(backupPath))
                {
                    throw new InvalidDataException(
                        $"解决方案配置“{filePath}”无法读取，且没有可用备份。",
                        primaryException);
                }

                try
                {
                    string backupJson = File.ReadAllText(backupPath);
                    SolutionConfig config = DeserializeAndMigrate(backupJson, out int sourceVersion);
                    string? corruptCopyPath = TryArchiveCorruptPrimary(filePath);
                    WriteContentAtomically(filePath, Serialize(config), createBackup: false);
                    return new SolutionConfigLoadResult(config, sourceVersion, true, corruptCopyPath);
                }
                catch (Exception backupException) when (backupException is IOException
                    or UnauthorizedAccessException
                    or JsonException
                    or InvalidDataException
                    or ArgumentException
                    or NotSupportedException)
                {
                    throw new InvalidDataException(
                        $"解决方案配置“{filePath}”及其备份都无法读取。",
                        new AggregateException(primaryException, backupException));
                }
            }
        }

        public static SolutionConfig DeserializeAndMigrate(string json, out int sourceSchemaVersion)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new InvalidDataException("解决方案配置为空。");

            JObject root = JObject.Parse(json);
            sourceSchemaVersion = GetValue(root, nameof(SolutionConfig.SchemaVersion))?.Value<int?>() ?? 0;
            if (sourceSchemaVersion < 0)
                throw new InvalidDataException($"解决方案 SchemaVersion 无效：{sourceSchemaVersion}。");
            if (sourceSchemaVersion > CurrentSchemaVersion)
            {
                throw new NotSupportedException(
                    $"解决方案使用较新的 SchemaVersion {sourceSchemaVersion}，当前仅支持到 {CurrentSchemaVersion}。");
            }

            int version = sourceSchemaVersion;
            while (version < CurrentSchemaVersion)
            {
                switch (version)
                {
                    case 0:
                        MigrateVersion0To1(root);
                        version = 1;
                        break;
                    case 1:
                        EnsureProperty(root, nameof(SolutionConfig.SolutionFolders), new JArray());
                        EnsureProperty(root, nameof(SolutionConfig.ProjectSolutionFolders), new JObject());
                        version = 2;
                        break;
                    case 2:
                        EnsureProperty(root, nameof(SolutionConfig.SolutionItems), new JArray());
                        version = 3;
                        break;
                    case 3:
                        EnsureProperty(
                            root,
                            nameof(SolutionConfig.ActivePlatform),
                            SolutionConfigurationIdentity.DefaultPlatform);
                        version = 4;
                        break;
                    default:
                        throw new InvalidDataException($"没有从 SchemaVersion {version} 开始的迁移路径。");
                }
                SetProperty(root, nameof(SolutionConfig.SchemaVersion), version);
            }

            SolutionConfig config = root.ToObject<SolutionConfig>()
                ?? throw new InvalidDataException("解决方案配置无法反序列化。");
            Normalize(config);
            return config;
        }

        public static void Save(string filePath, SolutionConfig config)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
            ArgumentNullException.ThrowIfNull(config);
            WriteContentAtomically(filePath, Serialize(config), createBackup: true);
        }

        public static string Serialize(SolutionConfig config)
        {
            Normalize(config);
            string json = JsonConvert.SerializeObject(config, Formatting.Indented);
            _ = JToken.Parse(json);
            return json;
        }

        public static void Normalize(SolutionConfig config)
        {
            config.SchemaVersion = CurrentSchemaVersion;
            config.RootPath ??= string.Empty;
            config.StartupProject ??= string.Empty;
            config.ActiveConfiguration = string.IsNullOrWhiteSpace(config.ActiveConfiguration)
                ? SolutionConfigurationIdentity.DefaultConfiguration
                : config.ActiveConfiguration.Trim();
            config.ActivePlatform = SolutionConfigurationIdentity.NormalizePlatform(config.ActivePlatform);
            config.Projects ??= new ObservableCollection<string>();
            foreach (string reference in config.Projects
                .Where(reference => string.IsNullOrWhiteSpace(reference))
                .ToList())
            {
                config.Projects.Remove(reference);
            }
            for (int index = config.Projects.Count - 1; index >= 0; index--)
            {
                string reference = config.Projects[index].Trim();
                if (config.Projects.Take(index).Any(existing => string.Equals(
                    existing.Trim(),
                    reference,
                    StringComparison.OrdinalIgnoreCase)))
                {
                    config.Projects.RemoveAt(index);
                }
                else
                {
                    config.Projects[index] = reference;
                }
            }

            config.ProjectConfigurations ??= new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            config.ProjectConfigurations = config.ProjectConfigurations
                .Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
                .GroupBy(pair => pair.Key.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => new Dictionary<string, string>(
                        group.Last().Value ?? new Dictionary<string, string>(),
                        StringComparer.OrdinalIgnoreCase),
                    StringComparer.OrdinalIgnoreCase);
            config.SolutionFolders ??= new ObservableCollection<SolutionFolderDefinition>();
            config.ProjectSolutionFolders ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            config.SolutionItems ??= new ObservableCollection<SolutionItemDefinition>();

            foreach (SolutionFolderDefinition? nullFolder in config.SolutionFolders
                .Where(folder => folder == null)
                .ToList())
            {
                config.SolutionFolders.Remove(nullFolder!);
            }
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (SolutionFolderDefinition folder in config.SolutionFolders)
            {
                string id = folder.Id?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(id) || !ids.Add(id))
                {
                    do
                    {
                        id = Guid.NewGuid().ToString("N");
                    }
                    while (!ids.Add(id));
                }
                folder.Id = id;
                folder.Name = string.IsNullOrWhiteSpace(folder.Name)
                    ? "解决方案文件夹"
                    : folder.Name.Trim();
                folder.ParentId = string.IsNullOrWhiteSpace(folder.ParentId)
                    ? null
                    : folder.ParentId.Trim();
            }

            var foldersById = config.SolutionFolders.ToDictionary(
                folder => folder.Id,
                StringComparer.OrdinalIgnoreCase);
            foreach (SolutionFolderDefinition folder in config.SolutionFolders)
            {
                if (string.Equals(folder.ParentId, folder.Id, StringComparison.OrdinalIgnoreCase)
                    || (folder.ParentId != null && !foldersById.ContainsKey(folder.ParentId)))
                {
                    folder.ParentId = null;
                    continue;
                }

                var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { folder.Id };
                string? ancestorId = folder.ParentId;
                while (ancestorId != null && foldersById.TryGetValue(ancestorId, out SolutionFolderDefinition? ancestor))
                {
                    if (!visited.Add(ancestor.Id))
                    {
                        folder.ParentId = null;
                        break;
                    }
                    ancestorId = ancestor.ParentId;
                }
            }

            config.ProjectSolutionFolders = config.ProjectSolutionFolders
                .Where(pair => !string.IsNullOrWhiteSpace(pair.Key)
                    && !string.IsNullOrWhiteSpace(pair.Value)
                    && foldersById.ContainsKey(pair.Value))
                .GroupBy(pair => pair.Key.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.Last().Value.Trim(),
                    StringComparer.OrdinalIgnoreCase);

            foreach (SolutionItemDefinition? nullItem in config.SolutionItems
                .Where(item => item == null)
                .ToList())
            {
                config.SolutionItems.Remove(nullItem!);
            }
            var solutionItemIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var solutionItemPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (SolutionItemDefinition item in config.SolutionItems.ToList())
            {
                item.Path = item.Path?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(item.Path) || !solutionItemPaths.Add(item.Path))
                {
                    config.SolutionItems.Remove(item);
                    continue;
                }

                string id = item.Id?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(id) || !solutionItemIds.Add(id))
                {
                    do
                    {
                        id = Guid.NewGuid().ToString("N");
                    }
                    while (!solutionItemIds.Add(id));
                }
                item.Id = id;
                item.SolutionFolderId = string.IsNullOrWhiteSpace(item.SolutionFolderId)
                    || !foldersById.ContainsKey(item.SolutionFolderId)
                        ? null
                        : item.SolutionFolderId.Trim();
            }
        }

        public static string GetBackupPath(string filePath) => $"{filePath}.bak";

        private static void MigrateVersion0To1(JObject root)
        {
            if (GetValue(root, nameof(SolutionConfig.ProjectMode)) == null)
            {
                bool hasProjects = GetValue(root, nameof(SolutionConfig.Projects)) is JArray projects
                    && projects.Any(token => token.Type == JTokenType.String && !string.IsNullOrWhiteSpace(token.Value<string>()));
                SetProperty(
                    root,
                    nameof(SolutionConfig.ProjectMode),
                    hasProjects ? nameof(SolutionProjectMode.Explicit) : nameof(SolutionProjectMode.AutoDiscover));
            }
            EnsureProperty(root, nameof(SolutionConfig.StartupProject), string.Empty);
            EnsureProperty(root, nameof(SolutionConfig.ActiveConfiguration), "Debug");
            EnsureProperty(root, nameof(SolutionConfig.ProjectConfigurations), new JObject());
        }

        private static bool CanRecoverFromBackup(Exception exception)
        {
            return exception is IOException
                or UnauthorizedAccessException
                or JsonException
                or InvalidDataException;
        }

        private static JToken? GetValue(JObject root, string name) =>
            root.GetValue(name, StringComparison.OrdinalIgnoreCase);

        private static void EnsureProperty(JObject root, string name, JToken value)
        {
            if (GetValue(root, name) == null)
                root[name] = value;
        }

        private static void SetProperty(JObject root, string name, JToken value)
        {
            JProperty? existing = root.Properties().FirstOrDefault(property => string.Equals(
                property.Name,
                name,
                StringComparison.OrdinalIgnoreCase));
            if (existing == null)
                root[name] = value;
            else
                existing.Value = value;
        }

        private static string? TryArchiveCorruptPrimary(string filePath)
        {
            if (!File.Exists(filePath))
                return null;
            string archivePath = $"{filePath}.corrupt-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}";
            try
            {
                File.Copy(filePath, archivePath, overwrite: false);
                return archivePath;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return null;
            }
        }

        private static void WriteContentAtomically(string filePath, string content, bool createBackup)
        {
            string fullPath = Path.GetFullPath(filePath);
            string? directoryPath = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
                throw new DirectoryNotFoundException($"解决方案目录不存在：{directoryPath}");

            string temporaryPath = $"{fullPath}.{Guid.NewGuid():N}.tmp";
            try
            {
                using (var stream = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 4096,
                    FileOptions.WriteThrough))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
                {
                    writer.Write(content);
                    writer.Flush();
                    stream.Flush(flushToDisk: true);
                }

                if (!File.Exists(fullPath))
                {
                    File.Move(temporaryPath, fullPath);
                    return;
                }
                if (!createBackup)
                {
                    File.Move(temporaryPath, fullPath, overwrite: true);
                    return;
                }

                string backupPath = GetBackupPath(fullPath);
                try
                {
                    File.Replace(temporaryPath, fullPath, backupPath, ignoreMetadataErrors: true);
                }
                catch (PlatformNotSupportedException)
                {
                    File.Copy(fullPath, backupPath, overwrite: true);
                    File.Move(temporaryPath, fullPath, overwrite: true);
                }
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }
    }
}
