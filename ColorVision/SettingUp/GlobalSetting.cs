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
            SoftwareConfigLazy = new Lazy<SoftwareConfig>(() => ReadConfig<SoftwareConfig>(GlobalConst.SoftwareConfigFileName) ?? new SoftwareConfig());

            AppDomain.CurrentDomain.ProcessExit += (s, e) =>
            {
                WriteConfig(GlobalConst.SoftwareConfigFileName, SoftwareConfig);
            };
        }
        readonly Lazy<SoftwareConfig> SoftwareConfigLazy;

        public SoftwareConfig SoftwareConfig { get => SoftwareConfigLazy.Value; }






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

            string jsonString = JsonSerializer.Serialize(t, new JsonSerializerOptions());
            File.WriteAllText(fileName, jsonString);
        }
    }
}
