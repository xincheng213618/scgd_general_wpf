using AvalonDock.Layout;
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Themes;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using ColorVision.UI.LogImp;
using ColorVision.UI.Menus;
using ColorVision.UI.Sorts;
using cvColorVision;
using log4net;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using ScottPlot;
using ScottPlot.Plottables;
using Spectrum.Menus;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
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
            LayoutManager.RegisterContent("CIEDiagram", CiePane.Content);

            // Load saved layout if exists
            LayoutManager.LoadLayout();

            ViewResultManager.ListView = ViewResultList;

            // Wire up adaptive auto dark execution for gear settings dialog
            Manager.AutodarkParam.ExecuteAdaptiveAutoDark = () => Button4_Click_1(null, null);

            MenuManager.GetInstance().LoadMenuForWindow("Spectrum", menu);

            image.Source = src1931?.ToBitmapSource();
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

            this.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, (ThreadStart)delegate () { image.Source = pic1931; });

            UpdateEqeColumnsVisibility(MainWindowConfig.Instance.EqeEnabled);
        }

        float fIntTime = 0;
        int testid = 0;

        public static int MyCallback(IntPtr strText, int nLen)
        {
            string text = Marshal.PtrToStringAnsi(strText, nLen);
            log.Debug("光谱仪回调: " + text);
            return 0;
        }
        public IntPtr SpectrometerHandle => Manager.Handle;

        //连接光谱仪
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Manager.Handle = Spectrometer.CM_CreateEmission(0, MyCallback);

                int com = 0;
                if (Manager.Config.IsComPort)
                {
                     com = int.Parse(Manager.Config.SzComName.Replace("COM", ""));
                }

                int iR = Spectrometer.CM_Emission_Init(SpectrometerHandle, com, Manager.Config.BaudRate);
                if (iR == 1)
                {
                    log.Info("光谱仪连接成功");
                    Manager.IsConnected = true;

                    try
                    {
                        int bufferLength = 1024;
                        StringBuilder snBuilder = new StringBuilder(bufferLength);
                        int snRet = Spectrometer.CM_GetSpectrSerialNumber(SpectrometerHandle, snBuilder);
                        if (snRet == 1)
                        {
                            string sn = snBuilder.ToString().Trim();
                            if (!string.IsNullOrEmpty(sn))
                            {
                                Manager.SerialNumber = sn;
                                log.Info($"光谱仪序列号: {sn}");
                            }
                            else
                            {
                                Manager.SerialNumber = "Unknown";
                            }
                        }
                        else
                        {
                            log.Warn($"获取序列号失败: {Spectrometer.GetErrorMessage(snRet)}");
                            Manager.SerialNumber = "Unknown";
                        }
                    }
                    catch (Exception snEx)
                    {
                        log.Warn("读取序列号异常", snEx);
                        Manager.SerialNumber = "Unknown";
                    }
                    Manager.LoadCalibrationConfig();

                    iR = Spectrometer.CM_Emission_LoadWavaLengthFile(SpectrometerHandle, Manager.WavelengthFile);
                    if (iR == 1)
                        log.Info($"加载波长文件成功: {Manager.WavelengthFile}");
                    else
                        log.Warn($"加载波长文件失败: {Manager.WavelengthFile}, {Spectrometer.GetErrorMessage(iR)}");

                    iR = Spectrometer.CM_Emission_LoadMagiudeFile(SpectrometerHandle, Manager.MaguideFile);
                    if (iR == 1)
                        log.Info($"加载幅值文件成功: {Manager.MaguideFile}");
                    else
                        log.Warn($"加载幅值文件失败: {Manager.MaguideFile}, {Spectrometer.GetErrorMessage(iR)}");

                    log.Debug($"设置 SP100 参数: IsEnabled={SetEmissionSP100Config.Instance.IsEnabled}, nStartPos={SetEmissionSP100Config.Instance.nStartPos}, nEndPos={SetEmissionSP100Config.Instance.nEndPos}, dMeanThreshold={SetEmissionSP100Config.Instance.dMeanThreshold}");
                    int ret = Spectrometer.CM_SetEmissionSP100(SpectrometerHandle, SetEmissionSP100Config.Instance.IsEnabled, SetEmissionSP100Config.Instance.nStartPos, SetEmissionSP100Config.Instance.nEndPos, SetEmissionSP100Config.Instance.dMeanThreshold);
                    if (ret != 1)
                        log.Warn($"SP100 参数设置失败: {Spectrometer.GetErrorMessage(ret)}");

                    State2.Text = Spectrum.Properties.Resources.连接成功;
                    State4.Text = "SP-100";
                    button3.IsEnabled = true;
                    button5.IsEnabled = true;
                    button6.IsEnabled = true;
                }
                else
                {
                    Manager.IsConnected = false;
                    string errorMsg = Spectrometer.GetErrorMessage(iR);
                    log.Error($"光谱仪连接失败: {errorMsg}");
                    MessageBox.Show(Application.Current.GetActiveWindow(), $"连接失败: {errorMsg}");
                }
            }
            catch(Exception ex)
            {
                log.Error("光谱仪连接异常", ex);
                MessageBox.Show(ex.Message);
            }

        }
        int ret;

        //断开连接
        private void ButtonClose_Click(object sender, RoutedEventArgs e)
        {
            testid = 0;
            IsRun = false;
            ret = Manager.Disconnect();
            Manager.SerialNumber = string.Empty;
            State2.Text = Spectrum.Properties.Resources.未连接;
            State4.Text = "---";
        }
            
        //按钮显示1976图
        private void Button2_Click(object sender, RoutedEventArgs e)
        {
            this.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, (ThreadStart)delegate () { image.Source = pic1976; });
        }
        //按钮显示1931图
        private void Button1_Click(object sender, RoutedEventArgs e)
        {
            this.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, (ThreadStart)delegate () { image.Source = pic1931; });
        }


        //单次校零
        private void Button3_Click(object sender, RoutedEventArgs e)
        {
            if (IsRun)
            {
                MessageBox1.Show(Application.Current.GetActiveWindow(), "正在运行");
                return;
            }
            IsRun = true;
            SetOperationButtonsEnabled(false);

            Task.Run(async () =>
            {
                try
                {
                    if (Manager.ShutterController.IsConnected)
                    {
                        log.Debug("开启快门");
                       await  Manager.ShutterController.OpenShutter();
                    }
                    int ret = Spectrometer.CM_Emission_DarkStorage(SpectrometerHandle, Manager.IntTime, Manager.Average, 0, Manager.fDarkData);
                    if (Manager.ShutterController.IsConnected)
                    {
                        log.Debug("关闭快门");
                        await Manager.ShutterController.CloseShutter();
                    }
                    if (ret == 1)
                    {
                        log.Info("校零成功");
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show(Application.Current.GetActiveWindow(), "校零成功");
                        });
                    }
                    else
                    {
                        string errorMsg = Spectrometer.GetErrorMessage(ret);
                        log.Error($"校零失败: {errorMsg}");
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show(Application.Current.GetActiveWindow(), $"校零失败: {errorMsg}");
                        });
                    }
                    IsRun = false;
                    SetOperationButtonsEnabled(true);
                }
                catch (Exception ex)
                {
                    log.Error("校零异常", ex);
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show(Application.Current.GetActiveWindow(), "校零异常: " + ex.Message);
                    });
                    IsRun = false;
                    SetOperationButtonsEnabled(true);
                }
            });


        }

        public void DrawCIEPoinr(double fx, double fy ,double fu,double fv)
        {
            try
            {
                if (src1931 != null)
                {
                    Mat cir1931 = src1931.Clone();
                    OpenCvSharp.Point p1;
                    p1.X = Convert.ToInt32(Math.Round(fx * 10 * 97 + 104));
                    p1.Y = Convert.ToInt32(Math.Round(881 - fy * 10 * 97));
                    Cv2.Circle(cir1931, p1.X, p1.Y, 10, new Scalar(0, 0, 255), -1, LineTypes.Link8, 0);
                    pic1931 = cir1931.ToWriteableBitmap();
                }
                if (src1976 != null)
                {
                    Mat cir1976 = src1976.Clone();
                    OpenCvSharp.Point p2;
                    p2.X = Convert.ToInt32(Math.Round(fu * 10 * 154 + 49));
                    p2.Y = Convert.ToInt32(Math.Round(973 - fv * 10 * 154));
                    Cv2.Circle(cir1976, p2.X, p2.Y, 10, new Scalar(0, 0, 255), -1, LineTypes.Link8, 0);
                    pic1976 = cir1976.ToWriteableBitmap();
                }
            }
            catch (Exception ex)
            {

            }

        }

        public int MyAutoTimeCallback(int time, double spectum)
        {
            log.Debug($"自动积分时间回调: 积分时间={time}, 光谱强度={spectum}");
            return 0;
        }

        private void AutoIntTime_Click(object sender, RoutedEventArgs e)
        {
            if (IsRun)
            {
                MessageBox1.Show("正在运行");
                return;
            }
            SetOperationButtonsEnabled(false);

            Task.Run(() =>
            {
                IsRun = true;
                if (Manager.IntTimeConfig.IsOldVersion)
                {
                    ret = Spectrometer.CM_Emission_GetAutoTime(SpectrometerHandle, ref fIntTime, Manager.IntTimeConfig.IntLimitTime, Manager.IntTimeConfig.AutoIntTimeB, (int)Manager.MaxPercent);
                    if (ret == 1)
                    {
                        log.Info($"自动积分时间获取成功: {fIntTime}ms");
                        Manager.IntTime = fIntTime;
                    }
                    else
                    {
                        log.Warn($"自动积分时间获取失败: {Spectrometer.GetErrorMessage(ret)}");
                    }
                }
                else
                {
                    ret = Spectrometer.CM_Emission_GetAutoTimeEx(SpectrometerHandle, ref fIntTime, Manager.IntTimeConfig.IntLimitTime, Manager.IntTimeConfig.AutoIntTimeB, Manager.Max, MyAutoTimeCallback);
                    if (ret == 1)
                    {
                        log.Info($"自动积分时间获取成功: {fIntTime}ms");
                        Manager.IntTime = fIntTime;
                    }
                    else
                    {
                        log.Warn($"自动积分时间获取失败: {Spectrometer.GetErrorMessage(ret)}");
                    }
                }
                IsRun = false;
                SetOperationButtonsEnabled(true);
            });


        }
        public async Task Measure()
        {
            if (IsRun)
            {
                log.Info("上次执行还未结束");
                return;
            }
            IsRun = true;

            if (Manager.EnableAutodark)
            {
                if (!Manager.ShutterController.IsConnected)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show(Application.Current.GetActiveWindow(), "未配备shutter，无法自动校零", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    });
                    IsRun = false;
                    return;
                }
                log.Debug("开启快门");
                await Manager.ShutterController.OpenShutter();
                int ret = Spectrometer.CM_Emission_DarkStorage(SpectrometerHandle, Manager.IntTime, Manager.Average, 0, Manager.fDarkData);
                log.Debug("关闭快门");
                await Manager.ShutterController.CloseShutter();
                if (ret == 1)
                    log.Debug("测量前自动校零成功");
                else
                    log.Warn($"测量前自动校零失败: {Spectrometer.GetErrorMessage(ret)}");
            }

            if (Manager.EnableAutoIntegration)
            {

                if (Manager.IntTimeConfig.IsOldVersion)
                {
                    ret = Spectrometer.CM_Emission_GetAutoTime(SpectrometerHandle, ref fIntTime, Manager.IntTimeConfig.IntLimitTime, Manager.IntTimeConfig.AutoIntTimeB, (int)Manager.MaxPercent);
                    if (ret == 1)
                    {
                        log.Debug($"自动积分时间: {fIntTime}ms");
                        Manager.IntTime = fIntTime;
                    }
                    else
                    {
                        log.Warn($"自动积分时间获取失败: {Spectrometer.GetErrorMessage(ret)}");
                        IsRun = false;
                        return;
                    }
                }
                else
                {
                    ret = Spectrometer.CM_Emission_GetAutoTimeEx(SpectrometerHandle, ref fIntTime, Manager.IntTimeConfig.IntLimitTime, Manager.IntTimeConfig.AutoIntTimeB, Manager.Max, MyAutoTimeCallback);
                    if (ret == 1)
                    {
                        log.Debug($"自动积分时间: {fIntTime}ms");
                        Manager.IntTime = fIntTime;
                    }
                    else
                    {
                        log.Warn($"自动积分时间获取失败: {Spectrometer.GetErrorMessage(ret)}");
                        IsRun = false;
                        return;
                    }
                }
            }


            if (Manager.EnableAdaptiveAutoDark)
            {
                ret = Spectrometer.CM_Emission_AutoDarkStorage(SpectrometerHandle, Manager.IntTime, Manager.Average, 0, Manager.fDarkData);
                if (ret == 1)
                {
                    log.Debug("自适应校零数据获取成功");
                }
                else if (ret == 0)
                {
                    log.Warn("自适应校零未初始化，请先执行一次自适应校零");
                    isstartAuto = false;
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show("请先做一次自适应校零");
                    });
                    IsRun = false;
                    return;
                }
                else
                {
                    log.Warn($"自适应校零数据获取失败: {Spectrometer.GetErrorMessage(ret)}");
                }
            }

            float fDx = 0;
            float fDy = 0;
            COLOR_PARA cOLOR_PARA = new COLOR_PARA();

            if (Manager.GetDataConfig.IsSyncFrequencyEnabled)
            {
                float fIntTime = Manager.IntTime;
                ret = Spectrometer.CM_Emission_GetDataSyncfreq(SpectrometerHandle, 0, Manager.GetDataConfig.Syncfreq, Manager.GetDataConfig.SyncfreqFactor, ref fIntTime, Manager.Average, Manager.GetDataConfig.FilterBW, Manager.fDarkData, fDx, fDy, Manager.GetDataConfig.SetWL1, Manager.GetDataConfig.SetWL2, ref cOLOR_PARA);
                if (ret != 1)
                    log.Warn($"同步频率采集数据失败: {Spectrometer.GetErrorMessage(ret)}");

                if (Manager.EnableAutoIntegration)
                    Manager.IntTime = fIntTime;
            }
            else
            {
                ret = Spectrometer.CM_Emission_GetData(SpectrometerHandle, 0, Manager.IntTime, Manager.Average, Manager.GetDataConfig.FilterBW, Manager.fDarkData, fDx, fDy, Manager.GetDataConfig.SetWL1, Manager.GetDataConfig.SetWL2, ref cOLOR_PARA);
                if (ret == -13007)
                {
                    log.Warn($"采集数据超时，正在重试: {Spectrometer.GetErrorMessage(ret)}");
                    ret = Spectrometer.CM_Emission_GetData(SpectrometerHandle, 0, Manager.IntTime, Manager.Average, Manager.GetDataConfig.FilterBW, Manager.fDarkData, fDx, fDy, Manager.GetDataConfig.SetWL1, Manager.GetDataConfig.SetWL2, ref cOLOR_PARA);
                }
                if (ret != 1)
                    log.Warn($"采集光谱数据失败: {Spectrometer.GetErrorMessage(ret)}");
            }
            if (ret == 1)
            {
                if (cOLOR_PARA.fPh < 1)
                {
                    cOLOR_PARA.fPh = (float)Math.Round((float)cOLOR_PARA.fPh, 4);
                }
                else
                {
                    cOLOR_PARA.fPh = (float)Math.Round((float)cOLOR_PARA.fPh, 2);
                }
                Application.Current.Dispatcher.Invoke(() =>
                {
                    SprectrumModel sprectrumModel = new SprectrumModel() { ColorParam = cOLOR_PARA };
                    ViewResultManager.Save(sprectrumModel);
                    if (MainWindowConfig.Instance.EqeEnabled && ViewResultSpectrums.Count > 0)
                    {
                        // When SMU is connected, read V/I from it; otherwise use manual values
                        float voltage = MainWindowConfig.Instance.EqeVoltage;
                        float currentMA = MainWindowConfig.Instance.EqeCurrentMA;
                        if (Manager.SmuController.IsOpen)
                        {
                            Manager.SmuController.ApplySettings();
                            if (Manager.SmuController.MeasureData())
                            {
                                var (smuV, smuI) = Manager.SmuController.GetVI();
                                voltage = smuV;
                                currentMA = smuI;
                                MainWindowConfig.Instance.EqeVoltage = voltage;
                                MainWindowConfig.Instance.EqeCurrentMA = currentMA;
                            }
                        }

                        var latest = ViewResultManager.Config.OrderByType == SqlSugar.OrderByType.Desc
                            ? ViewResultSpectrums.FirstOrDefault()
                            : ViewResultSpectrums.LastOrDefault();
                        if (latest != null)
                        {
                            latest.CalculateEqeParams(voltage, currentMA);
                            ViewResultManager.UpdateEqeFields(latest, isRecalculated: false);
                        }
                    }
                });
            }
            else
            {
                errornum++;
                log.Error($"光谱数据采集失败: {Spectrometer.GetErrorMessage(ret)}");
            }
            IsRun = false;
        }

        int errornum = 0;

        public async Task ReConnet()
        {
            for (int i = 0; i < 6; i++)
            {
                log.Warn($"尝试重连光谱仪 ({i + 1}/6)");
                int ret = Spectrometer.CM_Emission_Close(Manager.Handle);
                log.Debug($"CM_Emission_Close: {ret}");
                ret = Spectrometer.CM_ReleaseEmission(Manager.Handle);
                log.Debug($"CM_ReleaseEmission: {ret}");
                await Task.Delay(200);
                Manager.Handle = Spectrometer.CM_CreateEmission(0, MyCallback);
                int ncom = 0;
                if (Manager.Config.IsComPort)
                {
                     ncom = int.Parse(Manager.Config.SzComName.Replace("COM", ""));
                }
                int iR = Spectrometer.CM_Emission_Init(SpectrometerHandle, ncom, Manager.Config.BaudRate);
                if (iR == 1)
                {
                    Manager.IsConnected = true;
                    iR = Spectrometer.CM_Emission_LoadWavaLengthFile(SpectrometerHandle, Manager.WavelengthFile);
                    if (iR != 1) log.Warn($"重连后加载波长文件失败: {Spectrometer.GetErrorMessage(iR)}");
                    iR = Spectrometer.CM_Emission_LoadMagiudeFile(SpectrometerHandle, Manager.MaguideFile);
                    if (iR != 1) log.Warn($"重连后加载幅值文件失败: {Spectrometer.GetErrorMessage(iR)}");

                    log.Debug($"重连后设置 SP100: IsEnabled={SetEmissionSP100Config.Instance.IsEnabled}, nStartPos={SetEmissionSP100Config.Instance.nStartPos}, nEndPos={SetEmissionSP100Config.Instance.nEndPos}");
                    ret = Spectrometer.CM_SetEmissionSP100(SpectrometerHandle, SetEmissionSP100Config.Instance.IsEnabled, SetEmissionSP100Config.Instance.nStartPos, SetEmissionSP100Config.Instance.nEndPos, SetEmissionSP100Config.Instance.dMeanThreshold);
                    if (ret != 1) log.Warn($"重连后 SP100 设置失败: {Spectrometer.GetErrorMessage(ret)}");

                    log.Info("光谱仪重连成功");
                    break;
                }
                else
                {
                    log.Debug($"重连尝试 {i + 1} 失败: {Spectrometer.GetErrorMessage(iR)}");
                }
                await Task.Delay(200);
            }


            IsRun = false;
        }

        //单次测量
        private void Button5_Click(object sender, RoutedEventArgs e)
        {
            if (IsRun)
            {
                MessageBox.Show("正在执行任务请稍后");
                return;
            }
            SetOperationButtonsEnabled(false);
            Task.Run(async () =>
            {
                try
                {
                    await Measure();
                }
                finally
                {
                    SetOperationButtonsEnabled(true);
                }
            });
        }
        bool IsRun;

        /// <summary>
        /// Disables/enables all C++ operation buttons to prevent concurrent spectrometer calls.
        /// Must be called on the UI thread.
        /// </summary>
        private void SetOperationButtonsEnabled(bool enabled)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                button3.IsEnabled = enabled;
                button5.IsEnabled = enabled;
                button6.IsEnabled = enabled;
                ButtonAutoInt.IsEnabled = enabled;
            });
        }
        //自适应校零
        private void Button4_Click_1(object sender, RoutedEventArgs e)
        {
           if (IsRun)
            {
                MessageBox.Show("正在执行任务请稍后");
                return;
            }
            log.Debug("开始自适应校零");
            SetOperationButtonsEnabled(false);
            Task.Run(() =>
            {
                IsRun = true;
                int ret = Spectrometer.CM_Emission_Init_Auto_Dark(SpectrometerHandle, Manager.AutodarkParam.fTimeStart, Manager.AutodarkParam.nStepTime, Manager.AutodarkParam.nStepCount, Manager.Average);
                IsRun = false;
                SetOperationButtonsEnabled(true);
                if (ret == 1)
                {
                    log.Info("自适应校零成功");
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show("自适应校零成功");
                    });
                }
                else
                {
                    string errorMsg = Spectrometer.GetErrorMessage(ret);
                    log.Error($"自适应校零失败: {errorMsg}");
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show($"自适应校零失败: {errorMsg}");
                    });
                }
            });
        }


        //连续测量
        private void Button6_Click(object sender, RoutedEventArgs e)
        {
            IsRun = false;
            isstartAuto = true;
            errornum = 0;
            button6.Visibility = Visibility.Collapsed;
            button7.Visibility = Visibility.Visible;
            ContinuousProgressBar.Value = 0;
            TimeEstimationPanel.Visibility = Visibility.Visible;
            ElapsedTimeText.Text = "--:--";
            RemainingTimeText.Text = "--:--";
            // Disable other operation buttons during continuous testing
            button3.IsEnabled = false;
            button5.IsEnabled = false;
            ButtonAutoInt.IsEnabled = false;
            if (Manager.EnableAutodark)
            {
                if (!Manager.ShutterController.IsConnected)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show(Application.Current.GetActiveWindow(), "未配备shutter，无法自动校零", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                        button6.Visibility = Visibility.Visible;
                        button7.Visibility = Visibility.Collapsed;
                        ContinuousProgressBar.Value = 100;
                        TimeEstimationPanel.Visibility = Visibility.Collapsed;
                        Manager.LoopMeasureNum = 0;
                        errornum = 0;
                        // Re-enable operation buttons
                        button3.IsEnabled = true;
                        button5.IsEnabled = true;
                        button6.IsEnabled = true;
                        ButtonAutoInt.IsEnabled = true;
                    });
                    IsRun = false;
                    return;
                }
            }

            Task.Run(()=> LoopMeasure());
        }
        public async void LoopMeasure()
        {
            log.Info($"LoopMeasure Start All Count {Manager.MeasurementNum}");
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            while (isstartAuto)
            {
                if (Manager.MeasurementNum > 0)
                {
                    if (Manager.LoopMeasureNum >= Manager.MeasurementNum)
                    {

                        isstartAuto = false;
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            button6.Visibility = Visibility.Visible;
                            button7.Visibility = Visibility.Collapsed;
                            ContinuousProgressBar.Value = 100;
                            TimeEstimationPanel.Visibility = Visibility.Collapsed;
                            Manager.LoopMeasureNum = 0;
                            errornum = 0;
                            // Re-enable operation buttons
                            button3.IsEnabled = true;
                            button5.IsEnabled = true;
                            button6.IsEnabled = true;
                            ButtonAutoInt.IsEnabled = true;
                            MessageBox.Show(Application.Current.MainWindow, $"连续测试执行完毕,执行失败{errornum}");
                        });
                        break;
                    }
                    Manager.LoopMeasureNum++;

                    // Update progress bar and time estimation
                    int current = Manager.LoopMeasureNum;
                    int total = Manager.MeasurementNum;
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        double progress = (double)current / total * 100;
                        ContinuousProgressBar.Value = progress;

                        var elapsed = stopwatch.Elapsed;
                        ElapsedTimeText.Text = FormatTimeSpan(elapsed);

                        if (current > 0)
                        {
                            double avgPerItem = elapsed.TotalSeconds / current;
                            double remainingSeconds = avgPerItem * (total - current);
                            RemainingTimeText.Text = FormatTimeSpan(TimeSpan.FromSeconds(remainingSeconds));
                        }
                    });
                }
                await Measure();
                await Task.Delay(Manager.MeasurementInterval);
            }
        }

        private static string FormatTimeSpan(TimeSpan ts)
        {
            if (ts.TotalHours >= 1)
                return $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}";
            return $"{ts.Minutes:D2}:{ts.Seconds:D2}";
        }

        //停止连续测量
        private void Button7_Click(object sender, RoutedEventArgs e)
        {
            isstartAuto = false;
            button6.Visibility = Visibility.Visible;
            button7.Visibility = Visibility.Collapsed;
            TimeEstimationPanel.Visibility = Visibility.Collapsed;
            Manager.LoopMeasureNum = 0;
            // Re-enable operation buttons
            button3.IsEnabled = true;
            button5.IsEnabled = true;
            button6.IsEnabled = true;
            ButtonAutoInt.IsEnabled = true;
        }
        //清空数据
        private void Cleartable_Click(object sender, RoutedEventArgs e)
        {
            ViewResultSpectrums.Clear();
            ScatterPlots.Clear();
            AbsoluteScatterPlots.Clear();
            listView2.ItemsSource = new ObservableCollection<SpectralData>();
            if (ViewResultSpectrums.Count > 0)
            {
                ViewResultList.SelectedIndex = 0;
            }
            else
            {
                wpfplot1.Plot.Clear();
                AddSpectrumColorBar(wpfplot1);
                wpfplot1.Refresh();
                wpfplot2.Plot.Clear();
                wpfplot2.Refresh();
            }
            ReDrawPlot();
        }
        //导出data数据至excel
        private void Export_Click(object sender, RoutedEventArgs e)
        {
            var selectedItemsCopy = new List<object>();
            foreach (var item in ViewResultList.SelectedItems)
            {
                selectedItemsCopy.Add(item);
            }

            bool isEqeMode = MainWindowConfig.Instance.EqeEnabled;

            if (!isEqeMode)
            {
                using var dialog = new System.Windows.Forms.SaveFileDialog();
                dialog.Filter = "CSV files (*.csv) | *.csv";
                dialog.FileName = "SpectrometerExport" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
                dialog.RestoreDirectory = true;
                if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

                var csvBuilder = new StringBuilder();

                List<string> properties = new List<string>();
                properties.Add("No");
                properties.Add("IP");
                properties.Add("Luminance(Lv)(cd/m2)");
                properties.Add("Blue Light Intensity");
                properties.Add("CIEx");
                properties.Add("CIEy");
                properties.Add("CIEz");
                properties.Add("Cx");
                properties.Add("Cy");
                properties.Add("u'");
                properties.Add("v'");
                properties.Add("Correlated Color Temperature(CCT)(K)");
                properties.Add("DW(Ld)(nm)");
                properties.Add("Color Purity(%)");
                properties.Add("Peak Wavelength(Lp)(nm)");
                properties.Add("Color Rendering(Ra)");
                properties.Add("FWHM");
                properties.Add("Excitation Purity(%)");
                properties.Add("Dominant Wavelength Color");
                properties.Add("CIE2015X");
                properties.Add("CIE2015Y");
                properties.Add("CIE2015Z");
                properties.Add("CIE2015x");
                properties.Add("CIE2015y");
                properties.Add("CIE2015u");
                properties.Add("CIE2015v");

                for (int i = 380; i <= 780; i++)
                {
                    properties.Add(i.ToString());
                }
                for (int i = 380; i <= 780; i++)
                {
                    properties.Add("sp" + i.ToString());
                }

                for (int i = 0; i < properties.Count; i++)
                {
                    csvBuilder.Append(properties[i]);
                    if (i < properties.Count - 1)
                        csvBuilder.Append(',');
                }
                csvBuilder.AppendLine();

                foreach (var item in selectedItemsCopy)
                {
                    if (item is ViewResultSpectrum result)
                    {
                        csvBuilder.Append(result.Id + ",");
                        csvBuilder.Append(result.IP + ",");
                        csvBuilder.Append(result.Lv + ",");
                        csvBuilder.Append(result.Blue + ",");
                        csvBuilder.Append(result.fCIEx + ",");
                        csvBuilder.Append(result.fCIEy + ",");
                        csvBuilder.Append(result.fCIEz + ",");
                        csvBuilder.Append(result.fx + ",");
                        csvBuilder.Append(result.fy + ",");
                        csvBuilder.Append(result.fu + ",");
                        csvBuilder.Append(result.fv + ",");
                        csvBuilder.Append(result.fCCT + ",");
                        csvBuilder.Append(result.fLd + ",");
                        csvBuilder.Append(result.ColorPurityPercent + ",");
                        csvBuilder.Append(result.fLp + ",");
                        csvBuilder.Append(result.fRa + ",");
                        csvBuilder.Append(result.fHW + ",");
                        csvBuilder.Append(result.ExcitationPurityPercent + ",");
                        csvBuilder.Append(result.DominantWavelengthHex + ",");
                        csvBuilder.Append(result.fCIEx2015 + ",");
                        csvBuilder.Append(result.fCIEy2015 + ",");
                        csvBuilder.Append(result.fCIEz2015 + ",");
                        csvBuilder.Append(result.fx2015 + ",");
                        csvBuilder.Append(result.fy2015 + ",");
                        csvBuilder.Append(result.fu2015 + ",");
                        csvBuilder.Append(result.fv2015 + ",");

                        for (int i = 0; i < result.SpectralDatas.Count; i++)
                        {
                            csvBuilder.Append(result.SpectralDatas[i].AbsoluteSpectrum);
                            csvBuilder.Append(',');
                        }
                        for (int i = 0; i < result.SpectralDatas.Count; i++)
                        {
                            csvBuilder.Append(result.SpectralDatas[i].RelativeSpectrum);
                            if (i < result.SpectralDatas.Count - 1)
                                csvBuilder.Append(',');
                        }
                        csvBuilder.AppendLine();
                    }
                }
                File.WriteAllText(dialog.FileName, csvBuilder.ToString(), Encoding.UTF8);
            }
            else
            {
                using var dialog = new System.Windows.Forms.SaveFileDialog();
                dialog.Filter = "CSV files (*.csv) | *.csv";
                dialog.FileName = "EQE" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
                dialog.RestoreDirectory = true;
                if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

                var csvBuilder = new StringBuilder();

                List<string> properties = new List<string>();
                properties.Add("No");
                properties.Add("IP");
                properties.Add("EQE(%)");
                properties.Add("LuminousFlux(lm)");
                properties.Add("RadiantFlux(W)");
                properties.Add("LuminousEfficacy(lm/W)");
                properties.Add("Cx");
                properties.Add("Cy");
                properties.Add("Correlated Color Temperature(CCT)(K)");
                properties.Add("Peak Wavelength(Lp)(nm)");
                properties.Add("Excitation Purity(%)");
                properties.Add("Dominant Wavelength Color");
                properties.Add("Voltage(V)");
                properties.Add("Current(mA)");
                properties.Add("CIE2015X");
                properties.Add("CIE2015Y");
                properties.Add("CIE2015Z");
                properties.Add("CIE2015x");
                properties.Add("CIE2015y");
                properties.Add("CIE2015u");
                properties.Add("CIE2015v");

                for (int i = 380; i <= 780; i++)
                {
                    properties.Add(i.ToString());
                }
                for (int i = 380; i <= 780; i++)
                {
                    properties.Add("sp" + i.ToString());
                }

                for (int i = 0; i < properties.Count; i++)
                {
                    csvBuilder.Append(properties[i]);
                    if (i < properties.Count - 1)
                        csvBuilder.Append(',');
                }
                csvBuilder.AppendLine();

                foreach (var item in selectedItemsCopy)
                {
                    if (item is ViewResultSpectrum result)
                    {
                        csvBuilder.Append(result.Id + ",");
                        csvBuilder.Append(result.IP + ",");
                        csvBuilder.Append(result.EqePercent + ",");
                        csvBuilder.Append(result.LuminousFlux + ",");
                        csvBuilder.Append(result.RadiantFlux + ",");
                        csvBuilder.Append(result.LuminousEfficacy + ",");
                        csvBuilder.Append(result.fx + ",");
                        csvBuilder.Append(result.fy + ",");
                        csvBuilder.Append(result.fCCT + ",");
                        csvBuilder.Append(result.fLp + ",");
                        csvBuilder.Append(result.ExcitationPurityPercent + ",");
                        csvBuilder.Append(result.DominantWavelengthHex + ",");
                        csvBuilder.Append(result.V + ",");
                        csvBuilder.Append(result.I + ",");
                        csvBuilder.Append(result.fCIEx2015 + ",");
                        csvBuilder.Append(result.fCIEy2015 + ",");
                        csvBuilder.Append(result.fCIEz2015 + ",");
                        csvBuilder.Append(result.fx2015 + ",");
                        csvBuilder.Append(result.fy2015 + ",");
                        csvBuilder.Append(result.fu2015 + ",");
                        csvBuilder.Append(result.fv2015 + ",");

                        for (int i = 0; i < result.SpectralDatas.Count; i++)
                        {
                            csvBuilder.Append(result.SpectralDatas[i].AbsoluteSpectrum);
                            csvBuilder.Append(',');
                        }
                        for (int i = 0; i < result.SpectralDatas.Count; i++)
                        {
                            csvBuilder.Append(result.SpectralDatas[i].RelativeSpectrum);
                            if (i < result.SpectralDatas.Count - 1)
                                csvBuilder.Append(',');
                        }
                        csvBuilder.AppendLine();
                    }
                }
                File.WriteAllText(dialog.FileName, csvBuilder.ToString(), Encoding.UTF8);
            }
        }

        bool isstartAuto;
        private void Delete()
        {
            if (ViewResultList.SelectedItems.Count == ViewResultList.Items.Count)
            {
                ViewResultManager.DeleteAllRecords();
            }
            else
            {
                var selectedItems = ViewResultList.SelectedItems.Cast<ViewResultSpectrum>().ToList();
                ViewResultList.SelectedIndex = -1;
                ViewResultManager.DeleteSelected(selectedItems);
            }
        }

        /// <summary>
        /// Column-aware copy: extracts text from visible GridView columns for each selected item.
        /// Copies header + data rows (tab-separated) to clipboard.
        /// </summary>
        private void CopyVisibleColumns(object sender, ExecutedRoutedEventArgs e)
        {
            if (ViewResultList.View is not GridView gridView) return;
            var selectedItems = ViewResultList.SelectedItems.Cast<ViewResultSpectrum>().ToList();
            if (selectedItems.Count == 0) return;

            // Collect visible columns and their binding paths
            var visibleColumns = new List<(string Header, string BindingPath)>();
            foreach (var col in gridView.Columns)
            {
                if (col.Width == 0) continue; // hidden column
                string header = col.Header?.ToString() ?? "";
                string path = "";

                if (col.DisplayMemberBinding is System.Windows.Data.Binding binding)
                {
                    path = binding.Path?.Path ?? "";
                }
                else if (col.CellTemplate is DataTemplate dt)
                {
                    // Extract binding path from the DataTemplate's TextBlock
                    var textBlock = dt.LoadContent() as System.Windows.Controls.TextBlock;
                    if (textBlock != null)
                    {
                        var tb = System.Windows.Data.BindingOperations.GetBinding(textBlock, System.Windows.Controls.TextBlock.TextProperty);
                        if (tb != null)
                            path = tb.Path?.Path ?? "";
                    }
                    // Also check for Border (e.g. DominantWavelengthColor) - use the Tag binding
                    if (string.IsNullOrEmpty(path))
                    {
                        var border = dt.LoadContent() as System.Windows.Controls.Border;
                        if (border != null)
                        {
                            var tagBinding = System.Windows.Data.BindingOperations.GetBinding(border, FrameworkElement.TagProperty);
                            if (tagBinding != null)
                                path = tagBinding.Path?.Path ?? "";
                        }
                    }
                }

                visibleColumns.Add((header, path));
            }

            var sb = new StringBuilder();
            // Header row
            sb.AppendLine(string.Join("\t", visibleColumns.Select(c => c.Header)));

            // Data rows
            var type = typeof(ViewResultSpectrum);
            foreach (var item in selectedItems)
            {
                var values = new List<string>();
                foreach (var (_, bindingPath) in visibleColumns)
                {
                    string val = "";
                    if (!string.IsNullOrEmpty(bindingPath))
                    {
                        var prop = type.GetProperty(bindingPath, BindingFlags.Public | BindingFlags.Instance);
                        if (prop != null)
                        {
                            var v = prop.GetValue(item);
                            val = v?.ToString() ?? "";
                        }
                    }
                    values.Add(val);
                }
                sb.AppendLine(string.Join("\t", values));
            }

            try
            {
                Clipboard.SetText(sb.ToString().TrimEnd());
            }
            catch (Exception ex)
            {
                log.Warn("Failed to copy to clipboard", ex);
            }
        }



        private ObservableCollection<GridViewColumnVisibility> GridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is ContextMenu contextMenu && contextMenu.Items.Count == 0 && ViewResultList.View is GridView gridView)
                GridViewColumnVisibility.GenContentMenuGridViewColumn(contextMenu, gridView.Columns, GridViewColumnVisibilitys);
        }
        private void GridViewColumnSort(object sender, RoutedEventArgs e)
        {
            if (sender is GridViewColumnHeader gridViewColumnHeader && gridViewColumnHeader.Content != null)
            {
                Type type = typeof(ViewResultSpectrum);

                var properties = type.GetProperties();
                foreach (var property in properties)
                {
                    var attribute = property.GetCustomAttribute<DisplayNameAttribute>();
                    if (attribute != null)
                    {
                        string displayName = attribute.DisplayName;
                        displayName = Properties.Resources.ResourceManager?.GetString(displayName, Thread.CurrentThread.CurrentUICulture) ?? displayName;
                        if (displayName == gridViewColumnHeader.Content.ToString())
                        {
                            var item = GridViewColumnVisibilitys.FirstOrDefault(x => x.ColumnName.ToString() == displayName);
                            if (item != null)
                            {
                                item.IsSortD = !item.IsSortD;
                                ViewResultSpectrums.SortByProperty(property.Name, item.IsSortD);
                            }
                        }
                    }
                }
            }
        }

        public List<Scatter> ScatterPlots => ViewResultManager.ScatterPlots;
        public List<Scatter> AbsoluteScatterPlots => ViewResultManager.AbsoluteScatterPlots;

        bool MulComparison;
        Scatter? LastMulSelectComparsion;
        private bool IsShowingAbsoluteSpectrum { get; set; } = false;

        private void DrawPlot()
        {
            if (ViewResultList.SelectedIndex < 0) return;

            if (IsShowingAbsoluteSpectrum)
            {
                DrawAbsolutePlot();
                return;
            }

            wpfplot1.Plot.Axes.SetLimitsX(380, 780);
            wpfplot1.Plot.Axes.SetLimitsY(-0.05, 1);
            wpfplot1.Plot.Axes.Bottom.Min = ViewResultSpectrums[ViewResultList.SelectedIndex].fSpect1;
            wpfplot1.Plot.Axes.Bottom.Max = ViewResultSpectrums[ViewResultList.SelectedIndex].fSpect2;
            wpfplot1.Plot.Axes.Left.Min = -0.05;
            wpfplot1.Plot.Axes.Left.Max = 1;

            if (ScatterPlots.Count > 0)
            {
                if (MulComparison)
                {
                    if (LastMulSelectComparsion != null)
                    {
                        LastMulSelectComparsion.Color = Color.FromColor(System.Drawing.Color.DarkGoldenrod);
                        LastMulSelectComparsion.LineWidth = 1;
                        LastMulSelectComparsion.MarkerSize = 1;
                    }

                    LastMulSelectComparsion = ScatterPlots[ViewResultList.SelectedIndex];
                    LastMulSelectComparsion.LineWidth = 3;
                    LastMulSelectComparsion.MarkerSize = 3;
                    LastMulSelectComparsion.Color = Color.FromColor(System.Drawing.Color.Red);
                    wpfplot1.Plot.PlottableList.Add(LastMulSelectComparsion);

                }
                else
                {
                    var temp = ScatterPlots[ViewResultList.SelectedIndex];
                    temp.Color = Color.FromColor(System.Drawing.Color.DarkGoldenrod);
                    temp.LineWidth = 1;
                    temp.MarkerSize = 1;

                    wpfplot1.Plot.PlottableList.Add(temp);
                    wpfplot1.Plot.Remove(LastMulSelectComparsion);
                    LastMulSelectComparsion = temp;

                }
            }

            wpfplot1.Refresh();
        }

        private void DrawAbsolutePlot()
        {
            if (ViewResultList.SelectedIndex < 0) return;

            wpfplot2.Plot.Axes.SetLimitsX(380, 780);
            wpfplot2.Plot.Axes.Bottom.Min = ViewResultSpectrums[ViewResultList.SelectedIndex].fSpect1;
            wpfplot2.Plot.Axes.Bottom.Max = ViewResultSpectrums[ViewResultList.SelectedIndex].fSpect2;
            wpfplot2.Plot.Axes.Left.Min = -0.05;
            wpfplot2.Plot.Axes.Left.Max = double.NaN;
            if (AbsoluteScatterPlots.Count > 0)
            {
                if (MulComparison)
                {
                    if (LastMulSelectComparsion != null)
                    {
                        LastMulSelectComparsion.Color = Color.FromColor(System.Drawing.Color.DarkGoldenrod);
                        LastMulSelectComparsion.LineWidth = 1;
                        LastMulSelectComparsion.MarkerSize = 1;
                    }

                    LastMulSelectComparsion = AbsoluteScatterPlots[ViewResultList.SelectedIndex];
                    LastMulSelectComparsion.LineWidth = 3;
                    LastMulSelectComparsion.MarkerSize = 3;
                    LastMulSelectComparsion.Color = Color.FromColor(System.Drawing.Color.Red);
                    wpfplot2.Plot.PlottableList.Add(LastMulSelectComparsion);
                }
                else
                {
                    var temp = AbsoluteScatterPlots[ViewResultList.SelectedIndex];
                    temp.Color = Color.FromColor(System.Drawing.Color.DarkGoldenrod);
                    temp.LineWidth = 1;
                    temp.MarkerSize = 1;

                    wpfplot2.Plot.PlottableList.Add(temp);
                    wpfplot2.Plot.Remove(LastMulSelectComparsion);
                    LastMulSelectComparsion = temp;
                }
            }

            wpfplot2.Refresh();
        }

        private void ToggleSpectrumType_Click(object sender, RoutedEventArgs e)
        {
            IsShowingAbsoluteSpectrum = !IsShowingAbsoluteSpectrum;

            if (IsShowingAbsoluteSpectrum)
            {
                wpfplot1.Visibility = Visibility.Collapsed;
                wpfplot2.Visibility = Visibility.Visible;
                SpectrumTypeText.Text = "绝对光谱";
            }
            else
            {
                wpfplot1.Visibility = Visibility.Visible;
                wpfplot2.Visibility = Visibility.Collapsed;
                SpectrumTypeText.Text = "相对光谱";
            }

            ReDrawPlot();
        }

        private void ReDrawPlot()
        {
            if (ViewResultList.SelectedIndex < 0) return;

            if (IsShowingAbsoluteSpectrum)
            {
                wpfplot2.Plot.Clear();
                LastMulSelectComparsion = null;
                if (MulComparison)
                {
                    ViewResultList.SelectedIndex = ViewResultList.Items.Count > 0 && ViewResultList.SelectedIndex == -1 ? 0 : ViewResultList.SelectedIndex;
                    for (int i = 0; i < ViewResultSpectrums.Count; i++)
                    {
                        if (i == ViewResultList.SelectedIndex) continue;
                        var plot = AbsoluteScatterPlots[i];
                        plot.Color = Color.FromColor(System.Drawing.Color.DarkGoldenrod);
                        plot.LineWidth = 1;
                        plot.MarkerSize = 1;
                        wpfplot2.Plot.PlottableList.Add(plot);
                    }
                }
                DrawAbsolutePlot();
            }
            else
            {
                wpfplot1.Plot.Clear();
                // Re-add spectrum color bar after clearing
                AddSpectrumColorBar(wpfplot1);
                LastMulSelectComparsion = null;
                if (MulComparison)
                {
                    ViewResultList.SelectedIndex = ViewResultList.Items.Count > 0 && ViewResultList.SelectedIndex == -1 ? 0 : ViewResultList.SelectedIndex;
                    for (int i = 0; i < ViewResultSpectrums.Count; i++)
                    {
                        if (i == ViewResultList.SelectedIndex) continue;
                        var plot = ScatterPlots[i];
                        plot.Color = Color.FromColor(System.Drawing.Color.DarkGoldenrod);
                        plot.LineWidth = 1;
                        plot.MarkerSize = 1;
                        wpfplot1.Plot.PlottableList.Add(plot);
                    }
                }
                DrawPlot();
            }
        }

        private void listView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListView listview && listview.SelectedIndex > -1)
            {
                DrawPlot();
                listView2.ItemsSource = ViewResultSpectrums[listview.SelectedIndex].SpectralDatas;
                // Always draw CIE point on selection
                DrawCIEPoinr(ViewResultSpectrums[listview.SelectedIndex].fx, ViewResultSpectrums[listview.SelectedIndex].fy, ViewResultSpectrums[listview.SelectedIndex].fu, ViewResultSpectrums[listview.SelectedIndex].fv);
                this.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, (ThreadStart)delegate () { image.Source = pic1931; });
            }
        }

        private void listView1_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete && ViewResultList.SelectedIndex > -1)
            {
                int temp = ViewResultList.SelectedIndex;
                ViewResultSpectrums.RemoveAt(ViewResultList.SelectedIndex);

                if (ViewResultList.Items.Count > 0)
                {
                    ViewResultList.SelectedIndex = temp - 1; ;
                    DrawPlot();
                }
                else
                {
                    wpfplot1.Plot.Clear();
                    AddSpectrumColorBar(wpfplot1);
                    wpfplot1.Refresh();
                }

            }
        }

        Marker markerPlot1;
        private void listView2_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            wpfplot1.Plot.Remove(markerPlot1);
            if (listView2.SelectedIndex > -1)
            {
                markerPlot1 = new Marker
                {
                    X = listView2.SelectedIndex + 380,
                    Y = ViewResultSpectrums[ViewResultList.SelectedIndex].fPL[listView2.SelectedIndex * 10],
                    MarkerShape = MarkerShape.FilledCircle,
                    MarkerSize = 10f,
                    Color = Color.FromColor(System.Drawing.Color.Orange),
                };
                wpfplot1.Plot.PlottableList.Add(markerPlot1);
            }
            wpfplot1.Refresh();
        }

        private void ContextMenu1_Opened(object sender, RoutedEventArgs e)
        {

        }

        private void DominantColor_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is string hex)
            {
                try
                {
                    Clipboard.SetText(hex);
                }
                catch (Exception ex)
                {
                    log.Error("Failed to copy color to clipboard", ex);
                }
            }
        }

        /// <summary>
        /// Adds a visible spectrum rainbow color bar to the bottom of the chart.
        /// Uses ScottPlot Rectangle annotations for each wavelength step.
        /// </summary>
        private void AddSpectrumColorBar(ScottPlot.WPF.WpfPlot plotControl)
        {
            // Add colored rectangles from 380 to 780 nm
            for (int wl = 380; wl < 780; wl += 2)
            {
                var color = WavelengthToColor.Convert(wl);
                var scottColor = new ScottPlot.Color(color.R, color.G, color.B);

                var rect = plotControl.Plot.Add.Rectangle(wl, wl + 2, -0.01, -0.06);
                rect.FillColor = scottColor;
                rect.LineColor = scottColor;
                rect.LineWidth = 0;
            }
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            log.Debug($"设置 SP100 参数: IsEnabled={SetEmissionSP100Config.Instance.IsEnabled}, nStartPos={SetEmissionSP100Config.Instance.nStartPos}, nEndPos={SetEmissionSP100Config.Instance.nEndPos}, dMeanThreshold={SetEmissionSP100Config.Instance.dMeanThreshold}");
            int ret = Spectrometer.CM_SetEmissionSP100(SpectrometerHandle, SetEmissionSP100Config.Instance.IsEnabled, SetEmissionSP100Config.Instance.nStartPos, SetEmissionSP100Config.Instance.nEndPos, SetEmissionSP100Config.Instance.dMeanThreshold);
            if (ret == 1)
            {
                log.Info("SP100 参数设置成功");
                MessageBox.Show("SP100 设置成功");
            }
            else
            {
                string errorMsg = Spectrometer.GetErrorMessage(ret);
                log.Error($"SP100 参数设置失败: {errorMsg}");
                MessageBox.Show($"SP100 设置失败: {errorMsg}");
            }
        }

        private void GridViewColumnSort1(object sender, RoutedEventArgs e)
        {

        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            ConfigService.Instance.SaveConfigs();
        }

        private void GridSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            ViewResultList.Height = ListRow2.ActualHeight - 32;
            ListRow2.Height = GridLength.Auto;
            ListRow1.Height = new GridLength(1, GridUnitType.Star);
        }

        private void ButtonMul_Click(object sender, RoutedEventArgs e)
        {
            MulComparison = !MulComparison;
            if (ViewResultList.SelectedIndex <= -1)
            {
                if (ViewResultList.Items.Count == 0)
                    return;
                ViewResultList.SelectedIndex = 0;
            }
            ReDrawPlot();
        }

        private void EqeCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            bool isEnabled = EqeCheckBox.IsChecked == true;
            UpdateEqeColumnsVisibility(isEnabled);
        }

        private void UpdateEqeColumnsVisibility(bool eqeEnabled)
        {
            if (!IsInitialized) return;

            EqePanel.Visibility = eqeEnabled ? Visibility.Visible : Visibility.Collapsed;
            // double.NaN = auto-size (visible), 0 = hidden
            double width = eqeEnabled ? double.NaN : 0;
            ColEqe.Width = width;
            ColLuminousFlux.Width = width;
            ColRadiantFlux.Width = width;
            ColLuminousEfficacy.Width = width;
            ColVoltage.Width = width;
            ColCurrent.Width = width;
            ColRecalculated.Width = width;
            // Hide brightness column in 光通量模式
            ColBrightness.Width = eqeEnabled ? 0 : double.NaN;
        }

        private void CalculateEqe_Click(object sender, RoutedEventArgs e)
        {
            float voltage = MainWindowConfig.Instance.EqeVoltage;
            float currentMA = MainWindowConfig.Instance.EqeCurrentMA;

            var selectedItems = ViewResultList.SelectedItems.Cast<ViewResultSpectrum>().ToList();
            if (selectedItems.Count == 0)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "请先选择要重新计算的数据", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            foreach (var item in selectedItems)
            {
                item.CalculateEqeParams(voltage, currentMA);
                item.IsRecalculated = true;
                ViewResultManager.UpdateEqeFields(item, isRecalculated: true);
            }
        }

        private void StatusBarConnectionType_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            try
            {
                // Open Device Manager on double-click (useful for USB connections)
                Process.Start(new ProcessStartInfo("devmgmt.msc") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                log.Warn("Failed to open Device Manager", ex);
            }
        }

        private void StatusBarSN_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (!string.IsNullOrEmpty(Manager.SerialNumber) && Manager.SerialNumber != "---")
            {
                try
                {
                    Clipboard.SetText(Manager.SerialNumber);
                    log.Debug($"序列号已复制到剪贴板: {Manager.SerialNumber}");
                }
                catch (Exception ex)
                {
                    log.Warn("Failed to copy SN to clipboard", ex);
                }
            }
        }

        public void Dispose()
        {
            Manager.SmuController.Close();
            logOutput?.Dispose();
            GC.SuppressFinalize(this);
        }

        private void SmuConnect_Click(object sender, RoutedEventArgs e)
        {
            if (Manager.SmuController.IsOpen)
            {
                Manager.SmuController.Close();
                ButtonSmuConnect.Content = "连接源表";
                ButtonSmuMeasure.IsEnabled = false;
                log.Info("SMU 已断开");
            }
            else
            {
                bool ok = Manager.SmuController.Open();
                if (ok)
                {
                    ButtonSmuConnect.Content = "断开源表";
                    ButtonSmuMeasure.IsEnabled = true;
                    log.Info($"SMU 连接成功: {Manager.SmuController.Version}");
                }
                else
                {
                    MessageBox.Show(Application.Current.GetActiveWindow(), "源表连接失败，请检查设备名称和连接方式", "连接失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void SmuMeasure_Click(object sender, RoutedEventArgs e)
        {
            if (!Manager.SmuController.IsOpen) return;
            Manager.SmuController.ApplySettings();
            bool ok = Manager.SmuController.MeasureData();
            if (ok)
            {
                var (voltage, currentMA) = Manager.SmuController.GetVI();
                MainWindowConfig.Instance.EqeVoltage = voltage;
                MainWindowConfig.Instance.EqeCurrentMA = currentMA;
                log.Debug($"SMU 测量结果: V={voltage}, I={currentMA}mA");
            }
            else
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "源表读取失败", "读取失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }


    }


}
