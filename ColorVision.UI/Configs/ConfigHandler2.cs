//using log4net;
//using System.IO;
//using System.Text.Json;
//using System.Text.Json.Serialization;

//namespace ColorVision.UI
//{
//    /// <summary>
//    /// 这里是旧的写法，留作纪念
//    /// </summary>
    
//    public class ConfigHandler2
//    {
//        private static ConfigHandler2 _instance;
//        private static readonly object _locker = new();
//        public static ConfigHandler2 GetInstance() { lock (_locker) { return _instance ??= new ConfigHandler2(); } }

//        internal static readonly ILog log = LogManager.GetLogger(typeof(ConfigHandler2));
//        public string SoftwareConfigFileName { get; set; }
//        public string MQTTMsgRecordsFileName { get; set; }

//        public ConfigHandler2()
//        {
//            //SoftwareConfigLazy = new Lazy<SoftwareConfig>(() =>
//            //{
//            //    SoftwareConfig config = ReadConfig<SoftwareConfig>(SoftwareConfigFileName);
//            //    if (config != null)
//            //    {
//            //        return config;
//            //    }
//            //    else
//            //    {
//            //        return new SoftwareConfig();
//            //    }
//            //});


//            //System.Windows.Application.Current.SessionEnding += (s, e) => 
//            //{
//            //    SaveConfig();
//            //};
//            //AppDomain.CurrentDomain.ProcessExit += (s, e) =>
//            //{
//            //    SaveConfig();
//            //};
//        }

//        private static JsonSerializerOptions jsonSerializerOptions = new() { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
//        private static T? ReadConfig<T>(string fileName)
//        {
//            if (File.Exists(fileName))
//            {
//                try
//                {
//                    return JsonSerializer.Deserialize<T>(File.ReadAllText(fileName), jsonSerializerOptions);
//                }
//                catch (Exception ex)
//                {
//                    log.Warn(ex);
//                    log.Info($"读取配置文件{fileName}失败，正常初始化配置文件");
//                    T t = (T)Activator.CreateInstance(typeof(T));
//                    WriteConfig(fileName, t);
//                    return t; 
//                }
//            }

//            else
//            {
//                T t = (T)Activator.CreateInstance(typeof(T));
//                WriteConfig(fileName, t);
//                return t;
//            }
//        }

//        private static void WriteConfig<T>(string fileName, T? t)
//        {
//            string DirectoryName = Path.GetDirectoryName(fileName);
//            if (!string.IsNullOrWhiteSpace(DirectoryName) && !Directory.Exists(DirectoryName))
//                Directory.CreateDirectory(DirectoryName);

//            if (File.Exists(fileName))
//            {
//                FileInfo fileInfo = new(fileName);
//                fileInfo.IsReadOnly = false;
//            }

//            string jsonString = JsonSerializer.Serialize(t, jsonSerializerOptions);
//            File.WriteAllText(fileName, jsonString);
//        }





//    }

//}
