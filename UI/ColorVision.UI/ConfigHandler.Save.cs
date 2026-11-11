#pragma warning disable CA1863
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Windows;

namespace ColorVision.UI
{
    public partial class ConfigHandler
    {
        public void SaveConfigs(string fileName)
            => SaveConfigSnapshot(fileName, backupSourcePath: null);

        private void SaveConfigSnapshot(string fileName, string? backupSourcePath)
        {
            string fullPath = Path.GetFullPath(fileName);
            while (true)
            {
                long observedVersion = CaptureSaveTransactionVersion();
                var stagedConfigs = CreateConfigSnapshot();
                if (!TryBeginSaveTransaction(observedVersion))
                    continue;

                var persisted = false;
                try
                {
                    JObject? backupBase = null;
                    if (backupSourcePath != null)
                    {
                        using var primaryLock = ConfigFileStore.AcquireLock(backupSourcePath);
                        backupBase = ConfigFileStore.ReadForMerge(backupSourcePath, missingFileFallback: jsonObject);
                    }
                    using var fileLock = ConfigFileStore.AcquireLock(fullPath);
                    var jObject = backupBase ?? ConfigFileStore.ReadForMerge(fullPath);
                    foreach (var property in stagedConfigs.Properties())
                        jObject[property.Name] = property.Value.DeepClone();

                    ConfigFileStore.Write(fullPath, jObject);
                    persisted = true;
                    return;
                }
                finally
                {
                    EndSaveTransaction(persisted);
                }
            }
        }

        private long CaptureSaveTransactionVersion()
        {
            lock (_saveStateLock)
            {
                WaitForSaveTransaction();
                return _saveTransactionVersion;
            }
        }

        private bool TryBeginSaveTransaction(long expectedVersion)
        {
            lock (_saveStateLock)
            {
                WaitForSaveTransaction();
                if (expectedVersion != _saveTransactionVersion)
                    return false;

                StartSaveTransaction();
                return true;
            }
        }

        private void BeginSaveTransaction()
        {
            lock (_saveStateLock)
            {
                WaitForSaveTransaction();
                StartSaveTransaction();
            }
        }

        private void StartSaveTransaction()
        {
            _saveTransactionPending = true;
            _saveTransactionOwnerThreadId = Environment.CurrentManagedThreadId;
        }

        private void WaitForSaveTransaction()
        {
            while (_saveTransactionPending)
            {
                if (_savePublicationScope.Value
                    || _saveTransactionOwnerThreadId == Environment.CurrentManagedThreadId)
                {
                    throw new InvalidOperationException(
                        "A configuration save cannot be started reentrantly while a persisted configuration is being published.");
                }

                Monitor.Wait(_saveStateLock);
            }
        }

        private void EndSaveTransaction(bool transactionCommitted)
        {
            lock (_saveStateLock)
            {
                if (transactionCommitted)
                    _saveTransactionVersion++;
                _saveTransactionPending = false;
                _saveTransactionOwnerThreadId = 0;
                Monitor.PulseAll(_saveStateLock);
            }
        }

        private JObject CreateConfigSnapshot()
        {
            var stagedConfigs = new JObject();
            var jsonSerializer = JsonSerializer.Create(JsonSerializerSettings);
            var errors = new List<Exception>();
            foreach (var configPair in Configs.ToArray())
            {
                try
                {
                    SaveConfig(stagedConfigs, configPair.Key, configPair.Value, jsonSerializer);
                }
                catch (Exception ex)
                {
                    log.Info(configPair.Key);
                    log.Error(ex);
                    errors.Add(new InvalidOperationException(
                        $"Configuration '{configPair.Key.FullName}' could not be serialized.",
                        ex));
                }
            }

            if (errors.Count > 0)
            {
                throw new AggregateException(
                    "The configuration snapshot was not saved because one or more sections could not be serialized.",
                    errors);
            }

            return stagedConfigs;
        }

        private static void SaveConfig(JObject jObject, Type configType, IConfig config, JsonSerializer serializer)
        {
            string sectionName = ConfigSectionNames.GetSectionName(configType);
            if (jObject.ContainsKey(sectionName))
                throw new InvalidOperationException($"Multiple configuration types map to section '{sectionName}'. The configuration snapshot cannot be saved.");

            InvokeOnApplicationDispatcher(() =>
                WriteConfigToken(jObject, sectionName, config, serializer));
        }

