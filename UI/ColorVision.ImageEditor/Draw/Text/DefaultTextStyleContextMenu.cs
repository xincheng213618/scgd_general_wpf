using ColorVision.Common.MVVM;
using ColorVision.UI;
using ColorVision.UI.Menus;
using System.Collections.Generic;
using System.Windows;

namespace ColorVision.ImageEditor.Draw
{
    internal static class DefaultTextStyleEditor
    {
        public static void Open(Window? owner = null)
        {
            PropertyEditorWindow window = new(DefaultTextStyleConfig.Current, false)
            {
                Owner = owner ?? Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            window.Submited += (_, _) => DefaultTextStyleConfig.SaveCurrent();
            window.ShowDialog();
        }
    }

    public record class DefaultTextStyleContextMenu(EditorContext context) : IIEditorToolContextMenu
    {
        public List<MenuItemMetadata> GetContextMenuItems()
        {
            RelayCommand editDefaultTextStyleCommand = new(a => DefaultTextStyleEditor.Open());

            return new List<MenuItemMetadata>
            {
                new MenuItemMetadata
                {
                    GuidId = "TextSettings",
                    Order = 215,
                    Header = "文本"
                },
                new MenuItemMetadata
                {
                    OwnerGuid = "TextSettings",
                    GuidId = "DefaultTextStyle",
                    Order = 1,
                    Header = "默认文本样式",
                    Command = editDefaultTextStyleCommand
                }
            };
        }
    }
}