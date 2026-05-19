using ColorVision.Common.MVVM;
using ColorVision.ImageEditor.Draw;
using ColorVision.UI.Menus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;

namespace ColorVision.ImageEditor.EditorTools
{


    public record class BitmapScalingEditorToolContextMenu(DrawEditorContext context) : IIEditorToolContextMenu
    {
        public List<MenuItemMetadata> GetContextMenuItems()
        {
            var MenuItemMetadatas = new List<MenuItemMetadata>();
            MenuItemMetadatas.Add(new MenuItemMetadata() { GuidId = "BitmapScalingMode", Order = 102, Header = Properties.Resources.BitmapScalingMode, Icon = MenuItemIcon.TryFindResource("DIOpen") });

            var currentMode = RenderOptions.GetBitmapScalingMode(context.DrawCanvas);

            foreach (var item in Enum.GetValues<BitmapScalingMode>().GroupBy(mode => (int)mode).Select(group => group.First()))
            {
                RelayCommand relayCommand = new RelayCommand( a=>
                {
                    RenderOptions.SetBitmapScalingMode(context.DrawCanvas, item);
                });

                MenuItemMetadatas.Add(new MenuItemMetadata() { OwnerGuid = "BitmapScalingMode", GuidId = item.ToString(), Order = (int)item, Header = item.ToString(),Command = relayCommand, IsChecked = currentMode == item });
            }

            return MenuItemMetadatas;
        }
    }


}
