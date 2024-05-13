using ColorVision.Adorners;
using ColorVision.Common.Utilities;
using ColorVision.MQTT;
using ColorVision.MySql;
using ColorVision.Projects;
using ColorVision.Services.Flow;
using ColorVision.Services.RC;
using ColorVision.Services.Templates;
using ColorVision.Settings;
using ColorVision.Solution;
using ColorVision.Solution.Searches;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.HotKey;
using ColorVision.Update;
using ColorVision.UserSpace;
using ColorVision.Utils;
using log4net;
using Microsoft.Xaml.Behaviors;
using Microsoft.Xaml.Behaviors.Layout;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

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

        public static ConfigHandler ConfigHandler  => ConfigHandler.GetInstance();
        public static SoftwareSetting SoftwareSetting => ConfigHandler.SoftwareConfig.SoftwareSetting;

        public MainWindow()
        {
            InitializeComponent();

            if (SoftwareSetting.IsRestoreWindow && SoftwareSetting.Height != 0 && SoftwareSetting.Width != 0)
            {
                Top = SoftwareSetting.Top;
                Left = SoftwareSetting.Left;
                Height = SoftwareSetting.Height;
                Width = SoftwareSetting.Width;
                WindowState = (WindowState)SoftwareSetting.WindowState;

                if (Width > SystemParameters.WorkArea.Width)
                {
                    Width = SystemParameters.WorkArea.Width;
                }
                if (Height > SystemParameters.WorkArea.Height)
                {
                    Height = SystemParameters.WorkArea.Height;
                }
            }
            SizeChanged += (s, e) =>
            {
                if (SoftwareSetting.IsRestoreWindow)
                {
                    SoftwareSetting.Top = Top;
                    SoftwareSetting.Left = Left;
                    SoftwareSetting.Height = Height;
                    SoftwareSetting.Width = Width;
                    SoftwareSetting.WindowState = (int)WindowState;
                }
            };
            var IsAdministrator = Tool.IsAdministrator();
            Title = Title + $"- {(IsAdministrator ? Properties.Resource.RunAsAdmin : Properties.Resource.NotRunAsAdmin)}";
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            TemplateControl.GetInstance();
            MenuManager.GetInstance().Menu = Menu1;
            if (MySqlControl.GetInstance().IsConnect)
            {
            }


            if (!WindowConfig.IsExist || (WindowConfig.IsExist && WindowConfig.Icon == null))
            {
                ThemeManager.Current.SystemThemeChanged += (e) =>
                {
                    Icon = new BitmapImage(new Uri($"pack://application:,,,/ColorVision;component/Assets/Image/{(e == Theme.Light ? "ColorVision.ico" : "ColorVision1.ico")}"));
                };
                if (ThemeManager.Current.SystemTheme == Theme.Dark)
                    Icon = new BitmapImage(new Uri("pack://application:,,,/ColorVision;component/Assets/Image/ColorVision1.ico"));
            }

            if (WindowConfig.IsExist)
            {
                if (WindowConfig.Icon != null)
                    Icon = WindowConfig.Icon;
                Title = WindowConfig.Title ?? Title;
            }

            ViewGridManager SolutionViewGridManager = new();
            SolutionViewGridManager.MainView = SolutionGrid;
            SolutionView solutionView = new();
            SolutionViewGridManager.AddView(0, solutionView);
            solutionView.View.ViewIndex = 0;
            SolutionViewGridManager.SetViewNum(-1);
            ViewGridManager = ViewGridManager.GetInstance();
            ViewGridManager.MainView = ViewGrid;

            StatusBarGrid.DataContext = ConfigHandler.GetInstance();
            MenuStatusBar.DataContext = ConfigHandler.GetInstance().SoftwareConfig;

            ViewGridManager.GetInstance().SetViewNum(1);
            Closed += (s, e) => { Environment.Exit(-1); };
            Debug.WriteLine(Properties.Resource.LaunchSuccess);

            MenuItem menulogs = new() { Header = Properties.Resource.Log };
            MenuHelp.Items.Insert(0, menulogs);

            MenuItem menulog = new() { Header = Properties.Resource.x64ServiceLog };
            menulog.Click += (s, e) =>
            {
                PlatformHelper.OpenFolder("http://localhost:8064/system/log");
            };
            menulogs.Items.Insert(0, menulog);

            MenuItem menulog1 = new() { Header = Properties.Resource.CameraLog };
            menulog1.Click += (s, e) =>
            {
                PlatformHelper.OpenFolder("http://localhost:8064/system/device/camera/log");
            };
            menulogs.Items.Insert(1, menulog1);

            MenuItem menulog2 = new() { Header = "x86服务相机日志" };
            menulog2.Click += (s, e) =>
            {
                PlatformHelper.OpenFolder("http://localhost:8086/system/log");

            };
            menulogs.Items.Insert(2, menulog2);

            MenuItem menulog3 = new() { Header = Properties.Resource.SpectrometerLog };
            menulog3.Click += (s, e) =>
            {
                PlatformHelper.OpenFolder("http://localhost:8086/system/device/Spectrum/log");
            };
            menulogs.Items.Insert(3, menulog3);

            MenuItem menulogs1 = new() { Header = "RC服务日志" };
            menulogs1.Click += (s, e) =>
            {
                PlatformHelper.OpenFolder("http://localhost:8080/system/log");
            };
            menulogs.Items.Insert(0, menulogs1);

            MenuItem menuItem4 = new() { Header = "河源精电" };
            menuItem4.Click += (s, e) =>
            {
                ProjectHeyuan projectHeiyuan = new();
                projectHeiyuan.Show();
            };
            menulogs.Items.Add(menuItem4);

            if (ConfigHandler.GetInstance().SoftwareConfig.SoftwareSetting.IsAutoUpdate)
            {
                Thread thread1 = new(async () => await CheckUpdate()) { IsBackground = true };
                thread1.Start();
            }



            Task.Run(CheckVersion);

            Task.Run(CheckCertificate);

            Task.Run(EnsureLocalInfile);


            SolutionTab1.Content = new TreeViewControl();

            PluginLoader.LoadPluginsAssembly("Plugins");

            PluginLoader.LoadPlugins("Plugins");
            PluginLoader.LoadAssembly<IPlugin>(Assembly.GetExecutingAssembly());
            MenuManager.GetInstance().LoadMenuItemFromAssembly(Assembly.GetExecutingAssembly());
            this.LoadHotKeyFromAssembly<IHotKey>(Assembly.GetExecutingAssembly());

            Application.Current.MainWindow = this;
        }

        public async static Task EnsureLocalInfile()
        {
            await Task.Delay(3000);
            log.Info($"{DateTime.Now}:EnsureLocalInfile ");
            try
            {
                if (MySqlControl.GetInstance().IsConnect)
                    MySqlControl.GetInstance().EnsureLocalInfile();
            }
            catch (Exception ex)
            {
                log.Info($"{DateTime.Now}:EnsureLocalInfile {ex.Message} ");

            }

        }


        public async Task CheckCertificate()
        {
            await Task.Delay(100);

            Application.Current.Dispatcher.Invoke(() =>
            {
                X509Certificate2 x509Certificate2 = GetCertificateFromSignedFile(Process.GetCurrentProcess()?.MainModule?.FileName);
                if (x509Certificate2 != null)
                {
                    MenuItem menuItem = new() { Header = Properties.Resource.InstallCertificate };
                    menuItem.Click += (s, e) =>
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
                X509Store store = new(StoreName.Root, StoreLocation.CurrentUser);
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

        public static async Task CheckVersion()
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
                            MessageBox.Show(Application.Current.MainWindow, $"{changeLogForCurrentVersion.ReplaceLineEndings()}", $"{currentVersion} {Properties.Resource.ChangeLog}：");
                        }
                        else
                        {
                            // 如果未找到匹配项，说明没有为当前版本列出变更日志
                            MessageBox.Show(Application.Current.MainWindow, "1.修复了一些已知的BUG", $"{currentVersion} {Properties.Resource.ChangeLog}：");
                        }

                    }
                    catch (Exception ex)
                    {
                        log.Error(ex.Message);
                    }



                });
                ConfigHandler.SoftwareConfig.Version = System.Reflection.Assembly.GetExecutingAssembly().GetName()?.Version?.ToString();
                ConfigHandler.SaveConfig();
            }
        }



        public static async Task CheckUpdate()
        {
            await Task.Delay(1000);
            Application.Current.Dispatcher.Invoke(() =>
            {
                AutoUpdater.DeleteAllCachedUpdateFiles();
                AutoUpdater autoUpdater = AutoUpdater.GetInstance();
                autoUpdater.CheckAndUpdate(false);
            });
        }

        private void MenuStatusBar_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem)
            {
                menuItem.IsChecked = !menuItem.IsChecked;
            }
        }

        private FlowDisplayControl flowDisplayControl;

        private void StackPanelSPD_Initialized(object sender, EventArgs e)
        {
            if (sender is StackPanel stackPanel1)
            {
                flowDisplayControl ??= new FlowDisplayControl();
                if (stackPanel1.Children.Contains(flowDisplayControl))
                    stackPanel1.Children.Remove(flowDisplayControl);
                stackPanel1.Children.Insert(0, flowDisplayControl);

                foreach (var item in DisPlayManager.GetInstance().IDisPlayControls)
                {
                    if (item is UserControl userControl)
                        stackPanel1.Children.Add(userControl);
                }

                DisPlayManager.GetInstance().IDisPlayControls.CollectionChanged += (s, e) =>
                {
                    if (s is ObservableCollection<IDisPlayControl> disPlayControls)
                    {
                        switch (e.Action)
                        {
                            case System.Collections.Specialized.NotifyCollectionChangedAction.Add:
                                if (e.NewItems != null)
                                    foreach (IDisPlayControl newItem in e.NewItems)
                                        if (newItem is UserControl userControl)
                                            stackPanel1.Children.Add(userControl);
                                break;
                            case System.Collections.Specialized.NotifyCollectionChangedAction.Remove:
                                if (e.OldItems != null)
                                    foreach (IDisPlayControl oldItem in e.OldItems)
                                        if (oldItem is UserControl userControl)
                                            stackPanel1.Children.Remove(userControl);
                                break;
                            case System.Collections.Specialized.NotifyCollectionChangedAction.Replace:
                                break;
                            case System.Collections.Specialized.NotifyCollectionChangedAction.Move:
                                break;
                            case System.Collections.Specialized.NotifyCollectionChangedAction.Reset:
                                stackPanel1.Children.Clear();
                                stackPanel1.Children.Insert(0, flowDisplayControl);
                                break;
                            default:
                                break;
                        }
                    }
                };


                FluidMoveBehavior fluidMoveBehavior = new()
                {
                    AppliesTo = FluidMoveScope.Children,
                    Duration = TimeSpan.FromSeconds(0.1)
                };

                Interaction.GetBehaviors(stackPanel1).Add(fluidMoveBehavior);
                stackPanel1.AddAdorners(this);
            }

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
