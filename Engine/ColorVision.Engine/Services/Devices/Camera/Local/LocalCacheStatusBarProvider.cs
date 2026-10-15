using ColorVision.Common.MVVM;
using ColorVision.UI;
using ColorVision.UI.Menus;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ColorVision.Engine.Services.Devices.Camera.Local
{
    public sealed class LocalCacheStatusBarProvider : IStatusBarProvider
    {
        public IEnumerable<StatusBarMeta> GetStatusBarIconMetadata()
        {
            return new[]
            {
                new StatusBarMeta
                {
                    Id = "LocalCacheManagement",
                    Name = EngineLocalization.Get("本地缓存管理"),
                    Description = EngineLocalization.Get("打开本地缓存管理，查看或释放校正与图像文件缓存。"),
                    Type = StatusBarType.Icon,
                    Alignment = StatusBarAlignment.Right,
                    Order = 996,
                    TargetName = MenuItemConstants.MainWindowTarget,
                    ActionType = StatusBarActionType.Command,
                    Command = new RelayCommand(_ => LocalCalibrationCacheManagerWindow.OpenWindow()),
                    IconContent = CreateCacheIcon(),
                },
            };
        }

        private static Path CreateCacheIcon()
        {
            Geometry geometry = Geometry.Parse("M3,3 L13,3 13,13 3,13 Z M0,5 L3,5 M0,11 L3,11 M13,5 L16,5 M13,11 L16,11 M5,0 L5,3 M11,0 L11,3 M5,13 L5,16 M11,13 L11,16 M6,6 L10,6 M6,10 L10,10");
            geometry.Freeze();
            SolidColorBrush brush = new(Color.FromRgb(0x4E, 0xC9, 0xB0));
            brush.Freeze();
            return new Path
            {
                Data = geometry,
                Stroke = brush,
                StrokeThickness = 1.2,
                Width = 14,
                Height = 14,
                Stretch = Stretch.Uniform,
                VerticalAlignment = VerticalAlignment.Center,
            };
        }
    }
}
