#pragma warning disable CA1859
using ColorVision.Common.MVVM;
using ColorVision.Scheduler.Properties;
using ColorVision.UI;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace ColorVision.Scheduler
{
    public class SchedulerStatusBarProvider : ViewModelBase, IStatusBarProviderUpdatable
    {
        private static readonly DrawingImage WaitingBadge = CreateBadge(Color.FromRgb(0x37, 0x71, 0xAA), new EllipseGeometry(new Point(4, 4), 1.2, 1.2));
        private static readonly DrawingImage RunningBadge = CreateBadge(Color.FromRgb(0x2D, 0x79, 0x59), Geometry.Parse("M 3,2 L 6,4 L 3,6 Z"));
        private static readonly DrawingImage PausedBadge = CreateBadge(Color.FromRgb(0x9F, 0x5C, 0x16), Geometry.Parse("M 2.5,2 H 3.5 V 6 H 2.5 Z M 4.5,2 H 5.5 V 6 H 4.5 Z"));

        public event EventHandler StatusBarItemsChanged;
        private readonly QuartzSchedulerManager _manager;
        private readonly HashSet<SchedulerInfo> _trackedTasks = new();

        public SchedulerStatusBarProvider() : this(QuartzSchedulerManager.GetInstance()) { }

        internal SchedulerStatusBarProvider(QuartzSchedulerManager manager)
        {
            _manager = manager;
            manager.TaskInfos.CollectionChanged += OnTaskInfosChanged;
            manager.ActiveExecutionsChanged += RefreshStatusText;
            RefreshTaskSubscriptions();
            RefreshStatusText();
        }

        public string StatusText { get; private set; } = string.Empty;
        public ImageSource? StatusBadge { get; private set; }

        private void OnTaskInfosChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            RefreshTaskSubscriptions();
            RefreshStatusText();
            StatusBarItemsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void RefreshTaskSubscriptions()
        {
            var currentTasks = _manager.TaskInfos.ToHashSet();
            foreach (var task in _trackedTasks.Where(task => !currentTasks.Contains(task)).ToArray())
            {
                task.PropertyChanged -= OnTaskPropertyChanged;
                _trackedTasks.Remove(task);
            }

            foreach (var task in currentTasks.Where(task => _trackedTasks.Add(task)))
                task.PropertyChanged += OnTaskPropertyChanged;
        }

        private void OnTaskPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(SchedulerInfo.Status) or nameof(SchedulerInfo.NextScheduledFireTimeUtc))
                RefreshStatusText();
        }

        private void RefreshStatusText()
        {
            if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
            {
                _ = dispatcher.InvokeAsync(RefreshStatusText);
                return;
            }

            var tasks = _manager.TaskInfos;
            var activeJobs = _manager.Listener?.GetActiveJobKeys();
            string text = string.Empty;
            ImageSource? badge = null;
            if (activeJobs is { Length: > 0 })
            {
                bool allPaused = activeJobs.All(job => tasks.Any(task => task.Status == SchedulerStatus.Paused
                    && task.JobName == job.Name && task.GroupName == job.Group));
                text = allPaused ? Resources.Sched_StatusBarPausedRunning : Resources.Sched_StatusBarRunning;
                badge = allPaused ? PausedBadge : RunningBadge;
            }
            else if (tasks.Where(task => task.Status == SchedulerStatus.Ready)
                .Min(task => task.NextScheduledFireTimeUtc) is { } nextFireTime)
            {
                DateTime local = nextFireTime.LocalDateTime;
                string time = local.ToString(local.Date == DateTime.Today ? "HH:mm:ss" : "yyyy/MM/dd HH:mm:ss", CultureInfo.CurrentCulture);
                text = string.Format(CultureInfo.CurrentCulture, Resources.Sched_StatusBarNextRun, time);
                badge = WaitingBadge;
            }

            if (StatusBadge != badge)
            {
                StatusBadge = badge;
                OnPropertyChanged(nameof(StatusBadge));
            }

            if (StatusText != text)
            {
                StatusText = text;
                OnPropertyChanged(nameof(StatusText));
            }
        }

        public IEnumerable<StatusBarMeta> GetStatusBarIconMetadata()
        {
            int taskCount = _manager.TaskInfos.Count;

            RelayCommand openCommand = new RelayCommand(a =>
                new TaskViewerWindow(_manager, _manager.InitializationTask)
                {
                    Owner = Application.Current.GetActiveWindow(),
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                }.ShowDialog());

            return new List<StatusBarMeta>
            {
                new StatusBarMeta
                {
                    Id = "Scheduler",
                    Name = Resources.TaskViewerWindow,
                    Description = taskCount > 0
                        ? $"Scheduler: {taskCount} task(s)"
                        : "Scheduler: No tasks",
                    Type = StatusBarType.Icon,
                    Alignment = StatusBarAlignment.Right,
                    Order = 0,
                    IconContent = CreateStatusContent(taskCount > 0),
                    Command = openCommand,
                }
            };
        }

        private StackPanel CreateStatusContent(bool hasTasks)
        {
            var text = new TextBlock { FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
            text.SetResourceReference(TextBlock.ForegroundProperty, "GlobalTextBrush");
            text.SetBinding(TextBlock.TextProperty, new Binding(nameof(StatusText)) { Source = this, Mode = BindingMode.OneWay });

            var icon = new Grid { Width = 18, Height = 16, Margin = new Thickness(5, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            icon.Children.Add(CreateSchedulerIcon(hasTasks));

            var badgeImage = new Image { Width = 9, Height = 9 };
            var badgeBinding = new Binding(nameof(StatusBadge)) { Source = this, Mode = BindingMode.OneWay };
            badgeImage.SetBinding(Image.SourceProperty, badgeBinding);
            var badge = new Border
            {
                Width = 11,
                Height = 11,
                CornerRadius = new CornerRadius(5.5),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Child = badgeImage,
            };
            badge.SetResourceReference(Border.BackgroundProperty, "GlobalBackground");
            var badgeStyle = new Style(typeof(Border));
            var hideBadge = new DataTrigger { Binding = badgeBinding, Value = null };
            hideBadge.Setters.Add(new Setter(UIElement.VisibilityProperty, Visibility.Collapsed));
            badgeStyle.Triggers.Add(hideBadge);
            badge.Style = badgeStyle;
            icon.Children.Add(badge);

            var separator = new Border { Width = 1, Height = 14, Opacity = 0.65, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            separator.SetResourceReference(Border.BackgroundProperty, "BorderBrush");

            var content = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, UseLayoutRounding = true };
            content.Children.Add(text);
            content.Children.Add(icon);
            content.Children.Add(separator);
            return content;
        }

        private static DrawingImage CreateBadge(Color color, Geometry symbol)
        {
            var drawing = new DrawingGroup();
            drawing.Children.Add(new GeometryDrawing(new SolidColorBrush(color), null, new EllipseGeometry(new Point(4, 4), 4, 4)));
            drawing.Children.Add(new GeometryDrawing(Brushes.White, null, symbol));
            var image = new DrawingImage(drawing);
            image.Freeze();
            return image;
        }

        private static UIElement CreateSchedulerIcon(bool hasActiveTasks)
        {
            // 时钟图标
            var ellipseGeometry = new EllipseGeometry(new Point(8, 8), 6, 6);

            // 时针
            var handFigure = new PathFigure { StartPoint = new Point(8, 8), IsClosed = false };
            handFigure.Segments.Add(new LineSegment(new Point(8, 4), true));

            // 分针
            var minuteFigure = new PathFigure { StartPoint = new Point(8, 8), IsClosed = false };
            minuteFigure.Segments.Add(new LineSegment(new Point(11, 8), true));

            var handsGeometry = new PathGeometry(new[] { handFigure, minuteFigure });

            var combinedGeometry = new GeometryGroup();
            combinedGeometry.Children.Add(ellipseGeometry);
            combinedGeometry.Children.Add(handsGeometry);
            combinedGeometry.Freeze();

            var color = hasActiveTasks
                ? Color.FromRgb(0x4E, 0xC9, 0xB0)  // 青绿色 - 有任务
                : Color.FromRgb(0x80, 0x80, 0x80);  // 灰色 - 无任务
            var brush = new SolidColorBrush(color);
            brush.Freeze();

            var path = new System.Windows.Shapes.Path
            {
                Data = combinedGeometry,
                Stroke = brush,
                StrokeThickness = 1.2,
                Width = 14,
                Height = 14,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
            };

            return path;
        }
    }
}
