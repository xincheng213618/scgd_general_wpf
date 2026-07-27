using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.ServiceHost;
using ColorVision.UI.Shell;
using ColorVision.UI.LogImp;
using Dm.util;
using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Layout;
using log4net.Repository.Hierarchy;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
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

        public StartWindow()
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            InitializeComponent();
            _startupProgressTimer.Interval = TimeSpan.FromMilliseconds(100);
            _startupProgressTimer.Tick += StartupProgressTimer_Tick;
            Closed += (s, e) => _startupProgressTimer.Stop();
            ContentRendered += StartWindow_ContentRendered;
            Left = SystemParameters.WorkArea.Right - Width;
            Top = SystemParameters.WorkArea.Bottom - Height;
        }
        StartupTextBoxAppender TextBoxAppender { get; set; }
        Hierarchy Hierarchy { get; set; }

        private void Window_Initialized(object sender, EventArgs e)
        {
            labelVersion.Text = Assembly.GetExecutingAssembly().GetName().Version?.ToString();

#if (DEBUG == true)
            string info= $"{(DebugBuild(Assembly.GetExecutingAssembly()) ? "(Debug) " : "(Release)")}{(Debugger.IsAttached ? ColorVision.Properties.Resources.Debugging : "")} ({(IntPtr.Size == 4 ? "32" : "64")} {ColorVision.Properties.Resources.Bit} - {Assembly.GetExecutingAssembly().GetName().Version} - .NET Core {Environment.Version} Build {File.GetLastWriteTime(System.Windows.Forms.Application.ExecutablePath):yyyy.MM.dd}";
#else
            string info= $"{(DebugBuild(Assembly.GetExecutingAssembly()) ? "(Debug)" : "")}{(Debugger.IsAttached ? ColorVision.Properties.Resources.Debugging : "")}{(IntPtr.Size == 4 ? "32" : "64")} {ColorVision.Properties.Resources.Bit} -  {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version} - .NET Core {Environment.Version} Build {File.GetLastWriteTime(System.Windows.Forms.Application.ExecutablePath):yyyy/MM/dd}";
#endif
            log.Info(info);
            logTextBox.Text = ProgramTimer.InitAppender.Buffer.ToString();
            Hierarchy = (Hierarchy)LogManager.GetRepository();
            TextBoxAppender = new StartupTextBoxAppender(logTextBox);
            TextBoxAppender.Layout = new PatternLayout("%date{HH:mm:ss;fff} %-5level %message%newline");
            Hierarchy.Root.RemoveAppender(ProgramTimer.InitAppender);

            Hierarchy.Root.AddAppender(TextBoxAppender);
            log4net.Config.BasicConfigurator.Configure(Hierarchy);

            ThemeManager.Current.SystemThemeChanged += (e) => {
                Icon = new BitmapImage(new Uri($"pack://application:,,,/ColorVision;component/Assets/Image/{(e == Theme.Light ? "ColorVision.ico" : "ColorVision1.ico")}"));
            };
            if (ThemeManager.Current.SystemTheme == Theme.Dark)
                Icon = new BitmapImage(new Uri("pack://application:,,,/ColorVision;component/Assets/Image/ColorVision1.ico"));
        }

        private async void StartWindow_ContentRendered(object? sender, EventArgs e)
        {
            ContentRendered -= StartWindow_ContentRendered;
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            try
            {
                await Task.Run(RunStartupAsync);
                await Dispatcher.InvokeAsync(() =>
                {
                    DetachStartupAppender();
                    ShowMainWindowAndClose();
                }, DispatcherPriority.ContextIdle);
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    DetachStartupAppender();
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

        private void UpdateStartupProgress(double completedWeight, bool initializerRunning = false, double runningWeight = 0)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (startupProgressBar == null)
                {
                    return;
                }

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

        private async Task InitializedOver()
        {
            Stopwatch stopwatch = new Stopwatch();
            int completedSteps = 0;
            double completedWeight = 0;

            foreach (var initializer in _IComponentInitializers)
            {
                double stepWeight = GetStartupStepWeight(initializer);
                UpdateStartupProgress(completedWeight, initializerRunning: true, runningWeight: stepWeight);
                stopwatch.Restart();

                log.Info($"{Properties.Resources.Initializer} {initializer.GetType().Name}");
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
                _startupObservedDurationsMs[GetInitializerProfileKey(initializer)] = Math.Max(stopwatch.ElapsedMilliseconds, MinimumProfiledStepWeightMs);
                completedSteps++;
                completedWeight += stepWeight;
                UpdateStartupProgress(completedWeight);
                await YieldToUiIfDueAsync();

            }
            SaveStartupProgressProfile();
            await CompleteStartupProgressAsync();
        }

        private Task CompleteStartupProgressAsync()
        {
            return Dispatcher.InvokeAsync(() =>
            {
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
            }, DispatcherPriority.Send).Task;
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

            return dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background).Task;
        }

        private void DetachStartupAppender()
        {
            if (Hierarchy == null || TextBoxAppender == null)
            {
                return;
            }

            TextBoxAppender.FlushPendingLogs();
            Hierarchy.Root.RemoveAppender(TextBoxAppender);
            TextBoxAppender.Dispose();
            log4net.Config.BasicConfigurator.Configure(Hierarchy);
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
                        StartupRegistryChecker.Clear();
                        project1.Execute();
                    }
                    else if (IFeatureLaunchers.Find(a => a.GetType().ToString().contains(feature)) is IFeatureLauncher project2)
                    {
                        StartupRegistryChecker.Clear();
                        project2.Execute();
                    }
                    else
                    {
                        log.Info($"Feature '{feature}' not found, starting main window.");
                        Window mainWindow = new MainWindow();
                        mainWindow.Show();
                    }
                }
                else
                {
                    Window mainWindow = new MainWindow();
                    mainWindow.Show();
                }
                ScheduleServiceHostStartupUpdate();
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("MainWindow Create Error:" + ex.Message);
                Environment.Exit(-1);
            }
        }

        private static void ScheduleServiceHostStartupUpdate()
        {
            Dispatcher dispatcher = Application.Current.Dispatcher;
            _ = dispatcher.BeginInvoke(async () => await ServiceHostStartupUpdateChecker.CheckAndUpdateAsync().ConfigureAwait(true), DispatcherPriority.ApplicationIdle);
        }

        private void TextBoxMsg_TextChanged(object sender, TextChangedEventArgs e)
        {
            logTextBox.ScrollToEnd();
        }

        private sealed class StartupTextBoxAppender : AppenderSkeleton, IDisposable
        {
            private const int StartupLogFlushIntervalMs = 80;

            private readonly TextBox _textBox;
            private readonly StringBuilder _pendingLogs = new();
            private readonly object _lock = new();
            private readonly DispatcherTimer _flushTimer;
            private bool _isClosed;

            public StartupTextBoxAppender(TextBox textBox)
            {
                _textBox = textBox ?? throw new ArgumentNullException(nameof(textBox));
                _flushTimer = new DispatcherTimer(DispatcherPriority.Background, _textBox.Dispatcher)
                {
                    Interval = TimeSpan.FromMilliseconds(StartupLogFlushIntervalMs)
                };
                _flushTimer.Tick += (s, e) => FlushPendingLogsOnUi();
                _flushTimer.Start();
            }

            protected override void Append(LoggingEvent loggingEvent)
            {
                if (_isClosed)
                {
                    return;
                }

                string renderedMessage = RenderLoggingEvent(loggingEvent);
                lock (_lock)
                {
                    _pendingLogs.Append(renderedMessage);
                }
            }

            public void FlushPendingLogs()
            {
                if (_textBox.Dispatcher.CheckAccess())
                {
                    FlushPendingLogsOnUi();
                    return;
                }

                _textBox.Dispatcher.Invoke(FlushPendingLogsOnUi, DispatcherPriority.Send);
            }

            private void FlushPendingLogsOnUi()
            {
                string logs;
                lock (_lock)
                {
                    logs = _pendingLogs.ToString();
                    _pendingLogs.Clear();
                }

                if (logs.Length == 0)
                {
                    return;
                }

                _textBox.AppendText(logs);
                TrimTextBox();
            }

            private void TrimTextBox()
            {
                if (LogConfig.Instance.MaxChars <= LogConstants.MinMaxCharsForTrimming || _textBox.Text.Length <= LogConfig.Instance.MaxChars)
                {
                    return;
                }

                _textBox.Text = _textBox.Text.Substring(_textBox.Text.Length - LogConfig.Instance.MaxChars);
            }

            protected override void OnClose()
            {
                if (_isClosed)
                {
                    return;
                }

                _isClosed = true;
                _flushTimer.Stop();
                FlushPendingLogs();
                base.OnClose();
            }

            public void Dispose()
            {
                Close();
                GC.SuppressFinalize(this);
            }
        }

    }

    public class StartupProgressProfileConfig : IConfig
    {
        public int Version { get; set; } = 1;

        public DateTime UpdatedAt { get; set; }

        public Dictionary<string, double> InitializerDurationsMs { get; set; } = new();
    }
}
