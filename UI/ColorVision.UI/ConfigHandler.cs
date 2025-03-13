#pragma warning disable CS8604
using ColorVision.UI.Authorizations;
using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Windows;

namespace ColorVision.UI
{
    public static class AssemblyHandlerExtension
    {
        public static void LoadImplementations<T>(this ObservableCollection<T> interfaces) 
        {
            interfaces.Clear();
            foreach (var assembly in AssemblyHandler.GetInstance().GetAssemblies())
            {
                foreach (Type type in assembly.GetTypes().Where(t => typeof(T).IsAssignableFrom(t) && !t.IsAbstract))
                {
                    if (Activator.CreateInstance(type) is T imageEditorFunction)
                    {
                        interfaces.Add(imageEditorFunction);
                    }
                }
            }
        }
    }


    public class AssemblyHandler
    {
        private static ILog log = LogManager.GetLogger(typeof(AssemblyHandler));
        private static AssemblyHandler _instance;
        private static readonly object _locker = new();
        public static AssemblyHandler GetInstance()
        {
            lock (_locker)
            {
                _instance ??= new AssemblyHandler();
                return _instance;
            }
        }

        public Assembly[] GetAssemblies()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            return assemblies.Where(a =>
                !RemoveAssemblies.Contains(a) &&
                !RemoveAssemblyNames.Contains(a.GetName().Name)
            ).ToArray();
        }
        public List<Assembly> RemoveAssemblies { get; set; } = new List<Assembly>();
        public List<string> RemoveAssemblyNames { get; set; } = new List<string>();



    }

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
            string AssemblyCompany = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company ?? "ColorVision";
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
            //Application.Current.SessionEnding += (s, e) =>
            //{
            //    SaveConfigs(ConfigFilePath);
            //};
            AppDomain.CurrentDomain.ProcessExit += (s, e) =>
            {
                if (IsAutoSave)
                    SaveConfigs(ConfigFilePath);
            };
            Authorization.Instance = GetRequiredService<Authorization>();
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
                try
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
                catch(Exception ex)
                {
                    log.Info(configPair.Key);
                    log.Error(ex);
                }

            }

            File.WriteAllText(fileName, jObject.ToString(JsonSerializerSettings.Formatting));
        }

        public void LoadDefaultConfigs()
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

                    //foreach (var assembly in AssemblyHandler.GetInstance().GetAssemblies())
                    //{
                    //    try
                    //    {
                    //        foreach (var type in assembly.GetTypes())
                    //        {
                    //            if (typeof(IConfig).IsAssignableFrom(type) && !type.IsInterface)
                    //            {
                    //                var configName = type.Name;
                    //                try
                    //                {
                    //                    if (jsonObject.TryGetValue(configName, out JToken configToken))
                    //                    {
                    //                        var config = configToken.ToObject(type, new JsonSerializer { Formatting = Formatting.Indented });
                    //                        if (config is IConfigSecure configSecure)
                    //                        {
                    //                            configSecure.Decrypt();
                    //                            Configs[type] = configSecure;
                    //                        }
                    //                        else if (config is IConfig configInstance)
                    //                        {
                    //                            Configs[type] = configInstance;
                    //                        }
                    //                    }
                    //                    else
                    //                    {
                    //                        if (Activator.CreateInstance(type) is IConfig defaultConfig)
                    //                        {
                    //                            Configs[type] = defaultConfig;
                    //                        }
                    //                    }
                    //                }
                    //                catch (Exception ex)
                    //                {
                    //                    log.Warn(ex);
                    //                    if (Activator.CreateInstance(type) is IConfig defaultConfig)
                    //                    {
                    //                        Configs[type] = defaultConfig;
                    //                    }
                    //                }
                    //            }
                    //        }
                    //    }
                    //    catch (Exception ex)
                    //    {
                    //        AssemblyHandler.GetInstance().RemoveAssemblies.Add(assembly);
                    //        MessageBox.Show("程序集加载失败，现在跳过该程序集，如果您不想要该弹窗提示，您需要移除插件：" + assembly);
                    //        log.Warn(ex);
                    //    }
                    //}


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
