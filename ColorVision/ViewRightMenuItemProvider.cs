using ColorVision.Common.MVVM;
using ColorVision.UI.Menus;
using ColorVision.UI.Views;
using System.Collections.Generic;
using System.Windows.Controls;

namespace ColorVision
{
    public class ViewRightMenuItemProvider 
    {
        public IEnumerable<MenuItemMetadata> GetMenuItems()
        {
            MenuItemMetadata menuItemMetadata1 = new MenuItemMetadata();
            menuItemMetadata1.Command = new RelayCommand(a => DockViewManager.GetInstance().ShowAllViews());
            var Image = new System.Windows.Controls.Image();
            Image.SetResourceReference(Image.SourceProperty,"DrawingImageSingleWindow");
            menuItemMetadata1.Icon = Image;
            menuItemMetadata1.Order = 0;

            return new MenuItemMetadata[] { menuItemMetadata1 };
        }
    }
}
