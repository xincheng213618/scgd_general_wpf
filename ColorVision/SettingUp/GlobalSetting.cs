using ColorVision.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ColorVision.SettingUp
{
    public static class GlobalConst
    {
        public const string SoftwareConfigFileName = "Config\\SoftwareConfig.json";


    }


    /// <summary>
    /// 全局设置
    /// </summary>
    public class GlobalSetting
    {
        private static GlobalSetting _instance;
        private static readonly object _locker = new();
        public static GlobalSetting GetInstance() { lock (_locker) { return _instance ??= new GlobalSetting(); } }

        public GlobalSetting()
        {
            SoftwareConfigLazy = new Lazy<SoftwareConfig>(() =>
            {
                SoftwareConfig config = ReadConfig<SoftwareConfig>(GlobalConst.SoftwareConfigFileName);
                if (config != null)
                {
                    config.MySqlConfig.UserPwd = AESUtil.AESDecrypt(config.MySqlConfig.UserPwd, "ColorVision", "ColorVision");
                    config.MQTTConfig.UserPwd = AESUtil.AESDecrypt(config.MQTTConfig.UserPwd, "ColorVision", "ColorVision");

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
        }


        readonly Lazy<SoftwareConfig> SoftwareConfigLazy;

        public SoftwareConfig SoftwareConfig { get => SoftwareConfigLazy.Value; }


        public void SaveSoftwareConfig()
        {
            string Temp = SoftwareConfig.MySqlConfig.UserPwd;
            string Temp1 = SoftwareConfig.MQTTConfig.UserPwd;

            SoftwareConfig.MySqlConfig.UserPwd = AESUtil.AESEncrypt(SoftwareConfig.MySqlConfig.UserPwd, "ColorVision", "ColorVision");
            SoftwareConfig.MQTTConfig.UserPwd = AESUtil.AESEncrypt(SoftwareConfig.MQTTConfig.UserPwd, "ColorVision", "ColorVision");
            WriteConfig(GlobalConst.SoftwareConfigFileName, SoftwareConfig);
            SoftwareConfig.MySqlConfig.UserPwd = Temp;
            SoftwareConfig.MQTTConfig.UserPwd = Temp1;
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
