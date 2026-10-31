#pragma warning disable CA1863
using ColorVision.UI.Authorizations;
using ColorVision.UI.Json;
using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Windows;

namespace ColorVision.UI
{
    public enum ConfigSavePublicationStatus
    {
        NotPersisted,
        PersistedAndPublished,
        PersistedButPublishFailed,
    }

    public class ConfigHandler : IConfigService, IConfigReloadNotifier
    {
        private const string BackupFolderName = "Backup";
        private const string QuarantineFolderName = "Quarantine";
        private const int MaxBackupCount = 10;

        private static readonly ILog log = LogManager.GetLogger(typeof(ConfigHandler));
        private static ConfigHandler? _instance;
        private static readonly object _locker = new();
        private static string[] _maintenanceResetSections = [];
        private static Func<bool>? _maintenanceResetStartupAdmission;
        private static bool _maintenanceResetPolicyFrozen;
        private readonly object _saveStateLock = new();
        private readonly AsyncLocal<bool> _savePublicationScope = new();
        private long _saveTransactionVersion;
        private bool _saveTransactionPending;
        private int _saveTransactionOwnerThreadId;

        public static ConfigHandler GetInstance() => GetInstance(null);

        public static ConfigHandler GetInstance(string? ConfigDIFileName)
        {
            if (_instance != null) return _instance;
            lock (_locker)
            {
                _instance ??= CreateInstance(ConfigDIFileName);
                return _instance;
            }
        }

        private static ConfigHandler CreateInstance(string? configDIFileName)
        {
            var instance = new ConfigHandler { ConfigDIFileName = configDIFileName };
            instance.Load();
            ConfigService.SetInstance(instance);
            AssemblyHandler.GetInstance();
            return instance;
        }

        public string ConfigFilePath { get; set; } = string.Empty;
        public string BackupFolderPath { get; set; } = string.Empty;

        public DateTime InitDateTime { get; set; }

        public string? ConfigDIFileName { get; set; }

        public ConfigMaintenanceResetResult? LastMaintenanceResetResult { get; private set; }

        /// <summary>Registers the reset allowlist and optional read-only startup admission before configuration startup.</summary>
        public static void ConfigureMaintenanceResetSections(IEnumerable<string> sectionNames, Func<bool>? startupAdmission = null)
        {
            var sections = ConfigMaintenanceResetService.ValidateSectionNames(sectionNames);
            lock (_locker)
            {
                if (_maintenanceResetPolicyFrozen || _instance != null)
                    throw new InvalidOperationException("Maintenance reset sections must be registered before configuration startup.");
                _maintenanceResetSections = sections;
                _maintenanceResetStartupAdmission = startupAdmission;
            }
        }

        public ConfigMaintenanceResetService CreateMaintenanceResetService()
        {
            lock (_locker)
            {
                _maintenanceResetPolicyFrozen = true;
                return new ConfigMaintenanceResetService(ConfigFilePath, _maintenanceResetSections);
            }
        }

        public ConfigHandler()
        {
        }

        public void Load()
        {
            JsonSerializerSettings = CreateJsonSerializerSettings();
            InitDateTime = DateTime.Now;

            InitializePaths();
            LastMaintenanceResetResult = CreateMaintenanceResetService().ApplyPending(_maintenanceResetStartupAdmission);
            if (!LastMaintenanceResetResult.Succeeded)
                log.Error($"Pending configuration reset was not completed: {LastMaintenanceResetResult.ErrorMessage}");
            else if (LastMaintenanceResetResult.Status == ConfigMaintenanceResetStatus.Deferred)
                log.Warn($"Pending configuration reset was deferred: {LastMaintenanceResetResult.ErrorMessage}");
            LoadConfigs(ConfigFilePath);
            ScheduleBackup();

            Authorization.Instance = GetRequiredService<Authorization>();

            AppDomain.CurrentDomain.ProcessExit += (s, e) =>
            {
                if (!IsAutoSave)
                    return;

                try
                {
                    SaveConfigs();
                }
                catch (Exception ex)
                {
                    log.Error("Failed to save configuration during process exit.", ex);
                }
            };
        }

