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
            IViewManager viewManager = ViewManagerProvider.Current;

            MenuItemMetadata menuItemMetadata1 = new MenuItemMetadata();
            menuItemMetadata1.Command = new RelayCommand(a => viewManager.SetViewGrid(1));
            var Image = new System.Windows.Controls.Image();
            Image.SetResourceReference(Image.SourceProperty,"DrawingImageSingleWindow");
            menuItemMetadata1.Icon = Image;
            menuItemMetadata1.Order = 0;

            MenuItemMetadata menuItemMetadata20 = new MenuItemMetadata();
            menuItemMetadata20.Command = new RelayCommand(a => SetViewGridTwoFallback(viewManager));
            var Image20 = new System.Windows.Controls.Image();
            Image20.SetResourceReference(Image.SourceProperty, "DrawingImageTwoWindow1");
            menuItemMetadata20.Icon = Image20;
            menuItemMetadata20.Order = 0;

            MenuItemMetadata menuItemMetadata21 = new MenuItemMetadata();
            menuItemMetadata21.Command = new RelayCommand(a => viewManager.SetViewGrid(2));
            var Image21 = new System.Windows.Controls.Image();
            Image21.SetResourceReference(Image.SourceProperty, "DrawingImageTwoWindow0");
            menuItemMetadata21.Icon = Image21;
            menuItemMetadata21.Order = 0;

            MenuItemMetadata menuItemMetadata30= new MenuItemMetadata();
            menuItemMetadata30.Command = new RelayCommand(a => SetViewGridThreeFallback(viewManager, true));
            var Image30 = new System.Windows.Controls.Image();
            Image30.SetResourceReference(Image.SourceProperty, "DrawingImageLeft1Right2");
            menuItemMetadata30.Icon = Image30;
            menuItemMetadata30.Order = 0;

            MenuItemMetadata menuItemMetadata31 = new MenuItemMetadata();
            menuItemMetadata31.Command = new RelayCommand(a => SetViewGridThreeFallback(viewManager, false));
            var Image31 = new System.Windows.Controls.Image();
            Image31.SetResourceReference(Image.SourceProperty, "DrawingImageLeft2Right1");
            menuItemMetadata31.Icon = Image31;
            menuItemMetadata31.Order = 0;

            MenuItemMetadata menuItemMetadata4 = new MenuItemMetadata();
            menuItemMetadata4.Command = new RelayCommand(a => viewManager.SetViewGrid(4));
            var Image4 = new System.Windows.Controls.Image();
            Image4.SetResourceReference(Image.SourceProperty, "DrawingImageFourWindow");
            menuItemMetadata4.Icon = Image4;
            menuItemMetadata4.Order = 0;

            return new MenuItemMetadata[] { menuItemMetadata1, menuItemMetadata20, menuItemMetadata21 , menuItemMetadata30 , menuItemMetadata31, menuItemMetadata4 };
        }

        /// <summary>
        /// Grid 专用双窗格布局，非 ViewGridManager 时回退到标准 SetViewGrid(2)
        /// </summary>
        private static void SetViewGridTwoFallback(IViewManager viewManager)
        {
            if (viewManager is ViewGridManager gridMgr)
                gridMgr.SetViewGridTwo();
            else
                viewManager.SetViewGrid(2);
        }

        /// <summary>
        /// Grid 专用三窗格布局，非 ViewGridManager 时回退到标准 SetViewGrid(3)
        /// </summary>
        private static void SetViewGridThreeFallback(IViewManager viewManager, bool left)
        {
            if (viewManager is ViewGridManager gridMgr)
                gridMgr.SetViewGridThree(left);
            else
                viewManager.SetViewGrid(3);
        }
    }
}
