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
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using ScottPlot;
using Spectrum.Data;
using Spectrum.Layout;
using Spectrum.Menus;
using Spectrum.Models;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Ports;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms.VisualStyles;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace Spectrum
{
    public class MenuSpectrumWindow : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.Tool;
        public override string Header => "光谱仪测试";
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
        BitmapSource pic1931;
        BitmapSource pic1976;
        Mat src1931;
        Mat src1976;

        public MainWindow()
        {
            string path1931 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Assets\Image\CIE-1931.jpg");
            string path1976 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Assets\Image\CIE-1976.jpg");
            if (File.Exists(path1931))
                src1931 = new Mat(path1931, ImreadModes.Color);
            if (File.Exists(path1976))
                src1976 = new Mat(path1976, ImreadModes.Color);
            InitializeComponent();
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
                Manager.Disconnect();
                Instance = null;
            };
            this.Title += " - " + Assembly.GetAssembly(typeof(MainWindow))?.GetName().Version?.ToString() ?? "";
        }
        private LogOutput? logOutput;

        private void Window_Initialized(object sender, EventArgs e)
        {
            log.Info("初始化 cvCamera 资源");
            cvCameraCSLib.InitResource(IntPtr.Zero, IntPtr.Zero);

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

            if (MainWindowConfig.Instance.LogControlVisibility)
            {
                logOutput = new LogOutput("%date{HH:mm:ss} [%thread] %-5level %message%newline");
                LogGrid.Children.Add(logOutput);
            }
            LayoutManager.RegisterContent("LogPanel", LogGrid);

            // Initialize native C++ spectrometer log panel
            InitializeNativeLogPanel();
            LayoutManager.RegisterContent("NativeLogPanel", NativeLogGrid);

            LayoutManager.RegisterContent("CIEDiagram", CiePane.Content);

            // Load saved layout if exists
            LayoutManager.LoadLayout();

            ViewResultManager.ListView = ViewResultList;

            // Wire up adaptive auto dark execution for gear settings dialog
            Manager.AutodarkParam.ExecuteAdaptiveAutoDark = () => Button4_Click_1(null, null);

            MenuManager.GetInstance().LoadMenuForWindow("Spectrum", menu);

            image1931.Source = src1931?.ToBitmapSource();
            image1976.Source = src1976?.ToBitmapSource();
            ComboBoxSpectrometerType.ItemsSource = from e1 in Enum.GetValues(typeof(SpectrometerType)).Cast<SpectrometerType>()
                                                   select new KeyValuePair<SpectrometerType, string>(e1, e1.ToDescription());

            cvCameraCSLib.InitResource(IntPtr.Zero, IntPtr.Zero);

            SetEmissionSP100Config.Instance.EditChanged += (s, e) =>
            {
                if (SpectrometerHandle != IntPtr.Zero)
                {
                    log.Debug($"设置 SP100 参数: IsEnabled={SetEmissionSP100Config.Instance.IsEnabled}, nStartPos={SetEmissionSP100Config.Instance.nStartPos}, nEndPos={SetEmissionSP100Config.Instance.nEndPos}, dMeanThreshold={SetEmissionSP100Config.Instance.dMeanThreshold}");
                    int ret = Spectrometer.CM_SetEmissionSP100(SpectrometerHandle, SetEmissionSP100Config.Instance.IsEnabled, SetEmissionSP100Config.Instance.nStartPos, SetEmissionSP100Config.Instance.nEndPos, SetEmissionSP100Config.Instance.dMeanThreshold);
                    if (ret == 1)
                        log.Info("SP100 参数设置成功");
                    else
                        log.Warn($"SP100 参数设置失败: {Spectrometer.GetErrorMessage(ret)}");
                }

            };
            string[] portNames = SerialPort.GetPortNames();
            List<int> BaudRates = new List<int>() { 9600,115200, 38400, 300, 600, 1200, 2400, 4800, 14400, 19200, 57600 };
            ComboBoxPort.ItemsSource = portNames;
            ComboBoxSerial.ItemsSource = BaudRates;

            string title = "相对光谱曲线";
            wpfplot1.Plot.XLabel("波长[nm]");
            wpfplot1.Plot.YLabel("相对光谱");
            wpfplot1.Plot.Axes.Title.Label.Text = title;
            wpfplot1.Plot.Axes.Title.Label.FontName = Fonts.Detect(title);
            wpfplot1.Plot.Axes.Title.Label.Text = title;
            wpfplot1.Plot.Axes.Left.Label.FontName = Fonts.Detect(title);
            wpfplot1.Plot.Axes.Bottom.Label.FontName = Fonts.Detect(title);

            wpfplot1.Plot.Axes.SetLimitsX(380, 780);
            wpfplot1.Plot.Axes.SetLimitsY(-0.05, 1);
            wpfplot1.Plot.Axes.Bottom.Min = 380;
            wpfplot1.Plot.Axes.Bottom.Max = 780;
            wpfplot1.Plot.Axes.Left.Min = -0.05;
            wpfplot1.Plot.Axes.Left.Max = 1;

            // Add visible spectrum rainbow color bar below the plot
            AddSpectrumColorBar(wpfplot1);

            string titleAbsolute = "绝对光谱曲线";
            wpfplot2.Plot.XLabel("波长[nm]");
            wpfplot2.Plot.YLabel("绝对光谱");
            wpfplot2.Plot.Axes.Title.Label.Text = titleAbsolute;
            wpfplot2.Plot.Axes.Title.Label.FontName = Fonts.Detect(titleAbsolute);
            wpfplot2.Plot.Axes.Left.Label.FontName = Fonts.Detect(titleAbsolute);
            wpfplot2.Plot.Axes.Bottom.Label.FontName = Fonts.Detect(titleAbsolute);
            wpfplot2.Plot.Axes.SetLimitsX(380, 780);
            wpfplot2.Plot.Axes.Bottom.Min = 380;
            wpfplot2.Plot.Axes.Bottom.Max = 780;

            AddSpectrumColorBar(wpfplot2);

            if (ViewResultSpectrums.Count != 0)
            {
                foreach (var item in ViewResultSpectrums)
                {
                    item.Gen();
                    ScatterPlots.Add(item.ScatterPlot);
                    AbsoluteScatterPlots.Add(item.AbsoluteScatterPlot);
                }

            }

            ViewResultList.ItemsSource = ViewResultSpectrums;
            if (ViewResultList.View is GridView gridView)
            {
                GridViewColumnVisibility.AddGridViewColumn(gridView.Columns, GridViewColumnVisibilitys);
                Config.GridViewColumnVisibilitys.CopyToGridView(GridViewColumnVisibilitys);
                Config.GridViewColumnVisibilitys = GridViewColumnVisibilitys;
                GridViewColumnVisibility.AdjustGridViewColumnAuto(gridView.Columns, GridViewColumnVisibilitys);
            }
            this.DataContext = Manager;

            pic1931 = src1931?.ToBitmapSource();
            pic1976 = src1976?.ToBitmapSource();

            ViewResultList.CommandBindings.Add(new CommandBinding(ApplicationCommands.Delete, (s, e) => Delete(), (s, e) => e.CanExecute = ViewResultList.SelectedIndex > -1));
            ViewResultList.CommandBindings.Add(new CommandBinding(ApplicationCommands.SelectAll, (s, e) => ViewResultList.SelectAll(), (s, e) => e.CanExecute = true));
            ViewResultList.CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, CopyVisibleColumns, (s, e) => e.CanExecute = ViewResultList.SelectedIndex > -1));

            this.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, (ThreadStart)delegate ()
            {
                image1931.Source = pic1931;
                image1976.Source = pic1976;
            });

            UpdateEqeColumnsVisibility(MainWindowConfig.Instance.EqeEnabled);
        }

        /// <summary>
        /// Initialize the native C++ spectrometer log panel in the DockingManager.
        /// Searches for spectrometer log files and creates a LogLocalOutput UserControl.
        /// </summary>
        private void InitializeNativeLogPanel()
        {
            string? logPath = Spectrum.License.MenuSpectrometerNativeLog.FindSpectrometerLogFile(AppDomain.CurrentDomain.BaseDirectory);
            if (!string.IsNullOrEmpty(logPath))
            {
                var nativeLogOutput = new LogLocalOutput(logPath, System.Text.Encoding.GetEncoding("GB2312"));
                NativeLogGrid.Children.Add(nativeLogOutput);
            }
            else
            {
                // Show a placeholder message when no log file is found yet
                var placeholder = new TextBlock
                {
                    Text = "光谱仪原生日志文件尚未生成。连接光谱仪后日志将自动显示。",
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = System.Windows.VerticalAlignment.Center,
                    Foreground = System.Windows.Media.Brushes.Gray
                };
                NativeLogGrid.Children.Add(placeholder);
            }
        }
    }
}
