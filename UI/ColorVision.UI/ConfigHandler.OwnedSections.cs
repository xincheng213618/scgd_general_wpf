using Newtonsoft.Json.Linq;
using System.IO;

namespace ColorVision.UI
{
    public partial class ConfigHandler
    {
        /// <summary>Reads a section managed directly by its owning component without registering an IConfig snapshot.</summary>
        public JObject? ReadOwnedSection(string sectionName)
        {
            string fullPath = Path.GetFullPath(ConfigFilePath);
            using var fileLock = ConfigFileStore.AcquireLock(fullPath);
            return ReadSection(ConfigFileStore.ReadForMerge(fullPath), sectionName);
        }

        /// <summary>
        /// Updates an owned section using the latest on-disk state. The callback returns true to commit the change.
        /// </summary>
        public bool TryUpdateOwnedSection(string sectionName, Func<JObject, bool> update, out string error)
        {
            try
            {
                string fullPath = Path.GetFullPath(ConfigFilePath);
                using var fileLock = ConfigFileStore.AcquireLock(fullPath);
                JObject document = ConfigFileStore.ReadForMerge(fullPath);
                JObject section = ReadSection(document, sectionName) ?? new JObject();
                if (update(section))
                {
                    document[sectionName] = section;
                    ConfigFileStore.Write(fullPath, document);
                    if (log.IsInfoEnabled)
                        LogPersistedConfigChanges(fullPath, [sectionName]);
                }

                error = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                log.Error($"Configuration section '{sectionName}' could not be saved.", ex);
                error = ex.GetBaseException().Message;
                return false;
            }
        }

        private static JObject? ReadSection(JObject document, string sectionName) => document[sectionName] switch
        {
            null => null,
            JObject section => section,
            _ => throw new InvalidDataException($"Configuration section '{sectionName}' is not a JSON object."),
        };
    }
}
