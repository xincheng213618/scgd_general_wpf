using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.UI
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
                    Name = "GitHub Copilot",
                    Description = "GitHub Copilot Agent",
                    Type = StatusBarType.Icon,
                    Alignment = StatusBarAlignment.Right,
                    Order = 9999, // 最右侧
                    ActionType = StatusBarActionType.Command,
                    IconContent = CreateCopilotIcon(),
                }
            };
        }

        private static UIElement CreateCopilotIcon()
        {
            // GitHub Copilot 星形图标（简化版 sparkle）
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
                Fill = new SolidColorBrush(Color.FromRgb(0x96, 0x7A, 0xDB)), // Copilot 紫色
                Width = 14,
                Height = 14,
                Stretch = Stretch.Uniform,
                VerticalAlignment = VerticalAlignment.Center,
            };

            return path;
        }
    }
}
