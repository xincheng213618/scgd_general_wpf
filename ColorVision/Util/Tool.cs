using ColorVision.MQTT;
using ColorVision.SettingUp;
using ColorVision.Video;
using log4net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ColorVision.Util
{
    public static class Tool
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(Tool));


        /// <summary>
        /// 开机自动启动
        /// </summary>
        /// <param name="run"></param>
        /// <returns></returns>
        public static void SetAutoRun(bool run)
        {
            try
            {
                var autoRunName = $"{GlobalConst.AutoRunName}";


                //delete first
                RegUtil.WriteValue(GlobalConst.AutoRunRegPath, autoRunName, "");
                if (IsAdministrator())
                {
                    //AutoStart(autoRunName, "", "");
                }

                if (run)
                {
                    string exePath = $"\"{Environment.ProcessPath}\"";
                    if (IsAdministrator())
                    {
                        //AutoStart(autoRunName, exePath, "");
                        RegUtil.WriteValue(GlobalConst.AutoRunRegPath, autoRunName, exePath);
                    }
                    else
                    {
                        RegUtil.WriteValue(GlobalConst.AutoRunRegPath, autoRunName, exePath);
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
        }




        /// <summary>
        /// IsAdministrator
        /// </summary>
        /// <returns></returns>
        public static bool IsAdministrator()
        {
            try
            {
                WindowsIdentity current = WindowsIdentity.GetCurrent();
                WindowsPrincipal windowsPrincipal = new WindowsPrincipal(current);
                //WindowsBuiltInRole可以枚举出很多权限，例如系统用户、User、Guest等等
                return windowsPrincipal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch (Exception ex)
            {
                log.Error(ex);
                return false;
            }
        }


        public static bool IsAutoRun()
        {
            try
            {
                if (string.IsNullOrEmpty(RegUtil.ReadValue(GlobalConst.AutoRunRegPath, GlobalConst.AutoRunName, "")))
                {
                    RegUtil.WriteValue(GlobalConst.AutoRunRegPath, GlobalConst.AutoRunName, "");
                }

                string value = RegUtil.ReadValue(GlobalConst.AutoRunRegPath, GlobalConst.AutoRunName, "");
                string exePath = Environment.ProcessPath;
                if (value == exePath || value == $"\"{exePath}\"")
                {
                    return true;
                }
                return false;

            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
            return false;
        }

        public static string GetMD5(string str)
        {
            byte[] byteOld = Encoding.UTF8.GetBytes(str);
            #pragma warning disable CA5351
            byte[] byteNew = MD5.HashData(byteOld);
            #pragma warning restore CA5351
            StringBuilder sb = new(32);
            foreach (byte b in byteNew)
            {
                sb.Append(b.ToString("x2"));
            }
            return sb.ToString();
        }

        public static T DeepCopy<T>(T obj)
        {
            if (obj is null)
                return (T)new object();
            object retval;
            MemoryStream ms = new MemoryStream();
            BinaryFormatter bf = new BinaryFormatter();
            //序列化成流
            #pragma warning disable SYSLIB0011
            bf.Serialize(ms, obj);
            ms.Seek(0, SeekOrigin.Begin);
            //反序列化成对象
            retval = bf.Deserialize(ms);
            #pragma warning restore SYSLIB0011
            return (T)retval;
        }
    }
}
