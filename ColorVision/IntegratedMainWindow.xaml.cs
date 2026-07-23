#pragma warning disable CA1822,CS8602
using AvalonDock.Layout;
using AvalonDock.Themes.VS2013.Themes;
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
using log4net;
using Microsoft.Xaml.Behaviors;
using Microsoft.Xaml.Behaviors.Layout;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;
using System.Windows.Threading;

namespace ColorVision
{
    public partial class IntegratedMainWindow : Window
    {
        private const double TopChromeCaptionButtonsWidth = 142;
        private const double TopChromeSearchReservedWidth = 320;
        private const double TopChromeSpacingReservedWidth = 80;
        private const double TopChromeButtonFallbackWidth = 32;
        private const double TopChromeRightMenuGlyphFontSize = 15;
        private const double TopChromeUpdateNotificationFallbackWidth = 76;

        private bool _topChromeVisibilityUpdateQueued;
        private WindowChrome? _integratedWindowChrome;

        private static readonly ILog log = LogManager.GetLogger(typeof(IntegratedMainWindow));
        public DockViewManager DockViewManager => DockViewManager.GetInstance();
        public static MainWindowConfig Config => MainWindowConfig.Instance;

        public IntegratedMainWindow()
        {
            InitializeComponent();
            _integratedWindowChrome = CreateIntegratedWindowChrome();
            InitializeWindowChromeCommands();
            StateChanged += (_, _) =>
            {
                UpdateWindowCommandButtonState();
                ApplyMainWindowDwmAttributes();
            };
            Activated += (_, _) => ApplyMainWindowDwmAttributes();
            Deactivated += (_, _) => ApplyMainWindowDwmAttributes();
            Loaded += (_, _) => ApplyMainWindowDwmAttributes();
            Config.SetWindow(this);

            Title = "ColorVision";
            this.ApplyCaption();
            this.SetWindowFull(Config);
            ApplyIntegratedMainWindowShell();
            UpdateWindowCommandButtonState();
            HookUpdateNotification();
        }

        private static WindowChrome CreateIntegratedWindowChrome()
        {
            return new WindowChrome
            {
                CaptionHeight = 40,
                CornerRadius = new CornerRadius(0),
                GlassFrameThickness = new Thickness(0),
                ResizeBorderThickness = new Thickness(6),
                UseAeroCaptionButtons = false,
            };
        }

        private void InitializeWindowChromeCommands()
        {
            CommandBindings.Add(new CommandBinding(SystemCommands.CloseWindowCommand, (_, _) => Close()));
            CommandBindings.Add(new CommandBinding(
                SystemCommands.MaximizeWindowCommand,
                (_, _) => SystemCommands.MaximizeWindow(this),
                (_, e) => e.CanExecute = CanResizeWindow()));
            CommandBindings.Add(new CommandBinding(
                SystemCommands.RestoreWindowCommand,
                (_, _) => SystemCommands.RestoreWindow(this),
                (_, e) => e.CanExecute = CanResizeWindow()));
            CommandBindings.Add(new CommandBinding(
                SystemCommands.MinimizeWindowCommand,
                (_, _) => SystemCommands.MinimizeWindow(this),
                (_, e) => e.CanExecute = ResizeMode != ResizeMode.NoResize));
        }

        private bool CanResizeWindow() => ResizeMode == ResizeMode.CanResize || ResizeMode == ResizeMode.CanResizeWithGrip;

        private void UpdateWindowCommandButtonState()
        {
            if (MaximizeButton == null || RestoreButton == null || MinimizeButton == null || CloseButton == null)
                return;

            bool canResize = CanResizeWindow();
            bool isMaximized = WindowState == WindowState.Maximized;
            MinimizeButton.Visibility = ResizeMode == ResizeMode.NoResize ? Visibility.Collapsed : Visibility.Visible;
            MaximizeButton.Visibility = canResize && !isMaximized ? Visibility.Visible : Visibility.Collapsed;
            RestoreButton.Visibility = canResize && isMaximized ? Visibility.Visible : Visibility.Collapsed;
            CloseButton.Visibility = Visibility.Visible;
        }

        private void ApplyIntegratedMainWindowShell()
        {
            _integratedWindowChrome ??= CreateIntegratedWindowChrome();
            WindowChrome.SetWindowChrome(this, _integratedWindowChrome);

            SearchHostGrid.HorizontalAlignment = HorizontalAlignment.Stretch;
            SearchHostGrid.Margin = new Thickness(18, 0, 18, 0);
            SearchControl1.HorizontalAlignment = HorizontalAlignment.Center;
            SearchControl1.MinWidth = 260;
            SearchControl1.MaxWidth = 360;
            TopChromeBar.BorderThickness = new Thickness(0, 0, 0, 1);
            WindowFrameBorder.BorderThickness = new Thickness(1);
            DockingManager1.Margin = new Thickness(0);

            UpdateWindowCommandButtonState();
            SetMaximizeRestoreButtonChromeState(false, false);
            ApplyMainWindowDwmAttributes();
            QueueTopChromeVisibilityUpdate();
        }

