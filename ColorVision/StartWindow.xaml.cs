using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.Startup;
using ColorVision.ServiceHost;
using ColorVision.UI.Shell;
using ColorVision.UI.Desktop.Operations;
using Dm.util;
using log4net;
using log4net.Repository.Hierarchy;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Threading;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVision
{

    /// <summary>
    /// StartWindow.xaml 的交互逻辑
    /// </summary>
    public partial class StartWindow : Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(StartWindow));
        private const int StartupUiYieldIntervalMs = 180;
        private const long SlowInitializerLogThresholdMs = 100;
        private const double DefaultStartupStepWeight = 1d;
        private const double MinimumProfiledStepWeightMs = 20d;
        private const double MaximumProfiledStepWeightMs = 12000d;
        private const double StartupProfileSmoothing = 0.35d;
        private ThemeManager? _subscribedThemeManager;
        private readonly CancellationTokenSource _startupCancellation = new();

        internal Func<Task>? PrepareStartupAsync { get; set; }
        internal bool ShowSetupWizardAfterPreparation { get; set; }
        internal CancellationToken StartupCancellationToken => _startupCancellation.Token;

        internal async Task ShowPreparationStageAsync(string stage)
        {
            _startupCancellation.Token.ThrowIfCancellationRequested();
            startupStatusText.Text = stage;
            await Dispatcher.Yield(DispatcherPriority.Background);
            _startupCancellation.Token.ThrowIfCancellationRequested();
        }

        public StartWindow()
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            InitializeComponent();
            headlineText.Text = StartupText.GetRandomHeadline();
            _startupProgressTimer.Interval = TimeSpan.FromMilliseconds(100);
            _startupProgressTimer.Tick += StartupProgressTimer_Tick;
            ContentRendered += StartWindow_ContentRendered;
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            labelVersion.Text = Assembly.GetExecutingAssembly().GetName().Version?.ToString();
            if (ProgramTimer.InitAppender is { } startupAppender)
            {
                ((Hierarchy)LogManager.GetRepository()).Root.RemoveAppender(startupAppender);
                startupAppender.Close();
            }

            _subscribedThemeManager = ThemeManager.Current;
            _subscribedThemeManager.SystemThemeChanged += ThemeManager_SystemThemeChanged;
            if (_subscribedThemeManager.SystemTheme == Theme.Dark)
                Icon = new BitmapImage(new Uri("pack://application:,,,/ColorVision;component/Assets/Image/ColorVision1.ico"));
            InitializePresentation();
        }

        private void ThemeManager_SystemThemeChanged(Theme theme)
        {
            Icon = new BitmapImage(new Uri($"pack://application:,,,/ColorVision;component/Assets/Image/{(theme == Theme.Light ? "ColorVision.ico" : "ColorVision1.ico")}"));
        }

        protected override void OnClosed(EventArgs e)
        {
            _startupCancellation.Cancel();
            ReleasePresentation();
            _startupProgressTimer.Stop();
            if (_subscribedThemeManager != null)
            {
                _subscribedThemeManager.SystemThemeChanged -= ThemeManager_SystemThemeChanged;
                _subscribedThemeManager = null;
            }

            base.OnClosed(e);
        }

        private async void StartWindow_ContentRendered(object? sender, EventArgs e)
        {
            ContentRendered -= StartWindow_ContentRendered;
            try
            {
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                if (PrepareStartupAsync != null)
                    await PrepareStartupAsync();
                if (Dispatcher.HasShutdownStarted || _presentationClosed)
                    return;
                if (ShowSetupWizardAfterPreparation)
                {
                    var wizard = new ColorVision.UI.Desktop.Wizards.WizardWindow { WindowStartupLocation = WindowStartupLocation.CenterScreen };
                    Application.Current.MainWindow = wizard;
                    wizard.Show();
                    Close();
                    return;
                }
                await Task.Run(RunStartupAsync);
                _startupCancellation.Token.ThrowIfCancellationRequested();
                await Dispatcher.InvokeAsync(() =>
                {
                    if (!_presentationClosed) ShowMainWindowAndClose();
                }, DispatcherPriority.ContextIdle);
            }
            catch (OperationCanceledException) when (_startupCancellation.IsCancellationRequested) { }
            catch (Exception ex)
            {
                if (_presentationClosed) return;
                log.Error("Startup failed.", ex);
                await Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show("Startup Error:" + ex.Message);
                    Environment.Exit(-1);
                }, DispatcherPriority.Send);
            }
        }

        private async Task RunStartupAsync()
        {
            _IComponentInitializers = CreateSortedInitializers();
            _startupTotalSteps = _IComponentInitializers.Count;
            LoadStartupProgressProfile();
            UpdateStartupProgress(0);
            await YieldToUiAsync();
            await InitializedOver();
        }

        private List<IInitializer> CreateSortedInitializers()
        {
            var parser = ArgumentParser.GetInstance();
            parser.AddArgument("skip", false, "skip");
            parser.Parse();
            HashSet<string> skipNames = (parser.GetValue("skip") ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.Ordinal);

            return AssemblyHandler.GetInstance()
                .LoadImplementations<IInitializer>()
                .Where(initializer => !skipNames.Contains(initializer.Name))
                .OrderBy(initializer => initializer.Order)
                .ThenBy(initializer => initializer.Name, StringComparer.Ordinal)
                .ToList();
        }

        private  List<IInitializer> _IComponentInitializers;
        private int _startupTotalSteps;
        private readonly DispatcherTimer _startupProgressTimer = new(DispatcherPriority.Normal);
        private readonly Dictionary<string, double> _startupStepWeights = new();
        private readonly Dictionary<string, double> _startupObservedDurationsMs = new();
        private double _startupProgressTarget;
        private double _startupProgressSoftCap;
        private double _startupTotalWeight;
        private bool _startupProgressCreepEnabled;
        private long _lastStartupYieldTimestamp;

        private void LoadStartupProgressProfile()
        {
            _startupStepWeights.Clear();

            StartupProgressProfileConfig profile = ConfigHandler.GetInstance().GetRequiredService<StartupProgressProfileConfig>();
            foreach (var initializer in _IComponentInitializers)
            {
                string key = GetInitializerProfileKey(initializer);
                double weight = DefaultStartupStepWeight;
                if (profile.InitializerDurationsMs.TryGetValue(key, out double profiledDurationMs) && profiledDurationMs > 0)
                {
                    weight = Math.Clamp(profiledDurationMs, MinimumProfiledStepWeightMs, MaximumProfiledStepWeightMs);
                }

                _startupStepWeights[key] = weight;
            }

            _startupTotalWeight = Math.Max(_startupStepWeights.Values.Sum(), DefaultStartupStepWeight);
        }

        private void UpdateStartupProgressProfile()
        {
            if (_startupObservedDurationsMs.Count == 0)
            {
                return;
            }

            try
            {
                StartupProgressProfileConfig profile = ConfigHandler.GetInstance().GetRequiredService<StartupProgressProfileConfig>();
                foreach (var item in _startupObservedDurationsMs)
                {
                    double duration = Math.Clamp(item.Value, MinimumProfiledStepWeightMs, MaximumProfiledStepWeightMs);
                    if (profile.InitializerDurationsMs.TryGetValue(item.Key, out double previousDuration) && previousDuration > 0)
                    {
                        duration = previousDuration * (1d - StartupProfileSmoothing) + duration * StartupProfileSmoothing;
                    }

                    profile.InitializerDurationsMs[item.Key] = duration;
                }

                profile.UpdatedAt = DateTime.Now;
            }
            catch (Exception ex)
            {
                log.Warn("Failed to save startup progress profile.", ex);
            }
        }

        private double GetStartupStepWeight(IInitializer initializer)
        {
            string key = GetInitializerProfileKey(initializer);
            return _startupStepWeights.TryGetValue(key, out double weight) && weight > 0
                ? weight
                : DefaultStartupStepWeight;
        }

        private static string GetInitializerProfileKey(IInitializer initializer)
        {
            return $"{initializer.Name}|{initializer.GetType().FullName}";
        }

        private void UpdateStartupProgress(double completedWeight, bool initializerRunning = false, double runningWeight = 0, string? stage = null)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (startupProgressBar == null)
                {
                    return;
                }

                // Reuse one status element and the existing progress dispatch. No per-log
                // notifications, growing text buffer or extra status animation/timer.
                if (stage != null && startupStatusText.Text != stage)
                    startupStatusText.Text = stage;

                double max = Math.Max(_startupTotalWeight, DefaultStartupStepWeight);
                startupProgressBar.Maximum = max;
                _startupProgressTarget = _startupTotalWeight <= 0
                    ? max
                    : Math.Min(Math.Max(completedWeight, 0), max);
                _startupProgressCreepEnabled = initializerRunning && _startupProgressTarget < max;
                _startupProgressSoftCap = _startupProgressCreepEnabled
                    ? Math.Min(_startupProgressTarget + Math.Max(runningWeight, DefaultStartupStepWeight) * 0.88, max - 0.02)
                    : _startupProgressTarget;

                if (!_startupProgressTimer.IsEnabled && startupProgressBar.Value < max)
                {
                    _startupProgressTimer.Start();
                }
            }));
        }

        private void StartupProgressTimer_Tick(object? sender, EventArgs e)
        {
            if (startupProgressBar == null)
            {
                return;
            }

            // An estimate may have advanced past a concurrent short step's completed
            // weight. Keep the visible fill anchored and never rewind at stage boundaries.
            double target = Math.Max(_startupProgressTarget, startupProgressBar.Value);
            if (_startupProgressCreepEnabled && startupProgressBar.Value < _startupProgressSoftCap)
            {
                double remainingToSoftCap = _startupProgressSoftCap - startupProgressBar.Value;
                target = Math.Max(target, startupProgressBar.Value + Math.Max(remainingToSoftCap * 0.045, 0.01));
            }

            double delta = target - startupProgressBar.Value;
            if (Math.Abs(delta) < 0.005)
            {
                startupProgressBar.Value = target;
                if (!_startupProgressCreepEnabled && startupProgressBar.Value >= startupProgressBar.Maximum)
                {
                    _startupProgressTimer.Stop();
                }
                return;
            }

            double step = Math.Max(Math.Abs(delta) * 0.35, 0.025);
            startupProgressBar.Value += Math.Sign(delta) * Math.Min(Math.Abs(delta), step);
        }

        private static string GetStartupStage(IInitializer initializer) => StartupText.GetStage(initializer.GetType().Name);

        private IReadOnlyList<StartupInitializerResult> _initializerResults = [];
        private readonly object _startupStateLock = new();

        private async Task InitializedOver()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            double completedWeight = 0;
            _initializerResults = await StartupInitializerRunner.RunAsync(_IComponentInitializers,
                starting: initializer =>
                {
                    lock (_startupStateLock)
                    {
                        StartupRegistryChecker.MarkStage("StartupInitializer", initializer.Name);
                        UpdateStartupProgress(completedWeight, initializerRunning: true,
                            runningWeight: GetStartupStepWeight(initializer), stage: GetStartupStage(initializer));
                    }
                },
                completed: async (initializer, result) =>
                {
                    if (result.Error != null)
                        log.Error($"Startup initializer {result.Name} failed.", result.Error);
                    if (result.Duration.TotalMilliseconds >= SlowInitializerLogThresholdMs)
                        log.Info($"Slow startup initializer {result.Name} completed in {result.Duration.TotalMilliseconds:0} ms. Success={result.Succeeded}.");
                    _startupObservedDurationsMs[GetInitializerProfileKey(initializer)] =
                        Math.Max(result.Duration.TotalMilliseconds, MinimumProfiledStepWeightMs);
                    lock (_startupStateLock)
                    {
                        completedWeight += GetStartupStepWeight(initializer);
                        UpdateStartupProgress(completedWeight);
                    }
                    await YieldToUiIfDueAsync();
                }, cancellationToken: _startupCancellation.Token).ConfigureAwait(false);
            log.Info($"Startup initializers completed in {stopwatch.ElapsedMilliseconds} ms. " +
                $"Summed={_initializerResults.Sum(result => result.Duration.TotalMilliseconds):0} ms, " +
                $"Failures={_initializerResults.Count(result => !result.Succeeded)}.");
            StartupRegistryChecker.MarkStage("StartupInitializersCompleted");
            UpdateStartupProgressProfile();
            await CompleteStartupProgressAsync();
        }

        private Task CompleteStartupProgressAsync()
        {
            // Keep completion behind the phase updates already queued at Normal priority.
            // A higher-priority callback could otherwise be overwritten by an older phase.
            return Dispatcher.InvokeAsync(() =>
            {
                startupStatusText.Text = StartupText.OpeningWorkspace;
                if (startupProgressBar == null)
                {
                    return;
                }

                double max = Math.Max(_startupTotalWeight, DefaultStartupStepWeight);
                _startupProgressTimer.Stop();
                startupProgressBar.Maximum = max;
                startupProgressBar.Value = max;
                _startupProgressTarget = max;
                _startupProgressSoftCap = max;
                _startupProgressCreepEnabled = false;
            }, DispatcherPriority.Normal).Task;
        }

        private async Task YieldToUiIfDueAsync()
        {
            long now = Stopwatch.GetTimestamp();
            if (_lastStartupYieldTimestamp != 0)
            {
                double elapsedMs = (now - _lastStartupYieldTimestamp) * 1000d / Stopwatch.Frequency;
                if (elapsedMs < StartupUiYieldIntervalMs)
                {
                    return;
                }
            }

            _lastStartupYieldTimestamp = now;
            await YieldToUiAsync();
        }

        private static Task YieldToUiAsync()
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.HasShutdownStarted)
            {
                return Task.CompletedTask;
            }

            return dispatcher.InvokeAsync(static () => { }, DispatcherPriority.Background).Task;
        }

        private void ShowMainWindowAndClose()
        {
            try
            {
                if (Application.Current is App app)
                    app.StartupSession.AddResults(_initializerResults);
                var parser = ArgumentParser.GetInstance();
                parser.AddArgument("feature", false, "e");
                parser.Parse();

                string feature = parser.GetValue("feature");
                if (feature != null)
                {
                    List<IFeatureLauncher> IFeatureLaunchers = AssemblyHandler.GetInstance().LoadImplementations<IFeatureLauncher>();
                    if (IFeatureLaunchers.Find(a => a.Header == feature) is IFeatureLauncher project1)
                    {
                        StartupRegistryChecker.MarkStage("FeatureLauncher", project1.GetType().FullName);
                        project1.Execute();
                        StartupRegistryChecker.Clear();
                    }
                    else if (IFeatureLaunchers.Find(a => a.GetType().ToString().contains(feature)) is IFeatureLauncher project2)
                    {
                        StartupRegistryChecker.MarkStage("FeatureLauncher", project2.GetType().FullName);
                        project2.Execute();
                        StartupRegistryChecker.Clear();
                    }
                    else
                    {
                        log.Info($"Feature '{feature}' not found, starting main window.");
                        CreateAndShowMainWindow();
                    }
                }
                else
                {
                    CreateAndShowMainWindow();
                }
                _ = StartFailureWatchdogAsync();
                ScheduleServiceHostStartupUpdate();
                Close();
            }
            catch (Exception ex)
            {
                log.Error("Main window creation failed.", ex);
                MessageBox.Show("MainWindow Create Error:" + ex.Message);
                Environment.Exit(-1);
            }
        }

        private void CreateAndShowMainWindow()
        {
            StartupUiTrace? trace = StartupUiTrace.Start(Application.Current.Dispatcher);
            try
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                Window mainWindow = MainWindowFactory.Create(MainWindowConfig.Instance.UseCompactMainWindow);
                trace?.Observe(mainWindow);
                mainWindow.ContentRendered += SaveProfileAfterFirstRender;
                mainWindow.Show();
                trace?.MarkShowReturned();
                log.Info($"Main window creation and Show completed in {stopwatch.ElapsedMilliseconds} ms (before ContentRendered).");
            }
            catch
            {
                trace?.Abort();
                throw;
            }
        }

        private void SaveProfileAfterFirstRender(object? sender, EventArgs e)
        {
            if (sender is Window window)
                window.ContentRendered -= SaveProfileAfterFirstRender;
            _ = Dispatcher.BeginInvoke(new Action(() =>
            {
                _ = Task.Run(() =>
                {
                    try { ConfigHandler.GetInstance().Save<StartupProgressProfileConfig>(); }
                    catch (Exception ex) { log.Warn("Failed to persist startup progress profile.", ex); }
                });
            }), DispatcherPriority.ApplicationIdle);
        }

        private static async Task StartFailureWatchdogAsync()
        {
            try
            {
                if (await OperationsApplicationFailureWatchdog.TryStartAsync().ConfigureAwait(false))
                {
                    _ = WindowsApplicationRestartRegistration.TryUnregisterForWatchdog();
                    log.Info("Fixed-target local ColorVision failure watchdog is active.");
                }
                else
                    log.Warn("Local ColorVision failure watchdog is unavailable; Windows application restart registration remains as fallback.");
            }
            catch (Exception ex) { log.Warn("Unable to start the local failure watchdog.", ex); }
        }

        private static void ScheduleServiceHostStartupUpdate()
        {
            Dispatcher dispatcher = Application.Current.Dispatcher;
            _ = dispatcher.BeginInvoke(async () => await ServiceHostStartupUpdateChecker.CheckAndUpdateAsync().ConfigureAwait(true), DispatcherPriority.ApplicationIdle);
        }

    }

    public class StartupProgressProfileConfig : IConfig
    {
        public int Version { get; set; } = 1;

        public DateTime UpdatedAt { get; set; }

        public Dictionary<string, double> InitializerDurationsMs { get; set; } = new();
    }
}
