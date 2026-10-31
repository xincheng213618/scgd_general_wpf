#pragma warning disable CA1513,CA1716,CA1863
using ColorVision.UI.Properties;
using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace ColorVision.UI
{
    public sealed class TimedButtonOperationOptions
    {
        public string OperationKey { get; set; } = string.Empty;
        public Func<TimedButtonOperationStats?, object>? ContentFactory { get; set; }
        public Func<TimedButtonOperationStats?, string?>? ToolTipFactory { get; set; }
        public Func<double>? ExpectedDurationProvider { get; set; }
        public Action<double>? OnSuccessfulCompletion { get; set; }
        public string? RunningText { get; set; }
        public Brush? ProgressForeground { get; set; } = Brushes.Red;
        public bool TreatFirstSuccessAsWarmup { get; set; } = true;
        public bool DisableButtonWhileRunning { get; set; } = true;
        public bool PersistStatsImmediately { get; set; } = true;
        public double MinimumExpectedDurationMs { get; set; } = 500;
    }

    public sealed class TimedButtonOperation : IDisposable
    {
        private const string StatsContextMenuTag = "TimedButtonOperation.OpenStatsWindow";
        private readonly Button _button;
        private readonly TimedButtonOperationOptions _options;
        private readonly DispatcherTimer _timer;
        private readonly Stopwatch _stopwatch = new Stopwatch();
        private readonly TimedButtonProgressHost _progressHost;
        private readonly object? _originalToolTip;
        private string? _assignedToolTip;
        private string _runningText = string.Empty;
        private double _expectedDurationMs;
        private bool _isRunning;
        private bool _statsShortcutAttached;
        private bool _buttonWasEnabled;

        public TimedButtonOperation(Button button, TimedButtonOperationOptions options)
        {
            ArgumentNullException.ThrowIfNull(button);
            ArgumentNullException.ThrowIfNull(options);

            if (string.IsNullOrWhiteSpace(options.OperationKey))
            {
                throw new ArgumentException("OperationKey cannot be empty.", nameof(options));
            }

            _button = button;
            _options = options;
            _originalToolTip = button.ToolTip;
            _progressHost = new TimedButtonProgressHost(button, options.ProgressForeground);
            _timer = new DispatcherTimer(DispatcherPriority.Background, button.Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            _timer.Tick += Timer_Tick;
            TimedButtonOperationStatsManager.StatsChanged += TimedButtonOperationStatsManager_StatsChanged;
            EnsureStatsShortcutAttached();

            RefreshIdleState();
        }

        public bool IsRunning => _isRunning;

        public TimedButtonOperationStats? CurrentStats => TimedButtonOperationStatsStore.Get(_options.OperationKey);

        public void RefreshIdleState()
        {
            EnsureStatsShortcutAttached();

            if (_isRunning)
            {
                return;
            }

            TimedButtonOperationStats? stats = CurrentStats;

            if (_options.ContentFactory != null)
            {
                object? content = _options.ContentFactory(stats);
                if (content != null)
                {
                    _button.Content = content;
                }
            }

            if (_options.ToolTipFactory != null)
            {
                _assignedToolTip = _options.ToolTipFactory(stats);
                _button.ToolTip = _assignedToolTip;
            }
        }

        public TimedButtonOperationScope Begin(double? expectedDurationMs = null, string? runningText = null)
        {
            if (_isRunning)
            {
                throw new InvalidOperationException("Timed button operation is already running.");
            }

            _button.Dispatcher.VerifyAccess();

            _isRunning = true;
            _buttonWasEnabled = _button.IsEnabled;
            if (_options.DisableButtonWhileRunning)
            {
                _button.IsEnabled = false;
            }

            _expectedDurationMs = ResolveExpectedDuration(expectedDurationMs);
            _runningText = runningText ?? _options.RunningText ?? _button.Content?.ToString() ?? string.Empty;

            _progressHost.Show();
            _progressHost.UpdateProgress(0, _runningText);
            _stopwatch.Restart();
            _timer.Start();
            return new TimedButtonOperationScope(this);
        }

        internal void Complete(bool success)
        {
            if (!_isRunning)
            {
                return;
            }

            _timer.Stop();
            _stopwatch.Stop();
            _progressHost.Remove();

            if (_options.DisableButtonWhileRunning)
            {
                _button.IsEnabled = _buttonWasEnabled;
            }

            _isRunning = false;
            double elapsedMilliseconds = _stopwatch.Elapsed.TotalMilliseconds;

            if (success)
            {
                TimedButtonOperationRecordResult recordResult = TimedButtonOperationStatsStore.Record(
                    _options.OperationKey,
                    elapsedMilliseconds,
                    _options.TreatFirstSuccessAsWarmup,
                    _options.PersistStatsImmediately);

                TimedButtonOperationStatsManager.NotifyStatsChanged(_options.OperationKey);

                if (!recordResult.WasWarmup)
                {
                    _options.OnSuccessfulCompletion?.Invoke(elapsedMilliseconds);
                }
            }

            RefreshIdleState();
        }

        private double ResolveExpectedDuration(double? expectedDurationMs)
        {
            if (expectedDurationMs.HasValue && expectedDurationMs.Value > 0)
            {
                return Math.Max(_options.MinimumExpectedDurationMs, expectedDurationMs.Value);
            }

            TimedButtonOperationStats? stats = CurrentStats;
            if (TimedButtonOperationWarmupSession.NeedsWarmup(_options.OperationKey, _options.TreatFirstSuccessAsWarmup)
                && stats?.WarmupCount > 0
                && stats.WarmupElapsedMs > 0)
            {
                return Math.Max(_options.MinimumExpectedDurationMs, stats.WarmupElapsedMs);
            }

            double configuredDuration = _options.ExpectedDurationProvider?.Invoke() ?? 0;
            if (configuredDuration > 0)
            {
                return Math.Max(_options.MinimumExpectedDurationMs, configuredDuration);
            }

            if (stats != null)
            {
                if (stats.SuccessCount > 0)
                {
                    double historicalDuration = stats.SuccessCount > 1 ? stats.AverageElapsedMs : stats.LastElapsedMs;
                    if (historicalDuration > 0)
                    {
                        return Math.Max(_options.MinimumExpectedDurationMs, historicalDuration);
                    }
                }

                if (stats.WarmupCount > 0 && stats.WarmupElapsedMs > 0)
                {
                    return Math.Max(_options.MinimumExpectedDurationMs, stats.WarmupElapsedMs);
                }
            }

            return _options.MinimumExpectedDurationMs;
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            double elapsedMilliseconds = _stopwatch.Elapsed.TotalMilliseconds;
            double progress = _expectedDurationMs <= 0
                ? 99
                : Math.Min(99, (elapsedMilliseconds / _expectedDurationMs) * 100);

            _progressHost.UpdateProgress(progress, _runningText);
        }

        private void EnsureStatsShortcutAttached()
        {
            if (_statsShortcutAttached)
            {
                return;
            }

            ITimedButtonOperationStatsWindowLauncher? launcher = TimedButtonOperationStatsWindowLauncherProvider.GetLauncher();
            if (launcher == null || !launcher.CanOpen)
            {
                return;
            }

            ContextMenu contextMenu = _button.ContextMenu ?? new ContextMenu();
            bool alreadyExists = contextMenu.Items
                .OfType<MenuItem>()
                .Any(item => string.Equals(item.Tag as string, StatsContextMenuTag, StringComparison.Ordinal));

            if (alreadyExists)
            {
                _button.ContextMenu = contextMenu;
                _statsShortcutAttached = true;
                return;
            }

            if (contextMenu.Items.Count > 0)
            {
                contextMenu.Items.Add(new Separator());
            }

            MenuItem menuItem = new MenuItem
            {
                Header = Resources.TimedOp_ViewStats,
                Tag = StatsContextMenuTag
            };
            menuItem.Click += (_, _) => launcher.OpenWindow(_options.OperationKey);

            contextMenu.Items.Add(menuItem);
            _button.ContextMenu = contextMenu;
            _statsShortcutAttached = true;
        }

        private void TimedButtonOperationStatsManager_StatsChanged(object? sender, TimedButtonOperationStatsChangedEventArgs e)
        {
            if (_isRunning)
            {
                return;
            }

            if (!e.IsGlobalChange && !string.Equals(e.OperationKey, _options.OperationKey, StringComparison.Ordinal))
            {
                return;
            }

            if (_button.Dispatcher.CheckAccess())
            {
                RefreshIdleState();
                return;
            }

            _button.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(RefreshIdleState));
        }

        public void Dispose()
        {
            TimedButtonOperationStatsManager.StatsChanged -= TimedButtonOperationStatsManager_StatsChanged;
            _timer.Tick -= Timer_Tick;
            _timer.Stop();
            _progressHost.Dispose();
            if (ReferenceEquals(_button.ToolTip, _assignedToolTip))
            {
                _button.ToolTip = _originalToolTip;
            }
            GC.SuppressFinalize(this);
        }
    }

    public sealed class TimedButtonOperationScope : IDisposable
    {
        private TimedButtonOperation? _owner;
        private bool _completed;

        internal TimedButtonOperationScope(TimedButtonOperation owner)
        {
            _owner = owner;
        }

        public void Complete(bool success)
        {
            if (_completed)
            {
                return;
            }

            _completed = true;
            _owner?.Complete(success);
            _owner = null;
        }

        public void CompleteSuccess()
        {
            Complete(true);
        }

        public void Dispose()
        {
            Complete(false);
        }
    }
}
