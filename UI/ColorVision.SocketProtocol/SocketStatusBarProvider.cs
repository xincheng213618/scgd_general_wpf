using ColorVision.Common.MVVM;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.SocketProtocol
{
    public class SocketStatusBarProvider : IStatusBarProviderUpdatable
    {
        public event EventHandler StatusBarItemsChanged;

        public SocketStatusBarProvider()
        {
            SocketConfig.Instance.ServerEnabledChanged += (s, e) =>
                StatusBarItemsChanged?.Invoke(this, EventArgs.Empty);
            SocketManager.GetInstance().SocketConnectChanged += (s, e) =>
                StatusBarItemsChanged?.Invoke(this, EventArgs.Empty);
        }

        public IEnumerable<StatusBarMeta> GetStatusBarIconMetadata()
        {
            var config = SocketConfig.Instance;
            if (!config.IsServerEnabled)
                return Array.Empty<StatusBarMeta>();

            var manager = SocketManager.GetInstance();
            bool isConnected = manager.IsConnect;

            RelayCommand editCommand = new RelayCommand(a =>
                new SocketManagerWindow { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog());

            return new List<StatusBarMeta>
            {
                new StatusBarMeta
                {
                    Id = "SocketServer",
                    Name = "Socket Server",
                    Description = isConnected ? "Socket Server Connected" : "Socket Server Disconnected",
                    Type = StatusBarType.Icon,
                    Alignment = StatusBarAlignment.Right,
                    Order = 998,
                    IconContent = CreateSocketIcon(isConnected),
                    Source = manager,
                    Command = editCommand,
                }
            };
        }

        private static UIElement CreateSocketIcon(bool isConnected)
        {
            var pathFigure1 = new PathFigure { StartPoint = new Point(2, 8), IsClosed = false };
            pathFigure1.Segments.Add(new LineSegment(new Point(6, 8), true));
            pathFigure1.Segments.Add(new LineSegment(new Point(6, 4), true));
            pathFigure1.Segments.Add(new LineSegment(new Point(10, 4), true));
            pathFigure1.Segments.Add(new LineSegment(new Point(10, 8), true));
            pathFigure1.Segments.Add(new LineSegment(new Point(14, 8), true));

            var pathFigure2 = new PathFigure { StartPoint = new Point(2, 12), IsClosed = false };
            pathFigure2.Segments.Add(new LineSegment(new Point(6, 12), true));
            pathFigure2.Segments.Add(new LineSegment(new Point(6, 12), true));
            pathFigure2.Segments.Add(new LineSegment(new Point(10, 12), true));
            pathFigure2.Segments.Add(new LineSegment(new Point(10, 12), true));
            pathFigure2.Segments.Add(new LineSegment(new Point(14, 12), true));

            var geometry = new PathGeometry(new[] { pathFigure1, pathFigure2 });
            geometry.Freeze();

            var color = isConnected
                ? Color.FromRgb(0x4E, 0xC9, 0xB0)  // 青绿色 - 已连接
                : Color.FromRgb(0xF4, 0x43, 0x36);  // 红色 - 未连接
            var brush = new SolidColorBrush(color);
            brush.Freeze();

            var path = new System.Windows.Shapes.Path
            {
                Data = geometry,
                Stroke = brush,
                StrokeThickness = 1.5,
                Width = 14,
                Height = 14,
                Stretch = Stretch.Uniform,
                VerticalAlignment = VerticalAlignment.Center,
            };

            return path;
        }
    }
}
