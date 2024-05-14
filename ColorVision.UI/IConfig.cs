using System.IO;
using System.Text.Json;

namespace ColorVision.UI
{
    //属性继承配置，用于配置属性继承，例如：配置文件中的属性继承
    public interface IConfig
    {

    }


    public class ConfigHandler1
    {
        private static ConfigHandler1 _instance;
        private static readonly object _locker = new();
        public static ConfigHandler1 GetInstance() { lock (_locker) { return _instance ??= new ConfigHandler1(); } }
        public string DIFile { get; set; }

        public const string ConfigDIFileName = "Config\\ColorVisionConfig.json";

        public ConfigHandler1()
        {
            _options = new JsonSerializerOptions
            {
                WriteIndented = true, // 美化输出
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            if (Directory.Exists("Config"))
            {
                DIFile = ConfigDIFileName;
            }
            else
            {
                string DirectoryPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\ColorVision\\";
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


        private readonly JsonSerializerOptions _options;
        public T GetRequiredService<T>() where T : IConfig
        {
            var type = typeof(T);
            if (Configs.TryGetValue(type, out var service))
            {
                return (T)service;
            }
            throw new InvalidOperationException($"Service of type {type.FullName} not registered.");
        }
        public Dictionary<Type, IConfig> Configs { get; set; }

        public void SaveConfigs(string fileName)
        {
            using (var outputStream = File.Create(fileName))
            {
                using (var jsonWriter = new Utf8JsonWriter(outputStream, new JsonWriterOptions { Indented = true }))
                {
                    jsonWriter.WriteStartObject();
                    foreach (var configPair in Configs)
                    {
                        jsonWriter.WritePropertyName(configPair.Key.Name);
                        JsonSerializer.Serialize(jsonWriter, configPair.Value, configPair.Key, _options);
                    }
                    jsonWriter.WriteEndObject();
                }
            }
        }

        public void LoadConfigs(string fileName)
        {
            Configs = new Dictionary<Type, IConfig>();
            if (File.Exists(fileName))
            {
                string json = File.ReadAllText(fileName);
                var jsonDoc = JsonDocument.Parse(json);
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        if (typeof(IConfig).IsAssignableFrom(type) && !type.IsInterface)
                        {
                            var configName = type.Name;
                            if (jsonDoc.RootElement.TryGetProperty(configName, out var configElement))
                            {
                                if (JsonSerializer.Deserialize(configElement.GetRawText(), type, _options) is IConfig config)
                                {
                                    Configs[type] = config;
                                }
                            }
                            else
                            {
                                if (Activator.CreateInstance(type) is IConfig config)
                                {
                                    Configs[type] = config;
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    foreach (var type in assembly.GetTypes().Where(t => typeof(IConfig).IsAssignableFrom(t) && !t.IsAbstract))
                    {
                        if (Activator.CreateInstance(type) is IConfig config)
                        {
                            Configs[type] = config;
                        }
                    }
                }
            }

        }




    }




}
