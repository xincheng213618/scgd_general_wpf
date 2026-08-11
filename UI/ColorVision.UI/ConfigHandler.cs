#pragma warning disable CA1863
using ColorVision.UI.Authorizations;
using ColorVision.UI.Json;
using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;

namespace ColorVision.UI
{
    public class ConfigHandler : IConfigService, IConfigReloadNotifier
    {
        private const string BackupFolderName = "Backup";
        private const int MaxBackupCount = 10;
        private static readonly string[] ObsoleteConfigSectionNames =
        {
            "ConfigOptions",
            "MarketplaceServiceConfig"
        };

        private static readonly ILog log = LogManager.GetLogger(typeof(ConfigHandler));
        private static ConfigHandler? _instance;
        private static readonly object _locker = new();
        private readonly object _reloadExecutionGate = new();
        private readonly AsyncLocal<long?> _reloadExecutionContext = new();
        private bool _reloadExecutionActive;
        private long _activeReloadExecutionId;
        private int _activeReloadExecutionThreadId;
        private long _nextReloadExecutionId;
        private bool _suppressReloadDispatcher;

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
            instance.LoadBeforeSingletonPublication();
            ConfigService.SetInstance(instance);
            AssemblyHandler.GetInstance();
            return instance;
        }

        private void LoadBeforeSingletonPublication()
        {
            // GetInstance still owns _locker here. Marshalling this first load to the UI thread
            // could invert that lock with a simultaneous UI GetInstance call. No participant or
            // legacy subscriber can observe this unpublished instance, so keep only this initial
            // load on its creating thread; every public reload and registration still marshals.
            _suppressReloadDispatcher = true;
            try
            {
                Load();
            }
            finally
            {
                _suppressReloadDispatcher = false;
            }
        }

        public string ConfigFilePath { get; set; } = string.Empty;
        public string BackupFolderPath { get; set; } = string.Empty;

        public DateTime InitDateTime { get; set; }

        public string? ConfigDIFileName { get; set; }

        public ConfigHandler()
        {
            ReloadCoordinator = new ConfigReloadCoordinator(this);
        }

        public ConfigReloadCoordinator ReloadCoordinator { get; }

        public ConfigReloadResult LastReloadResult { get; private set; } = ConfigReloadResult.Empty;

        /// <summary>
        /// Registers process-lifetime owners and performs their initial bind under the same gate
        /// used by file reloads, so registration cannot observe a partially installed generation.
        /// Only references newly registered by this call are bound.
        /// </summary>
        public ConfigReloadResult RegisterReloadParticipants(params IConfigReloadParticipant[] participants) =>
            ExecuteReload(() => ReloadCoordinator.RegisterAndBind(participants));

        /// <summary>
        /// Reloads from independent callers are queued. A reload requested from a participant or
        /// legacy callback in the active reload execution is rejected instead of waiting on itself.
        /// </summary>
        private T ExecuteReload<T>(Func<T> action)
        {
            ArgumentNullException.ThrowIfNull(action);

            ThrowIfReloadIsReentrant();
            var dispatcher = Application.Current?.Dispatcher;
            if (!_suppressReloadDispatcher && dispatcher != null && !dispatcher.CheckAccess())
                return dispatcher.Invoke(() => ExecuteReloadCore(action));

            return ExecuteReloadCore(action);
        }

        private void ThrowIfReloadIsReentrant()
        {
            long? inheritedExecutionId = _reloadExecutionContext.Value;
            if (!inheritedExecutionId.HasValue)
                return;

            lock (_reloadExecutionGate)
            {
                if (_reloadExecutionActive && inheritedExecutionId == _activeReloadExecutionId)
                {
                    throw new InvalidOperationException(
                        "A configuration reload cannot be started from inside an active configuration reload callback.");
                }
            }
        }

        private T ExecuteReloadCore<T>(Func<T> action)
        {
            long? inheritedExecutionId = _reloadExecutionContext.Value;
            long executionId;
            lock (_reloadExecutionGate)
            {
                if (_reloadExecutionActive && inheritedExecutionId == _activeReloadExecutionId)
                {
                    throw new InvalidOperationException(
                        "A configuration reload cannot be started from inside an active configuration reload callback.");
                }

                while (_reloadExecutionActive)
                    Monitor.Wait(_reloadExecutionGate);

                executionId = ++_nextReloadExecutionId;
                _reloadExecutionActive = true;
                _activeReloadExecutionId = executionId;
                _activeReloadExecutionThreadId = Environment.CurrentManagedThreadId;
            }

            _reloadExecutionContext.Value = executionId;
            try
            {
                return action();
            }
            finally
            {
                _reloadExecutionContext.Value = inheritedExecutionId;
                lock (_reloadExecutionGate)
                {
                    _reloadExecutionActive = false;
                    _activeReloadExecutionId = 0;
                    _activeReloadExecutionThreadId = 0;
                    Monitor.PulseAll(_reloadExecutionGate);
                }
            }
        }

