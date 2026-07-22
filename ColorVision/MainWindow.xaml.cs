#pragma warning disable CA1822
using ColorVision.Common.Utilities;
using ColorVision.Solution;
using ColorVision.Solution.Editor;
using ColorVision.Solution.Workspace;
using ColorVision.Themes;
using ColorVision.Update;
using ColorVision.UI;
using ColorVision.UI.HotKey;
using ColorVision.UI.LogImp;
using ColorVision.UI.Menus;
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
using System.Windows.Threading;

namespace ColorVision
{


        /// <summary>
        /// Interaction logic for MarkdownViewWindow.xaml
        /// </summary>
        /// 
    public partial class MainWindow : Window
    {
        private const double RightMenuGlyphFontSize = 15;

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
            HookUpdateNotification();
            
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            Stopwatch initializationStopwatch = Stopwatch.StartNew();
            Stopwatch phaseStopwatch = Stopwatch.StartNew();
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
            foreach (var provider in AssemblyHandler.GetInstance().LoadImplementations<IDockPanelProvider>()
                .OrderBy(p => p.Order))
            {
                Stopwatch providerStopwatch = Stopwatch.StartNew();
                try
                {
                    provider.RegisterPanels();
                }
                catch (Exception ex)
                {
                    log.Warn($"IDockPanelProvider {provider.GetType().Name} failed: {ex.Message}");
                }
                finally
                {
                    providerStopwatch.Stop();
                    log.Info($"Dock panel provider {provider.GetType().Name} took {providerStopwatch.ElapsedMilliseconds} ms.");
                }
            }
            log.Info($"Main window dock panel registration took {phaseStopwatch.ElapsedMilliseconds} ms.");

            // 初始化 DockViewManagerHost，注册 AvalonDock 回调等
            DockViewManagerHost.Initialize();

            // 初始化解决方案项目面板
            ProjectPanelGrid.Children.Add(new TreeViewControl());

            // 初始化显示控件管理器
            DisPlayManager.GetInstance().Init(this, StackPanelSPD);

            Debug.WriteLine(Properties.Resources.LaunchSuccess);

            // 加载已保存的布局
            phaseStopwatch.Restart();
            if (!layoutManager.LoadLayout())
                layoutManager.ResetLayout();
            log.Info($"Main window layout restore took {phaseStopwatch.ElapsedMilliseconds} ms.");

            // 重新应用主题以修复 AvalonDock 问题，确保所有切换元素使用正确的主题
            ApplyAvalonDockTheme(ThemeManager.Current.CurrentUITheme);

            // 将所有已注册的视图显示为文档标签页
            phaseStopwatch.Restart();
            DockViewManager.ShowAllViews();

            HookTerminalPanelActivation();

            // 执行延迟加载的操作
            foreach (var action in WorkspaceManager.DealyLoad)
            {
                action();
            }
            WorkspaceManager.DealyLoad.Clear();
            log.Info($"Main window view activation took {phaseStopwatch.ElapsedMilliseconds} ms.");

