using ColorVision.UI.Menus;
using System.Collections.Generic;

namespace ColorVision.ImageEditor.EditorTools.WindowTools
{
    public record class WindowEditorToolContextMenu(EditorContext context) : IIEditorToolContextMenu
    {
        public List<MenuItemMetadata> GetContextMenuItems()
        {
            var items = new List<MenuItemMetadata>();

            items.Add(new MenuItemMetadata() {  GuidId = "Full", Order = 500, Header = Properties.Resources.FullScreen, Command = new FullScreenEditorTool(context).Command, Icon = MenuItemIcon.TryFindResource("DIMax") });
            return items;
        }
    }
}
