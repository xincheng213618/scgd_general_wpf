﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
namespace ColorVision.UI
{

    public class ConfigBase<T> where T : IConfig
    {
        internal readonly JsonSerializerSettings JsonSerializerSettings  = new JsonSerializerSettings { Formatting = Formatting.Indented };

        public Dictionary<Type, T> Configs { get; set; }

        public T1 GetRequiredService<T1>() where T1:T
        { 
            var type = typeof(T1);
            if (Configs.TryGetValue(type, out var service))
            {
                return (T1)service;
            }
            throw new InvalidOperationException($"Service of type {type.FullName} not registered.");
        }

        public void LoadDefaultConfigs()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var type in assembly.GetTypes().Where(t => typeof(T).IsAssignableFrom(t) && !t.IsAbstract))
                    {
                        if (Activator.CreateInstance(type) is T config)
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

        public virtual void SaveConfigs(string fileName)
        {
            var jObject = new JObject();

            foreach (var configPair in Configs)
            {
                var valueToken = JToken.FromObject(configPair.Value, JsonSerializer.Create(JsonSerializerSettings));
                jObject[configPair.Key.Name] = valueToken;
            }

            File.WriteAllText(fileName, jObject.ToString(JsonSerializerSettings.Formatting));
        }


        public virtual void LoadConfigs(string fileName)
        {
            Configs = new Dictionary<Type, T>();
            if (File.Exists(fileName))
            {
                try
                {
                    string json = File.ReadAllText(fileName);
                    var jsonObject = JObject.Parse(json);
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
                                        if (config is T typedConfig)
                                        {
                                            Configs[type] = typedConfig;
                                        }
                                    }
                                    else
                                    {
                                        if (Activator.CreateInstance(type) is T defaultConfig)
                                        {
                                            Configs[type] = defaultConfig;
                                        }
                                    }
                                }
                                catch
                                {
                                    if (Activator.CreateInstance(type) is T config)
                                    {
                                        Configs[type] = config;
                                    }
                                }

                            }
                        }
                    }
                }
                catch
                {
                    LoadDefaultConfigs();
                }
            }
            else
            {
                LoadDefaultConfigs();
            }
        }
    }


    public class ConfigHandler:ConfigBase<IConfig>
    {
        private static ConfigHandler _instance;
        private static readonly object _locker = new();
        public static ConfigHandler GetInstance() { lock (_locker) { return _instance ??= new ConfigHandler(); } }
        public string DIFile { get; set; }

        public const string ConfigDIFileName = "ColorVisionConfig.json";

        public ConfigHandler()
        {
            if (Directory.Exists("Config"))
            {
                DIFile = $"Config\\{ConfigDIFileName}";
            }
            else
            {
                string DirectoryPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\ColorVision\\Config\\";
                if (!Directory.Exists(DirectoryPath))
                    Directory.CreateDirectory(DirectoryPath);
                DIFile = DirectoryPath + ConfigDIFileName;
            }

            LoadConfigs(DIFile);
            System.Windows.Application.Current.SessionEnding += (s, e) =>
            {
                SaveConfigs(DIFile);
            };
            AppDomain.CurrentDomain.ProcessExit += (s, e) =>
            {
                SaveConfigs(DIFile);
            };
        }

        public void SaveConfigs() => SaveConfigs(DIFile);


        public override void SaveConfigs(string fileName)
        {
            var jObject = new JObject();
            if (File.Exists(fileName))
            {
                string json = File.ReadAllText(fileName);
                jObject = JObject.Parse(json);
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



        public override void LoadConfigs(string fileName)
        {
            Configs = new Dictionary<Type, IConfig>();
            if (File.Exists(fileName))
            {
                try
                {
                    string json = File.ReadAllText(fileName);
                    var jsonObject = JObject.Parse(json);

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
                                catch 
                                {
                                    if (Activator.CreateInstance(type) is IConfig defaultConfig)
                                    {
                                        Configs[type] = defaultConfig;
                                    }
                                }
                            }
                        }
                    }
                }
                catch
                {
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
