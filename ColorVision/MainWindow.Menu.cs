using ColorVision.HotKey;
using ColorVision.Solution;
using ColorVision.SettingUp;
using ColorVision.Templates;
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ColorVision.MQTT;
using ColorVision.MySql;
using log4net;
using System.Diagnostics;
using ColorVision.Util;
using ColorVision.Services;
using ColorVision.RC;
using ColorVision.OnlineLicensing;
using ColorVision.Services.Msg;
using ColorVision.Language;
using ColorVision.Themes;
using System.Globalization;
using System.Threading;
using ColorVision.Extension;
using ColorVision.Update;
using ColorVision.Services.Device.Camera.Calibrations;

namespace ColorVision
{
    /// <summary>
    /// 这里写的是菜单栏的事件
    /// </summary>
    public partial class MainWindow
    {
        TemplateControl TemplateControl { get; set; }

        private void MenuItem_Template(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem)
            {
                SoftwareConfig SoftwareConfig = GlobalSetting.GetInstance().SoftwareConfig;
                WindowTemplate windowTemplate;
                if (SoftwareConfig.IsUseMySql && !SoftwareConfig.MySqlControl.IsConnect)
                {
                    MessageBox.Show("数据库连接失败，请先连接数据库在操作", "ColorVision");
                    return;
                }
                switch (menuItem.Tag?.ToString()??string.Empty)
                {
                    case "AoiParam":
                        windowTemplate = new WindowTemplate(TemplateType.AoiParam);
                        windowTemplate.Owner = GetWindow(this);
                        windowTemplate.ShowDialog();
                        break;
                    case "PGParam":
                        windowTemplate = new WindowTemplate(TemplateType.PGParam);
                        windowTemplate.Owner = GetWindow(this);
                        windowTemplate.ShowDialog();
                        break;
                    case "LedReusltParams":
                        windowTemplate = new WindowTemplate(TemplateType.LedResult);
                        windowTemplate.Owner = GetWindow(this);
                        windowTemplate.ShowDialog();
                        break;
                    case "SMUParam":
                        windowTemplate = new WindowTemplate(TemplateType.SMUParam);
                        windowTemplate.Owner = GetWindow(this);
                        windowTemplate.ShowDialog();
                        break;
                    case "FocusParm":
                        windowTemplate = new WindowTemplate(TemplateType.PoiParam);
                        windowTemplate.Owner = GetWindow(this);
                        windowTemplate.ShowDialog();
                        break;
                    case "FlowParam":
                        windowTemplate = new WindowTemplate(TemplateType.FlowParam);
                        windowTemplate.Owner = GetWindow(this);
                        windowTemplate.ShowDialog();
                        break;
                    case "ServiceParam":
                        new WindowService() { Owner =this,WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
                        break;
                    case "DeviceParam":
                        new WindowDevices() { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
                        break;
                    case "MeasureParm":
                        MeasureParamControl measure = new MeasureParamControl();
                        windowTemplate = new WindowTemplate(TemplateType.MeasureParam, measure);
                        windowTemplate.Owner = GetWindow(this);
                        windowTemplate.ShowDialog();
                        break;
                    case "MTFParam":
                        windowTemplate = new WindowTemplate(TemplateType.MTFParam);
                        windowTemplate.Owner = GetWindow(this);
                        windowTemplate.ShowDialog();
                        break;
                    case "SFRParam":
                        windowTemplate = new WindowTemplate(TemplateType.SFRParam);
                        windowTemplate.Owner = GetWindow(this);
                        windowTemplate.ShowDialog();
                        break;
                    case "FOVParam":
                        windowTemplate = new WindowTemplate(TemplateType.FOVParam);
                        windowTemplate.Owner = GetWindow(this);
                        windowTemplate.ShowDialog();
                        break;
                    case "GhostParam":
                        windowTemplate = new WindowTemplate(TemplateType.GhostParam);
                        windowTemplate.Owner = GetWindow(this);
                        windowTemplate.ShowDialog();
                        break;
                    case "DistortionParam":
                        windowTemplate = new WindowTemplate(TemplateType.DistortionParam);
                        windowTemplate.Owner = GetWindow(this);
                        windowTemplate.ShowDialog();
                        break;
                    case "LedCheckParam":
                        windowTemplate = new WindowTemplate(TemplateType.LedCheckParam);
                        windowTemplate.Owner = GetWindow(this);
                        windowTemplate.ShowDialog();
                        break;
                    case "FocusPointsParam":
                        windowTemplate = new WindowTemplate(TemplateType.FocusPointsParam);
                        windowTemplate.Owner = GetWindow(this);
                        windowTemplate.ShowDialog();
                        break;
                    case "CalibrationUpload":
                        UploadWindow calibrationUpload = new UploadWindow();
                        calibrationUpload.ShowDialog();
                        break;
                    default:
                        HandyControl.Controls.Growl.Info("开发中");
                        break;
                }
            }
        }


        private void MenuItem_Click8(object sender, RoutedEventArgs e)
        {
            new WindowFourColorCalibration() {Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show();
        }

        private void MenuItem9_Click(object sender, RoutedEventArgs e)
        {
            new WindowFlowEngine() { Owner = null, WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show();
        }


        private void MenuItem_ProjectNew_Click(object sender, RoutedEventArgs e)
        {
            SolutionManager.GetInstance().NewCreateWindow();
        }

        private void MenuItem_ProjectOpen_Click(object sender, RoutedEventArgs e)
        {
            SolutionManager.GetInstance().OpenSolutionWindow();
        }
        private DateTime lastClickTime = DateTime.MinValue;


        private void MenuItem_Click_2(object sender, RoutedEventArgs e)
        {
            OpenSetting();
        }

        private void OpenSetting()
        {
            new SettingWindow() { Owner =this, WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }


        private void MenuItem12_Click(object sender, RoutedEventArgs e)
        {
            MsgList();
        }

        private void MsgList()
        {
            new MsgList() { Owner = this }.Show();
        }

        private void Menu_Initialized(object sender, EventArgs e)
        {
            Application.Current.MainWindow = this;
            Application.Current.MainWindow.AddHotKeys(new HotKeys(Properties.Resource.Settings, new Hotkey(Key.I, ModifierKeys.Control), OpenSetting));
            Application.Current.MainWindow.AddHotKeys(new HotKeys(Properties.Resource.About, new Hotkey(Key.F1, ModifierKeys.Control), AboutMsg));
            Application.Current.MainWindow.AddHotKeys(new HotKeys("MsgList", new Hotkey(Key.M, ModifierKeys.Control), MsgList));

            MenuItem RecentListMenuItem = null;

            RecentListMenuItem ??= new MenuItem();
            RecentListMenuItem.Header = Properties.Resource.RecentFiles;
            RecentListMenuItem.SubmenuOpened += (s, e) =>
            {
                var firstMenuItem = RecentListMenuItem.Items[0];
                foreach (var item in  SolutionManager.GetInstance().SolutionHistory.RecentFiles)
                {
                    if (Directory.Exists(item))
                    {
                        MenuItem menuItem = new MenuItem();
                        menuItem.Header = item;
                        menuItem.Click += (sender, e) =>
                        {
                            SolutionManager.GetInstance().OpenSolutionDirectory(item);
                        };
                        RecentListMenuItem.Items.Add(menuItem);
                    }
                    else
                    {
                        SolutionManager.GetInstance().SolutionHistory.RecentFiles.Remove(item);
                    }



                };
                RecentListMenuItem.Items.Remove(firstMenuItem);

            };
            RecentListMenuItem.SubmenuClosed += (s, e) => {
                RecentListMenuItem.Items.Clear();
                RecentListMenuItem.Items.Add(new MenuItem());
            };
            RecentListMenuItem.Items.Add(new MenuItem());

            FileMenuItem.Items.Insert(FileMenuItem.Items.Count-2, RecentListMenuItem);

        }

        private void Update_Click(object sender, RoutedEventArgs e)
        {
            AutoUpdater AutoUpdater = new AutoUpdater();
            AutoUpdater.CheckAndUpdate();
        }
        private void License_Click(object sender, RoutedEventArgs e)
        {
            new LicenseMangerWindow() { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }

        private void MenuItem_Exit(object sender, RoutedEventArgs e)
        {
            Environment.Exit(0);
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
            Process.Start("explorer.exe", $"{Path.GetDirectoryName(GlobalSetting.GetInstance().SoftwareConfigFileName)}");
        }
        private void Log_Click(object sender, RoutedEventArgs e)
        {
            new WindowLog() { Owner = this }.Show();
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            new MQTTLog() { Owner = this }.Show();
        }
        private void About_Click(object sender, RoutedEventArgs e)
        {
            AboutMsg();
        }

        private void AboutMsg()
        {
            new AboutMsgWindow() { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }


        private void Setting_Click(object sender, RoutedEventArgs e)
        {
            string fileName = GlobalSetting.GetInstance().SoftwareConfigFileName;
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
                    GlobalSetting.GetInstance().SoftwareConfig.SoftwareSetting.UICulture = item;
                    GlobalSetting.GetInstance().SaveSoftwareConfig();
                    bool sucess = LanguageManager.Current.LanguageChange(item);
                    if (!sucess)
                    {
                        GlobalSetting.GetInstance().SoftwareConfig.SoftwareSetting.UICulture = temp;
                        GlobalSetting.GetInstance().SaveSoftwareConfig();
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
                    GlobalSetting.GetInstance().SoftwareConfig.SoftwareSetting.Theme = item;
                    GlobalSetting.GetInstance().SaveSoftwareConfig();
                    Application.Current.ApplyTheme(item);
                };
                ThemeItem.Tag = item;
                ThemeItem.IsChecked = ThemeManager.Current.CurrentTheme == item;
                MenuTheme.Items.Add(ThemeItem);
            }
        }


    }
}
