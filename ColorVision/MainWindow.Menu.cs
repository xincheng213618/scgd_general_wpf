using ColorVision.Common.Extension;
using ColorVision.Common.Utilities;
using ColorVision.HotKey;
using ColorVision.Language;
using ColorVision.MQTT;
using ColorVision.MySql;
using ColorVision.Services.Flow;
using ColorVision.Services.RC;
using ColorVision.Settings;
using ColorVision.Themes;
using ColorVision.UI;
using log4net;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision
{


    /// <summary>
    /// 这里写的是菜单栏的事件
    /// </summary>
    public partial class MainWindow
    {
        private void MenuItem9_Click(object sender, RoutedEventArgs e)
        {
            new WindowFlowEngine() { Owner = null, WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show();
        }


        private DateTime lastClickTime = DateTime.MinValue;


        private void Menu_Initialized(object sender, EventArgs e)
        {
            MenuManager.GetInstance().Menu = Menu1;
            MenuManager.GetInstance().LoadMenuItemFromAssembly<IMenuItem>(Assembly.GetExecutingAssembly());
            this.LoadHotKeyFromAssembly<IHotKey>(Assembly.GetExecutingAssembly());
            Application.Current.MainWindow = this;
        }

        private void TextBlock_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            new MQTTConnect() { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }

        private void TextBlock_MouseLeftButtonDown1(object sender, MouseButtonEventArgs e)
        {
            new MySqlConnect() { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        } 
        private void TextBlock_MouseLeftButtonDown_RC(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            new RCServiceConnect() { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }
        private void MenuItem10_Click(object sender, RoutedEventArgs e)
        {
        }
        private void LogF_Click(object sender, RoutedEventArgs e)
        {
            var fileAppender = (log4net.Appender.FileAppender)LogManager.GetRepository().GetAppenders().FirstOrDefault(a => a is log4net.Appender.FileAppender);
            if (fileAppender != null)
            {
                Process.Start("explorer.exe", $"{Path.GetDirectoryName(fileAppender.File)}");
            }
        }

        private void SettingF_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("explorer.exe", $"{Path.GetDirectoryName(ConfigHandler.GetInstance().SoftwareConfigFileName)}");
        }



        private void Setting_Click(object sender, RoutedEventArgs e)
        {
            string fileName = ConfigHandler.GetInstance().SoftwareConfigFileName;
            bool result = Tool.HasDefaultProgram(fileName);
            if (!result)
                Process.Start(result ? "explorer.exe" : "notepad.exe", fileName);
        }



        private void MenuLanguage_Initialized(object sender, EventArgs e)
        {
            foreach (var item in LanguageManager.Current.Languages)
            {
                MenuItem LanguageItem = new MenuItem();
                LanguageItem.Header = LanguageManager.keyValuePairs.TryGetValue(item, out string value) ? value : item;
                LanguageItem.Click += (s, e) =>
                {
                    string temp = Thread.CurrentThread.CurrentUICulture.Name;
                    ConfigHandler.GetInstance().SoftwareConfig.SoftwareSetting.UICulture = item;
                    ConfigHandler.GetInstance().SaveConfig();
                    bool sucess = LanguageManager.Current.LanguageChange(item);
                    if (!sucess)
                    {
                        ConfigHandler.GetInstance().SoftwareConfig.SoftwareSetting.UICulture = temp;
                        ConfigHandler.GetInstance().SaveConfig();
                    }
                };
                LanguageItem.Tag = item;
                LanguageItem.IsChecked = Thread.CurrentThread.CurrentUICulture.Name == item;
                MenuLanguage.Items.Add(LanguageItem);
            }
        }
        private void MenuLanguage_Loaded(object sender, RoutedEventArgs e)
        {
            foreach (var item in MenuTheme.Items)
            {
                if (item is MenuItem LanguageItem && LanguageItem.Tag is string Language)
                    LanguageItem.IsChecked = Thread.CurrentThread.CurrentUICulture.Name == Language;
            }
        }

        private void MenuTheme_Loaded(object sender, RoutedEventArgs e)
        {
            foreach (var item in MenuTheme.Items)
            {
                if (item is MenuItem ThemeItem && ThemeItem.Tag is Theme Theme)
                    ThemeItem.IsChecked = ThemeManager.Current.CurrentTheme == Theme;
            }

        }

        private void MenuTheme_Initialized(object sender, EventArgs e)
        {
            foreach (var item in Enum.GetValues(typeof(Theme)).Cast<Theme>())
            {
                MenuItem ThemeItem = new MenuItem();
                ThemeItem.Header = Properties.Resource.ResourceManager.GetString(item.ToDescription(), CultureInfo.CurrentUICulture) ?? "";
                ThemeItem.Click += (s, e) =>
                {
                    ConfigHandler.GetInstance().SoftwareConfig.SoftwareSetting.Theme = item;
                    ConfigHandler.GetInstance().SaveConfig();
                    Application.Current.ApplyTheme(item);
                };
                ThemeItem.Tag = item;
                ThemeItem.IsChecked = ThemeManager.Current.CurrentTheme == item;
                MenuTheme.Items.Add(ThemeItem);
            }
        }


    }
}
