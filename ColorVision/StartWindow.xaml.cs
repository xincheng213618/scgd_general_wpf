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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
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
        private const double DefaultStartupStepWeight = 1d;
        private const double MinimumProfiledStepWeightMs = 20d;
        private const double MaximumProfiledStepWeightMs = 12000d;
        private const double StartupProfileSmoothing = 0.35d;
        private ThemeManager? _subscribedThemeManager;

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

#if (DEBUG == true)
            string info= $"{(DebugBuild(Assembly.GetExecutingAssembly()) ? "(Debug) " : "(Release)")}{(Debugger.IsAttached ? ColorVision.Properties.Resources.Debugging : "")} ({(IntPtr.Size == 4 ? "32" : "64")} {ColorVision.Properties.Resources.Bit} - {Assembly.GetExecutingAssembly().GetName().Version} - .NET Core {Environment.Version} Build {File.GetLastWriteTime(System.Windows.Forms.Application.ExecutablePath):yyyy.MM.dd}";
#else
            string info= $"{(DebugBuild(Assembly.GetExecutingAssembly()) ? "(Debug)" : "")}{(Debugger.IsAttached ? ColorVision.Properties.Resources.Debugging : "")}{(IntPtr.Size == 4 ? "32" : "64")} {ColorVision.Properties.Resources.Bit} -  {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version} - .NET Core {Environment.Version} Build {File.GetLastWriteTime(System.Windows.Forms.Application.ExecutablePath):yyyy/MM/dd}";
