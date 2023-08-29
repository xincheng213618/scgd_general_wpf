using ColorVision.Theme;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Language
{
    public class LanguageManager
    {
        public static LanguageManager Current { get; set; } = new LanguageManager();

        public LanguageManager()
        {

        }


        public static void LanguageChange(string lang)
        {
            if (Thread.CurrentThread.CurrentUICulture.Name != lang)
            {
                if (MessageBox.Show("您即将切换语言并重启窗口!", "ColorVision", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                {
                    Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo(lang);
                    Process.Start(Application.ResourceAssembly.Location);
                    Application.Current.Shutdown();
                }
            }
        }


    }
}
