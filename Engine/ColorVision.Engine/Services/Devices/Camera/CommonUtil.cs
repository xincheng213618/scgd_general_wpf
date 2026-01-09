using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace WindowsFormsTest
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct IPOINT
    {
        [MarshalAs(UnmanagedType.R8, SizeConst = 8)]
        public double X;
        [MarshalAs(UnmanagedType.R8, SizeConst = 8)]
        public double Y;
    };

    class CommonUtil
    {
        public static bool SaveCfgFile<T>(string cfgFile, T cfg)
        {
            if (cfgFile != null && cfgFile.Length > 0)
            {
                string jsonCfg = JsonConvert.SerializeObject(cfg, Formatting.Indented);

                using (StreamWriter sw = new StreamWriter(cfgFile))
                {
                    sw.Write(jsonCfg);
                    sw.Flush();
                    sw.Close();
                    sw.Dispose();
                }
                return true;
            }

            return false;
        }

        // null 未读取到
        public static T LoadCfgFile<T>(string cfgFile)
        {
            if (cfgFile != null && cfgFile.Length > 0 && File.Exists(cfgFile))
            {
                string json = System.IO.File.ReadAllText(cfgFile);

                if (json != null && json.Length > 0)
                {
                    return JsonConvert.DeserializeObject<T>(json);
                }
            }

            return default(T);
        }

        public static T LoadCfgText<T>(string cfgText)
        {
            if (cfgText != null && cfgText.Length > 0)
            {
                return JsonConvert.DeserializeObject<T>(cfgText);
            }

            return default(T);
        }

        [DllImport("kernel32.dll")]
        private static extern int GetPrivateProfileString(
           string section,
           string key,
           string defval,
           StringBuilder retval,
           int size,
           string filepath);

        [DllImport("kernel32.dll")]
        private static extern int WritePrivateProfileString(
            string section,
            string key,
            string val,
            string filepath);

        public static T ReadKeyIni<T>(string section, string key, T defval, string filename)
        {
            StringBuilder val = new StringBuilder(1024);
            GetPrivateProfileString(section, key, defval.ToString(), val, 1024, filename);

            if(default(T) == null)
            {
                return (T)Convert.ChangeType(val.ToString(), key.GetType());
            }
            else
            {
                return (T)Convert.ChangeType(val.ToString(), default(T).GetType());
            }
        }

        public static void WriteKeyIni<T>(string section, string key, T val, string filename)
        {
            WritePrivateProfileString(section, key, val.ToString(), filename);
        }
    }
}
