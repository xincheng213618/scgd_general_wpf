#pragma warning disable CA1822
using ColorVision.Engine.Services;
using ColorVision.Engine.Templates.Flow;
using ColorVision.Engine.FlowProcessing;
using ColorVision.FileIO;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Languages;
using ColorVision.UI.Menus;
using Conoscope.Core;
using Conoscope.MVS;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using AvalonDock.Layout;
using ColorVision.Common.Utilities;
using System.Linq;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.PhyCameras.Group;
using ColorVision.Engine.Templates;
using Conoscope.ApplicationServices.Capture;
using System.Collections.Generic;
using System.ComponentModel;
using ColorVision.Core;
using ColorVision.ImageEditor;
using Conoscope.Presentation.Formatters;
using System.Windows.Input;
using Conoscope.Presentation.Helpers;
using Conoscope.Analysis;
using Conoscope.ApplicationServices.Analysis;
#pragma warning disable CS8602

namespace Conoscope
{
    public class MenuConoscopeWindow : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.Tool;
        public override int Order => 50;
        public override string Header => "VAM";

        public override void Execute()
        {
            ConoscopeModuleService.OpenModule();
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
        private bool disposed;
        private bool pendingPreprocessRefresh;
        private bool pendingDisplayRefresh;
        private DispatcherOperation? pendingConfigRefreshOperation;
        private readonly Stopwatch operationProgressStopwatch = new Stopwatch();
        private readonly DispatcherTimer operationProgressTimer;
        private string operationProgressLabel = string.Empty;
        private double operationExpectedDurationMs;

        private MVSViewWindow? observationCameraWindow;

        public ConoscopeWindow()
        {
            InitializeComponent();
            operationProgressTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            operationProgressTimer.Tick += OperationProgressTimer_Tick;
            StopOperationProgress();

            Instance = this;
            string version = Assembly.GetAssembly(typeof(ConoscopeWindow))?.GetName().Version?.ToString() ?? string.Empty;
            Title = string.IsNullOrWhiteSpace(version)
                ? Properties.Resources.WindowTitleConoscope
                : Conoscope.Core.CompositeFormatCache.Format(Properties.Resources.WindowTitleConoscopeWithVersion, version);
            this.ApplyCaption();
            ConoscopeWindowConfig.Instance.SetWindow(this);
            InitializeTheme();
            InitializeLanguageAndThemeSelectors();
            InitializeModelSelector();
            InitializeRibbonControls();

            ConoscopeManager.Instance.Config.ModelTypeChanged -= ConoscopeConfig_ModelTypeChanged;
            ConoscopeManager.Instance.Config.ModelTypeChanged += ConoscopeConfig_ModelTypeChanged;
            ConoscopeManager.Instance.Config.PropertyChanged -= ConoscopeConfig_PropertyChanged;
            ConoscopeManager.Instance.Config.PropertyChanged += ConoscopeConfig_PropertyChanged;
            ConoscopeManager.Instance.GlobalReferences.Changed -= GlobalReferences_Changed;
            ConoscopeManager.Instance.GlobalReferences.Changed += GlobalReferences_Changed;
            ServiceManager.GetInstance().ServiceChanged -= ServiceManager_ServiceChanged;
            ServiceManager.GetInstance().ServiceChanged += ServiceManager_ServiceChanged;
            RefreshWindowModelState();

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
        private ConoscopeConfig ConoscopeConfig => ConoscopeManager.Instance.Config;

        public void OpenConoscope(string filename, string? exposureSummary = null, bool preferReuseActiveView = false)
        {
            if (!File.Exists(filename) || !CVFileUtil.IsCVCIEFile(filename))
            {
                MessageBox.Show(Properties.Resources.PleaseSelectCVCIEFile, Properties.Resources.TitleHint, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            ConoscopeView? reuseView = preferReuseActiveView ? ActiveView : null;
            AddConoscopeView(filename, activate: true, exposureSummary, reuseView);
        }

        private void RefreshActiveViewUi()
        {
            ConoscopeView? activeView = ActiveView;
            btnApplyPreprocessToActiveView.IsEnabled = !isRunningOperation && activeView != null;
            RefreshRibbonState(activeView);

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
                tbExposureStatus.Text = Properties.Resources.StatusNotRecorded;
                tbExposureStatus.Foreground = Brushes.Gray;
            }
        }

        internal void RefreshAllReferenceState()
        {
            foreach (ConoscopeView view in GetOpenViews())
            {
                view.RefreshGlobalReferenceState();
            }

            RefreshActiveViewUi();
        }

        private void GlobalReferences_Changed(object? sender, EventArgs e)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() => GlobalReferences_Changed(sender, e)));
                return;
            }

