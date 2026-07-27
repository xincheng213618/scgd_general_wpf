using Newtonsoft.Json;
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
            string directory = Path.GetDirectoryName(fullPath)!;
            Directory.CreateDirectory(directory);

            string temporaryPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
            try
            {
                File.WriteAllText(temporaryPath, Serialize(tasks));
                if (File.Exists(fullPath))
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
