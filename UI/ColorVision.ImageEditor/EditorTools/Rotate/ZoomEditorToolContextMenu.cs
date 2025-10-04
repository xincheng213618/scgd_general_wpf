using ColorVision.Common.MVVM;
using ColorVision.ImageEditor.EditorTools.Rotate;
using ColorVision.UI;
using ColorVision.UI.Menus;
using System.Collections.Generic;

namespace ColorVision.ImageEditor.EditorTools
{
    public record class RotateEditorToolContextMenu(EditorContext context) : IIEditorToolContextMenu
    {



        public List<MenuItemMetadata> GetContextMenuItems()
        {
            var MenuItemMetadatas = new  List<MenuItemMetadata>();

            ImageTransformOperations imageTransformOperations = new ImageTransformOperations(context.DrawCanvas);


            RelayCommand RotateLeftCommand  = new(o => imageTransformOperations.RotateLeft());
            RelayCommand RotateRightCommand = new(o => imageTransformOperations.RotateRight());
            RelayCommand FlipHorizontalCommand= new(o => imageTransformOperations.FlipHorizontal());
            RelayCommand FlipVerticalCommand  = new(o => imageTransformOperations.FlipVertical());

            MenuItemMetadatas.Add(new MenuItemMetadata() { GuidId = "Rotate", Order = 101, Header = Properties.Resources.Rotate, Icon = MenuItemIcon.TryFindResource("DIRotate") });
            MenuItemMetadatas.Add(new MenuItemMetadata() { OwnerGuid = "Rotate", GuidId = "RotateLeft", Order = 1, Header = Properties.Resources.RotateLeft, Command = RotateLeftCommand, Icon = MenuItemIcon.TryFindResource("DIRotateLeft") });
            MenuItemMetadatas.Add(new MenuItemMetadata() { OwnerGuid = "Rotate", GuidId = "RotateRight", Order = 2, Header = Properties.Resources.RotateRight, Command= RotateRightCommand, Icon = MenuItemIcon.TryFindResource("DIRotateRight") });
            MenuItemMetadatas.Add(new MenuItemMetadata() { OwnerGuid = "Rotate", GuidId = "FlipHorizontal", Order = 3, Header = Properties.Resources.FlipHorizontal, Command = FlipHorizontalCommand, Icon = MenuItemIcon.TryFindResource("DIFlipHorizontal") });
            MenuItemMetadatas.Add(new MenuItemMetadata() { OwnerGuid = "Rotate", GuidId = "FlipVertical", Order = 4, Header = Properties.Resources.FlipVertical, Command = FlipVerticalCommand, Icon = MenuItemIcon.TryFindResource("DIFlipVertical") });

            return MenuItemMetadatas;
        }
    }


}
