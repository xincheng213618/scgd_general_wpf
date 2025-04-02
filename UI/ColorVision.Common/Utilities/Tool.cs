using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Net.NetworkInformation;
using System.Net;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Windows.Forms;
using System.Net.Sockets;

namespace ColorVision.Common.Utilities
{

    public static partial class Tool
    {
        public static readonly Version OSVersion = Environment.OSVersion.Version;
        public static readonly bool IsWin11 = OSVersion >= new Version(10, 0, 21996);
        public static readonly bool IsWin10 = OSVersion >= new Version(10, 0) && OSVersion < new Version(10, 0, 21996);
        public static readonly bool IsWin81 = OSVersion >= new Version(6, 3) && OSVersion < new Version(10, 0);
        public static readonly bool IsWin8 = OSVersion >= new Version(6, 2) && OSVersion < new Version(6, 3);
        public static readonly bool IsWin7 = OSVersion >= new Version(6, 1) && OSVersion < new Version(6, 2);
        public static readonly bool IsWinVista = OSVersion >= new Version(6, 0) && OSVersion < new Version(6, 1);
        public static readonly bool IsWinXP = OSVersion >= new Version(5, 1) && OSVersion < new Version(6, 0);
        public static readonly bool IsWinXP64 = OSVersion == new Version(5, 2); // Windows XP 64-bit Edition

        public static IntPtr GenerateRandomIntPtr()
        {
            // 使用随机数生成器
            Random random = new Random();

            // 生成一个随机的整数值
            int randomValue = random.Next();

            // 将随机整数转换为 IntPtr
            IntPtr randomIntPtr = new IntPtr(randomValue);

            return randomIntPtr;
        }

