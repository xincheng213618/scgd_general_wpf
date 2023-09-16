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

        public static Dictionary<string, string> keyValuePairs { get; set; } = new Dictionary<string, string>() {
            { "zh-hans" ,"简体中文" },
            { "zh-hant" ,"繁体中文" },
            { "en","English" },
            { "ja","日语" },
            { "ko","韩语"},
        };

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
                if (MessageBox.Show(Util.Properties.Resource.LanguageResartSign, "ColorVision", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                {
                    Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo(lang);
                    Process.Start(Application.ResourceAssembly.Location.Replace(".dll",".exe"),"-r");
                    Application.Current.Shutdown();
                }
            }
        }


    }
}
