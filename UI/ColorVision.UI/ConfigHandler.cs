using ColorVision.Common.MVVM;
using ColorVision.UI.Authorizations;
using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Windows;

namespace ColorVision.UI
{
    [DisplayName("配置相关参数")]
    public class ConfigOptions:ViewModelBase, IConfig
    {
        [DisplayName("是否启用定时备份")]
        public bool EnableBackup { get => _EnableBackup; set { _EnableBackup = value; OnPropertyChanged(); } }
        private bool _EnableBackup = true;
    }
    /// <summary>
    /// 加载插件
    /// </summary>

    public class ConfigHandler: IConfigService
    {
        private static ILog log = LogManager.GetLogger(typeof(ConfigHandler));
        private static ConfigHandler _instance;
        private static readonly object _locker = new();
        public static ConfigHandler GetInstance() 
        {
            lock (_locker) 
            {
                _instance ??= new ConfigHandler();
                ConfigService.SetInstance(_instance);
                AssemblyHandler.GetInstance();
                return _instance; 
            }
        }
        public ConfigOptions Options => GetRequiredService<ConfigOptions>();

        public string ConfigFilePath { get; set; }
        public string BackupFolderPath { get; set; }

        public DateTime InitDateTime { get; set; }

        public string ConfigDIFileName { get; set; }

        public ConfigHandler()
        {
            InitDateTime = DateTime.Now;
            string AssemblyCompany = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company ?? Assembly.GetEntryAssembly()?.GetName().Name;
            ConfigDIFileName =  $"{Assembly.GetEntryAssembly()?.GetName().Name ?? AssemblyCompany}Config";
            string backupDirName = "Backup";
            if (Directory.Exists("Config"))
            {
                ConfigFilePath = $"Config\\{ConfigDIFileName}.json";
                BackupFolderPath = $"Config\\{backupDirName}\\";
            }
            else
            {
                string DirectoryPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + $"\\{AssemblyCompany}\\Config\\";
                if (!Directory.Exists(DirectoryPath))
                    Directory.CreateDirectory(DirectoryPath);
                ConfigFilePath = DirectoryPath + ConfigDIFileName +".json";
                BackupFolderPath = DirectoryPath + backupDirName + "\\";
            }

            if (!Directory.Exists(BackupFolderPath))
                Directory.CreateDirectory(BackupFolderPath);
            LoadConfigs(ConfigFilePath);
            if (Options.EnableBackup)
            {
                Task.Delay(10000).ContinueWith(t =>
                {
                    BackupConfigs();
                });
            }

            Authorization.Instance = GetRequiredService<Authorization>();

            AppDomain.CurrentDomain.ProcessExit += (s, e) =>
            {
                if (IsAutoSave)
                    SaveConfigs(ConfigFilePath);
            };
        }


        public bool IsAutoSave { get; set; } = true;

        public void Reload()
        {
            SaveConfigs();
            LoadConfigs(ConfigFilePath);
        }

        public void SaveConfigs() => SaveConfigs(ConfigFilePath);

        internal readonly JsonSerializerSettings JsonSerializerSettings = new JsonSerializerSettings { Formatting = Formatting.Indented };

        public Dictionary<Type, IConfig> Configs { get; set; }

        public IConfig GetRequiredService(Type type)
        {
            ArgumentNullException.ThrowIfNull(type);
            if (!typeof(IConfig).IsAssignableFrom(type))
                throw new ArgumentException("Type must implement IConfig.", nameof(type));

            if (Configs.TryGetValue(type, out var service))
            {
                return (IConfig)service;
            }

            var configName = type.Name;
            try
            {
                if (jsonObject.TryGetValue(configName, out JToken configToken))
                {
                    var config = configToken.ToObject(type, new JsonSerializer { Formatting = Formatting.Indented });
                    if (config is IConfigSecure configSecure)
                    {
                        configSecure.Decrypt();
                        Configs[type] = configSecure;
                    }
                    else if (config is IConfig configInstance)
                    {
                        Configs[type] = configInstance;
                    }
                }
                else
                {
                    if (Activator.CreateInstance(type) is IConfig defaultConfig)
                    {
                        Configs[type] = defaultConfig;
                    }
                }
            }
            catch (Exception ex)
            {
                log.Warn(ex);
                if (Activator.CreateInstance(type) is IConfig defaultConfig)
                {
                    Configs[type] = defaultConfig;
                }
            }
            // 此处递归调用是为了确保缓存和异常处理逻辑一致
            return GetRequiredService(type);
        }