        /// <summary>
        /// 获取系统hosts
        /// </summary>
        /// <returns></returns>
        public static Dictionary<string, string> GetSystemHosts()
        {
            var systemHosts = new Dictionary<string, string>();
            var hostFilePath = @"C:\Windows\System32\drivers\etc\hosts";

            try
            {
                if (File.Exists(hostFilePath))
                {
                    using (var reader = new StreamReader(hostFilePath))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            line = line.Trim();
                            if (string.IsNullOrEmpty(line) || line.StartsWith("#",StringComparison.CurrentCulture))
                            {
                                continue;
                            }

#pragma warning disable CA1861 // 不要将常量数组作为参数
                            var hostParts = line.Split(new [] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
#pragma warning restore CA1861 // 不要将常量数组作为参数
                            if (hostParts.Length >= 2)
                            {
                                var ipAddress = hostParts[0];
                                var hostName = hostParts[1];

                                systemHosts.TryAdd(hostName, ipAddress);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log the exception or handle it as needed
                Console.WriteLine($"Error reading hosts file: {ex.Message}");
            }

            return systemHosts;
        }


        public static bool PortInUse(int port)
        {
            bool inUse = false;
            try
            {
                IPGlobalProperties ipProperties = IPGlobalProperties.GetIPGlobalProperties();
                IPEndPoint[] ipEndPoints = ipProperties.GetActiveTcpListeners();

                var lstIpEndPoints = new List<IPEndPoint>(IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners());

                foreach (IPEndPoint endPoint in ipEndPoints)
                {
                    if (endPoint.Port == port)
                    {
                        inUse = true;
                        break;
                    }
                }
            }
            catch 
            {
            }
            return inUse;
        }

        public static int GetFreePort(int defaultPort = 9090)
        {
            try
            {
                if (!PortInUse(defaultPort))
                {
                    return defaultPort;
                }
                TcpListener l = new(IPAddress.Loopback, 0);
                l.Start();
                int port = ((IPEndPoint)l.LocalEndpoint).Port;
                l.Stop();
                return port;
            }
            catch
            {
            }
            return 59090;
        }

        public static string SanitizeFileName(string fileName)
        {
            // 定义非法字符
            char[] invalidChars = Path.GetInvalidFileNameChars();
            foreach (char c in invalidChars)
            {
                fileName = fileName.Replace(c, '_'); // 将非法字符替换为下划线或其他合法字符
            }
            return fileName;
        }

        public static bool ValidateModbusCRC16(byte[] data)
        {
            ushort computedCrc = CalculateCRC16(data, data.Length - 2);
            ushort receivedCrc = BitConverter.ToUInt16(data, data.Length - 2);
            return computedCrc == receivedCrc;
        }

        private static ushort CalculateCRC16(byte[] data, int length)
        {
            ushort crc = 0xFFFF;

            for (int i = 0; i < length; i++)
            {
                crc ^= data[i];

                for (int j = 0; j < 8; j++)
                {
                    if ((crc & 0x0001) != 0)
                    {
                        crc >>= 1;
                        crc ^= 0xA001;
                    }
                    else
                    {
                        crc >>= 1;
                    }
                }
            }

            return crc;
        }
        public static bool IsImageFile(string filePath)
        {
            string[] imageExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff" };
            string fileExtension = Path.GetExtension(filePath).ToLower(System.Globalization.CultureInfo.CurrentCulture);
            return Array.Exists(imageExtensions, extension => extension == fileExtension);
        }

        public static string Base64Encode(string plainText)
        {
            var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
            return Convert.ToBase64String(plainTextBytes);
        }

        public static string Base64Decode(string base64EncodedData)
        {
            var base64EncodedBytes = Convert.FromBase64String(base64EncodedData);
            return Encoding.UTF8.GetString(base64EncodedBytes);
        }


        public static string CalculateMD5(string filename)
        {
            if (string.IsNullOrWhiteSpace(filename) || !File.Exists(filename)) return string.Empty;
            try
            {
#pragma warning disable CA5351
                using var md5 = MD5.Create();
#pragma warning restore CA5351
                using var stream = File.OpenRead(filename);
                var hash = md5.ComputeHash(stream);
                var sb = new StringBuilder();
                foreach (var b in hash)
                {
                    sb.Append(b.ToString("x2"));
                }
                return sb.ToString();
            }
            catch (Exception ex)
            {
                // Log the exception or handle it as needed
                Console.WriteLine($"An error occurred while calculating MD5: {ex.Message}");
                return string.Empty;
            }
        }

        public static void ExtractToDirectory(string zipPath, string extractPath)
        {
            ZipFile.ExtractToDirectory(zipPath, extractPath);
        }

        public static bool CreateDirectoryMax(string folderPath)
        {
            try
            {
                if (!Directory.Exists(folderPath))
                    Directory.CreateDirectory(folderPath);
                return true;
            }
            catch
            {
                return CreateDirectory(folderPath);
            }
        }

        public static bool ExecuteCommandAsAdmin(string command)
        {
            ProcessStartInfo startInfo = new();
            startInfo.UseShellExecute = true;
            startInfo.WorkingDirectory = @"C:\Windows\System32";
            startInfo.FileName = "cmd.exe";
            startInfo.Verb = "runas"; // 请求管理员权限
            startInfo.Arguments = "/c " + command; // 创建文件夹的命令
            startInfo.WindowStyle = ProcessWindowStyle.Hidden; // 隐藏命令行窗口


            try
            {
                Process process = Process.Start(startInfo);
                process?.WaitForExit();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return false;
            }
        }


        public static bool CreateDirectory(string folderPath)
        {
            ProcessStartInfo startInfo = new();
            startInfo.UseShellExecute = true;
            startInfo.WorkingDirectory = @"C:\Windows\System32";
            startInfo.FileName = "cmd.exe";
            startInfo.Verb = "runas"; // 请求管理员权限
            startInfo.Arguments = "/c mkdir " + folderPath; // 创建文件夹的命令
            startInfo.WindowStyle = ProcessWindowStyle.Hidden; // 隐藏命令行窗口

            try
            {
                Process process = Process.Start(startInfo);
                process?.WaitForExit(); // 等待命令完成
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return false;
            }
        }

        public static string? GetServicePath(string serviceName)
        {
            string registryPath = $@"SYSTEM\CurrentControlSet\Services\{serviceName}";
            string servicePath = string.Empty;

            using RegistryKey key = Registry.LocalMachine.OpenSubKey(registryPath);
            if (key != null)
            {
                object serviceImagePath = key.GetValue("ImagePath");
                servicePath = serviceImagePath?.ToString();

                // 如果路径包含引号，去掉它们
                if (!string.IsNullOrEmpty(servicePath) && servicePath.StartsWith("\"",StringComparison.CurrentCulture))
                {
                    servicePath = servicePath.Trim('"');
                }
                if (string.IsNullOrWhiteSpace(servicePath)) return servicePath;
                // 替换系统路径变量为实际路径
                servicePath = Environment.ExpandEnvironmentVariables(servicePath);
                return servicePath;
            }
            return null;
        }



        public static float GetScreenScalingFactor()
        {
            // 获取主屏幕
            Screen screen = Screen.PrimaryScreen;

            // 获取缩放比例
            float dpiX, dpiY;
            using (Graphics graphics = Graphics.FromHwnd(IntPtr.Zero))
            {
                dpiX = graphics.DpiX;
                dpiY = graphics.DpiY;
            }

            // 计算缩放比例
            float dpiScaleX = dpiX / 96f;
            float dpiScaleY = dpiY / 96f;

            // 返回较大的缩放比例
            return Math.Max(dpiScaleX, dpiScaleY);
        }

        public static bool HasDefaultProgram(string fileName)
        {
            bool hasDefaultProgram = false;
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(fileName);
                psi.UseShellExecute = true;
                Process.Start(psi);
                hasDefaultProgram = true;
            }
            catch (FileNotFoundException)
            {
                hasDefaultProgram = false;
            }
            catch
            {

            }
            return hasDefaultProgram;
        }


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
                RegUtils.WriteValue(AutoRunRegPath, autoRunName, "");
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
                        RegUtils.WriteValue(AutoRunRegPath, autoRunName, exePath);
                    }
                    else
                    {
                        RegUtils.WriteValue(AutoRunRegPath, autoRunName, exePath);
                    }
                }
            }
            catch (Exception ex)
            {
               Trace.TraceError(ex.Message);
            }
        }


        public static void RestartAsAdmin()
        {
            ProcessStartInfo proc = new ProcessStartInfo
            {
                UseShellExecute = true,
                WorkingDirectory = Environment.CurrentDirectory,
                FileName = System.Windows.Forms.Application.ExecutablePath,
                Verb = "runas" // 申请管理员权限
            };
            try
            {
                Process.Start(proc);
            }
            catch (Exception ex)
            {
                MessageBox.Show("无法以管理员权限重新启动程序：" + ex.Message);
            }
            Environment.Exit(0);
        }

        private static bool IsAdmin;
        private static bool IsInitAdmin;

        /// <summary>
        /// IsAdministrator
        /// </summary>
        /// <returns></returns>
        public static bool IsAdministrator()
        {
            if (IsInitAdmin) return IsAdmin;
            IsInitAdmin = true;
            try
            {
                WindowsIdentity current = WindowsIdentity.GetCurrent();
                WindowsPrincipal windowsPrincipal = new(current);
                //WindowsBuiltInRole可以枚举出很多权限，例如系统用户、User、Guest等等
                IsAdmin = windowsPrincipal.IsInRole(WindowsBuiltInRole.Administrator);
                return IsAdmin;
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.Message);
                IsInitAdmin = false;
                return false;
            }
        }


        public static bool IsAutoRun(string AutoRunName, string AutoRunRegPath)
        {
            try
            {
                if (string.IsNullOrEmpty(RegUtils.ReadValue(AutoRunRegPath, AutoRunName, "")))
                {
                    RegUtils.WriteValue(AutoRunRegPath, AutoRunName, "");
                }

                string value = RegUtils.ReadValue(AutoRunRegPath, AutoRunName, "");
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
            MemoryStream ms = new();
#pragma warning disable SYSLIB0011
            BinaryFormatter bf = new();
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
                ProcessStartInfo psi = new(filePath);
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
