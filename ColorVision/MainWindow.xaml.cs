using ColorVision.Adorners;
using ColorVision.Common.Utilities;
using ColorVision.Engine.Rbac;
using ColorVision.FloatingBall;
using ColorVision.Scheduler;
using ColorVision.Solution;
using ColorVision.Solution.Searches;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Configs;
using ColorVision.UI.HotKey;
using ColorVision.UI.Menus;
using ColorVision.UI.Shell;
using ColorVision.UI.Views;
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
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
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
        public static MainWindowConfig Config => MainWindowConfig.Instance;

        public MainWindow()
        {
            InitializeComponent();
            Config.SetWindow(this);
            SizeChanged += (s, e) => Config.SetConfig(this);
            var IsAdministrator = Tool.IsAdministrator();
            Title += $"- {(IsAdministrator ? Properties.Resources.RunAsAdmin : Properties.Resources.NotRunAsAdmin)}";
            this.ApplyCaption();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            MenuManager.GetInstance().Menu = Menu1;
            Config.IsOpenSidebar = true;
            this.DataContext = Config;
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

            Debug.WriteLine(Properties.Resources.LaunchSuccess);

            Task.Run(CheckCertificate);
            SolutionTab1.Content = new TreeViewControl();
            PluginLoader.LoadPlugins("Plugins");
            PluginLoader.LoadAssembly<IPlugin>(Assembly.GetExecutingAssembly());
            MenuManager.GetInstance().LoadMenuItemFromAssembly();
            this.LoadHotKeyFromAssembly();

            QuartzSchedulerManager.GetInstance();
            Application.Current.MainWindow = this;
            //LoadIMainWindowInitialized();
            if (Config.OpenFloatingBall)
                new FloatingBallWindow().Show();
        }

        public static async void LoadIMainWindowInitialized() 
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (Type type in assembly.GetTypes().Where(t => typeof(IMainWindowInitialized).IsAssignableFrom(t) && !t.IsAbstract))
                {
                    if (Activator.CreateInstance(type) is IMainWindowInitialized componentInitialize)
                    {
                        try
                        {
                            await componentInitialize.Initialize();
                        }
                        catch (Exception ex)
                        {
                            log.Error(ex);
                        }
                    }
                }
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
                    MenuItem menuItem = new() { Header = Properties.Resources.InstallCertificate };
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
                            DisPlayManagerConfig.Instance.PlayControls[disPlayControl.DisPlayName] = i;
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


        private void Login_Click(object sender, RoutedEventArgs e)
        {
            new UserInfoWindow() { }.ShowDialog();
        }

        private void StatusBarGrid_Initialized(object sender, EventArgs e)
        {
            ContextMenu contextMenu= new ContextMenu();
            StatusBarGrid.ContextMenu = contextMenu;


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
                        statusBarItem.SetBinding(VisibilityProperty, visibilityBinding);
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

                    MenuItem menuItem = new MenuItem() { Header = statusBarIconMetadata.Name };
                    menuItem.Click += (s, e) => menuItem.IsChecked = !menuItem.IsChecked;
                    menuItem.DataContext = statusBarIconMetadata.Source;
                    // 绑定 MenuItem 的 IsChecked 属性到 VisibilityBindingName
                    if (statusBarIconMetadata.VisibilityBindingName != null)
                    {
                        var isCheckedBinding1 = new Binding(statusBarIconMetadata.VisibilityBindingName)
                        {
                            Mode = BindingMode.TwoWay,
                        };
                        menuItem.SetBinding(MenuItem.IsCheckedProperty, isCheckedBinding1);
                    }
                    contextMenu.Items.Add(menuItem);
                }
                else if (statusBarIconMetadata.Type == StatusBarType.Text)
                {
                    StatusBarItem statusBarItem = new StatusBarItem();
                    statusBarItem.DataContext = statusBarIconMetadata.Source;

                    var Binding = new Binding(statusBarIconMetadata.BindingName) { Mode = BindingMode.OneWay };
                    statusBarItem.SetBinding(ContentProperty, Binding);


                    if (statusBarIconMetadata.VisibilityBindingName != null)
                    {
                        var visibilityBinding = new Binding(statusBarIconMetadata.VisibilityBindingName)
                        {
                            Converter = (IValueConverter)Application.Current.FindResource("bool2VisibilityConverter")
                        };
                        statusBarItem.SetBinding(VisibilityProperty, visibilityBinding);
                    }

                    StatusBarTextDocker.Children.Add(statusBarItem);

                    MenuItem menuItem = new MenuItem() { Header = statusBarIconMetadata.Name };
                    menuItem.Click += (s, e) => menuItem.IsChecked = !menuItem.IsChecked;
                    menuItem.DataContext = statusBarIconMetadata.Source;
                    // 绑定 MenuItem 的 IsChecked 属性到 VisibilityBindingName
                    if (statusBarIconMetadata.VisibilityBindingName != null)
                    {
                        var isCheckedBinding = new Binding(statusBarIconMetadata.VisibilityBindingName)
                        {
                            Mode = BindingMode.TwoWay,
                        };
                        menuItem.SetBinding(MenuItem.IsCheckedProperty, isCheckedBinding);
                    }
                    contextMenu.Items.Add(menuItem);
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
