using ColorVision.Adorners;
using ColorVision.Common.Utilities;
using ColorVision.MySql;
using ColorVision.Scheduler;
using ColorVision.Services;
using ColorVision.Settings;
using ColorVision.Solution;
using ColorVision.Solution.Searches;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Configs;
using ColorVision.UI.HotKey;
using ColorVision.UI.Menus;
using ColorVision.UI.Views;
using ColorVision.Update;
using ColorVision.UserSpace;
using HandyControl.Tools;
using HandyControl.Tools.Extension;
using log4net;
using Microsoft.Xaml.Behaviors;
using Microsoft.Xaml.Behaviors.Layout;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;
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

        public static MainWindowConfig MainWindowConfig => MainWindowConfig.Instance;

        public MainWindow()
        {
            InitializeComponent();
            MainWindowConfig.SetWindow(this);
            SizeChanged += (s, e) => MainWindowConfig.SetConfig(this);
            var IsAdministrator = Tool.IsAdministrator();
            Title += $"- {(IsAdministrator ? Properties.Resource.RunAsAdmin : Properties.Resource.NotRunAsAdmin)}";
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            MenuManager.GetInstance().Menu = Menu1;
            StatusBarGrid.DataContext = MainWindowConfig;
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

            ViewGridManager.SetViewGrid(ViewConfig.Instance.LastViewCount);

            ViewGridManager.GetInstance().ViewMaxChangedEvent += (e) =>
            {
                ViewConfig.Instance.LastViewCount = e;
            };

            Closed += (s, e) => { Environment.Exit(-1); };
            Debug.WriteLine(Properties.Resource.LaunchSuccess);

            MenuItem menulogs = new() { Header = ColorVision.Properties.Resource.ServiceLog };
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

            MenuItem menulog2 = new() { Header = ColorVision.Properties.Resource.x86ServiceLog };
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

            MenuItem menulogs1 = new() { Header = ColorVision.Properties.Resource.RCServiceLog };
            menulogs1.Click += (s, e) =>
            {
                PlatformHelper.OpenFolder("http://localhost:8080/system/log");
            };
            menulogs.Items.Insert(0, menulogs1);




            Task.Run(CheckVersion);

            Task.Run(CheckCertificate);

            Task.Run(EnsureLocalInfile);


            SolutionTab1.Content = new TreeViewControl();

            PluginLoader.LoadPlugins("Plugins");
            PluginLoader.LoadAssembly<IPlugin>(Assembly.GetExecutingAssembly());
            MenuManager.GetInstance().LoadMenuItemFromAssembly();
            this.LoadHotKeyFromAssembly();

            if (AutoUpdateConfig.Instance.IsAutoUpdate)
            {
                Task.Run(CheckUpdate);
            }
            QuartzSchedulerManager.GetInstance();

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

        public static X509Certificate2? GetCertificateFromSignedFile(string? fileName)
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

        public static void InstallCertificate(X509Certificate2 cert)
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
            if (System.Reflection.Assembly.GetExecutingAssembly().GetName()?.Version?.ToString() != SoftwareSetting.Instance.Version)
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
                            MessageBox.Show(Application.Current.GetActiveWindow(), $"{changeLogForCurrentVersion.ReplaceLineEndings()}", $"{currentVersion} {Properties.Resource.ChangeLog}：");
                        }
                        else
                        {
                            // 如果未找到匹配项，说明没有为当前版本列出变更日志
                            MessageBox.Show(Application.Current.GetActiveWindow(), "1.修复了一些已知的BUG", $"{currentVersion} {Properties.Resource.ChangeLog}：");
                        }

                    }
                    catch (Exception ex)
                    {
                        log.Error(ex.Message);
                    }
                });
                SoftwareSetting.Instance.Version = System.Reflection.Assembly.GetExecutingAssembly().GetName()?.Version?.ToString();
            }
        }



        public static async Task CheckUpdate()
        {
            await Task.Delay(1000);
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                AutoUpdater.DeleteAllCachedUpdateFiles();
                AutoUpdater autoUpdater = AutoUpdater.GetInstance();
                autoUpdater.CheckAndUpdate(false);
            });
        }


        private void StackPanelSPD_Initialized(object sender, EventArgs e)
        {
            if (sender is StackPanel stackPanel1)
            {
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
                                if (e.OldItems != null && e.NewItems != null && e.OldItems.Count == e.NewItems.Count)
                                {
                                    for (int i = 0; i < e.OldItems.Count; i++)
                                    {
                                        IDisPlayControl oldItem = (IDisPlayControl)e.OldItems[i];
                                        IDisPlayControl newItem = (IDisPlayControl)e.NewItems[i];
                                        if (oldItem is UserControl oldUserControl && newItem is UserControl newUserControl)
                                        {
                                            int index = stackPanel1.Children.IndexOf(oldUserControl);
                                            if (index >= 0)
                                            {
                                                stackPanel1.Children[index] = newUserControl;
                                            }
                                        }
                                    }
                                }
                                break;
                            case System.Collections.Specialized.NotifyCollectionChangedAction.Move:
                                if (e.OldItems != null && e.NewItems != null)
                                {
                                    // Assuming only one item is moved at a time
                                    IDisPlayControl movedItem = (IDisPlayControl)e.NewItems[0];
                                    if (movedItem is UserControl movedUserControl)
                                    {
                                        stackPanel1.Children.Remove(movedUserControl);
                                        stackPanel1.Children.Insert(e.NewStartingIndex, movedUserControl);
                                    }
                                }
                                break;
                            case System.Collections.Specialized.NotifyCollectionChangedAction.Reset:
                                stackPanel1.Children.Clear();
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
                var opoo = stackPanel1.AddAdorners(this);

                opoo.Changed += (s, e) =>
                {
                    for (int i = 0; i < stackPanel1.Children.Count; i++)
                    {
                        if (stackPanel1.Children[i] is IDisPlayControl disPlayControl)
                            ServicesConfig.Instance.PlayControls[disPlayControl.DisPlayName] = i;
                    }
                };
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
            Process.Start("explorer.exe", $"{Path.GetDirectoryName(ConfigHandler.GetInstance().DIFile)}");
        }



        private void Setting_Click(object sender, RoutedEventArgs e)
        {
            string fileName = ConfigHandler.GetInstance().DIFile;
            bool result = Tool.HasDefaultProgram(fileName);
            if (!result)
                Process.Start(result ? "explorer.exe" : "notepad.exe", fileName);
        }


        private void Login_Click(object sender, RoutedEventArgs e)
        {
            if (UserConfig.Instance.UserName != null)
            {
                var user = UserConfig.Instance;
                MessageBox.Show(user.PerMissionMode.ToString() + ":" + user.UserName + " 已经登录", "ColorVision");

            }
            else
            {
                new LoginWindow() { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
            }

        }


        private GridLength _columnDefinitionWidth;
        private void OnLeftMainContentShiftOut(object sender, RoutedEventArgs e)
        {
            ButtonShiftOut.Collapse();
            GridSplitter.IsEnabled = false;

            double targetValue = -ColumnDefinitionLeft.MaxWidth;
            _columnDefinitionWidth = ColumnDefinitionLeft.Width;

            DoubleAnimation animation = AnimationHelper.CreateAnimation(targetValue, milliseconds: 1);
            animation.FillBehavior = FillBehavior.Stop;
            animation.Completed += OnAnimationCompleted;
            LeftMainContent.RenderTransform.BeginAnimation(TranslateTransform.XProperty, animation);
            void OnAnimationCompleted(object? _, EventArgs args)
            {
                animation.Completed -= OnAnimationCompleted;
                LeftMainContent.RenderTransform.SetCurrentValue(TranslateTransform.XProperty, targetValue);

                Grid.SetColumn(MainContent, 0);
                Grid.SetColumnSpan(MainContent, 2);
                ColumnDefinitionLeft.MinWidth = 0;
                ColumnDefinitionLeft.Width = new GridLength(0);
                ButtonShiftIn.Show();
            }
        }

        private void OnLeftMainContentShiftIn(object sender, RoutedEventArgs e)
        {
            ButtonShiftIn.Collapse();

            GridSplitter.IsEnabled = true;

            double targetValue = ColumnDefinitionLeft.Width.Value;

            DoubleAnimation animation = AnimationHelper.CreateAnimation(targetValue, milliseconds: 1);
            animation.FillBehavior = FillBehavior.Stop;
            animation.Completed += OnAnimationCompleted;
            LeftMainContent.RenderTransform.BeginAnimation(TranslateTransform.XProperty, animation);

            void OnAnimationCompleted(object? _, EventArgs args)
            {
                animation.Completed -= OnAnimationCompleted;
                LeftMainContent.RenderTransform.SetCurrentValue(TranslateTransform.XProperty, targetValue);

                Grid.SetColumn(MainContent, 1);
                Grid.SetColumnSpan(MainContent, 1);

                ColumnDefinitionLeft.MinWidth = 240;
                ColumnDefinitionLeft.Width = _columnDefinitionWidth;
                ButtonShiftOut.Show();
            }
        }

        private void StatusBarGrid_Initialized(object sender, EventArgs e)
        {
             void AddStatusBarIconMetadata(StatusBarIconMetadata statusBarIconMetadata)
            {
                if (statusBarIconMetadata.Type == StatusBarType.Icon)
                {
                    // 创建 StatusBarItem
                    StatusBarItem statusBarItem = new StatusBarItem { ToolTip = statusBarIconMetadata.Description };
                    statusBarItem.DataContext = statusBarIconMetadata.Source;

                    if (statusBarIconMetadata.VisibilityBindingName != null)
                    {
                        var visibilityBinding = new Binding(statusBarIconMetadata.VisibilityBindingName)
                        {
                            Converter = (IValueConverter)Application.Current.FindResource("bool2VisibilityConverter")
                        };
                        statusBarItem.SetBinding(StatusBarItem.VisibilityProperty, visibilityBinding);
                    }
                    // 设置 MouseLeftButtonDown 事件处理程序
                    if (statusBarIconMetadata.Action != null)
                    {
                        statusBarItem.MouseLeftButtonDown += (s, e) => statusBarIconMetadata.Action.Invoke();
                    }
                    // 创建 ToggleButton
                    ToggleButton toggleButton = new ToggleButton { IsEnabled = false };
                    // 设置 Style 资源
                    if (Application.Current.TryFindResource(statusBarIconMetadata.ButtonStyleName) is Style styleResource)
                        toggleButton.Style = styleResource;

                    // 设置 IsChecked 绑定
                    var isCheckedBinding = new Binding(statusBarIconMetadata.BindingName) { Mode = BindingMode.OneWay };
                    toggleButton.SetBinding(ToggleButton.IsCheckedProperty, isCheckedBinding);
                    statusBarItem.Content = toggleButton;
                    toggleButton.DataContext = statusBarIconMetadata.Source;

                    StatusBarIconDocker.Children.Add(statusBarItem);
                }
                else if (statusBarIconMetadata.Type == StatusBarType.Text)
                {
                    StatusBarItem statusBarItem = new StatusBarItem();
                    statusBarItem.DataContext = statusBarIconMetadata.Source;

                    var Binding = new Binding(statusBarIconMetadata.BindingName) { Mode = BindingMode.OneWay };
                    statusBarItem.SetBinding(ToggleButton.ContentProperty, Binding);


                    if (statusBarIconMetadata.VisibilityBindingName != null)
                    {
                        var visibilityBinding = new Binding(statusBarIconMetadata.VisibilityBindingName)
                        {
                            Converter = (IValueConverter)Application.Current.FindResource("bool2VisibilityConverter")
                        };
                        statusBarItem.SetBinding(StatusBarItem.VisibilityProperty, visibilityBinding);
                    }

                    StatusBarTextDocker.Children.Add(statusBarItem);
                }

            }

            var allSettings = new List<StatusBarIconMetadata>();

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var type in assembly.GetTypes().Where(t => typeof(IStatusBarIconProvider).IsAssignableFrom(t) && !t.IsAbstract))
                {
                    if (Activator.CreateInstance(type) is IStatusBarIconProvider configSetting)
                    {
                        allSettings.AddRange(configSetting.GetStatusBarIconMetadata());
                    }
                }
            }
            // 先按 ConfigSettingType 分组，再在每个组内按 Order 排序
            var sortedSettings = allSettings
                .GroupBy(setting => setting.Type)
                .SelectMany(group => group.OrderBy(setting => setting.Order));

            // 将排序后的配置设置添加到集合中
            foreach (var item in sortedSettings)
            {
                AddStatusBarIconMetadata(item);
            }

        }
    }
}
