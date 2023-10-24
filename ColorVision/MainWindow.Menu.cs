using ColorVision.HotKey;
using ColorVision.Solution;
using ColorVision.SettingUp;
using ColorVision.Template;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using ColorVision.RecentFile;
using ColorVision.Lincense;
using ColorVision.Services.Msg;

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
                    MessageBox.Show("数据库连接失败，请先连接数据库在操作");
                    return;
                }
                switch (menuItem.Tag?.ToString()??string.Empty)
                {
                    case "AoiParam":
                        windowTemplate = new WindowTemplate(TemplateType.AoiParam) { Title = "AOI参数设置" };
                        TemplateAbb(windowTemplate, TemplateControl.AoiParams);
                        break;
                    case "Calibration":
                        Calibration calibration = new Calibration(TemplateControl.CalibrationParams[0].Value);
                        windowTemplate = new WindowTemplate(TemplateType.Calibration, calibration) { Title = "校正参数设置" };
                        TemplateAbb(windowTemplate, TemplateControl.CalibrationParams);
                        break;
                    case "PGParam":
                        //PG pg = new PG(TemplateControl.PGParams[0].Value);
                        windowTemplate = new WindowTemplate(TemplateType.PGParam) { Title = "PG设置" };
                        TemplateAbb(windowTemplate, TemplateControl.PGParams);
                        break;
                    case "LedReusltParams":
                        windowTemplate = new WindowTemplate(TemplateType.LedReuslt) { Title = "数据判断模板设置" };
                        TemplateAbb(windowTemplate, TemplateControl.LedReusltParams);
                        break;
                    case "SMUParam":
                        windowTemplate = new WindowTemplate(TemplateType.SMUParam) { Title = "源表模板设置" };
                        TemplateAbb(windowTemplate, TemplateControl.SMUParams);
                        break;
                    case "FocusParm":
                        windowTemplate = new WindowTemplate(TemplateType.PoiParam) { Title = "关注点设置" };
                        TemplateAbb(windowTemplate, TemplateControl.PoiParams);
                        break;
                    case "FlowParam":
                        windowTemplate = new WindowTemplate(TemplateType.FlowParam) { Title = "流程引擎" };
                        TemplateAbb(windowTemplate, TemplateControl.FlowParams);
                        break;
                    case "ServiceParam":
                        new WindowService() { Owner =this,WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
                        break;
                    case "DeviceParam":
                        new WindowDevices() { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
                        break;
                    case "MeasureParm":
                        MeasureParamControl measure = new MeasureParamControl();
                        windowTemplate = new WindowTemplate(TemplateType.MeasureParm, measure) { Title = "测量设置" };
                        TemplateControl.LoadMeasureParams();
                        TemplateAbb(windowTemplate, TemplateControl.MeasureParams);
                        break;
                    case "MTFParam":
                        windowTemplate = new WindowTemplate(TemplateType.MTFParam) { Title = "MTF算法设置" };
                        TemplateAbb(windowTemplate, TemplateControl.MTFParams);
                        break;
                    case "SFRParam":
                        windowTemplate = new WindowTemplate(TemplateType.SFRParam) { Title = "SFR算法设置" };
                        TemplateAbb(windowTemplate, TemplateControl.SFRParams);
                        break;
                    case "FOVParam":
                        windowTemplate = new WindowTemplate(TemplateType.FOVParam) { Title = "FOV算法设置" };
                        TemplateAbb(windowTemplate, TemplateControl.FOVParams);
                        break;
                    case "GhostParam":
                        windowTemplate = new WindowTemplate(TemplateType.GhostParam) { Title = "Ghost算法设置" };
                        TemplateAbb(windowTemplate, TemplateControl.GhostParams);
                        break;
                    case "DistortionParam":
                        windowTemplate = new WindowTemplate(TemplateType.DistortionParam) { Title = "Distortion算法设置" };
                        TemplateAbb(windowTemplate, TemplateControl.DistortionParams);
                        break;
                    default:
                        HandyControl.Controls.Growl.Info("开发中");
                        break;
                }
            }
        }
        private void TemplateAbb<T>(WindowTemplate windowTemplate, ObservableCollection<Template<T>> keyValuePairs) where T: ParamBase
        {
            windowTemplate.Owner = this;
            int id = 1;
            windowTemplate.ListConfigs.Clear();
            foreach (var item in keyValuePairs)
            {
                if (item.Value is PoiParam poiParam)
                {
                    item.Tag = $"{poiParam.Width}*{poiParam.Height}{(GlobalSetting.GetInstance().SoftwareConfig.IsUseMySql?"": $"_{poiParam.PoiPoints.Count}")}";
                }

                windowTemplate.ListConfigs.Add(item);
            }
            windowTemplate.ShowDialog();
        }

        private void MenuItem_Click8(object sender, RoutedEventArgs e)
        {
            new WindowFourColorCalibration() {Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show();
        }

        private void MenuItem9_Click(object sender, RoutedEventArgs e)
        {
            new WindowFlowEngine() { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show();
        }


        private void MenuItem_ProjectNew_Click(object sender, RoutedEventArgs e)
        {
            SolutionNewCreat();
        }

        private void MenuItem_ProjectOpen_Click(object sender, RoutedEventArgs e)
        {
            SolutionOpen();
        }
        private DateTime lastClickTime = DateTime.MinValue;

        private void TextBlock_MouseLeftButtonDown2(object sender, MouseButtonEventArgs e)
        {
            if (SolutionControl.GetInstance().SolutionConfig.SolutionFullName != null)
            {
                TimeSpan elapsedTime = DateTime.Now - lastClickTime;
                if (elapsedTime.TotalMilliseconds <= 300)
                {
                    System.Diagnostics.Process.Start("explorer.exe", $"{SolutionControl.GetInstance().SolutionConfig.SolutionFullName}");
                }
                lastClickTime = DateTime.Now;
            }
            else
            {
                SolutionNewCreat();
            }


        }

        private void MenuItem_Click_2(object sender, RoutedEventArgs e)
        {
            OpenSetting();
        }

        private void SolutionOpen()
        {
            OpenSolutionWindow openSolutionWindow = new OpenSolutionWindow() { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            openSolutionWindow.Closed += delegate
            {
                if (Directory.Exists(openSolutionWindow.FullName))
                    SolutionControl.GetInstance().CreateSolution(new DirectoryInfo(openSolutionWindow.FullName));
            };
            openSolutionWindow.Show();
        }

        private void SolutionNewCreat()
        {
            NewCreateWindow newCreatWindow = new NewCreateWindow() { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            newCreatWindow.Closed += delegate
            {
                if (newCreatWindow.IsCreate)
                {
                    string SolutionDirectoryPath = newCreatWindow.NewCreateViewMode.DirectoryPath + "\\" + newCreatWindow.NewCreateViewMode.Name;
                    SolutionControl.GetInstance().OpenSolution(SolutionDirectoryPath);
                }
            };
            newCreatWindow.ShowDialog();
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
            Application.Current.MainWindow.AddHotKeys(new HotKeys("打开工程", new Hotkey(Key.O, ModifierKeys.Control), SolutionOpen));
            Application.Current.MainWindow.AddHotKeys(new HotKeys("新建工程", new Hotkey(Key.N, ModifierKeys.Control), SolutionNewCreat));
            Application.Current.MainWindow.AddHotKeys(new HotKeys("设置", new Hotkey(Key.I, ModifierKeys.Control), OpenSetting));
            Application.Current.MainWindow.AddHotKeys(new HotKeys(Properties.Resource.About, new Hotkey(Key.F1, ModifierKeys.Control), AboutMsg));
            Application.Current.MainWindow.AddHotKeys(new HotKeys("MsgList", new Hotkey(Key.M, ModifierKeys.Control), MsgList));

            MenuItem RecentListMenuItem = null;

            RecentListMenuItem ??= new MenuItem();
            RecentListMenuItem.Header = Properties.Resource.RecentFiles;
            RecentListMenuItem.SubmenuOpened += (s, e) =>
            {
                var firstMenuItem = RecentListMenuItem.Items[0];
                foreach (var item in  SolutionControl.GetInstance().SolutionHistory.RecentFiles)
                {
                    if (Directory.Exists(item))
                    {
                        MenuItem menuItem = new MenuItem();
                        menuItem.Header = item;
                        menuItem.Click += (sender, e) =>
                        {
                            SolutionControl.GetInstance().OpenSolution(item);
                        };
                        RecentListMenuItem.Items.Add(menuItem);
                    }
                    else
                    {
                        SolutionControl.GetInstance().SolutionHistory.RecentFiles.Remove(item);
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
            MessageBox.Show("当前版本已经是最新版本","ColorVision",MessageBoxButton.OK);
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
    }
}
