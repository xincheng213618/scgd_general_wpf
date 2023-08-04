using ColorVision.HotKey;
using ColorVision.Solution;
using ColorVision.Solution.RecentFile;
using ColorVision.SettingUp;
using ColorVision.Template;
using HandyControl.Tools.Extension;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ColorVision.MQTT;
using ColorVision.MySql;
using log4net;
using log4net.Appender;
using System.Diagnostics;
using ColorVision.Video;
using NPOI.XSSF.UserModel;
using ColorVision.Service;

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
                        windowTemplate = new WindowTemplate(WindowTemplateType.AoiParam) { Title = "AOI参数设置" };
                        TemplateControl.LoadAoiParam();
                        TemplateAbb(windowTemplate, TemplateControl.AoiParams);
                        break;
                    case "Calibration":
                        Calibration calibration = new Calibration(TemplateControl.CalibrationParams[0].Value);
                        windowTemplate = new WindowTemplate(WindowTemplateType.Calibration, calibration) { Title = "校正参数设置" };
                        TemplateAbb(windowTemplate, TemplateControl.CalibrationParams);
                        break;
                    case "PGParam":
                        PG pg = new PG(TemplateControl.PGParams[0].Value);
                        windowTemplate = new WindowTemplate(WindowTemplateType.PGParam, pg) { Title = "PG通讯设置" };
                        TemplateAbb(windowTemplate, TemplateControl.PGParams);
                        break;
                    case "LedReusltParams":
                        windowTemplate = new WindowTemplate(WindowTemplateType.LedReuslt) { Title = "数据判断模板设置" };
                        TemplateAbb(windowTemplate, TemplateControl.LedReusltParams);
                        break;
                    case "SxParms":
                        windowTemplate = new WindowTemplate(WindowTemplateType.SxParm) { Title = "源表模板设置" };
                        TemplateAbb(windowTemplate, TemplateControl.SxParams);
                        break;
                    case "FocusParm":
                        windowTemplate = new WindowTemplate(WindowTemplateType.PoiParam) { Title = "关注点设置" };
                        TemplateAbb(windowTemplate, TemplateControl.PoiParams);
                        break;
                    case "FlowParam":
                        windowTemplate = new WindowTemplate(WindowTemplateType.FlowParam) { Title = "流程引擎" };
                        TemplateAbb(windowTemplate, TemplateControl.FlowParams);
                        break;
                    case "ServiceParam":
                        new WindowService() { Owner =this,WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
                        break;
                    case "MeasureParm":
                        MeasureParamControl measure = new MeasureParamControl();
                        windowTemplate = new WindowTemplate(WindowTemplateType.MeasureParm, measure) { Title = "测量设置" };
                        TemplateControl.LoadMeasureParams();
                        TemplateAbb(windowTemplate, TemplateControl.MeasureParams);
                        break;
                    default:
                        HandyControl.Controls.Growl.Info("开发中");
                        break;
                }
            }
        }
        private void TemplateAbb<T>(WindowTemplate windowTemplate, ObservableCollection<KeyValuePair<string, T>> keyValuePairs)
        {
            windowTemplate.Owner = this;
            int id = 1;
            windowTemplate.ListConfigs.Clear();
            foreach (var item in keyValuePairs)
            {
                ListConfig listConfig = new ListConfig();
                listConfig.ID = id++;
                listConfig.Name = item.Key;
                listConfig.Value = item.Value;
                if (item.Value is PoiParam poiParam)
                {
                    listConfig.Tag = $"{poiParam.Width}*{poiParam.Height}{(GlobalSetting.GetInstance().SoftwareConfig.IsUseMySql?"": $"_{poiParam.PoiPoints.Count}")}";
                }

                windowTemplate.ListConfigs.Add(listConfig);
            }
            windowTemplate.ShowDialog();
        }

        private void MenuItem_Click8(object sender, RoutedEventArgs e)
        {
            new WindowFourColorCalibration() {Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show();
        }

        private void MenuItem9_Click(object sender, RoutedEventArgs e)
        {
            new ColorVision.WindowFlowEngine() { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show();
        }


        private void MenuItem_ProjectNew_Click(object sender, RoutedEventArgs e)
        {
            NewCreatSolution();
        }

        private void MenuItem_ProjectOpen_Click(object sender, RoutedEventArgs e)
        {
            OpenSolution();
        }
        private DateTime lastClickTime = DateTime.MinValue;

        private void TextBlock_MouseLeftButtonDown2(object sender, MouseButtonEventArgs e)
        {
            TimeSpan elapsedTime = DateTime.Now - lastClickTime;
            if (elapsedTime.TotalMilliseconds <= 300) 
            {
                System.Diagnostics.Process.Start("explorer.exe", $"{GlobalSetting.GetInstance().SoftwareConfig.SolutionConfig.SolutionFullName}");
            }

            lastClickTime = DateTime.Now;
        }

        private void MenuItem_Click_2(object sender, RoutedEventArgs e)
        {
            OpenSetting();
        }

        private void OpenSolution()
        {
            OpenSolutionWindow openSolutionWindow = new OpenSolutionWindow() { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            openSolutionWindow.Closed += delegate
            {
                string SolutionDirectoryPath = openSolutionWindow.FullName;
                SolutionConfig ProjectConfig = GlobalSetting.GetInstance().SoftwareConfig.SolutionConfig;
                if (Directory.Exists(SolutionDirectoryPath))
                {
                    DirectoryInfo Info = new DirectoryInfo(SolutionDirectoryPath);
                    ProjectConfig.SolutionName = Info.Name;
                    ProjectConfig.SolutionFullName = Info.FullName;
                    RecentFileList SolutionHistory = new RecentFileList() { Persister = new RegistryPersister("Software\\ColorVision\\SolutionHistory") };
                    SolutionHistory.InsertFile(Info.FullName);
                }

            };
            openSolutionWindow.Show();
        }

        private void NewCreatSolution()
        {
            NewCreateWindow newCreatWindow = new NewCreateWindow() { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            newCreatWindow.Closed += delegate
            {
                if (newCreatWindow.IsCreate)
                {
                    string SolutionDirectoryPath = newCreatWindow.NewCreateViewMode.DirectoryPath + "\\" + newCreatWindow.NewCreateViewMode.Name;
                    OpenSolution(SolutionDirectoryPath);
                }
            };
            newCreatWindow.ShowDialog();
        }

        private void OpenSolution(string SolutionFullPath)
        {
            SolutionConfig ProjectConfig = GlobalSetting.GetInstance().SoftwareConfig.SolutionConfig;
            if (Directory.Exists(SolutionFullPath))
            {
                DirectoryInfo Info = new DirectoryInfo(SolutionFullPath);
                ProjectConfig.SolutionName = Info.Name;
                ProjectConfig.SolutionFullName = Info.FullName;
                SolutionHistory.InsertFile(Info.FullName);
            }
        }

        private void OpenSetting()
        {
            new SettingWindow() { Owner =this, WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }


        RecentFileList SolutionHistory = new RecentFileList() { Persister = new RegistryPersister("Software\\ColorVision\\SolutionHistory") };

        private void Menu_Initialized(object sender, EventArgs e)
        {
            Application.Current.MainWindow = this;
            Application.Current.MainWindow.AddHotKeys(new HotKeys("打开工程", new Hotkey(Key.O, ModifierKeys.Control), OpenSolution));
            Application.Current.MainWindow.AddHotKeys(new HotKeys("新建工程", new Hotkey(Key.N, ModifierKeys.Control), NewCreatSolution));
            Application.Current.MainWindow.AddHotKeys(new HotKeys("设置", new Hotkey(Key.I, ModifierKeys.Control), OpenSetting));
            Application.Current.MainWindow.AddHotKeys(new HotKeys("关于", new Hotkey(Key.F1, ModifierKeys.Control), AboutMsg));

            MenuItem RecentListMenuItem = null;




            RecentListMenuItem ??= new MenuItem();
            RecentListMenuItem.Header = "最近使用过的文件(_F)";
            RecentListMenuItem.SubmenuOpened += (s, e) =>
            {
                var firstMenuItem = RecentListMenuItem.Items[0];
                foreach (var item in SolutionHistory.RecentFiles)
                {
                    if (Directory.Exists(item))
                    {
                        MenuItem menuItem = new MenuItem();
                        menuItem.Header = item;
                        menuItem.Click += (sender, e) =>
                        {
                            OpenSolution(item);
                        };
                        RecentListMenuItem.Items.Add(menuItem);
                    }
                    else
                    {
                        SolutionHistory.RecentFiles.Remove(item);
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
            new LicenseManger() { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
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
        private void MenuItem10_Click(object sender, RoutedEventArgs e)
        {
            new CameraVideoConnect() { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }
        private void LogF_Click(object sender, RoutedEventArgs e)
        {
            var fileAppender = (log4net.Appender.FileAppender)LogManager.GetRepository().GetAppenders().FirstOrDefault(a => a is log4net.Appender.FileAppender);
            if (fileAppender != null)
            {
                System.Diagnostics.Process.Start("explorer.exe", $"{Path.GetDirectoryName(fileAppender.File)}");
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
            bool hasDefaultProgram = false;
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(GlobalSetting.GetInstance().SoftwareConfigFileName);
                psi.UseShellExecute = true;
                Process.Start(psi);
                hasDefaultProgram = true;
            }
            catch (FileNotFoundException)
            {
                hasDefaultProgram = false;
            }
            if (hasDefaultProgram)
            {
                System.Diagnostics.Process.Start("explorer.exe", $"{GlobalSetting.GetInstance().SoftwareConfigFileName}");
            }
            else
            {
                System.Diagnostics.Process.Start("notepad.exe", GlobalSetting.GetInstance().SoftwareConfigFileName);

            }
        }
    }
}