        private void ScheduleBackup()
        {
            if (!IsAutoSave)
                return;

            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromMinutes(2));
                BackupConfigs();
            });
        }

        private static JsonSerializerSettings CreateJsonSerializerSettings()
        {
            var settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                ContractResolver = new WpfContractResolver()
            };
            settings.Converters.Add(new BrushJsonConverter());
            return settings;
        }

        private void InitializePaths()
        {
            Assembly? entryAssembly = Assembly.GetEntryAssembly();
            string assemblyName = entryAssembly?.GetName().Name ?? "ColorVision";
            string assemblyCompany = entryAssembly?.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company ?? assemblyName;

            ConfigDIFileName ??= $"{assemblyName}Config";

            if (Directory.Exists("Config"))
            {
                ConfigFilePath = Path.Combine("Config", $"{ConfigDIFileName}.json");
                BackupFolderPath = Path.Combine("Config", BackupFolderName);
            }
            else
            {
                string directoryPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    assemblyCompany,
                    "Config");
                Directory.CreateDirectory(directoryPath);

                ConfigFilePath = Path.Combine(directoryPath, $"{ConfigDIFileName}.json");
                BackupFolderPath = Path.Combine(directoryPath, BackupFolderName);
            }

            ConfigFilePath = Path.GetFullPath(ConfigFilePath);
            BackupFolderPath = Path.GetFullPath(BackupFolderPath);
            Directory.CreateDirectory(BackupFolderPath);
        }


        public bool IsAutoSave { get; set; } = true;

        public event EventHandler? ConfigsReloaded;

        public void Reload()
        {
            SaveConfigs();
            LoadConfigs(ConfigFilePath);
        }

        public void ReloadFromDisk()
        {
            string fullPath = Path.GetFullPath(ConfigFilePath);
            var transactionStarted = false;
            var stateChanged = false;
            try
            {
                BeginSaveTransaction();
                transactionStarted = true;
                using (ConfigFileStore.AcquireLock(fullPath))
                {
                    if (!ConfigFileStore.TryRead(fullPath, out JObject loadedJson, ex => log.Warn(ex)))
                        throw new InvalidOperationException($"Unable to reload configuration file '{fullPath}'.");

                    ApplyLoadedConfig(loadedJson);
                    stateChanged = true;
                    Authorization.Instance = GetRequiredService<Authorization>();
                }
            }
            finally
            {
                if (transactionStarted)
                    EndSaveTransaction(stateChanged);
            }

            ConfigsReloaded?.Invoke(this, EventArgs.Empty);
        }

        public void SaveConfigs() => SaveConfigs(ConfigFilePath);

        internal JsonSerializerSettings JsonSerializerSettings { get; set; } = CreateJsonSerializerSettings();

        public ConcurrentDictionary<Type, IConfig> Configs { get; set; } = new();


        public IConfig GetRequiredService(Type type)
        {
            ArgumentNullException.ThrowIfNull(type);
            if (!typeof(IConfig).IsAssignableFrom(type))
                throw new ArgumentException("Type must implement IConfig.", nameof(type));

            return Configs.GetOrAdd(type, CreateConfig);
        }

        private IConfig CreateConfig(Type type)
        {
            string? sectionName = ConfigSectionNames.GetReadCandidates(type).FirstOrDefault(jsonObject.ContainsKey);
            if (sectionName == null)
                return CreateDefaultConfig(type);

            JToken configToken = jsonObject[sectionName]!;
            try
            {
                var config = configToken.ToObject(type, JsonSerializer.Create(JsonSerializerSettings)) as IConfig;
                if (config != null)
                {
                    if (config is IConfigSecure configSecure)
                        configSecure.Decrypt();

                    return config;
                }
            }
            catch (Exception ex)
            {
                log.Warn($"Configuration section '{sectionName}' could not be read as '{type.FullName}'; defaults are used.", ex);
                QuarantineSection(sectionName, configToken);
            }

            return CreateDefaultConfig(type);
        }

        /// <summary>
        /// Keeps a copy of an unreadable section before defaults can overwrite it on the next save,
        /// so a schema mistake does not silently destroy the user's settings.
        /// </summary>
        private void QuarantineSection(string sectionName, JToken token)
        {
            if (string.IsNullOrEmpty(BackupFolderPath))
                return;

            try
            {
                string directory = Path.Combine(BackupFolderPath, QuarantineFolderName);
                string safeName = string.Concat(sectionName.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
                string path = Path.Combine(directory, $"{ConfigDIFileName}_{safeName}_{DateTime.Now:yyyyMMdd_HHmmss_fff}.json");
                ConfigFileStore.Write(path, new JObject { [sectionName] = token.DeepClone() });
                log.Warn($"The unreadable configuration section '{sectionName}' was preserved at '{path}'.");
            }
            catch (Exception ex)
            {
                log.Error($"Failed to preserve the unreadable configuration section '{sectionName}'.", ex);
            }
        }

        private static IConfig CreateDefaultConfig(Type type) => (IConfig)Activator.CreateInstance(type)!;

        public T1 GetRequiredService<T1>() where T1 : IConfig => (T1)GetRequiredService(typeof(T1));

        public void SaveConfigs(string fileName)
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
                    using var fileLock = ConfigFileStore.AcquireLock(fullPath);
                    var jObject = ConfigFileStore.ReadForMerge(fullPath);
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

        public void LoadConfigs() => LoadConfigs(ConfigFilePath);
        private JObject jsonObject = new JObject();

        public void LoadConfigs(string fileName)
        {
            string fullPath = Path.GetFullPath(fileName);
            var transactionStarted = false;
            var stateChanged = false;
            try
            {
                BeginSaveTransaction();
                transactionStarted = true;
                using (ConfigFileStore.AcquireLock(fullPath))
                {
                    if (ConfigFileStore.TryRead(fullPath, out var loadedJson, ex => log.Warn(ex)))
                    {
                        ApplyLoadedConfig(loadedJson);
                        stateChanged = true;
                    }
                }

                if (!stateChanged)
                {
                    LoadDefaultConfigsCore();
                    stateChanged = true;
                }
            }
            finally
            {
                if (transactionStarted)
                    EndSaveTransaction(stateChanged);
            }

            ConfigsReloaded?.Invoke(this, EventArgs.Empty);
        }

        private void ApplyLoadedConfig(JObject loadedJson)
        {
            jsonObject = loadedJson;
            Configs = new ConcurrentDictionary<Type, IConfig>();
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
