using ColorVision.UI;
using ColorVision.UI.Menus;
using System.Collections.Generic;
using System.Windows.Input;

namespace ColorVision.ImageEditor.EditorTools.AppCommand
{
    public record class ZoomEditorToolContextMenu(EditorContext context) : IIEditorToolContextMenu
    {
        public List<MenuItemMetadata> GetContextMenuItems()
        {
            var MenuItemMetadatas = new  List<MenuItemMetadata>();
            MenuItemMetadatas.Add(new MenuItemMetadata() { GuidId = "OpenImage", Order = 10, Header = Properties.Resources.Open, Command = ApplicationCommands.Open , Icon = MenuItemIcon.TryFindResource("DIOpen") });
            MenuItemMetadatas.Add(new MenuItemMetadata() { GuidId = "SaveAsImage", Order = 300, Header = Properties.Resources.SaveAsImage, Command = ApplicationCommands.SaveAs });
            return MenuItemMetadatas;
        }
    }


}
