using ColorVision.Scheduler.Properties;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.ObjectModel;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

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
            => SaveToFile(filePath, tasks, checkRevision: false, loadedRevision: null);

        internal static string SaveLoadedFile(string filePath, ObservableCollection<SchedulerInfo> tasks, string? loadedRevision)
            => SaveToFile(filePath, tasks, checkRevision: true, loadedRevision);

        private static string SaveToFile(string filePath, ObservableCollection<SchedulerInfo> tasks, bool checkRevision, string? loadedRevision)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
            ArgumentNullException.ThrowIfNull(tasks);

            string fullPath = Path.GetFullPath(filePath);
            return WithFileLock(fullPath, () =>
            {
                // Block ordinary writes to the checked file until it is replaced.
                using FileStream? primary = OpenExistingFile(fullPath);
                string? existing = ReadText(primary);
                string? currentRevision = existing == null ? null : ComputeRevision(existing);
                if (checkRevision && !string.Equals(loadedRevision, currentRevision, StringComparison.Ordinal))
                    throw new IOException(string.Format(Resources.Sched_TaskFileChanged, fullPath));
                if (existing != null)
                    ValidateDocument(fullPath, existing);
                else if (ReadExistingText(fullPath + ".bak") != null)
                    throw new InvalidDataException(string.Format(Resources.Sched_TaskBackupRequiresRestore, fullPath + ".bak"));

                string contents = Serialize(tasks);
                WriteAtomically(fullPath, contents, replace: existing != null);
                return ComputeRevision(contents);
            });
        }

        internal static (ObservableCollection<SchedulerInfo>? Tasks, string? Revision, bool RestoredBackup) LoadForStartup(string filePath)
        {
            string fullPath = Path.GetFullPath(filePath);
            return WithFileLock<(ObservableCollection<SchedulerInfo>?, string?, bool)>(fullPath, () =>
            {
                string? primary = ReadExistingText(fullPath);
                if (primary != null)
                    return (ReadDefinitions(fullPath, primary), ComputeRevision(primary), false);

                string backupPath = fullPath + ".bak";
                string? backup = ReadExistingText(backupPath);
                if (backup == null) return default;

                var tasks = ReadDefinitions(backupPath, backup);
                // Creating without overwrite protects any primary written by an external tool.
                WriteAtomically(fullPath, backup, replace: false);
                return (tasks, ComputeRevision(backup), true);
            });
        }

        private static T WithFileLock<T>(string fullPath, Func<T> operation)
        {
            // The loaded revision and atomic replacement share a lock across app instances.
            using var mutex = new Mutex(false, @"Local\ColorVision.Scheduler.Definitions." + ComputeRevision(fullPath.ToUpperInvariant()));
            try
            {
                if (!mutex.WaitOne(TimeSpan.FromSeconds(2)))
                    throw new IOException(string.Format(Resources.Sched_TaskFileBusy, fullPath));
            }
            catch (AbandonedMutexException)
            {
                // Ownership transferred. The existing file is still checked below.
            }
            try { return operation(); }
            finally { mutex.ReleaseMutex(); }
        }

        private static string? ReadExistingText(string path)
        {
            using FileStream? stream = OpenExistingFile(path);
            return ReadText(stream);
        }

        private static FileStream? OpenExistingFile(string path)
        {
            try { return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete); }
            catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException) { return null; }
        }

        private static string? ReadText(FileStream? stream)
        {
            if (stream == null) return null;
            using var reader = new StreamReader(stream, leaveOpen: true);
            return reader.ReadToEnd();
        }

        private static string ComputeRevision(string contents)
            => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(contents)));

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
