#pragma warning disable CS8625
using ColorVision.UI.Menus;
using System.Collections.Generic;
using System.Windows.Controls;

namespace ColorVision.ImageEditor
{
    public interface IImageOpen
    {
        public List<string> Extension { get; }

        public List<MenuItemMetadata> GetContextMenuItems(ImageView imageView);

        public void OpenImage(ImageView imageView, string? filePath);
    }

}