        private void ExecuteReload(Action action)
        {
            ExecuteReload(() =>
            {
                action();
                return true;
            });
        }

        /// <summary>
        /// Persistence shares the reload execution owner. A synchronous save requested by a
        /// participant remains inside the current owner; a flowed callback on another thread is
        /// rejected because it cannot wait for, or run concurrently with, its parent execution.
        /// </summary>
        private void ExecutePersistence(Action action)
        {
            ArgumentNullException.ThrowIfNull(action);

            long? inheritedExecutionId = _reloadExecutionContext.Value;
            bool executeInline = false;
            lock (_reloadExecutionGate)
            {
                if (_reloadExecutionActive
                    && (inheritedExecutionId == _activeReloadExecutionId
                        || _activeReloadExecutionThreadId == Environment.CurrentManagedThreadId))
                {
                    if (inheritedExecutionId == _activeReloadExecutionId
                        && _activeReloadExecutionThreadId == Environment.CurrentManagedThreadId)
                    {
                        executeInline = true;
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            "Configuration persistence cannot leave the thread that owns the active configuration execution.");
                    }
                }
            }

            if (executeInline)
            {
                action();
                return;
            }

            ExecuteReload(action);
        }

        public void Load()
        {
            JsonSerializerSettings = CreateJsonSerializerSettings();
            InitDateTime = DateTime.Now;

            InitializePaths();
            LoadConfigs(ConfigFilePath);
            ScheduleBackup();

            AppDomain.CurrentDomain.ProcessExit += (s, e) =>
            {
                if (IsAutoSave)
                    SaveConfigs();
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

            Directory.CreateDirectory(BackupFolderPath);
        }


        public bool IsAutoSave { get; set; } = true;

        public event EventHandler? ConfigsReloaded;

        public void Reload() => ReloadWithResult().ThrowIfFailed();

        public ConfigReloadResult ReloadWithResult()
        {
            return ExecuteReload(() =>
            {
                try
                {
                    SaveConfigsNoLock(ConfigFilePath);
                }
                catch (Exception ex)
                {
                    return SetSourceInstallFailure(
                        ConfigFilePath,
                        ConfigSourceReadStatus.NotAttempted,
                        ex,
                        "Unable to save the active configuration before reload.");
                }

                return LoadConfigsCore(ConfigFilePath, allowRecovery: true);
            });
        }

        public void ReloadFromDisk() => ReloadFromDiskWithResult().ThrowIfFailed();

        public ConfigReloadResult ReloadFromDiskWithResult()
        {
            return ExecuteReload(() =>
            {
                ConfigSourceReadStatus sourceReadStatus = ReadConfigFile(
                    ConfigFilePath,
                    out JObject loadedJson,
                    out Exception? sourceException);
                if (sourceReadStatus != ConfigSourceReadStatus.Succeeded)
                    return SetSourceReadFailure(ConfigFilePath, sourceReadStatus, sourceException);

                InstallConfigDocument(loadedJson);
                return NotifyConfigsReloaded(sourceReadStatus, ConfigRecoveryStatus.NotRequired);
            });
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

        public void SaveConfigs(string fileName) =>
            ExecutePersistence(() => SaveConfigsNoLock(fileName));

        private void SaveConfigsNoLock(string fileName)
        {
            var jObject = ReadExistingConfigFile(fileName);
            RemoveObsoleteConfigSections(jObject);
            var jsonSerializer = JsonSerializer.Create(JsonSerializerSettings);

            foreach (var configPair in Configs.ToArray())
            {
                try
                {
                    SaveConfig(jObject, configPair.Key, configPair.Value, jsonSerializer);
                }
                catch (Exception ex)
                {
                    log.Info(configPair.Key);
                    log.Error(ex);
                }
            }

            WriteConfigFile(fileName, jObject);
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
            if (config is not IConfigSecure configSecure)
            {
                jObject[configName] = JToken.FromObject(config, serializer);
                return;
            }

            configSecure.Encryption();
            try
            {
                jObject[configName] = JToken.FromObject(config, serializer);
            }
            finally
            {
                configSecure.Decrypt();
            }
        }

        private static JObject ReadExistingConfigFile(string fileName)
        {
            return TryReadConfigFile(fileName, out var jObject, ex => log.Error(ex))
                ? jObject
                : new JObject();
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
            ConfigSourceReadStatus status = ReadConfigFile(fileName, out jObject, out Exception? exception);
            if (exception != null)
                logException(exception);
            return status == ConfigSourceReadStatus.Succeeded;
        }

        private static ConfigSourceReadStatus ReadConfigFile(
            string fileName,
            out JObject jObject,
            out Exception? exception)
        {
            jObject = new JObject();
            exception = null;
            if (!File.Exists(fileName))
            {
                exception = new FileNotFoundException("The configuration source file does not exist.", fileName);
                return ConfigSourceReadStatus.Missing;
            }

            try
            {
                string json = File.ReadAllText(fileName);
                using StringReader file = new(json);
                using StrictJsonTextReader reader = new(file);
                if (!reader.Read())
                    throw new JsonReaderException("The configuration source is empty.");
                if (reader.TokenType != JsonToken.StartObject)
                    throw new JsonReaderException("The configuration source root must be a JSON object.");

                jObject = JObject.Load(
                    reader,
                    new JsonLoadSettings
                    {
                        DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
                    });
                if (reader.Read())
                    throw new JsonReaderException("Additional content was found after the root configuration object.");

                // Json.NET deliberately accepts trailing commas. Validate the same source with
                // the platform strict JSON parser before treating it as installable content.
                using System.Text.Json.JsonDocument strictDocument = System.Text.Json.JsonDocument.Parse(
                    json,
                    new System.Text.Json.JsonDocumentOptions
                    {
                        AllowTrailingCommas = false,
                        CommentHandling = System.Text.Json.JsonCommentHandling.Disallow,
                    });
                return ConfigSourceReadStatus.Succeeded;
            }
            catch (Exception ex)
            {
                jObject = new JObject();
                exception = ex;
                return ConfigSourceReadStatus.Invalid;
            }
        }

        private static void WriteConfigFile(string fileName, JObject jObject)
        {
            string? directory = Path.GetDirectoryName(fileName);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            using StreamWriter file = File.CreateText(fileName);
            using JsonTextWriter writer = new(file);
            jObject.WriteTo(writer);
        }

        private static void WriteConfigFileAtomically(string fileName, JObject jObject)
        {
            string fullPath = Path.GetFullPath(fileName);
            string directory = Path.GetDirectoryName(fullPath)
                ?? throw new InvalidOperationException($"Unable to resolve the configuration directory for '{fileName}'.");
            Directory.CreateDirectory(directory);

            string temporaryPath = Path.Combine(
                directory,
                $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
            try
            {
                WriteConfigFile(temporaryPath, jObject);
                File.Move(temporaryPath, fullPath, overwrite: true);
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
                    log.Warn($"Unable to remove temporary configuration file '{temporaryPath}'.", ex);
                }
            }
        }

        public void LoadDefaultConfigs() => LoadDefaultConfigsWithResult().ThrowIfFailed();

        public ConfigReloadResult LoadDefaultConfigsWithResult()
        {
            return ExecuteReload(() =>
            {
                ConfigRecoveryStatus recoveryStatus = LoadRecoveryConfigNoLock();
                return NotifyConfigsReloaded(ConfigSourceReadStatus.NotAttempted, recoveryStatus);
            });
        }

        private ConfigRecoveryStatus LoadRecoveryConfigNoLock()
        {
            try
            {
                foreach (string backupFile in GetBackupFiles())
                {
                    if (!TryReadConfigFile(backupFile, out var backupJson, ex => log.Warn(ex)))
                        continue;

                    InstallConfigDocument(backupJson);
                    WriteConfigFileAtomically(ConfigFilePath, backupJson);
                    return ConfigRecoveryStatus.RestoredBackup;
                }
            }
            catch (Exception ex)
            {
                log.Error(Properties.Resources.RestoreConfigFileFailed, ex);
            }

            jsonObject = new JObject();
            Configs = new ConcurrentDictionary<Type, IConfig>();
            LoadDefaultConfigInstances();
            return ConfigRecoveryStatus.LoadedDefaults;
        }

        private void LoadDefaultConfigInstances()
        {
            foreach (var assembly in AssemblyHandler.GetInstance().GetAssemblies())
            {
                foreach (var type in GetConfigTypes(assembly))
                {
                    try
                    {
                        Configs[type] = CreateDefaultConfig(type);
                    }
                    catch
                    {
                    }
                }
            }
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
                ExecutePersistence(BackupConfigsNoLock);
            }
            catch (Exception ex)
            {
                log.Error(Properties.Resources.BackupConfigFileFailed, ex);
            }
        }

        private void BackupConfigsNoLock()
        {
            string backupFileName = $"{ConfigDIFileName}Backup_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            string backupPath = Path.Combine(BackupFolderPath, backupFileName);
            SaveConfigsNoLock(backupPath);
            CleanupOldBackups();
        }

        private void BackupConfigsForImport()
        {
            if (string.IsNullOrWhiteSpace(BackupFolderPath))
                throw new InvalidOperationException("A configuration backup folder must be configured before import.");

            string backupFileName = $"{ConfigDIFileName}Backup_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            string backupPath = Path.Combine(BackupFolderPath, backupFileName);
            JObject backupJson = (JObject)jsonObject.DeepClone();
            RemoveObsoleteConfigSections(backupJson);
            JsonSerializer jsonSerializer = JsonSerializer.Create(JsonSerializerSettings);
            foreach (var configPair in Configs.ToArray())
                SaveConfig(backupJson, configPair.Key, configPair.Value, jsonSerializer);

            WriteConfigFileAtomically(backupPath, backupJson);
            CleanupOldBackups();
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

        public void LoadConfigs() => LoadConfigsWithResult().ThrowIfFailed();

        public ConfigReloadResult LoadConfigsWithResult() =>
            ExecuteReload(() => LoadConfigsCore(ConfigFilePath, allowRecovery: true));
        private JObject jsonObject = new JObject();

        public void LoadConfigs(string fileName) => LoadConfigsWithResult(fileName).ThrowIfFailed();

        public ConfigReloadResult LoadConfigsWithResult(string fileName) =>
            ExecuteReload(() => LoadConfigsCore(fileName, allowRecovery: true));

        /// <summary>
        /// Requires one complete JSON object and rejects duplicate properties, JSON comments
        /// (including trailing comments), or any other trailing content before changing the official file.
        /// Unknown or unmaterialized plugin sections are kept
        /// as JSON; import deliberately does not instantiate every <see cref="IConfig"/> type as a
        /// schema-validation side effect. Source recovery never substitutes a backup or defaults
        /// for an invalid selected file.
        /// </summary>
        public ConfigReloadResult ImportConfigsWithResult(string fileName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
            return ExecuteReload(() =>
            {
                ConfigSourceReadStatus sourceReadStatus = ReadConfigFile(
                    fileName,
                    out JObject loadedJson,
                    out Exception? sourceException);
                if (sourceReadStatus != ConfigSourceReadStatus.Succeeded)
                    return SetSourceReadFailure(fileName, sourceReadStatus, sourceException);

                try
                {
                    BackupConfigsForImport();
                    WriteConfigFileAtomically(ConfigFilePath, loadedJson);
                }
                catch (Exception ex)
                {
                    return SetSourceInstallFailure(
                        ConfigFilePath,
                        sourceReadStatus,
                        ex,
                        $"Configuration import source '{fileName}' could not be backed up or installed.");
                }

                InstallConfigDocument(loadedJson);
                return NotifyConfigsReloaded(sourceReadStatus, ConfigRecoveryStatus.NotRequired);
            });
        }

        private ConfigReloadResult LoadConfigsCore(string fileName, bool allowRecovery)
        {
            ConfigSourceReadStatus sourceReadStatus = ReadConfigFile(
                fileName,
                out JObject loadedJson,
                out Exception? sourceException);

            ConfigRecoveryStatus recoveryStatus;
            if (sourceReadStatus == ConfigSourceReadStatus.Succeeded)
            {
                InstallConfigDocument(loadedJson);
                recoveryStatus = ConfigRecoveryStatus.NotRequired;
            }
            else if (allowRecovery)
            {
                if (sourceException != null)
                    log.Warn(sourceException);
                recoveryStatus = LoadRecoveryConfigNoLock();
            }
            else
            {
                return SetSourceReadFailure(fileName, sourceReadStatus, sourceException);
            }

            return NotifyConfigsReloaded(sourceReadStatus, recoveryStatus);
        }

        private void InstallConfigDocument(JObject loadedJson)
        {
            jsonObject = (JObject)loadedJson.DeepClone();
            Configs = new ConcurrentDictionary<Type, IConfig>();
        }

        private ConfigReloadResult SetSourceReadFailure(
            string fileName,
            ConfigSourceReadStatus sourceReadStatus,
            Exception? exception)
        {
            var failure = new ConfigReloadFailure(
                ConfigReloadFailureKind.SourceRead,
                $"Config source '{fileName}'",
                exception ?? new InvalidOperationException($"Unable to read configuration source '{fileName}'."));
            LastReloadResult = ConfigReloadResult.FromSource(
                sourceReadStatus,
                ConfigRecoveryStatus.NotAttempted,
                failure);
            log.Error($"Unable to read configuration source '{fileName}'.", failure.Exception);
            return LastReloadResult;
        }

        private ConfigReloadResult SetSourceInstallFailure(
            string fileName,
            ConfigSourceReadStatus sourceReadStatus,
            Exception exception,
            string message)
        {
            var failure = new ConfigReloadFailure(
                ConfigReloadFailureKind.SourceInstall,
                $"Config destination '{fileName}'",
                exception);
            LastReloadResult = ConfigReloadResult.FromSource(
                sourceReadStatus,
                ConfigRecoveryStatus.NotAttempted,
                failure);
            log.Error($"{message} Destination: '{fileName}'.", exception);
            return LastReloadResult;
        }

        private ConfigReloadResult NotifyConfigsReloaded(
            ConfigSourceReadStatus sourceReadStatus,
            ConfigRecoveryStatus recoveryStatus)
        {
            // Authorization is a legacy static owner. Rebind it at the common successful-install
            // boundary before process-lifetime participants or legacy subscribers observe C2.
            Authorization.Instance = GetRequiredService<Authorization>();
            ConfigReloadResult result = ReloadCoordinator
                .BindCurrentConfigs()
                .WithSourceStatus(sourceReadStatus, recoveryStatus);
            Delegate[] subscribers = ConfigsReloaded?.GetInvocationList() ?? Array.Empty<Delegate>();
            var subscriberFailures = new List<ConfigReloadFailure>();

            foreach (EventHandler subscriber in subscribers.Cast<EventHandler>())
            {
                try
                {
                    subscriber(this, EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    string ownerName = subscriber.Method.DeclaringType?.FullName ?? subscriber.Method.Name;
                    subscriberFailures.Add(new ConfigReloadFailure(
                        ConfigReloadFailureKind.LegacySubscriber,
                        $"{ownerName}.{subscriber.Method.Name}",
                        ex));
                }
            }

            LastReloadResult = result.AppendLegacySubscriberResults(subscribers.Length, subscriberFailures);
            foreach (ConfigReloadFailure failure in LastReloadResult.Failures)
                log.Error($"Configuration reload failed for '{failure.OwnerName}'.", failure.Exception);
            return LastReloadResult;
        }

        private sealed class StrictJsonTextReader : JsonTextReader
        {
            public StrictJsonTextReader(TextReader reader)
                : base(reader)
            {
            }

            public override bool Read()
            {
                bool hasToken = base.Read();
                if (hasToken && TokenType == JsonToken.Comment)
                    throw new JsonReaderException("JSON comments are not valid configuration content.");
                return hasToken;
            }
        }

        public void Save<T1>() where T1 : IConfig => ExecutePersistence(SaveNoLock<T1>);

        private void SaveNoLock<T1>() where T1 : IConfig
        {
            var type = typeof(T1);
            var configName = type.Name;

            var configInstance = GetRequiredService<T1>();
            var jObject = ReadExistingConfigFile(ConfigFilePath);
            RemoveObsoleteConfigSections(jObject);
            var jsonSerializer = JsonSerializer.Create(JsonSerializerSettings);

            try
            {
                SaveConfig(jObject, type, configInstance, jsonSerializer);
            }
            catch (Exception ex)
            {
                log.Error(string.Format(Properties.Resources.SaveSingleConfigFailed, configName), ex);
                return;
            }

            try
            {
                WriteConfigFile(ConfigFilePath, jObject);
            }
            catch (Exception ex)
            {
                log.Error(string.Format(Properties.Resources.WriteConfigFileFailed, ConfigFilePath), ex);
            }
        }
    }



}
