using ColorVision.MVVM;
using ColorVision.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ColorVision.SettingUp
{
    public static class GlobalConst
    {
        public const string SoftwareConfigFileName = "Config\\SoftwareConfig.json";
        public const string MQTTMsgRecordsFileName = "Config\\MsgRecords.json";

        public const string SoftwareConfigAESKey = "ColorVision";
        public const string SoftwareConfigAESVector = "ColorVision";


        public const string AutoRunRegPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        public const string AutoRunName = "ColorVisionAutoRun";


        public static readonly List<string> LogLevel = new() { "all","debug", "info", "warning", "error", "none" };
    }


    /// <summary>
    /// 全局设置
    /// </summary>
    public class GlobalSetting:ViewModelBase
    {
        private static GlobalSetting _instance;
        private static readonly object _locker = new();
        public static GlobalSetting GetInstance() { lock (_locker) { return _instance ??= new GlobalSetting(); } }

        public GlobalSetting()
        {
            SoftwareConfigFileName = AppDomain.CurrentDomain.BaseDirectory + GlobalConst.SoftwareConfigFileName;
            SoftwareConfigLazy = new Lazy<SoftwareConfig>(() =>
            {
                SoftwareConfig config = ReadConfig<SoftwareConfig>(SoftwareConfigFileName);
                if (config != null)
                {
                    config.MySqlConfig.UserPwd = Encrypt.AESDecrypt(config.MySqlConfig.UserPwd, GlobalConst.SoftwareConfigAESKey, GlobalConst.SoftwareConfigAESVector);
                    config.MQTTConfig.UserPwd = Encrypt.AESDecrypt(config.MQTTConfig.UserPwd, GlobalConst.SoftwareConfigAESKey, GlobalConst.SoftwareConfigAESVector);
                    config.UserConfig.UserPwd = Encrypt.AESDecrypt(config.UserConfig.UserPwd, GlobalConst.SoftwareConfigAESKey, GlobalConst.SoftwareConfigAESVector);
                    foreach (var item in config.MySqlConfigs)
                        item.UserPwd = Encrypt.AESDecrypt(item.UserPwd, GlobalConst.SoftwareConfigAESKey, GlobalConst.SoftwareConfigAESVector);
                    foreach (var item in config.MQTTConfigs)
                        item.UserPwd = Encrypt.AESDecrypt(item.UserPwd, GlobalConst.SoftwareConfigAESKey, GlobalConst.SoftwareConfigAESVector);


                    return config;
                }
                else
                {
                    return new SoftwareConfig();
                }
            });
            SoftwareConfig.Version = System.Reflection.Assembly.GetExecutingAssembly().GetName()?.Version?.ToString() ?? "1.0";

            AppDomain.CurrentDomain.ProcessExit += (s, e) =>
            {
                SaveSoftwareConfig();
            };

            PerformanceControlLazy = new Lazy<PerformanceControl>(() => PerformanceControl.GetInstance());
        }



        public bool IsAutoRun { get => Util.Tool.IsAutoRun(); set { Util.Tool.SetAutoRun(value); NotifyPropertyChanged(); } }

        [JsonIgnore]
        readonly Lazy<PerformanceControl> PerformanceControlLazy;
        [JsonIgnore]
        public PerformanceControl PerformanceControl { get => PerformanceControlLazy.Value; }
        
        public string SoftwareConfigFileName { get; set; }

        readonly Lazy<SoftwareConfig> SoftwareConfigLazy;

        public SoftwareConfig SoftwareConfig { get => SoftwareConfigLazy.Value; }


        public void SaveSoftwareConfig()
        {
            string Temp0 = SoftwareConfig.MySqlConfig.UserPwd;
            string Temp1 = SoftwareConfig.MQTTConfig.UserPwd;
            string Temp2 = SoftwareConfig.UserConfig.UserPwd;

            SoftwareConfig.MySqlConfig.UserPwd = Encrypt.AESEncrypt(SoftwareConfig.MySqlConfig.UserPwd, GlobalConst.SoftwareConfigAESKey, GlobalConst.SoftwareConfigAESVector);
            SoftwareConfig.MQTTConfig.UserPwd = Encrypt.AESEncrypt(SoftwareConfig.MQTTConfig.UserPwd, GlobalConst.SoftwareConfigAESKey, GlobalConst.SoftwareConfigAESVector);
            SoftwareConfig.UserConfig.UserPwd = Encrypt.AESEncrypt(SoftwareConfig.UserConfig.UserPwd, GlobalConst.SoftwareConfigAESKey, GlobalConst.SoftwareConfigAESVector);

            List<string> MySqlConfigsPwd = new List<string>();
            foreach (var item in SoftwareConfig.MySqlConfigs)
            {
                MySqlConfigsPwd.Add(item.UserPwd);
                item.UserPwd = Encrypt.AESEncrypt(item.UserPwd, GlobalConst.SoftwareConfigAESKey, GlobalConst.SoftwareConfigAESVector);
            }

            List<string> MQTTConfigsPwd = new List<string>();
            foreach (var item in SoftwareConfig.MQTTConfigs)
            {
                MQTTConfigsPwd.Add(item.UserPwd);
                item.UserPwd = Encrypt.AESEncrypt(item.UserPwd, GlobalConst.SoftwareConfigAESKey, GlobalConst.SoftwareConfigAESVector);
            }

            WriteConfig(GlobalConst.SoftwareConfigFileName, SoftwareConfig);
            SoftwareConfig.MySqlConfig.UserPwd = Temp0;
            SoftwareConfig.MQTTConfig.UserPwd = Temp1;
            SoftwareConfig.UserConfig.UserPwd = Temp2;

            for (int i = 0; i < MySqlConfigsPwd.Count; i++)
                SoftwareConfig.MySqlConfigs[i].UserPwd = MySqlConfigsPwd[i];

            for (int i = 0; i < MQTTConfigsPwd.Count; i++)
                SoftwareConfig.MQTTConfigs[i].UserPwd = MQTTConfigsPwd[i];
        }

        private static T? ReadConfig<T>(string fileName)
        {
            if (File.Exists(fileName))
                return JsonSerializer.Deserialize<T>(File.ReadAllText(fileName), new JsonSerializerOptions());
            else
            {
                T t = (T)Activator.CreateInstance(typeof(T));
                WriteConfig<T>(fileName,t);
                return t;
            }
        }

        private static void WriteConfig<T>(string fileName, T? t)
        {
            string DirectoryName = Path.GetDirectoryName(fileName);
            if (DirectoryName != null && !Directory.Exists(DirectoryName))
                Directory.CreateDirectory(DirectoryName);

            string jsonString = JsonSerializer.Serialize(t, new JsonSerializerOptions() {WriteIndented =true});
            File.WriteAllText(fileName, jsonString);
        }
    }
}