        public T1 GetRequiredService<T1>() where T1 : IConfig
        {
            var type = typeof(T1);
            if (Configs.TryGetValue(type, out var service))
            {
                return (T1)service;
            }

            var configName = type.Name;
            try
            {
                if (jsonObject.TryGetValue(configName, out JToken configToken))
                {
                    var config = configToken.ToObject(type, new JsonSerializer { Formatting = Formatting.Indented });
                    if (config is IConfigSecure configSecure)
                    {
                        configSecure.Decrypt();
                        Configs[type] = configSecure;
                    }
                    else if (config is IConfig configInstance)
                    {
                        Configs[type] = configInstance;
                    }
                }
                else
                {
                    if (Activator.CreateInstance(type) is IConfig defaultConfig)
                    {
                        Configs[type] = defaultConfig;
                    }
                }
            }
            catch (Exception ex)
            {
                log.Warn(ex);
                if (Activator.CreateInstance(type) is IConfig defaultConfig)
                {
                    Configs[type] = defaultConfig;
                }
            }
            return GetRequiredService<T1>();
        }

        public void SaveConfigs(string fileName)
        {
            var jObject = new JObject();
            if (File.Exists(fileName))
            {
                string json = File.ReadAllText(fileName);
                try
                {
                    jObject = JObject.Parse(json);
                }
                catch (Exception ex)
                {
                    log.Error(ex);
                    MessageBox.Show(Properties.Resources.ConfigFileResetDueToError);
                }
            }
            //防止被修改
            var configsSnapshot = Configs.ToArray();

            foreach (var configPair in configsSnapshot)
            {
                try
                {
                    if (Application.Current == null)
                    {
                        if (configPair.Value is IConfigSecure configSecure)
                        {
                            configSecure.Encryption();
                            jObject[configPair.Key.Name] = JToken.FromObject(configPair.Value, JsonSerializer.Create(JsonSerializerSettings));
                            configSecure.Decrypt();
                        }
                        else
                        {
                            jObject[configPair.Key.Name] = JToken.FromObject(configPair.Value, JsonSerializer.Create(JsonSerializerSettings));
                        }
                    }
                    else if (Application.Current.Dispatcher.CheckAccess())
                    {
                        if (configPair.Value is IConfigSecure configSecure)
                        {
                            configSecure.Encryption();
                            jObject[configPair.Key.Name] = JToken.FromObject(configPair.Value, JsonSerializer.Create(JsonSerializerSettings));
                            configSecure.Decrypt();
                        }
                        else
                        {
                            jObject[configPair.Key.Name] = JToken.FromObject(configPair.Value, JsonSerializer.Create(JsonSerializerSettings));
                        }
                    }
                    else
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (configPair.Value is IConfigSecure configSecure)
                            {
                                configSecure.Encryption();
                                jObject[configPair.Key.Name] = JToken.FromObject(configPair.Value, JsonSerializer.Create(JsonSerializerSettings));
                                configSecure.Decrypt();
                            }
                            else
                            {
                                jObject[configPair.Key.Name] = JToken.FromObject(configPair.Value, JsonSerializer.Create(JsonSerializerSettings));
                            }

                        });
                    }



                }
                catch(Exception ex)
                {
                    log.Info(configPair.Key);
                    log.Error(ex);
                }
            }

            using (StreamWriter file = File.CreateText(fileName))
            {
                using (JsonTextWriter writer = new JsonTextWriter(file))
                {
                    jObject.WriteTo(writer);
                }
            }
        }

        public void LoadDefaultConfigs()
        {
            try
            {
                var files = Directory.GetFiles(BackupFolderPath, $"{ConfigDIFileName}Backup_*.json")
                    .OrderByDescending(f => f)
                    .ToList();
                if (files.Count !=0)
                {
                    LoadConfigs(files.First());
                    File.Copy(files.First(), ConfigFilePath, true);
                    MessageBox.Show(Properties.Resources.ConfigFileRestored, Properties.Resources.ConfirmUpdate);
                }
                else
                {
                    foreach (var assembly in AssemblyHandler.GetInstance().GetAssemblies())
                    {
                        try
                        {
                            foreach (var type in assembly.GetTypes().Where(t => typeof(IConfig).IsAssignableFrom(t) && !t.IsAbstract))
                            {
                                if (Activator.CreateInstance(type) is IConfig config)
                                {
                                    Configs[type] = config;
                                }
                            }
                        }
                        catch
                        {

                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error(Properties.Resources.RestoreConfigFileFailed, ex);
                MessageBox.Show(Properties.Resources.RestoreConfigFileFailed);
            }


        }

        public void BackupConfigs()
        {
            try
            {
                string backupFileName = $"{ConfigDIFileName}Backup_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                string backupPath = Path.Combine(BackupFolderPath, backupFileName);
                CleanupOldBackups();
                SaveConfigs(backupPath);
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
                var files = Directory.GetFiles(BackupFolderPath, "ConfigBackup_*.json")
                    .OrderByDescending(f => f)
                    .ToList();
                if (files.Count > 10)
                {
                    foreach (var file in files.Skip(10))
                    {
                        File.Delete(file);
                    }
                }
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
            Configs = new Dictionary<Type, IConfig>();
            if (File.Exists(fileName))
            {
                try
                {
                    using (StreamReader file = File.OpenText(fileName))
                    using (JsonTextReader reader = new JsonTextReader(file))
                    {
                        jsonObject = (JObject)JToken.ReadFrom(reader);
                    }
                }
                catch(Exception ex)
                {
                    log.Warn(ex);
                    LoadDefaultConfigs();
                }
            }
            else
            {
                LoadDefaultConfigs();
            }
        }

        public void Save<T1>() where T1 : IConfig
        {
            var type = typeof(T1);
            var configName = type.Name;

            if (Configs == null)
            {
                Configs = new Dictionary<Type, IConfig>();
            }

            // Ensure the config instance exists (will load or create default)
            var configInstance = GetRequiredService<T1>();

            JObject jObject = new JObject();
            if (File.Exists(ConfigFilePath))
            {
                try
                {
                    jObject = JObject.Parse(File.ReadAllText(ConfigFilePath));
                }
                catch (Exception ex)
                {
                    log.Error(ex);
                    // If parse fails, start with a clean object to avoid corrupt carry-over
                    jObject = new JObject();
                }
            }

            void Persist()
            {
                if (configInstance is IConfigSecure secure)
                {
                    secure.Encryption();
                    jObject[configName] = JToken.FromObject(configInstance, JsonSerializer.Create(JsonSerializerSettings));
                    secure.Decrypt();
                }
                else
                {
                    jObject[configName] = JToken.FromObject(configInstance, JsonSerializer.Create(JsonSerializerSettings));
                }
            }

            try
            {
                if (Application.Current == null)
                {
                    Persist();
                }
                else if (Application.Current.Dispatcher.CheckAccess())
                {
                    Persist();
                }
                else
                {
                    Application.Current.Dispatcher.Invoke(Persist);
                }
            }
            catch (Exception ex)
            {
                log.Error(string.Format(Properties.Resources.SaveSingleConfigFailed, configName), ex);
                return;
            }

            try
            {
                using (StreamWriter file = File.CreateText(ConfigFilePath))
                using (JsonTextWriter writer = new JsonTextWriter(file))
                {
                    jObject.WriteTo(writer);
                }
            }
            catch (Exception ex)
            {
                log.Error(string.Format(Properties.Resources.WriteConfigFileFailed, ConfigFilePath), ex);
            }
        }
    }

}
