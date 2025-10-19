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
            MenuItemMetadatas.Add(new MenuItemMetadata() { GuidId = "SaveAsImage", Order = 300, Header = Properties.Resources.SaveAsImage, Command = ApplicationCommands.SaveAs ,Icon = MenuItemIcon.TryFindResource("DISave") });

            MenuItemMetadatas.Add(new MenuItemMetadata() { GuidId = "ClearImage", Order = 11, Header = Properties.Resources.Clear, Command = ApplicationCommands.Close, Icon = MenuItemIcon.TryFindResource("DIDelete") });
            MenuItemMetadatas.Add(new MenuItemMetadata() { GuidId = "Print", Order = 300, Header = Properties.Resources.Print, Command = ApplicationCommands.Print, Icon = MenuItemIcon.TryFindResource("DIPrint"), InputGestureText = "Ctrl+P" });
           
            return MenuItemMetadatas;
        }
    }


}
