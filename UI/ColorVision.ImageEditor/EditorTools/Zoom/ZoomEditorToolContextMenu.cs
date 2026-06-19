using ColorVision.UI.Menus;
using ColorVision.ImageEditor.Draw;
using System.Collections.Generic;

namespace ColorVision.ImageEditor.EditorTools.Zoom
{
    public record class ZoomEditorToolContextMenu(DrawEditorContext context) : IIEditorToolContextMenu
    {
        public List<MenuItemMetadata> GetContextMenuItems()
        {
            var MenuItemMetadatas = new  List<MenuItemMetadata>();
            MenuItemMetadatas.Add(new MenuItemMetadata() { GuidId = "Zoom", Order = 100, Header = Properties.Resources.Zoom, Icon = MenuItemIcon.TryFindResource("DIZoom") });
            MenuItemMetadatas.Add(new MenuItemMetadata() { OwnerGuid = "Zoom", GuidId = "ZoomIn", Order = 1, Header = Properties.Resources.ZoomIn, Command = new ZoomInEditorTool(context).Command });
            MenuItemMetadatas.Add(new MenuItemMetadata() { OwnerGuid = "Zoom", GuidId = "ZoomOut", Order = 2, Header = Properties.Resources.ZoomOut, Command = new ZoomOutEditorTool(context).Command });
            MenuItemMetadatas.Add(new MenuItemMetadata() { OwnerGuid = "Zoom", GuidId = "ZoomUniform", Order = 3, Header = Properties.Resources.ZoomUniform, Command = new ZoomUniformEditorTool(context).Command });
            return MenuItemMetadatas;
        }
    }


}
