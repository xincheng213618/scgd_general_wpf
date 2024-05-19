using System.IO;
using System.Text.Json;
using static System.Windows.Forms.Design.AxImporter;

namespace ColorVision.UI
{
    public class ConfigBase<T> where T : IConfig
    {
        internal readonly JsonSerializerOptions _options = new JsonSerializerOptions { WriteIndented = true };

        public Dictionary<Type, IConfig> Configs { get; set; }

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
                foreach (var type in assembly.GetTypes().Where(t => typeof(T).IsAssignableFrom(t) && !t.IsAbstract))
                {
                    if (Activator.CreateInstance(type) is T config)
                    {
                        Configs[type] = config;
                    }
                }
            }
        }

        public virtual void SaveConfigs(string fileName)
        {
            using (var outputStream = File.Create(fileName))
            {
                using (var jsonWriter = new Utf8JsonWriter(outputStream, new JsonWriterOptions { Indented = true }))
                {
                    jsonWriter.WriteStartObject();
                    foreach (var configPair in Configs)
                    {
                        jsonWriter.WritePropertyName(configPair.Key.Name);
                        if (configPair.Value is IConfigSecure configSecure)
                        {
                            configSecure.Encryption();
                            JsonSerializer.Serialize(jsonWriter, configPair.Value, configPair.Key, _options);
                            configSecure.Decrypt();
                        }
                        else
                        {
                            JsonSerializer.Serialize(jsonWriter, configPair.Value, configPair.Key, _options);
                        }
                    }
                    jsonWriter.WriteEndObject();
                }
            }
        }


        public virtual void LoadConfigs(string fileName)
        {
            Configs = new Dictionary<Type, IConfig>();
            if (File.Exists(fileName))
            {
                try
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

        public const string ConfigDIFileName = "Config\\ColorVisionConfig.json";

        public ConfigHandler()
        {


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

        public void SaveConfigs() => SaveConfigs(DIFile);

        public override void SaveConfigs(string fileName)
        {
            using var outputStream = File.Create(fileName);
            using var jsonWriter = new Utf8JsonWriter(outputStream, new JsonWriterOptions { Indented = true });
            jsonWriter.WriteStartObject();
            foreach (var configPair in Configs)
            {
                jsonWriter.WritePropertyName(configPair.Key.Name);
                if (configPair.Value is IConfigSecure configSecure)
                {
                    configSecure.Encryption();
                    JsonSerializer.Serialize(jsonWriter, configPair.Value, configPair.Key, _options);
                    configSecure.Decrypt();
                }
                else
                {
                    JsonSerializer.Serialize(jsonWriter, configPair.Value, configPair.Key, _options);
                }
            }
            jsonWriter.WriteEndObject();
        }

        public override void LoadConfigs(string fileName)
        {
            Configs = new Dictionary<Type, IConfig>();
            if (File.Exists(fileName))
            {
                try
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
                                    if (JsonSerializer.Deserialize(configElement.GetRawText(), type, _options) is IConfigSecure  configSecure)
                                    {
                                        configSecure.Decrypt();
                                        Configs[type] = configSecure;
                                    }
                                    else if (JsonSerializer.Deserialize(configElement.GetRawText(), type, _options) is IConfig config)
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
