using ColorVision.Common.MVVM;
using ColorVision.UI;
using ColorVision.UI.Menus;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ColorVision.Settings.Maintenance;

public sealed class FileManagementStatusBarProvider : IStatusBarProvider
{
    public IEnumerable<StatusBarMeta> GetStatusBarIconMetadata()
    {
        var geometry = Geometry.Parse("M1.5,6 V3.75 Q1.5,2.5 2.75,2.5 H5.75 Q6.25,2.5 6.6,2.85 L8.25,4.5 H13.25 Q14.5,4.5 14.5,5.75 V12.25 Q14.5,13.5 13.25,13.5 H2.75 Q1.5,13.5 1.5,12.25 Z M1.5,6 H14.5");
        geometry.Freeze();
        var brush = new SolidColorBrush(Color.FromRgb(0x5A, 0x9B, 0xD5));
        brush.Freeze();
        var icon = new Path
        {
            Data = geometry,
            Stroke = brush,
            StrokeThickness = 1.2, Width = 14, Height = 14, Stretch = Stretch.Uniform,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            VerticalAlignment = VerticalAlignment.Center,
        };
        yield return new StatusBarMeta
        {
            Id = "FileManagement", Name = MaintenanceText.FileManagement,
            Description = MaintenanceText.FileManagementDescription,
            Type = StatusBarType.Icon, Alignment = StatusBarAlignment.Right, Order = 995,
            TargetName = MenuItemConstants.MainWindowTarget, ActionType = StatusBarActionType.Command,
            Command = new RelayCommand(_ => FileManagementWindow.OpenWindow()), IconContent = icon,
        };
    }
}