            RefreshAllReferenceState();
        }

        private void InitializeRibbonControls()
        {
            RefreshFlowTemplates();
            RefreshCameraDevices();
            EnsureCaptureTimedButtonOperations();
            InitializePreprocessControls();
            InitializeAnalysisRibbonControls();
            RefreshActiveViewControlState(ActiveView);
        }

        private void RefreshRibbonState(ConoscopeView? activeView)
        {
            RefreshActiveViewControlState(activeView);
            RefreshAnalysisRibbonState(activeView);
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

        private void InitializeLanguageAndThemeSelectors()
        {
            // Language selector
            cbLanguage.Items.Clear();
            var languages = LanguageManager.Current.Languages;
            foreach (var lang in languages)
            {
                string displayName = LanguageManager.keyValuePairs.TryGetValue(lang, out string value) ? value : lang;
                cbLanguage.Items.Add(new ComboBoxItem { Content = displayName, Tag = lang });
                if (lang == Thread.CurrentThread.CurrentUICulture.Name)
                    cbLanguage.SelectedIndex = cbLanguage.Items.Count - 1;
            }

            // Theme selector
            cbTheme.Items.Clear();
            foreach (Theme theme in ThemeManager.SupportedThemes)
            {
                string displayName = theme switch
                {
                    Theme.UseSystem => $"{Properties.Resources.GroupConfig}: {Properties.Resources.ThemeSystem}",
                    Theme.Light => Properties.Resources.ThemeLight,
                    Theme.Dark => Properties.Resources.ThemeDark,
                    _ => theme.ToString()
                };

                cbTheme.Items.Add(new ComboBoxItem { Content = displayName, Tag = theme });
                if (theme == ThemeConfig.Instance.Theme)
                    cbTheme.SelectedIndex = cbTheme.Items.Count - 1;
            }
        }

        private void cbLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbLanguage.SelectedItem is ComboBoxItem item && item.Tag is string lang)
            {
                LanguageManager.Current.LanguageChange(lang);
            }
        }

        private void cbTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbTheme.SelectedItem is ComboBoxItem item && item.Tag is Theme theme)
            {
                ThemeConfig.Instance.Theme = theme;
                Application.Current.ApplyTheme(theme);
            }
        }

        private void InitializeModelSelector()
        {
            cbModelType.ItemsSource = Enum.GetValues<ConoscopeModelType>();
            isUpdatingModelSelection = true;
            try
            {
                cbModelType.SelectedItem = ConoscopeManager.Instance.Config.CurrentModel;
            }
            finally
            {
                isUpdatingModelSelection = false;
            }
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
            tbOperationProgressText.Text = $"{operationProgressLabel} {TimedButtonOperationTextFormatter.FormatDuration(elapsedMilliseconds)} / {Properties.Resources.Estimated} {TimedButtonOperationTextFormatter.FormatDuration(operationExpectedDurationMs)}";
        }

        private void ConoscopeConfig_ModelTypeChanged(object? sender, ConoscopeModelType e)
        {
            RefreshWindowModelState();
        }

        private void RefreshWindowModelState()
        {
            ConoscopeConfig config = ConoscopeManager.Instance.Config;
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
                MessageBox.Show(Properties.Resources.MsgConfigSaved, Properties.Resources.TitleSuccess, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{Properties.Resources.MsgConfigSaveFailed}: {ex.Message}", Properties.Resources.TitleError, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            using var openFileDialog = new System.Windows.Forms.OpenFileDialog
            {
                Filter = Properties.Resources.Conoscope_CvcieFileFilter,
                DefaultExt = "cvcie",
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

        private void btnRefreshCameraDevices_Click(object sender, RoutedEventArgs e)
        {
            RefreshCameraDevices();
        }

        private void btnApplyPreprocessToActiveView_Click(object sender, RoutedEventArgs e)
        {
            ConoscopeView? activeView = ActiveView;
            if (activeView == null)
            {
                MessageBox.Show(Properties.Resources.MsgNoActiveView, Properties.Resources.TitleHint, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            activeView.ApplyPreprocessFromCurrentSettings();
        }

        private void cbModelType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isUpdatingModelSelection || cbModelType.SelectedItem is not ConoscopeModelType conoscopeModelType)
            {
                return;
            }

            ConoscopeManager.Instance.Config.CurrentModel = conoscopeModelType;
        }

        private void btnOpenObservationCamera_Click(object sender, RoutedEventArgs e)
        {
            if (observationCameraWindow != null && observationCameraWindow.IsVisible)
            {
                observationCameraWindow.Activate();
                return;
            }

            observationCameraWindow = new MVSViewWindow
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            observationCameraWindow.Closed += (s, e) =>
            {
                observationCameraWindow = null;
                tbObservationCameraStatus.Text = Properties.Resources.NotOpened;
                tbObservationCameraStatus.Foreground = Brushes.Gray;
            };
            tbObservationCameraStatus.Text = Properties.Resources.MsgOpened;
            tbObservationCameraStatus.Foreground = Brushes.LimeGreen;
            observationCameraWindow.Show();
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            ConoscopeManager.Instance.Config.ModelTypeChanged -= ConoscopeConfig_ModelTypeChanged;
            ConoscopeManager.Instance.Config.PropertyChanged -= ConoscopeConfig_PropertyChanged;
            ConoscopeManager.Instance.GlobalReferences.Changed -= GlobalReferences_Changed;
            ServiceManager.GetInstance().ServiceChanged -= ServiceManager_ServiceChanged;
            DetachActiveViewControlView();
            pendingConfigRefreshOperation?.Abort();
            pendingConfigRefreshOperation = null;
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

        private ConoscopeView AddConoscopeView(string? filePath, bool activate, string? exposureSummary = null, ConoscopeView? reuseView = null)
        {
            if (!string.IsNullOrWhiteSpace(filePath))
            {
                string existingContentId = GetContentId(filePath);
                LayoutDocument? existingDocument = ViewDocumentPane.Children
                    .OfType<LayoutDocument>()
                    .FirstOrDefault(item => item.ContentId == existingContentId);
                if (existingDocument?.Content is ConoscopeView existingView && !ReferenceEquals(existingView, reuseView))
                {
                    SelectDocument(existingDocument);
                    return existingView;
                }
            }

            if (reuseView != null && !string.IsNullOrWhiteSpace(filePath))
            {
                LayoutDocument? reuseDocument = GetDocument(reuseView);
                if (reuseDocument != null)
                {
                    reuseView.OpenConoscope(filePath, exposureSummary);
                    reuseDocument.Title = Path.GetFileName(filePath);
                    reuseDocument.ContentId = GetContentId(filePath);
                    if (activate)
                    {
                        SelectDocument(reuseDocument);
                    }
                    else
                    {
                        RefreshActiveViewUi();
                    }

                    return reuseView;
                }
            }

            ConoscopeView view = new ConoscopeView();
            if (!string.IsNullOrWhiteSpace(filePath))
            {
                view.OpenConoscope(filePath, exposureSummary);
            }

            LayoutDocument layoutDocument = new LayoutDocument
            {
                Title = string.IsNullOrWhiteSpace(filePath) ? Properties.Resources.WindowTitleConoscope : Path.GetFileName(filePath),
                ContentId = string.IsNullOrWhiteSpace(filePath) ? $"StandaloneConoscope:{Guid.NewGuid():N}" : GetContentId(filePath),
                Content = view,
                CanClose = true,
                CanFloat = true
            };

            layoutDocument.IsActiveChanged += (s, e) =>
            {
                if (layoutDocument.IsActive)
                {
                    RefreshActiveViewUi();
                }
            };
            layoutDocument.Closing += (s, e) =>
            {
                view.Dispose();
                Dispatcher.BeginInvoke(RefreshActiveViewUi);
            };

            ViewDocumentPane.Children.Add(layoutDocument);
            if (activate)
            {
                SelectDocument(layoutDocument);
            }

            return view;
        }

        private void SelectDocument(LayoutDocument document)
        {
            ViewDocumentPane.SelectedContentIndex = ViewDocumentPane.IndexOf(document);
            document.IsActive = true;
            RefreshActiveViewUi();
        }

        private ConoscopeView? GetActiveView()
        {
            LayoutDocument? activeDocument = ViewDocumentPane.Children
                .OfType<LayoutDocument>()
                .FirstOrDefault(item => item.IsActive);

            if (activeDocument?.Content is ConoscopeView activeView)
            {
                return activeView;
            }

            int selectedIndex = ViewDocumentPane.SelectedContentIndex;
            if (selectedIndex >= 0 && selectedIndex < ViewDocumentPane.Children.Count
                && ViewDocumentPane.Children[selectedIndex] is LayoutDocument selectedDocument
                && selectedDocument.Content is ConoscopeView selectedView)
            {
                return selectedView;
            }

            return null;
        }

        internal ConoscopeView[] GetOpenViews()
        {
            return ViewDocumentPane.Children
                .OfType<LayoutDocument>()
                .Select(item => item.Content as ConoscopeView)
                .Where(item => item != null)
                .Cast<ConoscopeView>()
                .ToArray();
        }

        private LayoutDocument? GetDocument(ConoscopeView view)
        {
            return ViewDocumentPane.Children
                .OfType<LayoutDocument>()
                .FirstOrDefault(item => ReferenceEquals(item.Content, view));
        }

        private static string GetContentId(string filePath)
        {
            return "StandaloneConoscope:" + Tool.GetMD5(Path.GetFullPath(filePath));
        }

        private const double DefaultFlowExpectedDurationMs = 20000;
        private const double DefaultCameraCaptureExpectedDurationMs = 20000;

        private void RefreshFlowTemplates()
        {
            int preferredId = GetSelectedFlowTemplate()?.Id ?? FlowEngineConfig.Instance.LastSelectFlow;
            cbFlowTemplate.ItemsSource = null;
            cbFlowTemplate.ItemsSource = TemplateFlow.Params;
            if (TemplateFlow.Params.Count > 0)
            {
                cbFlowTemplate.SelectedItem = TemplateFlow.Params.FirstOrDefault(item => item.Id == preferredId)
                    ?? TemplateFlow.Params[0];
            }

            btnRunFlow.IsEnabled = !isRunningOperation && GetSelectedFlowTemplate() != null;
        }

        private void RefreshCameraDevices()
        {
            string? selectedCameraCode = GetSelectedCamera()?.Config.Code;
            List<DeviceCamera> cameras = ServiceManager.GetInstance().DeviceServices.OfType<DeviceCamera>().ToList();

            cbCameraDevice.ItemsSource = cameras;

            if (!string.IsNullOrWhiteSpace(selectedCameraCode))
            {
                cbCameraDevice.SelectedItem = cameras.FirstOrDefault(item => item.Config.Code == selectedCameraCode);
            }

            if (cbCameraDevice.SelectedItem == null && cameras.Count > 0)
            {
                cbCameraDevice.SelectedIndex = 0;
            }

            RefreshCalibrationTemplates();
            btnCaptureCamera.IsEnabled = !isRunningOperation && GetSelectedCamera() != null;
        }

        private void RefreshCalibrationTemplates()
        {
            DeviceCamera? camera = GetSelectedCamera();
            TemplateModel<CalibrationParam>? previous = cbCalibrationTemplate.SelectedItem as TemplateModel<CalibrationParam>;
            int previousId = previous?.Id ?? -1;
            string previousKey = previous?.Key ?? string.Empty;

            cbCalibrationTemplate.ItemsSource = camera?.PhyCamera?.CalibrationParams.CreateEmpty();
            TemplateModel<CalibrationParam>? target = cbCalibrationTemplate.Items.OfType<TemplateModel<CalibrationParam>>()
                .FirstOrDefault(item => item.Id == previousId || string.Equals(item.Key, previousKey, StringComparison.OrdinalIgnoreCase));

            cbCalibrationTemplate.SelectedItem = target;
            if (cbCalibrationTemplate.SelectedItem == null)
            {
                cbCalibrationTemplate.SelectedIndex = cbCalibrationTemplate.Items.Count > 0 ? 0 : -1;
            }
        }

        private void ServiceManager_ServiceChanged(object? sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(RefreshCameraDevices));
        }

        private TemplateModel<FlowParam>? GetSelectedFlowTemplate()
        {
            return cbFlowTemplate.SelectedItem as TemplateModel<FlowParam>;
        }

        private DeviceCamera? GetSelectedCamera()
        {
            return cbCameraDevice.SelectedItem as DeviceCamera;
        }

        private CalibrationParam GetSelectedCalibrationParam()
        {
            return (cbCalibrationTemplate.SelectedItem as TemplateModel<CalibrationParam>)?.Value
                ?? new CalibrationParam { Id = -1, Name = string.Empty };
        }

        private bool ShouldReuseActiveViewOnCapture()
        {
            return chkReuseActiveViewOnCapture?.IsChecked == true && ActiveView != null;
        }

        private TimedButtonOperationRegistry EnsureCaptureTimedButtonOperations()
        {
            TimedButtonOperationRegistry operations = this.GetTimedButtonOperations(BuildTimedOperationKey);

            operations.Register(btnRunFlow, options =>
            {
                options.RunningText = Properties.Resources.StatusExecuting;
                options.ProgressForeground = Brushes.DodgerBlue;
                options.MinimumExpectedDurationMs = DefaultFlowExpectedDurationMs;
            });

            operations.Register(btnCaptureCamera, options =>
            {
                options.RunningText = Properties.Resources.StatusCapturing;
                options.ProgressForeground = Brushes.DodgerBlue;
                options.MinimumExpectedDurationMs = DefaultCameraCaptureExpectedDurationMs;
            });

            return operations;
        }

        private static string BuildTimedOperationKey(string actionKey)
        {
            return $"conoscope:capture:{actionKey}";
        }

        private TimedButtonOperationScope? BeginTrackedOperation(Button button, string progressLabel, double expectedDurationMs)
        {
            TimedButtonOperationRegistry operations = EnsureCaptureTimedButtonOperations();
            TimedButtonOperationScope? operationScope = operations.Begin(button, expectedDurationMs, progressLabel);
            StartOperationProgress(progressLabel, expectedDurationMs);
            return operationScope;
        }

        private void SetOperationBusy(bool busy)
        {
            isRunningOperation = busy;
            btnRunFlow.IsEnabled = !busy && GetSelectedFlowTemplate() != null;
            btnCaptureCamera.IsEnabled = !busy && GetSelectedCamera() != null;
            btnRefreshCameraDevices.IsEnabled = !busy;
            btnApplyPreprocessToActiveView.IsEnabled = !busy && ActiveView != null;
        }

        private void btnEditFlowTemplates_Click(object sender, RoutedEventArgs e)
        {
            int selectedIndex = Math.Max(0, cbFlowTemplate.SelectedIndex);
            new TemplateEditorWindow(new TemplateFlow(), selectedIndex)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            }.ShowDialog();

            RefreshFlowTemplates();
        }

        private void btnEditCalibrationTemplates_Click(object sender, RoutedEventArgs e)
        {
            DeviceCamera? camera = GetSelectedCamera();
            if (camera?.PhyCamera == null)
            {
                MessageBox.Show(Properties.Resources.MsgSelectCamera, Properties.Resources.TitleHint, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int selectedIndex = Math.Max(0, cbCalibrationTemplate.SelectedIndex - 1);
            new TemplateEditorWindow(new TemplateCalibrationParam(camera.PhyCamera), selectedIndex)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            }.ShowDialog();

            RefreshCalibrationTemplates();
        }

        private async void btnRunFlow_Click(object sender, RoutedEventArgs e)
        {
            TemplateModel<FlowParam>? flowTemplate = GetSelectedFlowTemplate();
            if (flowTemplate == null)
            {
                MessageBox.Show(Properties.Resources.MsgSelectFlow, Properties.Resources.TitleHint, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            TimedButtonOperationScope? operationScope = null;
            bool operationSucceeded = false;

            try
            {
                operationScope = BeginTrackedOperation(btnRunFlow, Properties.Resources.BtnExecute, DefaultFlowExpectedDurationMs);
                SetOperationBusy(true);

                ConoscopeFlowCaptureResult result = await ConoscopeCaptureWorkflow.RunFlowAsync(flowTemplate);
                if (disposed)
                {
                    return;
                }

                if (!result.Started)
                {
                    return;
                }

                if (!result.Completed)
                {
                    MessageBox.Show(Conoscope.Core.CompositeFormatCache.Format(Properties.Resources.MsgFlowFailedDetail, result.FlowResult.FlowStatus, result.FlowResult.Params), Properties.Resources.TitleHint, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (result.HasFile)
                {
                    OpenConoscope(result.FilePath!, preferReuseActiveView: ShouldReuseActiveViewOnCapture());
                    operationSucceeded = true;
                }
                else
                {
                    MessageBox.Show(Properties.Resources.MsgFlowCvcieNotFoundDetail, Properties.Resources.TitleHint, MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                if (!disposed)
                {
                    MessageBox.Show(Conoscope.Core.CompositeFormatCache.Format(Properties.Resources.MsgFlowFailedDetail, ex.Message, string.Empty), Properties.Resources.TitleError, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            finally
            {
                if (!disposed)
                {
                    operationScope?.Complete(operationSucceeded);
                    StopOperationProgress();
                    SetOperationBusy(false);
                }
            }
        }

        private async void btnCaptureCamera_Click(object sender, RoutedEventArgs e)
        {
            DeviceCamera? camera = GetSelectedCamera();
            if (camera == null)
            {
                MessageBox.Show(Properties.Resources.MsgSelectCamera, Properties.Resources.TitleHint, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            TimedButtonOperationScope? operationScope = null;
            bool operationSucceeded = false;

            try
            {
                operationScope = BeginTrackedOperation(btnCaptureCamera, Properties.Resources.BtnCapturePhoto, DefaultCameraCaptureExpectedDurationMs);
                SetOperationBusy(true);

                ConoscopeCameraCaptureResult result = await ConoscopeCaptureWorkflow.CaptureCameraAsync(camera, GetSelectedCalibrationParam());
                if (disposed)
                {
                    return;
                }

                if (!result.Succeeded)
                {
                    MessageBox.Show(Conoscope.Core.CompositeFormatCache.Format(Properties.Resources.MsgCaptureFailedDetail, result.State, result.MessageRecord.MsgReturn?.Message), Properties.Resources.TitleHint, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (result.HasFile)
                {
                    OpenConoscope(result.FilePath!, result.ExposureSummary, preferReuseActiveView: ShouldReuseActiveViewOnCapture());
                    operationSucceeded = true;
                }
                else
                {
                    MessageBox.Show(Properties.Resources.MsgCaptureCvcieNotFoundDetail, Properties.Resources.TitleHint, MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                if (!disposed)
                {
                    MessageBox.Show(Conoscope.Core.CompositeFormatCache.Format(Properties.Resources.MsgCaptureFailedDetail, ex.Message, string.Empty), Properties.Resources.TitleError, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            finally
            {
                if (!disposed)
                {
                    operationScope?.Complete(operationSucceeded);
                    StopOperationProgress();
                    SetOperationBusy(false);
                }
            }
        }

        private void cbFlowTemplate_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GetSelectedFlowTemplate() is TemplateModel<FlowParam> flowTemplate)
            {
                FlowEngineConfig.Instance.LastSelectFlow = flowTemplate.Id;
            }

            btnRunFlow.IsEnabled = !isRunningOperation && GetSelectedFlowTemplate() != null;
        }

        private void cbCameraDevice_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshCalibrationTemplates();
            btnCaptureCamera.IsEnabled = !isRunningOperation && GetSelectedCamera() != null;
        }

        private ImageFilterType lastEnabledWindowFilterType = ImageFilterType.LowPass;

        private sealed class PseudoColorMapOption
        {
            public PseudoColorMapOption(string name, ColormapTypes value)
            {
                Name = name;
                Value = value;
            }

            public string Name { get; }
            public ColormapTypes Value { get; }
        }

        private void InitializePreprocessControls()
        {
            isUpdatingPreprocessControls = true;
            try
            {
                InitializePseudoColorMapOptions();
                chkWindowApplyFilterOnOpen.IsChecked = ConoscopeConfig.ApplyFilterOnOpen;
                chkWindowUsePseudoColor.IsChecked = ConoscopeConfig.UsePseudoColor;
                chkWindowUsePseudoColorRangeLimit.IsChecked = ConoscopeConfig.UsePseudoColorRangeLimit;
                SelectPseudoColorMap(ConoscopeConfig.PseudoColorMap);

                ImageFilterType filterType = NormalizeFilterType(ConoscopeConfig.FilterType);
                if (filterType != ImageFilterType.None)
                {
                    lastEnabledWindowFilterType = filterType;
                }

                chkWindowEnableFilter.IsChecked = filterType != ImageFilterType.None;
                cbWindowFilterType.SelectedValue = filterType == ImageFilterType.None ? lastEnabledWindowFilterType : filterType;
                txtWindowFilterKernelSize.Text = ConoscopeConfig.FilterKernelSize.ToString();
                txtWindowFilterSigma.Text = ConoscopeConfig.FilterSigma.ToString("0.0");
                txtWindowFilterD.Text = ConoscopeConfig.FilterD.ToString();
                txtWindowFilterSigmaColor.Text = ConoscopeConfig.FilterSigmaColor.ToString("0");
                txtWindowFilterSigmaSpace.Text = ConoscopeConfig.FilterSigmaSpace.ToString("0");

                chkWindowDustRemovalEnabled.IsChecked = ConoscopeConfig.DustRemovalEnabled;

                UpdateWindowPreprocessVisibility();
            }
            finally
            {
                isUpdatingPreprocessControls = false;
            }

            btnApplyPreprocessToActiveView.IsEnabled = !isRunningOperation && ActiveView != null;
        }

        private void UpdateWindowPreprocessVisibility()
        {
            bool usePseudoColor = chkWindowUsePseudoColor.IsChecked == true;
            bool useFilter = chkWindowEnableFilter.IsChecked == true;
            ImageFilterType filterType = cbWindowFilterType.SelectedValue is ImageFilterType selectedFilterType
                ? NormalizeFilterType(selectedFilterType)
                : lastEnabledWindowFilterType;

            panelWindowPseudoColorOptions.Visibility = usePseudoColor ? Visibility.Visible : Visibility.Collapsed;
            panelWindowFilterOptions.Visibility = useFilter ? Visibility.Visible : Visibility.Collapsed;

            fieldWindowFilterKernel.Visibility = filterType is ImageFilterType.LowPass or ImageFilterType.MovingAverage or ImageFilterType.Gaussian or ImageFilterType.Median
                ? Visibility.Visible
                : Visibility.Collapsed;
            fieldWindowFilterSigma.Visibility = filterType == ImageFilterType.Gaussian
                ? Visibility.Visible
                : Visibility.Collapsed;
            bool showBilateral = filterType == ImageFilterType.Bilateral;
            fieldWindowFilterD.Visibility = showBilateral ? Visibility.Visible : Visibility.Collapsed;
            fieldWindowFilterSigmaColor.Visibility = showBilateral ? Visibility.Visible : Visibility.Collapsed;
            fieldWindowFilterSigmaSpace.Visibility = showBilateral ? Visibility.Visible : Visibility.Collapsed;
        }

        private void WindowPreprocess_Changed(object sender, RoutedEventArgs e)
        {
            if (isUpdatingPreprocessControls || !IsInitialized)
            {
                return;
            }

            ConoscopeConfig.ApplyFilterOnOpen = chkWindowApplyFilterOnOpen.IsChecked == true;
            SaveConoscopeConfig();
        }

        private void chkWindowEnableFilter_Changed(object sender, RoutedEventArgs e)
        {
            if (isUpdatingPreprocessControls || !IsInitialized)
            {
                return;
            }

            bool isEnabled = chkWindowEnableFilter.IsChecked == true;
            ImageFilterType currentFilterType = NormalizeFilterType(ConoscopeConfig.FilterType);
            if (!isEnabled)
            {
                if (currentFilterType != ImageFilterType.None)
                {
                    lastEnabledWindowFilterType = currentFilterType;
                }

                ConoscopeConfig.FilterType = ImageFilterType.None;
            }
            else
            {
                ImageFilterType selectedFilterType = cbWindowFilterType.SelectedValue is ImageFilterType filterType
                    ? NormalizeFilterType(filterType)
                    : lastEnabledWindowFilterType;
                lastEnabledWindowFilterType = selectedFilterType;
                ConoscopeConfig.FilterType = selectedFilterType;
            }

            UpdateWindowPreprocessVisibility();
            SaveConoscopeConfig();
        }

        private void cbWindowFilterType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isUpdatingPreprocessControls || !IsInitialized)
            {
                return;
            }

            if (cbWindowFilterType.SelectedValue is ImageFilterType filterType)
            {
                lastEnabledWindowFilterType = NormalizeFilterType(filterType);
                if (chkWindowEnableFilter.IsChecked == true)
                {
                    ConoscopeConfig.FilterType = lastEnabledWindowFilterType;
                }

                UpdateWindowPreprocessVisibility();
                SaveConoscopeConfig();
            }
        }

        private void cbWindowPseudoColorMap_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isUpdatingPreprocessControls || !IsInitialized)
            {
                return;
            }

            if (cbWindowPseudoColorMap.SelectedItem is PseudoColorMapOption selectedItem)
            {
                ConoscopeConfig.PseudoColorMap = selectedItem.Value;
                SaveConoscopeConfig();
            }
        }

        private void WindowDisplay_Changed(object sender, RoutedEventArgs e)
        {
            if (isUpdatingPreprocessControls || !IsInitialized)
            {
                return;
            }

            ConoscopeConfig.UsePseudoColor = chkWindowUsePseudoColor.IsChecked == true;
            ConoscopeConfig.UsePseudoColorRangeLimit = chkWindowUsePseudoColorRangeLimit.IsChecked == true;
            UpdateWindowPreprocessVisibility();
            SaveConoscopeConfig();
        }

        private void WindowDustRemoval_Changed(object sender, RoutedEventArgs e)
        {
            if (isUpdatingPreprocessControls || !IsInitialized)
            {
                return;
            }

            ConoscopeConfig.DustRemovalEnabled = chkWindowDustRemovalEnabled.IsChecked == true;
            SaveConoscopeConfig();
        }

        private void WindowPreprocessValue_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
            {
                return;
            }

            CommitWindowPreprocessValues();
            e.Handled = true;
        }

        private void WindowPreprocessValue_LostFocus(object sender, RoutedEventArgs e)
        {
            CommitWindowPreprocessValues();
        }

        private void CommitWindowPreprocessValues()
        {
            if (isUpdatingPreprocessControls || !IsInitialized)
            {
                return;
            }

            if (!TryApplyWindowPreprocessValues())
            {
                InitializePreprocessControls();
                return;
            }

            SaveConoscopeConfig();
        }

        private bool TryApplyWindowPreprocessValues()
        {
            if (chkWindowEnableFilter.IsChecked == true)
            {
                if (fieldWindowFilterKernel.Visibility == Visibility.Visible)
                {
                    if (!TryParseWindowInt(txtWindowFilterKernelSize, out int kernelSize))
                    {
                        return false;
                    }

                    ConoscopeConfig.FilterKernelSize = ConoscopeNumericHelper.NormalizeOddKernelSize(kernelSize);
                }

                if (fieldWindowFilterSigma.Visibility == Visibility.Visible)
                {
                    if (!TryParseWindowDouble(txtWindowFilterSigma, out double filterSigma))
                    {
                        return false;
                    }

                    ConoscopeConfig.FilterSigma = filterSigma;
                }

                if (fieldWindowFilterD.Visibility == Visibility.Visible)
                {
                    if (!TryParseWindowInt(txtWindowFilterD, out int filterD))
                    {
                        return false;
                    }

                    ConoscopeConfig.FilterD = filterD;
                }

                if (fieldWindowFilterSigmaColor.Visibility == Visibility.Visible)
                {
                    if (!TryParseWindowDouble(txtWindowFilterSigmaColor, out double sigmaColor))
                    {
                        return false;
                    }

                    ConoscopeConfig.FilterSigmaColor = sigmaColor;
                }

                if (fieldWindowFilterSigmaSpace.Visibility == Visibility.Visible)
                {
                    if (!TryParseWindowDouble(txtWindowFilterSigmaSpace, out double sigmaSpace))
                    {
                        return false;
                    }

                    ConoscopeConfig.FilterSigmaSpace = sigmaSpace;
                }
            }

            return true;
        }

        private static bool TryParseWindowInt(TextBox? textBox, out int value)
        {
            value = 0;
            if (!ConoscopeNumericHelper.TryParseDouble(textBox?.Text, out double parsedValue) || !double.IsFinite(parsedValue))
            {
                return false;
            }

            value = Math.Max(1, (int)Math.Round(parsedValue));
            return true;
        }

        private static bool TryParseWindowDouble(TextBox? textBox, out double value)
        {
            value = 0;
            return ConoscopeNumericHelper.TryParseDouble(textBox?.Text, out value) && double.IsFinite(value);
        }

        private void InitializePseudoColorMapOptions()
        {
            ComboBox? pseudoColorMapComboBox = cbWindowPseudoColorMap;
            if (pseudoColorMapComboBox == null || pseudoColorMapComboBox.ItemsSource != null)
            {
                return;
            }

            pseudoColorMapComboBox.DisplayMemberPath = nameof(PseudoColorMapOption.Name);
            pseudoColorMapComboBox.ItemsSource = Enum.GetValues<ColormapTypes>()
                .Select(item => new PseudoColorMapOption(ColormapNameFormatter.Format(item), item))
                .ToArray();
        }

        private void SelectPseudoColorMap(ColormapTypes colormapType)
        {
            if (cbWindowPseudoColorMap?.ItemsSource == null)
            {
                return;
            }

            cbWindowPseudoColorMap.SelectedItem = cbWindowPseudoColorMap.Items
                .OfType<PseudoColorMapOption>()
                .FirstOrDefault(item => item.Value == colormapType);
        }

        private void btnOpenPreprocessSettings_Click(object sender, RoutedEventArgs e)
        {
            ShowConoscopeConfig(selectPreprocess: true);
        }

        private void btnOpenConoscopeSettings_Click(object sender, RoutedEventArgs e)
        {
            ShowConoscopeConfig(selectPreprocess: false);
        }

        private void ShowConoscopeConfig(bool selectPreprocess)
        {
            ConoscopeModelType previousModel = ConoscopeConfig.CurrentModel;
            ConoscopeConfigWindow dialog = new(ConoscopeConfig)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            if (selectPreprocess)
            {
                dialog.SelectPreprocessTab();
            }

            if (dialog.ShowDialog() == true)
            {
                RefreshWindowModelState();

                // A model switch has already refreshed every View through
                // ModelTypeChanged. When the active model itself was edited, apply
                // its final nested axis values once, after the dialog's copy has
                // completed. A pending display refresh will perform the render.
                if (previousModel == ConoscopeConfig.CurrentModel && dialog.CurrentModelViewSettingsChanged)
                {
                    bool refreshDisplayNow = !pendingDisplayRefresh;
                    foreach (ConoscopeView view in GetOpenViews())
                    {
                        view.ApplyCurrentModelDefaults(dialog.CurrentModelGeometryChanged, refreshDisplayNow);
                    }
                }
            }
        }

        private void SaveConoscopeConfig()
        {
            ConfigService.Instance.Save<ConoscopeConfig>();
        }

        private void ConoscopeConfig_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (isUpdatingPreprocessControls)
            {
                return;
            }

            bool preprocessChanged = IsPreprocessProperty(e.PropertyName);
            bool displayChanged = IsDisplayProperty(e.PropertyName);
            if (!preprocessChanged && !displayChanged)
            {
                return;
            }

            pendingPreprocessRefresh |= preprocessChanged;
            pendingDisplayRefresh |= displayChanged;
            if (pendingConfigRefreshOperation?.Status == DispatcherOperationStatus.Pending)
            {
                return;
            }

            pendingConfigRefreshOperation = Dispatcher.BeginInvoke(DispatcherPriority.DataBind, new Action(() =>
            {
                bool refreshPreprocess = pendingPreprocessRefresh;
                bool refreshDisplay = pendingDisplayRefresh;
                pendingPreprocessRefresh = false;
                pendingDisplayRefresh = false;
                pendingConfigRefreshOperation = null;

                InitializePreprocessControls();
                foreach (ConoscopeView view in GetOpenViews())
                {
                    if (refreshDisplay)
                    {
                        view.ApplyWindowRenderingDefaults();
                    }

                    if (refreshPreprocess)
                    {
                        view.ApplyWindowPreprocessDefaults();
                    }
                }
            }));
        }

        private static bool IsPreprocessProperty(string? propertyName)
        {
            return propertyName is nameof(ConoscopeConfig.ApplyFilterOnOpen)
                or nameof(ConoscopeConfig.ClampNonPositiveXyzOnLoad)
                or nameof(ConoscopeConfig.DustRemovalEnabled)
                or nameof(ConoscopeConfig.DustRemovalMode)
                or nameof(ConoscopeConfig.DustThresholdPercent)
                or nameof(ConoscopeConfig.DustMinArea)
                or nameof(ConoscopeConfig.DustMaxArea)
                or nameof(ConoscopeConfig.DustRepairRadius)
                or nameof(ConoscopeConfig.FilterType)
                or nameof(ConoscopeConfig.FilterKernelSize)
                or nameof(ConoscopeConfig.FilterSigma)
                or nameof(ConoscopeConfig.FilterD)
                or nameof(ConoscopeConfig.FilterSigmaColor)
                or nameof(ConoscopeConfig.FilterSigmaSpace);
        }

        private static bool IsDisplayProperty(string? propertyName)
        {
            return propertyName is nameof(ConoscopeConfig.DisplayChannel)
                or nameof(ConoscopeConfig.PseudoColorMap)
                or nameof(ConoscopeConfig.UsePseudoColor)
                or nameof(ConoscopeConfig.UsePseudoColorRangeLimit);
        }

        private static ImageFilterType NormalizeFilterType(ImageFilterType filterType)
        {
            return Enum.IsDefined(filterType) ? filterType : ImageFilterType.None;
        }

        private bool isUpdatingActiveViewControls;
        private ConoscopeView? subscribedActiveViewControlView;

        private void RefreshActiveViewControlState(ConoscopeView? activeView)
        {
            AttachActiveViewControlView(activeView);

            if (bdActiveViewControls == null || bdActiveViewExportControls == null)
            {
                return;
            }

            if (activeView == null || !activeView.HasActiveViewState)
            {
                bdActiveViewControls.IsEnabled = false;
                bdActiveViewExportControls.IsEnabled = false;
                panelActiveAnalysisParameters.IsEnabled = false;
                bdActiveViewControls.DataContext = null;
                panelActiveAnalysisParameters.DataContext = null;

                if (panelActiveColorDifferenceCustomUv != null)
                {
                    panelActiveColorDifferenceCustomUv.Visibility = Visibility.Collapsed;
                }

                return;
            }

            ConoscopeViewState state = activeView.State;
            bdActiveViewControls.IsEnabled = true;
            bdActiveViewExportControls.IsEnabled = true;
            panelActiveAnalysisParameters.IsEnabled = true;

            isUpdatingActiveViewControls = true;
            try
            {
                bdActiveViewControls.DataContext = state;
                panelActiveAnalysisParameters.DataContext = state;
                RefreshActiveViewChannelAvailability(activeView.CanUseDerivedChannels, activeView.CanUseContrastChannel);
                RefreshActiveReferenceControls(activeView);

                UpdateActiveColorDifferenceCustomVisibility(state.ColorDifferenceReferenceMode);

                UpdateActiveContrastReferenceStatus();
                UpdateActiveColorDifferenceReferenceStatus();
            }
            finally
            {
                isUpdatingActiveViewControls = false;
            }
        }

        private void RefreshActiveReferenceControls(ConoscopeView activeView)
        {
            ConoscopeCoordinateAxisParam axis = activeView.State.CoordinateAxis;
            SetActiveReferenceModeSelection(axis.ReferenceMode);
            double referenceValue = axis.ReferenceMode == ConoscopeCoordinateReferenceMode.AzimuthLine
                ? axis.ReferenceAngle
                : axis.ReferenceRadiusAngle;
            txtActiveReferenceValue.Text = referenceValue.ToString("F2", CultureInfo.InvariantCulture);
            txtActiveReferenceValue.ToolTip = axis.ReferenceMode == ConoscopeCoordinateReferenceMode.AzimuthLine
                ? Properties.Resources.TipEnterAzimuth
                : CompositeFormatCache.Format(Properties.Resources.TipEnterPolarAngle, activeView.MaxAngle);
        }

        private void RefreshActiveViewChannelAvailability(bool canUseDerivedChannels, bool canUseContrastChannel)
        {
            UpdateActiveChannelOptionVisibility(cbActiveDisplayChannel, canUseDerivedChannels, canUseContrastChannel);
        }

        private static void UpdateActiveChannelOptionVisibility(ComboBox? comboBox, bool canUseDerivedChannels, bool canUseContrastChannel)
        {
            if (comboBox == null)
            {
                return;
            }

            foreach (ExportChannel channel in new[]
            {
                ExportChannel.X,
                ExportChannel.Z,
                ExportChannel.CieX,
                ExportChannel.CieY,
                ExportChannel.CieU,
                ExportChannel.CieV,
                ExportChannel.ColorDifference
            })
            {
                ComboBoxHelper.SetItemVisibilityByTag(
                    comboBox,
                    channel.ToString(),
                    canUseDerivedChannels ? Visibility.Visible : Visibility.Collapsed);
            }

            string contrastTag = ExportChannel.Contrast.ToString();
            ComboBoxHelper.SetItemVisibilityByTag(comboBox, contrastTag, canUseContrastChannel ? Visibility.Visible : Visibility.Collapsed);
            bool selectedDerivedChannelUnavailable = !canUseDerivedChannels
                && comboBox.SelectedItem is ComboBoxItem derivedSelectedItem
                && Enum.TryParse<ExportChannel>(derivedSelectedItem.Tag?.ToString(), out ExportChannel derivedChannel)
                && derivedChannel is ExportChannel.X
                    or ExportChannel.Z
                    or ExportChannel.CieX
                    or ExportChannel.CieY
                    or ExportChannel.CieU
                    or ExportChannel.CieV
                    or ExportChannel.ColorDifference;
            bool selectedContrastChannelUnavailable = !canUseContrastChannel
                && comboBox.SelectedItem is ComboBoxItem selectedItem
                && string.Equals(selectedItem.Tag?.ToString(), contrastTag, StringComparison.OrdinalIgnoreCase);

            if (selectedDerivedChannelUnavailable || selectedContrastChannelUnavailable)
            {
                ComboBoxHelper.TrySelectItemByTag(comboBox, ExportChannel.Y.ToString(), visibleOnly: true);
            }
        }

        private void SetActiveReferenceModeSelection(ConoscopeCoordinateReferenceMode mode)
        {
            if (rbActiveReferenceLine != null)
            {
                rbActiveReferenceLine.IsChecked = mode == ConoscopeCoordinateReferenceMode.AzimuthLine;
            }

            if (rbActiveReferenceCircle != null)
            {
                rbActiveReferenceCircle.IsChecked = mode == ConoscopeCoordinateReferenceMode.PolarCircle;
            }
        }

        private void UpdateActiveColorDifferenceCustomVisibility(ColorDifferenceReferenceMode mode)
        {
            if (panelActiveColorDifferenceCustomUv == null)
            {
                return;
            }

            panelActiveColorDifferenceCustomUv.Visibility = mode == ColorDifferenceReferenceMode.Custom ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateActiveContrastReferenceStatus()
        {
            ConoscopeGlobalReferenceStore globalReferences = ConoscopeManager.Instance.GlobalReferences;
            UpdateActiveContrastReferenceState(
                btnActiveSaveBlackContrastReference,
                globalReferences.HasContrastReference(ContrastReferenceKind.Black),
                ContrastReferenceKind.Black,
                globalReferences.GetContrastReferenceFileName(ContrastReferenceKind.Black));
            UpdateActiveContrastReferenceState(
                btnActiveSaveWhiteContrastReference,
                globalReferences.HasContrastReference(ContrastReferenceKind.White),
                ContrastReferenceKind.White,
                globalReferences.GetContrastReferenceFileName(ContrastReferenceKind.White));
        }

        private static string GetContrastReferenceLabel(ContrastReferenceKind referenceKind)
        {
            return referenceKind == ContrastReferenceKind.Black
                ? Properties.Resources.ContrastReferenceBlackField
                : Properties.Resources.ContrastReferenceWhiteField;
        }

        private static void UpdateActiveContrastReferenceState(
            Button? button,
            bool isSaved,
            ContrastReferenceKind referenceKind,
            string? fileName)
        {
            if (button != null)
            {
                string label = GetContrastReferenceLabel(referenceKind);
                string savedName = Path.GetFileName(fileName) ?? Properties.Resources.StateSaved;
                button.ToolTip = isSaved
                    ? CompositeFormatCache.Format(Properties.Resources.TipGlobalContrastReferenceSaved, label, savedName)
                    : CompositeFormatCache.Format(Properties.Resources.TipSaveGlobalContrastReference, label);

                if (isSaved)
                {
                    button.Background = Brushes.LightGreen;
                    button.Foreground = Brushes.Black;
                }
                else
                {
                    button.ClearValue(Control.BackgroundProperty);
                    button.ClearValue(Control.ForegroundProperty);
                }
            }
        }

        private void UpdateActiveColorDifferenceReferenceStatus()
        {
            ConoscopeGlobalReferenceStore globalReferences = ConoscopeManager.Instance.GlobalReferences;
            bool hasReference = globalReferences.HasColorDifferenceReference;

            if (btnActiveSaveColorDifferenceReference != null)
            {
                string savedName = Path.GetFileName(globalReferences.ColorDifferenceReferenceFileName) ?? Properties.Resources.StateSaved;
                btnActiveSaveColorDifferenceReference.ToolTip = hasReference
                    ? CompositeFormatCache.Format(Properties.Resources.TipGlobalColorDifferenceReferenceSaved, savedName)
                    : Properties.Resources.TipSaveGlobalColorDifferenceReference;

                if (hasReference)
                {
                    btnActiveSaveColorDifferenceReference.Background = Brushes.LightGreen;
                    btnActiveSaveColorDifferenceReference.Foreground = Brushes.Black;
                }
                else
                {
                    btnActiveSaveColorDifferenceReference.ClearValue(Control.BackgroundProperty);
                    btnActiveSaveColorDifferenceReference.ClearValue(Control.ForegroundProperty);
                }
            }
        }

        private void AttachActiveViewControlView(ConoscopeView? activeView)
        {
            if (ReferenceEquals(subscribedActiveViewControlView, activeView))
            {
                return;
            }

            if (subscribedActiveViewControlView != null)
            {
                subscribedActiveViewControlView.State.PropertyChanged -= ActiveViewState_PropertyChanged;
                subscribedActiveViewControlView.State.CoordinateAxis.PropertyChanged -= ActiveCoordinateAxis_PropertyChanged;
            }

            subscribedActiveViewControlView = activeView;

            if (subscribedActiveViewControlView != null)
            {
                subscribedActiveViewControlView.State.PropertyChanged += ActiveViewState_PropertyChanged;
                subscribedActiveViewControlView.State.CoordinateAxis.PropertyChanged += ActiveCoordinateAxis_PropertyChanged;
            }
        }

        private void DetachActiveViewControlView()
        {
            if (subscribedActiveViewControlView == null)
            {
                return;
            }

            subscribedActiveViewControlView.State.PropertyChanged -= ActiveViewState_PropertyChanged;
            subscribedActiveViewControlView.State.CoordinateAxis.PropertyChanged -= ActiveCoordinateAxis_PropertyChanged;
            subscribedActiveViewControlView = null;
        }

        private void ActiveViewState_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ConoscopeView? activeView = ActiveView;
                if (activeView == null || !ReferenceEquals(sender, activeView.State))
                {
                    return;
                }

                if (e.PropertyName is nameof(ConoscopeViewState.HasDisplayData)
                    or nameof(ConoscopeViewState.CanUseDerivedChannels)
                    or nameof(ConoscopeViewState.CanUseContrastChannel))
                {
                    RefreshActiveViewControlState(activeView);
                }
                else if (e.PropertyName == nameof(ConoscopeViewState.ColorDifferenceReferenceMode))
                {
                    UpdateActiveColorDifferenceCustomVisibility(activeView.State.ColorDifferenceReferenceMode);
                }
            }));
        }

        private void ActiveCoordinateAxis_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is not nameof(ConoscopeCoordinateAxisParam.ReferenceMode)
                and not nameof(ConoscopeCoordinateAxisParam.ReferenceAngle)
                and not nameof(ConoscopeCoordinateAxisParam.ReferenceRadiusAngle))
            {
                return;
            }

            Dispatcher.BeginInvoke(new Action(() =>
            {
                ConoscopeView? activeView = ActiveView;
                if (activeView != null && ReferenceEquals(sender, activeView.State.CoordinateAxis))
                {
                    isUpdatingActiveViewControls = true;
                    try
                    {
                        RefreshActiveReferenceControls(activeView);
                    }
                    finally
                    {
                        isUpdatingActiveViewControls = false;
                    }
                }
            }));
        }

        private void cbActiveDisplayChannel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isUpdatingActiveViewControls || !IsInitialized || ActiveView == null)
            {
                return;
            }

            ExportChannel channel = ComboBoxHelper.GetSelectedEnumByTag(cbActiveDisplayChannel, ExportChannel.Y);
            ActiveView.SetDisplayChannel(channel);
        }

        private void rbActiveReferenceLine_Checked(object sender, RoutedEventArgs e)
        {
            ApplyActiveReferenceMode(ConoscopeCoordinateReferenceMode.AzimuthLine);
        }

        private void rbActiveReferenceCircle_Checked(object sender, RoutedEventArgs e)
        {
            ApplyActiveReferenceMode(ConoscopeCoordinateReferenceMode.PolarCircle);
        }

        private void ApplyActiveReferenceMode(ConoscopeCoordinateReferenceMode mode)
        {
            if (isUpdatingActiveViewControls || !IsInitialized || ActiveView == null)
            {
                return;
            }

            ActiveView.SetReferenceMode(mode);
            if (txtActiveReferenceValue != null)
            {
                txtActiveReferenceValue.ToolTip = mode == ConoscopeCoordinateReferenceMode.AzimuthLine
                    ? Properties.Resources.TipEnterAzimuth
                    : CompositeFormatCache.Format(Properties.Resources.TipEnterPolarAngle, ActiveView.MaxAngle);
            }
        }

        private void cbActiveContrastImageKind_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isUpdatingActiveViewControls || !IsInitialized || ActiveView == null)
            {
                return;
            }

            ContrastReferenceKind imageKind = ComboBoxHelper.GetSelectedEnumByTag(cbActiveContrastImageKind, ContrastReferenceKind.Black);
            ActiveView.SetContrastImageKind(imageKind);
        }

        private void cbActiveColorDifferenceReference_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isUpdatingActiveViewControls || !IsInitialized || ActiveView == null)
            {
                return;
            }

            ColorDifferenceReferenceMode mode = ComboBoxHelper.GetSelectedEnumByTag(cbActiveColorDifferenceReference, ColorDifferenceReferenceMode.D65);
            UpdateActiveColorDifferenceCustomVisibility(mode);
            ActiveView.SetColorDifferenceReferenceMode(mode);
        }

        private void btnActiveExportAngle_Click(object sender, RoutedEventArgs e)
        {
            ActiveView?.ExportAngleMode();
        }

        private void btnActiveExportCircle_Click(object sender, RoutedEventArgs e)
        {
            ActiveView?.ExportCircleMode();
        }

        private void btnActiveAdvancedExport_Click(object sender, RoutedEventArgs e)
        {
            ActiveView?.AdvancedExport();
        }

        private void btnActiveOpen3D_Click(object sender, RoutedEventArgs e)
        {
            ActiveView?.Open3DForCurrentView();
        }

        private void btnActiveOpenCie_Click(object sender, RoutedEventArgs e)
        {
            ActiveView?.OpenCieForCurrentView();
        }

        private void btnActiveSaveBlackContrastReference_Click(object sender, RoutedEventArgs e)
        {
            SaveActiveViewContrastReference(ContrastReferenceKind.Black);
        }

        private void btnActiveSaveWhiteContrastReference_Click(object sender, RoutedEventArgs e)
        {
            SaveActiveViewContrastReference(ContrastReferenceKind.White);
        }

        private void btnActiveSaveColorDifferenceReference_Click(object sender, RoutedEventArgs e)
        {
            ToggleColorDifferenceReference();
        }

        private void txtActiveReferenceValue_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
            {
                return;
            }

            ApplyActiveReferenceValueFromText();
            e.Handled = true;
        }

        private void txtActiveReferenceValue_LostFocus(object sender, RoutedEventArgs e)
        {
            ApplyActiveReferenceValueFromText();
        }

        private void txtActiveColorDifferenceCustom_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
            {
                return;
            }

            ApplyActiveColorDifferenceCustomValuesFromText();
            e.Handled = true;
        }

        private void txtActiveColorDifferenceCustom_LostFocus(object sender, RoutedEventArgs e)
        {
            ApplyActiveColorDifferenceCustomValuesFromText();
        }

        private void ApplyActiveReferenceValueFromText()
        {
            if (isUpdatingActiveViewControls || ActiveView == null || txtActiveReferenceValue == null)
            {
                return;
            }

            if (!double.TryParse(txtActiveReferenceValue.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) || !double.IsFinite(value))
            {
                RefreshActiveViewControlState(ActiveView);
                return;
            }

            if (!ActiveView.HasActiveViewState)
            {
                RefreshActiveViewControlState(ActiveView);
                return;
            }

            ActiveView.SetReferenceValue(value);
        }

        private void ApplyActiveColorDifferenceCustomValuesFromText()
        {
            if (isUpdatingActiveViewControls || ActiveView == null || txtActiveColorDifferenceCustomU == null || txtActiveColorDifferenceCustomV == null)
            {
                return;
            }

            if (!double.TryParse(txtActiveColorDifferenceCustomU.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double u)
                || !double.TryParse(txtActiveColorDifferenceCustomV.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double v)
                || !double.IsFinite(u)
                || !double.IsFinite(v))
            {
                MessageBox.Show(this, Properties.Resources.MsgInvalidCustomUV, Properties.Resources.PanelColorDiff, MessageBoxButton.OK, MessageBoxImage.Warning);
                RefreshActiveViewControlState(ActiveView);
                return;
            }

            ActiveView.SetColorDifferenceCustomReference(u, v);
        }

        private void ToggleColorDifferenceReference()
        {
            ConoscopeGlobalReferenceStore globalReferences = ConoscopeManager.Instance.GlobalReferences;
            if (globalReferences.HasColorDifferenceReference)
            {
                globalReferences.ClearColorDifferenceReference();
                return;
            }

            if (ActiveView == null)
            {
                return;
            }

            try
            {
                ActiveView.SaveColorDifferenceReference();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Properties.Resources.GroupColorDifference, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void SaveActiveViewContrastReference(ContrastReferenceKind referenceKind)
        {
            ConoscopeGlobalReferenceStore globalReferences = ConoscopeManager.Instance.GlobalReferences;
            if (globalReferences.HasContrastReference(referenceKind))
            {
                globalReferences.ClearContrastReference(referenceKind);
                return;
            }

            if (ActiveView == null)
            {
                return;
            }

            try
            {
                ActiveView.SaveCurrentAsGlobalContrastReference(referenceKind);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Properties.Resources.GroupContrast, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private readonly ConoscopeAnalysisSession analysisSession = new();
        private readonly Dictionary<Button, (object? Content, object? ToolTip)> recordButtonVisualStates = new();

        private void InitializeAnalysisRibbonControls()
        {
            if (cbRibbonGamutStandard == null)
            {
                return;
            }

            cbRibbonGamutStandard.ItemsSource = ColorGamutStandards.All;
            ColorGamutStandard? selectedStandard = ColorGamutStandards.All
                .FirstOrDefault(standard => string.Equals(standard.Name, "sRGB", StringComparison.OrdinalIgnoreCase));
            cbRibbonGamutStandard.SelectedItem = selectedStandard ?? (ColorGamutStandards.All.Count > 0 ? ColorGamutStandards.All[0] : null);
            RefreshAnalysisRibbonState(ActiveView);
        }

        private void RefreshAnalysisRibbonState(ConoscopeView? activeView)
        {
            bool hasActiveView = activeView != null;

            if (btnRecordGamutRed == null)
            {
                return;
            }

            btnRecordGamutRed.IsEnabled = hasActiveView;
            btnRecordGamutGreen.IsEnabled = hasActiveView;
            btnRecordGamutBlue.IsEnabled = hasActiveView;
            btnRecordContrastWhite.IsEnabled = hasActiveView;
            btnRecordContrastBlack.IsEnabled = hasActiveView;

            btnComputeGamut.IsEnabled = analysisSession.CanComputeGamut(cbRibbonGamutStandard?.SelectedItem as ColorGamutStandard);
            btnClearGamut.IsEnabled = analysisSession.HasAnyGamutCapture;
            btnComputeContrast.IsEnabled = analysisSession.CanComputeContrast;
            btnClearContrast.IsEnabled = analysisSession.HasAnyContrastCapture;

            UpdateRecordButton(btnRecordGamutRed, analysisSession.GamutRedCapture, Color.FromRgb(214, 69, 65), "R");
            UpdateRecordButton(btnRecordGamutGreen, analysisSession.GamutGreenCapture, Color.FromRgb(66, 165, 79), "G");
            UpdateRecordButton(btnRecordGamutBlue, analysisSession.GamutBlueCapture, Color.FromRgb(52, 120, 246), "B");
            UpdateRecordButton(btnRecordContrastWhite, analysisSession.ContrastWhiteCapture, Color.FromRgb(160, 160, 160), Properties.Resources.SlotWhite);
            UpdateRecordButton(btnRecordContrastBlack, analysisSession.ContrastBlackCapture, Color.FromRgb(90, 90, 90), Properties.Resources.SlotBlack);
        }

        private (object? Content, object? ToolTip) GetRecordButtonVisualState(Button button)
        {
            if (recordButtonVisualStates.TryGetValue(button, out var state))
            {
                return state;
            }

            state = (button.Content, button.ToolTip);
            recordButtonVisualStates.Add(button, state);
            return state;
        }

        private void UpdateRecordButton(Button button, MeasurementCapture? capture, Color accentColor, string slotName)
        {
            var baseState = GetRecordButtonVisualState(button);
            button.Content = baseState.Content;

            if (capture == null)
            {
                button.ClearValue(Control.BackgroundProperty);
                button.ClearValue(Control.BorderBrushProperty);
                button.ClearValue(Control.ForegroundProperty);
                button.ClearValue(Control.FontWeightProperty);
                button.ToolTip = baseState.ToolTip;
                return;
            }

            button.Background = new SolidColorBrush(Color.FromArgb(64, accentColor.R, accentColor.G, accentColor.B));
            button.BorderBrush = new SolidColorBrush(accentColor);
            button.Foreground = Brushes.White;
            button.FontWeight = FontWeights.SemiBold;
            button.ToolTip = Conoscope.Core.CompositeFormatCache.Format(Properties.Resources.MsgSlotRecordedDetail, slotName, capture.SourceLabel, capture.PointCount);
        }

        private void btnRecordGamutRed_Click(object sender, RoutedEventArgs e) => RecordFocusCapture(CaptureSlot.GamutRed, "R");
        private void btnRecordGamutGreen_Click(object sender, RoutedEventArgs e) => RecordFocusCapture(CaptureSlot.GamutGreen, "G");
        private void btnRecordGamutBlue_Click(object sender, RoutedEventArgs e) => RecordFocusCapture(CaptureSlot.GamutBlue, "B");

        private void btnClearGamut_Click(object sender, RoutedEventArgs e)
        {
            analysisSession.ClearGamut();
            RefreshAnalysisRibbonState(ActiveView);
        }

        private void btnComputeGamut_Click(object sender, RoutedEventArgs e)
        {
            if (cbRibbonGamutStandard.SelectedItem is not ColorGamutStandard standard)
            {
                MessageBox.Show(this, Properties.Resources.MsgSelectGamutStandard, Properties.Resources.TitleGamutCalc, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var (gamutResult, gamutError) = analysisSession.ComputeGamut(standard);
            if (gamutError != null)
            {
                MessageBox.Show(this, gamutError, Properties.Resources.TitleGamutCalc, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ColorGamutResultWindow window = new(gamutResult!)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            window.Show();
            window.Activate();
        }

        private void btnRecordContrastWhite_Click(object sender, RoutedEventArgs e) => RecordFocusCapture(CaptureSlot.ContrastWhite, Properties.Resources.SlotWhite);
        private void btnRecordContrastBlack_Click(object sender, RoutedEventArgs e) => RecordFocusCapture(CaptureSlot.ContrastBlack, Properties.Resources.SlotBlack);

        private void btnClearContrast_Click(object sender, RoutedEventArgs e)
        {
            analysisSession.ClearContrast();
            RefreshAnalysisRibbonState(ActiveView);
        }

        private void btnComputeContrast_Click(object sender, RoutedEventArgs e)
        {
            var (contrastResult, contrastError) = analysisSession.ComputeContrast();
            if (contrastError != null)
            {
                MessageBox.Show(this, contrastError, Properties.Resources.TitleContrastCalc, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ContrastResultWindow window = new(contrastResult!)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            window.Show();
            window.Activate();
        }

        private void RecordFocusCapture(CaptureSlot slot, string slotName)
        {
            ConoscopeView? activeView = ActiveView;
            if (activeView == null)
            {
                MessageBox.Show(this, Properties.Resources.MsgNoActiveView, Properties.Resources.TitleAnalysisRecord, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!activeView.TryGetFocusPointMeasurementCapture(slotName, out MeasurementCapture capture, out string? errorMessage))
            {
                MessageBox.Show(this, errorMessage ?? Properties.Resources.MsgFocusPointsUnavailable, Properties.Resources.TitleAnalysisRecord, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            analysisSession.RecordCapture(slot, capture);
            RefreshAnalysisRibbonState(activeView);
        }
    }
}
