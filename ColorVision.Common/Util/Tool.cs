using Microsoft.VisualBasic.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace ColorVision.Util
{
    public static partial class Tool
    {
        public static string GetNoRepeatFileName(string DirectoryPath, string FileName,string Ex)
        {
            if (!File.Exists($"{DirectoryPath}\\{FileName}.{Ex}"))
                return FileName;
            for (int i = 1; i < 999; i++)
            {
                if (!File.Exists($"{DirectoryPath}\\{FileName}{i}.{Ex}"))
                    return $"{FileName}{i}";
            }
            return FileName;
        }


        public static string GetNoRepeatFilePath(string DirectoryPath, string FileName)
        {
            if (!Directory.Exists($"{DirectoryPath}\\{FileName}"))
                return FileName;
            for (int i = 1; i < 999; i++)
            {
                if (!Directory.Exists($"{DirectoryPath}\\{FileName}{i}"))
                    return $"{FileName}{i}";
            }
            return FileName;
        }


        public static string FileToBase64(string fileName)
        {
            if (File.Exists(fileName))
            {
                byte[] fileBytes = File.ReadAllBytes(fileName);
                string base64String = Convert.ToBase64String(fileBytes);
                return base64String;
            }
            else
            {
                return string.Empty;
            }
        }

        public static bool Base64ToFile(string base64String, string fileFullPath, string fileName)
        {
            try
            {
                byte[] fileBytes = Convert.FromBase64String(base64String);
                File.WriteAllBytes($"{fileFullPath}\\{fileName}", fileBytes);
                return true;
            }
            catch { return false; }
        }

        public static bool Base64ToFile(string base64String, string fileName)
        {
            try
            {
                byte[] fileBytes = Convert.FromBase64String(base64String);
                File.WriteAllBytes(fileName, fileBytes);
                return true;
            }
            catch { return false; }
        }

        /// <summary>
        /// 开机自动启动
        /// </summary>
        /// <param name="run"></param>
        /// <returns></returns>
        public static void SetAutoRun(bool run, string AutoRunName, string AutoRunRegPath)
        {
            try
            {
                var autoRunName = $"{AutoRunName}";


                //delete first
                RegUtil.WriteValue(AutoRunRegPath, autoRunName, "");
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
                        RegUtil.WriteValue(AutoRunRegPath, autoRunName, exePath);
                    }
                    else
                    {
                        RegUtil.WriteValue(AutoRunRegPath, autoRunName, exePath);
                    }
                }
            }
            catch (Exception ex)
            {
               Trace.TraceError(ex.Message);
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
                Trace.TraceError(ex.Message);
                return false;
            }
        }


        public static bool IsAutoRun(string AutoRunName, string AutoRunRegPath)
        {
            try
            {
                if (string.IsNullOrEmpty(RegUtil.ReadValue(AutoRunRegPath, AutoRunName, "")))
                {
                    RegUtil.WriteValue(AutoRunRegPath, AutoRunName, "");
                }

                string value = RegUtil.ReadValue(AutoRunRegPath, AutoRunName, "");
                string exePath = Environment.ProcessPath;
                if (value == exePath || value == $"\"{exePath}\"")
                {
                    return true;
                }
                return false;

            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
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
#pragma warning disable SYSLIB0011
            BinaryFormatter bf = new BinaryFormatter();
            //序列化成流
            bf.Serialize(ms, obj);
            ms.Seek(0, SeekOrigin.Begin);
            //反序列化成对象
            retval = bf.Deserialize(ms);
#pragma warning restore SYSLIB0011
            return (T)retval;
        }

        public static bool IsHasDefaultOpenWay(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return false;
            if (!File.Exists(filePath))
                return false;

            bool hasDefaultProgram = false;
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(filePath);
                psi.UseShellExecute = true;
                Process.Start(psi);
                hasDefaultProgram = true;
            }
            catch (FileNotFoundException)
            {
                hasDefaultProgram = false;
            }
            return hasDefaultProgram;
        }
    }
}
