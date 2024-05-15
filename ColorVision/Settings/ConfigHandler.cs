using ColorVision.MQTT;
using ColorVision.MySql;
using ColorVision.Services.RC;
using ColorVision.UI;
using log4net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ColorVision.Settings
{
    public static partial class GlobalConst
    {
        public const string ConfigFileName = "Config\\SoftwareConfig.json";

        public const string ConfigDIFileName = "Config\\ColorVisionConfig.json";

        public const string MQTTMsgRecordsFileName = "Config\\MsgRecords.json";

        public const string ConfigAESKey = "ColorVision";
        public const string ConfigAESVector = "ColorVision";

        public const string ConfigPath = "Config";

        public const string AutoRunRegPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        public const string AutoRunName = "ColorVisionAutoRun";

        public static readonly List<string> LogLevel = new() { "all","debug", "info", "warning", "error", "none" };
    }


    /// <summary>
    /// 全局设置
    /// </summary>
    public class ConfigHandler
    {
        private static ConfigHandler _instance;
        private static readonly object _locker = new();
        public static ConfigHandler GetInstance() { lock (_locker) { return _instance ??= new ConfigHandler(); } }

        internal static readonly ILog log = LogManager.GetLogger(typeof(ConfigHandler));
        public string SoftwareConfigFileName { get; set; }
        public string MQTTMsgRecordsFileName { get; set; }

        public string DIFile { get; set; }

        public ConfigHandler()
        {
            if (Directory.Exists(GlobalConst.ConfigPath))
            {
                SoftwareConfigFileName = AppDomain.CurrentDomain.BaseDirectory + GlobalConst.ConfigFileName;
                MQTTMsgRecordsFileName = GlobalConst.MQTTMsgRecordsFileName;
                DIFile = GlobalConst.ConfigDIFileName;
            }
            else
            {
                string DirectoryPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\ColorVision\\";
                if (!Directory.Exists(DirectoryPath))
                    Directory.CreateDirectory(DirectoryPath);
                SoftwareConfigFileName = DirectoryPath + GlobalConst.ConfigFileName;
                MQTTMsgRecordsFileName = DirectoryPath + GlobalConst.MQTTMsgRecordsFileName;
                DIFile = DirectoryPath+ GlobalConst.ConfigDIFileName;
            }

            SoftwareConfigLazy = new Lazy<SoftwareConfig>(() =>
            {
                SoftwareConfig config = ReadConfig<SoftwareConfig>(SoftwareConfigFileName);
                if (config != null)
                {
                    return config;
                }
                else
                {
                    return new SoftwareConfig();
                }
            });


            System.Windows.Application.Current.SessionEnding += (s, e) => 
            {
                SaveConfig();
            };
            AppDomain.CurrentDomain.ProcessExit += (s, e) =>
            {
                SaveConfig();
            };
        }

        public static SystemMonitor SystemMonitor => SystemMonitor.GetInstance();

        readonly Lazy<SoftwareConfig> SoftwareConfigLazy;

        public SoftwareConfig SoftwareConfig { get => SoftwareConfigLazy.Value; }


        public void SaveConfig()
        {
            WriteConfig(SoftwareConfigFileName, SoftwareConfig);
        }

        private static JsonSerializerOptions jsonSerializerOptions = new() { WriteIndented = true,DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        private static T? ReadConfig<T>(string fileName)
        {
            if (File.Exists(fileName))
            {
                try
                {
                    return JsonSerializer.Deserialize<T>(File.ReadAllText(fileName), jsonSerializerOptions);
                }
                catch(Exception ex)
                {
                    log.Warn(ex);
                    log.Info($"读取配置文件{fileName}失败，正常初始化配置文件");
                    T t = (T)Activator.CreateInstance(typeof(T));
                    WriteConfig(fileName, t);
                    return t;
                }
            }

            else
            {
                T t = (T)Activator.CreateInstance(typeof(T));
                WriteConfig(fileName,t);
                return t;
            }
        }

        private static void WriteConfig<T>(string fileName, T? t)
        {
            string DirectoryName = Path.GetDirectoryName(fileName);
            if (!string.IsNullOrWhiteSpace(DirectoryName) && !Directory.Exists(DirectoryName))
                Directory.CreateDirectory(DirectoryName);

            if (File.Exists(fileName))
            {
                FileInfo fileInfo = new(fileName);
                fileInfo.IsReadOnly = false;
            }

            string jsonString = JsonSerializer.Serialize(t, jsonSerializerOptions);
            File.WriteAllText(fileName, jsonString);
        }





    }
}
