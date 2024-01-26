using Microsoft.Win32;
using System;

namespace ColorVision.Common.Utilities
{
    public static class RegUtil
    {
        public static string ReadValue(string path, string name, string def)
        {
            RegistryKey regKey = null;
            try
            {
                regKey = Registry.CurrentUser.OpenSubKey(path, false);
                string value = regKey?.GetValue(name) as string;
                if (string.IsNullOrEmpty(value))
                {
                    WriteValue(path, name, def);
                    return def;
                }
                else
                {
                    return value;
                }
            }
            catch
            {
            }
            finally
            {
                regKey?.Close();
            }
            return def;
        }

        public static void WriteValue(string path, string name, object value)
        {
            RegistryKey regKey = null;
            try
            {
                regKey = Registry.CurrentUser.CreateSubKey(path);
                if (string.IsNullOrEmpty(value.ToString()))
                {
                    regKey?.DeleteValue(name, false);
                }
                else
                {
                    regKey?.SetValue(name, value);
                }
            }
            catch
            {
            }
            finally
            {
                regKey?.Close();
            }
        }


        public static int ReadValue(string path, string name, int def = 0)
        {
            RegistryKey regKey = null;
            try
            {
                regKey = Registry.CurrentUser.OpenSubKey(path, false);
                if (regKey?.GetValue(name) is int value)
                {
                    return value;
                }
                WriteValue(path, name, def);
                return def;
            }
            catch
            {
            }
            finally
            {
                regKey?.Close();
            }
            return def;
        }


        public static bool ReadValue(string path, string name, bool def)
        {
            RegistryKey regKey = null;
            try
            {
                regKey = Registry.CurrentUser.OpenSubKey(path, false);
                string value = regKey?.GetValue(name) as string;
                if (string.IsNullOrEmpty(value))
                {
                    WriteValue(path, name, def);
                    return false;
                }
                else
                {
                    return Convert.ToBoolean(regKey?.GetValue(name));
                }

            }
            catch
            {
            }
            finally
            {
                regKey?.Close();
            }
            return false;
        }


        public static bool ReadValue(string path, string name)
        {
            RegistryKey regKey = null;
            try
            {
                regKey = Registry.CurrentUser.OpenSubKey(path, false);
                string value = regKey?.GetValue(name) as string;
                if (string.IsNullOrEmpty(value))
                {
                    WriteValue(path, name, false);
                    return false;
                }
                else
                {
                    return Convert.ToBoolean(regKey?.GetValue(name));
                }

            }
            catch
            {
            }
            finally
            {
                regKey?.Close();
            }
            return false;
        }
    }
}
