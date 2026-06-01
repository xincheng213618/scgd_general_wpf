using ColorVision.Engine.Services;
using ColorVision.Engine.Templates.Flow;
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
            string version = Assembly.GetAssembly(typeof(ConoscopeWindow))?.GetName().Version?.ToString() ?? string.Empty;
            Title = string.IsNullOrWhiteSpace(version)
                ? Properties.Resources.WindowTitleConoscope
                : Conoscope.Core.CompositeFormatCache.Format(Properties.Resources.WindowTitleConoscopeWithVersion, version);
            DataContext = ConoscopeManager.GetInstance();
            this.ApplyCaption();
            ConoscopeWindowConfig.Instance.SetWindow(this);
            InitializeTheme();
            InitializeLanguageAndThemeSelectors();
            InitializeModelSelector();
            InitializeRibbonControls();

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
            var themeNames = new[] { Theme.UseSystem, Theme.Light, Theme.Dark, Theme.Pink, Theme.Cyan };
            var themeDisplayNames = new[]
            {
                $"{Properties.Resources.GroupConfig}: {Properties.Resources.ThemeSystem}",
                Properties.Resources.ThemeLight,
                Properties.Resources.ThemeDark,
                Properties.Resources.ThemePink,
                Properties.Resources.ThemeCyan
            };
            for (int i = 0; i < themeNames.Length; i++)
            {
                cbTheme.Items.Add(new ComboBoxItem { Content = themeDisplayNames[i], Tag = themeNames[i] });
                if (themeNames[i] == ThemeConfig.Instance.Theme)
                    cbTheme.SelectedIndex = i;
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
                cbModelType.SelectedItem = ConoscopeManager.GetInstance().Config.CurrentModel;
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

        private void btnNewView_Click(object sender, RoutedEventArgs e)
        {
            AddConoscopeView(null, activate: true);
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
            tbObservationCameraStatus.Text = Properties.Resources.MsgOpened;
            tbObservationCameraStatus.Foreground = Brushes.LimeGreen;
            observationCameraWindow.Show();
        }

        public void Dispose()
        {
            ConoscopeManager.GetInstance().Config.ModelTypeChanged -= ConoscopeConfig_ModelTypeChanged;
            ConoscopeManager.GetInstance().Config.PropertyChanged -= ConoscopeConfig_PropertyChanged;
            ServiceManager.GetInstance().ServiceChanged -= ServiceManager_ServiceChanged;
            DetachActiveViewControlView();
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
