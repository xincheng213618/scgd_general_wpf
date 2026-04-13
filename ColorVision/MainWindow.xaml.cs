using ColorVision.Common.Utilities;
using ColorVision.Solution;
using ColorVision.Solution.Editor;
using ColorVision.Solution.Workspace;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.HotKey;
using ColorVision.UI.LogImp;
using ColorVision.UI.Menus;
using ColorVision.UI.Shell;
using ColorVision.UI.Views;
using AvalonDock.Layout;
using log4net;
using Microsoft.Xaml.Behaviors;
using Microsoft.Xaml.Behaviors.Layout;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision
{


        /// <summary>
        /// Interaction logic for MarkdownViewWindow.xaml
        /// </summary>
        /// 
    public partial class MainWindow : Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MainWindow));
        public DockViewManager DockViewManager => DockViewManager.GetInstance();
        public static MainWindowConfig Config => MainWindowConfig.Instance;

        public MainWindow()
        {
            InitializeComponent();
            Config.SetWindow(this);

            var IsAdministrator = Tool.IsAdministrator();
            //Title += $"- {(IsAdministrator ? Properties.Resources.RunAsAdmin : Properties.Resources.NotRunAsAdmin)}";
            Title = "ColorVision";
            this.ApplyCaption();
            this.SetWindowFull(Config);
            
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            this.SizeChanged += (s, e) =>
            {
                SearchControl1.Visibility = this.ActualWidth < 700 ? Visibility.Collapsed : Visibility.Visible;
                RightMenuItemPanel.Visibility = this.ActualWidth < Menu1.ActualWidth + RightMenuItemPanel.ActualWidth + 100 ? Visibility.Collapsed : Visibility.Visible;
            };

            this.DataContext = Config;

            // 初始化 AvalonDock 主题
            void ApplyAvalonDockTheme(Theme theme)
            {
                // 先设置为 null 以强制 AvalonDock 重新加载资源
                DockingManager1.Theme = null;
                if (theme == Theme.Dark)
                    DockingManager1.Theme = new AvalonDock.Themes.Vs2013DarkTheme();
                else
                    DockingManager1.Theme = new AvalonDock.Themes.Vs2013LightTheme();
            }
            ThemeManager.Current.CurrentUIThemeChanged += ApplyAvalonDockTheme;

            // 设置 WorkspaceManager 指向当前的 DockingManager
            WorkspaceManager.layoutRoot = _layoutRoot;
            WorkspaceManager.LayoutDocumentPane = LayoutDocumentPane;


            // 初始化停靠面板管理器
            var layoutManager = new DockLayoutManager(DockingManager1);
            layoutManager.RegisterPanel("ProjectPanel", ProjectPanelGrid, Properties.Resources.SolutionExplorer, PanelPosition.Left);
            layoutManager.RegisterPanel("AcquirePanel", StackPanelSPD.Parent, Properties.Resources.DeviceControl, PanelPosition.Left);

            var logOutput = new LogOutput("%date{HH:mm:ss} [%thread] %-5level %message%newline");
            layoutManager.RegisterPanel("LogPanel", logOutput, Properties.Resources.Log, PanelPosition.Bottom);
            WorkspaceManager.LayoutManager = layoutManager;

            // 扫描并注册所有 IDockPanelProvider 提供的面板（在 LoadLayout 之前确保所有面板正确注册）
            foreach (var provider in AssemblyHandler.GetInstance().GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .Where(t => typeof(IDockPanelProvider).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface)
                .Select(t =>
                {
                    try { return Activator.CreateInstance(t) as IDockPanelProvider; }
                    catch (Exception ex) { log.Debug($"Failed to instantiate IDockPanelProvider {t.Name}: {ex.Message}"); return null; }
                })
                .Where(p => p != null)
                .OrderBy(p => p!.Order))
            {
                try
                {
                    provider!.RegisterPanels();
                }
                catch (Exception ex)
                {
                    log.Warn($"IDockPanelProvider {provider!.GetType().Name} failed: {ex.Message}");
                }
            }

            // 初始化 DockViewManagerHost，注册 AvalonDock 回调等
            DockViewManagerHost.Initialize();

            // 初始化解决方案项目面板
            ProjectPanelGrid.Children.Add(new TreeViewControl());

            // 初始化显示控件管理器
            DisPlayManager.GetInstance().Init(this, StackPanelSPD);

            Debug.WriteLine(Properties.Resources.LaunchSuccess);

            // 加载已保存的布局
            layoutManager.LoadLayout();

            // 重新应用主题以修复 AvalonDock 问题，确保所有切换元素使用正确的主题
            ApplyAvalonDockTheme(ThemeManager.Current.CurrentUITheme);

            // 将所有已注册的视图显示为文档标签页
            DockViewManager.ShowAllViews();

            // 切换到 DeviceControl 面板时，跳转回上次显示的视图
            HookAcquirePanelActivation();

            // 执行延迟加载的操作
            foreach (var action in WorkspaceManager.DealyLoad)
            {
                action();
            }
            WorkspaceManager.DealyLoad.Clear();

            // 更新后首次打开时显示更新日志
            ShowChangelogIfUpdated();

            // Ctrl+W 关闭当前活动的文档
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Close, (s, e) =>
            {
                var doc = WorkspaceManager.FindDocumentActive(WorkspaceManager.LayoutDocumentPane);
                doc?.Close();
            }));
            InputBindings.Add(new KeyBinding(ApplicationCommands.Close, new KeyGesture(Key.W, ModifierKeys.Control)));

            MenuManager.GetInstance().LoadMenuForWindow(MenuItemConstants.MainWindowTarget,Menu1);
            this.LoadHotKeyFromAssembly();
            StatusBarManager.GetInstance().Init(StatusBarGrid, MenuItemConstants.MainWindowTarget);

            // 监听 DockingManager 活动文档切换和状态变化，更新视图管理器并通知视图变更
            DockingManager1.ActiveContentChanged += (s, e) =>
            {
                StatusBarManager.GetInstance().OnActiveDocumentChanged(DockingManager1.ActiveContent);

                var viewManager = DockViewManager.GetInstance();
                var activeControl = DockingManager1.ActiveContent as System.Windows.Controls.Control;
                var activeView = activeControl != null && viewManager.Views.Contains(activeControl) ? activeControl : null;
                viewManager.RaiseActiveViewChanged(activeView);
            };

            Application.Current.MainWindow = this;
            Task.Run(() =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    LoadIMainWindowInitialized();

                    FluidMoveBehavior fluidMoveBehavior = new()
                    {
                        AppliesTo = FluidMoveScope.Children,
                        Duration = TimeSpan.FromSeconds(0.1)
                    };
                    Interaction.GetBehaviors(StackPanelSPD).Add(fluidMoveBehavior);
                });
            });
            ProgramTimer.StopAndReport();

            // 设置快捷键 Ctrl + F
            var gesture = new KeyGesture(Key.F, ModifierKeys.Control);
            var command = new RoutedCommand();
            command.InputGestures.Add(gesture);
            CommandBindings.Add(new CommandBinding(command, FocusSearchBox));
            InitRightMenuItemPanel();

            StartupRegistryChecker.Clear();

            this.AllowDrop = true;
            this.Drop += MainWindow_Drop;

            // 窗口关闭时自动保存布局
            this.Closing += (s, e) =>
            {
                WorkspaceManager.LayoutManager?.SaveLayout();
            };
        }

        /// <summary>
        /// 当 AcquirePanel (DeviceControl) 变为活动状态时
        /// 自动跳转到上次显示的视图，实现临时切换后恢复
        /// </summary>
        private void HookAcquirePanelActivation()
        {
            var acquirePanel = DockingManager1.Layout.Descendents()
                .OfType<AvalonDock.Layout.LayoutAnchorable>()
                .FirstOrDefault(a => a.ContentId == "AcquirePanel");
            if (acquirePanel != null)
            {
                acquirePanel.IsActiveChanged += (s, e) =>
                {
                    if (acquirePanel.IsActive)
                        DockViewManager.ActivateLastView();
                };
            }
        }

        /// <summary>
        /// 更新后首次打开时显示更新日志。
        /// 对比当前版本与上次记录的版本，若版本变更则显示 CHANGELOG.md。
        /// </summary>
        private void ShowChangelogIfUpdated()
        {
            try
            {
                string currentVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString();
                if (string.IsNullOrEmpty(currentVersion)) return;

                if (Config.LastOpenedVersion != currentVersion)
                {
                    string changelogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CHANGELOG.md");
                    if (File.Exists(changelogPath))
                    {
                        var editor = new WebView2Editor();
                        editor.Open(changelogPath);
                    }

                    Config.LastOpenedVersion = currentVersion;
                }
            }
            catch (Exception ex)
            {
                log.Warn("显示更新日志失败", ex);
            }
        }

        private void MainWindow_Drop(object sender, DragEventArgs e)
        {
            var b = e.Data.GetDataPresent(DataFormats.FileDrop);
            if (b)
            {
                var sarr = e.Data.GetData(DataFormats.FileDrop);
                var a = sarr as string[];
                var fn = a?.First();

                if (File.Exists(fn))
                { 
                    FileProcessorFactory.GetInstance().HandleFile(fn);
                    e.Handled = true;
                }
                else if (Directory.Exists(fn))
                {
                    DirectoryInfo directoryInfo = new DirectoryInfo(fn);
                }
            }
        }

        private void InitRightMenuItemPanel()
        {
            var allSettings = new List<MenuItemMetadata>();
            foreach (var item in AssemblyService.Instance.LoadImplementations<IRightMenuItemProvider>())
            {
                allSettings.AddRange(item.GetMenuItems());
            }
            allSettings.Sort((a, b) => a.Order.CompareTo(b.Order));
            foreach (var item in allSettings)
            {
                Button button = new Button
                {
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Width = 20,
                    Padding = new Thickness(0),
                    Margin = new Thickness(0, 0, 5, 0)
                };
                button.Content = item.Icon;
                button.Command = item.Command;
                RightMenuItemPanel.Children.Add(button);
            }
        }


        private void FocusSearchBox(object sender, ExecutedRoutedEventArgs e)
        {
            SearchControl1.FocusSearchBox();
        }

        public static async void LoadIMainWindowInitialized() 
        {
            List<IMainWindowInitialized> IMainWindowInitializeds = new List<IMainWindowInitialized>();
            foreach (var assembly in AssemblyHandler.GetInstance().GetAssemblies())
            {
                foreach (Type type in assembly.GetTypes().Where(t => typeof(IMainWindowInitialized).IsAssignableFrom(t) && !t.IsAbstract))
                {
                    if (Activator.CreateInstance(type) is IMainWindowInitialized componentInitialize)
                    {
                        IMainWindowInitializeds.Add(componentInitialize);
  
                    }
                }
            }
            foreach (var componentInitialize in IMainWindowInitializeds.OrderBy(a => a.Order))
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
