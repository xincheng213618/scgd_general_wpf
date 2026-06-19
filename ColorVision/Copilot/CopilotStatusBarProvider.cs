#pragma warning disable CA1859
using ColorVision.Common.MVVM;
using ColorVision.UI;
using ColorVision.UI.Menus;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.Copilot
{
    public class CopilotStatusBarProvider : IStatusBarProvider
    {
        public IEnumerable<StatusBarMeta> GetStatusBarIconMetadata()
        {
            return new List<StatusBarMeta>
            {
                new StatusBarMeta
                {
                    Id = "CopilotAgent",
                    Name = "ColorVision Copilot",
                    Description = "Open AI chat panel",
                    Type = StatusBarType.Icon,
                    Alignment = StatusBarAlignment.Right,
                    Order = 9999, // Rightmost
                    ActionType = StatusBarActionType.Command,
                    TargetName = MenuItemConstants.MainWindowTarget,
                    Command = new RelayCommand(_ => CopilotPanelService.GetInstance().ShowPanel()),
                    IconContent = CreateCopilotIcon(),
                }
            };
        }

        private static UIElement CreateCopilotIcon()
        {
            // Simplified Copilot sparkle icon.
            var pathFigure = new PathFigure { StartPoint = new Point(8, 0), IsClosed = true };
            pathFigure.Segments.Add(new LineSegment(new Point(10, 6), true));
            pathFigure.Segments.Add(new LineSegment(new Point(16, 8), true));
            pathFigure.Segments.Add(new LineSegment(new Point(10, 10), true));
            pathFigure.Segments.Add(new LineSegment(new Point(8, 16), true));
            pathFigure.Segments.Add(new LineSegment(new Point(6, 10), true));
            pathFigure.Segments.Add(new LineSegment(new Point(0, 8), true));
            pathFigure.Segments.Add(new LineSegment(new Point(6, 6), true));

            var geometry = new PathGeometry(new[] { pathFigure });
            geometry.Freeze();

            var path = new System.Windows.Shapes.Path
            {
                Data = geometry,
                Fill = new SolidColorBrush(Color.FromRgb(0x96, 0x7A, 0xDB)),
                Width = 14,
                Height = 14,
                Stretch = Stretch.Uniform,
                VerticalAlignment = VerticalAlignment.Center,
            };

            return path;
        }
    }
}
