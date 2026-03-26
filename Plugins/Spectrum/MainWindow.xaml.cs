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
            Config.SetWindow(this);
            this.SizeChanged += (s, e) => Config.SetConfig(this);
            this.ApplyCaption();
            this.SetWindowFull(Config);
            this.Closed += (s, e) =>
            {
                Manager.Disconnect();
            };
            this.Title += " - " + Assembly.GetAssembly(typeof(MainWindow))?.GetName().Version?.ToString() ?? "";
        }
        private LogOutput? logOutput;

        private void Window_Initialized(object sender, EventArgs e)
        {
            log.Info("初始化cvCamera日志");
            cvCameraCSLib.InitResource(IntPtr.Zero, IntPtr.Zero);

            ViewResultManager.ListView = ViewResultList;

            MenuManager.GetInstance().LoadMenuForWindow("Spectrum", menu);

            if (MainWindowConfig.Instance.LogControlVisibility)
            {
                logOutput = new LogOutput("%date{HH:mm:ss} [%thread] %-5level %message%newline");
                LogGrid.Children.Add(logOutput);
            }

            image.Source = src1931?.ToBitmapSource();
            ComboBoxSpectrometerType.ItemsSource = from e1 in Enum.GetValues(typeof(SpectrometerType)).Cast<SpectrometerType>()
                                                   select new KeyValuePair<SpectrometerType, string>(e1, e1.ToString());

            cvCameraCSLib.InitResource(IntPtr.Zero, IntPtr.Zero);

            SetEmissionSP100Config.Instance.EditChanged += (s, e) =>
            {
                if (SpectrometerHandle != IntPtr.Zero)
                {
                    log.Info($"CM_SetEmissionSP100:IsEnabled{SetEmissionSP100Config.Instance.IsEnabled},nStartPos{SetEmissionSP100Config.Instance.nStartPos},nEndPos{SetEmissionSP100Config.Instance.nEndPos},dMeanThreshold{SetEmissionSP100Config.Instance.dMeanThreshold}");
                    int ret = Spectrometer.CM_SetEmissionSP100(SpectrometerHandle, SetEmissionSP100Config.Instance.IsEnabled, SetEmissionSP100Config.Instance.nStartPos, SetEmissionSP100Config.Instance.nEndPos, SetEmissionSP100Config.Instance.dMeanThreshold);
                    log.Info($"CM_SetEmissionSP100 ret:{ret}");
                }

            };
            string[] portNames = SerialPort.GetPortNames();
            List<int> BaudRates = new List<int>() { 9600,115200, 38400, 300, 600, 1200, 2400, 4800, 14400, 19200, 57600 };
            ComboBoxPort.ItemsSource = portNames;
            ComboBoxSerial.ItemsSource = BaudRates;
            ComboBoxShutterPort.ItemsSource = portNames;
            ComboBoxShutterSerial.ItemsSource = BaudRates;

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
            ViewResultList.CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, ListViewUtils.Copy, (s, e) => e.CanExecute = true));

            this.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, (ThreadStart)delegate () { image.Source = pic1931; });

            UpdateEqeColumnsVisibility(MainWindowConfig.Instance.EqeEnabled);
        }

        float fIntTime = 0;
        int testid = 0;

        public static int MyCallback(IntPtr strText, int nLen)
        {
            string text = Marshal.PtrToStringAnsi(strText, nLen);
            log.Info("Callback: " + text);
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
                log.Info($"CM_Emission_Init:{iR}");
                if (iR == 1)
                {
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
                                log.Info($"Spectrometer SN: {sn}");
                            }
                            else
                            {
                                Manager.SerialNumber = "Unknown";
                            }
                        }
                        else
                        {
                            log.Warn($"CM_Emission_GetAllSN returned {snRet}");
                            Manager.SerialNumber = "Unknown";
                        }
                    }
                    catch (Exception snEx)
                    {
                        log.Warn("Failed to read SN", snEx);
                        Manager.SerialNumber = "Unknown";
                    }
                    Manager.LoadCalibrationConfig();

                    iR = Spectrometer.CM_Emission_LoadWavaLengthFile(SpectrometerHandle, Manager.WavelengthFile);
                    log.Info($"CM_Emission_LoadWavaLengthFile{Manager.WavelengthFile},ret{iR}");
                    iR = Spectrometer.CM_Emission_LoadMagiudeFile(SpectrometerHandle, Manager.MaguideFile);
                    log.Info($"CM_Emission_LoadMagiudeFile{Manager.MaguideFile},ret{iR}");

                    log.Info($"CM_SetEmissionSP100:IsEnabled{SetEmissionSP100Config.Instance.IsEnabled},nStartPos{SetEmissionSP100Config.Instance.nStartPos},nEndPos{SetEmissionSP100Config.Instance.nEndPos},dMeanThreshold{SetEmissionSP100Config.Instance.dMeanThreshold}");
                    int ret = Spectrometer.CM_SetEmissionSP100(SpectrometerHandle, SetEmissionSP100Config.Instance.IsEnabled, SetEmissionSP100Config.Instance.nStartPos, SetEmissionSP100Config.Instance.nEndPos, SetEmissionSP100Config.Instance.dMeanThreshold);
                    log.Info($"CM_SetEmissionSP100 ret:{ret}");

                    State2.Text = Spectrum.Properties.Resources.连接成功;
                    State4.Text = "SP-100";
                    button3.IsEnabled = true;
                    button4.IsEnabled = true;
                    button5.IsEnabled = true;
                    button6.IsEnabled = true;
                }
                else
                {
                    Manager.IsConnected = false;
                    string logDir = "log";
                    string logPrefix = "Spectrum_Main";
                    string logSuffix = ".log";

                    // 获取目录下所有符合条件的log文件
                    var files = Directory.GetFiles(logDir, $"{logPrefix}*{logSuffix}");
                    if (files.Length == 0)
                    {
                        log.Info("未找到任何日志文件");
                        return;
                    }

                    // 按最后修改时间降序排序，取最新的文件
                    var latestFile = files.Select(f => new FileInfo(f))
                                          .OrderByDescending(f => f.LastWriteTime)
                                          .First().FullName;

                    string keyword = "序列号验证失败！请联系厂商";
                    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

                    using (var fs = new FileStream(latestFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var sr = new StreamReader(fs,Encoding.GetEncoding("GB2312")))
                    {
                        string line;   
                        bool containsKeyword = false;
                        while ((line = sr.ReadLine()) != null)
                        {
                            if (line.Contains(keyword))
                            {
                                containsKeyword = true;
                                log.Info(line.Trim());
                                MessageBox.Show(Application.Current.GetActiveWindow(), line.Trim());
                                break;
                            }
                        }
                        if (!containsKeyword)
                            MessageBox.Show(Spectrum.Properties.Resources.连接失败);
                    }
                }
            }
            catch(Exception ex)
            {
                log.Error(ex);
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
                        log.Info("OpenShutter");
                       await  Manager.ShutterController.OpenShutter();
                    }
                    int ret = Spectrometer.CM_Emission_DarkStorage(SpectrometerHandle, Manager.IntTime, Manager.Average, 0, Manager.fDarkData);
                    if (Manager.ShutterController.IsConnected)
                    {
                        log.Info("CloseShutter");
                        await Manager.ShutterController.CloseShutter();
                    }
                    log.Info($"CM_Emission_DarkStorage {ret}");
                    if (ret == 1)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show(Application.Current.GetActiveWindow(), "校零成功");
                        });
                    }
                    else
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show(Application.Current.GetActiveWindow(), "校零失败");
                        });
                    }
                    IsRun = false;
                    SetOperationButtonsEnabled(true);
                }
                catch (Exception ex)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show(Application.Current.GetActiveWindow(), "校零异常" + ex.Message);
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
            log.Info($"当前自动积分时间: {time},光谱强度:{spectum}");
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
                    log.Info($"CM_Emission_GetAutoTime: {ret}");
                    if (ret == 1)
                    {
                        log.Info($"自动积分：{fIntTime}");
                        Manager.IntTime = fIntTime;
                    }
                    else
                    {
                        log.Info("自动积分获取失败：" + ret);
                    }
                }
                else
                {
                    ret = Spectrometer.CM_Emission_GetAutoTimeEx(SpectrometerHandle, ref fIntTime, Manager.IntTimeConfig.IntLimitTime, Manager.IntTimeConfig.AutoIntTimeB, Manager.Max, MyAutoTimeCallback);
                    log.Info($"CM_Emission_GetAutoTimeEx: {ret}");

                    if (ret == 1)
                    {
                        log.Info($"自动积分：{fIntTime}");
                        Manager.IntTime = fIntTime;
                    }
                    else
                    {
                        log.Info("自动积分获取失败：" + ret);
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
                if (Manager.ShutterController.IsConnected)
                {
                    log.Info("OpenShutter");
                    await Manager.ShutterController.OpenShutter();
                }
                int ret = Spectrometer.CM_Emission_DarkStorage(SpectrometerHandle, Manager.IntTime, Manager.Average, 0, Manager.fDarkData);
                if (Manager.ShutterController.IsConnected)
                {
                    log.Info("CloseShutter");
                    await Manager.ShutterController.CloseShutter();
                }
                log.Info($"CM_Emission_DarkStorage {ret}");
            }

            if (Manager.EnableAutoIntegration)
            {

                if (Manager.IntTimeConfig.IsOldVersion)
                {
                    ret = Spectrometer.CM_Emission_GetAutoTime(SpectrometerHandle, ref fIntTime, Manager.IntTimeConfig.IntLimitTime, Manager.IntTimeConfig.AutoIntTimeB, (int)Manager.MaxPercent);
                    log.Info($"CM_Emission_GetAutoTime: {ret}");
                    if (ret == 1)
                    {
                        log.Info($"自动积分：{fIntTime}");
                        Manager.IntTime = fIntTime;
                    }
                    else
                    {
                        log.Info("自动积分获取失败：" + ret);
                        IsRun = false;
                        return;
                    }
                }
                else
                {
                    ret = Spectrometer.CM_Emission_GetAutoTimeEx(SpectrometerHandle, ref fIntTime, Manager.IntTimeConfig.IntLimitTime, Manager.IntTimeConfig.AutoIntTimeB, Manager.Max, MyAutoTimeCallback);
                    log.Info($"CM_Emission_GetAutoTimeEx: {ret}");

                    if (ret == 1)
                    {
                        log.Info($"自动积分：{fIntTime}");
                        Manager.IntTime = fIntTime;
                    }
                    else
                    {
                        log.Info("自动积分获取失败：" + ret);
                        IsRun = false;
                        return;
                    }
                }
            }


            if (Manager.EnableAdaptiveAutoDark)
            {
                ret = Spectrometer.CM_Emission_AutoDarkStorage(SpectrometerHandle, Manager.IntTime, Manager.Average, 0, Manager.fDarkData);
                log.Info($"CM_Emission_AutoDarkStorage: {ret}");
                if (ret == 0)
                {
                    isstartAuto = false;
                    MessageBox.Show("请先做一次自适应校零");
                    IsRun = false;
                    return;
                }
            }

            float fDx = 0;
            float fDy = 0;
            COLOR_PARA cOLOR_PARA = new COLOR_PARA();

            if (Manager.GetDataConfig.IsSyncFrequencyEnabled)
            {
                float fIntTime = Manager.IntTime;
                ret = Spectrometer.CM_Emission_GetDataSyncfreq(SpectrometerHandle, 0, Manager.GetDataConfig.Syncfreq, Manager.GetDataConfig.SyncfreqFactor, ref fIntTime, Manager.Average, Manager.GetDataConfig.FilterBW, Manager.fDarkData, fDx, fDy, Manager.GetDataConfig.SetWL1, Manager.GetDataConfig.SetWL2, ref cOLOR_PARA);
                log.Info($"CM_Emission_GetDataSyncfreq: {ret}");

                if (Manager.EnableAutoIntegration)
                    Manager.IntTime = fIntTime;
            }
            else
            {
                ret = Spectrometer.CM_Emission_GetData(SpectrometerHandle, 0, Manager.IntTime, Manager.Average, Manager.GetDataConfig.FilterBW, Manager.fDarkData, fDx, fDy, Manager.GetDataConfig.SetWL1, Manager.GetDataConfig.SetWL2, ref cOLOR_PARA);
                log.Info($"CM_Emission_GetData: {ret}");
                if (ret == -13007)
                {
                    ret = Spectrometer.CM_Emission_GetData(SpectrometerHandle, 0, Manager.IntTime, Manager.Average, Manager.GetDataConfig.FilterBW, Manager.fDarkData, fDx, fDy, Manager.GetDataConfig.SetWL1, Manager.GetDataConfig.SetWL2, ref cOLOR_PARA);
                    log.Info($"CM_Emission_ReGetData: {ret}");
                }
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
                        var latest = ViewResultManager.Config.OrderByType == SqlSugar.OrderByType.Desc
                            ? ViewResultSpectrums.FirstOrDefault()
                            : ViewResultSpectrums.LastOrDefault();
                        latest?.CalculateEqeParams(MainWindowConfig.Instance.EqeVoltage, MainWindowConfig.Instance.EqeCurrentMA);
                    }
                });
            }
            else
            {
                errornum++;
                log.Info($"{ret} SA_GetSpectum 失败!");
            }
            IsRun = false;
        }

        int errornum = 0;

        public async Task ReConnet()
        {
            for (int i = 0; i < 6; i++)
            {
                log.Info($"Try ReConnet {i}");
                int ret = Spectrometer.CM_Emission_Close(Manager.Handle);
                log.Debug($"CM_Emission_Close {ret}");
                ret = Spectrometer.CM_ReleaseEmission(Manager.Handle);
                log.Debug($"CM_ReleaseEmission {ret}");
                await Task.Delay(200);
                Manager.Handle = Spectrometer.CM_CreateEmission(0, MyCallback);
                int ncom = 0;
                if (Manager.Config.IsComPort)
                {
                     ncom = int.Parse(Manager.Config.SzComName.Replace("COM", ""));
                }
                int iR = Spectrometer.CM_Emission_Init(SpectrometerHandle, ncom, Manager.Config.BaudRate);
                log.Debug($"CM_Emission_Init:{iR}");
                if (iR == 1)
                {
                    Manager.IsConnected = true;
                    iR = Spectrometer.CM_Emission_LoadWavaLengthFile(SpectrometerHandle, Manager.WavelengthFile);
                    log.Debug($"CM_Emission_LoadWavaLengthFile{Manager.WavelengthFile},ret{iR}");
                    iR = Spectrometer.CM_Emission_LoadMagiudeFile(SpectrometerHandle, Manager.MaguideFile);
                    log.Debug($"CM_Emission_LoadMagiudeFile{Manager.MaguideFile},ret{iR}");

                    log.Debug($"CM_SetEmissionSP100:IsEnabled{SetEmissionSP100Config.Instance.IsEnabled},nStartPos{SetEmissionSP100Config.Instance.nStartPos},nEndPos{SetEmissionSP100Config.Instance.nEndPos},dMeanThreshold{SetEmissionSP100Config.Instance.dMeanThreshold}");
                    ret = Spectrometer.CM_SetEmissionSP100(SpectrometerHandle, SetEmissionSP100Config.Instance.IsEnabled, SetEmissionSP100Config.Instance.nStartPos, SetEmissionSP100Config.Instance.nEndPos, SetEmissionSP100Config.Instance.dMeanThreshold);
                    log.Debug($"CM_SetEmissionSP100 ret:{ret}");

                    log.Info($"ReConnet Sucess");
                    break;
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
                button4.IsEnabled = enabled;
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
            log.Info($"CM_Emission_Init_Auto_Dark Start");
            SetOperationButtonsEnabled(false);
            Task.Run(() =>
            {
                IsRun = true;
                int ret = Spectrometer.CM_Emission_Init_Auto_Dark(SpectrometerHandle, Manager.AutodarkParam.fTimeStart, Manager.AutodarkParam.nStepTime, Manager.AutodarkParam.nStepCount, Manager.Average);
                log.Info($"CM_Emission_Init_Auto_Dark {ret}");
                IsRun = false;
                SetOperationButtonsEnabled(true);
                if (ret == 1)
                {
                    MessageBox.Show("自适应校零成功");
                }
                else
                {
                    MessageBox.Show("自适应校零失败");
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
            button4.IsEnabled = false;
            button5.IsEnabled = false;
            ButtonAutoInt.IsEnabled = false;
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
                            button4.IsEnabled = true;
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
            button4.IsEnabled = true;
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

            using (var dialog = new System.Windows.Forms.SaveFileDialog())
            {
                dialog.Filter = "CSV files (*.csv) | *.csv";
                dialog.FileName = DateTime.Now.ToString("光谱仪导出yyyy-MM-dd-HH-mm-ss");
                dialog.RestoreDirectory = true;
                if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;


                var csvBuilder = new StringBuilder();

                List<string> properties = new List<string>();
                properties.Add("序号");
                properties.Add("IP");
                properties.Add("亮度Lv(cd/m2)");
                properties.Add("蓝光");
                properties.Add("CIEx");
                properties.Add("CIEy");
                properties.Add("CIEz");
                properties.Add("色度x");
                properties.Add("色度y");
                properties.Add("色度u");
                properties.Add("色度v");
                properties.Add("相关色温(K)");
                properties.Add("主波长Ld(nm)");
                properties.Add("色纯度(%)");
                properties.Add("峰值波长Lp(nm");
                properties.Add("显色性指数Ra");
                properties.Add("半波宽");
                properties.Add("兴奋纯度");
                properties.Add("主波长颜色");
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
                // 写入列头
                for (int i = 0; i < properties.Count; i++)
                {
                    // 添加列名
                    csvBuilder.Append(properties[i]);

                    // 如果不是最后一列，则添加逗号
                    if (i < properties.Count - 1)
                        csvBuilder.Append(',');
                }
                // 添加换行符
                csvBuilder.AppendLine();

                var selectedItemsCopy = new List<object>();
                foreach (var item in ViewResultSpectrums)
                {
                    selectedItemsCopy.Add(item);
                }

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
                        csvBuilder.Append(result.fPur + ",");
                        csvBuilder.Append(result.fLp + ",");
                        csvBuilder.Append(result.fRa + ",");
                        csvBuilder.Append(result.fHW + ",");
                        csvBuilder.Append(result.ExcitationPurity + ",");
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

            };



        }

        bool isstartAuto;
        private void Delete()
        {
            if (ViewResultList.SelectedItems.Count == ViewResultList.Items.Count)
                ViewResultSpectrums.Clear();
            else
            {
                ViewResultList.SelectedIndex = -1;
                foreach (var item in ViewResultList.SelectedItems.Cast<ViewResultSpectrum>().ToList())
                    ViewResultSpectrums.Remove(item);
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
            log.Info($"CM_SetEmissionSP100:IsEnabled{SetEmissionSP100Config.Instance.IsEnabled},nStartPos{SetEmissionSP100Config.Instance.nStartPos},nEndPos{SetEmissionSP100Config.Instance.nEndPos},dMeanThreshold{SetEmissionSP100Config.Instance.dMeanThreshold}");
            int ret = Spectrometer.CM_SetEmissionSP100(SpectrometerHandle, SetEmissionSP100Config.Instance.IsEnabled, SetEmissionSP100Config.Instance.nStartPos, SetEmissionSP100Config.Instance.nEndPos, SetEmissionSP100Config.Instance.dMeanThreshold);
            log.Info($"CM_SetEmissionSP100 ret:{ret}");
            string a = ret != 1 ? "失败" : "成功";
            MessageBox.Show("CM_SetEmissionSP100："  + a );
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
            EqePanel.Visibility = eqeEnabled ? Visibility.Visible : Visibility.Collapsed;
            // double.NaN = auto-size (visible), 0 = hidden
            double width = eqeEnabled ? double.NaN : 0;
            ColEqe.Width = width;
            ColLuminousFlux.Width = width;
            ColRadiantFlux.Width = width;
            ColLuminousEfficacy.Width = width;
            ColVoltage.Width = width;
            ColCurrent.Width = width;
        }

        private void CalculateEqe_Click(object sender, RoutedEventArgs e)
        {
            float voltage = MainWindowConfig.Instance.EqeVoltage;
            float currentMA = MainWindowConfig.Instance.EqeCurrentMA;

            foreach (var item in ViewResultSpectrums)
            {
                item.CalculateEqeParams(voltage, currentMA);
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
                    log.Info($"SN copied to clipboard: {Manager.SerialNumber}");
                }
                catch (Exception ex)
                {
                    log.Warn("Failed to copy SN to clipboard", ex);
                }
            }
        }

        public void Dispose()
        {
            logOutput?.Dispose();
            GC.SuppressFinalize(this);
        }


    }


}
