using ColorVision.Scheduler.Properties;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.ObjectModel;
using System.IO;

namespace ColorVision.Scheduler
{
    /// <summary>
    /// Keeps the existing scheduler_tasks.json type metadata contract in one place.
    /// </summary>
    public static class SchedulerTaskSerializer
    {
        public static string Serialize(ObservableCollection<SchedulerInfo> tasks)
        {
            ArgumentNullException.ThrowIfNull(tasks);
            return JsonConvert.SerializeObject(tasks, Formatting.Indented, CreateSettings());
        }

        public static ObservableCollection<SchedulerInfo> Deserialize(string json)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(json);
            return JsonConvert.DeserializeObject<ObservableCollection<SchedulerInfo>>(json, CreateSettings())
                ?? new ObservableCollection<SchedulerInfo>();
        }

        public static ObservableCollection<SchedulerInfo> LoadFromFile(string filePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
            return Deserialize(File.ReadAllText(filePath));
        }

        public static void SaveToFile(string filePath, ObservableCollection<SchedulerInfo> tasks)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
            ArgumentNullException.ThrowIfNull(tasks);

            string fullPath = Path.GetFullPath(filePath);
            string? existing = ReadExistingText(fullPath);
            if (existing != null)
                ValidateDocument(fullPath, existing);
            else if (ReadExistingText(fullPath + ".bak") != null)
                throw new InvalidDataException(string.Format(Resources.Sched_TaskBackupRequiresRestore, fullPath + ".bak"));

            WriteAtomically(fullPath, Serialize(tasks), replace: existing != null);
        }

        internal static ObservableCollection<SchedulerInfo>? LoadForStartup(string filePath, out bool restoredBackup)
        {
            string fullPath = Path.GetFullPath(filePath);
            restoredBackup = false;
            string? primary = ReadExistingText(fullPath);
            if (primary != null)
                return ReadDefinitions(fullPath, primary);

            string backupPath = fullPath + ".bak";
            string? backup = ReadExistingText(backupPath);
            if (backup == null) return null;

            var tasks = ReadDefinitions(backupPath, backup);
            // Another instance may have already restored or written the primary.
            // Creating without overwrite protects that file and the original backup.
            WriteAtomically(fullPath, backup, replace: false);
            restoredBackup = true;
            return tasks;
        }

        private static string? ReadExistingText(string path)
        {
            try { return File.ReadAllText(path); }
            catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException) { return null; }
        }

        private static ObservableCollection<SchedulerInfo> ReadDefinitions(string path, string json)
        {
            try
            {
                var tasks = JsonConvert.DeserializeObject<ObservableCollection<SchedulerInfo>>(json, CreateSettings());
                return tasks != null && tasks.All(task => task != null)
                    ? tasks
                    : throw new InvalidDataException(string.Format(Resources.Sched_TaskFileUnreadable, path));
            }
            catch (JsonException error)
            {
                throw new InvalidDataException(string.Format(Resources.Sched_TaskFileUnreadable, path), error);
            }
        }

        private static void ValidateDocument(string path, string json)
        {
            try
            {
                // Saving does not need to construct existing job configuration objects.
                JToken document = JToken.Parse(json);
                JArray? tasks = document as JArray ?? (document as JObject)?["$values"] as JArray;
                if (tasks == null || tasks.Any(task => task is not JObject))
                    throw new InvalidDataException(string.Format(Resources.Sched_TaskFileUnreadable, path));
            }
            catch (JsonException error)
            {
                throw new InvalidDataException(string.Format(Resources.Sched_TaskFileUnreadable, path), error);
            }
        }

        private static void WriteAtomically(string fullPath, string contents, bool replace)
        {
            string directory = Path.GetDirectoryName(fullPath)!;
            Directory.CreateDirectory(directory);

            string temporaryPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
            try
            {
                File.WriteAllText(temporaryPath, contents);
                if (replace)
                {
                    File.Replace(temporaryPath, fullPath, fullPath + ".bak", ignoreMetadataErrors: true);
                }
                else
                {
                    File.Move(temporaryPath, fullPath);
                }
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }

        private static JsonSerializerSettings CreateSettings()
        {
            return new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.All,
            };
        }
    }
}