        private void ApplyAvalonDockIdeThemeOverrides()
        {
            if (DockingManager1 == null)
                return;

            Brush background = GetBrushResource("GlobalBackground");
            Brush panelBackground = GetBrushResource("GlobalBorderBrush1");
            Brush border = GetBrushResource("GlobalBorderBrush");
            Brush text = GetBrushResource("GlobalTextBrush");
            Brush hover = GetBrushResource("ButtonBorderBrush");
            Brush accent = GetBrushResource("StatusBarBackgroundBrush");
            Color accentColor = GetBrushColor(accent);

            var resources = DockingManager1.Resources;
            resources[ResourceKeys.Background] = background;
            resources[ResourceKeys.TabBackground] = background;
            resources[ResourceKeys.PanelBorderBrush] = border;
            resources[ResourceKeys.ControlAccentBrushKey] = accent;
            resources[ResourceKeys.ControlAccentColorKey] = accentColor;

            resources[ResourceKeys.ToolWindowCaptionActiveBackground] = panelBackground;
            resources[ResourceKeys.ToolWindowCaptionInactiveBackground] = background;
            resources[ResourceKeys.ToolWindowCaptionActiveText] = text;
            resources[ResourceKeys.ToolWindowCaptionInactiveText] = text;
            resources[ResourceKeys.ToolWindowCaptionActiveGrip] = border;
            resources[ResourceKeys.ToolWindowCaptionInactiveGrip] = border;

            resources[ResourceKeys.ToolWindowTabSelectedActiveBackground] = panelBackground;
            resources[ResourceKeys.ToolWindowTabSelectedInactiveBackground] = panelBackground;
            resources[ResourceKeys.ToolWindowTabUnselectedBackground] = background;
            resources[ResourceKeys.ToolWindowTabUnselectedHoveredBackground] = hover;
            resources[ResourceKeys.ToolWindowTabSelectedActiveText] = text;
            resources[ResourceKeys.ToolWindowTabSelectedInactiveText] = text;
            resources[ResourceKeys.ToolWindowTabUnselectedText] = text;
            resources[ResourceKeys.ToolWindowTabUnselectedHoveredText] = text;

            resources[ResourceKeys.DocumentWellTabSelectedActiveBackground] = panelBackground;
            resources[ResourceKeys.DocumentWellTabSelectedInactiveBackground] = panelBackground;
            resources[ResourceKeys.DocumentWellTabUnselectedBackground] = background;
            resources[ResourceKeys.DocumentWellTabUnselectedHoveredBackground] = hover;
            resources[ResourceKeys.DocumentWellTabSelectedActiveText] = text;
            resources[ResourceKeys.DocumentWellTabSelectedInactiveText] = text;
            resources[ResourceKeys.DocumentWellTabUnselectedText] = text;
            resources[ResourceKeys.DocumentWellTabUnselectedHoveredText] = text;
        }

        private Brush GetBrushResource(string resourceKey)
        {
            return TryFindResource(resourceKey) as Brush ?? Brushes.Transparent;
        }