        private static void InvokeOnApplicationDispatcher(Action action)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                action();
                return;
            }

            dispatcher.Invoke(action);
        }

        private static void WriteConfigToken(JObject jObject, string configName, IConfig config, JsonSerializer serializer)
        {
            if (config is not IConfigSecure)
            {
                jObject[configName] = JToken.FromObject(config, serializer);
                return;
            }

            var plaintextSnapshot = JToken.FromObject(config, serializer);
            var secureSnapshot = plaintextSnapshot.ToObject(config.GetType(), serializer) as IConfigSecure
                ?? throw new JsonSerializationException(
                    $"Secure configuration '{configName}' could not be cloned for persistence.");
            secureSnapshot.Encryption();
            jObject[configName] = JToken.FromObject(secureSnapshot, serializer);
        }

        public void Save<T1>() where T1 : IConfig
        {
            var configInstance = GetRequiredService<T1>();
            TrySave(configInstance, out _);
        }

        public bool TrySave<T1>(T1 candidate, out string errorMessage) where T1 : IConfig
        {
            return TrySaveCore(candidate, onPersisted: null, out errorMessage)
                == ConfigSavePublicationStatus.PersistedAndPublished;
        }

        public ConfigSavePublicationStatus TrySaveAndPublish<T1>(
            T1 candidate,
            Action onPersisted,
            out string errorMessage) where T1 : IConfig
        {
            ArgumentNullException.ThrowIfNull(onPersisted);
            return TrySaveCore(candidate, onPersisted, out errorMessage);
        }

        private ConfigSavePublicationStatus TrySaveCore<T1>(
            T1 candidate,
            Action? onPersisted,
            out string errorMessage) where T1 : IConfig
        {
            var type = typeof(T1);
            var configName = ConfigSectionNames.GetSectionName(type);
            JToken? candidateToken = null;

            if (_savePublicationScope.Value)
            {
                errorMessage = "A configuration save cannot be started reentrantly while a persisted configuration is being published.";
                return ConfigSavePublicationStatus.NotPersisted;
            }

            try
            {
                ArgumentNullException.ThrowIfNull(candidate);
                var stagedConfig = new JObject();
                SaveConfig(
                    stagedConfig,
                    type,
                    candidate,
                    JsonSerializer.Create(JsonSerializerSettings));
                candidateToken = stagedConfig[configName]?.DeepClone()
                    ?? throw new JsonSerializationException(
                        $"Configuration candidate '{configName}' did not produce a JSON token.");
            }
            catch (Exception ex)
            {
                log.Error(string.Format(Properties.Resources.SaveSingleConfigFailed, configName), ex);
                errorMessage = ex.GetBaseException().Message;
                return ConfigSavePublicationStatus.NotPersisted;
            }

            var transactionStarted = false;
            var persisted = false;
            try
            {
                string fullPath = Path.GetFullPath(ConfigFilePath);
                BeginSaveTransaction();
                transactionStarted = true;
                using (ConfigFileStore.AcquireLock(fullPath))
                {
                    var jObject = ConfigFileStore.ReadForMerge(fullPath);
                    jObject[configName] = candidateToken;
                    ConfigFileStore.Write(fullPath, jObject);
                    persisted = true;
                }

                try
                {
                    _savePublicationScope.Value = true;
                    onPersisted?.Invoke();
                }
                catch (Exception ex)
                {
                    log.Error($"Configuration '{configName}' was persisted but could not be published in memory.", ex);
                    errorMessage = ex.GetBaseException().Message;
                    return ConfigSavePublicationStatus.PersistedButPublishFailed;
                }
                finally
                {
                    _savePublicationScope.Value = false;
                }

                errorMessage = string.Empty;
                return ConfigSavePublicationStatus.PersistedAndPublished;
            }
            catch (Exception ex)
            {
                log.Error(string.Format(Properties.Resources.WriteConfigFileFailed, ConfigFilePath), ex);
                errorMessage = ex.GetBaseException().Message;
                return ConfigSavePublicationStatus.NotPersisted;
            }
            finally
            {
                if (transactionStarted)
                    EndSaveTransaction(persisted);
            }
        }
    }
}
