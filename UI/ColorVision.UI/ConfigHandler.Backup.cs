using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using System.IO;

namespace ColorVision.UI
{
    public partial class ConfigHandler
    {
        public void LoadDefaultConfigs()
        {
            var transactionStarted = false;
            var stateChanged = false;
            try
            {
                BeginSaveTransaction();
                transactionStarted = true;
                LoadDefaultConfigsCore();
                stateChanged = true;
            }
            catch (Exception ex)
            {
                log.Error(Properties.Resources.RestoreConfigFileFailed, ex);
            }
            finally
            {
                if (transactionStarted)
                    EndSaveTransaction(stateChanged);
            }
        }

        private void LoadDefaultConfigsCore()
        {
            try
            {
                if (TryRestoreLatestBackup(out JObject backupJson))
                {
                    ApplyLoadedConfig(backupJson);
                    return;
                }
            }
            catch (Exception ex)
            {
                log.Error(Properties.Resources.RestoreConfigFileFailed, ex);
            }

            jsonObject = new JObject();
            Configs = CreateDefaultConfigInstances();
        }

        private bool TryRestoreLatestBackup(out JObject restoredJson)
        {
            restoredJson = new JObject();
            foreach (string backupFile in GetBackupFiles())
            {
                if (!ConfigFileStore.TryRead(backupFile, out var backupJson, ex => log.Warn(ex)))
                    continue;

                string fullPath = Path.GetFullPath(ConfigFilePath);
                using (ConfigFileStore.AcquireLock(fullPath))
                    ConfigFileStore.Write(fullPath, backupJson);

                restoredJson = backupJson;
                return true;
            }

            return false;
        }

        private ConcurrentDictionary<Type, IConfig> CreateDefaultConfigInstances()
        {
            var configs = new ConcurrentDictionary<Type, IConfig>();
            foreach (var type in AssemblyHandler.GetInstance().GetImplementationTypes(typeof(IConfig)))
            {
                try
                {
                    configs[type] = CreateDefaultConfig(type);
                }
                catch (Exception ex)
                {
                    log.Debug($"Skipped default configuration '{type.FullName}'.", ex);
                }
            }

            return configs;
        }

        private IEnumerable<string> GetBackupFiles()
        {
            if (string.IsNullOrEmpty(BackupFolderPath) || !Directory.Exists(BackupFolderPath))
                return Enumerable.Empty<string>();

            return Directory.GetFiles(BackupFolderPath, $"{ConfigDIFileName}Backup_*.json")
                .OrderByDescending(f => f);
        }

        public void BackupConfigs()
        {
            try
            {
                string backupFileName = $"{ConfigDIFileName}Backup_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                string backupPath = Path.Combine(BackupFolderPath, backupFileName);
                SaveConfigs(backupPath);
                CleanupOldBackups();
            }
            catch (Exception ex)
            {
                log.Error(Properties.Resources.BackupConfigFileFailed, ex);
            }
        }

        private void CleanupOldBackups()
        {
            try
            {
                foreach (var file in GetBackupFiles().Skip(MaxBackupCount))
                    File.Delete(file);
            }
            catch (Exception ex)
            {
                log.Warn(Properties.Resources.CleanupBackupFailed, ex);
            }
        }
    }
}
