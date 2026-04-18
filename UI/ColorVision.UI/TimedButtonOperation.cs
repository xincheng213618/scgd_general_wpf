using ColorVision.Adorners;
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
        public double LastElapsedMs { get; set; }
        public double AverageElapsedMs { get; set; }
        public double BestElapsedMs { get; set; }
        public double WorstElapsedMs { get; set; }
        public DateTime LastCompletedAt { get; set; }

        public void Record(double elapsedMilliseconds)
        {
            elapsedMilliseconds = Math.Max(0, elapsedMilliseconds);
            SuccessCount++;
            LastElapsedMs = elapsedMilliseconds;
            LastCompletedAt = DateTime.Now;

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
        private TimedButtonProgressAdorner? _adorner;
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

            _adorner ??= new TimedButtonProgressAdorner(_button, _options.ProgressForeground);
            _adorner.UpdateProgress(0, _runningText);
            _adorner.Show();

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
            _adorner?.Hide();

            if (_options.DisableButtonWhileRunning)
            {
                _button.IsEnabled = _buttonWasEnabled;
            }

            _isRunning = false;
            double elapsedMilliseconds = _stopwatch.Elapsed.TotalMilliseconds;

            if (success)
            {
                TimedButtonOperationStatsStore.Record(_options.OperationKey, elapsedMilliseconds, _options.PersistStatsImmediately);
                _options.OnSuccessfulCompletion?.Invoke(elapsedMilliseconds);
            }

            RefreshIdleState();
        }

        private double ResolveExpectedDuration(double? expectedDurationMs)
        {
            if (expectedDurationMs.HasValue && expectedDurationMs.Value > 0)
            {
                return Math.Max(_options.MinimumExpectedDurationMs, expectedDurationMs.Value);
            }

            double configuredDuration = _options.ExpectedDurationProvider?.Invoke() ?? 0;
            if (configuredDuration > 0)
            {
                return Math.Max(_options.MinimumExpectedDurationMs, configuredDuration);
            }

            TimedButtonOperationStats? stats = CurrentStats;
            if (stats != null)
            {
                double historicalDuration = stats.SuccessCount > 1 ? stats.AverageElapsedMs : stats.LastElapsedMs;
                if (historicalDuration > 0)
                {
                    return Math.Max(_options.MinimumExpectedDurationMs, historicalDuration);
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

            _adorner?.UpdateProgress(progress, _runningText);
        }

        public void Dispose()
        {
            _timer.Tick -= Timer_Tick;
            _timer.Stop();
            _adorner?.Detach();
            _adorner = null;
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
            if (stats == null || stats.SuccessCount == 0)
            {
                return label;
            }

            return $"{label} ({FormatDuration(stats.LastElapsedMs)})";
        }

        public static string BuildTooltip(string label, TimedButtonOperationStats? stats)
        {
            if (stats == null || stats.SuccessCount == 0)
            {
                return $"{label}\n首次执行，暂无历史耗时。";
            }

            return $"{label}\n上次: {FormatDuration(stats.LastElapsedMs)}\n平均: {FormatDuration(stats.AverageElapsedMs)}\n最快: {FormatDuration(stats.BestElapsedMs)}\n最慢: {FormatDuration(stats.WorstElapsedMs)}\n成功次数: {stats.SuccessCount}\n{BuildTrendText(stats)}";
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
                return "当前只有第一次样本，后续可观察是否继续下降。";
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

    internal static class TimedButtonOperationStatsStore
    {
        public static TimedButtonOperationStats? Get(string operationKey)
        {
            if (string.IsNullOrWhiteSpace(operationKey))
            {
                return null;
            }

            TimedButtonOperationStatsConfig config = ConfigHandler.GetInstance().GetRequiredService<TimedButtonOperationStatsConfig>();
            config.Records ??= new Dictionary<string, TimedButtonOperationStats>();
            config.Records.TryGetValue(operationKey.Trim(), out TimedButtonOperationStats? stats);
            return stats;
        }

        public static TimedButtonOperationStats Record(string operationKey, double elapsedMilliseconds, bool persistImmediately)
        {
            TimedButtonOperationStatsConfig config = ConfigHandler.GetInstance().GetRequiredService<TimedButtonOperationStatsConfig>();
            config.Records ??= new Dictionary<string, TimedButtonOperationStats>();

            string normalizedKey = operationKey.Trim();
            if (!config.Records.TryGetValue(normalizedKey, out TimedButtonOperationStats? stats))
            {
                stats = new TimedButtonOperationStats();
                config.Records[normalizedKey] = stats;
            }

            stats.Record(elapsedMilliseconds);

            if (persistImmediately)
            {
                ConfigHandler.GetInstance().Save<TimedButtonOperationStatsConfig>();
            }

            return stats;
        }
    }

    internal sealed class TimedButtonProgressAdorner : UIElementAdornerBase
    {
        private readonly Border _overlay;
        private readonly ProgressBar _progressBar;
        private readonly TextBlock _textBlock;

        public TimedButtonProgressAdorner(Button adornedElement, Brush? progressForeground) : base(adornedElement)
        {
            IsHitTestVisible = false;
            AdornerVisual.IsHitTestVisible = false;

            _overlay = new Border
            {
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2),
                Opacity = 0.96,
                Visibility = Visibility.Collapsed
            };
            _overlay.SetResourceReference(Border.BackgroundProperty, "GlobalBackground");
            _overlay.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");

            Grid layoutRoot = new Grid();

            _progressBar = new ProgressBar
            {
                Minimum = 0,
                Maximum = 100,
                Background = Brushes.Transparent,
                Value = 0
            };
            _progressBar.SetResourceReference(Control.BorderBrushProperty, "BorderBrush");
            _progressBar.Foreground = progressForeground ?? Brushes.Red;

            _textBlock = new TextBlock
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 8, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            _textBlock.SetResourceReference(TextBlock.ForegroundProperty, "GlobalTextBrush");

            layoutRoot.Children.Add(_progressBar);
            layoutRoot.Children.Add(_textBlock);
            _overlay.Child = layoutRoot;
            AdornerVisual.Children.Add(_overlay);
        }

        public void Show()
        {
            _overlay.Visibility = Visibility.Visible;
        }

        public void Hide()
        {
            _overlay.Visibility = Visibility.Collapsed;
        }

        public void UpdateProgress(double progressValue, string? text)
        {
            _progressBar.Value = Math.Max(0, Math.Min(99, progressValue));
            _textBlock.Text = text ?? string.Empty;
        }
    }
}