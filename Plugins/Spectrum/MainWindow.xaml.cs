#pragma warning disable CA1805,CA1822,CA1863,CS8604,CS8625
using AvalonDock.Layout;
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Cie;
using ColorVision.Themes;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using ColorVision.UI.LogImp;
using ColorVision.UI.Menus;
using ColorVision.UI.Sorts;
using cvColorVision;
using log4net;
using ScottPlot;
using ScottPlot.Plottables;
using Spectrum.Calibration;
using Spectrum.Configs;
using Spectrum.Data;
using Spectrum.Layout;
using Spectrum.License;
using Spectrum.Models;
using Spectrum.TimedButtons;
using Spectrum.Update;
using SpectrumResources = Spectrum.Properties.Resources;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
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
            if (MainWindow.Instance is { IsLoaded: true } existingWindow)
            {
                if (existingWindow.WindowState == WindowState.Minimized)
                    existingWindow.WindowState = WindowState.Normal;
                existingWindow.Activate();
                return;
            }

            new MainWindow().Show();
        }
    }

    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : System.Windows.Window, IDisposable
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
        private readonly CancellationTokenSource windowLifetimeCancellation = new();
        private Task<ViewResultManager>? viewResultInitializationTask;
        private Task<string[]>? serialPortDiscoveryTask;
        private Task<TimeSpan>? cvCameraInitializationTask;
        private Task? deferredInitializationTask;
        private Task? smuAutoConnectTask;
        private Task? closePreparationTask;
        private MeasurementAdmissionPause? measurementPause;
        private int latestSessionMeasurementResultId;
        private string latestSessionMagnitudeFile = string.Empty;
        private string latestSessionMagnitudeFileSha256 = string.Empty;
        private bool absoluteSpectrumPlotInitialized;
        private bool isPreparingClose;
        private bool isClosePrepared;
        private bool calibrationReloadInProgress;
        private bool auxiliaryShutdownWarningShown;
        private bool disposed;
        public static SpectrometerManager Manager => SpectrometerManager.Instance;

        /// <summary>
        /// Static reference to current MainWindow instance for menu items access.
        /// </summary>
        internal static MainWindow? Instance { get; private set; }

        /// <summary>
        /// Layout manager for AvalonDock persistence, reset, and panel visibility.
        /// </summary>
        internal DockLayoutManager? LayoutManager { get; private set; }

        internal bool TryGetCorrectionResult(out ViewResultSpectrum? result, out string reason)
        {
            if (continuousMeasurementTask is { IsCompleted: false } || Manager.IsBusy)
            {
                result = null;
                reason = "光谱仪正在测量或执行其他操作，请完成后再打开光谱校正。";
                return false;
            }

            if (latestSessionMeasurementResultId <= 0)
            {
                result = null;
                reason = "当前会话还没有可用于校正的测量结果，请先用当前标定文件完成一次正常测量。";
                return false;
            }

            result = ViewResultSpectrums.FirstOrDefault(item => item.Id == latestSessionMeasurementResultId);
            if (result == null)
            {
                reason = "当前会话最近一次测量结果已不在列表中，请重新测量后再校正。";
                return false;
            }

            if (string.IsNullOrWhiteSpace(Manager.MaguideFile))
            {
                result = null;
                reason = "当前标定组没有幅值 DAT，请先配置后重新测量。";
                return false;
            }

            string currentMagnitudeFile;
            string currentMagnitudeHash;
            try
            {
                currentMagnitudeFile = Path.GetFullPath(Manager.MaguideFile);
                currentMagnitudeHash = ComputeMagnitudeFileSha256(currentMagnitudeFile);
            }
            catch (Exception ex)
            {
                result = null;
                reason = $"无法读取当前幅值 DAT：{ex.GetBaseException().Message}";
                return false;
            }

            if (!string.Equals(currentMagnitudeFile, latestSessionMagnitudeFile, StringComparison.OrdinalIgnoreCase))
            {
                result = null;
                reason = "最近一次测量后当前幅值 DAT 已发生切换，请使用当前标定文件重新测量后再校正。";
                return false;
            }
            if (!string.Equals(currentMagnitudeHash, latestSessionMagnitudeFileSha256, StringComparison.OrdinalIgnoreCase))
            {
                result = null;
                reason = "最近一次测量后当前幅值 DAT 内容已发生变化，请重新测量后再校正。";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private static (string Path, string Sha256) CaptureMagnitudeFileSnapshot()
        {
            if (string.IsNullOrWhiteSpace(Manager.MaguideFile))
                throw new InvalidOperationException("当前标定组没有幅值 DAT。");
            string path = Path.GetFullPath(Manager.MaguideFile);
            return (path, ComputeMagnitudeFileSha256(path));
        }

        private void TrackCorrectionMeasurementResult(
            SpectrumMeasurementResult result,
            (string Path, string Sha256)? magnitudeSnapshot)
        {
            if (!result.IsSuccess || result.Result == null || magnitudeSnapshot == null)
                return;

            (string path, string sha256) = magnitudeSnapshot.Value;
            try
            {
                if (!string.Equals(Path.GetFullPath(Manager.MaguideFile), path, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(ComputeMagnitudeFileSha256(path), sha256, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }
            catch
            {
                return;
            }

            latestSessionMeasurementResultId = result.Result.Id;
            latestSessionMagnitudeFile = path;
            latestSessionMagnitudeFileSha256 = sha256;
        }

        private static string ComputeMagnitudeFileSha256(string filePath)
        {
            using FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return Convert.ToHexString(SHA256.HashData(stream));
        }

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
            Closing += MainWindow_Closing;
            Closed += (_, _) =>
            {
                measurementPause?.Dispose();
                measurementPause = null;
                Dispose();
                if (ReferenceEquals(Instance, this))
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

            ThemeManager.Current.CurrentUIThemeChanged += ApplyDockTheme;
            ApplyDockTheme(ThemeManager.Current.CurrentUITheme);

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

            CancellationToken cancellationToken = windowLifetimeCancellation.Token;
            deferredInitializationTask = InitializeDeferredWindowAsync(cancellationToken);
            try
            {
                await deferredInitializationTask;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                log.Debug("主窗口已开始关闭，取消延后初始化");
            }
            catch (Exception ex)
            {
                log.Error("主窗口延后初始化失败", ex);
                if (!IsWindowActive(cancellationToken))
                {
                    return;
                }

                DockingManager.IsEnabled = true;
                ResourceInitializationProgress.IsIndeterminate = false;
                ResourceInitializationProgress.Value = 0;
                ResourceInitializationText.Text = SpectrumResources.CvCameraInitializationFailed + ex.GetBaseException().Message;
            }
        }

        private async Task InitializeDeferredWindowAsync(CancellationToken cancellationToken)
        {
            await Dispatcher.Yield(DispatcherPriority.Background);
            ThrowIfWindowInactive(cancellationToken);

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

            if (MainWindowConfig.Instance.LogControlVisibility)
            {
                logOutput = new LogOutput("%date{HH:mm:ss} [%thread] %-5level %message%newline", SpectrumLogConfig.Instance);
                LogGrid.Children.Add(logOutput);
            }
            MarkPhase("模型与日志控件");

            await Dispatcher.Yield(DispatcherPriority.Background);
            ThrowIfWindowInactive(cancellationToken);
            MenuManager.GetInstance().LoadMenuForWindow("Spectrum", menu);
            MarkPhase("菜单");
            _ = SpectrumUpdateCoordinator.CheckAtStartupAsync(this);

            await Dispatcher.Yield(DispatcherPriority.Background);
            ThrowIfWindowInactive(cancellationToken);
            StatusBarManager.GetInstance().Init(StatusBarGrid, "Spectrum");
            MarkPhase("状态栏");

            string[] portNames;
            try
            {
                portNames = await (serialPortDiscoveryTask ?? Task.Run(SerialPort.GetPortNames)).WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                log.Warn("串口枚举失败", ex);
                portNames = Array.Empty<string>();
            }

            ThrowIfWindowInactive(cancellationToken);
            ComboBoxPort.ItemsSource = portNames;
            ComboBoxSerial.ItemsSource = new List<int>() { 9600, 115200, 38400, 300, 600, 1200, 2400, 4800, 14400, 19200, 57600 };
            MarkPhase("串口");

            InitializeRelativeSpectrumPlot();
            MarkPhase("首张曲线");

            ViewResultManager viewResultManager = await (viewResultInitializationTask ?? Task.Run(ViewResultManager.GetInstance)).WaitAsync(cancellationToken);
            ThrowIfWindowInactive(cancellationToken);

            ViewResultList.ItemsSource = viewResultManager.ViewResluts;
            viewResultManager.ViewResluts.CollectionChanged += ViewResults_CollectionChanged;
            Config.PropertyChanged += MainWindowConfig_PropertyChanged;
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
            smuAutoConnectTask = AutoConnectSmuIfNeededAsync(cancellationToken);

            try
            {
                await (cvCameraInitializationTask ?? CvCameraResourceInitialization.Value).WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                log.Error("cvCamera 资源初始化失败", ex);
                if (!IsWindowActive(cancellationToken))
                {
                    return;
                }

                ResourceInitializationProgress.IsIndeterminate = false;
                ResourceInitializationProgress.Value = 0;
                ResourceInitializationText.Text = SpectrumResources.CvCameraInitializationFailed + ex.GetBaseException().Message;
                return;
            }

            ThrowIfWindowInactive(cancellationToken);

            SpectrometerConnectionGroup.IsEnabled = true;
            ResourceInitializationBanner.Visibility = Visibility.Collapsed;
            MarkPhase("设备资源");
            stopwatch.Stop();
            log.Info($"主窗口功能初始化完成，耗时 {stopwatch.ElapsedMilliseconds} ms；{string.Join(", ", phases)}");

            _ = Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                if (!IsWindowActive(cancellationToken))
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

        private bool IsWindowActive(CancellationToken cancellationToken) =>
            !cancellationToken.IsCancellationRequested && !isPreparingClose && !disposed && IsLoaded;

        private void ThrowIfWindowInactive(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsWindowActive(cancellationToken))
            {
                throw new OperationCanceledException(cancellationToken);
            }
        }

        private async void SetEmissionSP100Config_EditChanged(object? sender, EventArgs e)
        {
            if (!Manager.IsConnected || SpectrometerHandle == IntPtr.Zero)
            {
                return;
            }

            try
            {
                int result = await Manager.ApplySp100ConfigurationAsync();
                if (result == 1)
                    log.Info("SP100 参数设置成功");
                else
                    log.Warn($"SP100 参数设置失败: {Spectrometer.GetErrorMessage(result)}");
            }
            catch (Exception ex)
            {
                log.Warn("SP100 参数设置异常", ex);
            }
        }

        private void EditEmissionSP100Config_Click(object sender, RoutedEventArgs e)
        {
            new PropertyEditorWindow(SpectrometerManager.SetEmissionSP100Config)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            }.ShowDialog();
            SetEmissionSP100Config_EditChanged(sender, EventArgs.Empty);
        }

        private async void GetSpectrometerSerialNumbers_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                (int result, string json) = await Manager.GetSpectrometerSerialNumbersAsync(windowLifetimeCancellation.Token);
                if (result != 1)
                {
                    MessageBox1.Show(this, $"获取设备列表失败（原生返回值: {result}）", "Sprectrum");
                    return;
                }

                MessageBox1.Show(this, SpectrometerManager.FormatSerialNumberResult(json), "Sprectrum");
            }
            catch (OperationCanceledException) when (windowLifetimeCancellation.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                log.Error("获取光谱仪设备列表失败", ex);
                MessageBox.Show(this, ex.GetBaseException().Message, SpectrumResources.PromptTitle,
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void OpenCalibrationGroupWindow_Click(object sender, RoutedEventArgs e)
        {
            new CalibrationGroupWindow(Manager)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            }.ShowDialog();
        }

        private async void ReloadCalibrationFiles_Click(object sender, RoutedEventArgs e)
        {
            if (calibrationReloadInProgress)
                return;
            if (Manager.IsCalibrationConfigurationPending)
            {
                MessageBox.Show(this, Manager.CalibrationStatus, SpectrumResources.PromptTitle,
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            calibrationReloadInProgress = true;
            Button? button = sender as Button;
            if (button != null)
                button.IsEnabled = false;
            try
            {
                SpectrumCalibrationApplyResult result = await Manager
                    .ApplyConfiguredCalibrationAsync(windowLifetimeCancellation.Token);
                if (!IsWindowActive(windowLifetimeCancellation.Token))
                    return;

                MessageBox.Show(this,
                    result.IsSuccess ? "标定文件加载成功" : $"标定文件加载失败：{result.ErrorMessage}",
                    SpectrumResources.PromptTitle,
                    MessageBoxButton.OK,
                    result.IsSuccess ? MessageBoxImage.Information : MessageBoxImage.Warning);
            }
            catch (OperationCanceledException) when (windowLifetimeCancellation.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                log.Error("重新加载标定文件失败", ex);
                MessageBox.Show(this, ex.GetBaseException().Message, SpectrumResources.PromptTitle,
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                calibrationReloadInProgress = false;
                if (button != null && !disposed)
                    button.IsEnabled = true;
            }
        }

        private void EditMeasurementDataConfig_Click(object sender, RoutedEventArgs e) =>
            ShowPropertyEditor(Manager.MeasurementDataConfig);

        private void EditSmuConfig_Click(object sender, RoutedEventArgs e) =>
            ShowPropertyEditor(Manager.SmuController.Config);

        private void EditShutterConfig_Click(object sender, RoutedEventArgs e) =>
            ShowPropertyEditor(Manager.ShutterController.Config);

        private void EditFilterWheelConfig_Click(object sender, RoutedEventArgs e) =>
            ShowPropertyEditor(Manager.FilterWheelConfig);

        private void ShowPropertyEditor(object config)
        {
            new PropertyEditorWindow(config)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            }.ShowDialog();
        }

        private void EditAutodarkParam_Click(object sender, RoutedEventArgs e)
        {
            PropertyEditorWindow window = new(Manager.AutodarkParam)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            if (Manager.AutodarkParam.ExecuteAdaptiveAutoDark != null)
            {
                Button executeButton = new()
                {
                    Content = "执行自适应校零",
                    Margin = new Thickness(10, 10, 10, 0),
                    Padding = new Thickness(10, 4, 10, 4),
                    Foreground = System.Windows.Media.Brushes.White
                };
                executeButton.SetResourceReference(Control.BackgroundProperty, "WarningBrush");
                executeButton.Click += (_, _) => Manager.AutodarkParam.ExecuteAdaptiveAutoDark?.Invoke();
                if (window.Content is Panel panel)
                    panel.Children.Add(executeButton);
                else if (window.Content is UIElement existingContent)
                {
                    StackPanel content = new();
                    content.Children.Add(existingContent);
                    content.Children.Add(executeButton);
                    window.Content = content;
                }
            }
            window.ShowDialog();
        }

        private void ApplyDockTheme(Theme theme)
        {
            DockingManager.Theme = theme == Theme.Dark
                ? new AvalonDock.Themes.Vs2013DarkTheme()
                : new AvalonDock.Themes.Vs2013LightTheme();
        }

        private async void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (isClosePrepared)
                return;

            CancelWindowLifetime();
            ContentRendered -= Window_ContentRendered;
            e.Cancel = true;
            if (isPreparingClose)
                return;

            try
            {
                await PrepareForShutdownAsync();
                Close();
            }
            catch (Exception ex)
            {
                log.Error("准备关闭 Spectrum 窗口失败", ex);
                IsEnabled = true;
            }
        }

        internal Task PrepareForShutdownAsync()
        {
            if (isClosePrepared)
            {
                return Task.CompletedTask;
            }

            if (closePreparationTask is null || closePreparationTask.IsFaulted || closePreparationTask.IsCanceled)
            {
                closePreparationTask = PrepareForShutdownCoreAsync();
            }

            return closePreparationTask;
        }

        private async Task PrepareForShutdownCoreAsync()
        {
            isPreparingClose = true;
            IsEnabled = false;
            CancelWindowLifetime();
            ContentRendered -= Window_ContentRendered;

            try
            {
                await WaitForDeferredStartupAsync();

                try
                {
                    LayoutManager?.SaveLayout();
                }
                catch (Exception ex)
                {
                    log.Warn("保存 Spectrum 窗口布局失败", ex);
                }

                measurementPause ??= Manager.StopAcceptingMeasurements();
                try
                {
                    CancelContinuousMeasurement();
                }
                catch (Exception ex)
                {
                    log.Warn("停止连续测量失败", ex);
                }

                if (continuousMeasurementTask is { } measurementTask)
                {
                    try
                    {
                        await measurementTask;
                    }
                    catch (OperationCanceledException)
                    {
                    }
                    catch (Exception ex)
                    {
                        log.Warn("等待连续测量结束失败", ex);
                    }
                }

                try
                {
                    await measurementPause.WhenDrained;
                }
                catch (Exception ex)
                {
                    log.Warn("等待光谱测量结束失败", ex);
                }

                try
                {
                    ThemeManager.Current.CurrentUIThemeChanged -= ApplyDockTheme;
                    ViewResultManager.ViewResluts.CollectionChanged -= ViewResults_CollectionChanged;
                    Config.PropertyChanged -= MainWindowConfig_PropertyChanged;
                    Manager.AutodarkParam.ExecuteAdaptiveAutoDark = null;
                }
                catch (Exception ex)
                {
                    log.Warn("取消 Spectrum 窗口事件订阅失败", ex);
                }

                try
                {
                    CloseCieWindow();
                }
                catch (Exception ex)
                {
                    log.Warn("关闭 CIE 窗口失败", ex);
                }

                try
                {
                    CleanupSmuTimedButtons();
                }
                catch (Exception ex)
                {
                    log.Warn("清理源表定时按钮失败", ex);
                }

                try
                {
                    await Manager.DisconnectAsync();
                }
                catch (Exception ex)
                {
                    log.Warn("断开光谱仪失败", ex);
                }

                try
                {
                    await CloseAuxiliaryDevicesAsync();
                }
                catch (Exception ex)
                {
                    log.Warn("关闭辅助设备失败", ex);
                }

                try
                {
                    Dispose();
                }
                catch (Exception ex)
                {
                    log.Warn("释放 Spectrum 窗口资源失败", ex);
                }

                isClosePrepared = true;
            }
            finally
            {
                isPreparingClose = false;
                if (!isClosePrepared && IsLoaded)
                {
                    IsEnabled = true;
                }
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            ContentRendered -= Window_ContentRendered;
            CancelWindowLifetime();
            continuousMeasurementCancellation?.Dispose();
            continuousMeasurementCancellation = null;
            logOutput?.Dispose();
            logOutput = null;
            nativeLogOutput?.Dispose();
            nativeLogOutput = null;
            windowLifetimeCancellation.Dispose();
            GC.SuppressFinalize(this);
        }

        private void CancelWindowLifetime()
        {
            try
            {
                if (!windowLifetimeCancellation.IsCancellationRequested)
                {
                    windowLifetimeCancellation.Cancel();
                }
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private async Task WaitForDeferredStartupAsync()
        {
            if (deferredInitializationTask is { } initializationTask)
            {
                try
                {
                    await initializationTask;
                }
                catch (OperationCanceledException) when (windowLifetimeCancellation.IsCancellationRequested)
                {
                }
                catch (Exception ex)
                {
                    log.Warn("等待主窗口延后初始化结束失败", ex);
                }
            }

            if (smuAutoConnectTask is { } autoConnectTask)
            {
                try
                {
                    await autoConnectTask.WaitAsync(TimeSpan.FromSeconds(5));
                }
                catch (OperationCanceledException) when (windowLifetimeCancellation.IsCancellationRequested)
                {
                }
                catch (TimeoutException)
                {
                    log.Warn("等待 SMU 自动连接结束超时，继续关闭窗口");
                }
                catch (Exception ex)
                {
                    log.Warn("等待 SMU 自动连接结束失败", ex);
                }
            }
        }

        private async Task CloseAuxiliaryDevicesAsync()
        {
            TimeSpan waitTimeout = TimeSpan.FromSeconds(12);
            Stopwatch waitStopwatch = Stopwatch.StartNew();
            while (Manager.ShutterController.IsBusy || Manager.FilterWheelController.IsBusy || Manager.SmuController.IsBusy)
            {
                if (waitStopwatch.Elapsed >= waitTimeout)
                {
                    break;
                }

                await Task.Delay(20);
            }

            bool smuBusy = Manager.SmuController.IsBusy;
            bool shutterBusy = Manager.ShutterController.IsBusy;
            bool filterWheelBusy = Manager.FilterWheelController.IsBusy;
            List<string> devicesNotSafelyClosed = new(3);
            if (smuBusy)
                devicesNotSafelyClosed.Add("SMU");
            if (shutterBusy)
                devicesNotSafelyClosed.Add("快门");
            if (filterWheelBusy)
                devicesNotSafelyClosed.Add("滤光轮");

            if (devicesNotSafelyClosed.Count > 0)
            {
                string deviceNames = string.Join("、", devicesNotSafelyClosed);
                log.Warn($"等待辅助设备操作结束超过 {waitTimeout.TotalSeconds:0} 秒，以下设备未安全关闭且未强制释放：{deviceNames}");
                ShowAuxiliaryShutdownWarning(deviceNames, waitTimeout);
            }

            if (smuBusy)
            {
                log.Warn("SMU 仍在执行操作，跳过同步关闭以避免阻塞窗口退出");
            }
            else
            {
                try
                {
                    await Manager.SmuController.CloseAsync();
                }
                catch (Exception ex)
                {
                    log.Warn("关闭源表失败", ex);
                }
            }

            if (shutterBusy)
            {
                log.Warn("快门仍在执行操作，跳过同步释放以避免阻塞窗口退出");
            }
            else
            {
                try
                {
                    Manager.ShutterController.Dispose();
                }
                catch (Exception ex)
                {
                    log.Warn("关闭快门串口失败", ex);
                }
            }

            if (filterWheelBusy)
            {
                log.Warn("滤光轮仍在执行操作，跳过同步释放以避免阻塞窗口退出");
            }
            else
            {
                try
                {
                    Manager.FilterWheelController.Dispose();
                }
                catch (Exception ex)
                {
                    log.Warn("关闭滤光轮串口失败", ex);
                }
            }
        }

        private void ShowAuxiliaryShutdownWarning(string deviceNames, TimeSpan waitTimeout)
        {
            if (auxiliaryShutdownWarningShown || !IsLoaded || !IsVisible || Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
            {
                return;
            }

            auxiliaryShutdownWarningShown = true;
            try
            {
                MessageBox.Show(
                    this,
                    $"以下辅助设备在 {waitTimeout.TotalSeconds:0} 秒内未结束当前操作，未能安全关闭：{deviceNames}。\n\n为避免中断设备通信，本次未强制释放。请确认设备状态；若下次启动连接异常，请重新连接或重启设备。",
                    SpectrumResources.PromptTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                log.Warn("显示辅助设备未安全关闭提示失败", ex);
            }
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

        private async Task AutoConnectSmuIfNeededAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested || !Manager.SmuController.Config.IsAutoStart || Manager.SmuController.IsOpen || Manager.SmuController.IsBusy)
            {
                return;
            }

            await Task.Yield();
            if (cancellationToken.IsCancellationRequested || isPreparingClose || disposed || !IsLoaded)
            {
                return;
            }

            bool ok = await Manager.SmuController.OpenAsync();
            if (cancellationToken.IsCancellationRequested || isPreparingClose || disposed || !IsLoaded)
            {
                if (ok)
                {
                    try
                    {
                        await Manager.SmuController.CloseAsync();
                    }
                    catch (Exception ex)
                    {
                        log.Warn("窗口关闭期间回收 SMU 自动连接失败", ex);
                    }
                }

                return;
            }

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
        public IntPtr SpectrometerHandle => Manager.Handle;

        //连接光谱仪
        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                int result = await Manager.ConnectAsync();
                if (result == 1)
                {
                    button3.IsEnabled = true;
                    button5.SetCurrentValue(IsEnabledProperty, Manager.IsCalibrationReady);
                    button6.SetCurrentValue(IsEnabledProperty, Manager.IsCalibrationReady);
                }
                else if (result == SpectrometerManager.OperationBusy)
                {
                    MessageBox.Show("光谱仪驱动当前不可用。请先关闭直连诊断窗口；若刚才释放失败，请重启程序。");
                }
                else
                {
                    string errorMsg = Manager.GetOperationErrorMessage(result);
                    log.Error($"光谱仪连接失败: {errorMsg}");
                    if (result == SpectrometerManager.CalibrationUnavailable)
                        MessageBox.Show(Application.Current.GetActiveWindow(), errorMsg, SpectrumResources.PromptTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                    else
                        await CheckDeviceAndPromptLicenseAsync(errorMsg);
                }
            }
            catch (Exception ex)
            {
                log.Error("光谱仪连接异常", ex);
                MessageBox.Show(ex.Message);
            }
        }

        //断开连接
        private async void ButtonClose_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                int result = await Manager.DisconnectAsync();
                if (result != 1)
                {
                    log.Warn($"断开光谱仪时原生接口返回错误: {Spectrometer.GetErrorMessage(result)}");
                }
            }
            catch (Exception ex)
            {
                log.Error("断开光谱仪异常", ex);
                MessageBox.Show(ex.Message);
            }
        }

        /// <summary>
        /// On connection failure, detect if a device exists via CM_Emission_GetAllSN.
        /// If exactly one device is found, it's likely a license issue - open the license manager.
        /// </summary>
        private async Task CheckDeviceAndPromptLicenseAsync(string errorMsg)
        {
            try
            {
                string? serialNumber = await Task.Run(() => Manager.FindSingleDetectedSerialNumber());
                if (!string.IsNullOrEmpty(serialNumber))
                {
                    log.Info($"检测到设备 {serialNumber}，连接失败可能是许可证问题");
                    var msgResult = MessageBox.Show(
                        Application.Current.GetActiveWindow(),
                        string.Format(SpectrumResources.ConnectionFailedWithDeviceDetected, errorMsg, serialNumber),
                        SpectrumResources.ConnectionFailedLicenseCheckTitle,
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (msgResult == MessageBoxResult.Yes)
                    {
                        new LicenseManagerWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
                    }
                    return;
                }
            }
            catch (Exception ex)
            {
                log.Debug($"设备检测失败: {ex.Message}");
            }

            // Default: just show the error message
            MessageBox.Show(Application.Current.GetActiveWindow(), string.Format(SpectrumResources.ConnectionFailedWithError, errorMsg));
        }

        private CancellationTokenSource? continuousMeasurementCancellation;
        private Task? continuousMeasurementTask;
        private int continuousFailureCount;

        internal bool CanInstallUpdate(out string reason)
        {
            if (Manager.IsBusy || continuousMeasurementTask is { IsCompleted: false })
            {
                reason = UpdateText.Get("UpdateDeferredMeasurementBusy", "测量或设备操作正在进行，更新已安全延后。请停止测量后重试安装。");
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private async void AutoIntTime_Click(object sender, RoutedEventArgs e)
        {
            if (Manager.IsDeviceBusy)
            {
                MessageBox1.Show(SpectrumResources.OperationInProgressPleaseWait);
                return;
            }

            SetOperationButtonsEnabled(false);
            try
            {
                (bool entered, float? integrationTime) = await Manager.TryGetAutoIntegrationTimeAsync();
                if (!entered)
                {
                    MessageBox1.Show(SpectrumResources.OperationInProgressPleaseWait);
                    return;
                }

                if (integrationTime.HasValue)
                {
                    Manager.IntTime = integrationTime.Value;
                    log.Info($"自动积分时间获取成功: {integrationTime.Value}ms");
                }
                else
                {
                    MessageBox1.Show("自动积分时间获取失败，请查看日志。");
                }
            }
            catch (Exception ex)
            {
                log.Error("自动积分时间异常", ex);
                MessageBox1.Show(ex.GetBaseException().Message);
            }
            finally
            {
                SetOperationButtonsEnabled(true);
            }
        }

        private async void Button3_Click(object sender, RoutedEventArgs e)
        {
            if (Manager.IsDeviceBusy)
            {
                MessageBox1.Show(Application.Current.GetActiveWindow(), SpectrumResources.OperationInProgressPleaseWait);
                return;
            }

            SetOperationButtonsEnabled(false);
            try
            {
                int result = await Manager.PerformDarkCalibrationAsync();
                if (result == SpectrometerManager.OperationBusy)
                {
                    MessageBox1.Show(Application.Current.GetActiveWindow(), SpectrumResources.OperationInProgressPleaseWait);
                }
                else if (result == 1)
                {
                    log.Info("校零成功");
                    MessageBox.Show(Application.Current.GetActiveWindow(), SpectrumResources.ZeroCalibrationSuccess);
                }
                else
                {
                    string errorMessage = Manager.GetOperationErrorMessage(result);
                    log.Error($"校零失败: {errorMessage}");
                    MessageBox.Show(Application.Current.GetActiveWindow(), string.Format(SpectrumResources.ZeroCalibrationFailed, errorMessage));
                }
            }
            catch (Exception ex)
            {
                log.Error("校零异常", ex);
                MessageBox.Show(Application.Current.GetActiveWindow(), string.Format(SpectrumResources.ZeroCalibrationException, ex.Message));
            }
            finally
            {
                SetOperationButtonsEnabled(true);
            }
        }

        private async void Button5_Click(object sender, RoutedEventArgs e)
        {
            if (Manager.IsDeviceBusy)
            {
                MessageBox.Show(SpectrumResources.OperationInProgressPleaseWait);
                return;
            }

            SetOperationButtonsEnabled(false);
            try
            {
                (string Path, string Sha256)? magnitudeSnapshot = null;
                try
                {
                    magnitudeSnapshot = CaptureMagnitudeFileSnapshot();
                }
                catch (Exception ex)
                {
                    log.Warn("无法记录本次测量使用的幅值 DAT，结果不会用于光谱校正。", ex);
                }

                SpectrumMeasurementResult result = await Manager.MeasureAsync();
                if (!result.IsSuccess)
                    ShowMeasurementFailure(result);
                else
                    TrackCorrectionMeasurementResult(result, magnitudeSnapshot);
            }
            finally
            {
                SetOperationButtonsEnabled(true);
            }
        }

        private void SetOperationButtonsEnabled(bool enabled)
        {
            void ApplyState()
            {
                button3.IsEnabled = enabled;
                button5.SetCurrentValue(IsEnabledProperty, enabled && Manager.IsCalibrationReady);
                button6.SetCurrentValue(IsEnabledProperty, enabled && Manager.IsCalibrationReady);
                ButtonAutoInt.IsEnabled = enabled;
            }

            if (Dispatcher.CheckAccess())
                ApplyState();
            else
                Dispatcher.Invoke(ApplyState);
        }

        private async void Button4_Click_1(object sender, RoutedEventArgs e)
        {
            if (Manager.IsDeviceBusy)
            {
                MessageBox.Show(SpectrumResources.OperationInProgressPleaseWait);
                return;
            }

            SetOperationButtonsEnabled(false);
            try
            {
                int result = await Manager.PerformAdaptiveDarkCalibrationAsync();
                if (result == SpectrometerManager.OperationBusy)
                {
                    MessageBox.Show(SpectrumResources.OperationInProgressPleaseWait);
                }
                else if (result == 1)
                {
                    log.Info("自适应校零成功");
                    MessageBox.Show(SpectrumResources.AdaptiveAutoDarkSuccess);
                }
                else
                {
                    string errorMessage = cvColorVision.Spectrometer.GetErrorMessage(result);
                    log.Error($"自适应校零失败: {errorMessage}");
                    MessageBox.Show(string.Format(SpectrumResources.AdaptiveAutoDarkFailed, errorMessage));
                }
            }
            finally
            {
                SetOperationButtonsEnabled(true);
            }
        }

        private async void Button6_Click(object sender, RoutedEventArgs e)
        {
            if (continuousMeasurementTask is { IsCompleted: false } || Manager.IsDeviceBusy)
            {
                MessageBox.Show(SpectrumResources.OperationInProgressPleaseWait);
                return;
            }

            if (Manager.EnableAutodark && !Manager.ShutterController.IsConnected)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), SpectrumResources.NoShutterAutoZero,
                    SpectrumResources.PromptTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            continuousMeasurementCancellation = new CancellationTokenSource();
            continuousFailureCount = 0;
            SetContinuousMeasurementUi(true);
            continuousMeasurementTask = RunContinuousMeasurementAsync(continuousMeasurementCancellation.Token);
            bool completedNormally = false;
            try
            {
                await continuousMeasurementTask;
                completedNormally = !continuousMeasurementCancellation.IsCancellationRequested;
            }
            catch (OperationCanceledException)
            {
                log.Info("连续测量已停止");
            }
            finally
            {
                int failureCount = continuousFailureCount;
                continuousMeasurementCancellation.Dispose();
                continuousMeasurementCancellation = null;
                continuousMeasurementTask = null;
                Manager.LoopMeasureNum = 0;
                SetContinuousMeasurementUi(false);

                if (completedNormally && Manager.MeasurementNum > 0)
                    MessageBox.Show(this, string.Format(SpectrumResources.ContinuousTestCompletedWithFailureCount, failureCount));
            }
        }

        private async Task RunContinuousMeasurementAsync(CancellationToken cancellationToken)
        {
            log.Info($"连续测量开始，总数 {Manager.MeasurementNum}");
            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
            int completedCount = 0;

            while (Manager.MeasurementNum <= 0 || completedCount < Manager.MeasurementNum)
            {
                cancellationToken.ThrowIfCancellationRequested();
                (string Path, string Sha256)? magnitudeSnapshot = null;
                try
                {
                    magnitudeSnapshot = CaptureMagnitudeFileSnapshot();
                }
                catch (Exception ex)
                {
                    log.Warn("无法记录本次连续测量使用的幅值 DAT，结果不会用于光谱校正。", ex);
                }

                SpectrumMeasurementResult result = await Manager.MeasureAsync(cancellationToken);
                if (!result.IsSuccess)
                {
                    continuousFailureCount++;
                    log.Warn($"连续测量失败: {result.ErrorMessage}");
                }
                else
                {
                    TrackCorrectionMeasurementResult(result, magnitudeSnapshot);
                }

                completedCount++;
                Manager.LoopMeasureNum = completedCount;
                UpdateContinuousProgress(completedCount, stopwatch.Elapsed);

                if (Manager.MeasurementNum > 0 && completedCount >= Manager.MeasurementNum)
                    break;

                await Task.Delay(Manager.MeasurementInterval, cancellationToken);
            }
        }

        private void UpdateContinuousProgress(int completedCount, TimeSpan elapsed)
        {
            ElapsedTimeText.Text = FormatTimeSpan(elapsed);
            if (Manager.MeasurementNum <= 0)
                return;

            ContinuousProgressBar.Value = (double)completedCount / Manager.MeasurementNum * 100;
            double remainingSeconds = elapsed.TotalSeconds / completedCount * (Manager.MeasurementNum - completedCount);
            RemainingTimeText.Text = FormatTimeSpan(TimeSpan.FromSeconds(remainingSeconds));
        }

        private void SetContinuousMeasurementUi(bool running)
        {
            button6.Visibility = running ? Visibility.Collapsed : Visibility.Visible;
            button7.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
            button7.IsEnabled = running;
            TimeEstimationPanel.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
            button3.IsEnabled = !running;
            button5.IsEnabled = !running;
            ButtonAutoInt.IsEnabled = !running;
            if (running)
            {
                ContinuousProgressBar.Value = 0;
                ElapsedTimeText.Text = "--:--";
                RemainingTimeText.Text = "--:--";
            }
        }

        private static string FormatTimeSpan(TimeSpan value) => value.TotalHours >= 1
            ? $"{(int)value.TotalHours}:{value.Minutes:D2}:{value.Seconds:D2}"
            : $"{value.Minutes:D2}:{value.Seconds:D2}";

        private static void ShowMeasurementFailure(SpectrumMeasurementResult result)
        {
            string message = result.IsBusy
                ? SpectrumResources.OperationInProgressPleaseWait
                : result.ErrorMessage ?? "测量失败，请查看日志。";
            MessageBox.Show(Application.Current.GetActiveWindow(), message,
                SpectrumResources.PromptTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void Button7_Click(object sender, RoutedEventArgs e)
        {
            button7.IsEnabled = false;
            continuousMeasurementCancellation?.Cancel();
        }

        private void CancelContinuousMeasurement() => continuousMeasurementCancellation?.Cancel();

        private WindowCIE? _cieWindow;
        private CieMarker? _currentCieMarker;

        private void UpdateCieSelection(double fx, double fy, double fu, double fv)
        {
            CieChromaticity xy = new(fx, fy);
            if (!IsUsableChromaticity(xy))
            {
                xy = CieColorConverter.Uv1976ToXy(new CieChromaticity(fu, fv));
            }

            _currentCieMarker = IsUsableChromaticity(xy)
                ? new CieMarker(SpectrumResources.SampleLabel, xy, System.Windows.Media.Colors.Red)
                : null;

            _cieWindow?.SetSelectedMarker(_currentCieMarker);
        }

        private void OpenCieWindow_Click(object sender, RoutedEventArgs e)
        {
            ShowCieWindow();
        }

        internal void ShowCieWindow()
        {
            if (_cieWindow == null)
            {
                _cieWindow = new WindowCIE { Owner = this };
                _cieWindow.Closed += CieWindow_Closed;
            }

            _cieWindow.SetSelectedMarker(_currentCieMarker);
            _cieWindow.Show();
            _cieWindow.Activate();
        }

        private void CieWindow_Closed(object? sender, EventArgs e)
        {
            if (_cieWindow != null)
            {
                _cieWindow.Closed -= CieWindow_Closed;
                _cieWindow = null;
            }
        }

        private void ClearCieSelection()
        {
            _currentCieMarker = null;
            _cieWindow?.SetSelectedMarker(null);
        }

        private void CloseCieWindow()
        {
            if (_cieWindow == null)
            {
                return;
            }

            WindowCIE window = _cieWindow;
            _cieWindow = null;
            window.Closed -= CieWindow_Closed;
            window.Close();
        }

        private static bool IsUsableChromaticity(CieChromaticity xy)
        {
            return xy.IsFinite
                && (Math.Abs(xy.X) > double.Epsilon || Math.Abs(xy.Y) > double.Epsilon)
                && xy.X >= 0
                && xy.X <= 1
                && xy.Y >= 0
                && xy.Y <= 1;
        }

        bool MulComparison;
        Scatter? LastMulSelectComparsion;
        private bool IsShowingAbsoluteSpectrum { get; set; } = false;

        private void DrawPlot()
        {
            if (ViewResultList.SelectedItem is not Models.ViewResultSpectrum selectedResult)
                return;

            if (IsShowingAbsoluteSpectrum)
            {
                DrawAbsolutePlot();
                return;
            }

            wpfplot1.Plot.Axes.SetLimitsX(380, 780);
            wpfplot1.Plot.Axes.SetLimitsY(-0.05, 1);
            wpfplot1.Plot.Axes.Bottom.Min = selectedResult.fSpect1;
            wpfplot1.Plot.Axes.Bottom.Max = selectedResult.fSpect2;
            wpfplot1.Plot.Axes.Left.Min = -0.05;
            wpfplot1.Plot.Axes.Left.Max = 1;

            Scatter selectedPlot = selectedResult.ScatterPlot;
            if (MulComparison)
            {
                if (LastMulSelectComparsion != null)
                {
                    LastMulSelectComparsion.Color = Color.FromColor(System.Drawing.Color.DarkGoldenrod);
                    LastMulSelectComparsion.LineWidth = 1;
                    LastMulSelectComparsion.MarkerSize = 1;
                }

                LastMulSelectComparsion = selectedPlot;
                selectedPlot.LineWidth = 3;
                selectedPlot.MarkerSize = 3;
                selectedPlot.Color = Color.FromColor(System.Drawing.Color.Red);
                if (!wpfplot1.Plot.PlottableList.Contains(selectedPlot))
                    wpfplot1.Plot.PlottableList.Add(selectedPlot);
            }
            else
            {
                wpfplot1.Plot.Remove(LastMulSelectComparsion);
                selectedPlot.Color = Color.FromColor(System.Drawing.Color.DarkGoldenrod);
                selectedPlot.LineWidth = 1;
                selectedPlot.MarkerSize = 1;
                if (!wpfplot1.Plot.PlottableList.Contains(selectedPlot))
                    wpfplot1.Plot.PlottableList.Add(selectedPlot);
                LastMulSelectComparsion = selectedPlot;
            }

            wpfplot1.Refresh();
        }

        private void DrawAbsolutePlot()
        {
            if (ViewResultList.SelectedItem is not Models.ViewResultSpectrum selectedResult)
                return;

            wpfplot2.Plot.Axes.SetLimitsX(380, 780);
            wpfplot2.Plot.Axes.Bottom.Min = selectedResult.fSpect1;
            wpfplot2.Plot.Axes.Bottom.Max = selectedResult.fSpect2;
            wpfplot2.Plot.Axes.Left.Min = -0.05;
            wpfplot2.Plot.Axes.Left.Max = double.NaN;

            Scatter selectedPlot = selectedResult.AbsoluteScatterPlot;
            if (MulComparison)
            {
                if (LastMulSelectComparsion != null)
                {
                    LastMulSelectComparsion.Color = Color.FromColor(System.Drawing.Color.DarkGoldenrod);
                    LastMulSelectComparsion.LineWidth = 1;
                    LastMulSelectComparsion.MarkerSize = 1;
                }

                LastMulSelectComparsion = selectedPlot;
                selectedPlot.LineWidth = 3;
                selectedPlot.MarkerSize = 3;
                selectedPlot.Color = Color.FromColor(System.Drawing.Color.Red);
                if (!wpfplot2.Plot.PlottableList.Contains(selectedPlot))
                    wpfplot2.Plot.PlottableList.Add(selectedPlot);
            }
            else
            {
                wpfplot2.Plot.Remove(LastMulSelectComparsion);
                selectedPlot.Color = Color.FromColor(System.Drawing.Color.DarkGoldenrod);
                selectedPlot.LineWidth = 1;
                selectedPlot.MarkerSize = 1;
                if (!wpfplot2.Plot.PlottableList.Contains(selectedPlot))
                    wpfplot2.Plot.PlottableList.Add(selectedPlot);
                LastMulSelectComparsion = selectedPlot;
            }

            wpfplot2.Refresh();
        }

        private void ToggleSpectrumType_Click(object sender, RoutedEventArgs e)
        {
            IsShowingAbsoluteSpectrum = !IsShowingAbsoluteSpectrum;

            if (IsShowingAbsoluteSpectrum)
            {
                EnsureAbsoluteSpectrumPlotInitialized();
                wpfplot1.Visibility = Visibility.Collapsed;
                wpfplot2.Visibility = Visibility.Visible;
                SpectrumTypeText.Text = SpectrumResources.AbsoluteSpectrum;
            }
            else
            {
                wpfplot1.Visibility = Visibility.Visible;
                wpfplot2.Visibility = Visibility.Collapsed;
                SpectrumTypeText.Text = SpectrumResources.相对光谱;
            }

            ReDrawPlot();
        }

        private void ReDrawPlot()
        {
            if (ViewResultList.SelectedIndex < 0) return;

            if (IsShowingAbsoluteSpectrum)
            {
                ClearSpectrumSeries(wpfplot2);
                LastMulSelectComparsion = null;
                spectrumPointMarker = null;
                if (MulComparison)
                {
                    ViewResultList.SelectedIndex = ViewResultList.Items.Count > 0 && ViewResultList.SelectedIndex == -1 ? 0 : ViewResultList.SelectedIndex;
                    for (int i = 0; i < ViewResultSpectrums.Count; i++)
                    {
                        if (i == ViewResultList.SelectedIndex) continue;
                        var plot = ViewResultSpectrums[i].AbsoluteScatterPlot;
                        plot.Color = Color.FromColor(System.Drawing.Color.DarkGoldenrod);
                        plot.LineWidth = 1;
                        plot.MarkerSize = 1;
                        wpfplot2.Plot.PlottableList.Add(plot);
                    }
                }
                UpdateSpectrumPointMarker(refresh: false);
                DrawAbsolutePlot();
            }
            else
            {
                ClearSpectrumSeries(wpfplot1);
                LastMulSelectComparsion = null;
                spectrumPointMarker = null;
                if (MulComparison)
                {
                    ViewResultList.SelectedIndex = ViewResultList.Items.Count > 0 && ViewResultList.SelectedIndex == -1 ? 0 : ViewResultList.SelectedIndex;
                    for (int i = 0; i < ViewResultSpectrums.Count; i++)
                    {
                        if (i == ViewResultList.SelectedIndex) continue;
                        var plot = ViewResultSpectrums[i].ScatterPlot;
                        plot.Color = Color.FromColor(System.Drawing.Color.DarkGoldenrod);
                        plot.LineWidth = 1;
                        plot.MarkerSize = 1;
                        wpfplot1.Plot.PlottableList.Add(plot);
                    }
                }
                UpdateSpectrumPointMarker(refresh: false);
                DrawPlot();
            }
        }

        private static void ClearSpectrumSeries(ScottPlot.WPF.WpfPlot plotControl)
        {
            var dataPlots = plotControl.Plot.PlottableList
                .Where(plot => plot is Scatter or Marker)
                .ToArray();
            foreach (var plot in dataPlots)
                plotControl.Plot.Remove(plot);
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

        private ObservableCollection<GridViewColumnVisibility> GridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();
        private bool comparisonRedrawQueued;

        private void ViewResults_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (!MulComparison)
                return;

            QueueComparisonRedraw();
        }

        private void QueueComparisonRedraw()
        {
            if (comparisonRedrawQueued || !IsLoaded)
                return;

            comparisonRedrawQueued = true;
            _ = Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
            {
                comparisonRedrawQueued = false;
                if (!MulComparison || !IsLoaded)
                    return;
                if (ViewResultSpectrums.Count == 0)
                    ClearResultView();
                else if (ViewResultList.SelectedIndex < 0)
                    ViewResultList.SelectedIndex = 0;
                else
                    ReDrawPlot();
            }));
        }

        private void listView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListView { SelectedItem: ViewResultSpectrum selected })
            {
                ViewResultList.ScrollIntoView(selected);
                if (MulComparison)
                    QueueComparisonRedraw();
                else
                    DrawPlot();
                listView2.ItemsSource = selected.SpectralDatas;
                // Keep the optional CIE window synced with the selected result.
                UpdateCieSelection(selected.fx, selected.fy, selected.fu, selected.fv);
            }
            else
            {
                ClearCieSelection();
            }
        }

        private void listView1_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete && ViewResultList.SelectedItems.Count > 0)
            {
                Delete();
                e.Handled = true;
            }
        }

        private Marker? spectrumPointMarker;

        private void listView2_SelectionChanged(object sender, SelectionChangedEventArgs e)
            => UpdateSpectrumPointMarker(refresh: true);

        private void UpdateSpectrumPointMarker(bool refresh)
        {
            if (spectrumPointMarker != null)
            {
                if (wpfplot1.Plot.PlottableList.Contains(spectrumPointMarker))
                    wpfplot1.Plot.Remove(spectrumPointMarker);
                if (wpfplot2.Plot.PlottableList.Contains(spectrumPointMarker))
                    wpfplot2.Plot.Remove(spectrumPointMarker);
                spectrumPointMarker = null;
            }

            ScottPlot.WPF.WpfPlot targetPlot = IsShowingAbsoluteSpectrum ? wpfplot2 : wpfplot1;
            if (listView2.SelectedItem is SpectralData spectralData)
            {
                spectrumPointMarker = new Marker
                {
                    X = spectralData.Wavelength,
                    Y = IsShowingAbsoluteSpectrum ? spectralData.AbsoluteSpectrum : spectralData.RelativeSpectrum,
                    MarkerShape = MarkerShape.FilledCircle,
                    MarkerSize = 10f,
                    Color = ScottPlot.Color.FromColor(System.Drawing.Color.Orange),
                };
                targetPlot.Plot.PlottableList.Add(spectrumPointMarker);
            }
            if (refresh)
                targetPlot.Refresh();
        }

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is ContextMenu contextMenu && contextMenu.Items.Count == 0 && ViewResultList.View is GridView gridView)
                GridViewColumnVisibility.GenContentMenuGridViewColumn(contextMenu, gridView.Columns, GridViewColumnVisibilitys);
        }
        private void GridViewColumnSort(object sender, RoutedEventArgs e)
        {
            e.Handled = ViewResultSpectrums.SortByGridViewColumn<ViewResultSpectrum>(sender, GridViewColumnVisibilitys, Properties.Resources.ResourceManager);
        }

        //清空数据
        private void Cleartable_Click(object sender, RoutedEventArgs e)
        {
            ViewResultManager.ViewReslutsClear();
            listView2.ItemsSource = Array.Empty<SpectralData>();
            if (ViewResultSpectrums.Count > 0)
            {
                ViewResultList.SelectedIndex = 0;
            }
            else
            {
                ClearResultView();
            }
        }

        private void Delete()
        {
            int selectedIndex = ViewResultList.SelectedIndex;
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

            if (ViewResultSpectrums.Count == 0)
                ClearResultView();
            else
                ViewResultList.SelectedIndex = Math.Min(Math.Max(selectedIndex, 0), ViewResultSpectrums.Count - 1);
        }

        private void ClearResultView()
        {
            listView2.ItemsSource = Array.Empty<SpectralData>();
            LastMulSelectComparsion = null;
            spectrumPointMarker = null;
            ClearSpectrumSeries(wpfplot1);
            wpfplot1.Refresh();
            ClearSpectrumSeries(wpfplot2);
            wpfplot2.Refresh();
            ClearCieSelection();
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
                    // Also check for Border with Tag binding
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

        private void GridSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            ViewResultList.Height = ListRow2.ActualHeight - 32;
            ListRow2.Height = GridLength.Auto;
            ListRow1.Height = new GridLength(1, GridUnitType.Star);
        }


        private async void Export_Click(object sender, RoutedEventArgs e)
        {
            List<ViewResultSpectrum> selectedResults = ViewResultList.SelectedItems
                .OfType<ViewResultSpectrum>()
                .ToList();
            if (selectedResults.Count == 0)
            {
                MessageBox.Show(this, "请先选择要导出的数据。", SpectrumResources.PromptTitle,
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            bool isEqeMode = MainWindowConfig.Instance.EqeEnabled;
            using System.Windows.Forms.SaveFileDialog dialog = new()
            {
                Filter = "CSV files (*.csv)|*.csv",
                DefaultExt = "csv",
                AddExtension = true,
                FileName = $"{(isEqeMode ? "EQE" : "SpectrometerExport")}{DateTime.Now:yyyy-MM-dd-HH-mm-ss}",
                RestoreDirectory = true
            };
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;

            try
            {
                await SpectrumCsvExporter.WriteAsync(dialog.FileName, selectedResults, isEqeMode);
                log.Info($"光谱 CSV 导出成功: {dialog.FileName}, count={selectedResults.Count}, eqe={isEqeMode}");
            }
            catch (Exception ex)
            {
                log.Error("光谱 CSV 导出失败", ex);
                MessageBox.Show(this, $"导出失败：{ex.GetBaseException().Message}", SpectrumResources.PromptTitle,
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool _smuTimedButtonsInitialized;

        private TimedButtonOperationRegistry EnsureSmuTimedButtonOperations()
        {
            TimedButtonOperationRegistry operations = this.GetTimedButtonOperations(SpectrumTimedButtonHost.BuildOperationKey);

            operations.Register(SmuConnectButton, options =>
            {
                options.ContentFactory = _ => Manager.SmuController.ConnectButtonText;
                options.ToolTipFactory = stats => Manager.SmuController.IsOpen
                    ? SpectrumResources.DisconnectSourceMeter
                    : TimedButtonOperationTextFormatter.BuildTooltip(SpectrumResources.ConnectSourceMeter, stats);
                options.MinimumExpectedDurationMs = 2000;
            });

            operations.Register(SmuMeasureButton, options =>
            {
                options.MinimumExpectedDurationMs = 1000;
            });

            operations.Register(SmuCloseOutputButton, options =>
            {
                options.MinimumExpectedDurationMs = 600;
            });

            return operations;
        }

        internal void InitializeSmuTimedButtons()
        {
            EnsureSmuTimedButtonOperations();

            if (_smuTimedButtonsInitialized)
            {
                return;
            }

            Manager.SmuController.PropertyChanged += SmuController_PropertyChanged;
            _smuTimedButtonsInitialized = true;
        }

        internal void CleanupSmuTimedButtons()
        {
            if (_smuTimedButtonsInitialized)
            {
                Manager.SmuController.PropertyChanged -= SmuController_PropertyChanged;
                _smuTimedButtonsInitialized = false;
            }

            this.DisposeTimedButtonOperations();
        }

        private void SmuController_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(SmuController.IsOpen)
                && e.PropertyName != nameof(SmuController.ConnectButtonText))
            {
                return;
            }

            void RefreshConnectButton()
            {
                this.TryGetTimedButtonOperations()?.RefreshIdleState(SmuConnectButton);
            }

            if (Dispatcher.CheckAccess())
            {
                RefreshConnectButton();
                return;
            }

            Dispatcher.BeginInvoke(RefreshConnectButton);
        }

        internal void UpdateEqeColumnsVisibility(bool eqeEnabled)
        {
            if (!IsInitialized) return;

            EqePanel.Visibility = eqeEnabled ? Visibility.Visible : Visibility.Collapsed;
            EqeGroupBox.Visibility = eqeEnabled ? Visibility.Visible : Visibility.Collapsed;
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
            // Update measurement mode
            Manager.MeasurementMode = eqeEnabled ? SpectrumResources.LuminousFluxMode : SpectrumResources.BrightnessChromaticityMode;
        }

        private void MainWindowConfig_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(MainWindowConfig.EqeEnabled))
                return;

            if (Dispatcher.CheckAccess())
                UpdateEqeColumnsVisibility(Config.EqeEnabled);
            else
                _ = Dispatcher.BeginInvoke(() => UpdateEqeColumnsVisibility(Config.EqeEnabled));
        }

        private async void CalculateEqe_Click(object sender, RoutedEventArgs e)
        {
            float voltage = MainWindowConfig.Instance.EqeVoltage;
            float currentMA = MainWindowConfig.Instance.EqeCurrentMA;

            var selectedItems = ViewResultList.SelectedItems.Cast<ViewResultSpectrum>().ToList();
            if (selectedItems.Count == 0)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), SpectrumResources.SelectDataToRecalculate, SpectrumResources.PromptTitle, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            Button? calculateButton = sender as Button;
            var previousStates = selectedItems
                .Select(item => (Item: item, State: item.CaptureEqeState()))
                .ToList();
            if (calculateButton != null)
                calculateButton.IsEnabled = false;
            try
            {
                foreach (var item in selectedItems)
                {
                    item.CalculateEqeParams(voltage, currentMA);
                    item.IsRecalculated = true;
                }
                await ViewResultManager.UpdateEqeFieldsAsync(selectedItems, isRecalculated: true);
            }
            catch (Exception ex)
            {
                foreach (var previous in previousStates)
                    previous.Item.RestoreEqeState(previous.State);
                log.Error("批量更新 EQE 结果失败", ex);
                MessageBox.Show(Application.Current.GetActiveWindow(), ex.GetBaseException().Message,
                    SpectrumResources.PromptTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                if (calculateButton != null)
                    calculateButton.IsEnabled = true;
            }
        }

        private async void SmuConnect_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || Manager.SmuController.IsBusy) return;

            bool disconnecting = Manager.SmuController.IsOpen;
            TimedButtonOperationScope? operationScope = null;
            bool success = false;

            try
            {
                operationScope = EnsureSmuTimedButtonOperations().Begin(button, runningText: disconnecting ? SpectrumResources.DisconnectSourceMeter : SpectrumResources.ConnectSourceMeter);

                if (disconnecting)
                {
                    await Manager.SmuController.CloseAsync();
                    success = !Manager.SmuController.IsOpen;
                    if (success)
                    {
                        log.Info("SMU 已断开");
                    }
                }
                else
                {
                    success = await Manager.SmuController.OpenAsync();
                    if (success)
                    {
                        log.Info($"SMU 连接成功: {Manager.SmuController.Version}");
                    }
                    else
                    {
                        string errorMessage = string.IsNullOrWhiteSpace(Manager.SmuController.LastErrorMessage)
                            ? SpectrumResources.SourceMeterConnectFailedCheckSettings
                            : Manager.SmuController.LastErrorMessage;
                        MessageBox.Show(Application.Current.GetActiveWindow(), errorMessage, SpectrumResources.ConnectionFailedTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error(disconnecting ? "SMU 断开失败" : "SMU 连接失败", ex);
                MessageBox.Show(
                    Application.Current.GetActiveWindow(),
                    ex.Message,
                    disconnecting ? SpectrumResources.DisconnectionFailedTitle : SpectrumResources.ConnectionFailedTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            finally
            {
                operationScope?.Complete(success);
                this.TryGetTimedButtonOperations()?.RefreshIdleState(SmuConnectButton);
            }
        }

        private async void SmuMeasure_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || !Manager.SmuController.IsOpen || Manager.SmuController.IsBusy) return;

            TimedButtonOperationScope? operationScope = null;
            bool success = false;

            try
            {
                operationScope = EnsureSmuTimedButtonOperations().Begin(button, runningText: SpectrumResources.SmuMeasureOrSet);
                success = await Manager.SmuController.MeasureAndApplyAsync();
                if (success)
                {
                    var (voltage, currentMA) = Manager.SmuController.GetVI();
                    MainWindowConfig.Instance.EqeVoltage = voltage;
                    MainWindowConfig.Instance.EqeCurrentMA = currentMA;
                    log.Debug($"SMU 测量结果: V={voltage}, I={currentMA}mA");
                }
                else
                {
                    string errorMessage = string.IsNullOrWhiteSpace(Manager.SmuController.LastErrorMessage)
                        ? SpectrumResources.SourceMeterReadFailed
                        : Manager.SmuController.LastErrorMessage;
                    MessageBox.Show(Application.Current.GetActiveWindow(), errorMessage, SpectrumResources.ReadFailedTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                log.Error("SMU 测量失败", ex);
                MessageBox.Show(Application.Current.GetActiveWindow(), ex.Message, SpectrumResources.ReadFailedTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                operationScope?.Complete(success);
            }
        }

        private void SmuCloseOutput_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || !Manager.SmuController.CanCloseOutput) return;

            TimedButtonOperationScope? operationScope = null;
            bool success = false;

            try
            {
                operationScope = EnsureSmuTimedButtonOperations().Begin(button, runningText: SpectrumResources.CloseOutput);
                success = Manager.SmuController.CloseOutput();
                if (success)
                {
                    log.Info("SMU 输出已关闭");
                }
                else
                {
                    string errorMessage = string.IsNullOrWhiteSpace(Manager.SmuController.LastErrorMessage)
                        ? SpectrumResources.SourceMeterCloseOutputFailed
                        : Manager.SmuController.LastErrorMessage;
                    MessageBox.Show(Application.Current.GetActiveWindow(), errorMessage, SpectrumResources.CloseOutputFailedTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                log.Error("SMU 关闭输出失败", ex);
                MessageBox.Show(Application.Current.GetActiveWindow(), ex.Message, SpectrumResources.CloseOutputFailedTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                operationScope?.Complete(success);
            }
        }
    }
}
