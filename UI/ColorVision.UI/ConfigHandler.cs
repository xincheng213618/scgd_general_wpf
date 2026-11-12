using ColorVision.UI.Authorizations;
using ColorVision.UI.Json;
using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;

namespace ColorVision.UI
{
    public enum ConfigSavePublicationStatus
    {
        NotPersisted,
        PersistedAndPublished,
        PersistedButPublishFailed,
    }

    public partial class ConfigHandler : IConfigService, IConfigReloadNotifier
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
                    LoadDefaultConfigsCore(failedLoadPath: fullPath);
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
    }
}
