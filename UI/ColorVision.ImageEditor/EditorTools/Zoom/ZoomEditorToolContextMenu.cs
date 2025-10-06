using ColorVision.UI.Menus;
using System.Collections.Generic;

namespace ColorVision.ImageEditor.EditorTools.Zoom
{
    public record class ZoomEditorToolContextMenu(EditorContext context) : IIEditorToolContextMenu
    {
        public List<MenuItemMetadata> GetContextMenuItems()
        {
            var MenuItemMetadatas = new  List<MenuItemMetadata>();
            MenuItemMetadatas.Add(new MenuItemMetadata() { GuidId = "Zoom", Order = 100, Header = Properties.Resources.Zoom, Icon = MenuItemIcon.TryFindResource("DIZoom") });
            MenuItemMetadatas.Add(new MenuItemMetadata() { OwnerGuid = "Zoom", GuidId = "ZoomIn", Order = 1, Header = Properties.Resources.ZoomIn, Command = new ZoomInEditorTool(context).Command });
            MenuItemMetadatas.Add(new MenuItemMetadata() { OwnerGuid = "Zoom", GuidId = "ZoomOut", Order = 2, Header = Properties.Resources.ZoomOut, Command = new ZoomOutEditorTool(context).Command });
            MenuItemMetadatas.Add(new MenuItemMetadata() { OwnerGuid = "Zoom", GuidId = "ZoomNone", Order = 3, Header = Properties.Resources.ZoomNone, Command = new ZoomNoneEditorTool(context).Command });
            MenuItemMetadatas.Add(new MenuItemMetadata() { OwnerGuid = "Zoom", GuidId = "ZoomUniform", Order = 4, Header = Properties.Resources.ZoomUniform, Command = new ZoomUniformEditorTool(context).Command });
            MenuItemMetadatas.Add(new MenuItemMetadata() { OwnerGuid = "Zoom", GuidId = "ZoomUniformToFill", Order = 5, Header = "ZoomUniformToFill", Command = new ZoomUniformToFillEditorTool(context).Command });
            return MenuItemMetadatas;
        }
    }


}
