using ColorVision.ImageEditor.Realtime;
using ColorVision.Themes;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;

namespace ColorVision.ImageEditor.EditorTools.Realtime
{
    public sealed class RealtimeDiagnosticsWindow : Window
    {
        private readonly RealtimeImageViewService _realtime;
        private readonly DispatcherTimer _refreshTimer;

        public RealtimeDiagnosticsWindow(RealtimeImageViewService realtime)
        {
            _realtime = realtime ?? throw new ArgumentNullException(nameof(realtime));
            DataContext = _realtime.Stats;
            Title = "实时诊断";
            Width = 300;
            Height = 300;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Content = CreateContent();
            this.ApplyCaption();

            _refreshTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(500),
            };
            _refreshTimer.Tick += RefreshTimer_Tick;
            Loaded += RealtimeDiagnosticsWindow_Loaded;
            Closed += RealtimeDiagnosticsWindow_Closed;
        }

        private void RealtimeDiagnosticsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _refreshTimer.Start();
        }

        private void RealtimeDiagnosticsWindow_Closed(object? sender, EventArgs e)
        {
            _refreshTimer.Stop();
            _refreshTimer.Tick -= RefreshTimer_Tick;
        }

        private void RefreshTimer_Tick(object? sender, EventArgs e)
        {
            _realtime.Stats.Refresh(_realtime.IsFrozen);
        }

        private static FrameworkElement CreateContent()
        {
            Grid grid = new()
            {
                Margin = new Thickness(14),
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            AddRow(grid, 0, "冻结", nameof(RealtimeFrameStats.IsFrozen));
            AddRow(grid, 1, "采集 FPS", nameof(RealtimeFrameStats.SubmittedFps), "F1");
            AddRow(grid, 2, "显示 FPS", nameof(RealtimeFrameStats.DisplayedFps), "F1");
            AddRow(grid, 3, "UI 延迟", nameof(RealtimeFrameStats.LastUiLatencyMilliseconds), "F1", " ms");
            AddRow(grid, 4, "提交帧", nameof(RealtimeFrameStats.SubmittedFrames));
            AddRow(grid, 5, "接受帧", nameof(RealtimeFrameStats.AcceptedFrames));
            AddRow(grid, 6, "显示帧", nameof(RealtimeFrameStats.DisplayedFrames));
            AddRow(grid, 7, "丢弃帧", nameof(RealtimeFrameStats.DroppedFrames));
            AddRow(grid, 8, "冻结丢弃", nameof(RealtimeFrameStats.FrozenDroppedFrames));

            return grid;
        }

        private static void AddRow(Grid grid, int row, string label, string bindingPath, string? numericFormat = null, string suffix = "")
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            TextBlock labelBlock = new()
            {
                Text = label,
                Margin = new Thickness(0, 0, 16, 8),
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetRow(labelBlock, row);
            Grid.SetColumn(labelBlock, 0);
            grid.Children.Add(labelBlock);

            TextBlock valueBlock = new()
            {
                Margin = new Thickness(0, 0, 0, 8),
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.SemiBold,
            };

            Binding binding = new(bindingPath);
            if (numericFormat != null)
            {
                binding.StringFormat = "{0:" + numericFormat + "}" + suffix;
            }
            else if (!string.IsNullOrEmpty(suffix))
            {
                binding.StringFormat = "{0}" + suffix;
            }

            valueBlock.SetBinding(TextBlock.TextProperty, binding);
            Grid.SetRow(valueBlock, row);
            Grid.SetColumn(valueBlock, 1);
            grid.Children.Add(valueBlock);
        }
    }
}
