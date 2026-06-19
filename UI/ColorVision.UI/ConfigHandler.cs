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
    public class ConfigHandler : IConfigService
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

        public ConfigHandler()
        {
        }

        public void Load()
        {
            JsonSerializerSettings = CreateJsonSerializerSettings();
            InitDateTime = DateTime.Now;

            InitializePaths();
            LoadConfigs(ConfigFilePath);
            ScheduleBackup();

            Authorization.Instance = GetRequiredService<Authorization>();

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

        public void Reload()
        {
            SaveConfigs();
            LoadConfigs(ConfigFilePath);
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
            jObject = new JObject();
            if (!File.Exists(fileName))
                return false;

            try
            {
                using StreamReader file = File.OpenText(fileName);
                using JsonTextReader reader = new(file);
                jObject = JObject.Load(reader);
                return true;
            }
            catch (Exception ex)
            {
                logException(ex);
                return false;
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

        public void LoadDefaultConfigs()
        {
            try
            {
                if (TryRestoreLatestBackup())
                    return;

                LoadDefaultConfigInstances();
            }
            catch (Exception ex)
            {
                log.Error(Properties.Resources.RestoreConfigFileFailed, ex);
            }
        }

        private bool TryRestoreLatestBackup()
        {
            foreach (string backupFile in GetBackupFiles())
            {
                if (!TryReadConfigFile(backupFile, out var backupJson, ex => log.Warn(ex)))
                    continue;

                jsonObject = backupJson;
                Configs = new ConcurrentDictionary<Type, IConfig>();
                File.Copy(backupFile, ConfigFilePath, true);
                return true;
            }

            return false;
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
            Configs = new ConcurrentDictionary<Type, IConfig>();
            jsonObject = new JObject();

            if (TryReadConfigFile(fileName, out var loadedJson, ex => log.Warn(ex)))
                jsonObject = loadedJson;
            else
                LoadDefaultConfigs();
        }

        public void Save<T1>() where T1 : IConfig
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