        private static Color GetBrushColor(Brush brush)
        {
            return brush is SolidColorBrush solidColorBrush
                ? solidColorBrush.Color
                : Colors.Transparent;
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            SizeChanged += (_, _) => QueueTopChromeVisibilityUpdate();

            DataContext = Config;

            void ApplyAvalonDockTheme(Theme theme)
            {
                DockingManager1.Theme = null;
                DockingManager1.Theme = theme == Theme.Dark
                    ? new AvalonDock.Themes.Vs2013DarkTheme()
                    : new AvalonDock.Themes.Vs2013LightTheme();

                ApplyAvalonDockIdeThemeOverrides();
            }

            ThemeManager.Current.CurrentUIThemeChanged += ApplyAvalonDockTheme;

            WorkspaceManager.layoutRoot = _layoutRoot;
            WorkspaceManager.LayoutDocumentPane = LayoutDocumentPane;

            var layoutManager = new DockLayoutManager(DockingManager1);
            layoutManager.RegisterPanel("ProjectPanel", ProjectPanelGrid, Properties.Resources.SolutionExplorer, PanelPosition.Left);
            layoutManager.RegisterPanel("AcquirePanel", StackPanelSPD.Parent, Properties.Resources.DeviceControl, PanelPosition.Left);

            var logOutput = new LogOutput("%date{HH:mm:ss} [%thread] %-5level %message%newline");
            layoutManager.RegisterPanel("LogPanel", logOutput, Properties.Resources.Log, PanelPosition.Bottom);
            WorkspaceManager.LayoutManager = layoutManager;

            foreach (var provider in AssemblyHandler.GetInstance().LoadImplementations<IDockPanelProvider>()
                .OrderBy(p => p.Order))
            {
                Stopwatch providerStopwatch = Stopwatch.StartNew();
                try
                {
                    provider!.RegisterPanels();
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

            DockViewManagerHost.Initialize();
            ProjectPanelGrid.Children.Add(new TreeViewControl());
            DisPlayManager.GetInstance().Init(this, StackPanelSPD);
            Debug.WriteLine(Properties.Resources.LaunchSuccess);

            if (!layoutManager.LoadLayout())
                layoutManager.ResetLayout();
            ApplyAvalonDockTheme(ThemeManager.Current.CurrentUITheme);
            DockViewManager.ShowAllViews();
            HookTerminalPanelActivation();

            foreach (var action in WorkspaceManager.DealyLoad)
            {
                action();
            }
            WorkspaceManager.DealyLoad.Clear();

            ShowChangelogIfUpdated();

            CommandBindings.Add(new CommandBinding(ApplicationCommands.Close, (_, _) =>
            {
                var doc = WorkspaceManager.FindDocumentActive(WorkspaceManager.LayoutDocumentPane);
                doc?.Close();
            }));
            InputBindings.Add(new KeyBinding(ApplicationCommands.Close, new KeyGesture(Key.W, ModifierKeys.Control)));

            MenuManager.GetInstance().LoadMenuForWindow(MenuItemConstants.MainWindowTarget, Menu1);
            this.LoadHotKeyFromAssembly();

            DockingManager1.ActiveContentChanged += (_, _) =>
            {
                StatusBarManager.GetInstance().OnActiveDocumentChanged(DockingManager1.ActiveContent);

                var viewManager = DockViewManager.GetInstance();
                var activeControl = DockingManager1.ActiveContent as Control;
                var activeView = activeControl != null && viewManager.Views.Contains(activeControl) ? activeControl : null;
                viewManager.RaiseActiveViewChanged(activeView);
            };

            Application.Current.MainWindow = this;
            ContentRendered += IntegratedMainWindow_ContentRendered;
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

            var gesture = new KeyGesture(Key.F, ModifierKeys.Control);
            var command = new RoutedCommand();
            command.InputGestures.Add(gesture);
            CommandBindings.Add(new CommandBinding(command, FocusSearchBox));

            InitRightMenuItemPanel();
            QueueTopChromeVisibilityUpdate();

            StartupRegistryChecker.Clear();

            AllowDrop = true;
            Drop += MainWindow_Drop;

            Closing += (_, e) =>
            {
                if (!EditorDocumentService.TryCloseAllDocuments())
                {
                    e.Cancel = true;
                    return;
                }
                WorkspaceManager.LayoutManager?.SaveLayout();
            };
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

        private void HookUpdateNotification()
        {
            UpdateUpdateNotificationButton();
            CombinedUpdateCoordinator.PendingStartupUpdateChanged += CombinedUpdateCoordinator_PendingStartupUpdateChanged;
            Closed += (_, _) => CombinedUpdateCoordinator.PendingStartupUpdateChanged -= CombinedUpdateCoordinator_PendingStartupUpdateChanged;
        }

        private void CombinedUpdateCoordinator_PendingStartupUpdateChanged(object? sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                UpdateUpdateNotificationButton();
                QueueTopChromeVisibilityUpdate();
            }));
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
                QueueTopChromeVisibilityUpdate();
            }
        }

        private void ShowChangelogIfUpdated()
        {
            try
            {
                string currentVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString();
                if (string.IsNullOrEmpty(currentVersion))
                    return;

                if (Config.LastOpenedVersion != currentVersion)
                {
                    ChangelogPage.Open();
                    Config.LastOpenedVersion = currentVersion;
                }
            }
            catch (Exception ex)
            {
                log.Warn("显示更新日志失败", ex);
            }
        }

