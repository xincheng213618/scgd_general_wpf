using ColorVision.Common.MVVM;
using ColorVision.UI;
using ColorVision.UI.Menus;
using System.Collections.Generic;
using System.Windows;

namespace YoloObjectDetection;

public sealed class YoloObjectDetectionMenuProvider : IMenuItemProvider
{
    public IEnumerable<MenuItemMetadata> GetMenuItems()
    {
        return
        [
            new MenuItemMetadata
            {
                OwnerGuid = MenuItemConstants.Tool,
                GuidId = "YoloObjectDetection",
                Header = "YOLO 工业检测",
                Order = 560,
                Command = new RelayCommand(_ =>
                {
                    var window = new MainWindow
                    {
                        Owner = Application.Current.GetActiveWindow(),
                        WindowStartupLocation = WindowStartupLocation.CenterOwner
                    };
                    window.Show();
                })
            }
        ];
    }
}