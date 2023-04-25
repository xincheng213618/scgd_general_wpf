using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ColorVision.NativeMethods
{
    public static class IniFile
    {
        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        private static extern long WritePrivateProfileString(string section, string key, string val, string lpFileName);

        //[DllImport("kernel32", CharSet = CharSet.Unicode)]
        //private static extern int GetPrivateProfileString(string section, string key, string defaultValue, StringBuilder retVal, int size, string filePath);

        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        private static extern int GetPrivateProfileString(string section, string key, string defaultValue, char[] retVal, int size, string lpFileName);

        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        private static extern int GetPrivateProfileInt(string lpAppName, string lpKeyName, int nDefault, string lpFileName);


        public static string ReadStringFromIniFile(string filePath, string sectionName, string keyName, string defaultValue)
        {
            //StringBuilder sb = new StringBuilder(1024);
            //int size = GetPrivateProfileString(sectionName, keyName, defaultValue, sb, sb.Capacity, filePath);
            //return sb.ToString();

            char[] chars = new char[1024];
            int size = GetPrivateProfileString(sectionName, keyName, defaultValue, chars, chars.Length, filePath);
            return new string(chars, 0, size);
        }


        public static int ReadIntFromIniFile(string filePath, string sectionName, string keyName, int defaultValue)
        {
            return GetPrivateProfileInt(sectionName, keyName, defaultValue, filePath);
        }

        public static bool WriteStringToIniFile(string filePath, string sectionName, string keyName, string value)
        {
            return WritePrivateProfileString(sectionName, keyName, value, filePath) != 0;
        }
    }
}
