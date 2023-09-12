using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;

namespace ColorVision.Language
{
    public class LanguageManager
    {
        public static LanguageManager Current { get; set; } = new LanguageManager();
        public List<string> Languages { get; set; } = GetLanguages();
        public LanguageManager()
        {

        }

        public static List<string> GetLanguages()
        {
            List<string>  list =  new List<string>() { };
            string exeFolderPath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

            foreach (var subDirectory in Directory.GetDirectories(exeFolderPath??string.Empty))
            {
                string[] files = Directory.GetFiles(subDirectory, "ColorVision.resources.dll", System.IO.SearchOption.AllDirectories);

                if (files.Length > 0)
                {
                    list.Add(new DirectoryInfo(subDirectory).Name);
                }
            }
            list.Add("zh-Hans");
            return list;
        }

        public void LanguageChange(string lang)
        {
            if (Thread.CurrentThread.CurrentUICulture.Name != lang)
            {
                if (MessageBox.Show("您即将切换语言并重启窗口!", "ColorVision", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                {
                    Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo(lang);
                    Process.Start(Application.ResourceAssembly.Location.Replace(".dll",".exe"));
                    Application.Current.Shutdown();
                }
            }
        }


    }
}
