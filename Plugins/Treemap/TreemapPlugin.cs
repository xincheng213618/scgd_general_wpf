using ColorVision.Common.MVVM;
using ColorVision.UI.Menus;
using System.Collections.Generic;
using System.Windows;

namespace ColorVision.Treemap
{
    public class TreemapPlugin : IMenuItemProvider
    {
        public IEnumerable<MenuItemMetadata> GetMenuItems()
        {
            return new List<MenuItemMetadata>
            {
                new MenuItemMetadata
                {
                    OwnerGuid = "Tool",
                    GuidId = "TreemapViewer",
                    Header = "树图可视化",
                    Order = 600,
                    Command = new RelayCommand(_ =>
                    {
                        var win = new TreemapDemoWindow
                        {
                            Owner = Application.Current.GetActiveWindow()
                        };
                        win.Show();
                    })
                }
            };
        }
    }
}