        private async void MainWindow_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
                return;

            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files is not { Length: > 0 })
                return;

            e.Handled = true;
            await ResourceOpenService.Instance.TryOpenManyWithFeedbackAsync(files, this);
        }

        private void InitRightMenuItemPanel()
        {
            var topChromeButtonStyle = TryFindResource("TopChromeButtonStyle") as Style;
            var allSettings = new List<MenuItemMetadata>();
            foreach (var item in AssemblyService.Instance.LoadImplementations<IRightMenuItemProvider>())
            {
                allSettings.AddRange(item.GetMenuItems());
            }
            allSettings.Sort((a, b) => a.Order.CompareTo(b.Order));

            foreach (var item in allSettings)
            {
                Button button = new()
                {
                    Style = topChromeButtonStyle,
                };
                WindowChrome.SetIsHitTestVisibleInChrome(button, true);
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
                    textBlock.FontSize = TopChromeRightMenuGlyphFontSize;
                    textBlock.HorizontalAlignment = HorizontalAlignment.Center;
                    textBlock.VerticalAlignment = VerticalAlignment.Center;
                    textBlock.TextAlignment = TextAlignment.Center;
                    textBlock.SetResourceReference(TextBlock.ForegroundProperty, "GlobalTextBrush");
                    break;
                case Image image:
                    image.Width = TopChromeRightMenuGlyphFontSize;
                    image.Height = TopChromeRightMenuGlyphFontSize;
                    image.Stretch = Stretch.Uniform;
                    break;
                case Viewbox viewbox:
                    viewbox.Width = TopChromeRightMenuGlyphFontSize;
                    viewbox.Height = TopChromeRightMenuGlyphFontSize;
                    break;
            }

            return icon;
        }

        private void UpdateTopChromeVisibility()
        {
            _topChromeVisibilityUpdateQueued = false;
            double menuWidth = Menu1.ActualWidth > 0 ? Menu1.ActualWidth : Menu1.DesiredSize.Width;
            double rightPanelWidth = GetRightMenuItemPanelWidth();
            double fixedChromeWidth = GetVisibleElementWidth(ApplicationIconButton)
                                      + menuWidth
                                      + rightPanelWidth
                                      + GetWindowCommandButtonsWidth()
                                      + TopChromeSpacingReservedWidth;
            double updateNotificationWidth = GetVisibleElementWidth(UpdateNotificationButton);
            if (UpdateNotificationButton.Visibility == Visibility.Visible && updateNotificationWidth <= 0)
                updateNotificationWidth = TopChromeUpdateNotificationFallbackWidth;

            SetVisibilityIfChanged(
                SearchControl1,
                ActualWidth < fixedChromeWidth + TopChromeSearchReservedWidth + updateNotificationWidth
                    ? Visibility.Collapsed
                    : Visibility.Visible);

            SetVisibilityIfChanged(
                RightMenuItemPanel,
                ActualWidth < fixedChromeWidth
                    ? Visibility.Collapsed
                    : Visibility.Visible);
        }

        private void QueueTopChromeVisibilityUpdate()
        {
            if (_topChromeVisibilityUpdateQueued)
                return;

            _topChromeVisibilityUpdateQueued = true;
            Dispatcher.BeginInvoke(UpdateTopChromeVisibility, DispatcherPriority.Background);
        }

        private static void SetVisibilityIfChanged(UIElement element, Visibility visibility)
        {
            if (element.Visibility != visibility)
                element.Visibility = visibility;
        }

        private double GetRightMenuItemPanelWidth()
        {
            double width = 0;
            foreach (FrameworkElement child in RightMenuItemPanel.Children.OfType<FrameworkElement>())
            {
                double childWidth = child.ActualWidth > 0 ? child.ActualWidth : child.Width;
                if (double.IsNaN(childWidth) || childWidth <= 0)
                {
                    childWidth = child.DesiredSize.Width;
                }
                if (double.IsNaN(childWidth) || childWidth <= 0)
                {
                    childWidth = TopChromeButtonFallbackWidth;
                }

                width += childWidth + child.Margin.Left + child.Margin.Right;
            }

            return width;
        }

        private double GetWindowCommandButtonsWidth()
        {
            if (WindowCommandButtonsPanel.Visibility != Visibility.Visible)
                return 0;

            double width = WindowCommandButtonsPanel.ActualWidth > 0
                ? WindowCommandButtonsPanel.ActualWidth
                : WindowCommandButtonsPanel.DesiredSize.Width;

            return double.IsNaN(width) || width <= 0 ? TopChromeCaptionButtonsWidth : width;
        }

        private static double GetVisibleElementWidth(FrameworkElement element)
        {
            if (element.Visibility != Visibility.Visible)
                return 0;

            double width = element.ActualWidth > 0 ? element.ActualWidth : element.DesiredSize.Width;
            if (double.IsNaN(width) || width <= 0)
            {
                width = element.Width;
            }

            return double.IsNaN(width) || width <= 0 ? 0 : width + element.Margin.Left + element.Margin.Right;
        }

        private void ApplicationIconButton_Click(object sender, RoutedEventArgs e)
        {
            Point menuLocation = ApplicationIconButton.PointToScreen(new Point(0, ApplicationIconButton.ActualHeight));
            SystemCommands.ShowSystemMenu(this, menuLocation);
        }

        private void FocusSearchBox(object sender, ExecutedRoutedEventArgs e)
        {
            SearchControl1.FocusSearchBox();
        }

        private void IntegratedMainWindow_ContentRendered(object? sender, EventArgs e)
        {
            ContentRendered -= IntegratedMainWindow_ContentRendered;
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
