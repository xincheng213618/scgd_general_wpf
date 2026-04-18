using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace ColorVision.UI
{
    public sealed class TimedButtonOperationStats
    {
        public int SuccessCount { get; set; }
        public int WarmupCount { get; set; }
        public double WarmupElapsedMs { get; set; }
        public double LastElapsedMs { get; set; }
        public double AverageElapsedMs { get; set; }
        public double BestElapsedMs { get; set; }
        public double WorstElapsedMs { get; set; }
        public DateTime LastCompletedAt { get; set; }

        public void Record(double elapsedMilliseconds, bool treatAsWarmupSample)
        {
            elapsedMilliseconds = Math.Max(0, elapsedMilliseconds);
            LastCompletedAt = DateTime.Now;

            if (treatAsWarmupSample)
            {
                WarmupCount++;
                WarmupElapsedMs = elapsedMilliseconds;
                return;
            }

            SuccessCount++;
            LastElapsedMs = elapsedMilliseconds;

            if (SuccessCount == 1)
            {
                AverageElapsedMs = elapsedMilliseconds;
                BestElapsedMs = elapsedMilliseconds;
                WorstElapsedMs = elapsedMilliseconds;
                return;
            }

            AverageElapsedMs = ((AverageElapsedMs * (SuccessCount - 1)) + elapsedMilliseconds) / SuccessCount;
            BestElapsedMs = Math.Min(BestElapsedMs, elapsedMilliseconds);
            WorstElapsedMs = Math.Max(WorstElapsedMs, elapsedMilliseconds);
        }

        public TimedButtonOperationStats Clone()
        {
            return new TimedButtonOperationStats
            {
                SuccessCount = SuccessCount,
                WarmupCount = WarmupCount,
                WarmupElapsedMs = WarmupElapsedMs,
                LastElapsedMs = LastElapsedMs,
                AverageElapsedMs = AverageElapsedMs,
                BestElapsedMs = BestElapsedMs,
                WorstElapsedMs = WorstElapsedMs,
                LastCompletedAt = LastCompletedAt
            };
        }
    }

    public sealed class TimedButtonOperationStatsConfig : IConfig
    {
        public Dictionary<string, TimedButtonOperationStats> Records { get; set; } = new Dictionary<string, TimedButtonOperationStats>();
    }

    public sealed class TimedButtonOperationOptions
    {
        public string OperationKey { get; set; } = string.Empty;
        public Func<TimedButtonOperationStats?, object>? ContentFactory { get; set; }
        public Func<TimedButtonOperationStats?, string?>? ToolTipFactory { get; set; }
        public Func<double>? ExpectedDurationProvider { get; set; }
        public Action<double>? OnSuccessfulCompletion { get; set; }
        public string? RunningText { get; set; }
        public Brush? ProgressForeground { get; set; }
        public bool TreatFirstSuccessAsWarmup { get; set; }
        public bool DisableButtonWhileRunning { get; set; } = true;
        public bool PersistStatsImmediately { get; set; } = true;
        public double MinimumExpectedDurationMs { get; set; } = 500;
    }

    public sealed class TimedButtonOperation : IDisposable
    {
        private readonly Button _button;
        private readonly TimedButtonOperationOptions _options;
        private readonly DispatcherTimer _timer;
        private readonly Stopwatch _stopwatch = new Stopwatch();
        private readonly TimedButtonProgressHost _progressHost;
        private string _runningText = string.Empty;
        private double _expectedDurationMs;
        private bool _isRunning;
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
            _progressHost = new TimedButtonProgressHost(button, options.ProgressForeground);
            _timer = new DispatcherTimer(DispatcherPriority.Background, button.Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            _timer.Tick += Timer_Tick;

            RefreshIdleState();
        }

        public bool IsRunning => _isRunning;

        public TimedButtonOperationStats? CurrentStats => TimedButtonOperationStatsStore.Get(_options.OperationKey);

        public void RefreshIdleState()
        {
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
                _button.ToolTip = _options.ToolTipFactory(stats);
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
            if (_progressHost.IsHosted)
            {
                _button.Visibility = Visibility.Hidden;
            }

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
            _button.Visibility = Visibility.Visible;
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

        public void Dispose()
        {
            _timer.Tick -= Timer_Tick;
            _timer.Stop();
            _progressHost.Dispose();
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

    public static class TimedButtonOperationTextFormatter
    {
        public static object BuildCompactContent(string label, TimedButtonOperationStats? stats)
        {
            return label;
        }

        public static string BuildTooltip(string label, TimedButtonOperationStats? stats)
        {
            if (stats == null || (stats.SuccessCount == 0 && stats.WarmupCount == 0))
            {
                return $"{label}\n暂无历史耗时。";
            }

            List<string> lines = new List<string> { label };

            if (stats.WarmupCount > 0)
            {
                lines.Add($"预热: {FormatDuration(stats.WarmupElapsedMs)}");
            }

            if (stats.SuccessCount == 0)
            {
                lines.Add("稳定样本暂无记录。");
                lines.Add("本次软件启动的首轮样本按预热处理，不参与平均。");
                return string.Join("\n", lines);
            }

            lines.Add($"上次: {FormatDuration(stats.LastElapsedMs)}");
            lines.Add($"平均: {FormatDuration(stats.AverageElapsedMs)}");
            lines.Add($"最快: {FormatDuration(stats.BestElapsedMs)}");
            lines.Add($"最慢: {FormatDuration(stats.WorstElapsedMs)}");
            lines.Add($"稳定样本: {stats.SuccessCount}");

            if (stats.WarmupCount > 0)
            {
                lines.Add("本次软件启动的首轮样本按预热处理，不参与平均。");
            }

            lines.Add(BuildTrendText(stats));
            return string.Join("\n", lines);
        }

        public static string FormatDuration(double elapsedMilliseconds)
        {
            if (elapsedMilliseconds < 1000)
            {
                return $"{elapsedMilliseconds:F0} ms";
            }

            return $"{elapsedMilliseconds / 1000:F1} s";
        }

        private static string BuildTrendText(TimedButtonOperationStats stats)
        {
            if (stats.SuccessCount <= 1)
            {
                return "稳定样本仍然较少，继续执行后会更准确。";
            }

            if (stats.AverageElapsedMs <= 0)
            {
                return "耗时统计已记录。";
            }

            double deltaRatio = (stats.LastElapsedMs - stats.AverageElapsedMs) / stats.AverageElapsedMs;
            if (Math.Abs(deltaRatio) < 0.05)
            {
                return "本次与历史平均接近。";
            }

            return deltaRatio < 0
                ? $"本次比历史平均快 {Math.Abs(deltaRatio):P0}。"
                : $"本次比历史平均慢 {Math.Abs(deltaRatio):P0}。";
        }
    }

    public interface ITimedButtonOperationStatsRepository
    {
        TimedButtonOperationStats? Get(string operationKey);
        TimedButtonOperationRecordResult Record(string operationKey, double elapsedMilliseconds, bool treatAsWarmupSample, bool persistImmediately);
    }

    public readonly struct TimedButtonOperationRecordResult
    {
        public TimedButtonOperationRecordResult(TimedButtonOperationStats stats, bool wasWarmup)
        {
            Stats = stats;
            WasWarmup = wasWarmup;
        }

        public TimedButtonOperationStats Stats { get; }
        public bool WasWarmup { get; }
    }

    public static class TimedButtonOperationWarmupSession
    {
        private static readonly object SyncRoot = new object();
        private static readonly HashSet<string> WarmedOperationKeys = new HashSet<string>(StringComparer.Ordinal);

        public static bool NeedsWarmup(string operationKey, bool enabled)
        {
            if (!enabled || string.IsNullOrWhiteSpace(operationKey))
            {
                return false;
            }

            lock (SyncRoot)
            {
                return !WarmedOperationKeys.Contains(operationKey.Trim());
            }
        }

        public static bool ConsumeWarmup(string operationKey, bool enabled)
        {
            if (!enabled || string.IsNullOrWhiteSpace(operationKey))
            {
                return false;
            }

            string normalizedKey = operationKey.Trim();
            lock (SyncRoot)
            {
                if (WarmedOperationKeys.Contains(normalizedKey))
                {
                    return false;
                }

                WarmedOperationKeys.Add(normalizedKey);
                return true;
            }
        }
    }

    public static class TimedButtonOperationStatsRepositoryProvider
    {
        private static readonly object SyncRoot = new object();
        private static ITimedButtonOperationStatsRepository? _repository;

        public static void SetRepository(ITimedButtonOperationStatsRepository repository)
        {
            ArgumentNullException.ThrowIfNull(repository);

            lock (SyncRoot)
            {
                _repository = repository;
            }
        }

        internal static ITimedButtonOperationStatsRepository GetRepository()
        {
            lock (SyncRoot)
            {
                return _repository ??= new TimedButtonOperationConfigStatsRepository();
            }
        }
    }

    internal sealed class TimedButtonOperationConfigStatsRepository : ITimedButtonOperationStatsRepository
    {
        public TimedButtonOperationStats? Get(string operationKey)
        {
            if (string.IsNullOrWhiteSpace(operationKey))
            {
                return null;
            }

            TimedButtonOperationStatsConfig config = ConfigHandler.GetInstance().GetRequiredService<TimedButtonOperationStatsConfig>();
            config.Records ??= new Dictionary<string, TimedButtonOperationStats>();
            config.Records.TryGetValue(operationKey.Trim(), out TimedButtonOperationStats? stats);
            return stats?.Clone();
        }

        public TimedButtonOperationRecordResult Record(string operationKey, double elapsedMilliseconds, bool treatAsWarmupSample, bool persistImmediately)
        {
            TimedButtonOperationStatsConfig config = ConfigHandler.GetInstance().GetRequiredService<TimedButtonOperationStatsConfig>();
            config.Records ??= new Dictionary<string, TimedButtonOperationStats>();

            string normalizedKey = operationKey.Trim();
            if (!config.Records.TryGetValue(normalizedKey, out TimedButtonOperationStats? stats))
            {
                stats = new TimedButtonOperationStats();
                config.Records[normalizedKey] = stats;
            }

            stats.Record(elapsedMilliseconds, treatAsWarmupSample);

            if (persistImmediately)
            {
                ConfigHandler.GetInstance().Save<TimedButtonOperationStatsConfig>();
            }

            return new TimedButtonOperationRecordResult(stats.Clone(), treatAsWarmupSample);
        }
    }

    internal static class TimedButtonOperationStatsStore
    {
        public static TimedButtonOperationStats? Get(string operationKey)
        {
            return TimedButtonOperationStatsRepositoryProvider.GetRepository().Get(operationKey);
        }

        public static TimedButtonOperationRecordResult Record(string operationKey, double elapsedMilliseconds, bool treatFirstSuccessAsWarmup, bool persistImmediately)
        {
            bool treatAsWarmupSample = TimedButtonOperationWarmupSession.ConsumeWarmup(operationKey, treatFirstSuccessAsWarmup);
            return TimedButtonOperationStatsRepositoryProvider.GetRepository().Record(operationKey, elapsedMilliseconds, treatAsWarmupSample, persistImmediately);
        }
    }

    internal sealed class TimedButtonProgressHost : IDisposable
    {
        private readonly Button _button;
        private readonly Brush? _progressForeground;
        private readonly Grid _host = new Grid();
        private readonly Border _overlay;
        private readonly Grid _layoutRoot;
        private readonly Border _progressTrack;
        private readonly Border _progressFill;
        private readonly TextBlock _baseTextBlock;
        private readonly TextBlock _filledTextBlock;
        private readonly RectangleGeometry _filledTextClip = new RectangleGeometry();
        private bool _isHosted;
        private bool _overlayInserted;
        private double _progressValue;

        public bool IsHosted => _isHosted;

        public TimedButtonProgressHost(Button button, Brush? progressForeground)
        {
            _button = button;
            _progressForeground = progressForeground;
            _isHosted = TryCreateHost();

            _overlay = new Border
            {
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2),
                Opacity = 0.98,
                SnapsToDevicePixels = true,
                UseLayoutRounding = true
            };
            _overlay.SetResourceReference(Border.BackgroundProperty, "GlobalBackground");
            _overlay.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
            _overlay.IsHitTestVisible = false;
            _overlay.SizeChanged += Overlay_SizeChanged;

            _layoutRoot = new Grid
            {
                IsHitTestVisible = false,
                ClipToBounds = true
            };

            _progressTrack = new Border
            {
                CornerRadius = new CornerRadius(2),
                Background = new SolidColorBrush(Color.FromArgb(24, 0, 0, 0))
            };
            _progressTrack.IsHitTestVisible = false;

            _progressFill = new Border
            {
                CornerRadius = new CornerRadius(2),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Stretch,
                Width = 0,
                Background = _progressForeground ?? Brushes.Red
            };
            _progressFill.IsHitTestVisible = false;

            _baseTextBlock = CreateCenteredTextBlock(_progressForeground ?? Brushes.Red);
            _filledTextBlock = CreateCenteredTextBlock(Brushes.White);
            _filledTextBlock.Clip = _filledTextClip;

            _layoutRoot.Children.Add(_progressTrack);
            _layoutRoot.Children.Add(_progressFill);
            _layoutRoot.Children.Add(_baseTextBlock);
            _layoutRoot.Children.Add(_filledTextBlock);
            _overlay.Child = _layoutRoot;
        }

        private TextBlock CreateCenteredTextBlock(Brush foreground)
        {
            TextBlock textBlock = new TextBlock
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0),
                TextAlignment = TextAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            textBlock.Foreground = foreground;
            textBlock.FontSize = _button.FontSize;
            textBlock.FontWeight = _button.FontWeight;
            textBlock.FontFamily = _button.FontFamily;
            textBlock.IsHitTestVisible = false;
            return textBlock;
        }

        public void Show()
        {
            if (!_isHosted)
            {
                return;
            }

            if (!_overlayInserted)
            {
                _host.Children.Add(_overlay);
                _overlayInserted = true;
            }
        }

        public void Remove()
        {
            if (_overlayInserted)
            {
                _host.Children.Remove(_overlay);
                _overlayInserted = false;
            }
        }

        public void UpdateProgress(double progressValue, string? text)
        {
            _progressValue = Math.Max(0, Math.Min(99, progressValue));

            string resolvedText = text ?? string.Empty;
            _baseTextBlock.Text = resolvedText;
            _filledTextBlock.Text = resolvedText;
            UpdateProgressVisual();
        }

        private void Overlay_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateProgressVisual();
        }

        private void UpdateProgressVisual()
        {
            double availableWidth = Math.Max(0, _layoutRoot.ActualWidth);
            double availableHeight = Math.Max(0, _layoutRoot.ActualHeight);
            double filledWidth = availableWidth * (_progressValue / 100d);

            _progressFill.Width = filledWidth;
            _filledTextClip.Rect = new Rect(0, 0, filledWidth, availableHeight);
        }

        private bool TryCreateHost()
        {
            if (_button.Parent == _host)
            {
                return true;
            }

            CopyLayoutProperties(_button, _host);
            PrepareButtonForHost(_button);

            switch (_button.Parent)
            {
                case Panel panel:
                    int childIndex = panel.Children.IndexOf(_button);
                    if (childIndex < 0)
                    {
                        return false;
                    }

                    panel.Children.RemoveAt(childIndex);
                    _host.Children.Add(_button);
                    panel.Children.Insert(childIndex, _host);
                    return true;

                case Decorator decorator when ReferenceEquals(decorator.Child, _button):
                    decorator.Child = null;
                    _host.Children.Add(_button);
                    decorator.Child = _host;
                    return true;

                case ContentControl contentControl when ReferenceEquals(contentControl.Content, _button):
                    contentControl.Content = null;
                    _host.Children.Add(_button);
                    contentControl.Content = _host;
                    return true;

                default:
                    return false;
            }
        }

        private static void PrepareButtonForHost(Button button)
        {
            button.Margin = new Thickness(0);
            button.HorizontalAlignment = HorizontalAlignment.Stretch;
            button.VerticalAlignment = VerticalAlignment.Stretch;
            button.ClearValue(FrameworkElement.WidthProperty);
            button.ClearValue(FrameworkElement.HeightProperty);
        }

        private static void CopyLayoutProperties(FrameworkElement source, FrameworkElement target)
        {
            target.Margin = source.Margin;
            target.HorizontalAlignment = source.HorizontalAlignment;
            target.VerticalAlignment = source.VerticalAlignment;
            target.Width = source.Width;
            target.Height = source.Height;
            target.MinWidth = source.MinWidth;
            target.MinHeight = source.MinHeight;
            target.MaxWidth = source.MaxWidth;
            target.MaxHeight = source.MaxHeight;

            Grid.SetRow(target, Grid.GetRow(source));
            Grid.SetColumn(target, Grid.GetColumn(source));
            Grid.SetRowSpan(target, Grid.GetRowSpan(source));
            Grid.SetColumnSpan(target, Grid.GetColumnSpan(source));
            DockPanel.SetDock(target, DockPanel.GetDock(source));
            Panel.SetZIndex(target, Panel.GetZIndex(source));
        }

        public void Dispose()
        {
            Remove();
            _overlay.SizeChanged -= Overlay_SizeChanged;
            GC.SuppressFinalize(this);
        }
    }
}