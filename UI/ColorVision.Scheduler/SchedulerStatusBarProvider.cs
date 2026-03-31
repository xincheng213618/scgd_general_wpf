using ColorVision.Common.MVVM;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.Scheduler
{
    public class SchedulerStatusBarProvider : IStatusBarProviderUpdatable
    {
        public event EventHandler StatusBarItemsChanged;

        public SchedulerStatusBarProvider()
        {
            var manager = QuartzSchedulerManager.GetInstance();
            manager.TaskInfos.CollectionChanged += OnTaskInfosChanged;
        }

        private void OnTaskInfosChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            StatusBarItemsChanged?.Invoke(this, EventArgs.Empty);
        }

        public IEnumerable<StatusBarMeta> GetStatusBarIconMetadata()
        {
            var manager = QuartzSchedulerManager.GetInstance();
            int taskCount = manager.TaskInfos.Count;

            RelayCommand openCommand = new RelayCommand(a =>
                new TaskViewerWindow { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog());

            return new List<StatusBarMeta>
            {
                new StatusBarMeta
                {
                    Id = "Scheduler",
                    Name = Properties.Resources.TaskViewerWindow,
                    Description = taskCount > 0
                        ? $"Scheduler: {taskCount} task(s)"
                        : "Scheduler: No tasks",
                    Type = StatusBarType.Icon,
                    Alignment = StatusBarAlignment.Right,
                    Order = 997,
                    IconContent = CreateSchedulerIcon(taskCount > 0),
                    Command = openCommand,
                }
            };
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
                VerticalAlignment = VerticalAlignment.Center,
            };

            return path;
        }
    }
}
