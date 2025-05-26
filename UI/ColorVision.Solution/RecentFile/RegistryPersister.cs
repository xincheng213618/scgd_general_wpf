using Microsoft.Win32;
using System.Windows.Forms;

namespace ColorVision.Solution.RecentFile
{
    public class RegistryPersister : IRecentFile
    {
        public static List<string> RegistryKeyList { get; set; } = new List<string>();

        /// <summary>
        /// 注册表信息
        /// </summary>
        public string RegistryKey { get => _RegistryKey; set { _RegistryKey = value;  if (!RegistryKeyList.Contains(value)) RegistryKeyList.Add(value);  } }
        private string _RegistryKey;

        public RegistryPersister()
        {
            RegistryKey ="Software\\" + Application.ProductName + "\\" + "RecentFileList";
        }  
        public RegistryPersister(string key)
        {
            RegistryKey = key;
        }

        static string Key(int i) { return i.ToString("00"); }

        public List<string> RecentFiles(int max)
        {
            RegistryKey k = Registry.CurrentUser.OpenSubKey(RegistryKey);
            if (k == null)
            {
                k = Registry.CurrentUser.CreateSubKey(RegistryKey);
            }

            List<string> list = new(max);

            for (int i = 0; i < max; i++)
            {
                string filename = (string)k.GetValue(Key(i));
                if (string.IsNullOrEmpty(filename))
                {
                    break;
                }

                list.Add(filename);
            }

            return list;
        }

        public void InsertFile(string filepath, int max)
        {
            if (string.IsNullOrEmpty(filepath))
                return;
            RegistryKey k = Registry.CurrentUser.OpenSubKey(RegistryKey);
            if (k == null)
                Registry.CurrentUser.CreateSubKey(RegistryKey);

            k = Registry.CurrentUser.OpenSubKey(RegistryKey, true);

            RemoveFile(filepath, max);

            for (int i = max - 2; i >= 0; i--)
            {
                string sThis = Key(i);
                string sNext = Key(i + 1);

                object oThis = k?.GetValue(sThis);
                if (oThis == null)
                {
                    continue;
                }

                k?.SetValue(sNext, oThis);
            }

            k?.SetValue(Key(0), filepath);
        }

        public void RemoveFile(string filepath, int max)
        {
            RegistryKey k = Registry.CurrentUser.OpenSubKey(RegistryKey);
            if (k == null)
            {
                return;
            }

            for (int i = 0; i < max; i++)
            {
                again:
                string s = (string)k.GetValue(Key(i));
                if (s != null && s.Equals(filepath, StringComparison.OrdinalIgnoreCase))
                {
                    RemoveFile(i, max);
                    goto again;
                }
            }
        }

        void RemoveFile(int index, int max)
        {
            RegistryKey k = Registry.CurrentUser.OpenSubKey(RegistryKey, true);
            if (k == null)
            {
                return;
            }

            k.DeleteValue(Key(index), false);

            for (int i = index; i < max - 1; i++)
            {
                string sThis = Key(i);
                string sNext = Key(i + 1);

                object oNext = k.GetValue(sNext);
                if (oNext == null)
                {
                    break;
                }

                k.SetValue(sThis, oNext);
                k.DeleteValue(sNext);
            }
        }
        public void Clear()
        {
            RegistryKey k = Registry.CurrentUser.OpenSubKey(RegistryKey);
            if (k == null)
            {
                return;
            }
            Registry.CurrentUser.DeleteSubKeyTree(RegistryKey);
        }

    }
}
