#pragma warning disable CA1822,CS8625
using AvalonDock.Layout;
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.LogImp;
using ColorVision.UI.Menus;
using ColorVision.UI.Sorts;
using cvColorVision;
using log4net;
using ScottPlot;
using Spectrum.Data;
using Spectrum.Layout;
using Spectrum.Models;
using SpectrumResources = Spectrum.Properties.Resources;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO.Ports;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace Spectrum
{
    public class MenuSpectrumWindow : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.Tool;
        public override string Header => SpectrumResources.SpectrumWindowTitle;
        public override int Order => 1;
        public override void Execute()
        {
            new MainWindow().Show();
        }
    }


    public class MainWindowResult : ViewModelBase, IConfig
    {
        public static MainWindowResult Instance => ConfigService.Instance.GetRequiredService<MainWindowResult>();
    }

    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : System.Windows.Window,IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(App));
        private static readonly Lazy<Task<TimeSpan>> CvCameraResourceInitialization = new(() => Task.Run(() =>
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            log.Info("开始初始化 cvCamera 资源");
            cvCameraCSLib.InitResource(IntPtr.Zero, IntPtr.Zero);
            stopwatch.Stop();
            log.Info($"cvCamera 资源初始化完成，耗时 {stopwatch.ElapsedMilliseconds} ms");
            return stopwatch.Elapsed;
        }));
        private readonly Stopwatch startupStopwatch = Stopwatch.StartNew();
        private Task<ViewResultManager>? viewResultInitializationTask;
        private Task<string[]>? serialPortDiscoveryTask;
        private Task<TimeSpan>? cvCameraInitializationTask;
        private bool absoluteSpectrumPlotInitialized;
        public static SpectrometerManager Manager => SpectrometerManager.Instance;

        /// <summary>
        /// Static reference to current MainWindow instance for menu items access.
        /// </summary>
        internal static MainWindow? Instance { get; private set; }

        /// <summary>
        /// Layout manager for AvalonDock persistence, reset, and panel visibility.
        /// </summary>
        internal DockLayoutManager? LayoutManager { get; private set; }

        public static ViewResultManager ViewResultManager => ViewResultManager.GetInstance();

        public static ObservableCollection<ViewResultSpectrum> ViewResultSpectrums => ViewResultManager.ViewResluts;

        public static MainWindowConfig Config => MainWindowConfig.Instance;

        public MainWindow()
        {
            log.Info("开始创建主窗口");
            InitializeComponent();
            ContentRendered += Window_ContentRendered;
            Instance = this;
            Config.SetWindow(this);
            this.SizeChanged += (s, e) => Config.SetConfig(this);
            this.ApplyCaption();
            this.SetWindowFull(Config);
            this.Closing += (s, e) =>
            {
                // Auto-save layout when the window is closing
                LayoutManager?.SaveLayout();
            };
            this.Closed += (s, e) =>
            {
                CloseCieWindow();
                CleanupSmuTimedButtons();
                Manager.SmuController.Close();
                Manager.Disconnect();
                nativeLogOutput?.Dispose();
                Instance = null;
            };
            this.Title += " - " + Assembly.GetAssembly(typeof(MainWindow))?.GetName().Version?.ToString() ?? "";

            viewResultInitializationTask = Task.Run(() =>
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                ViewResultManager manager = ViewResultManager.GetInstance();
                stopwatch.Stop();
                log.Info($"历史结果初始化完成，数量 {manager.ViewResluts.Count}，耗时 {stopwatch.ElapsedMilliseconds} ms");
                return manager;
            });
            serialPortDiscoveryTask = Task.Run(SerialPort.GetPortNames);
            cvCameraInitializationTask = CvCameraResourceInitialization.Value;
            log.Info($"主窗口构造函数完成，耗时 {startupStopwatch.ElapsedMilliseconds} ms");
        }
        private LogOutput? logOutput;
        private LogLocalOutput? nativeLogOutput;

        private void Window_Initialized(object sender, EventArgs e)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            // AvalonDock theme integration
            void ThemeChange(Theme theme)
            {
                if (theme == Theme.Dark)
                    DockingManager.Theme = new AvalonDock.Themes.Vs2013DarkTheme();
                else
                    DockingManager.Theme = new AvalonDock.Themes.Vs2013LightTheme();
            }
            ThemeManager.Current.CurrentUIThemeChanged += ThemeChange;
            ThemeChange(ThemeManager.Current.CurrentUITheme);

            // Initialize layout manager and register all panel content
            LayoutManager = new DockLayoutManager(DockingManager);
            LayoutManager.RegisterContent("ControlPanel", ControlPanelPane.Content);
            LayoutManager.RegisterContent("SpectrumChart",
                _layoutRoot.Descendents().OfType<LayoutDocument>()
                    .First(d => d.ContentId == "SpectrumChart").Content);

            LayoutManager.RegisterContent("LogPanel", LogGrid);

            // Avoid reading a native log file before the first window render.
            ShowNativeLogPlaceholder();
            LayoutManager.RegisterContent("NativeLogPanel", NativeLogGrid);

            // Load saved layout if exists
            LayoutManager.LoadLayout();
            stopwatch.Stop();
            log.Info($"主窗口框架初始化完成，耗时 {stopwatch.ElapsedMilliseconds} ms");
        }

        private async void Window_ContentRendered(object? sender, EventArgs e)
        {
            ContentRendered -= Window_ContentRendered;
            log.Info($"主窗口首次内容已呈现，耗时 {startupStopwatch.ElapsedMilliseconds} ms");

            await Dispatcher.Yield(DispatcherPriority.Background);

            try
            {
                await InitializeDeferredWindowAsync();
            }
            catch (Exception ex)
            {
                log.Error("主窗口延后初始化失败", ex);
                if (!IsLoaded)
                {
                    return;
                }

                DockingManager.IsEnabled = true;
                ResourceInitializationProgress.IsIndeterminate = false;
                ResourceInitializationProgress.Value = 0;
                ResourceInitializationText.Text = SpectrumResources.CvCameraInitializationFailed + ex.GetBaseException().Message;
            }
        }

        private async Task InitializeDeferredWindowAsync()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            long phaseStarted = 0;
            List<string> phases = new();

            void MarkPhase(string name)
            {
                long elapsed = stopwatch.ElapsedMilliseconds;
                phases.Add($"{name}={elapsed - phaseStarted}ms");
                phaseStarted = elapsed;
            }

            SpectrometerManager manager = Manager;
            manager.AutodarkParam.ExecuteAdaptiveAutoDark = () => Button4_Click_1(null, null);

            ComboBoxSpectrometerType.ItemsSource = from e1 in Enum.GetValues<SpectrometerType>().Cast<SpectrometerType>()
                                                   select new KeyValuePair<SpectrometerType, string>(e1, e1.ToDescription());

            SetEmissionSP100Config.Instance.EditChanged += (s, e) =>
            {
                if (SpectrometerHandle == IntPtr.Zero)
                {
                    return;
                }

                log.Debug($"设置 SP100 参数: IsEnabled={SetEmissionSP100Config.Instance.IsEnabled}, nStartPos={SetEmissionSP100Config.Instance.nStartPos}, nEndPos={SetEmissionSP100Config.Instance.nEndPos}, dMeanThreshold={SetEmissionSP100Config.Instance.dMeanThreshold}");
                int ret = Spectrometer.CM_SetEmissionSP100(SpectrometerHandle, SetEmissionSP100Config.Instance.IsEnabled, SetEmissionSP100Config.Instance.nStartPos, SetEmissionSP100Config.Instance.nEndPos, SetEmissionSP100Config.Instance.dMeanThreshold);
                if (ret == 1)
                    log.Info("SP100 参数设置成功");
                else
                    log.Warn($"SP100 参数设置失败: {Spectrometer.GetErrorMessage(ret)}");
            };

            if (MainWindowConfig.Instance.LogControlVisibility)
            {
                logOutput = new LogOutput("%date{HH:mm:ss} [%thread] %-5level %message%newline");
                LogGrid.Children.Add(logOutput);
            }
            MarkPhase("模型与日志控件");

            await Dispatcher.Yield(DispatcherPriority.Background);
            MenuManager.GetInstance().LoadMenuForWindow("Spectrum", menu);
            MarkPhase("菜单");

            await Dispatcher.Yield(DispatcherPriority.Background);
            StatusBarManager.GetInstance().Init(StatusBarGrid, "Spectrum");
            MarkPhase("状态栏");

            string[] portNames;
            try
            {
                portNames = await (serialPortDiscoveryTask ?? Task.Run(SerialPort.GetPortNames));
            }
            catch (Exception ex)
            {
                log.Warn("串口枚举失败", ex);
                portNames = Array.Empty<string>();
            }

            ComboBoxPort.ItemsSource = portNames;
            ComboBoxSerial.ItemsSource = new List<int>() { 9600, 115200, 38400, 300, 600, 1200, 2400, 4800, 14400, 19200, 57600 };
            MarkPhase("串口");

            InitializeRelativeSpectrumPlot();
            MarkPhase("首张曲线");

            ViewResultManager viewResultManager = await (viewResultInitializationTask ?? Task.Run(ViewResultManager.GetInstance));
            if (!IsLoaded)
            {
                return;
            }

            viewResultManager.ListView = ViewResultList;
            ViewResultList.ItemsSource = viewResultManager.ViewResluts;
            if (ViewResultList.View is GridView gridView)
            {
                GridViewColumnVisibility.AddGridViewColumn(gridView.Columns, GridViewColumnVisibilitys);
                Config.GridViewColumnVisibilitys.CopyToGridView(GridViewColumnVisibilitys);
                Config.GridViewColumnVisibilitys = GridViewColumnVisibilitys;
                GridViewColumnVisibility.AdjustGridViewColumnAuto(gridView.Columns, GridViewColumnVisibilitys);
            }

            DataContext = manager;
            ViewResultList.CommandBindings.Add(new CommandBinding(ApplicationCommands.Delete, (s, e) => Delete(), (s, e) => e.CanExecute = ViewResultList.SelectedIndex > -1));
            ViewResultList.CommandBindings.Add(new CommandBinding(ApplicationCommands.SelectAll, (s, e) => ViewResultList.SelectAll(), (s, e) => e.CanExecute = true));
            ViewResultList.CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, CopyVisibleColumns, (s, e) => e.CanExecute = ViewResultList.SelectedIndex > -1));

            UpdateEqeColumnsVisibility(MainWindowConfig.Instance.EqeEnabled);
            InitializeSmuTimedButtons();
            MarkPhase("历史结果与绑定");

            DockingManager.IsEnabled = true;
            _ = AutoConnectSmuIfNeededAsync();

            try
            {
                await (cvCameraInitializationTask ?? CvCameraResourceInitialization.Value);
            }
            catch (Exception ex)
            {
                log.Error("cvCamera 资源初始化失败", ex);
                if (!IsLoaded)
                {
                    return;
                }

                ResourceInitializationProgress.IsIndeterminate = false;
                ResourceInitializationProgress.Value = 0;
                ResourceInitializationText.Text = SpectrumResources.CvCameraInitializationFailed + ex.GetBaseException().Message;
                return;
            }

            if (!IsLoaded)
            {
                return;
            }

            SpectrometerConnectionGroup.IsEnabled = true;
            ResourceInitializationBanner.Visibility = Visibility.Collapsed;
            MarkPhase("设备资源");
            stopwatch.Stop();
            log.Info($"主窗口功能初始化完成，耗时 {stopwatch.ElapsedMilliseconds} ms；{string.Join(", ", phases)}");

            _ = Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                if (!IsLoaded)
                {
                    return;
                }

                Stopwatch nativeLogStopwatch = Stopwatch.StartNew();
                try
                {
                    InitializeNativeLogPanel();
                    nativeLogStopwatch.Stop();
                    log.Info($"原生日志面板初始化完成，耗时 {nativeLogStopwatch.ElapsedMilliseconds} ms");
                }
                catch (Exception ex)
                {
                    log.Warn("加载光谱仪原生日志面板失败", ex);
                    ShowNativeLogPlaceholder();
                }
            }));
        }

        private void InitializeRelativeSpectrumPlot()
        {
            string title = SpectrumResources.相对光谱曲线;
            string fontName = Fonts.Detect(title);
            wpfplot1.Plot.XLabel(SpectrumResources.波长Nm);
            wpfplot1.Plot.YLabel(SpectrumResources.相对光谱);
            wpfplot1.Plot.Axes.Title.Label.Text = title;
            wpfplot1.Plot.Axes.Title.Label.FontName = fontName;
            wpfplot1.Plot.Axes.Left.Label.FontName = fontName;
            wpfplot1.Plot.Axes.Bottom.Label.FontName = fontName;
            wpfplot1.Plot.Axes.SetLimitsX(380, 780);
            wpfplot1.Plot.Axes.SetLimitsY(-0.05, 1);
            wpfplot1.Plot.Axes.Bottom.Min = 380;
            wpfplot1.Plot.Axes.Bottom.Max = 780;
            wpfplot1.Plot.Axes.Left.Min = -0.05;
            wpfplot1.Plot.Axes.Left.Max = 1;
            AddSpectrumColorBar(wpfplot1);
        }

        private void EnsureAbsoluteSpectrumPlotInitialized()
        {
            if (absoluteSpectrumPlotInitialized)
            {
                return;
            }

            string title = SpectrumResources.AbsoluteSpectrumCurve;
            string fontName = Fonts.Detect(title);
            wpfplot2.Plot.XLabel(SpectrumResources.波长Nm);
            wpfplot2.Plot.YLabel(SpectrumResources.AbsoluteSpectrum);
            wpfplot2.Plot.Axes.Title.Label.Text = title;
            wpfplot2.Plot.Axes.Title.Label.FontName = fontName;
            wpfplot2.Plot.Axes.Left.Label.FontName = fontName;
            wpfplot2.Plot.Axes.Bottom.Label.FontName = fontName;
            wpfplot2.Plot.Axes.SetLimitsX(380, 780);
            wpfplot2.Plot.Axes.SetLimitsY(-0.05, 1);
            wpfplot2.Plot.Axes.Bottom.Min = 380;
            wpfplot2.Plot.Axes.Bottom.Max = 780;
            wpfplot2.Plot.Axes.Left.Min = -0.05;
            wpfplot2.Plot.Axes.Left.Max = 1;
            AddSpectrumColorBar(wpfplot2);
            absoluteSpectrumPlotInitialized = true;
        }

        private async Task AutoConnectSmuIfNeededAsync()
        {
            if (!Manager.SmuController.Config.IsAutoStart || Manager.SmuController.IsOpen || Manager.SmuController.IsBusy)
            {
                return;
            }

            await Task.Yield();

            bool ok = await Manager.SmuController.OpenAsync();
            if (ok)
            {
                log.Info($"SMU 自动连接成功: {Manager.SmuController.Version}");
                return;
            }

            if (!string.IsNullOrWhiteSpace(Manager.SmuController.LastErrorMessage))
            {
                log.Warn($"SMU 自动连接失败: {Manager.SmuController.LastErrorMessage}");
            }
        }

        /// <summary>
        /// Initialize the native C++ spectrometer log panel in the DockingManager.
        /// Searches for spectrometer log files and creates a LogLocalOutput UserControl.
        /// </summary>
        private void InitializeNativeLogPanel()
        {
            nativeLogOutput?.Dispose();
            nativeLogOutput = null;
            NativeLogGrid.Children.Clear();

            string? logPath = Spectrum.License.MenuSpectrometerNativeLog.FindSpectrometerLogFile(AppDomain.CurrentDomain.BaseDirectory);
            if (!string.IsNullOrEmpty(logPath))
            {
                nativeLogOutput = new LogLocalOutput(logPath, System.Text.Encoding.GetEncoding("GB2312"));
                NativeLogGrid.Children.Add(nativeLogOutput);
            }
            else
            {
                ShowNativeLogPlaceholder();
            }
        }

        private void ShowNativeLogPlaceholder()
        {
            nativeLogOutput?.Dispose();
            nativeLogOutput = null;
            NativeLogGrid.Children.Clear();
            NativeLogGrid.Children.Add(new TextBlock
            {
                Text = SpectrumResources.NativeLogPlaceholder,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                Foreground = System.Windows.Media.Brushes.Gray
            });
        }
    }
}
