using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using ColorVision.Themes;
using System.Diagnostics;
using ColorVision.Services;
using ColorVision.Solution;
using ColorVision.Update;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.ServiceProcess;
using ColorVision.Users;
using System.Text.RegularExpressions;
using log4net;
using ColorVision.Services.Flow;
using ColorVision.SettingUp;
using System.Security.Cryptography.X509Certificates;
using ColorVision.Common.Util;
using Microsoft.Xaml.Behaviors.Layout;
using Microsoft.Xaml.Behaviors;

namespace ColorVision
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 
    public partial class MainWindow : Window
    {

        private static readonly ILog log = LogManager.GetLogger(typeof(MainWindow));
        public ViewGridManager ViewGridManager { get; set; }

        public ConfigHandler ConfigHandler { get; set; }
        public SoftwareSetting SoftwareSetting
        {
            get
            {
                return ConfigHandler.SoftwareConfig.SoftwareSetting;
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

                if (this.Width > SystemParameters.WorkArea.Width)
                {
                    this.Width = SystemParameters.WorkArea.Width;
                }
                if (this.Height > SystemParameters.WorkArea.Height)
                {
                    this.Height = SystemParameters.WorkArea.Height;
                }
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
            var IsAdministrator = Utils.IsAdministrator();
            this.Title = Title + $"- {(IsAdministrator ? Properties.Resource.RunAsAdmin : Properties.Resource.NotRunAsAdmin)}";
        }

        private  void Window_Initialized(object sender, EventArgs e)
        {
            ConfigHandler = ConfigHandler.GetInstance();
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
            ViewGridManager = ViewGridManager.GetInstance();
            ViewGridManager.MainView = ViewGrid;

            StatusBarGrid.DataContext = ConfigHandler.GetInstance();
            MenuStatusBar.DataContext = ConfigHandler.GetInstance().SoftwareConfig;

            FlowDisplayControl flowDisplayControl = new FlowDisplayControl();
            SPDisplay.Children.Insert(0, flowDisplayControl);

            ViewGridManager.GetInstance().SetViewNum(1);
            this.Closed += (s, e) => { Environment.Exit(-1); };
            Debug.WriteLine(ColorVision.Properties.Resource.LaunchSuccess);

            MenuItem menulogs = new MenuItem() { Header = ColorVision.Properties.Resource.Log };
            MenuHelp.Items.Insert(0, menulogs);

            MenuItem menulog = new MenuItem() { Header = Properties.Resource.x64ServiceLog };
            menulog.Click += (s, e) =>
            {
                Process.Start("explorer.exe", "http://localhost:8064/system/log");
            };
            menulogs.Items.Insert(0, menulog);

            MenuItem menulog1 = new MenuItem() { Header = ColorVision.Properties.Resource.CameraLog };
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

            MenuItem menulog3 = new MenuItem() { Header = ColorVision.Properties.Resource.SpectrometerLog };
            menulog3.Click += (s, e) =>
            {
                Process.Start("explorer.exe", "http://localhost:8086/system/device/Spectrum/log");
            };
            menulogs.Items.Insert(3, menulog3);

#if (DEBUG == true)
            MenuItem menuItem = new MenuItem() { Header = ColorVision.Properties.Resource.ExperimentalFeature };
            MenuItem menuItem1 = new MenuItem() { Header = "折线图" };
            menuItem1.Click += Test_Click;
            menuItem.Items.Add(menuItem1);
            Menu1.Items.Add(menuItem);


            MenuItem menuItem3 = new MenuItem() { Header = ColorVision.Properties.Resource.RestartService, Tag = "CalibrationUpload" };
            menuItem3.Click += (s,e) =>
            {
                Common.Utilities.Tool.ExecuteCommandAsAdmin("net stop RegistrationCenterService&net stop CVMainService_x64&net start RegistrationCenterService&net start CVMainService_x64");
            };
            menuItem.Items.Add(menuItem3);

            MenuItem menuItem4 = new MenuItem() { Header = "光谱" };
            menuItem4.Click += ReadTest;
            menuItem.Items.Add(menuItem4);

#endif

            if (ConfigHandler.GetInstance().SoftwareConfig.SoftwareSetting.IsAutoUpdate)
            {
                Thread thread1 = new Thread(async () => await CheckUpdate()) { IsBackground = true };
                thread1.Start();
            }
            if (ConfigHandler.GetInstance().SoftwareConfig.SoftwareSetting.IsOpenLoaclService)
            {
                Task.Run(CheckLocalService);
            }
            Task.Run(CheckVersion);

            Task.Run(CheckCertificate);
        }
        public async Task CheckCertificate()
        {
            await Task.Delay(100);

            Application.Current.Dispatcher.Invoke(() =>
            {
                X509Certificate2 x509Certificate2 = GetCertificateFromSignedFile(Process.GetCurrentProcess()?.MainModule?.FileName);
                if (x509Certificate2 != null)
                {
                    MenuItem menuItem = new MenuItem() { Header = ColorVision.Properties.Resource.InstallCertificate };
                    menuItem.Click += (s,e) =>
                    {
                        InstallCertificate(x509Certificate2);
                    };
                    MenuHelp.Items.Insert(5, menuItem);
                }
            });
        }

        static X509Certificate2? GetCertificateFromSignedFile(string? fileName)
        {
            if (!File.Exists(fileName)) return null;
            X509Certificate2 certificate = null;
            try
            {
                X509Certificate signer = X509Certificate.CreateFromSignedFile(fileName);
                certificate = new X509Certificate2(signer);
            }
            catch (Exception ex)
            {
                log.Warn(ex.Message);
            }
            return certificate;
        }

        static void InstallCertificate(X509Certificate2 cert)
        {
            try
            {
                X509Store store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
                store.Open(OpenFlags.ReadWrite);
                store.Add(cert);
                store.Close();

                // 显示一个UI来提示用户安装证书
                X509Certificate2UI.DisplayCertificate(cert);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while installing the certificate: {ex.Message}");
            }
        }

        public async Task CheckVersion()
        {
            await Task.Delay(500);
            if (System.Reflection.Assembly.GetExecutingAssembly().GetName()?.Version?.ToString() != ConfigHandler.SoftwareConfig.Version)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        string? currentVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName()?.Version?.ToString();
                        string changelogPath = "CHANGELOG.md";

                        // 读取CHANGELOG.md文件的所有内容
                        string changelogContent = File.ReadAllText(changelogPath);

                        // 使用正则表达式来匹配当前版本的日志条目
                        string versionPattern = $"## \\[{currentVersion}\\].*?\\n(.*?)(?=\\n## |$)";
                        Match match = Regex.Match(changelogContent, versionPattern, RegexOptions.Singleline);

                        if (match.Success)
                        {
                            // 如果找到匹配项，提取变更日志
                            string changeLogForCurrentVersion = match.Groups[1].Value.Trim();
                            // 显示变更日志
                            MessageBox.Show(Application.Current.MainWindow, $"{changeLogForCurrentVersion.ReplaceLineEndings()}",$"{currentVersion} 的变更日志：");
                        }
                        else
                        {
                            // 如果未找到匹配项，说明没有为当前版本列出变更日志
                            MessageBox.Show(Application.Current.MainWindow,"1.修复了一些已知的BUG", $"{currentVersion} 的变更日志：");
                        }

                    }
                    catch(Exception ex)
                    {
                        log.Error(ex.Message);
                    }



                });
                ConfigHandler.SoftwareConfig.Version = System.Reflection.Assembly.GetExecutingAssembly().GetName()?.Version?.ToString();
                ConfigHandler.SaveConfig();
            }
        }

        public static async Task CheckLocalService()
        {
            await Task.Delay(2000);
            try
            {
                string excmd = string.Empty;
                ServiceController sc = new ServiceController("RegistrationCenterService");
                if (sc.Status == ServiceControllerStatus.Stopped)
                {
                    excmd += "net start RegistrationCenterService&&";
                }
                ServiceController sc1 = new ServiceController("CVMainService_x86");
                if (sc1.Status == ServiceControllerStatus.Stopped)
                {
                    excmd += "net start CVMainService_x86&&";
                }
                ServiceController sc2 = new ServiceController("CVMainService_x64");
                if (sc2.Status == ServiceControllerStatus.Stopped)
                {
                    excmd += "net start CVMainService_x64&&";
                }
                if (!string.IsNullOrEmpty(excmd))
                {
                    excmd += "1";
                    Common.Utilities.Tool.ExecuteCommandAsAdmin(excmd);
                }
                ///非管理员模式无法直接通过sc启动程序
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }

        }

        public static async Task CheckUpdate()
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

                FluidMoveBehavior fluidMoveBehavior = new FluidMoveBehavior
                {
                    AppliesTo = FluidMoveScope.Children,
                    Duration = TimeSpan.FromSeconds(0.5)
                };

                Interaction.GetBehaviors(ServiceManager.GetInstance().StackPanel).Add(fluidMoveBehavior);
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

        private void ChangeLog_Clik(object sender, RoutedEventArgs e)
        {
            ChangelogWindow changelogWindow = new ChangelogWindow();
            string changelogPath = "CHANGELOG.md";
            string changelogContent = File.ReadAllText(changelogPath);
            changelogWindow.SetChangelogText(changelogContent);
            changelogWindow.ShowDialog();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {

            if (ServiceManager.GetInstance().StackPanel.Children[0] is UserControl userControl1)
            {
                ServiceManager.GetInstance().StackPanel.Children.RemoveAt(0);
                ServiceManager.GetInstance().StackPanel.Children.Add(userControl1);
            }
        }
    }
}
