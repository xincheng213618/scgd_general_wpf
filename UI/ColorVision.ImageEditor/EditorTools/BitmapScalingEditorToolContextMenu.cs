using ColorVision.Common.MVVM;
using ColorVision.ImageEditor.Settings;
using ColorVision.UI.Menus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;

namespace ColorVision.ImageEditor.EditorTools
{


    public record class BitmapScalingEditorToolContextMenu(EditorContext context) : IIEditorToolContextMenu
    {
        public List<MenuItemMetadata> GetContextMenuItems()
        {
            var MenuItemMetadatas = new List<MenuItemMetadata>();
            MenuItemMetadatas.Add(new MenuItemMetadata() { GuidId = "BitmapScalingMode", Order = 102, Header = Properties.Resources.BitmapScalingMode, Icon = MenuItemIcon.TryFindResource("DIOpen") });

            var ime = RenderOptions.GetBitmapScalingMode(context.DrawCanvas);
            var defaultMode = DefaultBitmapScalingConfig.Current.DefaultBitmapScalingMode;

            RelayCommand applyDefaultCommand = new RelayCommand(a =>
            {
                context.ImageView?.ApplyDefaultBitmapScalingMode();
            });

            MenuItemMetadatas.Add(new MenuItemMetadata()
            {
                OwnerGuid = "BitmapScalingMode",
                GuidId = "BitmapScalingModeDefault",
                Order = -1,
                Header = $"恢复默认 ({defaultMode})",
                Command = applyDefaultCommand,
                IsChecked = ime == defaultMode
            });

            foreach (var item in Enum.GetValues(typeof(BitmapScalingMode)).Cast<BitmapScalingMode>().GroupBy(mode => (int)mode).Select(group => group.First()))
            {
                RelayCommand relayCommand = new RelayCommand( a=>
                {
                    context.ImageView?.ApplyBitmapScalingMode(item);
                });

                MenuItemMetadatas.Add(new MenuItemMetadata() { OwnerGuid = "BitmapScalingMode", GuidId = item.ToString(), Order = (int)item, Header = item.ToString(),Command = relayCommand, IsChecked = ime == item });
            }

            return MenuItemMetadatas;
        }
    }


}
