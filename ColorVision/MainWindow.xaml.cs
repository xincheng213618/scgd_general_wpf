using ColorVision.Templates;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using ColorVision.Themes;
using ColorVision.Flow;
using System.Diagnostics;
using ColorVision.Services;
using ColorVision.Solution;
using LiveChartsCore.VisualElements;
using MySqlConnector.Logging;
using System.Windows.Documents;
using ColorVision.Media;
using ColorVision.Update;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ColorVision
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 
    public partial class MainWindow : Window
    {
        public ViewGridManager ViewGridManager { get; set; }

        public GlobalSetting GlobalSetting { get; set; }
        public SoftwareSetting SoftwareSetting
        {
            get
            {
                if (GlobalSetting.SoftwareConfig.SoftwareSetting == null)
                    GlobalSetting.SoftwareConfig.SoftwareSetting = new SoftwareSetting();
                return GlobalSetting.SoftwareConfig.SoftwareSetting;
            }
        }
        public MainWindow()
        {
            InitializeComponent();

            if (SoftwareSetting.IsRestoreWindow && SoftwareSetting.Height != 0 && SoftwareSetting.Width != 0)
            {
                this.Top = SoftwareSetting.Top;
                this.Left = SoftwareSetting.Left;
                this.Height = SoftwareSetting.Height;
                this.Width = SoftwareSetting.Width;
                this.WindowState = (WindowState)SoftwareSetting.WindowState;
            }
            this.SizeChanged +=(s, e) =>
            {
                if (SoftwareSetting.IsRestoreWindow)
                {
                    SoftwareSetting.Top = this.Top;
                    SoftwareSetting.Left = this.Left;
                    SoftwareSetting.Height = this.Height;
                    SoftwareSetting.Width = this.Width;
                    SoftwareSetting.WindowState = (int)this.WindowState;
                }
            };
        }

        private  void Window_Initialized(object sender, EventArgs e)
        {
            GlobalSetting = GlobalSetting.GetInstance();
            SolutionManager.GetInstance();
            if (!WindowConfig.IsExist||(WindowConfig.IsExist&& WindowConfig.Icon == null)) {
                ThemeManager.Current.SystemThemeChanged += (e) => {
                    this.Icon = new BitmapImage(new Uri($"pack://application:,,,/ColorVision;component/Assets/Image/{(e == Theme.Light ? "ColorVision.ico" : "ColorVision1.ico")}"));
                };
                if (ThemeManager.Current.SystemTheme == Theme.Dark)
                    this.Icon = new BitmapImage(new Uri("pack://application:,,,/ColorVision;component/Assets/Image/ColorVision1.ico"));
            }

            if (WindowConfig.IsExist)
            {
                if (WindowConfig.Icon != null)
                    this.Icon = WindowConfig.Icon;
                this.Title = WindowConfig.Title ?? this.Title;
            }
            TemplateControl = TemplateControl.GetInstance();
            ViewGridManager = ViewGridManager.GetInstance();
            ViewGridManager.MainView = ViewGrid;

            StatusBarGrid.DataContext = GlobalSetting.GetInstance();
            MenuStatusBar.DataContext = GlobalSetting.GetInstance().SoftwareConfig;

            FlowDisplayControl flowDisplayControl = new FlowDisplayControl();
            SPDisplay.Children.Insert(0, flowDisplayControl);

            ViewGridManager.GetInstance().SetViewNum(1);
            this.Closed += (s, e) => { Environment.Exit(-1); };
            Debug.WriteLine("启动成功");

            MenuItem menulogs = new MenuItem() { Header = "日志" };
            MenuHelp.Items.Insert(0, menulogs);

            MenuItem menulog = new MenuItem() { Header = "x64服务日志" };
            menulog.Click += (s, e) =>
            {
                Process.Start("explorer.exe", "http://localhost:8064/system/log");
            };
            menulogs.Items.Insert(0, menulog);

            MenuItem menulog1 = new MenuItem() { Header = "相机日志" };
            menulog1.Click += (s, e) =>
            {
                Process.Start("explorer.exe", "http://localhost:8064/system/device/camera/log");
            };
            menulogs.Items.Insert(1, menulog1);

            MenuItem menulog2 = new MenuItem() { Header = "x86服务相机日志" };
            menulog2.Click += (s, e) =>
            {
                Process.Start("explorer.exe", "http://localhost:8086/system/log");
            };
            menulogs.Items.Insert(2, menulog2);

            MenuItem menulog3 = new MenuItem() { Header = "光谱仪日志" };
            menulog3.Click += (s, e) =>
            {
                Process.Start("explorer.exe", "http://localhost:8086/system/device/Spectrum/log");
            };
            menulogs.Items.Insert(3, menulog3);

#if (DEBUG == true)
            MenuItem menuItem = new MenuItem() { Header = "测试" };
            MenuItem menuItem1 = new MenuItem() { Header = "折线图" };
            menuItem1.Click += Test_Click;
            menuItem.Items.Add(menuItem1);
            Menu1.Items.Add(menuItem);


            MenuItem menuItem2 = new MenuItem() { Header = "校正上传" ,Tag = "CalibrationUpload" };
            menuItem2.Click += MenuItem_Template;
            menuItem.Items.Add(menuItem2);

            MenuItem menuItem3 = new MenuItem() { Header = "重启服务", Tag = "CalibrationUpload" };
            menuItem3.Click += (s,e) =>
            {
                ColorVision.Util.Tool.ExecuteCommandAsAdmin("net stop RegistrationCenterService&net stop CVMainService_x64&net start RegistrationCenterService&net start CVMainService_x64");
            };
            menuItem.Items.Add(menuItem3);

            MenuItem menuItem4 = new MenuItem() { Header = "光谱" };
            menuItem4.Click += ReadTest;
            menuItem.Items.Add(menuItem4);

#endif

            if (GlobalSetting.GetInstance().SoftwareConfig.SoftwareSetting.IsAutoUpdate)
            {
                Thread thread1 = new Thread(async () => await CheckUpdate()) { IsBackground = true };
                thread1.Start();
            }
        }
        public async Task CheckUpdate()
        {
            await Task.Delay(1000);
            Application.Current.Dispatcher.Invoke(() =>
            {
                AutoUpdater autoUpdater = AutoUpdater.GetInstance();
                autoUpdater.CheckAndUpdate(false);
            });

        }

        class SpectralData
        {
            public double Wavelength { get; set; }
            public double X { get; set; }
            public double Y { get; set; }
            public double Z { get; set; }
        }
        List<SpectralData> spectralDataList = new List<SpectralData>();

        public void ReadTest(object sender, RoutedEventArgs e)
        {
            var lines = File.ReadAllLines("C:\\Users\\17917\\Desktop\\三刺激值曲线CIE2015 的副本.csv");

            foreach (var line in lines)
            {
                var values = line.Split(',');
                if (values.Length == 4)
                {
                    var spectralData = new SpectralData
                    {
                        Wavelength = double.Parse(values[0], CultureInfo.InvariantCulture),
                        X = double.Parse(values[1], CultureInfo.InvariantCulture),
                        Y = double.Parse(values[2], CultureInfo.InvariantCulture),
                        Z = double.Parse(values[3], CultureInfo.InvariantCulture)
                    };
                    spectralDataList.Add(spectralData);
                }
            }
        }



        private void MenuStatusBar_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem)
            {
                menuItem.IsChecked = !menuItem.IsChecked;
            }
        }



        private void StackPanelSPD_Initialized(object sender, EventArgs e)
        {
            if (sender is StackPanel stackPanel1)
            {
                var stackPanel = ServiceManager.GetInstance().StackPanel;
                stackPanel1.Children.Add(stackPanel);
                //bool isDown = false;
                //AdornerLayer adornerLayer = AdornerLayer.GetAdornerLayer(Root);
                //DragDropAdorner adorner = null;
                //stackPanel.PreviewMouseLeftButtonDown += (s, e) =>
                //{
                //    isDown = true;
                //    var control = stackPanel.Children[0];
                //    adorner = new DragDropAdorner(control);
                //    adornerLayer.Add(adorner);

                //};
                //stackPanel.PreviewMouseUp += (s, e) =>
                //{
                //    if (isDown)
                //    {
                //        if (adorner != null)
                //            adornerLayer.Remove(adorner);

                //    }
                //};
            }

        }

        private void StackPanel_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
        }

        private void ViewGrid_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && int.TryParse(button.Tag.ToString(), out int nums))
            {
                switch (nums)
                {
                    case 20:
                        ViewGridManager.SetViewGridTwo();
                        break;
                    case 21:
                        ViewGridManager.SetViewGrid(2);
                        break;
                    case 30:
                        ViewGridManager.SetViewGridThree();
                        break;
                    case 31:
                        ViewGridManager.SetViewGridThree(false);
                        break;
                    default:
                        ViewGridManager.SetViewGrid(nums);
                        break;
                }
            }
        }

        private void Test_Click(object sender, RoutedEventArgs e)
        {
            Window1 window1 = new Window1();
            window1.Show();
        }

        private void Login_Click(object sender, RoutedEventArgs e)
        {
            if (UserManager.Current.UserConfig != null)
            {
                var user = UserManager.Current.UserConfig;
                MessageBox.Show(user.PerMissionMode.ToString() + ":" + user.UserName + " 已经登录", "ColorVision");

            }
            else
            {
                new LoginWindow() { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
            }

        }


    }
}
