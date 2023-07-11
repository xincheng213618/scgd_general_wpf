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
                        TemplateControl.LoadPoiParam();
                        TemplateAbb(windowTemplate, TemplateControl.PoiParams);
                        break;
                    case "FlowParam":
                        windowTemplate = new WindowTemplate(WindowTemplateType.FlowParam) { Title = "流程引擎" };
                        TemplateControl.LoadFlowParam();
                        TemplateAbb(windowTemplate, TemplateControl.FlowParams);
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

        private void MenuItem_Click_7(object sender, RoutedEventArgs e)
        {
            new WindowORM().Show();
        }

        private void MenuItem_Click8(object sender, RoutedEventArgs e)
        {
            new WindowFourColorCalibration() {Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show();
        }

        private void MenuItem9_Click(object sender, RoutedEventArgs e)
        {
            new FlowEngine.WindowFlowEngine() { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show();
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
                System.Diagnostics.Process.Start("explorer.exe", $"{GlobalSetting.GetInstance().SoftwareConfig.ProjectConfig.ProjectFullName}");
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
                ProjectConfig ProjectConfig = GlobalSetting.GetInstance().SoftwareConfig.ProjectConfig;
                if (Directory.Exists(SolutionDirectoryPath))
                {
                    DirectoryInfo Info = new DirectoryInfo(SolutionDirectoryPath);
                    ProjectConfig.ProjectName = Info.Name;
                    ProjectConfig.ProjectFullName = Info.FullName;
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
            ProjectConfig ProjectConfig = GlobalSetting.GetInstance().SoftwareConfig.ProjectConfig;
            if (Directory.Exists(SolutionFullPath))
            {
                DirectoryInfo Info = new DirectoryInfo(SolutionFullPath);
                ProjectConfig.ProjectName = Info.Name;
                ProjectConfig.ProjectFullName = Info.FullName;
                SolutionHistory.InsertFile(Info.FullName);
            }
        }

        private void OpenSetting()
        {
            new SettingWindow() { Owner =this, WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show();
        }
        RecentFileList SolutionHistory = new RecentFileList() { Persister = new RegistryPersister("Software\\ColorVision\\SolutionHistory") };

        private void Menu_Initialized(object sender, EventArgs e)
        {

            Application.Current.MainWindow.AddHotKeys(new HotKeys("打开当前工程", new Hotkey(Key.O, ModifierKeys.Control), OpenSolution));
            Application.Current.MainWindow.AddHotKeys(new HotKeys("新建工程", new Hotkey(Key.N, ModifierKeys.Control), NewCreatSolution));
            Application.Current.MainWindow.AddHotKeys(new HotKeys("设置", new Hotkey(Key.I, ModifierKeys.Control), OpenSetting));


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
            new LicenseManger() { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show();
        }

        private void MenuItem_Exit(object sender, RoutedEventArgs e)
        {
            Environment.Exit(0);
        }
    }
}
