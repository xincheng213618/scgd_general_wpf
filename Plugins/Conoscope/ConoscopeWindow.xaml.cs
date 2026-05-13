using ColorVision.Engine.Services;
using ColorVision.Engine.Templates.Flow;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Menus;
using Conoscope.Core;
using Conoscope.MVS;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Conoscope
{
    public class MenuConoscopeWindow : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.Tool;
        public override int Order => 50;
        public override string Header => "VAM";

        public override void Execute()
        {
            ConoscopeWindow conoscopeWindow = new ConoscopeWindow();
            conoscopeWindow.Show();
        }
    }

    public class ConoscopeWindowConfig : WindowConfig
    {
        public static ConoscopeWindowConfig Instance => ConfigService.Instance.GetRequiredService<ConoscopeWindowConfig>();
    }

    public partial class ConoscopeWindow : Window, IDisposable
    {
        public static ConoscopeWindow? Instance { get; private set; }

        private ThemeChangedHandler? themeChangedHandler;
        private bool isUpdatingModelSelection;
        private bool isUpdatingPreprocessControls;
        private bool isRunningOperation;
        private readonly Stopwatch operationProgressStopwatch = new Stopwatch();
        private readonly DispatcherTimer operationProgressTimer;
        private string operationProgressLabel = string.Empty;
        private double operationExpectedDurationMs;

        private MVSViewWindow? observationCameraWindow;

        public ConoscopeWindow()
            : this(createInitialView: false)
        {
        }

        public ConoscopeWindow(string filePath)
            : this(createInitialView: false)
        {
            OpenConoscope(filePath);
        }

        private ConoscopeWindow(bool createInitialView)
        {
            InitializeComponent();
            operationProgressTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            operationProgressTimer.Tick += OperationProgressTimer_Tick;
            StopOperationProgress();

            Instance = this;
            Title = "Conoscope " + (Assembly.GetAssembly(typeof(ConoscopeWindow))?.GetName().Version?.ToString() ?? string.Empty);
            DataContext = ConoscopeManager.GetInstance();
            this.ApplyCaption();
            ConoscopeWindowConfig.Instance.SetWindow(this);
            InitializeTheme();
            InitializeModelSelector();
            InitializeOperationControls();

            ConoscopeManager.GetInstance().Config.ModelTypeChanged -= ConoscopeConfig_ModelTypeChanged;
            ConoscopeManager.GetInstance().Config.ModelTypeChanged += ConoscopeConfig_ModelTypeChanged;
            ConoscopeManager.GetInstance().Config.PropertyChanged -= ConoscopeConfig_PropertyChanged;
            ConoscopeManager.GetInstance().Config.PropertyChanged += ConoscopeConfig_PropertyChanged;
            ServiceManager.GetInstance().ServiceChanged -= ServiceManager_ServiceChanged;
            ServiceManager.GetInstance().ServiceChanged += ServiceManager_ServiceChanged;
            RefreshWindowModelState();

            if (createInitialView)
            {
                AddConoscopeView(null, activate: true);
            }

            Closed += (s, e) =>
            {
                if (ReferenceEquals(Instance, this))
                {
                    Instance = null;
                }

                Dispose();
            };
        }

        public ConoscopeView? ActiveView => GetActiveView();
        private ConoscopeConfig ConoscopeConfig => ConoscopeManager.GetInstance().Config;
        private ConoscopeRenderingSettings RenderingConfig => ConoscopeConfig.Rendering;
        private ConoscopePreprocessSettings PreprocessConfig => ConoscopeConfig.Preprocess;
        private ConoscopeCaptureSettings CaptureConfig => ConoscopeConfig.Capture;

        public void OpenConoscope(string filename, string? exposureSummary = null, bool preferReuseActiveView = false)
        {
            ConoscopeView? reuseView = preferReuseActiveView ? ActiveView : null;
            AddConoscopeView(filename, activate: true, exposureSummary, reuseView);
        }

        private void RefreshActiveViewUi()
        {
            ConoscopeView? activeView = ActiveView;
            btnApplyPreprocessToActiveView.IsEnabled = !isRunningOperation && activeView != null;
            btnOpenActiveView3D.IsEnabled = activeView != null;
            btnOpenActiveViewCie.IsEnabled = activeView != null;

            if (tbExposureStatus == null)
            {
                return;
            }

            if (activeView?.HasCaptureExposureSummary == true)
            {
                tbExposureStatus.Text = activeView.CaptureExposureSummary;
                tbExposureStatus.Foreground = Brushes.LimeGreen;
            }
            else
            {
                tbExposureStatus.Text = "未记录";
                tbExposureStatus.Foreground = Brushes.Gray;
            }
        }

        internal void RefreshConoscopeConfiguration()
        {
            foreach (ConoscopeView view in GetOpenViews())
            {
                view.RefreshConoscopeConfiguration();
            }

            RefreshWindowModelState();
        }

        private void InitializeTheme()
        {
            void ThemeChange(Theme theme)
            {
                DockingManager.Theme = theme == Theme.Dark
                    ? new AvalonDock.Themes.Vs2013DarkTheme()
                    : new AvalonDock.Themes.Vs2013LightTheme();
            }

            themeChangedHandler = ThemeChange;
            ThemeChange(ThemeManager.Current.CurrentUITheme);
            ThemeManager.Current.CurrentUIThemeChanged += themeChangedHandler;
        }

        private void InitializeModelSelector()
        {
            cbModelType.ItemsSource = Enum.GetValues<ConoscopeModelType>();
            isUpdatingModelSelection = true;
            try
            {
                cbModelType.SelectedItem = ConoscopeManager.GetInstance().Config.CurrentModel;
            }
            finally
            {
                isUpdatingModelSelection = false;
            }
        }

        private void InitializeOperationControls()
        {
            RefreshFlowTemplates();
            RefreshCameraDevices();
            EnsureCaptureTimedButtonOperations();
            InitializePreprocessControls();
            RefreshActiveViewUi();
        }

        private void StartOperationProgress(string label, double expectedDurationMs)
        {
            operationProgressLabel = label;
            operationExpectedDurationMs = Math.Max(1000, expectedDurationMs);

            pbOperationProgress.Value = 0;
            pbOperationProgress.Foreground = Brushes.DodgerBlue;
            tbOperationProgressText.Foreground = Brushes.DodgerBlue;
            operationProgressStatusItem.Visibility = Visibility.Visible;

            operationProgressStopwatch.Restart();
            UpdateOperationProgress();
            operationProgressTimer.Start();
        }

        private void StopOperationProgress()
        {
            operationProgressTimer.Stop();
            operationProgressStopwatch.Reset();
            operationProgressLabel = string.Empty;
            operationExpectedDurationMs = 0;

            pbOperationProgress.Value = 0;
            tbOperationProgressText.Text = string.Empty;
            operationProgressStatusItem.Visibility = Visibility.Collapsed;
        }

        private void OperationProgressTimer_Tick(object? sender, EventArgs e)
        {
            UpdateOperationProgress();
        }

        private void UpdateOperationProgress()
        {
            double elapsedMilliseconds = operationProgressStopwatch.Elapsed.TotalMilliseconds;
            double progressValue = operationExpectedDurationMs <= 0
                ? 0
                : Math.Min(99, elapsedMilliseconds / operationExpectedDurationMs * 100);

            pbOperationProgress.Value = progressValue;
            tbOperationProgressText.Text = $"{operationProgressLabel} {TimedButtonOperationTextFormatter.FormatDuration(elapsedMilliseconds)} / 预计 {TimedButtonOperationTextFormatter.FormatDuration(operationExpectedDurationMs)}";
        }

        private void ConoscopeConfig_ModelTypeChanged(object? sender, ConoscopeModelType e)
        {
            RefreshWindowModelState();
        }

        private void RefreshWindowModelState()
        {
            ConoscopeConfig config = ConoscopeManager.GetInstance().Config;
            tbCurrentModel.Text = config.CurrentModelProfile.DisplayName;
            btnOpenObservationCamera.Visibility = config.CurrentModelProfile.HasObservationCamera
                ? Visibility.Visible
                : Visibility.Collapsed;

            isUpdatingModelSelection = true;
            try
            {
                cbModelType.SelectedItem = config.CurrentModel;
            }
            finally
            {
                isUpdatingModelSelection = false;
            }

            MenuService.Instance?.RefreshMenuItemsByGuid(MenuItemConstants.View);
        }

        private void btnSaveWindowConfig_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ConfigService.Instance.Save<ConoscopeConfig>();
                ConfigService.Instance.Save<ConoscopeWindowConfig>();
                ConfigService.Instance.Save<FlowEngineConfig>();
                SetOperationStatus("配置已保存", Brushes.LimeGreen);
                MessageBox.Show("配置已保存", "Conoscope", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                SetOperationStatus("配置保存失败", Brushes.OrangeRed);
                MessageBox.Show($"配置保存失败: {ex.Message}", "Conoscope", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            using var openFileDialog = new System.Windows.Forms.OpenFileDialog
            {
                RestoreDirectory = true,
                Multiselect = true
            };

            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                foreach (string filename in openFileDialog.FileNames)
                {
                    OpenConoscope(filename);
                }
            }
        }

        private void btnNewView_Click(object sender, RoutedEventArgs e)
        {
            AddConoscopeView(null, activate: true);
            SetOperationStatus("已新建视图", Brushes.LimeGreen);
        }

        private void btnRefreshCameraDevices_Click(object sender, RoutedEventArgs e)
        {
            RefreshCameraDevices();
            SetOperationStatus("相机列表已刷新", Brushes.LimeGreen);
        }

        private void btnApplyPreprocessToActiveView_Click(object sender, RoutedEventArgs e)
        {
            ConoscopeView? activeView = ActiveView;
            if (activeView == null)
            {
                MessageBox.Show("请先打开或新建一个 Conoscope 视图", "Conoscope", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            activeView.ApplyPreprocessFromCurrentSettings();
            SetOperationStatus("已应用当前预处理预设", Brushes.LimeGreen);
        }

        private void cbModelType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isUpdatingModelSelection || cbModelType.SelectedItem is not ConoscopeModelType conoscopeModelType)
            {
                return;
            }

            ConoscopeManager.GetInstance().Config.CurrentModel = conoscopeModelType;
        }

        private void btnOpenObservationCamera_Click(object sender, RoutedEventArgs e)
        {
            if (observationCameraWindow != null && observationCameraWindow.IsVisible)
            {
                observationCameraWindow.Activate();
                return;
            }

            observationCameraWindow = new MVSViewWindow();
            observationCameraWindow.Closed += (s, e) =>
            {
                observationCameraWindow = null;
                tbObservationCameraStatus.Text = Properties.Resources.NotOpened;
                tbObservationCameraStatus.Foreground = Brushes.Gray;
            };
            tbObservationCameraStatus.Text = "已打开";
            tbObservationCameraStatus.Foreground = Brushes.LimeGreen;
            observationCameraWindow.Show();
        }

        public void Dispose()
        {
            ConoscopeManager.GetInstance().Config.ModelTypeChanged -= ConoscopeConfig_ModelTypeChanged;
            ConoscopeManager.GetInstance().Config.PropertyChanged -= ConoscopeConfig_PropertyChanged;
            ServiceManager.GetInstance().ServiceChanged -= ServiceManager_ServiceChanged;
            operationProgressTimer.Stop();
            operationProgressTimer.Tick -= OperationProgressTimer_Tick;
            this.DisposeTimedButtonOperations();
            if (themeChangedHandler != null)
            {
                ThemeManager.Current.CurrentUIThemeChanged -= themeChangedHandler;
                themeChangedHandler = null;
            }

            foreach (ConoscopeView view in GetOpenViews())
            {
                view.Dispose();
            }

            GC.SuppressFinalize(this);
        }
    }
}