#endif
            log.Info(info);
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
            log.Info("Startup splash ContentRendered.");
            Stopwatch handoffStopwatch = Stopwatch.StartNew();
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            log.Info($"Startup splash ApplicationIdle handoff took {handoffStopwatch.ElapsedMilliseconds} ms.");
            try
            {
                handoffStopwatch.Restart();
                await Task.Run(() =>
                {
                    log.Info($"Startup worker queue took {handoffStopwatch.ElapsedMilliseconds} ms. UI={Dispatcher.CheckAccess()}.");
                    return RunStartupAsync();
                });
                handoffStopwatch.Restart();
                await Dispatcher.InvokeAsync(() =>
                {
                    log.Info($"Startup main window queue took {handoffStopwatch.ElapsedMilliseconds} ms.");
                    ShowMainWindowAndClose();
                }, DispatcherPriority.ContextIdle);
            }
            catch (Exception ex)
            {
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
            Stopwatch discoveryStopwatch = Stopwatch.StartNew();
            _IComponentInitializers = CreateSortedInitializers();
            log.Info($"Startup initializer discovery took {discoveryStopwatch.ElapsedMilliseconds} ms. Count={_IComponentInitializers.Count}.");
            _startupTotalSteps = _IComponentInitializers.Count;
            LoadStartupProgressProfile();
            UpdateStartupProgress(0);
            await YieldToUiAsync("discovery");
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
            log.Info($"Startup progress profile loaded. Steps={_startupTotalSteps}, Weight={_startupTotalWeight:0.##}");
        }

        private void SaveStartupProgressProfile()
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
                ConfigHandler.GetInstance().Save<StartupProgressProfileConfig>();
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

            double target = _startupProgressTarget;
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


        private static bool DebugBuild(Assembly assembly)
        {
            foreach (object attribute in assembly.GetCustomAttributes(false))
            {
                if (attribute is DebuggableAttribute _attribute)
                {
                    return _attribute.IsJITTrackingEnabled;
                }
            }   
            return false;
        }

        private static string GetStartupStage(IInitializer initializer) => StartupText.GetStage(initializer.GetType().Name);

        private sealed record StartupInitializerResult(IInitializer Initializer, long ElapsedMilliseconds);

        private static bool IsDatabaseInitializer(IInitializer initializer) =>
            initializer is global::ColorVision.Engine.MySqlInitializer;

        private static bool IsWorkspaceInitializer(IInitializer initializer) =>
            initializer is global::ColorVision.Solution.SolutionManagerInitializer;

        private static bool IsMqttInitializer(IInitializer initializer) =>
            initializer is global::ColorVision.Engine.MQTT.MqttInitializer;

        private static bool IsRcInitializer(IInitializer initializer) =>
            initializer is global::ColorVision.Engine.Services.RC.RCInitializer;

        private static bool IsTemplateInitializer(IInitializer initializer) =>
            initializer is global::ColorVision.Engine.Templates.TemplateInitializer;

        private async Task<StartupInitializerResult> RunInitializerAsync(IInitializer initializer)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            log.Info($"{Properties.Resources.Initializer} {initializer.GetType().Name}. UI={Dispatcher.CheckAccess()}.");
            try
            {
                await initializer.InitializeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }

            stopwatch.Stop();
            log.Info($"Initializer {initializer.GetType().Name} took {stopwatch.ElapsedMilliseconds} ms.");
            return new StartupInitializerResult(initializer, stopwatch.ElapsedMilliseconds);
        }

        private async Task<IReadOnlyList<StartupInitializerResult>> RunConnectivityInitializersAsync(
            IReadOnlyList<IInitializer> initializers)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            log.Info($"Startup connectivity initializer lane started. Count={initializers.Count}.");
            List<StartupInitializerResult> results = new(initializers.Count);
            foreach (IInitializer initializer in initializers)
                results.Add(await RunInitializerAsync(initializer).ConfigureAwait(false));

            log.Info($"Startup connectivity initializer lane completed in {stopwatch.ElapsedMilliseconds} ms.");
            return results;
        }

        private async Task<StartupInitializerResult> RunWorkspaceInitializerAsync(IInitializer initializer)
        {
            StartupInitializerResult result = await RunInitializerAsync(initializer).ConfigureAwait(false);
            if (Dispatcher.HasShutdownStarted)
                return result;

            long queuedAt = Stopwatch.GetTimestamp();
            await Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.Normal).Task.ConfigureAwait(false);
            log.Info($"Startup workspace UI barrier took {Stopwatch.GetElapsedTime(queuedAt).TotalMilliseconds:0.###} ms.");
            return result;
        }

        private async Task InitializedOver()
        {
            Stopwatch executionStopwatch = Stopwatch.StartNew();
            double completedWeight = 0;
            long summedInitializerMilliseconds = 0;

            int databaseIndex = _IComponentInitializers.FindIndex(IsDatabaseInitializer);
            bool canRunPrerequisiteLanes = databaseIndex >= 0
                && databaseIndex + 4 < _IComponentInitializers.Count
                && IsWorkspaceInitializer(_IComponentInitializers[databaseIndex + 1])
                && IsMqttInitializer(_IComponentInitializers[databaseIndex + 2])
                && IsRcInitializer(_IComponentInitializers[databaseIndex + 3])
                && IsTemplateInitializer(_IComponentInitializers[databaseIndex + 4]);
            int mqttIndex = databaseIndex + 2;
            List<IInitializer> connectivityInitializers = canRunPrerequisiteLanes
                ? [_IComponentInitializers[mqttIndex], _IComponentInitializers[mqttIndex + 1]]
                : [];

            async Task RecordCompletionAsync(StartupInitializerResult result)
            {
                IInitializer initializer = result.Initializer;
                _startupObservedDurationsMs[GetInitializerProfileKey(initializer)] =
                    Math.Max(result.ElapsedMilliseconds, MinimumProfiledStepWeightMs);
                summedInitializerMilliseconds += result.ElapsedMilliseconds;
                completedWeight += GetStartupStepWeight(initializer);
                UpdateStartupProgress(completedWeight);
                await YieldToUiIfDueAsync(initializer.Name);
            }

            for (int index = 0; index < _IComponentInitializers.Count; index++)
            {
                IInitializer initializer = _IComponentInitializers[index];

                if (canRunPrerequisiteLanes && index == databaseIndex)
                {
                    IInitializer workspaceInitializer = _IComponentInitializers[index + 1];
                    string connectivityComponent = string.Join(" -> ", connectivityInitializers.Select(item => item.Name));
                    string component = $"{initializer.Name} || {workspaceInitializer.Name} || ({connectivityComponent})";
                    StartupRegistryChecker.MarkStage("StartupInitializerLane", component);
                    UpdateStartupProgress(
                        completedWeight,
                        initializerRunning: true,
                        runningWeight: GetStartupStepWeight(initializer)
                            + GetStartupStepWeight(workspaceInitializer)
                            + connectivityInitializers.Sum(GetStartupStepWeight),
                        stage: GetStartupStage(initializer));

                    Stopwatch laneStopwatch = Stopwatch.StartNew();
                    log.Info($"Startup prerequisite lanes started. Component={component}.");
                    Task<StartupInitializerResult> databaseTask = Task.Run(() => RunInitializerAsync(initializer));
                    Task<StartupInitializerResult> workspaceTask = Task.Run(() => RunWorkspaceInitializerAsync(workspaceInitializer));
                    Task<IReadOnlyList<StartupInitializerResult>> connectivityTask =
                        Task.Run(() => RunConnectivityInitializersAsync(connectivityInitializers));

                    StartupInitializerResult[] independentResults =
                        await Task.WhenAll(databaseTask, workspaceTask).ConfigureAwait(false);
                    IReadOnlyList<StartupInitializerResult> connectivityResults =
                        await connectivityTask.ConfigureAwait(false);
                    foreach (StartupInitializerResult result in independentResults)
                        await RecordCompletionAsync(result);
                    foreach (StartupInitializerResult result in connectivityResults)
                        await RecordCompletionAsync(result);
                    log.Info($"Startup prerequisite lanes completed in {laneStopwatch.ElapsedMilliseconds} ms.");

                    index += 3;
                    continue;
                }

                StartupRegistryChecker.MarkStage("StartupInitializer", initializer.Name);
                double stepWeight = GetStartupStepWeight(initializer);
                UpdateStartupProgress(completedWeight, initializerRunning: true, runningWeight: stepWeight, stage: GetStartupStage(initializer));
                await RecordCompletionAsync(await RunInitializerAsync(initializer).ConfigureAwait(false));
            }

            executionStopwatch.Stop();
            long overlapMilliseconds = Math.Max(0, summedInitializerMilliseconds - executionStopwatch.ElapsedMilliseconds);
            log.Info($"Startup initializers completed in {executionStopwatch.ElapsedMilliseconds} ms. " +
                $"Summed={summedInitializerMilliseconds} ms, ParallelOverlap={overlapMilliseconds} ms.");
            StartupRegistryChecker.MarkStage("StartupInitializersCompleted");
            SaveStartupProgressProfile();
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

        private async Task YieldToUiIfDueAsync(string initializerName)
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
            await YieldToUiAsync(initializerName);
        }

        private static Task YieldToUiAsync(string stage)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.HasShutdownStarted)
            {
                return Task.CompletedTask;
            }

            long queuedAt = Stopwatch.GetTimestamp();
            return dispatcher.InvokeAsync(() =>
            {
                log.Info($"Startup UI checkpoint '{stage}' queue took {Stopwatch.GetElapsedTime(queuedAt).TotalMilliseconds:0.###} ms.");
            }, DispatcherPriority.Background).Task;
        }

        private void ShowMainWindowAndClose()
        {
            try
            {
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
                if (OperationsApplicationFailureWatchdog.TryStart())
                {
                    _ = WindowsApplicationRestartRegistration.TryUnregisterForWatchdog();
                    log.Info("Fixed-target local ColorVision failure watchdog is active.");
                }
                else
                {
                    log.Warn("Local ColorVision failure watchdog is unavailable; Windows application restart registration remains as fallback.");
                }
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

        private static void CreateAndShowMainWindow()
        {
            StartupUiTrace? trace = StartupUiTrace.Start(Application.Current.Dispatcher);
            try
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                Window mainWindow = MainWindowFactory.Create(MainWindowConfig.Instance.UseCompactMainWindow);
                trace?.Observe(mainWindow);
                log.Info($"Main window factory construction took {stopwatch.ElapsedMilliseconds} ms.");
                stopwatch.Restart();
                mainWindow.Show();
                trace?.MarkShowReturned();
                log.Info($"Main window Show took {stopwatch.ElapsedMilliseconds} ms (before ContentRendered).");
            }
            catch
            {
                trace?.Abort();
                throw;
            }
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
