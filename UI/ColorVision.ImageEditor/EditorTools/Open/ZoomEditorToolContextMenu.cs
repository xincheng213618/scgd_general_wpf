using ColorVision.UI;
using ColorVision.UI.Menus;
using System.Collections.Generic;
using System.Windows.Input;

namespace ColorVision.ImageEditor.EditorTools.Open
{
    public record class ZoomEditorToolContextMenu(EditorContext context) : IIEditorToolContextMenu
    {
        public List<MenuItemMetadata> GetContextMenuItems()
        {
            var MenuItemMetadatas = new  List<MenuItemMetadata>();
            MenuItemMetadatas.Add(new MenuItemMetadata() { GuidId = "OpenImage", Order = 10, Header = Properties.Resources.Open, Command = ApplicationCommands.Open , Icon = MenuItemIcon.TryFindResource("DIOpen") });
            return MenuItemMetadatas;
        }
    }


}
