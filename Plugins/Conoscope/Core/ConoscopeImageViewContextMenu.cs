using ColorVision.Common.MVVM;
using ColorVision.ImageEditor;
using ColorVision.UI.Menus;
using System.Collections.Generic;
using System.Windows;

namespace Conoscope.Core
{
    public record class ConoscopeImageViewContextMenu(EditorContext Context) : IIEditorToolContextMenu
    {
        public List<MenuItemMetadata> GetContextMenuItems()
        {
            return new List<MenuItemMetadata>
            {
                new MenuItemMetadata
                {
                    GuidId = "OpenByConoscope",
                    Order = 20,
                    Header = "通过 Conoscope 打开",
                    Icon = MenuItemIcon.TryFindResource("DIOpen"),
                    Command = new RelayCommand(_ => ConoscopeModuleService.OpenFromImageView(Context)),
                    Visibility = ConoscopeModuleService.CanOpenFromImageView(Context) ? Visibility.Visible : Visibility.Collapsed
                }
            };
        }
    }
}
