﻿using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Reflection;
using System.Windows;
namespace ColorVision.UI
{

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
                return _instance; 
            }
        }

        public string ConfigFilePath { get; set; }
        public ConfigHandler()
        {
            string AssemblyCompany = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyCompanyAttribute>()?.Company ?? "ColorVision";
            string ConfigDIFileName = $"{AssemblyCompany}Config.json";
            if (Directory.Exists("Config"))
            {
                ConfigFilePath = $"Config\\{ConfigDIFileName}";
            }
            else
            {
                string DirectoryPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + $"\\{AssemblyCompany}\\Config\\";
                if (!Directory.Exists(DirectoryPath))
                    Directory.CreateDirectory(DirectoryPath);
                ConfigFilePath = DirectoryPath + ConfigDIFileName;
            }

            LoadConfigs(ConfigFilePath);
            Application.Current.SessionEnding += (s, e) =>
            {
                SaveConfigs(ConfigFilePath);
            };
            AppDomain.CurrentDomain.ProcessExit += (s, e) =>
            {
                SaveConfigs(ConfigFilePath);
            };
        }

        public void Reload()
        {
            SaveConfigs();
            LoadConfigs(ConfigFilePath);
        }

        public void SaveConfigs() => SaveConfigs(ConfigFilePath);

        internal readonly JsonSerializerSettings JsonSerializerSettings = new JsonSerializerSettings { Formatting = Formatting.Indented };

        public Dictionary<Type, IConfig> Configs { get; set; }

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
                }catch(Exception ex)
                {
                    log.Error(ex);
                    MessageBox.Show("配置文件异常,已经重置");
                }
            }
            foreach (var configPair in Configs)
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

            File.WriteAllText(fileName, jObject.ToString(JsonSerializerSettings.Formatting));
        }

        public void LoadDefaultConfigs()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
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

        public void LoadConfigs() => LoadConfigs(ConfigFilePath);

        private JObject jsonObject;

        public void LoadConfigs(string fileName)
        {
            Configs = new Dictionary<Type, IConfig>();
            if (File.Exists(fileName))
            {
                try
                {
                    string json = File.ReadAllText(fileName);
                    jsonObject = JObject.Parse(json);

                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        foreach (var type in assembly.GetTypes())
                        {
                            if (typeof(IConfig).IsAssignableFrom(type) && !type.IsInterface)
                            {
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
                            }
                        }
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

    }

}