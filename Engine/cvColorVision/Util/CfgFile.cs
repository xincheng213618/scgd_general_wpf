using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;

namespace cvColorVision.Util
{
    public static class CfgFile
    {
        public static bool Save<T>(string cfgFile, T cfg)
        {
            try
            {
                string json = JsonConvert.SerializeObject(cfg, Formatting.Indented);
                File.WriteAllText(cfgFile, json);
                return true;
            }
            catch (Exception ex)
            {
                Trace.WriteLine("### [" + ex.Source + "] Exception: " + ex.Message);
                Trace.WriteLine("### " + ex.StackTrace);
                return false;
            }
        }
        /// <summary>
        /// 读取配置文件，
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="cfgFile"></param>
        /// <returns></returns>
        public static T Load<T>(string cfgFile)
        {
            if (File.Exists(cfgFile))
            {
                try
                {
                    return JsonConvert.DeserializeObject<T>(File.ReadAllText(cfgFile));
                }
                catch (Exception ex)
                {
                    Trace.WriteLine("### [" + ex.Source + "] Exception: " + ex.Message);
                    Trace.WriteLine("### " + ex.StackTrace);
                }
            }
            return default;
        }

    }
}
