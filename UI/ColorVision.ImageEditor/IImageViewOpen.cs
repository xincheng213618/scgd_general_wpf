#pragma warning disable CS8625
using System.Collections.Generic;
using System.Windows.Controls;

namespace ColorVision.ImageEditor
{
    public interface IImageViewOpen
    {
        public List<string> Extension { get; }

        public List<MenuItem> GetContextMenuItems(ImageView imageView);

        public void OpenImage(ImageView imageView, string? filePath);
    }

}
