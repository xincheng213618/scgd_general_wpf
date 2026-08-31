#pragma warning disable CA1863
using ColorVision.UI.Authorizations;
using ColorVision.UI.Json;
using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
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
        private const int MaxBackupCount = 10;
        private static readonly TimeSpan SaveFileLockTimeout = TimeSpan.FromSeconds(30);
        private static readonly string[] ObsoleteConfigSectionNames =
        {
            "ConfigOptions",
            "MarketplaceServiceConfig"
        };

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
                using (AcquireSaveFileLock(fullPath))
                {
                    if (!TryReadConfigFile(fullPath, out JObject loadedJson, ex => log.Warn(ex)))
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
            try
            {
                if (jsonObject.TryGetValue(type.Name, out JToken? configToken))
                {
                    var config = configToken.ToObject(type, JsonSerializer.Create(JsonSerializerSettings)) as IConfig;
                    if (config != null)
                    {
                        if (config is IConfigSecure configSecure)
                            configSecure.Decrypt();

                        return config;
                    }
                }
            }
            catch (Exception ex)
            {
                log.Warn(ex);
            }

            return CreateDefaultConfig(type);
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
                    using var fileLock = AcquireSaveFileLock(fullPath);
                    var jObject = ReadExistingConfigFileForSave(fullPath);
                    RemoveObsoleteConfigSections(jObject);
                    foreach (var property in stagedConfigs.Properties())
                        jObject[property.Name] = property.Value.DeepClone();

                    WriteConfigFile(fullPath, jObject);
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

        internal static IDisposable AcquireSaveFileLock(string fullPath)
        {
            string canonicalPath = Path.GetFullPath(fullPath).ToUpperInvariant();
            string pathHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalPath)));
            var mutex = new Mutex(initiallyOwned: false, $"Local\\ColorVision.ConfigSave.{pathHash}");
            try
            {
                var acquired = false;
                try
                {
                    acquired = mutex.WaitOne(SaveFileLockTimeout);
                }
                catch (AbandonedMutexException)
                {
                    acquired = true;
                }

                if (!acquired)
                {
                    throw new TimeoutException(
                        $"Timed out waiting to save configuration file '{fullPath}'.");
                }

                return new SaveFileLockLease(mutex);
            }
            catch
            {
                mutex.Dispose();
                throw;
            }
        }

        private sealed class SaveFileLockLease : IDisposable
        {
            private Mutex? _mutex;

            public SaveFileLockLease(Mutex mutex)
            {
                _mutex = mutex;
            }

            public void Dispose()
            {
                var mutex = Interlocked.Exchange(ref _mutex, null);
                if (mutex == null)
                    return;

                try
                {
                    mutex.ReleaseMutex();
                }
                finally
                {
                    mutex.Dispose();
                }
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
                        $"Configuration '{configPair.Key.Name}' could not be serialized.",
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
            InvokeOnApplicationDispatcher(() =>
                WriteConfigToken(jObject, configType.Name, config, serializer));
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

        private static JObject ReadExistingConfigFileForSave(string fileName)
        {
            if (!File.Exists(fileName))
                return new JObject();

            Exception? readException = null;
            if (TryReadConfigFile(fileName, out var jObject, ex => readException = ex))
                return jObject;

            throw new InvalidDataException(
                $"Existing configuration file '{fileName}' is not a valid JSON object.",
                readException);
        }

        private static void RemoveObsoleteConfigSections(JObject jObject)
        {
            foreach (string sectionName in ObsoleteConfigSectionNames)
            {
                jObject.Remove(sectionName);
            }
        }

        private static bool TryReadConfigFile(string fileName, out JObject jObject, Action<Exception> logException)
        {
            jObject = new JObject();
            if (!File.Exists(fileName))
                return false;

            try
            {
                using StreamReader file = File.OpenText(fileName);
                using JsonTextReader reader = new(file);
                jObject = JObject.Load(reader);
                if (reader.Read())
                    throw new JsonReaderException("Additional content was found after the configuration JSON object.");
                return true;
            }
            catch (Exception ex)
            {
                logException(ex);
                return false;
            }
        }

        internal static void WriteConfigFile(string fileName, JObject jObject)
        {
            string fullPath = Path.GetFullPath(fileName);
            string directory = Path.GetDirectoryName(fullPath)
                ?? throw new ArgumentException("Configuration path must include a directory.", nameof(fileName));
            Directory.CreateDirectory(directory);

            string temporaryPath = Path.Combine(
                directory,
                $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
            try
            {
                using (var stream = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    4096,
                    FileOptions.WriteThrough))
                using (var file = new StreamWriter(
                    stream,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                    4096,
                    leaveOpen: true))
                using (var writer = new JsonTextWriter(file) { CloseOutput = false })
                {
                    jObject.WriteTo(writer);
                    writer.Flush();
                    file.Flush();
                    stream.Flush(flushToDisk: true);
                }

                Exception? validationException = null;
                if (!TryReadConfigFile(temporaryPath, out _, ex => validationException = ex))
                {
                    throw new InvalidDataException(
                        $"Temporary configuration file '{temporaryPath}' failed JSON validation.",
                        validationException);
                }

                if (File.Exists(fullPath))
                    File.Replace(temporaryPath, fullPath, destinationBackupFileName: null, ignoreMetadataErrors: true);
                else
                    File.Move(temporaryPath, fullPath);
            }
            finally
            {
                try
                {
                    if (File.Exists(temporaryPath))
                        File.Delete(temporaryPath);
                }
                catch (Exception ex)
                {
                    log.Warn($"Failed to remove temporary configuration file '{temporaryPath}'.", ex);
                }
            }
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
                if (!TryReadConfigFile(backupFile, out var backupJson, ex => log.Warn(ex)))
                    continue;

                string fullPath = Path.GetFullPath(ConfigFilePath);
                using (AcquireSaveFileLock(fullPath))
                    WriteConfigFile(fullPath, backupJson);

                restoredJson = backupJson;
                return true;
            }

            return false;
        }

        private ConcurrentDictionary<Type, IConfig> CreateDefaultConfigInstances()
        {
            var configs = new ConcurrentDictionary<Type, IConfig>();
            foreach (var assembly in AssemblyHandler.GetInstance().GetAssemblies())
            {
                foreach (var type in GetConfigTypes(assembly))
                {
                    try
                    {
                        configs[type] = CreateDefaultConfig(type);
                    }
                    catch
                    {
                    }
                }
            }

            return configs;
        }

        private static IEnumerable<Type> GetConfigTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes()
                    .Where(t => typeof(IConfig).IsAssignableFrom(t) && !t.IsAbstract)
                    .ToArray();
            }
            catch
            {
                return Enumerable.Empty<Type>();
            }
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
                using (AcquireSaveFileLock(fullPath))
                {
                    if (TryReadConfigFile(fullPath, out var loadedJson, ex => log.Warn(ex)))
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
            var configName = type.Name;
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
                using (AcquireSaveFileLock(fullPath))
                {
                    var jObject = ReadExistingConfigFileForSave(fullPath);
                    RemoveObsoleteConfigSections(jObject);
                    jObject[configName] = candidateToken;
                    WriteConfigFile(fullPath, jObject);
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