            // Ctrl+W 关闭当前活动的文档
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Close, (s, e) =>
            {
                var doc = WorkspaceManager.FindDocumentActive(WorkspaceManager.LayoutDocumentPane);
                doc?.Close();
            }));
            InputBindings.Add(new KeyBinding(ApplicationCommands.Close, new KeyGesture(Key.W, ModifierKeys.Control)));

            phaseStopwatch.Restart();
            MenuManager.GetInstance().LoadMenuForWindow(MenuItemConstants.MainWindowTarget, Menu1);
            log.Info($"Main window menu phase took {phaseStopwatch.ElapsedMilliseconds} ms.");
            phaseStopwatch.Restart();
            this.LoadHotKeyFromAssembly();
            log.Info($"Main window hotkey phase took {phaseStopwatch.ElapsedMilliseconds} ms.");

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
            ContentRendered += MainWindow_ContentRendered;
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                LoadIMainWindowInitialized();

                FluidMoveBehavior fluidMoveBehavior = new()
                {
                    AppliesTo = FluidMoveScope.Children,
                    Duration = TimeSpan.FromSeconds(0.1)
                };
                Interaction.GetBehaviors(StackPanelSPD).Add(fluidMoveBehavior);
            }));

            // 设置快捷键 Ctrl + F
            var gesture = new KeyGesture(Key.F, ModifierKeys.Control);
            var command = new RoutedCommand();
            command.InputGestures.Add(gesture);
            CommandBindings.Add(new CommandBinding(command, FocusSearchBox));
            phaseStopwatch.Restart();
            InitRightMenuItemPanel();
            log.Info($"Main window right-side menu phase took {phaseStopwatch.ElapsedMilliseconds} ms.");

            StartupRegistryChecker.Clear();

            this.AllowDrop = true;
            this.Drop += MainWindow_Drop;

            // 窗口关闭时自动保存布局
            this.Closing += (s, e) =>
            {
                if (!EditorDocumentService.TryCloseAllDocuments())
                {
                    e.Cancel = true;
                    return;
                }
                WorkspaceManager.LayoutManager?.SaveLayout();
            };
            initializationStopwatch.Stop();
            log.Info($"Main window initialized event completed in {initializationStopwatch.ElapsedMilliseconds} ms.");
        }

        private void HookTerminalPanelActivation()
        {
            var terminalPanel = DockingManager1.Layout.Descendents()
                .OfType<AvalonDock.Layout.LayoutAnchorable>()
                .FirstOrDefault(a => a.ContentId == ColorVision.Solution.Terminal.TerminalService.PanelId);
            if (terminalPanel == null)
                return;

            void ActivateTerminalPanel()
            {
                if (terminalPanel.IsActive && !terminalPanel.IsHidden)
                    ColorVision.Solution.Terminal.TerminalService.GetInstance().NotifyPanelActivated();
            }

            terminalPanel.IsActiveChanged += (_, _) => ActivateTerminalPanel();
            ActivateTerminalPanel();
        }

        private async void MainWindow_Drop(object sender, DragEventArgs e)
        {
            var b = e.Data.GetDataPresent(DataFormats.FileDrop);
            if (b)
            {
                var sarr = e.Data.GetData(DataFormats.FileDrop);
                var a = sarr as string[];
                if (a is { Length: > 0 })
                {
                    e.Handled = true;
                    await ResourceOpenService.Instance.TryOpenManyWithFeedbackAsync(a, this);
                }
            }
        }

        private void HookUpdateNotification()
        {
            UpdateUpdateNotificationButton();
            CombinedUpdateCoordinator.PendingStartupUpdateChanged += CombinedUpdateCoordinator_PendingStartupUpdateChanged;
            Closed += (_, _) => CombinedUpdateCoordinator.PendingStartupUpdateChanged -= CombinedUpdateCoordinator_PendingStartupUpdateChanged;
        }

        private void CombinedUpdateCoordinator_PendingStartupUpdateChanged(object? sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(UpdateUpdateNotificationButton));
        }

        private void UpdateUpdateNotificationButton()
        {
            UpdateNotificationButton.Visibility = CombinedUpdateCoordinator.HasPendingStartupUpdate
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private async void UpdateNotificationButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateNotificationButton.IsEnabled = false;
            try
            {
                await CombinedUpdateCoordinator.StartPendingStartupUpdateAsync();
            }
            finally
            {
                UpdateNotificationButton.IsEnabled = true;
                UpdateUpdateNotificationButton();
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
                button.Content = NormalizeRightMenuIcon(item.Icon);
                button.Command = item.Command;
                RightMenuItemPanel.Children.Add(button);
            }
        }

        private static object? NormalizeRightMenuIcon(object? icon)
        {
            switch (icon)
            {
                case TextBlock textBlock:
                    textBlock.FontSize = RightMenuGlyphFontSize;
                    textBlock.HorizontalAlignment = HorizontalAlignment.Center;
                    textBlock.VerticalAlignment = VerticalAlignment.Center;
                    textBlock.TextAlignment = TextAlignment.Center;
                    textBlock.SetResourceReference(TextBlock.ForegroundProperty, "GlobalTextBrush");
                    break;
                case Image image:
                    image.Width = RightMenuGlyphFontSize;
                    image.Height = RightMenuGlyphFontSize;
                    image.Stretch = Stretch.Uniform;
                    break;
                case Viewbox viewbox:
                    viewbox.Width = RightMenuGlyphFontSize;
                    viewbox.Height = RightMenuGlyphFontSize;
                    break;
            }

            return icon;
        }


        private void FocusSearchBox(object sender, ExecutedRoutedEventArgs e)
        {
            SearchControl1.FocusSearchBox();
        }

        private void MainWindow_ContentRendered(object? sender, EventArgs e)
        {
            ContentRendered -= MainWindow_ContentRendered;
            ProgramTimer.StopAndReport();
            Update.ApplicationUpdateScanProtection.CompleteAfterUpdateRestart();
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                StatusBarManager.GetInstance().Init(StatusBarGrid, MenuItemConstants.MainWindowTarget);
                stopwatch.Stop();
                log.Info($"Main window status bar materialized in {stopwatch.ElapsedMilliseconds} ms after first render.");
            }), DispatcherPriority.Background);
        }

        public static async void LoadIMainWindowInitialized()
        {
            List<IMainWindowInitialized> initializers = AssemblyHandler.GetInstance().LoadImplementations<IMainWindowInitialized>();
            foreach (var componentInitialize in initializers.OrderBy(a => a.Order))
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                try
                {
                    await componentInitialize.Initialize();
                }
                catch (Exception ex)
                {
                    log.Error(ex);
                }
                finally
                {
                    stopwatch.Stop();
                    log.Info($"Main window initializer {componentInitialize.Name} took {stopwatch.ElapsedMilliseconds} ms.");
                }
            }
        }

    }
}
