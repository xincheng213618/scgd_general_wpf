#pragma warning disable CS8625
using ColorVision.UI.Menus;
using System.Collections.Generic;

namespace ColorVision.ImageEditor
{
    public interface IImageOpen
    {
        public List<MenuItemMetadata> GetContextMenuItems(ImageViewConfig imageView);

        public void OpenImage(ImageView imageView, string? filePath);
    }

}
