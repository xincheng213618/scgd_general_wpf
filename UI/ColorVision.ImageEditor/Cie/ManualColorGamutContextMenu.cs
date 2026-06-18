using ColorVision.Common.MVVM;
using ColorVision.UI;
using ColorVision.UI.Menus;
using System.Collections.Generic;
using System.Windows;

namespace ColorVision.ImageEditor.Cie
{
    public sealed record class ManualColorGamutContextMenu(EditorContext Context) : IIEditorToolContextMenu
    {
        public List<MenuItemMetadata> GetContextMenuItems()
        {
            RelayCommand openCommand = new(_ => OpenWindow());

            return new List<MenuItemMetadata>
            {
                new()
                {
                    GuidId = "ManualColorGamut",
                    Order = 311,
                    Header = "手动色域计算",
                    Command = openCommand,
                    Icon = CieDiagramEditorTool.CreateIcon()
                }
            };
        }

        private static void OpenWindow()
        {
            ManualColorGamutWindow window = new()
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            window.Show();
        }
    }
}
